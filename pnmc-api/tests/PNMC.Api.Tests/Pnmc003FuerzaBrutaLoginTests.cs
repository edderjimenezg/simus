using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Endpoints;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-003. La puerta de la consola institucional admitia fuerza bruta ilimitada.
/// <para>
/// LA REGLA: <c>/api/v1/admin/auth/login</c> se mapeaba con <c>.AllowAnonymous()</c> y nada
/// mas. Su gemela del canal externo, <c>/api/v1/externo/auth/login</c>, si llevaba
/// <c>RequireRateLimiting("external-login")</c> desde el principio; la institucional —la que
/// entrega la cookie de webmaster, es decir el control total de usuarios, datos y publicacion—
/// no llevaba ninguno. Probar contrasenas contra ella costaba exactamente lo que cuesta abrir
/// una conexion HTTP, y nada en la peticion numero un millon la distinguia de la primera.
/// </para>
/// <para>
/// EL PARCHE: politica <c>admin-login</c> en <c>Program.cs</c>
/// (<c>CreateAdminLoginRateLimitPartition</c>), 20 intentos por minuto y por direccion de
/// origen, mas <c>.RequireRateLimiting("admin-login")</c> en el endpoint. El razonamiento del
/// numero y de por que se corta por IP y no por cuenta esta escrito alli.
/// </para>
/// <para>
/// POR QUE ESTE FICHERO TRAE SU PROPIO ARNES. <c>Program.cs</c> devuelve
/// <c>RateLimitPartition.GetNoLimiter</c> cuando el entorno es "Test" —igual que las otras
/// cuatro politicas del fichero, y por el mismo motivo: la suite institucional inicia sesion
/// decenas de veces por minuto contra el mismo host y se autoexpulsaria—. Eso convierte la
/// prueba obvia en una trampa: pedir 429 desde <c>TestWebApplicationFactory</c> pasa en verde
/// sin ejercitar limitador ninguno, tanto con el parche como sin el. Por eso
/// <see cref="FabricaConLimitadorDeLoginActivo"/> levanta el API en un entorno DISTINTO de
/// "Test", donde el limitador si corre de verdad, y
/// <see cref="En_el_entorno_Test_el_limitador_esta_desactivado_a_proposito"/> deja esa trampa
/// escrita para que nadie la vuelva a pisar.
/// </para>
/// </summary>
public sealed class Pnmc003FuerzaBrutaLoginTests
    : IClassFixture<FabricaConLimitadorDeLoginActivo>, IClassFixture<FabricaEnEntornoTestPlano>
{
    /// <summary>
    /// El cupo declarado en <c>Program.cs</c>, <c>CreateAdminLoginRateLimitPartition</c>.
    /// <para>
    /// Va repetido a mano y no leido del contenedor a proposito: si alguien sube el numero,
    /// estas pruebas se ponen rojas y hay que venir aqui a justificar el cambio, que es
    /// justamente lo que se quiere que pase con el tope de una pantalla de acceso.
    /// </para>
    /// </summary>
    private const int CupoPorMinutoYPorOrigen = 20;

    private const string Ruta = "/api/v1/admin/auth/login";

    private readonly FabricaConLimitadorDeLoginActivo _conLimitador;
    private readonly FabricaEnEntornoTestPlano _entornoTest;

    public Pnmc003FuerzaBrutaLoginTests(
        FabricaConLimitadorDeLoginActivo conLimitador,
        FabricaEnEntornoTestPlano entornoTest)
    {
        _conLimitador = conLimitador;
        _entornoTest = entornoTest;
    }

    // ---------- Andamio ----------------------------------------------------------

    private static async Task<HttpStatusCode> IntentarAsync(
        HttpClient cliente,
        string ipDeOrigen,
        string correo,
        string contrasena)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, Ruta)
        {
            Content = JsonContent.Create(new AdminLoginRequest { Email = correo, Password = contrasena })
        };
        peticion.Headers.Add(FabricaConLimitadorDeLoginActivo.CabeceraIpDeOrigen, ipDeOrigen);

        using var respuesta = await cliente.SendAsync(peticion);
        return respuesta.StatusCode;
    }

    /// <summary>Dispara <paramref name="intentos"/> adivinanzas seguidas desde una misma IP.</summary>
    private static async Task<List<HttpStatusCode>> AdivinarEnRafagaAsync(
        HttpClient cliente,
        string ipDeOrigen,
        int intentos)
    {
        var respuestas = new List<HttpStatusCode>(intentos);
        for (var numero = 0; numero < intentos; numero++)
        {
            respuestas.Add(await IntentarAsync(
                cliente,
                ipDeOrigen,
                FabricaConLimitadorDeLoginActivo.CorreoDelGestor,
                $"adivinanza-numero-{numero}"));
        }

        return respuestas;
    }

    // ---------- El defecto ------------------------------------------------------

    /// <summary>
    /// La prueba de PNMC-003. Sin el parche las 25 adivinanzas responden 401 y la rafaga podria
    /// seguir indefinidamente; con el, la numero 21 ya no llega al manejador.
    /// </summary>
    [Fact]
    public async Task Agotado_el_cupo_el_login_institucional_responde_429()
    {
        var cliente = _conLimitador.CreateClient();

        var respuestas = await AdivinarEnRafagaAsync(cliente, "203.0.113.11", CupoPorMinutoYPorOrigen + 5);

        // Los intentos dentro del cupo llegan al manejador y este los rechaza por credencial,
        // no por cupo: si aqui apareciera un 429 el limite estaria estrangulando a gente
        // legitima antes de tiempo.
        Assert.All(
            respuestas.Take(CupoPorMinutoYPorOrigen),
            estado => Assert.Equal(HttpStatusCode.Unauthorized, estado));

        // Y el primero que se pasa del cupo ya no se atiende. Esto es lo que no existia.
        Assert.Equal(HttpStatusCode.TooManyRequests, respuestas[CupoPorMinutoYPorOrigen]);
    }

    /// <summary>
    /// Lo que de verdad frena el ataque: agotado el cupo, la puerta no distingue. Ni siquiera
    /// la contrasena buena entra, asi que la rafaga no puede terminar en sesion abierta.
    /// </summary>
    [Fact]
    public async Task Agotado_el_cupo_ni_la_contrasena_correcta_abre_la_puerta()
    {
        var cliente = _conLimitador.CreateClient();
        const string ipDelAtacante = "203.0.113.12";

        await AdivinarEnRafagaAsync(cliente, ipDelAtacante, CupoPorMinutoYPorOrigen);

        var conLaClaveBuena = await IntentarAsync(
            cliente,
            ipDelAtacante,
            FabricaConLimitadorDeLoginActivo.CorreoDelGestor,
            FabricaConLimitadorDeLoginActivo.ContrasenaDelGestor);

        Assert.Equal(HttpStatusCode.TooManyRequests, conLaClaveBuena);
    }

    /// <summary>
    /// El cupo se reparte por origen y no es una ventana global. Con una ventana global, quien
    /// quisiera tumbar la consola de todo el pais solo tendria que gastar 20 intentos por
    /// minuto desde su casa: el limite de abuso se convertiria en la propia caida del servicio.
    /// </summary>
    [Fact]
    public async Task El_cupo_es_por_origen_y_no_una_ventana_global()
    {
        var cliente = _conLimitador.CreateClient();

        await AdivinarEnRafagaAsync(cliente, "203.0.113.13", CupoPorMinutoYPorOrigen + 3);

        var desdeOtraOficina = await IntentarAsync(
            cliente,
            "198.51.100.7",
            FabricaConLimitadorDeLoginActivo.CorreoDelGestor,
            "una-clave-cualquiera");

        Assert.Equal(HttpStatusCode.Unauthorized, desdeOtraOficina);
    }

    /// <summary>
    /// El otro lado del numero: un tope agresivo deja fuera a quien tiene derecho a entrar. Una
    /// gestora que se equivoca tres veces —y comparte la IP publica de su alcaldia con el resto
    /// de la oficina— sigue entrando a la cuarta.
    /// </summary>
    [Fact]
    public async Task Una_gestora_que_falla_tres_veces_todavia_entra()
    {
        var cliente = _conLimitador.CreateClient();
        const string ipDeLaAlcaldia = "198.51.100.20";

        for (var intento = 0; intento < 3; intento++)
        {
            var fallido = await IntentarAsync(
                cliente,
                ipDeLaAlcaldia,
                FabricaConLimitadorDeLoginActivo.CorreoDelGestor,
                $"me-equivoque-{intento}");
            Assert.Equal(HttpStatusCode.Unauthorized, fallido);
        }

        var acertado = await IntentarAsync(
            cliente,
            ipDeLaAlcaldia,
            FabricaConLimitadorDeLoginActivo.CorreoDelGestor,
            FabricaConLimitadorDeLoginActivo.ContrasenaDelGestor);

        Assert.Equal(HttpStatusCode.OK, acertado);
    }

    // ---------- El mecanismo -----------------------------------------------------

    /// <summary>
    /// La ruta declara el cupo en sus metadatos. Es la comprobacion barata y directa del
    /// parche: sin <c>.RequireRateLimiting("admin-login")</c> no hay metadato que encontrar.
    /// </summary>
    [Fact]
    public void La_ruta_de_login_institucional_declara_la_politica_admin_login()
    {
        // Crear el cliente arranca el host; sin eso las fuentes de rutas estan vacias.
        using var arranque = _entornoTest.CreateClient();
        var rutas = _entornoTest.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(fuente => fuente.Endpoints)
            .Distinct()
            .ToList();

        // Se busca por sufijo y no por igualdad exacta para no atarse a como
        // RoutePattern.Combine componga el prefijo de los grupos anidados.
        var login = rutas
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.RoutePattern.RawText?.TrimEnd('/')
                    .EndsWith("admin/auth/login", StringComparison.OrdinalIgnoreCase) == true)
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") != false)
            .ToList();

        var encontrada = Assert.Single(login);
        var politica = encontrada.Metadata.GetMetadata<EnableRateLimitingAttribute>();

        Assert.NotNull(politica);
        Assert.Equal("admin-login", politica!.PolicyName);
    }

    /// <summary>
    /// LA TRAMPA, ESCRITA. En el entorno "Test" el limitador devuelve
    /// <c>GetNoLimiter</c> y esta rafaga de 25 intentos no ve un solo 429 — ni antes ni despues
    /// del parche. Cualquier prueba de fuerza bruta escrita contra
    /// <c>TestWebApplicationFactory</c> estaria afirmando el defecto en verde.
    /// <para>
    /// SI ESTA PRUEBA SE PONE ROJA porque alguien retiro la exencion de "Test": no es una
    /// regresion, es una mejora. Borrala, y comprueba que la suite institucional sigue en verde
    /// (inicia sesion muchas mas de 20 veces por minuto contra el mismo host, que es
    /// exactamente el motivo de que la exencion exista).
    /// </para>
    /// </summary>
    [Fact]
    public async Task En_el_entorno_Test_el_limitador_esta_desactivado_a_proposito()
    {
        var cliente = _entornoTest.CreateClient();

        var respuestas = new List<HttpStatusCode>();
        for (var numero = 0; numero < CupoPorMinutoYPorOrigen + 5; numero++)
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Post, Ruta)
            {
                Content = JsonContent.Create(new AdminLoginRequest
                {
                    Email = "nadie.en.absoluto@pnmc.local",
                    Password = $"adivinanza-numero-{numero}"
                })
            };
            using var respuesta = await cliente.SendAsync(peticion);
            respuestas.Add(respuesta.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, respuestas);
    }
}

/// <summary>
/// El API levantada en un entorno donde los limitadores de tasa SI corren.
/// <para>
/// No basta con <c>TestWebApplicationFactory</c>: su entorno es "Test" y ahi todas las
/// politicas de <c>Program.cs</c> devuelven <c>GetNoLimiter</c>. Esta fabrica usa un nombre de
/// entorno propio, y con eso paga dos peajes que hay que deshacer a mano:
/// </para>
/// <list type="number">
/// <item><description>
/// Fuera de "Test", <c>AddPnmcInfrastructure</c> exige una cadena de conexion a SQL Server o
/// revienta al construir el host. Se le da una falsa por variable de entorno —la primera
/// fuente que consulta <c>DatabaseConnectionResolver</c>— y acto seguido se sustituye el
/// contexto entero por SQLite, de modo que esa cadena no llega a abrirse nunca.
/// </description></item>
/// <item><description>
/// <c>TestServer</c> no pone direccion de origen en la conexion, y el limitador particiona
/// precisamente por ahi: sin esto, todas las pruebas de este fichero compartirian la particion
/// "unknown" y se gastarian el cupo entre ellas. Un middleware de prueba —insertado por
/// <see cref="IStartupFilter"/>, es decir ANTES de <c>UseRateLimiter</c>— copia la IP de una
/// cabecera. Es andamio de pruebas y no toca el codigo de produccion.
/// </description></item>
/// </list>
/// </summary>
public sealed class FabricaConLimitadorDeLoginActivo : WebApplicationFactory<Program>
{
    /// <summary>Cualquier nombre sirve mientras NO sea "Test": ese es el que apaga los cupos.</summary>
    public const string NombreDelEntorno = "PruebaLimitadorDeTasa";

    public const string CabeceraIpDeOrigen = "X-Pnmc-Prueba-Ip-De-Origen";

    public const string CorreoDelGestor = "gestora.municipal@pnmc.local";
    public const string ContrasenaDelGestor = "clave-de-oficina-2026";

    static FabricaConLimitadorDeLoginActivo()
    {
        // Se pone solo si no habia ninguna, para no pisarle la suya a quien tenga un .env con
        // credenciales reales. Sirva la que sirva, no se abre: el contexto se sustituye abajo.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING")))
        {
            Environment.SetEnvironmentVariable(
                "AZURE_SQL_CONNECTION_STRING",
                "Server=pnmc-servidor-inexistente;Database=pnmc-nunca-se-abre;User Id=nadie;Password=nada;Encrypt=False;");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(NombreDelEntorno);
        builder.UseSetting(
            "ConnectionStrings:SqlServer",
            "Server=pnmc-servidor-inexistente;Database=pnmc-nunca-se-abre;User Id=nadie;Password=nada;Encrypt=False;");
        // Fuera de Development/Local/Test el API exige origenes CORS explicitos y no
        // arranca sin ellos (23 ago 2026): este entorno es «no local» a proposito,
        // asi que declara uno. Lo que se mide aqui es el limitador, no CORS.
        builder.UseSetting("Cors:AllowedOrigins:0", "https://pruebas.pnmc.local");

        // ESTA LINEA ES UNA DECISION, NO UN AJUSTE. Este entorno no es local, asi que desde el
        // 24 ago 2026 heredaria HTTPS exigido y con el una redireccion 307 en cada peticion:
        // las cuatro pruebas de abajo piden http://localhost y esperan 401, 429 y 200. Hoy
        // seguirian en verde igualmente, porque bajo TestServer el middleware de redireccion no
        // consigue resolver un puerto HTTPS y se comporta como si no estuviera. Eso es un verde
        // por accidente: bastaria que alguien declarase el puerto para tumbar las cuatro sin
        // que ninguna hable de HTTPS. Se apaga a proposito y se deja escrito. Lo que aqui se
        // mide es el limitador de tasa; el transporte tiene su propio fichero
        // (CabecerasDeProxyYTransporteTests), y alli si esta encendido.
        builder.UseSetting(PNMC.Api.Security.SeguridadDeTransporte.ClaveExigirHttps, "false");

        // Y este entorno tambien tiene que declarar si hay proxy delante, porque fuera de local
        // el arranque lo exige. Aqui no lo hay: el arnes escribe la IP de origen por su cuenta
        // (ver IpDeOrigenTomadaDeLaCabecera). Declararlo «false» es ademas lo correcto de verdad,
        // no un tramite: con TestServer no existe ningun proxy.
        builder.UseSetting(PNMC.Api.Security.SeguridadDeTransporte.ClaveDetrasDeProxy, "false");

        builder.ConfigureServices(services =>
        {
            // Se barren TODOS los registros del contexto y no solo DbContextOptions<T>: fuera
            // de "Test" el proveedor registrado es SQL Server, y si sobreviviera cualquier
            // pieza de esa configuracion junto a la de SQLite, EF abortaria por tener dos
            // proveedores para el mismo contexto.
            var registrosDelContexto = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(PnmcDbContext)
                    || descriptor.ServiceType == typeof(DbContextOptions)
                    || (descriptor.ServiceType.IsGenericType
                        && descriptor.ServiceType.GetGenericArguments().Contains(typeof(PnmcDbContext))))
                .ToList();
            foreach (var registro in registrosDelContexto)
            {
                services.Remove(registro);
            }

            var rutaDeLaBase = Path.Combine(
                Path.GetTempPath(),
                $"pnmc-limitador-login-{Guid.NewGuid():N}.db");
            services.AddDbContext<PnmcDbContext>(options => options.UseSqlite($"Data Source={rutaDeLaBase}"));

            services.AddSingleton<IStartupFilter, IpDeOrigenTomadaDeLaCabecera>();

            // La gestora legitima. Se siembra aqui y no se confia en la siembra de
            // DatabaseBootstrapper porque esa corre con ContinueOnStartupFailure y, si fallara,
            // "no entra con la clave buena" pareceria un defecto del limitador.
            using var ambito = services.BuildServiceProvider().CreateScope();
            var db = ambito.ServiceProvider.GetRequiredService<PnmcDbContext>();
            db.Database.EnsureCreated();

            var rol = new RoleRow { Name = "webmaster", Description = "Control total." };
            db.Roles.Add(rol);
            db.SaveChanges();

            var gestora = new UserRow
            {
                FullName = "Gestora Municipal",
                Email = CorreoDelGestor,
                AccessChannel = "interno",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            gestora.PasswordHash = AdminAuthEndpoints.HashPassword(gestora, ContrasenaDelGestor);
            db.Users.Add(gestora);
            db.SaveChanges();

            // LA FILA DE dbo.UsuariosRoles. Ver RolesEnPruebas: desde entonces es lo unico que
            // da el rol, y sin esto la gestora no puede entrar ni al primer intento — con lo que
            // la prueba de fuerza bruta mediria un 401 que no viene del limite de intentos.
            db.UsuariosRoles.Add(new UsuarioRolRow { UserId = gestora.Id, RoleId = rol.Id, CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        });
    }

    /// <summary>
    /// Copia la cabecera de prueba a <c>HttpContext.Connection.RemoteIpAddress</c>. Va por
    /// <see cref="IStartupFilter"/> para colarse por delante de toda la canalizacion de
    /// <c>Program.cs</c>, incluido <c>UseRateLimiter</c>: si corriera despues, el limitador ya
    /// habria elegido particion.
    /// <para>
    /// AHORA COMPARTE CAMPO CON <c>UseForwardedHeaders</c>, y conviene dejar escrito el orden.
    /// Desde el 24 ago 2026 este entorno tambien procesa el proxy de verdad, asi que hay dos
    /// escritores de <c>RemoteIpAddress</c>: este filtro primero —envuelve la canalizacion
    /// entera— y el middleware del marco despues. No se pisan porque este arnes manda una
    /// cabecera propia y NUNCA <c>X-Forwarded-For</c>; el middleware no toca nada cuando no hay
    /// cabecera que aplicar. Si alguien «moderniza» esto para usar <c>X-Forwarded-For</c>, que
    /// borre el filtro en el mismo cambio en vez de dejar los dos: el resultado dependeria de
    /// cual escribio ultimo, que es una carrera y no una prueba. La via real ya esta ejercitada
    /// en <c>CabecerasDeProxyYTransporteTests</c>.
    /// </para>
    /// </summary>
    private sealed class IpDeOrigenTomadaDeLaCabecera : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (HttpContext context, Func<Task> siguiente) =>
                {
                    var declarada = context.Request.Headers[CabeceraIpDeOrigen].ToString();
                    if (IPAddress.TryParse(declarada, out var direccion))
                    {
                        context.Connection.RemoteIpAddress = direccion;
                    }

                    await siguiente();
                });

                next(app);
            };
    }
}

/// <summary>
/// El API en el entorno "Test" de siempre, sin sustituciones. Se usa para leer metadatos de
/// ruta y para dejar constancia de que ahi el limitador esta apagado.
/// <para>
/// No reutiliza <c>TestWebApplicationFactory</c> a proposito: esa fabrica siembra un fixture
/// grande y compartido que otras oleadas retocan, y estas dos pruebas no necesitan nada de el
/// —la base la deja lista <c>DatabaseBootstrapper</c>, que en "Test" corre sobre SQLite—.
/// </para>
/// </summary>
public sealed class FabricaEnEntornoTestPlano : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        // El proveedor lo pone el arnés: la infraestructura ya no registra SQLite
        // por el nombre del entorno, para no arrastrarlo a producción.
        builder.ConfigureServices(services => services.UsarSqliteDePruebas("pnmc-test-plano"));
    }
}
