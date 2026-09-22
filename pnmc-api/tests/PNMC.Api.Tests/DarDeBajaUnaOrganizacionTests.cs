using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Dar de baja una organización tiene que cortarle el acceso de verdad, no solo poner una marca.
///
/// <para>
/// <b>DE DÓNDE SALE ESTA PRUEBA.</b> El desarrollo de septiembre dejó escrito, sobre la baja de una
/// cuenta, que dar de baja tiene que hacer <i>algo</i> que el simple guardado no hace: cortar el
/// acceso vivo. Al llevar esa regla a las organizaciones apareció que la comprobación de «esta
/// persona puede actuar por esta organización» estaba escrita <b>ocho veces</b> y <b>cinco copias
/// no miraban si la organización sigue activa</b>: una organización dada de baja seguía pudiendo
/// proponer revisiones por campos, escribir versiones, reclamar procesos ajenos y anunciar eventos.
/// </para>
/// <para>
/// LO QUE ESTAS PRUEBAS IMPIDEN: que la baja vuelva a ser cosmética en cualquiera de esos caminos.
/// </para>
/// </summary>
public sealed class DarDeBajaUnaOrganizacionTests
{
    private const string Clave = "ClaveExterna123";

    [Fact]
    public async Task Una_organizacion_activa_puede_trabajar()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "activa.trabaja@example.com");

        var creado = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", FestivalNuevo());

        // LA LINEA BASE: sin esto, una prueba que solo mire el rechazo pasaría aunque el circuito
        // estuviera roto para todo el mundo.
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
    }

    [Fact]
    public async Task Dada_de_baja_no_puede_crear_festivales()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "baja.festivales@example.com");
        await DarDeBajaAsync(factory, organizacionId);

        var respuesta = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", FestivalNuevo());

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Dada_de_baja_no_puede_anunciar_eventos()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "baja.eventos@example.com");

        // EL FESTIVAL SE CREA Y SE PUBLICA ANTES DE LA BAJA, para que lo único que cambie entre
        // poder y no poder sea la vigencia de la organización.
        var festival = await (await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", FestivalNuevo()))
            .Content.ReadFromJsonAsync<JsonElement>();
        var festivalId = int.Parse(festival.GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        await PublicarAsync(factory, festivalId);

        var antes = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId));
        Assert.Equal(HttpStatusCode.Created, antes.StatusCode);

        await DarDeBajaAsync(factory, organizacionId);

        var despues = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/eventos", EventoNuevo(festivalId, "Otro evento"));

        // ESTE ERA UNO DE LOS CINCO HUECOS: la comprobación miraba el vínculo de la persona y no la
        // vigencia de la organización.
        Assert.Equal(HttpStatusCode.Forbidden, despues.StatusCode);
    }

    [Fact]
    public async Task Dada_de_baja_no_puede_reclamar_procesos_ajenos()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "baja.reclama@example.com");
        await DarDeBajaAsync(factory, organizacionId);

        var respuesta = await EnviarAsync(cliente,
            $"/api/v1/externo/organizaciones/{organizacionId}/reclamaciones-administracion",
            new { festivalId = "1", justificacion = "Queremos administrarlo." });

        // RECLAMAR LA ADMINISTRACION DE OTRO PROCESO desde una organización dada de baja es justo lo
        // contrario de lo que una baja debería permitir.
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Dada_de_baja_no_puede_leer_su_propia_ficha_desde_fuera()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "baja.ficha@example.com");
        await DarDeBajaAsync(factory, organizacionId);

        var respuesta = await cliente.GetAsync($"/api/v1/externo/organizaciones/{organizacionId}/perfil");

        // PARA QUIEN ADMINISTRA, una fila desactivada es una fila que ya no está: 404 y no 403.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reactivarla_le_devuelve_el_acceso()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "reactivada@example.com");
        await DarDeBajaAsync(factory, organizacionId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", FestivalNuevo())).StatusCode);

        var consola = await CmsTestClient.LoginAsync(factory);
        await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new { activa = true });

        // LA BAJA SE DESHACE SIN TOCAR NADA MAS: la sesión de quien administra sigue siendo válida,
        // porque lo que se comprueba en cada petición es la vigencia de la organización y no el
        // testigo. Es la diferencia con la baja de una cuenta, donde el testigo vive por su cuenta.
        var despues = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", FestivalNuevo("Festival tras reactivar"));
        Assert.Equal(HttpStatusCode.Created, despues.StatusCode);
    }

    [Fact]
    public async Task Archivar_la_ficha_no_es_dar_de_baja_la_organizacion()
    {
        await using var factory = new TestWebApplicationFactory();
        var (cliente, organizacionId) = await OrganizacionAsync(factory, "archivada.no.baja@example.com");
        var consola = await CmsTestClient.LoginAsync(factory);

        await consola.PostAsJsonAsync($"/api/v1/admin/organizaciones/{organizacionId}/estado", new
        {
            estado = "archivado",
            motivo = "Su ficha se retira del circuito mientras se revisa.",
        });

        // SON DOS COSAS DISTINTAS Y LA PANTALLA TIENE QUE DECIRLO: el estado dice en qué punto del
        // circuito está su ficha; la vigencia, si la organización sigue operando. Archivar la ficha
        // NO le corta el acceso, y creer lo contrario es el error caro.
        var respuesta = await EnviarAsync(cliente, $"/api/v1/externo/organizaciones/{organizacionId}/festivales", FestivalNuevo());
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    // ---------- Andamio ----------------------------------------------------------

    private static object FestivalNuevo(string nombre = "Festival de la organización") => new
    {
        nombre,
        nivelCobertura = "nacional",
        practicasMusicalesIds = Array.Empty<int>(),
        territoriosSonorosIds = Array.Empty<int>(),
    };

    private static object EventoNuevo(int festivalId, string titulo = "Concierto") => new
    {
        titulo,
        descripcion = "Evento del festival.",
        fechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
        modalidad = "presencial",
        lugar = "Plaza principal",
        festivalId,
    };

    /// <summary>La baja, escrita directamente: lo que se mide es su efecto, no la ruta que la hace.</summary>
    private static async Task DarDeBajaAsync(TestWebApplicationFactory factory, int organizacionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var organizacion = await db.EntityProfiles.SingleAsync(x => x.Id == organizacionId);
        organizacion.IsActive = false;
        await db.SaveChangesAsync();
    }

    private static async Task PublicarAsync(TestWebApplicationFactory factory, int festivalId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PnmcDbContext>();
        var festival = await db.FestivalRecords.SingleAsync(x => x.Id == festivalId);
        festival.StatusCode = "publicado";
        await db.SaveChangesAsync();
    }

    private static async Task<(HttpClient Cliente, int OrganizacionId)> OrganizacionAsync(
        TestWebApplicationFactory factory, string correo)
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/externo/auth/register", new ExternalRegisterRequest
        {
            OrganizationName = "Organización de " + correo,
            FullName = "Responsable de la organización",
            NumeroDocumento = "1020304052",
            HeadquartersDepartmentCode = "05",
            HeadquartersMunicipalityCode = "05001",
            Phone = "3000000000",
            Email = correo,
            Password = Clave,
            PoliticasAceptadas = ["tratamiento", "terminos"],
        })).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/v1/externo/auth/login",
            new ExternalLoginRequest { Email = correo, Password = Clave })).EnsureSuccessStatusCode();

        var mias = await client.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/mis");
        var id = int.Parse(mias[0].GetProperty("id").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        return (client, id);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, string ruta, object cuerpo)
    {
        var testigo = await client.GetFromJsonAsync<JsonElement>("/api/v1/externo/organizaciones/csrf");
        var mensaje = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
        mensaje.Headers.Add("X-CSRF-TOKEN", testigo.GetProperty("requestToken").GetString());
        return await client.SendAsync(mensaje);
    }
}
