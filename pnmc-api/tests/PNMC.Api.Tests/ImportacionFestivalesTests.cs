using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

public sealed class ImportacionFestivalesTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ImportacionFestivalesTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Todas_las_rutas_exigen_sesion_institucional()
    {
        using var cliente = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/v1/admin/importaciones/festivales/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/v1/admin/importaciones/festivales/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/festivales/previsualizar", Solicitud("Sin sesión"))).StatusCode);
    }

    [Fact]
    public async Task Previsualizar_exige_antiforgery_y_no_escribe_festivales()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var antes = await ContarFestivalesAsync();

        var sinToken = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/festivales/previsualizar", Solicitud("Festival sin token"));
        Assert.Equal(HttpStatusCode.BadRequest, sinToken.StatusCode);

        await PrepararCsrfAsync(cliente);
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/festivales/previsualizar", Solicitud("Festival previsualizado"));
        respuesta.EnsureSuccessStatusCode();
        var informe = await respuesta.Content.ReadFromJsonAsync<ImportacionDto>();

        Assert.NotNull(informe);
        Assert.Equal("previsualizado", informe.Estado);
        Assert.Equal(1, informe.FilasImportables);
        Assert.Equal(0, informe.FilasRechazadas);
        Assert.Equal(antes, await ContarFestivalesAsync());
        Assert.Equal("Antioquia", informe.Filas[0].Datos["departamento"]);
        Assert.Equal("Medellin", informe.Filas[0].Datos["municipio"]);
        Assert.Equal("contacto@example.org", informe.Filas[0].Datos["correoContacto"]);
    }

    [Fact]
    public async Task La_previsualizacion_nombra_coincidencias_y_errores_sin_importarlos()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        await PrepararCsrfAsync(cliente);
        var solicitud = Solicitud("Festival Test");
        solicitud.Filas =
        [
            solicitud.Filas![0],
            Fila(3, ("nombre", "Sin territorio"), ("nivelCobertura", "municipal"),
                ("departamento", "Inventado"), ("municipio", "Ninguno")),
        ];

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/festivales/previsualizar", solicitud);
        respuesta.EnsureSuccessStatusCode();
        var informe = await respuesta.Content.ReadFromJsonAsync<ImportacionDto>();

        Assert.NotNull(informe);
        Assert.Equal(0, informe.FilasImportables);
        Assert.Equal(2, informe.FilasRechazadas);
        Assert.Contains(informe.Filas[0].Hallazgos, item => item.Codigo == "festival_existente");
        Assert.Contains(informe.Filas[1].Hallazgos, item => item.Codigo == "departamento_invalido");
        Assert.Contains(informe.Filas[1].Hallazgos, item => item.Codigo == "municipio_invalido");
    }

    [Fact]
    public async Task Confirmar_crea_borrador_institucional_y_el_reintento_no_duplica()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        await PrepararCsrfAsync(cliente);
        var nombre = $"Festival importado {Guid.NewGuid():N}";
        var vista = await PrevisualizarAsync(cliente, Solicitud(nombre));
        var confirmacion = new ConfirmarImportacionSolicitud
        {
            HuellaPlan = vista.HuellaPlan,
            ClaveIdempotencia = $"prueba-{Guid.NewGuid():N}",
            IdsFilasExcluidas = [],
        };

        var primera = await cliente.PostAsJsonAsync($"/api/v1/admin/importaciones/festivales/{vista.Id}/confirmar", confirmacion);
        primera.EnsureSuccessStatusCode();
        var aplicada = await primera.Content.ReadFromJsonAsync<ImportacionDto>();
        var segunda = await cliente.PostAsJsonAsync($"/api/v1/admin/importaciones/festivales/{vista.Id}/confirmar", confirmacion);
        segunda.EnsureSuccessStatusCode();

        Assert.NotNull(aplicada);
        Assert.Equal("aplicado", aplicada.Estado);
        Assert.Equal(1, aplicada.FilasAplicadas);
        Assert.NotNull(aplicada.Filas[0].RegistroCreadoId);
        Assert.Equal("datos_duplicados_minimizados", aplicada.EstadoRetencion);
        Assert.Null(aplicada.Filas[0].Datos["correoContacto"]);
        Assert.Null(aplicada.Filas[0].Datos["telefonoContacto"]);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var creados = await db.FestivalRecords.Where(item => item.Name == nombre).ToListAsync();
        var organizacionInstitucional = await db.EntityProfiles.SingleAsync(item => item.IsInstitutional);
        Assert.Single(creados);
        Assert.Equal("borrador", creados[0].StatusCode);
        Assert.Equal("contacto@example.org", creados[0].ContactEmail);
        Assert.Equal(organizacionInstitucional.Id, creados[0].OrganizacionPrincipalId);
        Assert.Single(await db.DecisionesImportacion.Where(item => item.LoteImportacionId == vista.Id && item.Decision == "importar").ToListAsync());
        Assert.Contains(await db.AuditLogs.Where(item => item.TableName == "Festivales").ToListAsync(), item => item.RecordId == creados[0].Id.ToString());
    }

    [Fact]
    public async Task El_historial_es_paginado_filtrable_y_declara_quien_puede_confirmar()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        await PrepararCsrfAsync(cliente);
        var marca = $"historial-{Guid.NewGuid():N}";
        var solicitud = Solicitud($"Festival {marca}");
        solicitud.NombreArchivo = $"{marca}.csv";
        var vista = await PrevisualizarAsync(cliente, solicitud);

        var pagina = await cliente.GetFromJsonAsync<PaginaDeImportacionesDto>(
            $"/api/v1/admin/importaciones/festivales/?estado=previsualizado&archivo={marca}&pagina=1&tamano=10");
        var detalle = await cliente.GetFromJsonAsync<ImportacionDto>(
            $"/api/v1/admin/importaciones/festivales/{vista.Id}");

        Assert.NotNull(pagina);
        Assert.Single(pagina.Items);
        Assert.Equal(30, pagina.DiasRetencionPrevisualizacion);
        Assert.Equal(vista.Id, pagina.Items[0].Id);
        Assert.True(pagina.Items[0].EsPropio);
        Assert.True(pagina.Items[0].PuedeConfirmar);
        Assert.NotNull(pagina.Items[0].FechaExpiracion);
        Assert.NotNull(detalle);
        Assert.True(detalle.PuedeConfirmar);
        Assert.Equal("detalle_temporal", detalle.EstadoRetencion);

        using var otroFuncionario = await CmsTestClient.LoginAsync(
            _factory,
            "gestor@pnmc.local",
            "pnmc-gestor",
            withCsrf: false);
        await PrepararCsrfAsync(otroFuncionario);
        var detalleAjeno = await otroFuncionario.GetFromJsonAsync<ImportacionDto>(
            $"/api/v1/admin/importaciones/festivales/{vista.Id}");
        Assert.NotNull(detalleAjeno);
        Assert.False(detalleAjeno.EsPropio);
        Assert.False(detalleAjeno.PuedeConfirmar);
        var confirmacionAjena = await otroFuncionario.PostAsJsonAsync(
            $"/api/v1/admin/importaciones/festivales/{vista.Id}/confirmar",
            new ConfirmarImportacionSolicitud
            {
                HuellaPlan = vista.HuellaPlan,
                ClaveIdempotencia = $"ajena-{Guid.NewGuid():N}",
            });
        Assert.Equal(HttpStatusCode.Forbidden, confirmacionAjena.StatusCode);
    }

    [Fact]
    public async Task Una_exclusion_humana_queda_registrada_y_no_crea_su_fila()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        await PrepararCsrfAsync(cliente);
        var solicitud = Solicitud($"Festival elegido {Guid.NewGuid():N}");
        solicitud.Filas =
        [
            solicitud.Filas![0],
            Fila(3,
                ("nombre", $"Festival excluido {Guid.NewGuid():N}"),
                ("nivelCobertura", "departamental"),
                ("departamento", "Antioquia")),
        ];
        var vista = await PrevisualizarAsync(cliente, solicitud);
        var excluida = vista.Filas.Single(item => item.NumeroFila == 3);

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/admin/importaciones/festivales/{vista.Id}/confirmar", new ConfirmarImportacionSolicitud
        {
            HuellaPlan = vista.HuellaPlan,
            ClaveIdempotencia = $"prueba-{Guid.NewGuid():N}",
            IdsFilasExcluidas = [excluida.Id],
        });
        respuesta.EnsureSuccessStatusCode();
        var aplicada = await respuesta.Content.ReadFromJsonAsync<ImportacionDto>();

        Assert.NotNull(aplicada);
        Assert.Equal(1, aplicada.FilasAplicadas);
        Assert.Equal(1, aplicada.FilasExcluidas);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        Assert.Equal("excluir", (await db.DecisionesImportacion.SingleAsync(item => item.FilaImportacionId == excluida.Id)).Decision);
        Assert.Null((await db.FilasImportacion.SingleAsync(item => item.Id == excluida.Id)).RegistroCreadoId);
    }

    [Fact]
    public async Task Una_huella_de_plan_distinta_responde_conflicto()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        await PrepararCsrfAsync(cliente);
        var vista = await PrevisualizarAsync(cliente, Solicitud($"Festival conflicto {Guid.NewGuid():N}"));

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/admin/importaciones/festivales/{vista.Id}/confirmar", new ConfirmarImportacionSolicitud
        {
            HuellaPlan = new string('b', 64),
            ClaveIdempotencia = $"prueba-{Guid.NewGuid():N}",
        });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    private static PrevisualizarImportacionSolicitud Solicitud(string nombre) => new()
    {
        NombreArchivo = "festivales.csv",
        Formato = "csv",
        HuellaArchivo = new string('a', 64),
        VersionContrato = 1,
        Filas =
        [
            Fila(2,
                ("nombre", nombre),
                ("descripcion", "Fila de prueba"),
                ("nivelCobertura", "Municipal"),
                ("departamento", "Antioquia"),
                ("municipio", "Medellin"),
                ("periodicidad", "Anual"),
                ("correoContacto", " CONTACTO @Example.org ")),
        ],
    };

    /// <summary>Una fila del archivo, con los campos que el dominio de Festivales declara.</summary>
    /// <remarks>
    /// EL CONTRATO ES UN DICCIONARIO DESDE EL 12 DE SEPTIEMBRE DE 2026, y por eso las pruebas
    /// construyen la fila con pares campo/valor: importar cualquier otra cosa dejó de exigir otro
    /// contrato, otro endpoint y otra pantalla.
    /// </remarks>
    internal static FilaDeArchivoDto Fila(int numero, params (string Campo, string? Valor)[] valores) => new()
    {
        NumeroFila = numero,
        Valores = valores.ToDictionary(par => par.Campo, par => par.Valor, StringComparer.Ordinal),
    };

    private static async Task PrepararCsrfAsync(HttpClient cliente)
    {
        var token = await cliente.GetFromJsonAsync<TokenDto>("/api/v1/admin/importaciones/festivales/csrf");
        Assert.NotNull(token);
        cliente.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.Token);
    }

    private static async Task<ImportacionDto> PrevisualizarAsync(HttpClient cliente, PrevisualizarImportacionSolicitud solicitud)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/festivales/previsualizar", solicitud);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ImportacionDto>())!;
    }

    private async Task<int> ContarFestivalesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PnmcDbContext>().FestivalRecords.CountAsync();
    }

    private sealed record TokenDto(string Token);
}
