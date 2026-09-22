using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Noticias, Agenda y Catálogo Editorial entran a la Importación Asistida.
/// </summary>
/// <remarks>
/// <para>
/// <b>LO QUE ESTO COMPRUEBA, ADEMAS DE QUE FUNCIONA.</b> El Bloque 5b dejó dicho que sumar un
/// dominio cuesta «un fichero de reglas y dos líneas». Estas pruebas recorren el circuito entero de
/// los tres sin que ninguna ruta ni ninguna pantalla haya cambiado, que es la forma de comprobar
/// esa afirmación en vez de repetirla.
/// </para>
/// <para>
/// Las reglas transversales —que ninguno publique solo, que cada uno explique su estado, que los
/// identificadores no se repitan— ya las vigila <c>ImportacionAsistidaTests</c> leyendo los
/// dominios de la inyección de dependencias, así que alcanzan a estos tres sin escribir nada.
/// Aquí va lo que es PROPIO de cada uno.
/// </para>
/// </remarks>
public sealed class ImportacionDeContenidosTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ImportacionDeContenidosTests(TestWebApplicationFactory factory) => _factory = factory;

    private static async Task<HttpClient> ConsolaAsync(TestWebApplicationFactory factory, string dominio)
    {
        var cliente = await CmsTestClient.LoginAsync(factory, withCsrf: false);
        var testigo = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/admin/importaciones/{dominio}/csrf");
        cliente.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN", testigo.GetProperty("token").GetString());
        return cliente;
    }

    private static object Sobre(string dominio, params Dictionary<string, string?>[] filas) => new
    {
        nombreArchivo = $"{dominio}.csv",
        formato = "csv",
        huellaArchivo = new string('c', 64),
        versionContrato = 1,
        filas = filas.Select((valores, indice) => new { numeroFila = indice + 2, valores }).ToArray(),
    };

    [Fact]
    public async Task Una_noticia_importada_nace_en_borrador_y_con_la_direccion_derivada_del_titulo()
    {
        using var cliente = await ConsolaAsync(_factory, "noticias");
        var titulo = $"Convocatoria de bandas {Guid.NewGuid():N}";

        var vista = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/noticias/previsualizar",
            Sobre("noticias", new Dictionary<string, string?>
            {
                ["titulo"] = titulo,
                ["resumen"] = "Se abre la convocatoria para bandas municipales.",
                ["cuerpo"] = "Cuerpo de la noticia.",
                ["fechaPublicacion"] = "2026-10-01",
            }));
        vista.EnsureSuccessStatusCode();
        var previsualizada = await vista.Content.ReadFromJsonAsync<ImportacionDto>();

        Assert.NotNull(previsualizada);
        Assert.Equal(1, previsualizada!.FilasImportables);
        // LA DIRECCION SE CALCULA CON LA MISMA REGLA QUE USA EL FORMULARIO. Otra normalización daría
        // direcciones distintas para el mismo título según por dónde entrara.
        Assert.Equal(ReglasDeNoticias.SlugDesde(titulo), previsualizada.Filas[0].Datos["slug"]);

        var confirmada = await cliente.PostAsJsonAsync(
            $"/api/v1/admin/importaciones/noticias/{previsualizada.Id}/confirmar",
            new { huellaPlan = previsualizada.HuellaPlan, claveIdempotencia = $"not-{Guid.NewGuid():N}", idsFilasExcluidas = Array.Empty<long>() });
        confirmada.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var creada = await db.Noticias.SingleAsync(item => item.Titulo == titulo);

        // UNA NOTICIA PUBLICADA ES TEXTO QUE APARECE EN EL PORTAL CON LA VOZ DEL PROGRAMA. De un
        // archivo de doscientas filas saldrían doscientas publicaciones que nadie leyó.
        Assert.Equal("borrador", creada.Estado);
    }

    [Fact]
    public async Task Una_noticia_con_fecha_ilegible_se_rechaza_en_vez_de_perder_la_fecha_en_silencio()
    {
        using var cliente = await ConsolaAsync(_factory, "noticias");

        var vista = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/noticias/previsualizar",
            Sobre("noticias", new Dictionary<string, string?>
            {
                ["titulo"] = $"Noticia con fecha rara {Guid.NewGuid():N}",
                ["resumen"] = "Resumen.",
                ["fechaPublicacion"] = "el martes que viene",
            }));
        vista.EnsureSuccessStatusCode();
        var previsualizada = await vista.Content.ReadFromJsonAsync<ImportacionDto>();

        // MUTANTE QUE MATA: devolver nulo sin hallazgo. La noticia entraría sin fecha y nadie sabría
        // que el archivo traía una; el hallazgo es lo que permite corregir la hoja antes de confirmar.
        Assert.Equal(0, previsualizada!.FilasImportables);
        Assert.Contains(previsualizada.Filas[0].Hallazgos, h => h.Codigo == "fecha_invalida");
    }

    [Fact]
    public async Task Un_evento_presencial_sin_lugar_se_rechaza_al_planear_y_no_al_escribir()
    {
        using var cliente = await ConsolaAsync(_factory, "agenda");

        var vista = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/agenda/previsualizar",
            Sobre("agenda",
                new Dictionary<string, string?>
                {
                    ["titulo"] = $"Concierto sin lugar {Guid.NewGuid():N}",
                    ["descripcion"] = "Un concierto.",
                    ["fechaInicio"] = "2026-11-20",
                    ["modalidad"] = "presencial",
                },
                new Dictionary<string, string?>
                {
                    ["titulo"] = $"Taller sin enlace {Guid.NewGuid():N}",
                    ["descripcion"] = "Un taller.",
                    ["fechaInicio"] = "2026-11-21",
                    ["modalidad"] = "virtual",
                }));
        vista.EnsureSuccessStatusCode();
        var previsualizada = await vista.Content.ReadFromJsonAsync<ImportacionDto>();

        // LO QUE LA BASE EXIGE, COMPROBADO AL PLANEAR. `CK_EventosAgenda_DatosDeModalidad` rechaza
        // las dos filas; sin esta comprobación, la previsualización diría que todo está bien y el
        // lote reventaría en el `SaveChanges`, a mitad de escribir.
        Assert.Equal(0, previsualizada!.FilasImportables);
        Assert.Contains(previsualizada.Filas[0].Hallazgos, h => h.Codigo == "lugar_obligatorio");
        Assert.Contains(previsualizada.Filas[1].Hallazgos, h => h.Codigo == "enlace_obligatorio");
    }

    [Fact]
    public async Task El_nivel_de_cobertura_de_un_evento_se_deriva_del_territorio_que_trae_la_fila()
    {
        using var cliente = await ConsolaAsync(_factory, "agenda");
        var municipal = $"Evento municipal {Guid.NewGuid():N}";
        var nacional = $"Evento nacional {Guid.NewGuid():N}";

        var vista = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/agenda/previsualizar",
            Sobre("agenda",
                new Dictionary<string, string?>
                {
                    ["titulo"] = municipal,
                    ["descripcion"] = "Con territorio.",
                    ["fechaInicio"] = "2026-11-20",
                    ["modalidad"] = "virtual",
                    ["url"] = "https://ejemplo.test/evento",
                    ["departamento"] = "Antioquia",
                    ["municipio"] = "Medellin",
                },
                new Dictionary<string, string?>
                {
                    ["titulo"] = nacional,
                    ["descripcion"] = "Sin territorio.",
                    ["fechaInicio"] = "2026-11-21",
                    ["modalidad"] = "virtual",
                    ["url"] = "https://ejemplo.test/otro",
                }));
        vista.EnsureSuccessStatusCode();
        var previsualizada = await vista.Content.ReadFromJsonAsync<ImportacionDto>();
        Assert.Equal(2, previsualizada!.FilasImportables);

        var confirmada = await cliente.PostAsJsonAsync(
            $"/api/v1/admin/importaciones/agenda/{previsualizada.Id}/confirmar",
            new { huellaPlan = previsualizada.HuellaPlan, claveIdempotencia = $"age-{Guid.NewGuid():N}", idsFilasExcluidas = Array.Empty<long>() });
        confirmada.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // MUTANTE QUE MATA: pedir el nivel como columna. La hoja podría decir «municipal» sin
        // municipio, y `CK_EventosAgenda_NivelCobertura` rechaza esa combinación.
        var conTerritorio = await db.EventosAgenda.SingleAsync(item => item.Titulo == municipal);
        Assert.Equal("municipal", conTerritorio.NivelCobertura);
        Assert.Equal("05001", conTerritorio.CodigoMunicipio);
        Assert.Equal("borrador", conTerritorio.Estado);

        var sinTerritorio = await db.EventosAgenda.SingleAsync(item => item.Titulo == nacional);
        Assert.Equal("nacional", sinTerritorio.NivelCobertura);
        Assert.Null(sinTerritorio.CodigoDepartamento);
    }

    [Fact]
    public async Task Una_publicacion_editorial_exige_su_codigo_y_nace_sin_derechos_concedidos()
    {
        using var cliente = await ConsolaAsync(_factory, "catalogo-editorial");
        var codigo = $"PNMC-{Guid.NewGuid():N}"[..20];

        var sinCodigo = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/catalogo-editorial/previsualizar",
            Sobre("catalogo-editorial", new Dictionary<string, string?>
            {
                ["titulo"] = "Cancionero sin signatura",
            }));
        sinCodigo.EnsureSuccessStatusCode();
        var rechazada = await sinCodigo.Content.ReadFromJsonAsync<ImportacionDto>();

        // NO SE DERIVA DEL TITULO, a diferencia de la dirección de una noticia: es la signatura que
        // asigna quien cataloga, e inventarla produciría signaturas falsas que corregir a mano.
        Assert.Equal(0, rechazada!.FilasImportables);
        Assert.Contains(rechazada.Filas[0].Hallazgos, h => h.Codigo == "codigo_obligatorio");

        var vista = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/catalogo-editorial/previsualizar",
            Sobre("catalogo-editorial", new Dictionary<string, string?>
            {
                ["codigo"] = codigo,
                ["titulo"] = "Cancionero de la región andina",
                ["anioInicio"] = "1998",
                ["paginas"] = "48 p.",
            }));
        vista.EnsureSuccessStatusCode();
        var previsualizada = await vista.Content.ReadFromJsonAsync<ImportacionDto>();
        Assert.Equal(1, previsualizada!.FilasImportables);

        var confirmada = await cliente.PostAsJsonAsync(
            $"/api/v1/admin/importaciones/catalogo-editorial/{previsualizada.Id}/confirmar",
            new { huellaPlan = previsualizada.HuellaPlan, claveIdempotencia = $"edi-{Guid.NewGuid():N}", idsFilasExcluidas = Array.Empty<long>() });
        confirmada.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var creada = await db.PublicacionesEditoriales.SingleAsync(item => item.Codigo == codigo);

        // LOS DOS ESTADOS, CADA UNO EN SU VALOR MAS PRUDENTE, y los derechos SIN conceder: importar
        // una ficha no dice nada sobre quién puede publicar su archivo.
        Assert.Equal("borrador", creada.EstadoPublicacion);
        Assert.Equal("pendiente_revision", creada.EstadoCatalogacion);
        Assert.Equal("pendiente", creada.DerechosEstado);
        Assert.False(creada.DerechosPermitePublicarFicha);
        Assert.False(creada.DerechosPermitePublicarArchivo);
        Assert.Equal(1998, creada.AnioInicio);
        // TEXTO Y NO NUMERO: la fuente trae «48 p.», «2 v.». Normalizar es otra decisión.
        Assert.Equal("48 p.", creada.Paginas);
    }

    [Fact]
    public async Task Los_cinco_dominios_se_anuncian_en_el_registro_que_lee_la_consola()
    {
        using var consola = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var respuesta = await consola.GetFromJsonAsync<JsonElement>("/api/v1/admin/importaciones");

        var dominios = respuesta.GetProperty("dominios").EnumerateArray()
            .Select(item => item.GetProperty("dominio").GetString())
            .ToList();

        // NI LA PANTALLA NI LAS RUTAS CAMBIARON para sumar tres destinos: la consola pregunta qué
        // se puede importar y el servidor responde. Es lo que el Bloque 5b afirmó y esto comprueba.
        Assert.Contains("festivales", dominios);
        Assert.Contains("organizaciones", dominios);
        Assert.Contains("noticias", dominios);
        Assert.Contains("agenda", dominios);
        Assert.Contains("catalogo-editorial", dominios);
    }
}
