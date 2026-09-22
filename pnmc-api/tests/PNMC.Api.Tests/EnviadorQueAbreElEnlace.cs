using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Correo;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Tests;

/// <summary>
/// El enviador del arnés: encola el mensaje de verdad y además hace de persona que abre el enlace.
/// </summary>
/// <remarks>
/// <para>
/// <b>ENCOLA PRIMERO, Y ESO IMPORTA.</b> Delega en el enviador real —el que deja la fila en
/// <c>dbo.Notificaciones</c> con estado <c>pendiente</c>—, así que las pruebas que miran la cola de
/// salida siguen viendo exactamente lo que vería producción. Lo que añade es el gesto que en
/// producción hace una persona: abrir el correo y pulsar.
/// </para>
/// <para>
/// <b>NO CONFIRMA NADA QUE NO SE HAYA PEDIDO.</b> Solo actúa sobre el mensaje de confirmación de
/// correo, identificado por su tipo de evento. Cualquier otro correo se encola y ya.
/// </para>
/// </remarks>
internal sealed class EnviadorQueAbreElEnlace : IEnviadorDeCorreo
{
    private readonly PnmcDbContext _db;
    private readonly EnviadorDeCorreoPendienteDeProveedor _real;

    public EnviadorQueAbreElEnlace(PnmcDbContext db)
    {
        _db = db;
        _real = new EnviadorDeCorreoPendienteDeProveedor(db);
    }

    public async Task EncolarAsync(CorreoSaliente correo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correo);
        await _real.EncolarAsync(correo, cancellationToken);

        if (correo.TipoDeEvento != "ConfirmacionDeCorreo" || correo.UsuarioDestinatarioId is not int cuentaId) return;

        var cuenta = await _db.Users.FirstOrDefaultAsync(x => x.Id == cuentaId, cancellationToken);
        if (cuenta is null) return;
        cuenta.CorreoConfirmado = true;
        cuenta.FechaConfirmacionCorreo = DateTime.UtcNow;

        // Y ACTIVA SUS ORGANIZACIONES, igual que la ruta real: dejar la cuenta confirmada y la
        // organización pendiente produciría una combinación que el sistema nunca genera, y las
        // pruebas estarían midiendo un estado imposible.
        var suyas = await _db.UserEntities
            .Where(v => v.UserId == cuenta.Id && v.IsActive)
            .Select(v => v.EntityId)
            .ToListAsync(cancellationToken);
        var pendientes = await _db.EntityProfiles
            .Where(o => suyas.Contains(o.Id) && o.StatusCode == "pendiente_de_confirmacion")
            .ToListAsync(cancellationToken);
        foreach (var organizacion in pendientes)
        {
            organizacion.StatusCode = "activa";
            organizacion.IsActive = true;
        }
    }
}
