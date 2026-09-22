using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-050. El tablero publico de analitica de festivales.
/// <para>
/// LO QUE SE MIDIO ANTES DE TOCAR NADA, porque el punto entro al backlog como SOSPECHA y no
/// como hallazgo. Contra el servicio vivo y la base local, el 2026-08-22 a las 04:30 UTC,
/// 1.000 peticiones seguidas sobre una
/// sola conexion:
/// </para>
/// <code>
/// curl -K cfg1000.txt   # 1.000 veces la misma URL, keep-alive
/// # CPU del proceso PNMC.Api antes 57,73 s / despues 62,47 s
/// # -> 4,73 ms de CPU por peticion; 8,79 ms de reloj; 113,8 peticiones por segundo en serie
/// # -> p50 7,3 ms  p95 19,7 ms  p99 26,1 ms   (respuesta de 1.320 bytes, 30 festivales)
/// </code>
/// <para>
/// El informe estimaba 219 ms de CPU por peticion y saturacion de 2 vCPU a partir de 9
/// peticiones por segundo. Lo medido es 46 veces menor, y en la base local la ruta no se acerca
/// a saturar nada. La cifra del informe queda REFUTADA a la escala de hoy; lo que si es real, y
/// es lo que se corrige, es la FORMA del coste: la lectura hacia una union anidada en memoria
/// que crece con el cuadrado del censo (ver la nota de PNMC-050 en
/// <c>LecturaFestivalesPublicados.cs</c>), y la ruta era anonima y sin tope ninguno.
/// </para>
/// <para>
/// POR QUE ESTE FICHERO NO TRAE SU PROPIO ENTORNO, al reves que <c>Pnmc003FuerzaBrutaLoginTests</c>.
/// Aquella prueba necesita una fabrica en un entorno distinto de "Test" porque las cinco
/// politicas con nombre de <c>Program.cs</c> devuelven <c>GetNoLimiter</c> en "Test", y una
/// prueba de 429 escrita contra el arnes normal pasaria en verde sin ejercitar nada. El limite
/// del tablero publico se declara aparte, junto a su endpoint
/// (<c>LimiteDeTasaAnaliticaPublica</c>), y NO lleva esa exencion: corre tambien en pruebas.
/// Por eso el 429 de aqui es un 429 de verdad, en el mismo entorno "Test" que usa el resto de
/// la suite. Si alguien anadiera la exencion, <see cref="Agotado_el_cupo_el_tablero_publico_responde_429"/>
/// se pondria roja, que es exactamente lo que tiene que pasar.
/// </para>
/// <para>
/// La suite entera pide esta ruta dos veces (<c>ApiIntegrationTests</c> y
/// <c>AutorizacionPorDefectoTests</c>), muy por debajo del cupo, de modo que tenerlo activo en
/// pruebas no estrangula a nadie.
/// </para>
/// </summary>
public sealed class Pnmc050AnaliticaPublicaTests : IClassFixture<FabricaAnaliticaPublica>
{
    /// <summary>
    /// El cupo declarado en <c>LimiteDeTasaAnaliticaPublica.CupoPorMinutoYPorOrigen</c>.
    /// <para>
    /// Va repetido a mano y no leido de la constante a proposito, igual que en PNMC-003: si
    /// alguien afloja el tope, estas pruebas se ponen rojas y hay que venir aqui a justificarlo.
    /// </para>
    /// </summary>
    private const int CupoPorMinutoYPorOrigen = 120;

    private const string Ruta = "/api/v1/publico/analitica/festivales/resumen";

    private readonly FabricaAnaliticaPublica _fabrica;

    public Pnmc050AnaliticaPublicaTests(FabricaAnaliticaPublica fabrica) => _fabrica = fabrica;

    // ---------- Andamio ----------------------------------------------------------

    private static async Task<HttpResponseMessage> PedirElResumenAsync(HttpClient cliente, string ipDeOrigen)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, Ruta);
        peticion.Headers.Add(FabricaAnaliticaPublica.CabeceraIpDeOrigen, ipDeOrigen);
        return await cliente.SendAsync(peticion);
    }

    private static async Task<List<HttpStatusCode>> PedirEnRafagaAsync(
        HttpClient cliente,
        string ipDeOrigen,
        int peticiones)
    {
        var estados = new List<HttpStatusCode>(peticiones);
        for (var numero = 0; numero < peticiones; numero++)
        {
            using var respuesta = await PedirElResumenAsync(cliente, ipDeOrigen);
            estados.Add(respuesta.StatusCode);
        }

        return estados;
    }

    // ---------- La puerta -------------------------------------------------------

    /// <summary>
    /// LA PRUEBA DEL PARCHE. Sin <c>.RequireRateLimiting(...)</c> en el endpoint, esta rafaga
    /// devuelve 121 veces 200 y podria seguir hasta agotar la maquina; con el, la peticion que
    /// se pasa del cupo ya no llega al manejador.
    /// <para>
    /// SUPUESTO DE TIEMPO, escrito para quien la vea parpadear: la ventana es de un minuto y se
    /// abre con la primera peticion de la rafaga. Las 121 peticiones tardan bastante menos que
    /// eso (SQLite en fichero temporal, sin red), pero si algun dia esta prueba fallara SOLO por
    /// el ultimo <c>Assert</c> —con las 120 primeras en 200— la causa mas probable no es que el
    /// limitador haya desaparecido, sino que la maquina fue tan lenta que la ventana se
    /// repuso a mitad de la rafaga.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Agotado_el_cupo_el_tablero_publico_responde_429()
    {
        var cliente = _fabrica.CreateClient();

        var estados = await PedirEnRafagaAsync(cliente, "203.0.113.50", CupoPorMinutoYPorOrigen + 1);

        // Todo lo que cabe en el cupo se sirve. Si aqui saliera un 429, el tope estaria
        // estrangulando visitas legitimas antes de tiempo, que es la otra forma de romper la
        // pagina publica.
        Assert.All(estados.Take(CupoPorMinutoYPorOrigen), estado => Assert.Equal(HttpStatusCode.OK, estado));

        // Y la primera que se pasa ya no se atiende. Esto es lo que no existia.
        Assert.Equal(HttpStatusCode.TooManyRequests, estados[CupoPorMinutoYPorOrigen]);
    }

    /// <summary>
    /// El cupo se reparte por origen. Con una ventana global, quien quisiera dejar el tablero
    /// publico en blanco para todo el pais solo tendria que gastarla desde su casa: el limite
    /// contra el abuso seria el propio corte del servicio.
    /// </summary>
    [Fact]
    public async Task El_cupo_es_por_origen_y_no_una_ventana_global()
    {
        var cliente = _fabrica.CreateClient();

        await PedirEnRafagaAsync(cliente, "203.0.113.51", CupoPorMinutoYPorOrigen + 2);

        using var desdeOtraOficina = await PedirElResumenAsync(cliente, "198.51.100.60");

        Assert.Equal(HttpStatusCode.OK, desdeOtraOficina.StatusCode);
    }

    /// <summary>
    /// La comprobacion barata del mecanismo: la ruta declara un limitador en sus metadatos. Sin
    /// el parche no hay <c>EnableRateLimitingAttribute</c> que encontrar.
    /// </summary>
    [Fact]
    public void La_ruta_del_tablero_publico_declara_un_limite_de_tasa()
    {
        // Crear el cliente arranca el host; sin eso las fuentes de rutas estan vacias.
        using var arranque = _fabrica.CreateClient();

        var rutas = _fabrica.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(fuente => fuente.Endpoints)
            .Distinct()
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.TrimEnd('/')
                .EndsWith("publico/analitica/festivales/resumen", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var encontrada = Assert.Single(rutas);

        Assert.NotNull(encontrada.Metadata.GetMetadata<EnableRateLimitingAttribute>());
    }

    // ---------- La lectura -------------------------------------------------------

    /// <summary>
    /// GUARDA DEL REFACTOR, no prueba de un defecto: esto pasaba antes del cambio y tiene que
    /// seguir pasando despues. Se deja escrito para que no se confunda con las tres de arriba.
    /// <para>
    /// La agrupacion por <c>ILookup</c> de <c>LecturaFestivalesPublicados</c> sustituye cuatro
    /// busquedas lineales, y el error tipico al escribirla es equivocar la clave —indexar por
    /// festival lo que estaba indexado por version, o al reves—. Ese fallo no se nota con un
    /// solo festival: hace falta un festival historico y otro versionado a la vez, cada uno con
    /// su propio catalogo, y un tercero sin nada que no debe heredar el de los otros.
    /// </para>
    /// </summary>
    [Fact]
    public async Task El_resumen_cuenta_el_catalogo_de_cada_festival_y_no_el_del_vecino()
    {
        const string practicaDelHistorico = "PNMC050 practica solo del historico";
        const string practicaDelVersionado = "PNMC050 practica solo del versionado";
        const string territorioDelHistorico = "PNMC050 territorio solo del historico";

        using (var ambito = _fabrica.Services.CreateScope())
        {
            var db = ambito.ServiceProvider.GetRequiredService<PnmcDbContext>();

            var practicaA = new PracticaMusicalRow { Nombre = practicaDelHistorico };
            var practicaB = new PracticaMusicalRow { Nombre = practicaDelVersionado };
            var territorioA = new TerritorioSonoroRow { Nombre = territorioDelHistorico };
            db.PracticasMusicales.AddRange(practicaA, practicaB);
            db.TerritoriosSonoros.Add(territorioA);
            db.SaveChanges();

            var historico = NuevoFestivalPublicado("PNMC050 festival historico");
            var versionado = NuevoFestivalPublicado("PNMC050 festival versionado");
            var pelado = NuevoFestivalPublicado("PNMC050 festival sin catalogo");
            db.FestivalRecords.AddRange(historico, versionado, pelado);
            db.SaveChanges();

            var version = new VersionFestivalRow
            {
                FestivalOrigenId = versionado.Id,
                NumeroVersion = 1,
                EsVigente = true,
                Nombre = "PNMC050 festival versionado (version vigente)",
                NivelCobertura = "municipal",
                CodigoDepartamento = "05",
                CodigoMunicipio = "05001",
                FechaPublicacion = DateTime.UtcNow,
                FechaCreacion = DateTime.UtcNow
            };
            db.VersionesFestival.Add(version);
            db.SaveChanges();

            db.Agregar<PracticaMusicalDeRegistroRow>(Modulos.Festivales, historico.Id, [practicaA.Id], DateTime.UtcNow);
            db.Agregar<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, historico.Id, [territorioA.Id], DateTime.UtcNow);
            db.Agregar<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, version.Id, [practicaB.Id], DateTime.UtcNow);
            db.SaveChanges();
        }

        var cliente = _fabrica.CreateClient();
        using var respuesta = await PedirElResumenAsync(cliente, "198.51.100.61");
        respuesta.EnsureSuccessStatusCode();
        var resumen = await respuesta.Content.ReadFromJsonAsync<ResumenAnaliticoFestivalesDto>();

        Assert.NotNull(resumen);
        Assert.Equal(1, TotalDe(resumen!.PorPracticaMusical, practicaDelHistorico));
        Assert.Equal(1, TotalDe(resumen.PorPracticaMusical, practicaDelVersionado));
        Assert.Equal(1, TotalDe(resumen.PorTerritorioSonoro, territorioDelHistorico));
    }

    private static FestivalRow NuevoFestivalPublicado(string nombre) => new()
    {
        Name = nombre,
        CoverageLevel = "municipal",
        DepartmentCode = "05",
        MunicipalityCode = "05001",
        StatusCode = "Publicado",
        CreatedAt = DateTime.UtcNow
    };

    private static int TotalDe(IReadOnlyList<DistribucionAnaliticaFestivalDto> distribucion, string nombre) =>
        distribucion.SingleOrDefault(item => item.Nombre == nombre)?.Total ?? 0;
}

/// <summary>
/// El API en el entorno "Test" de siempre, con un solo anadido de andamio: una cabecera de
/// prueba que se copia a <c>HttpContext.Connection.RemoteIpAddress</c>.
/// <para>
/// <c>TestServer</c> no pone direccion de origen en la conexion, y el limitador particiona
/// justamente por ahi: sin esto todas las pruebas de este fichero compartirian la particion
/// "desconocido" y se gastarian el cupo entre ellas, con lo que el orden de ejecucion decidiria
/// cual pasa. El middleware entra por <see cref="IStartupFilter"/>, es decir POR DELANTE de
/// toda la canalizacion de <c>Program.cs</c> e <b>incluido</c> <c>UseRateLimiter</c>: si
/// corriera despues, el limitador ya habria elegido particion. Es andamio de pruebas y no toca
/// el codigo de produccion. Copiado tal cual de <c>Pnmc003FuerzaBrutaLoginTests</c>.
/// </para>
/// <para>
/// El entorno SI es "Test", al contrario que en PNMC-003: el limite del tablero publico no
/// tiene exencion de entorno, asi que aqui corre de verdad. Ver la nota de la clase de pruebas.
/// </para>
/// </summary>
public sealed class FabricaAnaliticaPublica : WebApplicationFactory<Program>
{
    public const string CabeceraIpDeOrigen = "X-Pnmc-Prueba-Ip-De-Origen";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureServices(services =>
        {
            // El proveedor lo pone el arnés: la infraestructura ya no registra
            // SQLite por el nombre del entorno, para no arrastrarlo a producción.
            services.UsarSqliteDePruebas("pnmc-analitica");
            services.AddSingleton<IStartupFilter, IpDeOrigenTomadaDeLaCabecera>();
        });
    }

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
