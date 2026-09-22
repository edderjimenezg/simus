using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using PNMC.Api.Endpoints;
using PNMC.Api.Observability;
using PNMC.Infrastructure;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;
using PNMC.Api.Security;
using PNMC.Api.Services;
using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using System.Security.Claims;
using System.Globalization;

PNMC.Api.DotEnvLoader.Load();
var builder = WebApplication.CreateBuilder(args);

// PNMC-039. REGISTRO ESTRUCTURADO.
//
// LA REGLA, CONFIRMADO POR BARRIDO ANTES DE TOCAR NADA: cero sentencias de
// registro de negocio en los 30 ficheros de Endpoints/ —26 de ellos *Endpoints.cs,
// el billete decia 27—, cero metricas y cero trazas en todo el API, ni un solo
// ILogger inyectado. Hoy eso significa que no se puede depurar; el dia del
// lanzamiento significa que no se puede responder a nadie. Ya se pago el precio
// esta semana: para archivar un expediente hubo que deducir la exposicion de
// datos leyendo "git log", porque no existia un solo registro que consultar.
//
// QUE CAMBIA AQUI, EN UNA FRASE: cada evento sale como una linea JSON con el
// identificador de correlacion de la peticion, y ninguna linea puede llevar un
// dato personal en claro.
//
// POR QUE ClearProviders(). El proveedor de consola de serie escribe el mensaje
// ya formateado, sin pasar por ningun saneado. Si se dejara puesto junto al
// nuestro, cada evento se escribiria dos veces —una enmascarada y otra en
// claro— y el enmascarado seria decorativo. Se sustituye, no se suma. El coste
// conocido: la salida deja de tener colores y pasa a ser JSON de una linea.
//
// LA CORRELACION YA EXISTIA a medias: RequestContextMiddleware genera o acepta
// X-Correlation-ID y abre un ambito con el. Lo que faltaba era alguien que lo
// escribiera; aqui se convierte en el campo "correlacion" de cada linea.
//
// EL DESTINO ES UNA INTERFAZ a proposito. No es abstraccion por gusto: es lo
// que permite que Pnmc039RegistroSinDatosPersonalesTests lea lo que el API
// escribio DE VERDAD, en vez de comprobar el enmascarador aislado en una prueba
// unitaria que pasaria en verde aunque nadie lo hubiera enchufado. La leccion
// del dia —una puerta que no puede fallar no es una puerta— aplicada al reves:
// la prueba tiene que poder ver la puerta cerrarse.
builder.Logging.ClearProviders();
builder.Services.AddSingleton<IDestinoRegistro, DestinoConsola>();
builder.Services.AddSingleton<ILoggerProvider, ProveedorRegistroEstructurado>();

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Una sola comprobacion, y solo en /ready: ver ComprobacionDeBaseDeDatos para el
// motivo de que /live siga sin dependencias. Antes de esto AddHealthChecks() iba
// vacio y las dos sondas respondian Healthy con la base caida (23 ago 2026).
builder.Services.AddHealthChecks()
    .AddCheck<ComprobacionDeBaseDeDatos>(
        ComprobacionDeBaseDeDatos.Nombre,
        tags: [ComprobacionDeBaseDeDatos.EtiquetaReady]);
// UNA SOLA DEFINICION DE «ENTORNO LOCAL». Hasta el 24 ago 2026 esta expresion estaba escrita
// tres veces —aqui para las cookies, en la comprobacion de CORS y DENTRO del delegado de
// AddCors—, con el riesgo clasico de la duplicacion: anadir un entorno a una copia y no a las
// otras deja la mitad de las defensas en un modo y la mitad en el contrario, sin que nada falle.
// Ahora la decide esta variable y de ella cuelgan cookies, CORS, HTTPS y HSTS.
//
// LA EXCEPCION, QUE ES DELIBERADA Y NO UN OLVIDO: Swagger (mas abajo) se monta en Development y
// Local pero NO en Test, asi que su condicion es a proposito mas estrecha que esta variable y no
// debe sustituirse por ella. Documentar la excepcion es lo que impide que alguien la «unifique».
var esEntornoLocal = builder.Environment.IsDevelopment()
    || builder.Environment.IsEnvironment("Local")
    || builder.Environment.IsEnvironment("Test");
var requiereCookiesSeguras = !esEntornoLocal;

// LAS CLAVES QUE FIRMAN LAS TRES COOKIES. Va antes de configurarlas, no despues: si el anillo se
// pierde en cada despliegue, las cookies emitidas antes dejan de validarse y publicar una version
// expulsa a todo el que estuviera dentro. Ver ProteccionDeDatos para la tabla de decision.
ProteccionDeDatos.Configurar(builder.Services, builder.Configuration, esEntornoLocal);

var politicaCookieSegura = requiereCookiesSeguras ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "pnmc.external.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = politicaCookieSegura;
});
builder.Services.AddAuthentication(SimusAuthentication.InstitutionalScheme)
    .AddCookie(SimusAuthentication.InstitutionalScheme, options =>
    {
        options.Cookie.Name = "pnmc.admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = politicaCookieSegura;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        // REVOCACION EFECTIVA. Sin esto, desactivar a alguien no lo echa: la cookie dura 8 h,
        // es deslizante y las politicas solo leen los claims que se escribieron al entrar. Ver
        // Security/RevalidacionDeSesion.cs para el porque y para el techo de la cache.
        options.Events.OnValidatePrincipal = RevalidarSesionAsync;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddCookie(SimusAuthentication.ExternalScheme, options =>
    {
        options.Cookie.Name = "pnmc.external";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = politicaCookieSegura;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        // REVOCACION EFECTIVA. Sin esto, desactivar a alguien no lo echa: la cookie dura 8 h,
        // es deslizante y las politicas solo leen los claims que se escribieron al entrar. Ver
        // Security/RevalidacionDeSesion.cs para el porque y para el techo de la cache.
        options.Events.OnValidatePrincipal = RevalidarSesionAsync;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
{
    // PNMC-056. El valor por defecto de ASP.NET Core es "abierto": un endpoint que no
    // declara nada sobre autorizacion se sirve sin autenticar. Con ~120 rutas mapeadas,
    // eso convertia el silencio del programador en una decision de seguridad: el censo
    // sobre Endpoints/ conto 40 rutas anonimas y ninguna lo era a proposito; tres de
    // ellas, las del portal de aliados, se cerraron aparte y despues desaparecieron con
    // el concepto entero (22 ago 2026), de modo que hoy quedan 37 mas las 2 sondas de
    // salud de este fichero = 39. La FallbackPolicy invierte el
    // defecto — cerrado salvo declaracion expresa — de modo que olvidarse de escribir
    // la autorizacion produce un 401 visible en el primer uso, y no un agujero mudo.
    //
    // POR QUE SIN AddAuthenticationSchemes: sin esquemas, la politica evalua el
    // HttpContext.User que dejo UseAuthentication, es decir el esquema por defecto
    // (el institucional). Una ruta que alguien olvide declarar queda entonces cerrada
    // incluso para una sesion externa valida, que es el defecto mas estricto posible;
    // quien necesite abrirla a los externos tendra que escribir ExternalPolicy, que es
    // justamente la declaracion expresa que buscamos.
    //
    // POR QUE NO BASTA POR SI SOLA: la FallbackPolicy solo actua donde NO hay metadatos.
    // Si manana alguien anade .AllowAnonymous() por costumbre, la puerta vuelve a abrirse
    // sin que nadie lo note. Por eso el guardian real es la prueba de barrido
    // AutorizacionPorDefectoTests, que obliga a que cada endpoint diga en voz alta cual
    // de las dos cosas es. Las 39 rutas publicas de hoy quedan marcadas una por una con
    // .AllowAnonymous() y su motivo escrito al lado.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(SimusAuthentication.InstitutionalPolicy, policy =>
        policy.AddAuthenticationSchemes(SimusAuthentication.InstitutionalScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(SimusAuthentication.AccessScopeClaim, SimusAuthentication.InstitutionalScope));
    options.AddPolicy(SimusAuthentication.ExternalPolicy, policy =>
        policy.AddAuthenticationSchemes(SimusAuthentication.ExternalScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(SimusAuthentication.AccessScopeClaim, SimusAuthentication.ExternalScope));

    // Las dos puertas a la vez, para lo que es de las dos partes: el buzon de notificaciones.
    // Exige sesion autenticada y uno de los dos ambitos; quien decide QUE ve cada quien es el
    // manejador, filtrando por destinatario y ámbito solicitado. Ver la política correspondiente.
    options.AddPolicy(SimusAuthentication.PoliticaCualquierSesion, policy =>
        policy.AddAuthenticationSchemes(SimusAuthentication.InstitutionalScheme, SimusAuthentication.ExternalScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(contexto => contexto.User.HasClaim(
                claim => claim.Type == SimusAuthentication.AccessScopeClaim
                    && (claim.Value == SimusAuthentication.InstitutionalScope
                        || claim.Value == SimusAuthentication.ExternalScope))));

    // TENER SESION DE CONSOLA NO ES SER FUNCIONARIO. InstitutionalPolicy solo comprueba que
    // hay cookie institucional con su ambito; no dice nada del rol de quien la lleva.
    //
    // POR QUE NACIO (22 de agosto de 2026). La puerta institucional era una lista negra de un
    // solo elemento —rechazaba unicamente el rol "externo"—, de modo que aliado_admin,
    // aliado_editor y aliado_lector recibian cookie de consola. Y el circuito de revision
    // institucional no comprobaba rol en ninguna de sus 554 lineas. Sumadas las dos cosas, un
    // administrador de entidad aliada podia publicar o rechazar cualquier Festival del pais.
    //
    // POR QUE SIGUE AQUI DESPUES DE RETIRAR A LOS ALIADOS. El concepto de entidad aliada se
    // elimino ese mismo dia y la puerta paso de lista negra a lista blanca, asi que hoy no
    // queda ningun rol no interno capaz de cruzarla: esta politica es, en la practica,
    // redundante. NO LA RETIRE POR ESO. Exige rol interno de forma POSITIVA, leyendo
    // Permisos.RolesInternos, y es la unica guarda de rol de todo el circuito de revision:
    // sus tres ficheros siguen sin comprobar rol en ningun manejador. El dia que aparezca la
    // subdivision fina de funcionarios que el modelo deja pendiente, esta politica es lo que
    // decide quien decide.
    options.AddPolicy(Permisos.PoliticaFuncionario, policy =>
        policy.AddAuthenticationSchemes(SimusAuthentication.InstitutionalScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(SimusAuthentication.AccessScopeClaim, SimusAuthentication.InstitutionalScope)
            .RequireRole(Permisos.RolesInternos));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("participation-submit", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    // Guardar la nomina puede traer megabytes de fotografias, e importar un
    // respaldo pisa 238 borradores de una vez. El contrato acota el tamano de una
    // peticion; esto acota cuantas caben en un minuto.
    //
    // Particionado POR USUARIO y no global: con una ventana compartida, un solo
    // editor guardando en bucle dejaria sin cupo a todos los demas, que es
    // convertir un limite de abuso en una negacion de servicio entre companeros.
    options.AddPolicy("web-team-save", context => CreateCmsRateLimitPartition(context, builder.Environment, 60));
    options.AddPolicy("web-content-import", context => CreateCmsRateLimitPartition(context, builder.Environment, 10));
    // Subir una imagen mueve hasta 2 MiB por peticion y escribe dos blobs mas una entrada de
    // historial. Treinta por minuto y por usuario: mas que suficiente para repasar las dieciseis
    // ranuras del catalogo de una sentada, y lejos de lo que costaria llenar la base a proposito.
    options.AddPolicy("web-media-upload", context => CreateCmsRateLimitPartition(context, builder.Environment, 30));
    options.AddPolicy("external-register", context => CreateExternalAuthRateLimitPartition(context, builder.Environment));
    options.AddPolicy("external-login", context => CreateExternalAuthRateLimitPartition(context, builder.Environment));
    // PNMC-003. La puerta institucional (/admin/auth/login) no tenia ningun tope:
    // su gemela externa llevaba "external-login" desde el principio y esta no,
    // asi que probar contrasenas contra la consola de administracion costaba lo
    // que costase abrir conexiones. Este es el mismo limitador, para la puerta
    // que faltaba.
    options.AddPolicy("admin-login", context => CreateAdminLoginRateLimitPartition(context, builder.Environment));
    // El asistente administrativo relee agregados de varias tablas. El cupo es por funcionario:
    // una sesión que reintenta no consume la capacidad de las demás personas de la oficina.
    options.AddPolicy("analisis-administrativo", context => CreateCmsRateLimitPartition(context, builder.Environment, 60));
    // Previsualizar y confirmar recorren hasta 500 filas y escriben trazabilidad por cada una.
    // Diez operaciones por minuto y por funcionario evita que un doble envío monopolice la base.
    options.AddPolicy("importaciones-festivales", context => CreateCmsRateLimitPartition(context, builder.Environment, 10));
    // El alta al boletin es un formulario ANONIMO: cualquiera puede llamarlo tantas veces
    // como quiera, y cada llamada escribe una fila. Por origen y no global, por el mismo
    // motivo que "web-team-save": con una ventana compartida, quien abuse deja sin cupo a
    // todo el que quiera suscribirse, que es convertir un limite de abuso en una negacion
    // de servicio. Se reutiliza la particion de las puertas externas —diez por minuto y por
    // IP— porque el perfil es el mismo: un envio de formulario que una persona hace una vez.
    options.AddPolicy("boletin-alta", context => CreateExternalAuthRateLimitPartition(context, builder.Environment));
});
// FUERA DE DESARROLLO, SIN ORIGENES NO SE ARRANCA. El appsettings base llegó a
// incluir un origen local y Produccion lo heredaba —las listas
// de configuracion se fusionan por indice, no se reemplazan—, asi que un despliegue
// en Azure habria salido con CORS de desarrollo y credenciales. Los origenes locales
// los anade el propio codigo en Dev/Local/Test (abajo); en cualquier otro entorno
// deben venir del entorno (Cors__AllowedOrigins__0) y su ausencia es un error de
// despliegue, no algo que se tapa con una politica vacia que bloquea al frontend
// sin decir por que.
//
// Va AQUI y no dentro del delegado de AddCors porque ese delegado es perezoso: se
// ejecuta en la primera peticion, no al arrancar. Un fallo que espera a la primera
// peticion no es un fallo temprano.
if (!esEntornoLocal
    && (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Length == 0)
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins esta vacio. Fuera de Development/Local/Test el API exige " +
        "los origenes del frontend de forma explicita (por ejemplo la variable de " +
        "entorno Cors__AllowedOrigins__0).");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("PnmcWebFrontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (esEntornoLocal)
        {
            var localOrigins = configuredOrigins
                .Concat(["http://localhost:4300", "http://127.0.0.1:4300"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            policy
                .WithOrigins(localOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
            return;
        }

        policy.WithOrigins(configuredOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

// Compresion de respuestas.
//
// El API devuelve JSON y poco mas, que es justo lo que mejor comprime: los
// textos del sitio bajan de 33 KB a unos 6, y la cartografia del DANE —28 MB—
// a poco mas de 2. En una conexion de territorio esa diferencia no es una
// optimizacion, es que la pagina cargue o no cargue.
//
// `EnableForHttps` esta activado a proposito. Se desaconseja cuando la respuesta
// mezcla secreto de sesion con contenido que el atacante controla (BREACH), y
// aqui no ocurre: lo que se comprime son textos publicos y cartografia publica,
// y el token antiforgery viaja en cabecera y cookie, nunca en el cuerpo.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json", "application/geo+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

// DIVIPOLA no cambia sin un despliegue: se carga una vez por proceso.
// Revalidacion de sesiones. Singleton porque su cache de estados vive entre peticiones; el
// contexto de datos se pide por alcance dentro (ver el tipo).
builder.Services.AddSingleton<RevalidacionDeSesion>();
builder.Services.AddSingleton<PNMC.Api.Endpoints.CacheDivipola>();

builder.Services.AddPnmcInfrastructure(builder.Configuration, builder.Environment);
builder.Services.Configure<RetencionImportacionesOptions>(
    builder.Configuration.GetSection(RetencionImportacionesOptions.SectionName));
builder.Services.AddScoped<DepuradorImportaciones>();

// EL UNICO PUNTO POR EL QUE SIMUS ENTREGA CORREO AL MUNDO. Hoy no hay proveedor: la implementación
// registrada deja el mensaje en la cola de salida de dbo.Notificaciones con estado `pendiente`, que
// es la verdad —hay algo que mandar y nadie lo ha mandado— y no una entrega fingida. Conectar un
// proveedor real es cambiar esta línea; ningún endpoint, regla ni pantalla se entera.
builder.Services.AddScoped<PNMC.Infrastructure.Correo.IEnviadorDeCorreo,
    PNMC.Infrastructure.Correo.EnviadorDeCorreoPendienteDeProveedor>();
builder.Services.AddSingleton(TimeProvider.System);

// EL UNICO PUNTO POR EL QUE LA CONSULTA GUIADA DECIDE QUE RESPONDE. Hoy son reglas escritas y
// revisables; el día que se mida un modelo local, se cambia esta línea y no se entera ninguna
// consulta, ningún permiso ni ninguna pantalla. El intérprete solo ELIGE una consulta del catálogo:
// no ve la base y no puede fabricar un dato, que es lo que hace segura la sustitución.
builder.Services.AddSingleton<PNMC.Api.ConsultaGuiada.IInterpreteDePregunta,
    PNMC.Api.ConsultaGuiada.InterpreteDeterministico>();

// LOS DOMINIOS QUE SE PUEDEN IMPORTAR. Cada uno pone qué campos lee, cómo normaliza y cómo se
// escribe; todo lo demás —previsualizar sin escribir, la idempotencia, la retención, la bitácora y
// la procedencia— lo pone el núcleo. Añadir uno es registrarlo aquí y declararlo en la base
// (`CK_LotesImportacion_Dominio`); ni la pantalla ni las rutas cambian.
builder.Services.AddSingleton<PNMC.Api.ImportacionAsistida.IDominioDeImportacion,
    PNMC.Api.ImportacionAsistida.DominioDeFestivales>();
builder.Services.AddSingleton<PNMC.Api.ImportacionAsistida.IDominioDeImportacion,
    PNMC.Api.ImportacionAsistida.DominioDeOrganizaciones>();
builder.Services.AddSingleton<PNMC.Api.ImportacionAsistida.IDominioDeImportacion,
    PNMC.Api.ImportacionAsistida.DominioDeNoticias>();
builder.Services.AddSingleton<PNMC.Api.ImportacionAsistida.IDominioDeImportacion,
    PNMC.Api.ImportacionAsistida.DominioDeAgenda>();
builder.Services.AddSingleton<PNMC.Api.ImportacionAsistida.IDominioDeImportacion,
    PNMC.Api.ImportacionAsistida.DominioDeCatalogoEditorial>();
// Las pruebas invocan el depurador de forma determinística. En ejecución real, el ciclo corre al
// arrancar y luego con el intervalo configurado, de modo que un reinicio no deja vencidos atrás.
if (!builder.Environment.IsEnvironment("Test"))
    builder.Services.AddHostedService<ServicioProgramadoRetencionImportaciones>();

// TRANSPORTE: PROXY INVERSO, HTTPS Y HSTS. Ver Security/SeguridadDeTransporte.cs para la
// tabla de decision y los valores por omision.
var seguridadDeTransporte = SeguridadDeTransporte.Leer(builder.Configuration, esEntornoLocal);
if (seguridadDeTransporte.DetrasDeProxyInverso)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // EL LIMITE IMPIDE ANTEPONER, NO IMPIDE ESCRIBIR. Leyendo de derecha a izquierda y
        // parando en la primera entrada, el visitante no gana nada mandando
        // «203.0.113.9, 198.51.100.7»: se lee la de la derecha, la del proxy. Lo que este numero
        // NO hace —y la primera version de este comentario lo daba por hecho— es autenticar al
        // par: quien alcance el contenedor SIN pasar por el proxy manda una sola entrada y esa se
        // aplica tal cual. El ancla de confianza es la topologia, no la cabecera. Por eso la
        // bandera de arriba hay que declararla a mano; ver SeguridadDeTransporte.
        options.ForwardLimit = SeguridadDeTransporte.SaltosDeProxyDeConfianza;

        // POR QUE SE VACIAN LAS LISTAS. Vienen pobladas con la red de bucle local, y en Azure
        // App Service el frontal no habla desde ahi ni tiene una direccion estable que se pueda
        // declarar: con las listas de serie, la cabecera se descartaria entera y los limitadores
        // por IP verian a todo el mundo en la misma particion —el limite se convertiria en la
        // caida del servicio—.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        // PERO SI ALGUIEN PUEDE NOMBRAR A SU PROXY, QUE LO NOMBRE. Detras de un nginx o un
        // balanceador propio la direccion se conoce, y declararla es lo unico que convierte esto
        // en una comprobacion real del par en vez de en una confianza documentada.
        foreach (var proxy in seguridadDeTransporte.ProxiesDeConfianza)
        {
            if (proxy.Contains('/', StringComparison.Ordinal))
            {
                var partes = proxy.Split('/', 2);
                if (System.Net.IPAddress.TryParse(partes[0], out var red)
                    && int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefijo))
                {
                    options.KnownIPNetworks.Add(new System.Net.IPNetwork(red, prefijo));
                }
            }
            else if (System.Net.IPAddress.TryParse(proxy, out var direccion))
            {
                options.KnownProxies.Add(direccion);
            }
        }
    });
}

if (seguridadDeTransporte.ExigirHttps)
{
    // SIN ESTO, UseHttpsRedirection NO REDIRIGE NADA. Busca el puerto en estas opciones, luego en
    // la clave HTTPS_PORT y por ultimo en las direcciones del servidor; detras de un proxy que
    // termina el TLS el contenedor solo escucha en claro, asi que las tres vias fallan y el
    // middleware deja pasar la peticion tras escribir un aviso. Fue un defecto real de la primera
    // version: la unica prueba que lo comprobaba pasaba porque la fabrica de pruebas inyectaba a
    // mano el puerto que produccion no tenia.
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = seguridadDeTransporte.PuertoHttps);

    builder.Services.AddHsts(options =>
    {
        options.MaxAge = seguridadDeTransporte.MaxEdadDeHsts;

        // INCLUDESUBDOMAINS QUEDA APAGADO A PROPOSITO. El dia que esto se sirva desde un
        // dominio del Ministerio, esa palabra obligaria a HTTPS a TODOS los subdominios
        // hermanos —sitios de otras dependencias, sobre los que este proyecto no manda— y
        // dejaria fuera de servicio a cualquiera que aun no tenga certificado. Es una decision
        // del dueno del dominio, no del API. Preload, por lo mismo y ademas irreversible en la
        // practica, tampoco se pide.
        options.IncludeSubDomains = false;
        options.Preload = false;
    });
}

var app = builder.Build();

// VA ANTES QUE NADA, INCLUIDA LA COMPRESION. Todo lo que viene debajo —el registro
// estructurado con su IP, los limitadores por origen, la decision de si la peticion llego
// cifrada— lee de HttpContext.Connection y de Request.Scheme. Si esto corriera despues, cada
// una de esas piezas trabajaria con los datos del proxy en vez de con los del visitante.
if (seguridadDeTransporte.DetrasDeProxyInverso)
{
    app.UseForwardedHeaders();
}

// Y ESTO, INMEDIATAMENTE DESPUES. UseHttpsRedirection decide mirando Request.IsHttps, que es
// justo lo que acaba de corregir la linea de arriba: al reves, detras de un proxy que termina
// el TLS el API veria http:// en cada peticion y redirigiria en bucle.
//
// AVISO OPERATIVO, ESCRITO PARA QUE NADIE LO DESCUBRA EN CALIENTE: con esto activo, una sonda
// que llame al contenedor DIRECTAMENTE por HTTP —sin pasar por el proxy y sin X-Forwarded-Proto—
// recibe un 307 y puede darse por caida. Si algun dia se anade esa sonda, la salida es
// Security:ExigirHttps=false, no quitar estas lineas.
if (seguridadDeTransporte.ExigirHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Lo primero que produce cuerpo: comprime lo que produzca cualquier cosa de abajo. Solo le
// preceden las piezas de transporte, que no escriben cuerpo (o escriben una redireccion vacia).
// Y por delante del ETag no hay conflicto —una respuesta 304 no lleva cuerpo, asi que no hay
// nada que comprimir—.
app.UseResponseCompression();

app.UseMiddleware<RequestContextMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
// EL FRONT LO SIRVE ESTA MISMA APLICACION, Y NO ES UNA COMODIDAD.
//
// Las tres cookies de sesion son `SameSite=Lax` y su dominio no se configura en ninguna parte
// —los valores estan literales aqui arriba—, y `environment.apiBaseUrl` del front esta VACIO en
// sus dos entornos: el navegador pide `/api/v1/...` a su propio origen. Con el sitio en un dominio
// y el API en otro, iniciar sesion responderia 200 y cada guardado 401, porque la cookie no se
// enviaria. Ese fallo ya ocurrio en desarrollo y esta escrito en `environment.ts`.
//
// VA DESPUES DE LAS CABECERAS DE SEGURIDAD a proposito: asi el `index.html` sale con
// X-Frame-Options y compania, igual que cualquier respuesta del API.
//
// En desarrollo local no hay `wwwroot` y esto no hace nada: el front lo sirve `ng serve` en el
// 4300 y su proxy reenvia `/api`. El directorio lo llena el pipeline al publicar.
app.UseDefaultFiles();
app.UseStaticFiles();

// EL ENRUTADO, EXPLICITO Y AQUI. Sin esta linea, WebApplication lo inserta al PRINCIPIO de la
// tuberia, y entonces el reparto de mas abajo ya ha elegido endpoint cuando le toca el turno a
// UseStaticFiles. Los ficheros estaticos no se sirven si ya hay endpoint elegido: el resultado es
// que CADA script y CADA imagen del front devolvian el index.html con codigo 200, y el sitio
// cargaba la pagina en lugar de sus propios recursos.
//
// Lo descubrio una prueba que pedia un fichero estatico y recibia la pagina. Ninguna de las piezas
// que quedan por encima —contexto de peticion, cabeceras de seguridad, compresion, cabeceras
// reenviadas— consulta el endpoint elegido, asi que mover el enrutado aqui no les cambia nada.
app.UseRouting();

app.UseMiddleware<GlobalExceptionMiddleware>();
await DatabaseBootstrapper.EnsureReadyAsync(app.Services);

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("PnmcWebFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ANONIMAS A PROPOSITO (1 y 2 de 39). Sondas de infraestructura: las consulta el
// orquestador (Docker/Kubernetes/el balanceador) antes de enrutar trafico y sin
// credenciales de nadie; una sonda que exige sesion se declara caida siempre y el
// contenedor entra en bucle de reinicio. No revelan datos: /live responde vacio y
// /ready solo dice si las comprobaciones registradas pasan.
// /live: ninguna comprobacion (Predicate falso). /ready: solo las etiquetadas «ready».
// Sin los predicados, las dos sondas ejecutarian el mismo conjunto y liveness
// dependeria de la base, que es justo lo que no debe pasar.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = comprobacion => comprobacion.Tags.Contains(ComprobacionDeBaseDeDatos.EtiquetaReady),
}).AllowAnonymous();

var api = app.MapGroup("/api/v1");
api.MapMapEndpoints();
api.MapParticipationEndpoints();
api.MapBoletinEndpoints();
api.MapPoliticasDeDatosEndpoints();
api.MapDivipolaPublicoEndpoints();
api.MapExternalAuthEndpoints();
api.MapExternalOrganizationEndpoints();
api.MapFestivalesExternosEndpoints();
api.MapReclamacionesAdministracionEndpoints();
// El perfil público versionado se opera mediante propuestas de cambio. Las antiguas rutas
// externas de `VersionesFestival` quedan retiradas: transportaban fechas, financiación y otras
// propiedades de una realización, que ahora pertenecen exclusivamente a EdicionesFestival.
// La tabla persiste para historial y propuestas, nunca como formulario externo de una Edición.
api.MapEdicionesFestivalExternosEndpoints();
// AQUI NO SE REGISTRA `MapVersionesFestivalExternosEndpoints`, Y ES LA DECISION DE ARRIBA.
//
// El 13 de septiembre de 2026 se registro por error, leyendo su ausencia como un olvido: el
// fichero existe, tiene 569 lineas y cinco rutas, y el frontend las llamaba. Pero el comentario
// que las retira esta UNA LINEA MAS ARRIBA, y lo que las llama es
// `ficha-festival.component.ts`, que dice «edicion» en sus mensajes mientras pide `/versiones`:
// es justo el formulario externo de Edicion que esa decision prohibe. Registrarlas hacia que ese
// camino equivocado volviera a funcionar, que es peor que el 404 —el 404 al menos se ve—.
//
// Lo que hay que arreglar es el frontend, no el registro. Las ediciones tienen sus diez rutas
// completas aqui arriba y `ediciones-temporales.component.ts` ya las usa bien.
api.MapPropuestasDeCambioFestivalExternosEndpoints();
api.MapRevisionInstitucionalFestivalesEndpoints();
api.MapRevisionInstitucionalEdicionesFestivalEndpoints();
api.MapRevisionInstitucionalEventosEndpoints();
// La devolucion campo por campo: el funcionario abre la ficha completa, anota que cambiar en cada
// campo, guarda el borrador y lo envia. Lleva las dos mitades —la institucional que escribe y la
// externa que lee— porque es una sola conversacion con dos puertas.
api.MapRevisionDeCamposFestivalEndpoints();
api.MapRevisionDeCamposEdicionFestivalEndpoints();
api.MapNormalizacionVersionesFestivalEndpoints();
api.MapRevisionInstitucionalPropuestasFestivalEndpoints();
api.MapFestivalesPublicosEndpoints();
api.MapTerritoriosSonorosPublicosEndpoints();
api.MapPracticasMusicalesPublicasEndpoints();
api.MapAnaliticaFestivalesPublicosEndpoints();
api.MapNotificationEndpoints();
api.MapRecordGovernanceEndpoints();
api.MapAdminAuthEndpoints();
// Qué módulos de la consola tiene activados cada cuenta. Permisos POR CUENTA.
api.MapPermisosDeModuloEndpoints();
api.MapAdminDataEndpoints();
api.MapAnalisisAdministrativoEndpoints();
// LA MISMA CAPACIDAD DEL OTRO LADO DE LA PUERTA: el catálogo, el intérprete y la forma de la
// respuesta son los de arriba; lo que cambia es el ámbito, que solo mira los registros de quien
// pregunta. Ver `ConsultaGuiadaDeOrganizacionEndpoints`.
api.MapConsultaGuiadaDeOrganizacionEndpoints();
api.MapCatalogoEditorialEndpoints();
api.MapNoticiasEndpoints();
api.MapMercadosExternosEndpoints();
api.MapEdicionesDeMercadoEndpoints();
api.MapMercadosEndpoints();
api.MapRevisionDeCamposDeMercadoEndpoints();
api.MapPropuestasDeCambioDeMercadoEndpoints();
api.MapAgendaEndpoints();
api.MapBancoDeArchivosEndpoints();
api.MapCategoriasDeContenidoEndpoints();
api.MapBorradoresDeConsolaEndpoints();
api.MapAltaAdministrativaEndpoints();
api.MapProyectosTransversalesEndpoints();
api.MapEventosDeOrganizacionEndpoints();
api.MapAsistenciaAOrganizacionEndpoints();
api.MapConfirmacionDeCorreoEndpoints();
api.MapImportacionAsistidaEndpoints();
api.MapAdminAuditoriaEndpoints();
api.MapAdminOrganizacionesEndpoints();
api.MapContenidoWebEndpoints();
api.MapEquipoWebEndpoints();
api.MapImportacionDeContenidoWebEndpoints();
api.MapImagenesWebEndpoints();

// LO QUE NO CASA CON NINGUNA RUTA. Son dos reglas y el orden entre ellas importa.
//
// PRIMERO `/api`: una ruta inexistente bajo el API tiene que responder 404 en JSON. Sin esta
// linea se la tragaria el reparto de abajo y `/api/v1/lo-que-sea` devolveria la PAGINA del front
// con codigo 200: el navegador recibiria HTML donde espera JSON, y cada error de ruta del cliente
// se veria como una pagina en blanco en vez de como el 404 que es. Gana sobre la regla siguiente
// por ser mas especifica, no por el orden en que se escriben.
//
// DESPUES EL RESTO: cualquier direccion la resuelve el enrutador del front. `/ecosistema/mi-panel`
// no existe como fichero, y sin esto una recarga de esa pagina daria 404.
//
// LAS DOS SON ANONIMAS Y HAY QUE DECIRLO: `FallbackPolicy` esta cerrada por omision, asi que sin
// `AllowAnonymous` el sitio entero pediria sesion para pintar su propia pantalla de entrada.
app.MapFallback("/api/{**resto}", () => Results.NotFound(new
{
    message = "La ruta solicitada no existe en el API.",
})).AllowAnonymous();

app.MapFallbackToFile("{**ruta}", "index.html").AllowAnonymous();

app.Run();

/// <summary>
/// Cupo por usuario autenticado para las rutas intensivas del CMS.
/// <para>
/// Se reparte por identidad y no por IP porque quienes editan comparten salida a
/// internet en la entidad: por IP, dos personas del mismo piso se quitarian el
/// cupo entre ellas. En pruebas no hay limite: una suite que dispara decenas de
/// solicitudes fallaria por su propia velocidad y el fallo pareceria del codigo.
/// </para>
/// </summary>
static RateLimitPartition<string> CreateCmsRateLimitPartition(
    HttpContext context,
    IHostEnvironment environment,
    int permitLimit)
{
    if (environment.IsEnvironment("Test"))
    {
        return RateLimitPartition.GetNoLimiter("test");
    }

    var partitionKey = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? context.User.Identity?.Name
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    });
}

static RateLimitPartition<string> CreateExternalAuthRateLimitPartition(HttpContext context, IHostEnvironment environment)
{
    if (environment.IsEnvironment("Test"))
    {
        return RateLimitPartition.GetNoLimiter("test");
    }

    var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    });
}

/// <summary>
/// PNMC-003. Cupo de intentos de inicio de sesion institucional, por direccion de origen.
/// <para>
/// POR QUE POR IP Y NO POR CUENTA. Bloquear por cuenta es lo que de verdad frena a quien
/// prueba contrasenas contra un correo concreto, pero regala el ataque contrario: cualquiera
/// que sepa el correo del webmaster puede dejarlo fuera de su propia consola gastandole el
/// cupo a proposito. Un limitador no puede, ademas, particionar por cuenta sin leer el correo
/// del cuerpo de la peticion, y el cuerpo todavia no esta deserializado cuando se decide la
/// particion: leerlo aqui obligaria a bufferizarlo para que el manejador pudiera volver a
/// leerlo. Se elige lo conservador: cortar por origen, que es gratis y no deja a nadie fuera
/// de su cuenta. El bloqueo por cuenta con desbloqueo (o el retardo exponencial por intento
/// fallido) es una decision de producto y queda anotada, no tomada.
/// </para>
/// <para>
/// DE DONDE SALE EL NUMERO. 20 por minuto y por IP. El limite lo marca abajo la oficina, no
/// arriba el atacante: una alcaldia entera sale a internet por una sola direccion publica, de
/// modo que el cupo no lo gasta una persona sino todo el edificio a la vez. Veinte deja entrar
/// holgadamente a una docena de personas fichando a la misma hora, varias de ellas
/// equivocandose una o dos veces, sin que la ultima se encuentre la puerta cerrada por culpa
/// de sus companeros. Por arriba, corta el ataque en seco: 20/min son 28.800 intentos al dia
/// desde una direccion, contra los millones que permite un endpoint sin tope, y frente al
/// minimo de 10 caracteres que exige AdminAuthEndpoints eso no es una via de entrada.
/// </para>
/// <para>
/// SALVEDAD HONESTA: ese razonamiento vale para las contrasenas que se crean por el API, que
/// pasan por la validacion de 10 caracteres. NO vale para las cuentas de arranque que siembra
/// DatabaseBootstrapper.EnsureLocalDevelopmentSeedAsync (admin@pnmc.local y companeras, con la
/// contrasena "admin"): contra una credencial de cinco caracteres conocida, ningun limite por
/// minuto es defensa. Eso es un hallazgo aparte y de otro fichero; queda anotado, no tapado.
/// </para>
/// <para>
/// Es deliberadamente mas holgado que "external-login" (10/min): por el canal externo entra
/// una organizacion desde su propia conexion, por este entra un municipio entero desde una.
/// </para>
/// <para>
/// EN PRUEBAS NO HAY LIMITE, igual que en las demas politicas de este fichero: la suite
/// institucional inicia sesion decenas de veces por minuto contra el mismo host y se
/// autoexpulsaria. Esa exencion es tambien una trampa —una prueba de 429 escrita contra el
/// entorno "Test" pasa en verde sin ejercitar nada—, y por eso la prueba que respalda a
/// PNMC-003 levanta el arnes en un entorno distinto, donde este limitador si corre. Ver
/// <c>Pnmc003FuerzaBrutaLoginTests</c>.
/// </para>
/// </summary>
static RateLimitPartition<string> CreateAdminLoginRateLimitPartition(HttpContext context, IHostEnvironment environment)
{
    if (environment.IsEnvironment("Test"))
    {
        return RateLimitPartition.GetNoLimiter("test");
    }

    var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 20,
        Window = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    });
}

/// <summary>
/// Comprueba en cada peticion que la cuenta de la cookie sigue activa y con el mismo rol.
/// </summary>
/// <remarks>
/// <para>
/// Es el enganche que hace efectiva la revocacion. Si la cuenta se desactivo, desaparecio o
/// cambio de rol desde que se firmo la cookie, se rechaza el principal y se cierra la sesion:
/// la peticion sigue su curso como ANONIMA, y la politica de autorizacion la convierte en el
/// 401 o el 403 que corresponda. No se responde desde aqui a proposito — este evento no es el
/// sitio donde se decide el codigo de estado.
/// </para>
/// <para>
/// UNA COOKIE SIN IDENTIFICADOR TAMBIEN SE RECHAZA. Si no hay `NameIdentifier` o no es un
/// numero, no se puede comprobar nada; dejarla pasar seria confiar en un tiquete que no se
/// puede verificar, que es justo lo que este evento existe para impedir.
/// </para>
/// <para>
/// EL CONJUNTO ENTERO DE ROLES SE COMPARA, NO EL PRIMERO. Desde el modelo vigente de
/// SIMUS una persona puede tener varios roles, y la cookie lleva un claim por cada uno. Leer
/// solo el primero —que es lo que hacia `FindFirst`— dejaria pasar el caso que mas importa:
/// a quien se le RETIRA uno de dos roles, la cookie sigue trayendo el otro en primera
/// posicion y la sesion se revalidaria como si nada hubiera cambiado. Se comparan los dos
/// conjuntos, normalizados y ordenados igual en los dos lados (RolesDeUsuario.Ordenar), para
/// que la comparacion falle por un cambio real de roles y no por el orden en que vinieron.
/// </para>
/// <para>
/// UNA CUENTA SIN NINGUN ROL TAMBIEN SE ECHA. Es un estado alcanzable desde entonces —la
/// tabla de asignacion puede quedarse vacia para alguien— y significa que esa persona ya no
/// tiene por que estar dentro. El conjunto vacio no coincide con ninguna cookie firmada, asi
/// que el rechazo sale solo de la misma comparacion.
/// </para>
/// </remarks>
static async Task RevalidarSesionAsync(CookieValidatePrincipalContext contexto)
{
    var revalidacion = contexto.HttpContext.RequestServices.GetRequiredService<RevalidacionDeSesion>();

    var identificador = contexto.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(identificador, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idUsuario))
    {
        contexto.RejectPrincipal();
        return;
    }

    var estado = await revalidacion.ObtenerAsync(idUsuario, contexto.HttpContext.RequestAborted);
    var rolesDelTiquete = string.Join(
        ',',
        RolesDeUsuario.Ordenar(
            contexto.Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value) ?? []));

    if (estado is null || !estado.Activo || estado.Huella != rolesDelTiquete)
    {
        contexto.RejectPrincipal();
        await contexto.HttpContext.SignOutAsync(contexto.Scheme.Name);
    }
}

public partial class Program;
