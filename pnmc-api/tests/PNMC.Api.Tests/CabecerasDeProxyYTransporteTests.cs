using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Api.Security;
using PNMC.Contracts;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// Lo que tiene que pasar cuando el API deja de hablar directamente con el visitante y pasa a
/// tener delante el proxy inverso de Azure.
/// </summary>
/// <remarks>
/// <para>
/// LA REGLA QUE ORIGINA ESTE FICHERO. Cinco politicas de limite de tasa reparten su cupo por
/// <c>Connection.RemoteIpAddress</c>. Detras de un proxy que no se procesa, esa direccion es
/// SIEMPRE la del proxy: las cinco particiones colapsan en una sola y el cupo pensado para
/// frenar a un atacante se lo gasta el trafico legitimo del pais entero. Es decir, el limite
/// pasa de proteger el servicio a ser exactamente lo que lo tumba. Estaba anotado desde el 23
/// de agosto en <c>AnaliticaFestivalesPublicosEndpoints.cs</c> como «el dia que esto se
/// despliegue»; este fichero es ese dia.
/// </para>
/// <para>
/// Y LA TRAMPA DEL ARREGLO. Procesar <c>X-Forwarded-For</c> sin limitar cuantos saltos se
/// aceptan es peor que no procesarla: la cabecera la escribe cualquiera, asi que un atacante se
/// elige su propia direccion en cada peticion y con ella una particion nueva y vacia. El cupo
/// dejaria de existir sin que nada lo delate. Por eso la prueba central de este fichero no es
/// «la cabecera se lee», sino <see cref="La_Cabecera_Que_Escribe_El_Visitante_No_Le_Regala_Una_Particion_Nueva"/>.
/// </para>
/// <para>
/// POR QUE UN ENTORNO PROPIO. En "Test" las cinco politicas devuelven <c>GetNoLimiter</c> —ver
/// el porque en <c>Pnmc003FuerzaBrutaLoginTests</c>—, de modo que una prueba de particiones
/// escrita contra la fabrica de siempre pasaria en verde con el arreglo y sin el. Estas usan un
/// nombre de entorno que no es local, donde los limitadores corren de verdad.
/// </para>
/// <para>
/// CADA PRUEBA LEVANTA SU PROPIA FABRICA a proposito. El estado de un limitador vive en el host:
/// compartir fabrica seria compartir cupo, y entonces el resultado de cada prueba dependeria de
/// en que orden las ejecute xunit. Cuesta unos segundos mas y elimina una clase entera de fallo
/// intermitente.
/// </para>
/// </remarks>
public sealed class CabecerasDeProxyYTransporteTests
{
    /// <summary>El mismo cupo de <c>admin-login</c> declarado en <c>Program.cs</c>.</summary>
    private const int CupoPorMinutoYPorOrigen = 20;

    private const string RutaDeLogin = "/api/v1/admin/auth/login";
    private const string RutaAnonima = "/health/live";

    /// <summary>Direccion que escribe el proxy: la buena.</summary>
    private const string IpDelProxy = "198.51.100.7";

    /// <summary>Direccion que se inventa el visitante en su propia cabecera.</summary>
    private const string IpFalsificada = "203.0.113.9";

    // ---------- La tabla de decision, sin levantar el API -------------------------

    /// <summary>
    /// En local, sin configuracion, todo apagado: no hay proxy delante y una redireccion a HTTPS
    /// en el puerto de desarrollo deja al frontend sin API.
    /// </summary>
    [Fact]
    public void En_Local_Sin_Configuracion_Todo_Queda_Apagado()
    {
        var seguridad = SeguridadDeTransporte.Leer(ConfiguracionCon(), esEntornoLocal: true);

        Assert.False(seguridad.DetrasDeProxyInverso);
        Assert.False(seguridad.ExigirHttps);
        Assert.Equal(SeguridadDeTransporte.DiasDeHstsPorOmision, seguridad.DiasDeHsts);
        Assert.Equal(SeguridadDeTransporte.PuertoHttpsPorOmision, seguridad.PuertoHttps);
    }

    /// <summary>
    /// FUERA DE LOCAL NO HAY VALOR POR OMISION PARA EL PROXY: HAY QUE DECLARARLO.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es la unica de las cuatro decisiones que no se puede adivinar, y sus dos errores no se
    /// parecen. <b>Encendido sin proxy delante</b> deja que cualquiera escriba su propia
    /// <c>X-Forwarded-For</c> y se elija una particion virgen en los cinco limitadores por
    /// direccion: el cupo de la puerta institucional deja de existir y nada lo delata.
    /// <b>Apagado con proxy delante</b> mete a todo el mundo en la particion del proxy y
    /// estrangula el servicio: molesto, pero ruidoso y de arreglo inmediato.
    /// </para>
    /// <para>
    /// Entre un fallo mudo y uno ruidoso, se elige que falle el ruidoso. Y como el API no puede
    /// saber si tiene un proxy delante, en vez de adivinar, pregunta — el mismo criterio que ya
    /// usa <c>Cors:AllowedOrigins</c>. La primera version de este fichero lo daba por cierto
    /// fuera de local «por seguridad»; era exactamente al reves.
    /// </para>
    /// </remarks>
    [Fact]
    public void Fuera_De_Local_El_Proxy_Hay_Que_Declararlo_O_No_Se_Arranca()
    {
        var excepcion = Assert.Throws<InvalidOperationException>(
            () => SeguridadDeTransporte.Leer(ConfiguracionCon(), esEntornoLocal: false));

        Assert.Contains(SeguridadDeTransporte.ClaveDetrasDeProxy, excepcion.Message, StringComparison.Ordinal);

        // Y declarandolo, arranca — en las dos direcciones, que es lo que hace util a la salida.
        foreach (var valor in new[] { "true", "false" })
        {
            var seguridad = SeguridadDeTransporte.Leer(
                ConfiguracionCon((SeguridadDeTransporte.ClaveDetrasDeProxy, valor)),
                esEntornoLocal: false);

            Assert.Equal(valor == "true", seguridad.DetrasDeProxyInverso);
            Assert.True(seguridad.ExigirHttps, "Fuera de local, HTTPS si viene encendido por omision.");
        }
    }

    /// <summary>
    /// En las dos direcciones. Que la configuracion pueda encender fuera de local es trivial;
    /// lo que hay que fijar es que pueda APAGAR, porque es la unica salida para quien despliegue
    /// sin proxy delante o con una sonda que llame al contenedor por HTTP.
    /// </summary>
    [Theory]
    [InlineData(true, "true", true)]
    [InlineData(false, "false", false)]
    public void La_Configuracion_Manda_Sobre_El_Entorno(bool esEntornoLocal, string valor, bool esperado)
    {
        var seguridad = SeguridadDeTransporte.Leer(
            ConfiguracionCon(
                (SeguridadDeTransporte.ClaveDetrasDeProxy, valor),
                (SeguridadDeTransporte.ClaveExigirHttps, valor)),
            esEntornoLocal);

        Assert.Equal(esperado, seguridad.DetrasDeProxyInverso);
        Assert.Equal(esperado, seguridad.ExigirHttps);
    }

    [Theory]
    [InlineData("-5", 0)]
    [InlineData("0", 0)]
    [InlineData("365", 365)]
    [InlineData("99999", 730)]
    public void Los_Dias_De_Hsts_Se_Acotan(string configurado, int esperado)
    {
        var seguridad = SeguridadDeTransporte.Leer(
            ConfiguracionCon(
                (SeguridadDeTransporte.ClaveDetrasDeProxy, "true"),
                (SeguridadDeTransporte.ClaveDiasDeHsts, configurado)),
            esEntornoLocal: false);

        Assert.Equal(esperado, seguridad.DiasDeHsts);
        Assert.Equal(TimeSpan.FromDays(esperado), seguridad.MaxEdadDeHsts);
    }

    // ---------- El reparto del cupo, que es de lo que se trata --------------------

    /// <summary>
    /// Con el proxy procesado, dos visitantes distintos son dos particiones distintas: agotar el
    /// cupo de uno no deja fuera al otro. Sin el arreglo, el segundo recibiria 429 porque para
    /// el API los dos son la misma direccion (o ninguna).
    /// </summary>
    [Fact]
    public async Task Con_Proxy_Procesado_Cada_Visitante_Tiene_Su_Propio_Cupo()
    {
        using var fabrica = new FabricaDeTransporte(detrasDeProxy: true, exigirHttps: false);
        var cliente = fabrica.CreateClient();

        var deLaPrimera = await AdivinarEnRafagaAsync(cliente, IpDelProxy, CupoPorMinutoYPorOrigen);
        Assert.All(deLaPrimera, estado => Assert.Equal(HttpStatusCode.Unauthorized, estado));

        // El cupo de 198.51.100.7 esta agotado. Otra direccion no tiene por que pagarlo.
        var deLaSegunda = await IntentarAsync(cliente, "198.51.100.8");

        Assert.Equal(HttpStatusCode.Unauthorized, deLaSegunda);
    }

    /// <summary>
    /// El control negativo, y la razon de que la prueba de arriba no pase en verde por casualidad:
    /// sin procesar el proxy, la cabecera es texto decorativo y las dos direcciones caen en la
    /// misma particion.
    /// </summary>
    [Fact]
    public async Task Sin_Proxy_Procesado_La_Cabecera_No_Cambia_Nada()
    {
        using var fabrica = new FabricaDeTransporte(detrasDeProxy: false, exigirHttps: false);
        var cliente = fabrica.CreateClient();

        await AdivinarEnRafagaAsync(cliente, IpDelProxy, CupoPorMinutoYPorOrigen);
        var deLaSegunda = await IntentarAsync(cliente, "198.51.100.8");

        Assert.Equal(HttpStatusCode.TooManyRequests, deLaSegunda);
    }

    /// <summary>
    /// LA PRUEBA QUE IMPORTA. Se agota el cupo de la direccion que escribe el proxy y despues se
    /// pide otra vez con una cadena de dos saltos, la primera inventada por el visitante:
    /// <c>X-Forwarded-For: 203.0.113.9, 198.51.100.7</c>.
    /// <para>
    /// Con <c>ForwardLimit = 1</c> el middleware lee de derecha a izquierda y para en la entrada
    /// del proxy, asi que la peticion cae en el cupo YA AGOTADO y recibe 429. Si alguien subiera
    /// ese limite —o lo quitara— el visitante se estrenaria una particion virgen, la respuesta
    /// seria 401 y esta prueba se pondria en rojo. Es el unico sitio del proyecto donde esa
    /// diferencia es visible.
    /// </para>
    /// </summary>
    [Fact]
    public async Task La_Cabecera_Que_Escribe_El_Visitante_No_Le_Regala_Una_Particion_Nueva()
    {
        using var fabrica = new FabricaDeTransporte(detrasDeProxy: true, exigirHttps: false);
        var cliente = fabrica.CreateClient();

        var dentroDelCupo = await AdivinarEnRafagaAsync(cliente, IpDelProxy, CupoPorMinutoYPorOrigen);
        Assert.All(dentroDelCupo, estado => Assert.Equal(HttpStatusCode.Unauthorized, estado));

        var conCadenaFalsificada = await IntentarAsync(cliente, $"{IpFalsificada}, {IpDelProxy}");

        Assert.Equal(HttpStatusCode.TooManyRequests, conCadenaFalsificada);
    }

    // ---------- HTTPS y HSTS -----------------------------------------------------

    /// <summary>
    /// La peticion que llega sin cifrar se redirige. Es la mitad facil.
    /// </summary>
    [Fact]
    public async Task Sin_Cifrar_Se_Redirige_A_Https()
    {
        using var fabrica = new FabricaDeTransporte(detrasDeProxy: true, exigirHttps: true);
        var cliente = ClienteQueNoSigueRedirecciones(fabrica);

        using var respuesta = await cliente.GetAsync(new Uri(RutaAnonima, UriKind.Relative));

        Assert.Equal(HttpStatusCode.TemporaryRedirect, respuesta.StatusCode);
        Assert.StartsWith("https://", respuesta.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// LA OTRA MITAD, QUE ES LA QUE TUMBA SITIOS. El proxy termina el TLS y habla HTTP con el
    /// API: si la redireccion mirase el esquema real de la conexion en vez del que anuncia
    /// <c>X-Forwarded-Proto</c>, cada peticion se redirigiria a si misma para siempre y el
    /// servicio quedaria inaccesible aunque el certificado estuviera perfecto.
    /// <para>
    /// Esta prueba es tambien la que ata las dos piezas: solo pasa si el procesado del proxy
    /// corre ANTES que la redireccion. Intercambiar esas dos lineas en <c>Program.cs</c> la pone
    /// en rojo.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Con_X_Forwarded_Proto_Https_No_Hay_Bucle_De_Redirecciones()
    {
        using var fabrica = new FabricaDeTransporte(detrasDeProxy: true, exigirHttps: true);
        var cliente = ClienteQueNoSigueRedirecciones(fabrica);

        using var peticion = new HttpRequestMessage(HttpMethod.Get, RutaAnonima);
        peticion.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var respuesta = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// HSTS solo se anuncia sobre una peticion ya cifrada; anunciarlo por HTTP no sirve de nada
    /// porque quien intercepta la conexion puede quitarlo.
    /// </summary>
    [Fact]
    public async Task Sobre_Https_Se_Anuncia_Hsts_Con_Su_Duracion()
    {
        using var fabrica = new FabricaDeTransporte(detrasDeProxy: true, exigirHttps: true);
        var cliente = ClienteQueNoSigueRedirecciones(fabrica);

        using var peticion = new HttpRequestMessage(HttpMethod.Get, RutaAnonima);
        peticion.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var respuesta = await cliente.SendAsync(peticion);

        Assert.True(
            respuesta.Headers.TryGetValues("Strict-Transport-Security", out var valores),
            "La respuesta cifrada no anuncio HSTS.");

        var cabecera = string.Join(";", valores!);
        var segundos = (int)TimeSpan.FromDays(SeguridadDeTransporte.DiasDeHstsPorOmision).TotalSeconds;
        Assert.Contains(
            "max-age=" + segundos.ToString(CultureInfo.InvariantCulture),
            cabecera,
            StringComparison.Ordinal);

        // includeSubDomains queda fuera a proposito: ver el porque en Program.cs. Si alguien lo
        // enciende sin querer, se entera aqui y no cuando deje sin servicio a un subdominio
        // hermano del Ministerio.
        Assert.DoesNotContain("includeSubDomains", cabecera, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preload", cabecera, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// La duracion configurada llega a la cabecera. Sin esta prueba, sustituir la lectura de la
    /// clave por un literal de 365 dias no mataba nada: la unica prueba que miraba la cabecera
    /// calculaba lo esperado con la MISMA constante que usa el codigo, asi que las dos se movian
    /// juntas. Aqui se pide un valor que no es el de omision, y ese es todo el truco.
    /// <para>
    /// Y no es un valor de laboratorio: bajar el <c>max-age</c> es exactamente el procedimiento
    /// para RETIRAR HSTS de un dominio sin dejar tirado a quien ya lo tenga memorizado. Si la
    /// clave no se leyera, ese procedimiento no funcionaria y nadie se enteraria hasta intentarlo.
    /// </para>
    /// </summary>
    [Fact]
    public async Task La_Duracion_Configurada_De_Hsts_Llega_A_La_Cabecera()
    {
        const int DiasPedidos = 30;
        using var fabrica = new FabricaDeTransporte(detrasDeProxy: true, exigirHttps: true, dias: DiasPedidos);
        var cliente = ClienteQueNoSigueRedirecciones(fabrica);

        using var peticion = new HttpRequestMessage(HttpMethod.Get, RutaAnonima);
        peticion.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var respuesta = await cliente.SendAsync(peticion);

        Assert.True(respuesta.Headers.TryGetValues("Strict-Transport-Security", out var valores));
        var segundos = (int)TimeSpan.FromDays(DiasPedidos).TotalSeconds;
        Assert.Contains(
            "max-age=" + segundos.ToString(CultureInfo.InvariantCulture),
            string.Join(";", valores!),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Y con la exigencia apagada no aparece ni la redireccion ni la cabecera. Sin este control,
    /// las dos pruebas de arriba pasarian igual si HSTS lo pusiera cualquier otra cosa del
    /// proyecto —por ejemplo <c>SecurityHeadersMiddleware</c>— y no lo que se acaba de cablear.
    /// </summary>
    [Fact]
    public async Task Con_La_Exigencia_Apagada_No_Hay_Redireccion_Ni_Hsts()
    {
        using var fabrica = new FabricaDeTransporte(detrasDeProxy: true, exigirHttps: false);
        var cliente = ClienteQueNoSigueRedirecciones(fabrica);

        using var peticion = new HttpRequestMessage(HttpMethod.Get, RutaAnonima);
        peticion.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var respuesta = await cliente.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False(
            respuesta.Headers.Contains("Strict-Transport-Security"),
            "Aparecio HSTS con la exigencia de HTTPS apagada.");
    }

    // ---------- Andamio ----------------------------------------------------------

    private static IConfiguration ConfiguracionCon(params (string Clave, string Valor)[] valores)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(par =>
                new KeyValuePair<string, string?>(par.Clave, par.Valor)))
            .Build();

    /// <summary>
    /// El anfitrion NO puede ser «localhost»: <c>UseHsts</c> excluye de serie el bucle local, de
    /// modo que una prueba de HSTS contra localhost daria negativo aunque todo estuviera bien.
    /// </summary>
    private static HttpClient ClienteQueNoSigueRedirecciones(FabricaDeTransporte fabrica)
        => fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://pnmc.ejemplo.gov.co"),
        });

    private static async Task<HttpStatusCode> IntentarAsync(HttpClient cliente, string cabeceraDeOrigen)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, RutaDeLogin)
        {
            Content = JsonContent.Create(new AdminLoginRequest
            {
                Email = "nadie@pnmc.local",
                Password = "una-contrasena-cualquiera",
            }),
        };
        peticion.Headers.TryAddWithoutValidation("X-Forwarded-For", cabeceraDeOrigen);

        using var respuesta = await cliente.SendAsync(peticion);
        return respuesta.StatusCode;
    }

    private static async Task<List<HttpStatusCode>> AdivinarEnRafagaAsync(
        HttpClient cliente,
        string cabeceraDeOrigen,
        int intentos)
    {
        var respuestas = new List<HttpStatusCode>(intentos);
        for (var numero = 0; numero < intentos; numero++)
        {
            respuestas.Add(await IntentarAsync(cliente, cabeceraDeOrigen));
        }

        return respuestas;
    }
}

/// <summary>
/// El API en un entorno que NO es local, con la base sustituida por SQLite y con las dos
/// banderas de transporte puestas a mano.
/// </summary>
/// <remarks>
/// Hereda los mismos dos peajes que <c>FabricaConLimitadorDeLoginActivo</c> y por los mismos
/// motivos: cadena de conexion falsa para que el registro de infraestructura no reviente, y
/// origenes CORS explicitos porque fuera de local el arranque los exige. Lo que aqui se mide es
/// el transporte, no CORS ni la base.
/// </remarks>
public sealed class FabricaDeTransporte(bool detrasDeProxy, bool exigirHttps, int? dias = null) : WebApplicationFactory<Program>
{
    /// <summary>Cualquier nombre sirve mientras NO sea Development, Local ni Test.</summary>
    public const string NombreDelEntorno = "PruebaDeTransporte";

    private const string CadenaQueNuncaSeAbre =
        "Server=pnmc-servidor-inexistente;Database=pnmc-nunca-se-abre;User Id=nadie;Password=nada;Encrypt=False;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(NombreDelEntorno);
        builder.UseSetting("ConnectionStrings:SqlServer", CadenaQueNuncaSeAbre);
        builder.UseSetting("Cors:AllowedOrigins:0", "https://pruebas.pnmc.local");
        builder.UseSetting(SeguridadDeTransporte.ClaveDetrasDeProxy, detrasDeProxy ? "true" : "false");
        builder.UseSetting(SeguridadDeTransporte.ClaveExigirHttps, exigirHttps ? "true" : "false");

        // AQUI HABIA UNA TRAMPA Y SE QUITO. Hasta que una revision adversarial lo señalo, esta
        // fabrica inyectaba `HTTPS_PORT=443` a mano «para que hubiera redireccion que probar».
        // Con eso, la prueba de la redireccion pasaba en verde... por una condicion que SOLO
        // existia dentro de la prueba: produccion no declaraba ese puerto en ninguna parte, asi
        // que alli UseHttpsRedirection no redirigia nada y `ExigirHttps=true` era decorativo.
        // El puerto lo declara ahora Program.cs (Security:PuertoHttps, 443 por omision), de modo
        // que lo que se ejercita aqui es el camino de produccion y no un andamio.
        if (dias is not null)
        {
            builder.UseSetting(SeguridadDeTransporte.ClaveDiasDeHsts, dias.Value.ToString(CultureInfo.InvariantCulture));
        }

        builder.ConfigureServices(services => services.UsarSqliteDePruebas("pnmc-transporte"));
    }
}
