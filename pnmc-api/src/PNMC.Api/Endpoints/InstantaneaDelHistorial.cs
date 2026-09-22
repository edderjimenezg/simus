using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Copia en una fila de historial quien era la organizacion y quien respondia por ella EN ESE
/// MOMENTO.
/// </summary>
/// <remarks>
/// <para>
/// EL DEFECTO QUE ESTO CIERRA, MEDIDO. <c>RevisionInstitucionalFestivalesEndpoints</c> armaba la
/// ficha de revision resolviendo el nombre de la organizacion por JOIN CONTRA EL PRESENTE, y a esa
/// misma respuesta le colgaba el historial entero. El dia en que una organizacion se cambiara el
/// nombre, TODAS sus entradas pasadas empezaban a decir el nombre nuevo — sin error, sin aviso y
/// sin que nadie lo viera. Es la clase de fallo que solo aparece cuando alguien pregunta por un
/// expediente viejo y la respuesta ya no cuadra con el papel.
/// </para>
/// <para>
/// POR QUE UN SITIO Y NO CUATRO. Cuatro rutas escriben historial —enviar un festival a revision,
/// enviar una propuesta, decidir sobre un festival y decidir sobre una propuesta—. Con la copia
/// repetida en cada una, la quinta ruta que alguien escriba nacera sin ella y nadie se dara cuenta:
/// los campos se quedan nulos y no rompen nada. Aqui al menos hay un solo sitio que leer.
/// </para>
/// <para>
/// LO QUE NO COPIA, Y ES DELIBERADO. No copia el numero de documento del responsable. El plan lo
/// pedia. Una entrada de historial no lo necesita para ser veraz sobre lo que paso: le bastan los
/// nombres tal y como estaban. Copiarlo en cada cambio de estado multiplicaria la cedula de un
/// ciudadano por todo el historial —decenas de filas por registro, sin caducidad y sin nadie que
/// las mire—, lo contrario del principio de minimizacion de la Ley 1581 de 2012. El documento
/// vigente vive una sola vez en <c>EntidadesResponsable</c>.
/// </para>
/// <para>
/// SI NO HAY DATO, SE DEJA NULO. Nunca se rellena con un valor de reemplazo: una fila vacia dice la
/// verdad —«esto no se guardo»— y un «Organizacion no disponible» guardado como si fuera el nombre
/// de entonces seria la misma falsificacion, escrita a mano.
/// </para>
/// </remarks>
internal static class InstantaneaDelHistorial
{
    /// <summary>
    /// Rellena los campos de instantánea de <paramref name="fila"/> antes de guardarla.
    /// </summary>
    /// <param name="organizacionId">
    /// La organización a cuyo nombre está el registro en este momento. Puede ser <c>null</c> en
    /// módulos que todavía no la tienen; entonces la instantánea queda vacía, que es lo correcto.
    /// </param>
    /// <param name="actorUsuarioId">Quien ejecuta la acción: la persona externa o la institucional.</param>
    public static async Task TomarAsync(
        PnmcDbContext dbContext,
        HistorialRevisionRegistroRow fila,
        int? organizacionId,
        int? actorUsuarioId,
        CancellationToken cancellationToken)
    {
        fila.OrganizacionId = organizacionId;

        if (organizacionId is int id)
        {
            fila.OrganizacionNombre = await dbContext.EntityProfiles.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.Name)
                .FirstOrDefaultAsync(cancellationToken);

            fila.ResponsableNombre = await dbContext.EntidadesResponsable.AsNoTracking()
                .Where(item => item.IdEntidad == id)
                .Select(item => item.ResponsableNombre)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (actorUsuarioId is int usuarioId)
        {
            fila.ActorNombre = await dbContext.Users.AsNoTracking()
                .Where(item => item.Id == usuarioId)
                .Select(item => item.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
