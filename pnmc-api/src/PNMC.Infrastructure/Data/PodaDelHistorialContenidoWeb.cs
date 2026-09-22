using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Mantiene el historial de cada texto acotado a las últimas entradas.
/// </summary>
/// <remarks>
/// <para>
/// El historial crecía sin techo: una fila por acción y por clave, para siempre.
/// En la base local llegó a <b>4.733 filas para 332 textos</b>, con una sola
/// clave acumulando 27 revisiones de una tarde de pruebas. Nada de eso se
/// consulta: el panel muestra las últimas y nadie ha vuelto nunca a la
/// vigesimoprimera.
/// </para>
/// <para>
/// El tope resuelve dos problemas a la vez. El obvio es el tamaño. El que
/// importa más es la <b>legibilidad</b>: un historial de 27 líneas en el que 22
/// son ensayos de la misma tarde no se lee, y un registro que no se lee deja de
/// cumplir su función aunque siga completo.
/// </para>
/// <para>
/// <b>Qué se conserva exactamente.</b> Las <see cref="Tope"/> entradas más
/// recientes de cada clave. Conviene saber que la más reciente describe el
/// estado <em>actual</em> del texto, así que como <b>puntos de retorno</b>
/// quedan <see cref="Tope"/> menos uno. El tope se fijó primero en cinco y se
/// subió a seis por esa misma razón: el equipo del PNMC quería cinco versiones
/// a las que poder volver, no cinco filas. Vive en una sola constante
/// justamente para poder cambiarlo sin buscarlo.
/// </para>
/// <para>
/// <b>Esto borra datos y no hay vuelta atrás.</b> La poda es deliberada: lo
/// contrario de un registro de auditoría que se puede alterar es uno que nadie
/// puede leer. Lo que NO se toca nunca es la fila de <c>ContenidoWeb</c>: el
/// borrador y el texto publicado siguen intactos, y el «Restaurar» del panel
/// sigue funcionando dentro de la ventana conservada.
/// </para>
/// </remarks>
public static class PodaDelHistorialContenidoWeb
{
    /// <summary>
    /// Cuántas entradas de historial se conservan por clave.
    /// <para>
    /// Seis, no cinco: la más reciente describe el estado actual, así que este
    /// número menos uno es lo que de verdad se puede restaurar.
    /// </para>
    /// </summary>
    public const int Tope = 6;

    /// <summary>
    /// Marca para borrado las entradas que sobran de una clave, contando las que
    /// se están a punto de insertar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No guarda.</b> Se apunta al mismo <see cref="PnmcDbContext"/> para que
    /// la poda entre en el mismo <c>SaveChanges</c> que el cambio que la
    /// provoca. Si fuesen dos transacciones, un fallo entre ambas dejaría el
    /// historial por encima del tope hasta la siguiente escritura, y la base
    /// contaría una historia distinta según el momento en que se mirara.
    /// </para>
    /// <para>
    /// Las entradas pendientes se cuentan desde el rastreador de cambios y no se
    /// dan por supuestas: una petición que escribiera dos veces la misma clave
    /// conservaría una de más si aquí se asumiera «siempre se añade una».
    /// </para>
    /// </remarks>
    public static async Task MarcarSobrantesAsync(
        PnmcDbContext db,
        string clave,
        CancellationToken cancellationToken = default)
    {
        var pendientes = db.ChangeTracker
            .Entries<HistorialDeContenidoWebRow>()
            .Count(entrada => entrada.State == EntityState.Added
                && string.Equals(entrada.Entity.Key, clave, StringComparison.Ordinal));

        var conservarDeLaBase = Math.Max(0, Tope - pendientes);

        // `Skip` sobre el orden descendente devuelve exactamente lo que sobra, así
        // que en el caso normal —una clave con menos entradas que el tope— la
        // consulta no devuelve ninguna fila y no hay nada que borrar.
        var sobrantes = await db.HistorialDeContenidoWeb
            .Where(entrada => entrada.Key == clave)
            .OrderByDescending(entrada => entrada.At)
            .ThenByDescending(entrada => entrada.Id)
            .Skip(conservarDeLaBase)
            .ToListAsync(cancellationToken);

        if (sobrantes.Count > 0)
        {
            db.HistorialDeContenidoWeb.RemoveRange(sobrantes);
        }
    }

    /// <summary>
    /// Recorta de una vez el historial acumulado antes de que existiera el tope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Corre en cada arranque y es idempotente: a partir del segundo no encuentra
    /// nada que borrar y no ejecuta ninguna escritura. Sin este paso, el tope
    /// solo se aplicaría a las claves que alguien volviera a editar, y las demás
    /// conservarían su acumulación para siempre.
    /// </para>
    /// <para>
    /// Se calcula qué sobra <b>en memoria</b> y se borra por lotes. La
    /// alternativa —una ventana <c>ROW_NUMBER</c> por clave en SQL— es más
    /// elegante pero no se expresa en LINQ, y el conjunto está acotado por el
    /// tamaño del catálogo: unos pocos miles de filas de tres columnas.
    /// </para>
    /// </remarks>
    public static async Task PodarTodoAsync(
        PnmcDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var entradas = await db.HistorialDeContenidoWeb
            .AsNoTracking()
            .Select(entrada => new { entrada.Id, entrada.Key, entrada.At })
            .ToListAsync(cancellationToken);

        var sobrantes = entradas
            .GroupBy(entrada => entrada.Key, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > Tope)
            .SelectMany(grupo => grupo
                .OrderByDescending(entrada => entrada.At)
                .ThenByDescending(entrada => entrada.Id)
                .Skip(Tope))
            .Select(entrada => entrada.Id)
            .ToList();

        if (sobrantes.Count == 0)
        {
            return;
        }

        // Por lotes porque SQL Server admite 2.100 parámetros por consulta y un
        // `IN` con todo lo que sobra los superaría en cuanto la base lleve un
        // tiempo en uso.
        const int TamanoDelLote = 500;
        var borradas = 0;
        for (var desde = 0; desde < sobrantes.Count; desde += TamanoDelLote)
        {
            var lote = sobrantes.Skip(desde).Take(TamanoDelLote).ToList();
            borradas += await db.HistorialDeContenidoWeb
                .Where(entrada => lote.Contains(entrada.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Historial de contenido web podado a {Tope} entradas por clave: {Borradas} filas retiradas de {Total}.",
                Tope, borradas, entradas.Count);
        }
    }
}
