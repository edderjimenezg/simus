using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Infrastructure.Correo;

/// <summary>Un mensaje que hay que hacer llegar por correo electrónico.</summary>
public sealed record CorreoSaliente(
    string Destinatario,
    string Asunto,
    string Cuerpo,
    /// <summary>Qué lo originó, para poder responder «¿se mandó el enlace de confirmación?».</summary>
    string TipoDeEvento,
    int? UsuarioDestinatarioId = null,
    /// <summary>
    /// Quién lee este mensaje: la persona de una organización o el equipo del PNMC.
    /// </summary>
    /// <remarks>
    /// <b>ESTABA FIJO EN «external» Y NO SIEMPRE ES VERDAD.</b> Mientras el único correo del sistema
    /// fue el de confirmación —que va siempre a quien se registra— daba igual; en cuanto el circuito
    /// de Festivales empieza a avisar también al funcionario que tiene que revisar, marcar su aviso
    /// como externo lo mandaría al buzón equivocado. El valor por omisión se conserva porque sigue
    /// siendo el caso corriente.
    /// </remarks>
    string Ambito = "external");

/// <summary>
/// El único punto por el que SIMUS entrega correo al mundo exterior.
/// </summary>
/// <remarks>
/// <para>
/// <b>EXISTE PORQUE EL PROVEEDOR TODAVIA NO.</b> El 12 de septiembre de 2026 Se define dejar
/// montada la confirmación de correo «como si ya existiese el proveedor de correo, lo solucionaremos
/// pronto». Así que el flujo está entero —testigo, expiración, reenvío, confirmación, reglas que
/// bloquean la entrega al Programa— y lo único que falta es el transporte, que es esta interfaz.
/// </para>
/// <para>
/// <b>CONECTAR UN PROVEEDOR REAL ES REGISTRAR OTRA IMPLEMENTACION</b> de <see cref="IEnviadorDeCorreo"/>
/// en el arranque. Ningún endpoint, ninguna regla y ninguna pantalla cambian.
/// </para>
/// </remarks>
public interface IEnviadorDeCorreo
{
    /// <summary>
    /// Deja el mensaje listo para salir. No promete entrega.
    /// </summary>
    Task EncolarAsync(CorreoSaliente correo, CancellationToken cancellationToken);
}

/// <summary>
/// La implementación provisional: registra lo que habría que enviar y no miente sobre el envío.
/// </summary>
/// <remarks>
/// <para>
/// <b>ESCRIBE EN <c>dbo.Notificaciones</c> Y NO EN UNA TABLA PROPIA.</b> Esa tabla ya tiene canal,
/// estado, intentos y fecha de envío: es literalmente la cola de salida que hacía falta, y una tabla
/// paralela daría dos respuestas a «¿qué se le ha mandado a esta persona?».
/// </para>
/// <para>
/// <b>EL ESTADO ES <c>pendiente</c>, QUE ES LA VERDAD Y ADEMAS YA EXISTIA.</b> Hay algo que mandar y
/// nadie lo ha mandado. Escribir «enviada» sería fingir que el servicio respondió, que es justo lo
/// que no se puede hacer: quien mire la ficha tiene que poder ver que el enlace está esperando
/// transporte. Cuando se conecte el proveedor, estas filas son exactamente la cola que hay que
/// drenar. Los dos valores salen del vocabulario que ya impone la base
/// —<c>CK_Notificaciones_Canal</c> y <c>CK_Notificaciones_Estado</c>—: inventar un canal
/// «correo» al lado de «email» habría dado dos nombres para lo mismo.
/// </para>
/// </remarks>
public sealed class EnviadorDeCorreoPendienteDeProveedor : IEnviadorDeCorreo
{
    /// <summary>El canal con el que viajan los mensajes destinados al correo electrónico.</summary>
    public const string Canal = "email";

    /// <summary>Hay algo que mandar y todavía no hay por dónde.</summary>
    public const string EstadoPendiente = "pendiente";

    private readonly PnmcDbContext _db;

    public EnviadorDeCorreoPendienteDeProveedor(PnmcDbContext db) => _db = db;

    public Task EncolarAsync(CorreoSaliente correo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correo);
        var ahora = DateTime.UtcNow;
        _db.Notifications.Add(new NotificationRow
        {
            RecipientUserId = correo.UsuarioDestinatarioId,
            RecipientEmail = correo.Destinatario,
            EventType = correo.TipoDeEvento,
            Channel = Canal,
            // QUIEN LO LEE LO DICE EL MENSAJE. Lo corriente es la persona de la organización, y ese
            // es el valor por omisión; los avisos de trabajo del circuito van al equipo del PNMC.
            AccessScope = correo.Ambito,
            Title = correo.Asunto,
            Body = correo.Cuerpo,
            Status = EstadoPendiente,
            // SIN FechaEnvio: no se ha enviado. Es la diferencia entre registrar y fingir.
            SentAt = null,
            Attempts = 0,
            CreatedAt = ahora,
        });
        return Task.CompletedTask;
    }
}
