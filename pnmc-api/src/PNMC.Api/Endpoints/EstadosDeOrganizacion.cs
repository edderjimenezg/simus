namespace PNMC.Api.Endpoints;

/// <summary>
/// El ciclo de vida de una organización: cuatro estados, un solo eje.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUE ESTABA MAL PLANTEADO.</b> El criterio es este: «no entiendo los
/// estados de organizaciones». Y había dos motivos.
/// </para>
/// <para>
/// <b>El vocabulario no era el suyo.</b> Los estados se llamaban <c>registrada</c>,
/// <c>verificada</c>, <c>ajustes_solicitados</c> y <c>archivado</c> —palabras del circuito
/// institucional—, y antes de eso habían sido los ocho del catálogo editorial, que describen un
/// contenido publicable y no un actor. Un estado que hay que explicar es un estado mal nombrado.
/// </para>
/// <para>
/// <b>Y había dos ejes.</b> <c>EstadoRegistro</c> por un lado y <c>Activo</c> por otro, que era el
/// que de verdad decidía si la organización podía entrar. La pantalla llegó a tener un recuadro
/// explicando la diferencia entre los dos campos, que es exactamente la señal de que el modelo
/// estaba mal.
/// </para>
/// <para>
/// <b>AHORA CADA ESTADO DICE A LA VEZ COMO ESTA Y QUE PUEDE HACER</b>, y la base impide que
/// <c>Activo</c> lo contradiga (<c>CK_Entidades_VigenciaCoherente</c>).
/// </para>
/// <list type="table">
///   <listheader><term>Estado</term><description>Entra · Publica · Cómo se llega</description></listheader>
///   <item>
///     <term>pendiente_de_confirmacion</term>
///     <description>Sí · <b>No</b> · Es el estado de nacimiento del alta externa. Se sale
///     confirmando el correo desde el enlace que recibe la cuenta responsable.</description>
///   </item>
///   <item>
///     <term>activa</term>
///     <description>Sí · Sí · Confirmando el correo —desde el enlace, o declarándolo el Programa
///     con «Confirmar el correo» de la consola—. La acción «Activar» solo devuelve el acceso a una
///     organización cerrada; si su correo sigue sin comprobar, la deja en
///     <c>pendiente_de_confirmacion</c>, que es el estado que le corresponde.</description>
///   </item>
///   <item>
///     <term>inactiva</term>
///     <description>No · No · Acción «Desactivar». Reversible.</description>
///   </item>
///   <item>
///     <term>eliminada</term>
///     <description>No · No · Acción «Eliminar». Exige resolver antes sus procesos.</description>
///   </item>
/// </list>
/// <para>
/// <b>«AJUSTES SOLICITADOS» NO ESTA, Y NO SE PIERDE NADA.</b> Pedirle a una organización que corrija
/// su registro es un <b>mensaje</b>, y el canal de mensajes de la consola existe desde una revisión anterior.
/// Un estado que solo servía para pintar un sello ámbar no describía ninguna situación distinta de
/// «activa».
/// </para>
/// </remarks>
public static class EstadosDeOrganizacion
{
    /// <summary>Nació y su cuenta responsable todavía no ha confirmado el correo.</summary>
    public const string PendienteDeConfirmacion = "pendiente_de_confirmacion";

    /// <summary>Opera con normalidad.</summary>
    public const string Activa = "activa";

    /// <summary>Suspendida: conserva su ficha y no puede entrar. Reversible.</summary>
    public const string Inactiva = "inactiva";

    /// <summary>Retirada del ecosistema.</summary>
    public const string Eliminada = "eliminada";

    /// <summary>Los cuatro estados, en el orden del ciclo de vida.</summary>
    public static readonly string[] Todos = [PendienteDeConfirmacion, Activa, Inactiva, Eliminada];

    /// <summary>Los estados desde los que la organización puede entrar y trabajar.</summary>
    public static readonly string[] ConAcceso = [PendienteDeConfirmacion, Activa];

    /// <summary>Los estados que dejan la organización fuera y obligan a poner <c>Activo = 0</c>.</summary>
    public static readonly string[] SinAcceso = [Inactiva, Eliminada];

    private static readonly Dictionary<string, string> Etiquetas = new(StringComparer.OrdinalIgnoreCase)
    {
        [PendienteDeConfirmacion] = "Pendiente de confirmación",
        [Activa] = "Activa",
        [Inactiva] = "Inactiva",
        [Eliminada] = "Eliminada",
        // LOS RETIRADOS CONSERVAN ETIQUETA. La migración movió las filas, pero un volcado viejo o
        // una petición guardada pueden traer el código antiguo, y pintarlo en crudo es peor.
        ["registrada"] = "Registrada",
        ["verificada"] = "Verificada",
        ["ajustes_solicitados"] = "Ajustes solicitados",
        ["archivado"] = "Archivada",
    };

    /// <summary>Si el código indicado es uno de los cuatro estados de organización.</summary>
    public static bool EsValido(string? codigo) =>
        codigo is not null && Array.Exists(Todos, estado => string.Equals(estado, codigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>Si desde ese estado la organización puede entrar y trabajar.</summary>
    public static bool PermiteAcceso(string? codigo) =>
        codigo is not null && Array.Exists(ConAcceso, estado => string.Equals(estado, codigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>El valor que debe tener <c>Activo</c> para ese estado. Lo impone además la base.</summary>
    public static bool VigenciaDe(string? codigo) => PermiteAcceso(codigo);

    /// <summary>El estado en palabras, o el código tal cual si no se conoce.</summary>
    public static string Etiquetar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return string.Empty;
        return Etiquetas.TryGetValue(codigo, out var etiqueta) ? etiqueta : codigo;
    }
}
