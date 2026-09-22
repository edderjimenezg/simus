using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;

using PNMC.Domain.Entities;

namespace PNMC.Infrastructure.Data;

public sealed record ResultadoDepuracionImportaciones(int LotesExpirados, int LotesAplicadosMinimizados);

/// <summary>
/// Ejecuta la política aprobada sin borrar evidencia administrativa: las previsualizaciones
/// vencidas pierden filas y hallazgos, mientras los lotes aplicados pierden de inmediato la copia
/// de correo y teléfono. Cabeceras, decisiones, vínculos y auditoría se conservan para la TRD.
/// </summary>
public sealed class DepuradorImportaciones(PnmcDbContext db)
{
    private static readonly string[] CamposContactoMinimizados = ["correoContacto", "telefonoContacto"];
    public const string DominioFestivales = Modulos.Festivales;
    public const string EstadoPrevisualizado = "previsualizado";
    public const string EstadoDepurando = "depurando";
    public const string EstadoExpirado = "expirado";
    public const string EstadoAplicado = "aplicado";

    public async Task<ResultadoDepuracionImportaciones> EjecutarAsync(
        DateTime ahoraUtc,
        int diasPrevisualizacion,
        CancellationToken cancellationToken = default)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha de depuración debe expresarse en UTC.", nameof(ahoraUtc));
        if (diasPrevisualizacion is < 1 or > 365)
            throw new ArgumentOutOfRangeException(nameof(diasPrevisualizacion));

        var minimizados = await MinimizarAplicadosAsync(ahoraUtc, cancellationToken);
        var limite = ahoraUtc.AddDays(-diasPrevisualizacion);
        var idsVencidos = await db.LotesImportacion.AsNoTracking()
            .Where(lote => lote.ModuloId == DominioFestivales
                && lote.Estado == EstadoPrevisualizado
                && lote.FechaPrevisualizacion <= limite)
            .Select(lote => lote.Id)
            .ToListAsync(cancellationToken);

        var expirados = 0;
        foreach (var loteId in idsVencidos)
        {
            var estrategia = db.Database.CreateExecutionStrategy();
            await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var reservado = await db.LotesImportacion
                    .Where(lote => lote.Id == loteId
                        && lote.ModuloId == DominioFestivales
                        && lote.Estado == EstadoPrevisualizado
                        && lote.FechaPrevisualizacion <= limite)
                    .ExecuteUpdateAsync(actualizacion => actualizacion
                        .SetProperty(lote => lote.Estado, EstadoDepurando), cancellationToken);
                if (reservado == 0) return;

                var idsFilas = await db.FilasImportacion
                    .Where(fila => fila.LoteImportacionId == loteId)
                    .Select(fila => fila.Id)
                    .ToListAsync(cancellationToken);
                if (idsFilas.Count > 0)
                {
                    await db.DecisionesImportacion.Where(decision => idsFilas.Contains(decision.FilaImportacionId))
                        .ExecuteDeleteAsync(cancellationToken);
                    await db.HallazgosImportacion.Where(hallazgo => idsFilas.Contains(hallazgo.FilaImportacionId))
                        .ExecuteDeleteAsync(cancellationToken);
                    await db.FilasImportacion.Where(fila => idsFilas.Contains(fila.Id))
                        .ExecuteDeleteAsync(cancellationToken);
                }

                await db.LotesImportacion.Where(lote => lote.Id == loteId && lote.Estado == EstadoDepurando)
                    .ExecuteUpdateAsync(actualizacion => actualizacion
                        .SetProperty(lote => lote.Estado, EstadoExpirado)
                        .SetProperty(lote => lote.FechaDepuracion, ahoraUtc), cancellationToken);
                db.AuditLogs.Add(new AuditLogRow
                {
                    UserId = null,
                    TableName = "LotesImportacion",
                    RecordId = loteId.ToString(CultureInfo.InvariantCulture),
                    Action = "actualizar",
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        evento = "PrevisualizacionImportacionExpirada",
                        diasRetencion = diasPrevisualizacion,
                        filasRetiradas = idsFilas.Count,
                    }),
                    CreatedAt = ahoraUtc,
                });
                await db.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
                expirados++;
            });
        }

        return new ResultadoDepuracionImportaciones(expirados, minimizados);
    }

    private async Task<int> MinimizarAplicadosAsync(DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        var ids = await db.LotesImportacion.AsNoTracking()
            .Where(lote => lote.ModuloId == DominioFestivales
                && lote.Estado == EstadoAplicado
                && lote.FechaDepuracion == null)
            .Select(lote => lote.Id)
            .ToListAsync(cancellationToken);
        var total = 0;

        foreach (var loteId in ids)
        {
            var estrategia = db.Database.CreateExecutionStrategy();
            await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var reservado = await db.LotesImportacion
                    .Where(lote => lote.Id == loteId && lote.Estado == EstadoAplicado && lote.FechaDepuracion == null)
                    .ExecuteUpdateAsync(actualizacion => actualizacion
                        .SetProperty(lote => lote.FechaDepuracion, ahoraUtc), cancellationToken);
                if (reservado == 0) return;

                var filas = await db.FilasImportacion.Where(fila => fila.LoteImportacionId == loteId).ToListAsync(cancellationToken);
                foreach (var fila in filas)
                    fila.ContenidoNormalizadoJson = MinimizarContenidoNormalizado(fila.ContenidoNormalizadoJson, fila.Id);

                db.AuditLogs.Add(new AuditLogRow
                {
                    UserId = null,
                    TableName = "LotesImportacion",
                    RecordId = loteId.ToString(CultureInfo.InvariantCulture),
                    Action = "actualizar",
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        evento = "DatosDuplicadosImportacionMinimizados",
                        camposRetirados = CamposContactoMinimizados,
                    }),
                    CreatedAt = ahoraUtc,
                });
                await db.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
                total++;
            });
        }

        return total;
    }

    /// <summary>
    /// Retira del contenido normalizado la copia de los datos de contacto.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TRABAJA SOBRE EL JSON Y NO SOBRE UNA FORMA CONCRETA.</b> Hasta
    /// deserializaba el contrato de Festival, con lo que el depurador —que es de toda la capacidad—
    /// solo sabía depurar un dominio: el primero que importara otra cosa habría reventado aquí, y en
    /// el ciclo de retención, que corre solo y de noche.
    /// </para>
    /// <para>
    /// SE PONEN A NULO Y NO SE BORRAN LAS CLAVES: la fila sigue diciendo que ese dato existió y se
    /// retiró, que es distinto de no haberlo tenido nunca.
    /// </para>
    /// </remarks>
    public static string MinimizarContenidoNormalizado(string contenido, long filaId = 0)
    {
        JsonNode? raiz;
        try
        {
            raiz = JsonNode.Parse(contenido);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"La fila {filaId} no contiene un JSON legible.", error);
        }

        if (raiz is not JsonObject objeto)
            throw new InvalidOperationException($"La fila {filaId} no contiene el contrato normalizado esperado.");

        // SIN DISTINGUIR MAYUSCULAS: en la base conviven filas escritas con la convención anterior
        // —`CorreoContacto`— y las nuevas —`correoContacto`—, y este ciclo corre solo y de noche.
        // Buscar solo una de las dos formas retiraría el dato de la mitad de las filas, en silencio.
        foreach (var clave in objeto.Select(par => par.Key).ToList())
        {
            if (CamposContactoMinimizados.Contains(clave, StringComparer.OrdinalIgnoreCase))
                objeto[clave] = null;
        }

        return objeto.ToJsonString();
    }
}
