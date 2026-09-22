namespace PNMC.Contracts;

/// <summary>
/// Un mensaje que la consola le escribe a una organización.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES ASISTENCIA, NO EDICIÓN.</b> Un funcionario no corrige los datos de una organización que no
/// es suya: le dice qué falta y ella lo corrige. La distinción importa porque es la misma que
/// sostiene la procedencia de los registros —quién los incorporó no es quién responde por ellos—.
/// </para>
/// <para>
/// NO LLEVA DESTINATARIO. Quien escribe no tiene por qué conocer los correos de la organización, y
/// pedírselos abriría la puerta a escribirle a cualquiera desde una pantalla que dice
/// «organización». El servidor resuelve a quién le llega.
/// </para>
/// </remarks>
public sealed class MensajeAOrganizacionSolicitud
{
    public string Asunto { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

/// <summary>Un mensaje ya enviado, con quién lo escribió y si lo han leído.</summary>
/// <remarks>
/// <c>LeidoEn</c> ES LA MITAD ÚTIL. «Le escribimos y no lo ha leído» es información de gestión:
/// distingue a quien ignora un aviso de quien nunca lo recibió.
/// </remarks>
public sealed record MensajeAOrganizacionDto(
    long Id,
    string Asunto,
    string Mensaje,
    string? RemitenteNombre,
    string? DestinatarioCorreo,
    DateTime FechaEnvio,
    DateTime? LeidoEn);

/// <summary>Lo que la consola envía para cambiar el estado de una organización.</summary>
/// <remarks>
/// <para>
/// <b>EL MOTIVO NO ES OPCIONAL AL CERRAR.</b> Archivar o desactivar una organización la saca de la
/// operación, y dentro de seis meses nadie recordará por qué. El motivo es la mitad del acto.
/// </para>
/// <para>
/// <b><c>Activa</c> YA NO ES UN EJE PROPIO.</b> Desde el ciclo de vida
/// tiene un solo eje —<c>pendiente_de_confirmacion</c>, <c>activa</c>, <c>inactiva</c>,
/// <c>eliminada</c>— y la vigencia se deriva de él. La marca suelta se sigue admitiendo por
/// compatibilidad y se traduce al estado que le corresponde; escribir las dos a la vez con valores
/// que no concuerdan lo rechaza la base (<c>CK_Entidades_VigenciaCoherente</c>).
/// </para>
/// <para>
/// <b><c>QueHacerConLosProcesos</c> ES LA RESPUESTA A UN 409, NO UN ATAJO.</b> Cerrar una
/// organización que todavía administra procesos publicados o pendientes de decisión los dejaría sin
/// nadie que responda por ellos, así que el servidor lo rechaza y los nombra. La siguiente petición
/// dice qué hacer con ellos, y son <b>dos salidas distintas</b>, no una:
/// </para>
/// <list type="bullet">
///   <item><c>liberar</c> — vuelven a la custodia del Programa, siguen publicados y otra
///   organización puede reclamarlos. Es lo que se hace cuando el proceso sigue vivo y solo cambia
///   quién responde por él.</item>
///   <item><c>archivar</c> — salen del sitio público conservando su historial y su auditoría, igual
///   que «Eliminar del ecosistema» sobre un Festival. Es lo que se hace cuando el proceso terminó
///   con la organización: un festival de una fundación disuelta que ya no se va a celebrar.</item>
/// </list>
/// <para>
/// <b>POR QUE HACEN FALTA LAS DOS.</b> Hasta solo existía liberar, así
/// que cerrar una organización obligaba a dejar sus festivales publicados en el portal a nombre del
/// Programa aunque ya no fueran a ocurrir; retirarlos exigía salir del diálogo, ir a Festivales y
/// eliminarlos uno a uno. Quedó definido así: «al eliminar una organización debe dar
/// la opción de también eliminar los procesos registrados o liberarlos».
/// </para>
/// <para>
/// Se aplica en la misma transacción que el cierre: liberar o archivar la mitad y fallar al cerrar
/// deja un ecosistema peor que el de partida.
/// </para>
/// </remarks>
public sealed class CambioDeEstadoDeOrganizacion
{
    public string? Estado { get; set; }
    public bool? Activa { get; set; }
    public string? Motivo { get; set; }

    /// <summary><c>liberar</c> o <c>archivar</c>. Sin valor, un cierre con procesos se rechaza.</summary>
    public string? QueHacerConLosProcesos { get; set; }
}

/// <summary>Lo que envía quien abre el enlace de confirmación de correo.</summary>
/// <remarks>
/// EL TESTIGO VIAJA EN EL CUERPO Y NO EN LA RUTA. Un testigo en la URL acaba en el registro del
/// servidor, en el historial del navegador y en la cabecera <c>Referer</c> de la siguiente petición.
/// La página lo lee de su propia dirección y lo manda aquí.
/// </remarks>
public sealed class ConfirmacionDeCorreoSolicitud
{
    public string? Testigo { get; set; }
}

/// <summary>La dirección correcta, cuando la que se escribió al registrarse tenía una errata.</summary>
/// <remarks>
/// <para>
/// <b>ES LA OTRA MITAD DE LA CONFIRMACION.</b> Pedirle a alguien que abra un enlace que llegó a una
/// dirección mal escrita es pedirle lo imposible: el correo es único y es el de la organización, así
/// que ni el restablecimiento de contraseña le llega. Sin esta ruta, una letra de más al registrarse
/// deja la cuenta muerta y obliga a crear otra organización.
/// </para>
/// <para>
/// <b>SOLO MIENTRAS SIGA SIN CONFIRMAR.</b> Una vez comprobada la dirección, cambiarla es otra cosa
/// —hay que comprobar la nueva antes de soltar la anterior, porque si no cualquiera que entre a una
/// sesión abierta se queda con la cuenta—, y esa es una decisión distinta que no entra aquí.
/// </para>
/// </remarks>
public sealed class CorreccionDeCorreoSolicitud
{
    public string? Correo { get; set; }
}

/// <summary>Lo que la consola envía para dar por comprobado el correo de una organización.</summary>
/// <remarks>
/// <para>
/// <b>ES UN ACTO HUMANO REGISTRADO, NO UN ATAJO.</b> Mientras SIMUS no tenga proveedor de correo,
/// nadie puede abrir el enlace que nunca sale, y una organización que llama por teléfono para
/// registrar su Festival se quedaría bloqueada indefinidamente. La salida no es apagar la regla: es
/// que una persona del Programa compruebe la dirección por otra vía y lo declare aquí, con su
/// nombre, la fecha y el motivo en la bitácora. Cuando llegue el proveedor esta vía se queda como
/// recurso excepcional; no se retira, porque el caso —«la organización no recibe nuestro correo»—
/// no desaparece con el proveedor.
/// </para>
/// <para>
/// <b>EL MOTIVO ES OBLIGATORIO Y ES LA MITAD DEL ACTO.</b> «Verificado por teléfono con la
/// directora el 12/09» es lo que distingue una comprobación real de un clic para desbloquear. Sin
/// él, dentro de seis meses la bitácora solo diría que alguien lo dio por bueno.
/// </para>
/// <para>
/// <b><c>Correo</c> NOMBRA LA DIRECCION QUE SE COMPROBO.</b> Es opcional cuando la organización
/// tiene una sola cuenta responsable, y obligatorio cuando tiene varias: dar por buena «la primera»
/// confirmaría una dirección que quien llamó no verificó. Si no coincide con ninguna, el servidor
/// responde nombrando las que hay en ficha.
/// </para>
/// </remarks>
public sealed class ConfirmacionDesdeLaConsola
{
    public string? Correo { get; set; }
    public string? Motivo { get; set; }
}
