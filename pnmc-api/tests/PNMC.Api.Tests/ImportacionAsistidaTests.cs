using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.ImportacionAsistida;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// La Importación Asistida como capacidad, no como pantalla de un módulo.
/// </summary>
/// <remarks>
/// <para>
/// <b>LO QUE ESTAS PRUEBAS IMPIDEN.</b> Que un dominio nuevo —o un cambio descuidado en el que ya
/// existe— haga que una carga aparezca publicada sin que nadie lo decidiera; y que la consola vuelva
/// a llevar escrita una lista de destinos que el servidor no sabe recibir.
/// </para>
/// <para>
/// <b>POR QUE NO BASTA LA PRUEBA DEL DOMINIO.</b> `ImportacionFestivalesTests` comprueba que un
/// Festival importado nace en borrador, que es cierto y es suficiente para Festivales. Esta
/// comprueba la <b>regla</b>: cualquier dominio que se declare mañana la cumple, aunque nadie se
/// acuerde de escribirle su propia prueba.
/// </para>
/// </remarks>
public sealed class ImportacionAsistidaTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ImportacionAsistidaTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>Los dominios registrados, tal como los ve el servidor al arrancar.</summary>
    /// <remarks>
    /// SE LEEN DE LA INYECCION DE DEPENDENCIAS y no de una lista estática: es donde se declaran de
    /// verdad desde, así que un dominio registrado y olvidado en una
    /// lista paralela no podría escaparse de estas comprobaciones.
    /// </remarks>
    private List<CapacidadDeImportacion> Capacidades()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetServices<IDominioDeImportacion>()
            .Select(dominio => dominio.Capacidad)
            .ToList();
    }

    [Fact]
    public void Ningun_dominio_importa_a_un_estado_que_ya_es_publico()
    {
        var capacidades = Capacidades();
        Assert.NotEmpty(capacidades);

        foreach (var capacidad in capacidades)
        {
            // LA REGLA DOS DE LA CAPACIDAD: nada importado se publica ni se activa solo. Escrita
            // como una asignación dentro de un endpoint, bastaba con que alguien la cambiara para
            // que una carga entera apareciera en la calle; escrita aquí, no se puede cambiar sin
            // que esto se ponga en rojo.
            Assert.False(
                ReglasDeImportacion.PublicaSinDecision(capacidad.EstadoAlImportar),
                $"El dominio «{capacidad.ModuloId}» importa directamente a «{capacidad.EstadoAlImportar}», "
                + "que deja el registro a la vista sin que nadie lo decida.");
        }
    }

    [Fact]
    public void Cada_dominio_explica_por_que_nace_en_ese_estado()
    {
        foreach (var capacidad in Capacidades())
        {
            // NO ES DOCUMENTACION DE CORTESIA: es lo que impide que alguien «suba» este estado
            // dentro de un año sin enterarse de lo que estaba protegiendo.
            Assert.False(string.IsNullOrWhiteSpace(capacidad.PorQueEseEstado));
            Assert.NotEmpty(capacidad.Campos);
            Assert.False(string.IsNullOrWhiteSpace(capacidad.Etiqueta));
        }
    }

    [Fact]
    public void Los_identificadores_de_dominio_no_se_repiten()
    {
        var dominios = Capacidades().Select(item => item.ModuloId).ToList();

        // EL IDENTIFICADOR VIAJA EN LA RUTA y separa los lotes en la tabla: repetido, un dominio
        // leería los lotes del otro.
        Assert.Equal(dominios.Count, dominios.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Un_segundo_dominio_recorre_el_mismo_circuito_sin_una_linea_propia_de_endpoint()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var testigo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/admin/importaciones/organizaciones/csrf");
        cliente.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        cliente.DefaultRequestHeaders.Add("X-CSRF-TOKEN", testigo.GetProperty("token").GetString());

        var nombre = $"Fundación importada {Guid.NewGuid():N}";
        var vista = await cliente.PostAsJsonAsync("/api/v1/admin/importaciones/organizaciones/previsualizar", new
        {
            nombreArchivo = "organizaciones.csv",
            formato = "csv",
            huellaArchivo = new string('b', 64),
            versionContrato = 1,
            filas = new[]
            {
                new
                {
                    numeroFila = 2,
                    valores = new Dictionary<string, string?>
                    {
                        ["nombre"] = nombre,
                        ["identificacion"] = "900.123.456-7",
                        ["departamentoSede"] = "Antioquia",
                        ["municipioSede"] = "Medellin",
                        ["correoContacto"] = " CONTACTO@Fundacion.ORG ",
                    },
                },
            },
        });
        vista.EnsureSuccessStatusCode();
        var previsualizada = await vista.Content.ReadFromJsonAsync<ImportacionDto>();

        Assert.NotNull(previsualizada);
        Assert.Equal("organizaciones", previsualizada.Dominio);
        Assert.Equal(1, previsualizada.FilasImportables);
        // EL NUCLEO NO SUPO NADA DE ORGANIZACIONES: el sobre, la huella, el lote, las filas y la
        // respuesta son los mismos que los de Festivales. Lo único propio es este fichero de reglas.
        Assert.Equal("contacto@fundacion.org", previsualizada.Filas[0].Datos["correoContacto"]);
        Assert.Contains("Nunca publica", previsualizada.Aviso, StringComparison.Ordinal);
        Assert.Contains("pendiente_de_confirmacion", previsualizada.Aviso, StringComparison.Ordinal);

        var confirmada = await cliente.PostAsJsonAsync(
            $"/api/v1/admin/importaciones/organizaciones/{previsualizada.Id}/confirmar",
            new
            {
                huellaPlan = previsualizada.HuellaPlan,
                claveIdempotencia = $"org-{Guid.NewGuid():N}",
                idsFilasExcluidas = Array.Empty<long>(),
            });
        confirmada.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var creada = await db.EntityProfiles.SingleAsync(item => item.Name == nombre);

        // LA REGLA DEL BLOQUE DE CORREO, AHORA TAMBIEN POR ARCHIVO: una organización importada nace
        // pendiente de confirmación. De un archivo de doscientas filas saldrían doscientas
        // afirmaciones falsas si naciera activa.
        Assert.Equal("pendiente_de_confirmacion", creada.StatusCode);
        Assert.False(creada.IsInstitutional);
    }

    [Fact]
    public async Task Un_dominio_que_no_existe_responde_404_y_no_una_lista_vacia()
    {
        using var cliente = await CmsTestClient.LoginAsync(_factory, withCsrf: false);

        // UNA LISTA VACIA HARIA CREER QUE ESE DESTINO EXISTE Y NO TIENE NADA, que es lo contrario de
        // lo que pasa: no hay quien sepa recibirlo.
        Assert.Equal(HttpStatusCode.NotFound,
            (await cliente.GetAsync("/api/v1/admin/importaciones/escuelas/")).StatusCode);
    }

    [Fact]
    public async Task La_consola_pregunta_al_servidor_que_se_puede_importar()
    {
        using var anonimo = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/v1/admin/importaciones")).StatusCode);

        using var consola = await CmsTestClient.LoginAsync(_factory, withCsrf: false);
        var respuesta = await consola.GetFromJsonAsync<JsonElement>("/api/v1/admin/importaciones");

        var dominios = respuesta.GetProperty("dominios").EnumerateArray().ToList();
        Assert.Equal(Capacidades().Count, dominios.Count);
        Assert.Contains(dominios, item => item.GetProperty("dominio").GetString() == "festivales");
        // SE DICE TAMBIEN EN QUE ESTADO NACE LO IMPORTADO: la pantalla puede avisarlo antes de que
        // alguien suba un archivo, en vez de descubrirlo al ver la lista de resultados.
        Assert.Contains(dominios, item => item.GetProperty("estadoAlImportar").GetString() == "borrador");
    }
}
