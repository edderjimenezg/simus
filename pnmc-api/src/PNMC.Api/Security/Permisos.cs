using System.Security.Claims;

namespace PNMC.Api.Security;

/// <summary>
/// Punto unico de decision de permisos y <b>catalogo unico de roles de plataforma</b>.
/// Ningun endpoint decide por su cuenta quien es funcionario, y ninguna lista de roles se
/// escribe dos veces.
/// </summary>
/// <remarks>
/// <para>
/// EL MODELO DE ROLES (aprobado, ver
/// <c>Fabrica de Software/00-Gobierno/modelo-del-ecosistema.md</c>). Cinco figuras, tres
/// filas en <c>dbo.Roles</c>:
/// </para>
/// <list type="table">
///   <listheader><term>Figura</term><description>Rol en la base</description></listheader>
///   <item><term>Publico</term><description>ninguno: no hay sesion</description></item>
///   <item><term>Persona registrada</term><description><c>externo</c> sin vinculo en <c>UsuariosEntidades</c></description></item>
///   <item><term>Usuario externo</term><description><c>externo</c> <b>con</b> vinculo en <c>UsuariosEntidades</c></description></item>
///   <item><term>Funcionario</term><description><c>gestor_interno</c></description></item>
///   <item><term>Webmaster</term><description><c>webmaster</c></description></item>
/// </list>
/// <para>
/// Que «persona registrada» y «usuario externo» compartan rol no es un atajo: es la regla
/// <c>ROL + ALCANCE</c> del modelo. El rol dice <i>que clase de cosas puede hacer</i>; el
/// alcance —la fila de <c>UsuariosEntidades</c>— dice <i>sobre que actor</i>. Multiplicar
/// roles por tipo de actor devolveria el sistema a los catorce nombres de rol que tenia en
/// agosto, de los cuales solo tres decidian algo.
/// </para>
/// <para>
/// POR QUE EXISTE ESTE FICHERO. Tener sesion de consola y ser funcionario del Ministerio no
/// son lo mismo, y confundirlos abrio un camino real. Los tres eslabones, <b>medidos el 22
/// de agosto de 2026 y cerrados ese mismo dia</b>:
/// </para>
/// <list type="number">
///   <item><description>
///     Aprobar una entidad aliada creaba una cuenta con rol <c>aliado_admin</c>.
///   </description></item>
///   <item><description>
///     La puerta institucional rechazaba <b>unicamente</b> el rol <c>externo</c>: era una
///     lista negra de un solo elemento, asi que los tres roles <c>aliado_*</c> entraban y
///     recibian la cookie <c>pnmc.admin</c> con ambito institucional.
///   </description></item>
///   <item><description>
///     El circuito de revision institucional no comprobaba rol en ninguna linea: cero
///     coincidencias en 267 lineas de <c>RevisionInstitucionalFestivalesEndpoints</c>, cero
///     en 171 de <c>RevisionInstitucionalPropuestasFestivalEndpoints</c> y cero en 116 de
///     <c>NormalizacionVersionesFestivalEndpoints</c>. Su unico cierre era
///     <c>RequireAuthorization(InstitutionalPolicy)</c>, que significa exactamente «hay una
///     cookie de consola».
///   </description></item>
/// </list>
/// <para>
/// Resultado de aquello: un administrador de entidad aliada podia publicar o rechazar
/// cualquier Festival, y lanzar la normalizacion masiva de versiones historicas.
/// </para>
/// <para>
/// QUE QUEDA DE AQUELLO, Y POR QUE NO SE RETIRA. El concepto de entidad aliada se elimino
/// del sistema —no quedan roles <c>aliado_*</c>, ni tablas, ni
/// portal—, de modo que el primer eslabon ya no existe. <b>Las dos cerraduras que se
/// pusieron entonces se conservan</b>, y no por inercia:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <see cref="PoliticaFuncionario"/> exige rol interno <b>de forma positiva</b>. No
///     depende del aliado: depende de <see cref="RolesInternos"/>. Es la unica guarda de rol
///     de todo el circuito de revision institucional, cuyos tres ficheros siguen sin
///     comprobar rol en ningun manejador.
///   </description></item>
///   <item><description>
///     La puerta institucional paso de lista negra a <b>lista blanca</b> derivada de
///     <see cref="RolesInternos"/>. Una lista negra solo ve lo que ya conoce: el dia que
///     aparezca un rol nuevo —la subdivision fina de funcionarios que el modelo deja para mas
///     adelante— una lista negra lo deja entrar por omision, y una lista blanca no.
///   </description></item>
/// </list>
/// <para>
/// POR QUE UN SOLO SITIO. Cuando este fichero se escribio ya habia <b>cuatro</b> predicados
/// identicos repartidos por la API, cada uno reescribiendo
/// <c>IsInRole("webmaster") || IsInRole("gestor_interno")</c> a mano
/// (<c>AdminOrganizacionesEndpoints</c>, <c>NotificationEndpoints</c>,
/// <c>RecordGovernanceEndpoints</c> y el portal de aliados, ya retirado) y tres ficheros mas
/// sin ninguno. Repetir una regla es como se llega a que una copia se quede atras sin que
/// nadie lo note; y una lista de roles repetida es como se llega a que la puerta y la
/// politica digan cosas distintas.
/// </para>
/// </remarks>
public static class Permisos
{
    /// <summary>
    /// Politica que exige sesion de consola <b>y ademas</b> rol interno del Ministerio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se aplica sobre el GRUPO de rutas, no sobre cada manejador, y es deliberado. Tres de
    /// las rutas que cierra —la bandeja de revision, la ficha institucional de un Festival y
    /// la lista de propuestas— ni siquiera reciben <c>ClaimsPrincipal</c> en su firma:
    /// cerrarlas una a una obligaria a cambiar cada firma y dejaria la puerta abierta en la
    /// siguiente ruta que alguien añada sin acordarse.
    /// </para>
    /// <para>
    /// El nombre vive aqui y no en <c>SimusAuthentication</c> para no mezclar dos cosas
    /// distintas: aquel fichero dice <b>quien eres y por que puerta entraste</b>; este dice
    /// <b>que puedes hacer</b>. Confundirlas es justamente el origen del hallazgo.
    /// </para>
    /// </remarks>
    public const string PoliticaFuncionario = "funcionario-institucional";

    /// <summary>Rol interno con control total: usuarios, configuracion y publicacion.</summary>
    public const string Webmaster = "webmaster";

    /// <summary>Rol interno de segundo nivel: revisa, decide y acompaña.</summary>
    public const string GestorInterno = "gestor_interno";

    /// <summary>
    /// Rol de toda persona del ecosistema: la que solo se registro y la que representa a un
    /// actor. Los distingue el alcance (<c>UsuariosEntidades</c>), no el rol.
    /// </summary>
    public const string Externo = "externo";

    /// <summary>
    /// Los roles del Ministerio. <b>Fuente unica</b> de las DOS decisiones que separan al
    /// personal interno del resto: la puerta institucional (<c>AdminAuthEndpoints</c>, manejador
    /// de <c>/admin/auth/login</c>) y <see cref="PoliticaFuncionario"/>.
    ///
    /// <para>
    /// La lista de roles <b>asignables</b> desde la consola NO sale de aqui, sale de
    /// <see cref="RolesDePlataforma"/>, y la diferencia importa: desde la consola se puede
    /// asignar <c>externo</c>, que no es interno. Confundir las dos listas es como se llega a
    /// creer que una puerta esta cerrada porque otra lo esta.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Antes de la retirada de aliados, la puerta y esta lista coincidian <b>por accidente</b>:
    /// la puerta enumeraba a quien rechazar y esta a quien admitir, y daba lo mismo porque el
    /// catalogo tenia seis nombres y todos estaban en una de las dos. Esa equivalencia se
    /// rompia sola en cuanto apareciera un rol nuevo. Ahora hay una sola lista y las dos
    /// decisiones que dependen de ella la leen.
    /// </remarks>
    public static readonly string[] RolesInternos = [Webmaster, GestorInterno];

    /// <summary>
    /// Los tres roles que la plataforma reconoce. Cualquier otro nombre en <c>dbo.Roles</c>
    /// es un residuo y no debe abrir ninguna puerta.
    /// </summary>
    public static readonly string[] RolesDePlataforma = [Webmaster, GestorInterno, Externo];

    /// <summary>
    /// Es un rol del Ministerio. Lista blanca: lo que no esta nombrado, no entra.
    /// </summary>
    /// <remarks>
    /// Compara por nombre de rol y no por <c>ClaimsPrincipal</c> porque la puerta
    /// institucional decide <b>antes</b> de que exista principal alguno: en el momento del
    /// login solo hay una fila de <c>dbo.Roles</c>.
    /// </remarks>
    public static bool EsRolInterno(string? nombreDeRol) =>
        nombreDeRol is not null
        && RolesInternos.Contains(Normalizar(nombreDeRol), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Alguno de estos roles es del Ministerio. La version de CONJUNTO de
    /// <see cref="EsRolInterno(string?)"/>.
    /// </summary>
    /// <remarks>
    /// Existe desde entonces, cuando una persona paso a poder tener varios roles a la vez. La
    /// puerta institucional pregunta por aqui: basta UNO interno para entrar, igual que basta un
    /// rol interno para que <see cref="EsFuncionario"/> diga que si. La combinacion prohibida
    /// —externo mezclado con interno— no se filtra en la puerta sino en el sitio donde se crea,
    /// que es <see cref="MezclaExternoConInterno"/>; ver alli por que.
    /// </remarks>
    /// <remarks>
    /// SE LLAMA DISTINTO A PROPOSITO, y no es una sobrecarga de <see cref="EsRolInterno(string?)"/>:
    /// una sobrecarga hacia ambigua la llamada <c>EsRolInterno(null)</c> —que una prueba hace, y
    /// con razon— porque <c>null</c> encaja igual de bien en <c>string</c> que en
    /// <c>IEnumerable</c>. Un nombre distinto ademas se lee mejor en el sitio de la llamada:
    /// dice que se pregunta por un conjunto.
    /// </remarks>
    public static bool AlgunoEsRolInterno(IEnumerable<string?> roles) =>
        roles is not null && roles.Any(EsRolInterno);

    /// <summary>
    /// El conjunto mezcla el rol del ecosistema con uno del Ministerio, y eso <b>no puede
    /// asignarse</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL DEFECTO U7 DEL PLAN DE CONSTRUCCION, y la razon por la que la transicion no podia entrar
    /// sin esta funcion. PNMC tiene dos cookies con dos ambitos distintos —<c>pnmc.admin</c>,
    /// institucional, y <c>pnmc.external</c>, del ecosistema— y con la columna escalar
    /// <c>Usuarios.IdRol</c> era imposible tener las dos: un rol, un ambito. En cuanto los roles
    /// pasan a ser un conjunto, <c>{externo, gestor_interno}</c> se vuelve representable, y esa
    /// persona podria entrar por las dos puertas y ser a la vez la revisada y quien revisa.
    /// </para>
    /// <para>
    /// SE CIERRA DONDE SE CREA, NO DONDE SE USA. Filtrarlo en cada puerta significaria acordarse
    /// en todas las puertas presentes y futuras; prohibir la asignacion significa que el estado
    /// no llega a existir. La deteccion sobre datos ya escritos vive aparte, en
    /// <c>pnmc-database/scripts/validar_migracion_simus.sql</c>, porque una fila puesta a mano
    /// contra la base no pasa por ninguna ruta del API.
    /// </para>
    /// <para>
    /// NO ES LO MISMO QUE «tener dos roles», que es legitimo y es todo el sentido de la transicion:
    /// <c>{gestor_interno, webmaster}</c> es correcto y da la union de los dos.
    /// </para>
    /// </remarks>
    public static bool MezclaExternoConInterno(IEnumerable<string?> roles)
    {
        if (roles is null)
        {
            return false;
        }

        var lista = roles.ToList();
        return lista.Any(rol => EsRolInterno(rol))
            && lista.Any(rol => rol is not null && Normalizar(rol) == Externo);
    }

    /// <summary>Es uno de los tres roles que la plataforma reconoce.</summary>
    public static bool EsRolDePlataforma(string? nombreDeRol) =>
        nombreDeRol is not null
        && RolesDePlataforma.Contains(Normalizar(nombreDeRol), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Es funcionario del Ministerio: puede revisar y decidir.
    /// </summary>
    /// <remarks>
    /// Incluye a <c>webmaster</c> porque puede administrarlo todo. La decision D3 del modelo
    /// de roles dice que <b>cualquier funcionario decide</b>, publicacion incluida; la regla
    /// contraria del CMS —donde solo el webmaster alcanza «publicado»— se conserva alli y no
    /// se extiende aqui, porque §41 separa contenidos editoriales de registros sectoriales.
    /// </remarks>
    public static bool EsFuncionario(ClaimsPrincipal? principal) =>
        TieneAlguno(principal, RolesInternos);

    /// <summary>Es webmaster: gestion de usuarios institucionales y configuracion.</summary>
    public static bool EsWebmaster(ClaimsPrincipal? principal) =>
        TieneAlguno(principal, [Webmaster]);

    /// <summary>
    /// Normaliza el nombre de rol como lo hace la base: sin espacios y en minusculas.
    /// </summary>
    private static string Normalizar(string valor) => valor.Trim().ToLowerInvariant();

    private static bool TieneAlguno(ClaimsPrincipal? principal, string[] roles)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        foreach (var rol in roles)
        {
            if (principal.IsInRole(rol))
            {
                return true;
            }
        }

        return false;
    }
}
