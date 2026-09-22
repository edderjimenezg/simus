using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Security;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-062 — La puerta institucional es una lista blanca, y las tres listas de roles del
/// sistema salen de una sola.
/// </summary>
/// <remarks>
/// <para>
/// QUE SE ROMPIO Y POR QUE ESTA PRUEBA NO EXISTIA ANTES. Hasta el
/// inicio de sesion institucional decia literalmente
/// <c>if (CleanRoleName(role.Name) == "externo") return Results.Unauthorized();</c>: una
/// <b>lista negra de un solo elemento</b>. Con seis roles en el catalogo eso coincidia por
/// accidente con «admite a los internos», pero admitia tambien a <c>aliado_admin</c>,
/// <c>aliado_editor</c> y <c>aliado_lector</c>, que no eran internos —y habria admitido a
/// cualquier rol futuro que nadie recordara añadir a la lista negra—.
/// </para>
/// <para>
/// Sumado a que el circuito de revision institucional no comprobaba rol en ninguna de sus 554
/// lineas, un administrador de entidad aliada podia publicar o rechazar cualquier Festival del
/// pais. El concepto de entidad aliada se retiro ese mismo dia; la lista negra, que es la
/// causa reutilizable, se convirtio en lista blanca.
/// </para>
/// <para>
/// LA PRUEBA QUE IMPORTA es la del rol desconocido. Un rol que nadie declaro no es una
/// hipotesis: es lo que fueron los tres <c>aliado_*</c> durante meses. Una lista negra no
/// puede verlo; una lista blanca lo rechaza sin que nadie tenga que acordarse.
/// </para>
/// </remarks>
public sealed class PuertaInstitucionalTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly string[] RolesDePlataformaEsperados = ["externo", "gestor_interno", "webmaster"];
    private static readonly string[] RolesInternosEsperados = ["gestor_interno", "webmaster"];

    /// <summary>
    /// Cuantas listas de roles <c>string[]</c> declaran hoy los endpoints del API.
    /// </summary>
    /// <remarks>
    /// Ocho: <c>EditorRoles</c> y <c>PublisherRoles</c> de <c>ContenidoWebEndpoints</c>, los dos
    /// mismos de <c>EquipoWebEndpoints</c>, <c>EditorRoles</c> e <c>ImporterRoles</c> de
    /// <c>ImportacionDeContenidoWebEndpoints</c>, y los dos de <c>ImagenesWebEndpoints</c> (29 ago 2026), que
    /// son <c>[webmaster, gestor_interno]</c> para subir y <c>[webmaster]</c> para cambiar lo que
    /// ve el visitante: los mismos que los textos y por el mismo motivo. Quedan fuera, por no
    /// seguir la convencion de nombre,
    /// cuatro decisiones de rol escritas en linea (organizaciones institucionales,
    /// <c>NotificationEndpoints</c> y dos de <c>ExternalAuthEndpoints</c>): conviene unificarlas
    /// contra <c>Permisos</c>, y mientras tanto esta cifra deja constancia de que el barrido no
    /// las ve.
    /// </remarks>
    private const int ListasDeRolesDeclaradas = 8;

    private readonly TestWebApplicationFactory _factory;

    public PuertaInstitucionalTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("test@pnmc.local", "pnmc-master", "webmaster")]
    [InlineData("gestor@pnmc.local", "pnmc-gestor", "gestor_interno")]
    public async Task Un_rol_interno_cruza_la_puerta_y_recibe_cookie_de_consola(
        string correo, string clave, string rolEsperado)
    {
        var cliente = _factory.CreateClient();

        var acceso = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = correo, Password = clave });

        Assert.Equal(HttpStatusCode.OK, acceso.StatusCode);
        var sesion = await acceso.Content.ReadFromJsonAsync<AdminAuthResponse>();
        Assert.Equal(rolEsperado, sesion!.User.Role);

        // Y la cookie sirve de verdad: sin esta segunda llamada, la prueba solo diria que el
        // login devolvio 200, no que dejo sesion utilizable.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/admin/auth/me")).StatusCode);
    }

    /// <summary>
    /// El caso que la lista negra ya cubria. Se conserva porque es el mas frecuente.
    /// </summary>
    [Fact]
    public async Task Una_persona_del_ecosistema_no_cruza_la_puerta_institucional()
    {
        var cliente = _factory.CreateClient();

        var acceso = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = "externo@pnmc.local", Password = "pnmc-externo" });

        Assert.Equal(HttpStatusCode.Unauthorized, acceso.StatusCode);
        await NoDejoSesionAsync(cliente);
    }

    /// <summary>
    /// El caso que la lista negra NO podia ver, y que es la razon de este fichero.
    /// </summary>
    /// <remarks>
    /// Las credenciales son correctas: el 401 viene del rol, no de la contraseña. Si alguien
    /// devuelve la puerta a una lista negra, esta prueba se pone en rojo y ninguna otra lo
    /// hace.
    /// </remarks>
    [Fact]
    public async Task Un_rol_que_nadie_declaro_no_cruza_la_puerta_institucional()
    {
        var cliente = _factory.CreateClient();

        var acceso = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = "desconocido@pnmc.local", Password = "pnmc-desconocido" });

        Assert.Equal(HttpStatusCode.Unauthorized, acceso.StatusCode);
        await NoDejoSesionAsync(cliente);
    }

    /// <summary>
    /// Las credenciales malas siguen dando 401 por su propio motivo. Es el control negativo:
    /// sin el, los dos asertos de arriba podrian estar pasando por la razon equivocada.
    /// </summary>
    [Fact]
    public async Task Una_clave_incorrecta_de_un_rol_interno_tambien_es_401()
    {
        var cliente = _factory.CreateClient();

        var acceso = await cliente.PostAsJsonAsync("/api/v1/admin/auth/login",
            new AdminLoginRequest { Email = "gestor@pnmc.local", Password = "la-que-no-es" });

        Assert.Equal(HttpStatusCode.Unauthorized, acceso.StatusCode);
    }

    /// <summary>
    /// Las tres decisiones de rol del sistema leen la MISMA lista.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Antes de la retirada de aliados habia tres listas escritas por separado —la puerta
    /// institucional, <c>PoliticaFuncionario</c> y <c>IsAssignableGlobalRole</c>— que
    /// coincidian por accidente. Esta prueba mira la politica <b>ya construida</b> por el
    /// contenedor, no el codigo fuente: si alguien escribe
    /// <c>RequireRole("webmaster", "gestor_interno")</c> a mano y despues añade un rol interno
    /// a <see cref="Permisos.RolesInternos"/>, las dos se separan y esto se pone en rojo.
    /// </para>
    /// <para>
    /// Se lee por reflexion porque <c>RolesAuthorizationRequirement</c> no expone sus roles de
    /// otra forma. Si una version de ASP.NET Core cambia ese tipo, la prueba fallara al
    /// buscarlo —y fallar es lo correcto: significa que dejo de poder comprobar lo que dice.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task La_politica_de_funcionario_exige_exactamente_los_roles_internos()
    {
        using var alcance = _factory.Services.CreateScope();
        var proveedor = alcance.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        var politica = await proveedor.GetPolicyAsync(Permisos.PoliticaFuncionario);
        Assert.NotNull(politica);

        var exigencia = politica!.Requirements.OfType<RolesAuthorizationRequirement>().SingleOrDefault();
        Assert.True(exigencia is not null,
            $"La politica '{Permisos.PoliticaFuncionario}' dejo de exigir rol. Si eso fue a proposito, "
            + "el circuito de revision institucional se quedo sin su unica guarda: sus tres ficheros "
            + "no comprueban rol en ningun manejador.");

        Assert.Equal(
            Permisos.RolesInternos.OrderBy(rol => rol, StringComparer.Ordinal),
            exigencia!.AllowedRoles.OrderBy(rol => rol, StringComparer.Ordinal));
    }

    /// <summary>
    /// El catalogo de plataforma son tres nombres y los internos son un subconjunto suyo.
    /// </summary>
    /// <remarks>
    /// Es el aserto que impide el error contrario al del hallazgo: no que entre alguien de
    /// fuera, sino que alguien añada <c>externo</c> a la lista de internos «para que pueda ver
    /// la consola».
    /// </remarks>
    [Fact]
    public void El_catalogo_de_roles_es_el_del_modelo_aprobado()
    {
        Assert.Equal(
            RolesDePlataformaEsperados,
            Permisos.RolesDePlataforma.OrderBy(rol => rol, StringComparer.Ordinal));

        Assert.Equal(
            RolesInternosEsperados,
            Permisos.RolesInternos.OrderBy(rol => rol, StringComparer.Ordinal));

        Assert.DoesNotContain(Permisos.Externo, Permisos.RolesInternos);
        Assert.True(Permisos.EsRolInterno("webmaster"));
        Assert.False(Permisos.EsRolInterno("externo"));
        Assert.False(Permisos.EsRolInterno("aliado_admin"));
        Assert.False(Permisos.EsRolInterno(null));
    }

    /// <summary>
    /// Ninguna lista de roles del API admite a alguien que no sea interno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Barrido por reflexion sobre los campos <c>string[]</c> de los endpoints que deciden por
    /// rol. Cubre el hueco que deja la desaparicion del aliado: los guardas cuya lista es
    /// <c>[webmaster, gestor_interno]</c> ya no tienen ningun rol con sesion valida al que
    /// negarle el paso, asi que ninguna prueba de integracion puede ejercitarlos. Lo que si se
    /// puede comprobar —y es lo que de verdad importa— es que ninguno se amplie a un rol de
    /// fuera del Ministerio.
    /// </para>
    /// <para>
    /// Si alguien añade un endpoint con su propia lista de roles, este barrido lo recoge solo:
    /// busca por convencion de nombre (<c>*Roles</c>) sobre todo el ensamblado del API.
    /// </para>
    /// </remarks>
    [Fact]
    public void Ninguna_lista_de_roles_del_api_admite_a_alguien_de_fuera_del_ministerio()
    {
        var ensamblado = typeof(Permisos).Assembly;
        var revisadas = 0;

        foreach (var tipo in ensamblado.GetTypes().Where(item => item.Name.EndsWith("Endpoints", StringComparison.Ordinal)))
        {
            foreach (var campo in tipo.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (campo.FieldType != typeof(string[]) || !campo.Name.EndsWith("Roles", StringComparison.Ordinal))
                {
                    continue;
                }

                var roles = (string[])campo.GetValue(null)!;
                revisadas++;

                Assert.All(roles, rol => Assert.True(
                    Permisos.EsRolInterno(rol),
                    $"{tipo.Name}.{campo.Name} admite el rol '{rol}', que no es interno. "
                    + "Los guardas de rol del API deciden sobre actos institucionales; quien no es "
                    + "del Ministerio no llega ahi ni con sesion valida."));
            }
        }

        // RECUENTO EXACTO, NO SUELO. Con `>= 5` sobre seis listas reales, renombrar UNA sola
        // —y la convencion al castellano de este repositorio produce nombres que EMPIEZAN por
        // «Roles»— la sacaba del barrido para siempre sin que nada enrojeciera. Es el mismo
        // mecanismo que AutorizacionPorDefectoTests usa con RutasAnonimasDeclaradas = 39: una
        // cifra declarada obliga a pasar por aqui a quien anada o quite una lista.
        Assert.True(revisadas == ListasDeRolesDeclaradas,
            $"El barrido encontro {revisadas} listas de roles y se declararon {ListasDeRolesDeclaradas}. "
            + "Si anadio una lista nueva, actualice la cifra Y compruebe que solo admite roles internos. "
            + "Si la cifra BAJO, lo probable es que alguien renombrase un campo y lo sacara del barrido "
            + "sin querer: la convencion es que el nombre TERMINE en 'Roles'.");
    }

    private static async Task NoDejoSesionAsync(HttpClient cliente)
    {
        // Un 401 en el login no prueba por si solo que no haya cookie: prueba que la respuesta
        // fue 401. Esta segunda llamada es la que lo comprueba.
        var sesion = await cliente.GetAsync("/api/v1/admin/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, sesion.StatusCode);
    }
}
