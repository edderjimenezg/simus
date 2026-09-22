using Microsoft.Extensions.Configuration;

namespace PNMC.Api.Security;

/// <summary>
/// Cuatro decisiones sobre el transporte, tomadas en un solo sitio: si el API esta detras de un
/// proxy inverso que reescribe la peticion, si exige HTTPS, a que puerto redirige y cuanto dura
/// el anuncio de HSTS.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE ESTE TIPO Y NO CUATRO <c>GetValue</c> SUELTOS EN <c>Program.cs</c>. Las
/// decisiones se toman juntas o salen mal. <c>UseHttpsRedirection</c> sin cabeceras de proxy
/// procesadas produce un bucle de redirecciones —el proxy termina el TLS y habla HTTP con el
/// API, que ve <c>http://</c> y redirige otra vez—; y sin puerto declarado no redirige nada, que
/// es la otra forma de fallar, esta en silencio. Tenerlas en un tipo con su propia prueba permite
/// comprobar la tabla de decision sin levantar el API.
/// </para>
/// <para>
/// DOS VALORES POR OMISION DISTINTOS, Y LA ASIMETRIA ESTA RAZONADA. <c>ExigirHttps</c> viene
/// encendido fuera de local: un despliegue que lo olvide sale protegido. <c>DetrasDeProxyInverso</c>
/// <b>no tiene valor por omision fuera de local: hay que declararlo</b>, y sin el el arranque
/// falla. El motivo es que sus dos errores no se parecen en nada:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Encendido sin proxy delante = agujero, y silencioso.</b> Cualquiera puede escribir su
/// propia <c>X-Forwarded-For</c> y con ella elegirse una particion virgen en los cinco
/// limitadores por direccion de origen. El cupo de 20 intentos por minuto de la puerta
/// institucional deja de existir y nada lo delata.
/// </description></item>
/// <item><description>
/// <b>Apagado con proxy delante = molestia, y ruidosa.</b> Todo el trafico cae en la particion
/// del proxy y los limitadores estrangulan a todo el mundo a la vez. Es un fallo de
/// disponibilidad que se nota en el primer minuto y se arregla con una variable de entorno.
/// </description></item>
/// </list>
/// <para>
/// Entre un fallo mudo y uno ruidoso se elige que falle el ruidoso; y como el API <b>no puede
/// saber</b> si tiene un proxy delante, en vez de adivinar, pregunta. Es el mismo criterio que ya
/// usa <c>Cors:AllowedOrigins</c> en <c>Program.cs</c>: fuera de local, lo que no se puede
/// deducir se exige.
/// </para>
/// <para>
/// EN LOCAL TODO ESTA APAGADO por omision (Development, Local y Test): no hay proxy delante, el
/// certificado de desarrollo no siempre esta instalado, y una redireccion a HTTPS en el puerto de
/// desarrollo deja al frontend sin API sin decir por que.
/// </para>
/// </remarks>
/// <param name="DetrasDeProxyInverso">
/// Si es cierto, se procesan <c>X-Forwarded-For</c> y <c>X-Forwarded-Proto</c>.
/// </param>
/// <param name="ExigirHttps">Si es cierto, se activan <c>UseHsts</c> y <c>UseHttpsRedirection</c>.</param>
/// <param name="PuertoHttps">Puerto al que redirige <c>UseHttpsRedirection</c>.</param>
/// <param name="DiasDeHsts">Duracion del <c>max-age</c> de la cabecera HSTS, en dias.</param>
public sealed record SeguridadDeTransporte(
    bool DetrasDeProxyInverso,
    bool ExigirHttps,
    int PuertoHttps,
    int DiasDeHsts)
{
    /// <summary>Clave de configuracion: <c>Security:DetrasDeProxyInverso</c>.</summary>
    public const string ClaveDetrasDeProxy = "Security:DetrasDeProxyInverso";

    /// <summary>Clave de configuracion: <c>Security:ExigirHttps</c>.</summary>
    public const string ClaveExigirHttps = "Security:ExigirHttps";

    /// <summary>Clave de configuracion: <c>Security:PuertoHttps</c>.</summary>
    public const string ClavePuertoHttps = "Security:PuertoHttps";

    /// <summary>Clave de configuracion: <c>Security:HstsDias</c>.</summary>
    public const string ClaveDiasDeHsts = "Security:HstsDias";

    /// <summary>
    /// Un ano. Es el minimo que exige la lista de precarga de los navegadores y el valor que
    /// piden las guias de endurecimiento; por debajo, la cabecera existe pero no protege a
    /// quien no vuelva a entrar en unos dias.
    /// </summary>
    public const int DiasDeHstsPorOmision = 365;

    /// <summary>
    /// A donde redirige <c>UseHttpsRedirection</c> cuando nadie dice otra cosa.
    /// </summary>
    /// <remarks>
    /// HAY QUE DECLARARLO O LA REDIRECCION NO EXISTE, y este fue un defecto real de la primera
    /// version de este fichero. <c>UseHttpsRedirection</c> busca el puerto en sus opciones, luego
    /// en la clave <c>HTTPS_PORT</c> y por ultimo en las direcciones que publica el servidor. En
    /// la topologia que este proyecto va a usar —el proxy termina el TLS y habla HTTP con el
    /// contenedor— <b>no hay ninguna direccion https</b>, de modo que las tres vias fallan y el
    /// middleware se limita a registrar un aviso y dejar pasar la peticion. Es decir:
    /// <c>ExigirHttps=true</c> no habria redirigido nada, y la prueba que lo comprobaba solo
    /// pasaba porque la propia fabrica de pruebas inyectaba el puerto que produccion no tenia.
    /// El peor verde posible. Ahora el puerto se declara aqui y viaja al middleware.
    /// </remarks>
    public const int PuertoHttpsPorOmision = 443;

    /// <summary>
    /// Cuantas entradas de <c>X-Forwarded-For</c> se leen, empezando por la derecha.
    /// </summary>
    /// <remarks>
    /// <para>
    /// QUE HACE Y QUE NO HACE, DICHO CON PRECISION, porque la primera version de este comentario
    /// se pasaba de largo. Con las listas de proxies vacias, el middleware <b>no comprueba quien
    /// es el par TCP</b>: solo recorta la cadena. Este limite impide que el visitante
    /// <b>anteponga</b> entradas —mandar <c>203.0.113.9, 198.51.100.7</c> no le sirve de nada,
    /// porque se lee la de la derecha, que es la que escribio el proxy—.
    /// </para>
    /// <para>
    /// <b>NO impide que escriba la unica entrada.</b> Si alguien alcanza el contenedor sin pasar
    /// por el proxy y manda <c>X-Forwarded-For: 203.0.113.1</c>, esa direccion se aplica tal cual.
    /// El ancla de confianza no es la cabecera ni este numero: <b>es la topologia</b> —que al API
    /// solo se llegue a traves del proxy—. Por eso la bandera hay que declararla a mano y por eso
    /// existe <see cref="ProxiesDeConfianza"/>: quien SI pueda nombrar a su proxy debe nombrarlo,
    /// y entonces si se comprueba el par.
    /// </para>
    /// </remarks>
    public const int SaltosDeProxyDeConfianza = 1;

    /// <summary>
    /// Direcciones o redes del proxy, si se conocen: <c>Security:ProxiesDeConfianza:0</c>.
    /// </summary>
    /// <remarks>
    /// Es el unico mecanismo que de verdad autentica al par. En Azure App Service el frontal no
    /// tiene una direccion estable que se pueda declarar y hay que confiar en que la plataforma no
    /// expone el contenedor; detras de un nginx o un balanceador propio, en cambio, la direccion
    /// se conoce y declararla convierte una debilidad documentada en una comprobacion real. Cada
    /// entrada es una IP (<c>10.0.0.4</c>) o una red en notacion CIDR (<c>10.0.0.0/24</c>).
    /// </remarks>
    public const string ClaveProxiesDeConfianza = "Security:ProxiesDeConfianza";

    /// <summary>Direcciones y redes declaradas como proxy. Vacia = no se comprueba el par.</summary>
    public IReadOnlyList<string> ProxiesDeConfianza { get; init; } = [];

    /// <summary>Lee las decisiones de la configuracion, con el entorno como valor por omision.</summary>
    /// <exception cref="InvalidOperationException">
    /// Fuera de local, si <c>Security:DetrasDeProxyInverso</c> no esta declarado. Ver el porque en
    /// la nota del tipo.
    /// </exception>
    public static SeguridadDeTransporte Leer(IConfiguration configuracion, bool esEntornoLocal)
    {
        ArgumentNullException.ThrowIfNull(configuracion);

        var detrasDeProxy = configuracion.GetValue<bool?>(ClaveDetrasDeProxy);
        if (detrasDeProxy is null)
        {
            if (!esEntornoLocal)
            {
                throw new InvalidOperationException(
                    $"{ClaveDetrasDeProxy} no esta declarado. Fuera de Development/Local/Test hay que "
                    + "decir de forma explicita si el API tiene un proxy inverso delante, porque el API "
                    + "no puede averiguarlo y las dos suposiciones son peligrosas: darlo por cierto sin "
                    + "proxy deja que cualquiera se invente su direccion de origen y anule los limites "
                    + "de tasa; darlo por falso con proxy mete a todo el mundo en la misma particion y "
                    + "estrangula el servicio. Declara la variable de entorno "
                    + "Security__DetrasDeProxyInverso=true o =false.");
            }

            detrasDeProxy = false;
        }

        var exigirHttps = configuracion.GetValue<bool?>(ClaveExigirHttps) ?? !esEntornoLocal;
        var puerto = configuracion.GetValue<int?>(ClavePuertoHttps) ?? PuertoHttpsPorOmision;
        var dias = configuracion.GetValue<int?>(ClaveDiasDeHsts) ?? DiasDeHstsPorOmision;

        // El tope de dos anos es el que aceptan los navegadores; el suelo de cero permite
        // desactivar el anuncio sin desactivar la redireccion, que es como se retira HSTS de un
        // dominio sin dejar tirado a quien ya lo tenga memorizado.
        return new SeguridadDeTransporte(
            detrasDeProxy.Value,
            exigirHttps,
            Math.Clamp(puerto, 1, 65535),
            Math.Clamp(dias, 0, 730))
        {
            ProxiesDeConfianza = configuracion.GetSection(ClaveProxiesDeConfianza).Get<string[]>() ?? [],
        };
    }

    /// <summary>Duracion del <c>max-age</c> ya como intervalo.</summary>
    public TimeSpan MaxEdadDeHsts => TimeSpan.FromDays(DiasDeHsts);
}
