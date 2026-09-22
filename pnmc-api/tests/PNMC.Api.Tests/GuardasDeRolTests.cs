using System.Net;
using System.Net.Http.Json;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-063 — Sesion de consola valida + rol insuficiente = 403. El vehiculo es
/// <c>gestor_interno</c>.
/// </summary>
/// <remarks>
/// <para>
/// DE DONDE VIENE ESTE FICHERO. Hasta esta clase de prueba viajaba
/// sobre <c>aliado.admin@pnmc.local</c>: era el unico usuario del arnes que conseguia cookie
/// institucional sin pertenecer a ninguna lista blanca interna, porque la puerta era una lista
/// negra de un solo elemento. Al retirar el concepto de entidad aliada, ese vehiculo
/// desaparecio, y con el habrian desaparecido —en silencio, con la suite en verde— las unicas
/// pruebas de todos los guardas de rol del CMS y de la gestion de usuarios.
/// </para>
/// <para>
/// EL VEHICULO NUEVO NO ES UN INVENTO. <c>gestor_interno</c> es el rol «Funcionario» del modelo
/// aprobado: entra por la puerta institucional con pleno derecho y aun asi no alcanza lo que
/// esta reservado al webmaster —publicar al sitio publico, importar el CMS entero, gestionar
/// usuarios—. Es un caso real del producto, no un rol de laboratorio.
/// </para>
/// <para>
/// LA LECCION DE PNMC-011, TRASPLANTADA. Aquel hallazgo fue un
/// <c>if (environment.IsEnvironment("Test")) { return true; }</c> dentro de un guarda: toda
/// prueba que se quedara en el 401 del grupo seguia en verde sobre una puerta abierta. Por eso
/// aqui <b>cada 403 va acompañado de un control positivo</b>: el mismo <c>gestor_interno</c>
/// llegando a una ruta que si le corresponde. Sin ese par, un 403 podria venir de que la
/// persona no entro, y la prueba estaria midiendo otra cosa.
/// </para>
/// <para>
/// LO QUE ESTE FICHERO NO PUEDE CUBRIR, dicho en voz alta: los guardas cuya lista es
/// <c>[webmaster, gestor_interno]</c> ya no tienen ningun rol con sesion valida al que negarle
/// el paso, porque el catalogo de plataforma tiene tres nombres y el tercero no cruza la
/// puerta. Esos guardas los vigila
/// <see cref="PuertaInstitucionalTests.Ninguna_lista_de_roles_del_api_admite_a_alguien_de_fuera_del_ministerio"/>,
/// que comprueba por reflexion que ninguna lista se amplie hacia fuera del Ministerio.
/// </para>
/// </remarks>
public sealed class GuardasDeRolTests
{
    private const string CorreoGestor = "gestor@pnmc.local";
    private const string ClaveGestor = "pnmc-gestor";

    // ---------- Gestion de usuarios: solo el webmaster ----------

    [Fact]
    public async Task Un_gestor_interno_no_gestiona_usuarios_institucionales()
    {
        using var factory = new TestWebApplicationFactory();
        var gestor = await CmsTestClient.LoginAsync(factory, CorreoGestor, ClaveGestor, withCsrf: false);

        // CONTROL POSITIVO PRIMERO. Si esto fallara, los 403 de abajo vendrian de no tener
        // sesion y esta prueba no estaria midiendo el rol.
        Assert.Equal(HttpStatusCode.OK, (await gestor.GetAsync("/api/v1/admin/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await gestor.GetAsync("/api/v1/admin/auth/users")).StatusCode);

        var alta = await gestor.PostAsJsonAsync("/api/v1/admin/auth/users", new
        {
            fullName = "Cuenta Que No Deberia Nacer",
            email = "no.deberia@pnmc.local",
            role = "webmaster",
            password = "pnmc-lo-que-sea",
        });
        Assert.Equal(HttpStatusCode.Forbidden, alta.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await gestor.DeleteAsync("/api/v1/admin/auth/users/1")).StatusCode);
    }

    /// <summary>
    /// Y el webmaster si. Control positivo del bloque anterior.
    /// </summary>
    [Fact]
    public async Task Un_webmaster_si_gestiona_usuarios_institucionales()
    {
        using var factory = new TestWebApplicationFactory();
        var webmaster = await CmsTestClient.LoginAsync(factory, withCsrf: false);

        Assert.Equal(HttpStatusCode.OK,
            (await webmaster.GetAsync("/api/v1/admin/auth/users")).StatusCode);
    }

    // Los guardas del CMS (publicar, retirar, importar, nomina) se prueban con el mismo
    // vehiculo en el fichero de cada funcionalidad: GuardasDelCmsTests, ImportacionDeContenidoWebTests y
    // EquipoWebTests. Aqui no se repiten; lo que vive aqui es el modelo de roles en si.

    // ---------- El circuito institucional: quien decide sobre un Festival ----------

    /// <summary>
    /// Un funcionario llega a la bandeja de revision. Control positivo de
    /// <c>Permisos.PoliticaFuncionario</c>.
    /// </summary>
    /// <remarks>
    /// Sin esta prueba, una politica mal escrita que cerrara la puerta a todo el mundo dejaria
    /// en verde cualquier aserto de 403 mientras rompe la consola entera. Es el mismo control
    /// que protegia al circuito cuando el atacante hipotetico era un aliado.
    /// </remarks>
    [Theory]
    [InlineData("test@pnmc.local", "pnmc-master")]
    [InlineData(CorreoGestor, ClaveGestor)]
    public async Task Los_dos_roles_internos_llegan_a_la_bandeja_de_revision(string correo, string clave)
    {
        using var factory = new TestWebApplicationFactory();
        var cliente = await CmsTestClient.LoginAsync(factory, correo, clave, withCsrf: false);

        Assert.Equal(HttpStatusCode.OK,
            (await cliente.GetAsync("/api/v1/institucional/festivales/en-revision")).StatusCode);
    }

    /// <summary>
    /// Y quien no es del Ministerio no llega, ni siquiera a intentarlo: se queda en la puerta.
    /// </summary>
    /// <remarks>
    /// El aserto es 401 y no 403 <b>a proposito</b>. Desde que la puerta institucional es lista
    /// blanca, una persona del ecosistema no obtiene cookie de consola, asi que el circuito ni
    /// se entera de que existe. Si algun dia esto empieza a devolver 403, significa que alguien
    /// volvio a dejar entrar a un rol no interno y que la unica defensa que queda es la
    /// politica: hay que averiguar por que antes de cambiar el numero.
    /// </remarks>
    [Fact]
    public async Task Una_persona_del_ecosistema_no_llega_al_circuito_institucional()
    {
        using var factory = new TestWebApplicationFactory();
        var cliente = factory.CreateClient();

        var acceso = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = "externo@pnmc.local", Password = "pnmc-externo" });
        Assert.Equal(HttpStatusCode.Unauthorized, acceso.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.GetAsync("/api/v1/institucional/festivales/en-revision")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.PostAsJsonAsync("/api/v1/institucional/festivales/1/decisiones", new { accion = "Publicar" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await cliente.PostAsJsonAsync("/api/v1/institucional/festivales/normalizar-versiones-historicas", new { })).StatusCode);
    }
}
