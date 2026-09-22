using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-056. El valor por defecto de la autorizacion del API.
/// <para>
/// LA REGLA: de los 126 endpoints mapeados, 40 se servian sin autenticar y ni uno solo lo
/// hacia a proposito — no habia un solo <c>AllowAnonymous</c> escrito en todo el repositorio.
/// (De aquellas 40, las tres de <c>/admin/ally-requests</c> ya se cerraron aparte; quedan 37 en
/// <c>Endpoints/</c> mas las 2 sondas de salud de <c>Program.cs</c>, que el censo no contaba.)
/// La causa era mecanica: <c>AddAuthorization</c> declaraba InstitutionalPolicy y ExternalPolicy
/// pero no <c>FallbackPolicy</c>, y sin ella ASP.NET Core sirve sin autenticar cualquier endpoint
/// que no diga nada. El silencio del programador equivalia a "abierto".
/// </para>
/// <para>
/// POR QUE ESTAS PRUEBAS Y NO UNA BATERIA DE 401: comprobar ruta por ruta que la protegida
/// responde 401 deja intacto el problema real, que no son las rutas de hoy sino la numero 127.
/// Un endpoint nuevo al que se le olvide la autorizacion nace abierto y nadie se entera: no
/// rompe ninguna prueba, no aparece en ningun registro, no falla en produccion. Lo que se fija
/// aqui es el DEFECTO del sistema, en sus dos mitades:
/// </para>
/// <list type="number">
/// <item><description>
/// <see cref="El_valor_por_defecto_del_API_es_cerrado"/> — que exista FallbackPolicy y que
/// deniegue al anonimo. Es el mecanismo: sin ella, el olvido se sirve abierto.
/// </description></item>
/// <item><description>
/// <see cref="Toda_ruta_declara_si_exige_sesion_o_si_es_anonima"/> — el barrido de
/// <see cref="EndpointDataSource"/>. Es la red: obliga a que cada endpoint diga en voz alta cual
/// de las dos cosas es, y convierte el olvido futuro en una prueba roja en vez de en un agujero
/// mudo. Este es el entregable de verdad.
/// </description></item>
/// </list>
/// <para>
/// NOTA SOBRE EL ENTORNO "Test": ninguna de estas pruebas depende de <c>IsEnvironment("Test")</c>
/// ni de rol ni de identidad, asi que la puerta trasera de entorno que vivia en
/// <c>AdminAllyEndpoints.CanReviewAllies</c> (PNMC-011, ya retirada) no habria podido teñirlas de
/// verde: lo que miden son metadatos de ruta y la politica registrada en el contenedor, que son
/// identicos en todos los entornos.
/// </para>
/// </summary>
public sealed class AutorizacionPorDefectoTests : IClassFixture<TestWebApplicationFactory>
{
    /// <summary>
    /// Las rutas anonimas declaradas hoy: 41 en <c>Endpoints/</c> mas las 2 sondas de salud de
    /// <c>Program.cs</c>. Cada una lleva su motivo escrito al lado de su <c>.AllowAnonymous()</c>.
    /// <para>
    /// Fijar el numero no es decoracion: es lo que impide que el agujero se reabra en silencio
    /// por el otro lado. El barrido de metadatos obliga a DECLARAR; este recuento obliga a
    /// JUSTIFICAR, porque abrir una ruta nueva ya no se puede hacer sin tocar esta prueba y
    /// explicar por que sube el numero.
    /// </para>
    /// </summary>
    /// <remarks>
    /// SUBIO A 40 EL 25 DE AGOSTO DE 2026, y aqui queda por que. La nueva es
    /// <c>GET /api/v1/escenarios</c>, el directorio publico del sexto proceso del ecosistema
    /// (). Es anonima por la misma razon que las otras cuatro de su grupo —festivales,
    /// escuelas de musica, mercados musicales, redes de documentacion y luteria—: son el
    /// directorio que el sitio abierto y el geovisor consultan sin cuenta, y cerrarla dejaria una
    /// pagina publica en blanco sin proteger nada de nadie.
    /// <para>
    /// LO QUE SIRVE Y LO QUE NO. Solo filas en estado publicado, y solo el contacto que el propio
    /// escenario publica a proposito —correo, telefono, direccion, redes—, como los otros cuatro
    /// directorios. No hay ni un campo <c>Responsable*</c> en su proyeccion, y de eso no depende
    /// la buena voluntad de quien la escribio: la vigila el barrido de
    /// <c>FichaPublicaSinDatosPersonalesTests</c>, que la recorre sola por leerse de
    /// <c>EndpointDataSource</c>.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// 25 ago 2026: BAJA DE 40 A 39. Se retiro UNA ruta anonima del alta externa.
    /// <c>POST /external/auth/verify-email</c> canjeaba un codigo que ningun remitente enviaba —no
    /// hay una sola linea de envio de correo en toda la API— y que solo viajaba en la respuesta
    /// bajo Development, Local y Test; caducaba a los veinte minutos dejando el correo inservible.
    /// Con el alta convertida en un solo acto y la cuenta naciendo activa, ese paso no protegia
    /// nada. Un trinquete que baja es la unica clase de movimiento que no hay que justificar dos
    /// veces, pero se deja escrito igual: si vuelve a subir, es porque alguien abrio algo.
    /// </remarks>
    /// <remarks>
    /// 27 ago 2026: SUBE DE 39 A 41. El API pasa a servir tambien el front, y eso anade dos rutas
    /// de reparto que tienen que ser anonimas por fuerza:
    /// <list type="bullet">
    ///   <item><c>{**ruta}</c> devuelve el <c>index.html</c>. Es la pantalla de entrada del sitio:
    ///         exigirle sesion significa que nadie puede llegar nunca a iniciarla.</item>
    ///   <item><c>/api/{**resto}</c> devuelve el 404 del API en JSON. Sin ella, la regla de arriba
    ///         se tragaria las rutas inexistentes del API y devolveria la PAGINA con codigo 200,
    ///         de modo que el cliente recibiria HTML donde espera JSON.</item>
    /// </list>
    /// Ninguna de las dos devuelve dato alguno, y las dos quedan barridas por
    /// <c>FichaPublicaSinDatosPersonalesTests</c>, que desde hoy sabe invocar una ruta
    /// comodin.
    /// </remarks>
    /// <remarks>
    /// 28 ago 2026, rama del mapa: DOS MAS. El geovisor deja de descargar el TopoJSON monolitico de
    /// 28 MB con los 1.122 municipios y pide dos cosas por separado, asi que hay dos rutas
    /// anonimas nuevas en <c>MapEndpoints</c>:
    /// <list type="bullet">
    ///   <item><c>/api/v1/mapa/cartografia/departamentos</c>, los 33 departamentos de la vista inicial.</item>
    ///   <item><c>/api/v1/mapa/cartografia/departamentos/{codigoDepartamento}/municipios</c>, el fragmento
    ///         municipal de un solo departamento, que se pide al abrirlo.</item>
    /// </list>
    /// Las dos sirven fronteras administrativas del DANE, no datos del PNMC, igual que la
    /// <c>/topojson/territories</c> que ya era anonima y que se conserva para clientes anteriores.
    /// Cerrarlas deja el mapa publico en blanco y no protege el dato de ninguna persona.
    /// </remarks>
    /// <remarks>
    /// 28 ago 2026, rama de la sesion externa: DOS MAS. Las dos nuevas son del boletin de la portada, y las dos
    /// tienen que ser anonimas porque el formulario esta en el sitio abierto, antes de que exista
    /// ninguna cuenta:
    /// <list type="bullet">
    ///   <item><c>GET /publico/boletin/politica</c> devuelve el texto de la autorizacion de
    ///         tratamiento de datos y su version. Es lo que la persona lee ANTES de aceptar, asi
    ///         que exigirle sesion equivale a pedirle que se registre para poder leer a que se
    ///         registraria. No consulta la base ni devuelve dato de nadie: es una constante.</item>
    ///   <item><c>POST /publico/boletin/suscripciones</c> da de alta un correo. Lleva
    ///         <c>RequireRateLimiting("boletin-alta")</c> —diez por minuto y por direccion de
    ///         origen— y rechaza con 400 el correo mal formado y la peticion sin autorizacion
    ///         expresa.</item>
    /// </list>
    /// <para>
    /// LA DE ALTA NO DISTINGUE ENTRE CORREO NUEVO Y CORREO YA APUNTADO: responde 202 con el mismo
    /// cuerpo en los dos casos. Si respondiera distinto, esta ruta anonima seria un comprobador
    /// abierto de quien esta en la lista, que es exactamente la fuga que un formulario publico de
    /// boletin suele regalar.
    /// </para>
    /// <para>
    /// NINGUNA DE LAS DOS LEE LA LISTA. Consultarla, exportarla y dar de baja viven bajo
    /// <c>/admin/comunicaciones/boletin</c> con la politica institucional, y por eso no aparecen
    /// aqui.
    /// </para>
    /// <para>
    /// LAS DOS ULTIMAS SON LAS IMAGENES DEL SITIO (29 ago 2026).
    /// <c>GET /imagenes-web</c> es el manifiesto que el front pide al arrancar cualquier pagina, y
    /// <c>GET /imagenes-web/{clave}</c> es la URL que va dentro de cada <c>&lt;img src&gt;</c>.
    /// Exigir sesion en la primera dejaria el sitio con sus imagenes compiladas para todo
    /// visitante; en la segunda romperia todas las imagenes. Ninguna de las dos filtra por
    /// sesion: filtran por lo PUBLICADO, que es lo que las hace seguras.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// 29 ago 2026: QUEDA EN 45 AL REINTEGRAR EL MAPA, y esta nota existe porque el numero no
    /// se dedujo, se midio. Las dos ramas habian subido el techo de 41 a 43 por su cuenta y por
    /// motivos distintos —dos rutas de cartografia en una, dos del boletin en la otra—, asi que
    /// el conflicto de git ofrecia elegir un 43 o el otro. Los dos habrian pasado por
    /// «resolucion razonable» y los dos habrian dejado dos rutas anonimas sin declarar, que es
    /// justo lo que este trinquete existe para impedir.
    /// </remarks>
    /// <remarks>
    /// 29 ago 2026, AL DEVOLVER EL MAPA AL PROYECTO: QUEDA EN 47. Se juntaron tres sumas que
    /// venian por caminos distintos y que ninguna de las tres conocia a las otras dos:
    /// <list type="bullet">
    ///   <item>+2 del boletin, confirmadas en la rama de la sesion externa.</item>
    ///   <item>+2 de cartografia, confirmadas en la rama del mapa.</item>
    ///   <item>+2 de las imagenes del sitio, que estaban sin confirmar.</item>
    /// </list>
    /// 41 + 2 + 2 + 2 = 47. El numero no se escribio de cabeza: se dejo que esta misma prueba
    /// contara las rutas anonimas de verdad y se ajusto a lo que conto.
    /// </remarks>
    /// <remarks>
    /// 30 ago 2026: queda en 48. Una sola ruta nueva,
    /// <c>GET /api/v1/publico/festivales/{festivalId}/ediciones</c>, que sirve las ediciones
    /// publicadas de un Festival y alimenta la tarjeta de ediciones de su ficha publica.
    /// <para>
    /// LAS DOS PREGUNTAS QUE ESTE TRINQUETE OBLIGA A CONTESTAR, contestadas:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     <b>¿La llama el frontend antes de que exista sesion?</b> Si. Es la ficha publica de
    ///     <c>/ecosistema/festivales/{id}</c>, que no pide cuenta y es el destino de todo el
    ///     circuito de revision.
    ///   </item>
    ///   <item>
    ///     <b>¿Devuelve datos que exijan sesion?</b> No, y esta vez por construccion y no por
    ///     disciplina: responde <c>EdicionFestivalPublicaDto</c>, un record que NO TIENE
    ///     propiedades de correo, telefono ni director. Su equivalente con sesion
    ///     —<c>FichaVersionFestivalDto</c>— si las tiene, y son tipos distintos a proposito.
    ///     Ademas filtra por lo publicado con el mismo criterio que la ficha
    ///     (<c>LecturaFestivalesPublicados.VersionesRepudiadasAsync</c>), de modo que una edicion
    ///     nacida de una propuesta rechazada no sale por aqui —que seria PNMC-054 otra vez, por
    ///     la puerta de al lado—. Lo vigila <c>FichaPublicaSinDatosPersonalesTests</c>.
    ///   </item>
    /// </list>
    /// </remarks>
    // 7 sep 2026: queda en 29 después de retirar las APIs públicas de contenidos sin
    // modelo vigente. GET /publico/divipola es el único catálogo territorial canónico:
    // lo consumen el alta sin sesión, los formularios autenticados y el geovisor. No contiene
    // información de personas ni registros del PNMC; abrirlo evita duplicar la misma consulta bajo
    // tres permisos y es condición para que el registro pueda elegir departamento y municipio.
    // 11 sep 2026: sube a 35 con las dos rutas públicas del Catálogo Editorial —el listado y la
    // ficha por código—. Se abren a propósito y con una puerta estrecha: `EsVisiblePublicamente`
    // exige a la vez ficha validada, estado publicado, al menos una fuente y derechos que
    // autoricen publicar la ficha, y la consulta de la lista repite esas cuatro condiciones en
    // SQL. Un archivo restringido NO viaja aunque su ficha sí sea pública: son dos autorizaciones
    // distintas. La ficha por código responde 404 cuando no es visible, el mismo que si no
    // existiera, para que esta ruta anónima no sirva para averiguar qué hay sin publicar.
    // Lo vigila `CatalogoEditorialCircuitoTests`.
    // 11 sep 2026: sube a 37 con las dos rutas públicas de Noticias —el listado y la noticia por
    // dirección—. La puerta es `ReglasDeNoticias.EsVisiblePublicamente`, escrita una sola vez:
    // estado publicado Y fecha de publicación llegada. Una noticia fechada el lunes NO viaja el
    // viernes, que es lo que convertiría la fecha en decoración. La noticia por dirección responde
    // 404 cuando no es visible —el mismo que si no existiera— para que esta ruta anónima no sirva
    // para averiguar el titular de algo que aún no se ha anunciado.
    // Lo vigila `NoticiasCircuitoTests`.
    // 11 sep 2026: sube a 39 con las dos rutas públicas de la Agenda —el listado y el evento por
    // dirección—. Aquí la puerta es MAS SIMPLE que en Noticias a propósito: basta el estado
    // publicado, SIN corte por fecha. Lo que ya ocurrió sigue siendo información pública y el
    // portal lo presenta como finalizado en vez de esconderlo; quien busca qué hubo el año pasado
    // en el Cauca tiene tanto derecho a encontrarlo como quien busca qué viene. El evento por
    // dirección responde 404 cuando no está publicado —el mismo que si no existiera— para que esta
    // ruta anónima no revele que se está preparando algo sin anunciar.
    // Lo vigila `AgendaCircuitoTests`.
    // 11 sep 2026: sube a 40 con `GET /publico/archivos/{id}`, que sirve los bytes del banco de
    // archivos. SE ABRE A PROPOSITO: un cartel vinculado a un evento publicado, una portada de
    // noticia o la miniatura de una publicación tienen que poder verlos quienes entran al portal
    // sin sesión, y exigirla convertiría cada imagen en un 401. NO EXPONE MAS QUE LOS BYTES: la
    // ruta devuelve el contenido y su tipo, no quién lo subió ni a qué está vinculado, y para eso
    // está `GET /institucional/archivos/{id}`, que sí exige sesión. La SUBIDA nunca fue anónima.
    // Lo vigila `BancoDeArchivosTests`.
    // 11 sep 2026: sube a 41 con `GET /publico/categorias-contenido`. SE ABRE A PROPOSITO: el
    // portal construye con ella las barras de filtro de Agenda, Noticias y Catálogo Editorial
    // ANTES de que exista sesión, y exigirla dejaría esas tres pantallas sin filtros para quien
    // solo entra a leer. NO EXPONE NADA QUE NO ESTE YA A LA VISTA: devuelve el nombre y el orden
    // de unas categorías que se leen en cada tarjeta publicada. Administrarlas —crear, renombrar,
    // borrar— sigue exigiendo sesión institucional.
    // Lo vigila `CategoriasDeContenidoTests`.
    //
    // 42 (11 sep 2026): `GET /publico/proyectos-transversales`. Misma razón que las categorías: el
    // portal de una iniciativa —Celebra la Música— arma su calendario y su sala de prensa ANTES de
    // que exista sesión, y esta ruta no devuelve más que el nombre y la dirección de un proyecto
    // que ya es público. Administrarlos —crear, renombrar, cerrar— sigue exigiendo sesión
    // institucional. Lo vigila `ProyectosTransversalesTests`.
    //
    // 44 y 45 (12 sep 2026): `GET /publico/politicas` y `GET /publico/politicas/{clave}`, los
    // textos de autorización de datos. SE ABREN PORQUE LA LEY LO OBLIGA: el art. 12 de la Ley 1581
    // de 2012 exige informar al titular de las finalidades y de sus derechos ANTES de pedirle la
    // autorización, y quien va a registrarse todavía no tiene cuenta. Detrás de una sesión, esa
    // información llegaría después de haberla dado, que es justo lo que la norma impide. NO
    // EXPONEN NADA DE NADIE: devuelven el texto que el sistema publica para que cualquiera lo lea,
    // no qué autorizó ninguna persona —eso es `/externo/mis-autorizaciones`, que sí exige sesión—.
    // Lo vigila `PoliticasDeDatosTests`.
    //
    // 46 y 47 (15 sep 2026): `GET /publico/mercados` y `GET /publico/mercados/{id}`, el directorio
    // público de Mercados Musicales y la consulta de uno. SE ABREN POR LA MISMA RAZON QUE LAS DE
    // FESTIVALES: el geovisor y la página del Ecosistema los piden antes de que exista ninguna
    // sesión, y quien entra a mirar el país musical no tiene por qué tener cuenta. NO EXPONEN
    // TRABAJO INTERNO: solo entregan los mercados en estado «publicado», de modo que un borrador o
    // algo en revisión —que es conversación entre una organización y el Programa— no sale por aquí.
    // Lo vigila `MercadosMusicalesTests`.
    //
    // 48 y 49 (15 sep 2026): `GET /publico/mercados/{id}/ediciones` y
    // `GET /publico/festivales/{id}/mercados`. La primera son las ediciones PUBLICADAS de un mercado
    // publicado —una edición en borrador es trabajo de la organización sobre algo que todavía no
    // ha decidido anunciar, y no sale—. La segunda es la relación Mercado ↔ Festival leída desde el
    // festival: sin ella solo se puede recorrer en un sentido, que es la mitad de una relación. Las
    // dos exigen que el registro padre esté publicado, para que un identificador no revele por su
    // respuesta si existe algo que el público aún no puede ver. Lo vigila `MercadosMusicalesTests`.
    private const int RutasAnonimasDeclaradas = 49;

    private readonly TestWebApplicationFactory _factory;

    public AutorizacionPorDefectoTests(TestWebApplicationFactory factory) => _factory = factory;

    // ---------- Andamio ----------------------------------------------------------

    /// <summary>
    /// Todos los endpoints que el servidor tiene realmente montados.
    /// <para>
    /// Se leen de <see cref="EndpointDataSource"/> y no de una lista escrita a mano justamente
    /// para que una ruta nueva entre sola en el barrido: una lista a mano solo vigila lo que
    /// alguien se acordo de apuntar, que es el mismo descuido que se esta corrigiendo.
    /// </para>
    /// </summary>
    private List<Endpoint> LeerRutasMontadas()
    {
        // Crear el cliente arranca el host; sin eso el contenedor todavia no ha construido la
        // canalizacion y las fuentes de rutas estarian vacias.
        using var arranque = _factory.CreateClient();

        var rutas = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(fuente => fuente.Endpoints)
            .Distinct()
            .ToList();

        // Canario. Una prueba de barrido que barre cero rutas pasa siempre y no vigila nada, que
        // es peor que no tenerla: da una falsa sensacion de red. Si esto salta, lo roto es la
        // lectura de EndpointDataSource, no el API.
        Assert.True(
            rutas.Count >= 100,
            $"El barrido solo vio {rutas.Count} rutas y el API monta mas de 120. " +
            "La enumeracion de EndpointDataSource dejo de funcionar; arreglese la lectura antes " +
            "de fiarse de esta prueba, porque en este estado no vigila nada.");

        return rutas;
    }

    private static string Nombrar(Endpoint ruta)
        => ruta.DisplayName ?? (ruta as RouteEndpoint)?.RoutePattern.RawText ?? ruta.ToString() ?? "(sin nombre)";

    private static bool EsAnonimaDeclarada(Endpoint ruta)
        => ruta.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    private static bool ExigeAutorizacion(Endpoint ruta)
        => ruta.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;

    // ---------- 1. El mecanismo --------------------------------------------------

    /// <summary>
    /// SIN EL PARCHE: <c>AddAuthorization</c> nunca asignaba <c>options.FallbackPolicy</c>, de modo
    /// que <c>DefaultAuthorizationPolicyProvider.GetFallbackPolicyAsync()</c> devuelve
    /// <c>null</c> y el <c>Assert.NotNull</c> falla. Con el parche devuelve una politica que exige
    /// usuario autenticado.
    /// <para>
    /// Lo que fija es el comportamiento del middleware ante un endpoint sin metadatos:
    /// <c>AuthorizationMiddleware</c> combina los metadatos de la ruta y, cuando no hay ninguno,
    /// cae en esta politica. Nula significaba "pasa sin autenticar"; con el requisito
    /// <c>DenyAnonymousAuthorizationRequirement</c> significa 401.
    /// </para>
    /// </summary>
    [Fact]
    public async Task El_valor_por_defecto_del_API_es_cerrado()
    {
        using var arranque = _factory.CreateClient();
        var proveedor = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var porDefecto = await proveedor.GetFallbackPolicyAsync();

        Assert.True(
            porDefecto is not null,
            "No hay FallbackPolicy declarada. Sin ella, un endpoint que no diga nada sobre " +
            "autorizacion se sirve sin autenticar: el silencio vuelve a significar 'abierto'.");
        Assert.Contains(porDefecto!.Requirements, requisito => requisito is DenyAnonymousAuthorizationRequirement);
    }

    // ---------- 2. La red --------------------------------------------------------

    /// <summary>
    /// EL ENTREGABLE. Recorre las rutas montadas y exige que cada una declare una de las dos cosas:
    /// autorizacion (<c>IAuthorizeData</c>, que pone <c>RequireAuthorization</c> tanto en el grupo
    /// como en el endpoint) o anonimato expreso (<c>IAllowAnonymous</c>).
    /// <para>
    /// SIN EL PARCHE FALLA CON 39 RUTAS EN LA LISTA: las 37 publicas de <c>Endpoints/</c> mas las
    /// dos sondas de salud no tenian ninguno de los dos metadatos. Eran anonimas por descuido, que
    /// es precisamente lo que esta prueba prohibe. Con el parche cada una lleva su
    /// <c>.AllowAnonymous()</c> y su motivo escrito.
    /// </para>
    /// <para>
    /// POR QUE NO BASTABA LA FallbackPolicy SOLA: la FallbackPolicy cierra el olvido, pero lo cierra
    /// en silencio y a la primera peticion real. Esta prueba lo dice antes, en la suite, y ademas
    /// obliga a escribir el motivo: si la respuesta correcta era abrir la ruta, hay que teclear
    /// <c>AllowAnonymous</c> y justificarlo; si era cerrarla, ya estaba cerrada.
    /// </para>
    /// </summary>
    [Fact]
    public void Toda_ruta_declara_si_exige_sesion_o_si_es_anonima()
    {
        var mudas = LeerRutasMontadas()
            .Where(ruta => !ExigeAutorizacion(ruta) && !EsAnonimaDeclarada(ruta))
            .Select(Nombrar)
            .OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            mudas.Count == 0,
            $"Hay {mudas.Count} ruta(s) que no dicen nada sobre autorizacion. Ninguna ruta puede " +
            "quedarse callada: o lleva RequireAuthorization (en el endpoint o en su grupo) o lleva " +
            "AllowAnonymous con el motivo escrito al lado. Si es publica a proposito, escribalo; " +
            "si no lo es, ciérrela." + Environment.NewLine +
            string.Join(Environment.NewLine, mudas));
    }

    /// <summary>
    /// La otra mitad del descuido, la que el barrido no ve: un <c>AllowAnonymous</c> QUE SOBRA.
    /// <para>
    /// Es el error inverso y el mas peligroso, porque no rompe nada visible. Basta que alguien
    /// añada <c>.AllowAnonymous()</c> por costumbre a un endpoint de un grupo <c>/admin/*</c> para
    /// que la autorizacion del grupo quede anulada — cuando conviven los dos metadatos,
    /// <c>AuthorizationMiddleware</c> salta la autorizacion entera— y la consola pase a servirse a
    /// cualquiera. Aqui no hay ningun caso legitimo de convivencia: hoy son cero.
    /// </para>
    /// </summary>
    [Fact]
    public void Ninguna_ruta_se_declara_anonima_y_autorizada_a_la_vez()
    {
        var contradictorias = LeerRutasMontadas()
            .Where(ruta => ExigeAutorizacion(ruta) && EsAnonimaDeclarada(ruta))
            .Select(Nombrar)
            .OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            contradictorias.Count == 0,
            $"Hay {contradictorias.Count} ruta(s) con AllowAnonymous y RequireAuthorization a la vez. " +
            "Gana AllowAnonymous: la autorizacion no se ejecuta y la ruta queda abierta aunque su " +
            "grupo parezca cerrado. Retire uno de los dos." + Environment.NewLine +
            string.Join(Environment.NewLine, contradictorias));
    }

    /// <summary>
    /// El inventario. Cuantas rutas anonimas hay, no cuales — el "cuales" vive en el codigo, junto
    /// a cada <c>AllowAnonymous</c>, que es donde se lee.
    /// <para>
    /// SIN EL PARCHE tambien falla, y por el lado revelador: sin un solo <c>AllowAnonymous</c> en el
    /// repositorio el recuento es 0 frente a 39, aunque 39 rutas se estuvieran sirviendo abiertas.
    /// Esa distancia entre "39 abiertas" y "0 declaradas" es exactamente el defecto.
    /// </para>
    /// <para>
    /// Cuando alguien abra una ruta nueva, esta prueba se pondra roja. Es deliberado: subir el
    /// numero es barato y obliga a mirar la lista una vez mas, que es todo lo que se pide.
    /// </para>
    /// </summary>
    [Fact]
    public void El_numero_de_rutas_anonimas_no_crece_sin_que_alguien_lo_escriba()
    {
        var anonimas = LeerRutasMontadas()
            .Where(EsAnonimaDeclarada)
            .Select(Nombrar)
            .OrderBy(nombre => nombre, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            anonimas.Count == RutasAnonimasDeclaradas,
            $"Se esperaban {RutasAnonimasDeclaradas} rutas anonimas y hay {anonimas.Count}. Si acaba " +
            "de abrir una, compruebe que el frontend la llama antes de que exista sesion y que no " +
            "devuelve datos que exijan sesion; despues actualice RutasAnonimasDeclaradas. Si acaba " +
            "de cerrar una, bajelo." + Environment.NewLine +
            string.Join(Environment.NewLine, anonimas));
    }

    // ---------- 3. Que no se haya cerrado de mas ---------------------------------

    /// <summary>
    /// La contrapartida: cerrar de mas rompe al ciudadano en vez de al atacante, y lo hace en
    /// silencio porque la pagina publica simplemente se queda vacia. Estas son las rutas que el
    /// sitio abierto y el geovisor piden SIN sesion; si alguna empieza a responder 401 o 403,
    /// alguien se llevo por delante una pagina publica.
    /// <para>
    /// Se comprueba "ni 401 ni 403" y no "200" a proposito: lo que esta bajo prueba es el acceso,
    /// no el contenido. Atar estas rutas a un 200 exacto meteria en esta prueba de autorizacion
    /// fallos de datos sembrados que no son suyos.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/api/v1/contenido-web")]
    [InlineData("/api/v1/equipo-web")]
    [InlineData("/api/v1/agenda/events?limit=5&offset=0")]
    [InlineData("/api/v1/news/articles?limit=5&offset=0")]
    [InlineData("/api/v1/editorial/resources?limit=5&offset=0")]
    [InlineData("/api/v1/gallery/albums")]
    [InlineData("/api/v1/publico/festivales?limit=5&offset=0")]
    [InlineData("/api/v1/publico/analitica/festivales/resumen")]
    [InlineData("/api/v1/mapa/resumen?layer=General")]
    [InlineData("/api/v1/mapa/departamentos/05/detalle")]
    [InlineData("/api/v1/mapa/cartografia/territorios")]
    [InlineData("/api/v1/mapa/cartografia/departamentos")]
    [InlineData("/api/v1/mapa/cartografia/departamentos/05/municipalities")]
    [InlineData("/api/v1/map/geojson/departments")]
    [InlineData("/api/v1/map/geojson/municipalities")]
    public async Task El_sitio_publico_sigue_alcanzable_sin_sesion(string ruta)
    {
        var cliente = _factory.CreateClient();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.False(
            respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"{ruta} respondio {(int)respuesta.StatusCode} sin sesion. Es una ruta del sitio publico " +
            "o del geovisor: cerrarla no protege ningun dato de nadie, deja la pagina en blanco " +
            "para todo visitante.");
    }

    /// <summary>
    /// El contraste que impide leer la prueba anterior como "aqui todo esta abierto". La misma
    /// coleccion de participacion tiene el reparto escrito en el codigo: escribir la ficha es
    /// publico, leer el expediente —con nombres y correos de terceros— es institucional. Si esta
    /// prueba se pusiera verde con un 200, la de arriba habria dejado de significar nada.
    /// </summary>
    [Fact]
    public async Task Lo_que_exige_sesion_la_sigue_exigiendo()
    {
        var cliente = _factory.CreateClient();

        var expediente = await cliente.GetAsync("/api/v1/participaciones?limit=5&offset=0");
        var consola = await cliente.GetAsync("/api/v1/admin/data/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, expediente.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, consola.StatusCode);
    }
}
