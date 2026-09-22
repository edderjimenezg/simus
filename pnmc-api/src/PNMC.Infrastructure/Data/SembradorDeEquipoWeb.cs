using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Crea la fila única de [EquipoWeb] con la nómina compilada.
/// <para>
/// <b>Solo inserta.</b> Si la fila ya existe no se toca nada: ni el borrador, ni
/// lo publicado, ni la versión. La nómina la mantienen personas, y un arranque
/// del servicio no puede deshacer su trabajo. Sembrar dos veces no cambia nada.
/// </para>
/// </summary>
public static class SembradorDeEquipoWeb
{
    public static async Task EnsureSeededAsync(
        PnmcDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.EquipoWeb.AnyAsync(cancellationToken);
        if (exists)
        {
            return;
        }

        IReadOnlyList<MiembroDelEquipoWeb> defaults;
        try
        {
            defaults = SembradorDeContenidoWeb.LoadTeamDefaults();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "No fue posible leer la nómina del catálogo; la siembra del equipo se omitió.");
            return;
        }

        // La siembra pasa por la misma validación que un guardado del panel. Si el
        // catálogo trajera algo que después nadie pudiera volver a guardar, la
        // nómina quedaría bloqueada y el síntoma aparecería mucho más tarde.
        var errors = ContratoDeEquipoWeb.Validate(defaults, out var normalized);
        if (errors.Count > 0)
        {
            logger.LogError(
                "La nómina compilada no cumple los topes de la API ({Count} problemas); la siembra del equipo se omitió. Primero: {Detalle}",
                errors.Count,
                errors.First().Value.FirstOrDefault());
            return;
        }

        db.EquipoWeb.Add(new EquipoWebRow
        {
            Id = EquipoWebRow.SingletonId,
            // Igual que en los 238 textos: sembrar no es publicar. El sitio sigue
            // sirviendo su nómina compilada hasta que alguien publique de verdad.
            Draft = ContratoDeEquipoWeb.Serialize(normalized),
            Published = null,
            Version = 1,
            UpdatedBy = "Sistema",
            UpdatedAt = DateTime.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Dos instancias arrancando a la vez compiten por la misma fila única.
            // La que pierde encuentra la clave primaria ocupada, que es el
            // resultado correcto: la fila existe.
            db.ChangeTracker.Clear();
            logger.LogInformation(exception, "La fila de [EquipoWeb] ya existía al sembrar; no se hizo nada.");
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Nómina del equipo sembrada con {Count} personas.", normalized.Count);
        }
    }
}
