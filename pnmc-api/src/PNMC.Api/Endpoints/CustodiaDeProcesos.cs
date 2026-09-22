using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Qué pasa con los procesos de una organización cuando la organización deja de operar.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL AGUJERO QUE CIERRA.</b> Desde dar de baja una organización le
/// corta el acceso de verdad. Pero un Festival <b>publicado</b> cuya organización responsable acaba
/// de quedarse sin nadie dentro sigue en la página pública, con el nombre de esa organización
/// debajo, y ya no hay ser humano que pueda editarlo, proponerle un cambio ni registrarle una
/// edición. Tampoco se puede reclamar: <c>ReclamacionesAdministracionEndpoints</c> solo admite
/// reclamar lo que está en custodia del Programa. El proceso queda huérfano y nadie se entera hasta
/// que alguien pregunta por él.
/// </para>
/// <para>
/// <b>NO SE INVENTA MECANISMO.</b> El proyecto ya tiene el sitio donde devolver la custodia —la
/// entidad institucional— y ya tiene el acto que lo hace —liberar la administración, que escribe su
/// fila en <c>TransferenciasAdministracion</c> y deja el proceso reclamable—. Lo único que faltaba
/// era <b>obligar a usarlo</b> antes de cerrar la organización, y poder hacerlo para todos sus
/// procesos en un solo acto en vez de uno por uno.
/// </para>
/// <para>
/// <b>QUÉ CUENTA COMO DEPENDIENTE.</b> Un Festival cuya desaparición de su administradora deja algo
/// sin quién responda: los <b>publicados</b>, porque son públicos; los que están <b>en revisión</b>
/// o <b>aprobados</b>, porque la siguiente decisión institucional los publicaría sin administradora;
/// y los que tienen <b>ajustes solicitados</b>, porque se le pidieron a alguien que ya no está. Un
/// borrador no cuenta —no es público ni tiene nada pendiente del Programa— y un rechazado o
/// archivado tampoco, porque su ciclo terminó.
/// </para>
/// </remarks>
internal static class CustodiaDeProcesos
{
    /// <summary>
    /// Los estados de Festival en los que quedarse sin organización administradora deja algo sin
    /// quién responda.
    /// </summary>
    internal static readonly string[] EstadosQueDejanHuerfano =
        [EstadosFestival.Publicado, EstadosFestival.EnRevision, EstadosFestival.AjustesSolicitados];

    /// <summary>Un proceso que la organización administra, tal como lo ve la consola.</summary>
    internal sealed record ProcesoAdministrado(int Id, string Nombre, string Estado, bool DejaHuerfano);

    /// <summary>
    /// Los procesos que administra una organización, con su estado y si bloquean el cierre.
    /// </summary>
    /// <remarks>
    /// FESTIVAL ES EL UNICO PROCESO HABILITADO HOY. Cuando entren mercados, escuelas y los demás,
    /// esta consulta crece aquí y no en cada pantalla que pregunte por los procesos de una
    /// organización.
    /// </remarks>
    internal static async Task<List<ProcesoAdministrado>> DeLaOrganizacionAsync(
        PnmcDbContext db, int organizacionId, CancellationToken ct)
    {
        var filas = await db.FestivalRecords.AsNoTracking()
            .Where(festival => festival.OrganizacionPrincipalId == organizacionId)
            .OrderBy(festival => festival.Name)
            .Select(festival => new { festival.Id, festival.Name, festival.StatusCode })
            .ToListAsync(ct);

        return filas
            .Select(fila =>
            {
                // LA BASE GUARDA LAS DOS GRAFIAS. `publicado` y `Publicado` conviven desde la
                // importación del acervo; normalizar aquí evita que un festival publicado con la
                // grafía antigua se cuele como si no bloqueara nada.
                var estado = EstadosFestival.DesdeContrato(fila.StatusCode) ?? (fila.StatusCode ?? string.Empty);
                return new ProcesoAdministrado(
                    fila.Id,
                    fila.Name,
                    estado,
                    Array.Exists(EstadosQueDejanHuerfano, e => string.Equals(e, estado, StringComparison.OrdinalIgnoreCase)));
            })
            .ToList();
    }

    /// <summary>
    /// Devuelve un Festival a la custodia del Programa, sin guardar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NO LLAMA A <c>SaveChangesAsync</c> A PROPOSITO. Quien libera un proceso suelto lo hace en su
    /// propia transacción; quien cierra una organización libera todos los suyos y cambia el estado
    /// <b>en la misma</b>, porque liberar la mitad y fallar al cerrar deja un ecosistema peor que
    /// el de partida.
    /// </para>
    /// <para>
    /// DEJA DOS RASTROS Y HACEN FALTA LOS DOS: la fila de <c>TransferenciasAdministracion</c>, que
    /// es lo que cuenta la ficha del proceso cuando alguien pregunte cómo llegó a estar libre, y la
    /// de la bitácora, que es lo que cuenta la consola cuando alguien pregunte quién lo decidió.
    /// </para>
    /// </remarks>
    internal static void Liberar(
        PnmcDbContext db,
        FestivalRow festival,
        int institucionalId,
        int actorId,
        string motivo,
        DateTime ahora)
    {
        var anterior = festival.OrganizacionPrincipalId!.Value;
        festival.OrganizacionPrincipalId = institucionalId;
        festival.UpdatedAt = ahora;

        db.AdministrationTransfers.Add(new AdministrationTransferRow
        {
            ModuloId = Modulos.Festivales,
            CanonicalRecordId = festival.Id.ToString(CultureInfo.InvariantCulture),
            PreviousOrganizationId = anterior,
            NewOrganizationId = institucionalId,
            // SIN RECLAMACION: no la pidió nadie, la decidió el Programa.
            ClaimId = null,
            DeciderId = actorId,
            Reason = ValidationHelpers.SanitizeText(motivo, 2400),
            PreviousStatus = festival.StatusCode,
            NewStatus = festival.StatusCode,
            ExecutedAt = ahora,
        });

        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = actorId,
            TableName = "Festivales",
            RecordId = festival.Id.ToString(CultureInfo.InvariantCulture),
            Action = AccionesAuditoria.Actualizar,
            PreviousValuesJson = JsonSerializer.Serialize(new { organizacionResponsableId = anterior }),
            NewValuesJson = JsonSerializer.Serialize(new
            {
                evento = "AdministracionLiberadaAlPrograma",
                organizacionResponsableId = institucionalId,
                motivo = ValidationHelpers.SanitizeText(motivo, 600),
            }),
            CreatedAt = ahora,
        });
    }

    /// <summary>Saca el proceso del sitio público conservando su historial y su auditoría.</summary>
    /// <remarks>
    /// <para>
    /// <b>ES LA OTRA SALIDA AL CIERRE DE UNA ORGANIZACION,</b> y significa algo distinto de liberar.
    /// Liberar dice «este proceso sigue vivo y ahora responde el Programa»; archivar dice «este
    /// proceso terminó con la organización». Un festival de una fundación disuelta que ya no se va a
    /// celebrar no debería quedarse publicado a nombre del Programa esperando a que alguien lo
    /// reclame. El criterio es este: «al eliminar una
    /// organización debe dar la opción de también eliminar los procesos registrados o liberarlos».
    /// </para>
    /// <para>
    /// <b>NO BORRA NADA.</b> Es el mismo acto que «Eliminar del ecosistema» sobre un Festival: el
    /// registro se queda con su ficha, su historial y su bitácora, y deja de verse en el portal.
    /// La custodia no cambia de manos: el proceso sigue siendo de la organización que lo registró,
    /// que es lo que hay que poder responder años después.
    /// </para>
    /// </remarks>
    internal static void Archivar(
        PnmcDbContext db,
        FestivalRow festival,
        int actorId,
        string motivo,
        DateTime ahora)
    {
        var anterior = festival.StatusCode;
        festival.StatusCode = EstadosFestival.Archivado;
        festival.UpdatedAt = ahora;

        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = actorId,
            TableName = "Festivales",
            RecordId = festival.Id.ToString(CultureInfo.InvariantCulture),
            Action = AccionesAuditoria.Archivar,
            PreviousValuesJson = JsonSerializer.Serialize(new { estado = anterior }),
            NewValuesJson = JsonSerializer.Serialize(new
            {
                evento = "ArchivadoAlCerrarLaOrganizacion",
                estado = festival.StatusCode,
                motivo = ValidationHelpers.SanitizeText(motivo, 600),
            }),
            CreatedAt = ahora,
        });
    }
}
