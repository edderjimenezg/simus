using Microsoft.EntityFrameworkCore;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// El tope del historial de imágenes.
/// <para>
/// Es gemelo de <see cref="PodaDelHistorialContenidoWeb"/> y comparte su constante
/// <see cref="PodaDelHistorialContenidoWeb.Tope"/> a propósito: dos números distintos para
/// «cuántas versiones guarda el CMS» serían dos respuestas a la misma pregunta, y la del panel
/// tendría que decir cuál.
/// </para>
/// <para>
/// <b>El tope ya no acota bytes, acota ruido.</b> Hasta este historial
/// guardaba el archivo y seis entradas de una clave podían pesar 12 MiB; hoy no lo guarda
/// (<c>V20260829_03__medios_historial_sin_archivo.sql</c>), así que una entrada son unos 200
/// bytes y 45 claves por 6 entradas son unos 54 KB. El tope se conserva porque una lista de
/// cambios sin fin no se lee, no porque cueste espacio.
/// </para>
/// </summary>
public static class PodaDelHistorialMediosWeb
{
    /// <summary>
    /// Marca para borrado las entradas que sobran de una clave, contando las que se están a punto
    /// de insertar.
    /// </summary>
    /// <remarks>
    /// <b>No guarda.</b> Se apunta al mismo contexto para que la poda entre en el mismo
    /// <c>SaveChanges</c> que el cambio que la provoca. Si fuesen dos transacciones, un fallo
    /// entre ambas dejaría el historial por encima del tope hasta la siguiente escritura.
    /// </remarks>
    public static async Task MarcarSobrantesAsync(
        PnmcDbContext db,
        string clave,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        // Las pendientes se cuentan desde el rastreador y no se dan por supuestas: una petición
        // que escribiera dos veces la misma clave conservaría una de más si aquí se asumiera
        // «siempre se añade una».
        var pendientes = db.ChangeTracker
            .Entries<HistorialDeImagenWebRow>()
            .Count(entrada => entrada.State == EntityState.Added
                && string.Equals(entrada.Entity.Key, clave, StringComparison.Ordinal));

        var conservarDeLaBase = Math.Max(0, PodaDelHistorialContenidoWeb.Tope - pendientes);

        // SE PROYECTA SOLO EL IDENTIFICADOR. Con el archivo fuera del historial ya no ahorra
        // megabytes, pero sigue siendo lo correcto: un DELETE por clave primaria no necesita que
        // EF haya leído la fila, y así esta poda no cambia de coste si mañana el historial vuelve
        // a crecer.
        var sobrantes = await db.HistorialDeImagenesWeb
            .Where(entrada => entrada.Key == clave)
            .OrderByDescending(entrada => entrada.At)
            .ThenByDescending(entrada => entrada.Id)
            .Skip(conservarDeLaBase)
            .Select(entrada => entrada.Id)
            .ToListAsync(cancellationToken);

        if (sobrantes.Count == 0)
        {
            return;
        }

        foreach (var id in sobrantes)
        {
            // Instancia mínima con la clave primaria puesta: EF emite el DELETE por el Id sin
            // haber leído nunca el contenido.
            var fantasma = new HistorialDeImagenWebRow { Id = id };
            db.HistorialDeImagenesWeb.Attach(fantasma);
            db.HistorialDeImagenesWeb.Remove(fantasma);
        }
    }
}
