using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Siembra la tabla <c>dbo.MediosWeb</c> con las ranuras de imagen del catálogo.
/// <para>
/// El catálogo sale del mismo JSON incrustado que los textos, generado desde
/// <c>pnmc-web/src/app/core/cms/registro-de-imagenes-web.ts</c> con <c>npm run cms:catalog</c>. Así no
/// existe una segunda copia de las 44 ranuras mantenida a mano en C#.
/// </para>
/// <para>
/// <b>SEMBRAR NO ES PUBLICAR, y es la propiedad que hace seguro este cambio.</b> Las filas nacen
/// con las catorce columnas de blob en NULL: el manifiesto público sale vacío, ninguna
/// <c>&lt;img&gt;</c> apunta al API, y el sitio se ve exactamente igual que el día antes. La URL
/// compilada de cada ranura sigue siendo la que el front tiene escrita, y solo la reemplaza lo que
/// alguien publique a propósito desde el panel.
/// </para>
/// </summary>
public static class MediosWebSeeder
{
    /// <summary>
    /// Inserta las ranuras que faltan y refresca los metadatos de las existentes.
    /// <para>
    /// <b>No destructivo por diseño:</b> nunca toca los blobs de una fila que ya existe. Cambiar
    /// una etiqueta o un grupo en el registro se propaga; la imagen que subió una editora, no se
    /// pisa. Sembrar dos veces no produce ningún cambio.
    /// </para>
    /// <para>
    /// <b>El texto alternativo tampoco se refresca</b>, y es la única excepción que no es obvia:
    /// el del registro es solo el de partida, y el panel lo puede cambiar. Refrescarlo aquí
    /// desharía esa edición en cada arranque, que es justo la clase de defecto que tarda semanas
    /// en verse porque no rompe nada.
    /// </para>
    /// </summary>
    public static async Task EnsureSeededAsync(
        PnmcDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        IReadOnlyList<SembradorDeContenidoWeb.CatalogImage> catalogo;
        try
        {
            catalogo = SembradorDeContenidoWeb.LoadImageCatalog();
        }
        catch (Exception exception)
        {
            // Sin catálogo no hay ranuras que sembrar. Es un fallo de empaquetado y tiene que
            // verse: el panel mostraría cero imágenes y nadie sabría por qué.
            logger.LogError(exception, "No fue posible leer el catálogo de imágenes web; la siembra se omitió.");
            return;
        }

        if (catalogo.Count == 0)
        {
            return;
        }

        // Proyección sin blobs: sembrar no necesita los bytes, y traerlos aquí serían decenas de
        // megabytes en cada arranque para no mirarlos.
        var existentes = await db.ImagenesWeb
            .Select(x => new
            {
                x.Id,
                x.Key,
                x.GroupId,
                x.GroupLabel,
                x.Section,
                x.Label,
                x.Use,
                x.Editable,
                x.SuggestedWidth,
                x.SuggestedHeight,
                // LA VERSIÓN SE TRAE AUNQUE NO SE REFRESQUE, y no es un descuido: `Version` es
                // token de concurrencia (PnmcDbContext.cs), así que EF la mete en el WHERE
                // del UPDATE. Sin ella el fantasma sale con `Version = 0`, ninguna fila casa y el
                // arranque muere. Ver el comentario del refresco.
                x.Version,
            })
            .ToDictionaryAsync(x => x.Key, cancellationToken);

        var ahora = DateTime.UtcNow;
        var insertadas = 0;
        var refrescadas = 0;

        foreach (var ranura in catalogo)
        {
            if (existentes.TryGetValue(ranura.Key, out var actual))
            {
                if (actual.GroupId == ranura.GroupId
                    && actual.GroupLabel == ranura.GroupLabel
                    && actual.Section == ranura.Section
                    && actual.Label == ranura.Label
                    && actual.Use == ranura.Use
                    && actual.Editable == ranura.Editable
                    && actual.SuggestedWidth == ranura.SuggestedWidth
                    && actual.SuggestedHeight == ranura.SuggestedHeight)
                {
                    continue;
                }

                // Se adjunta una instancia mínima y se marcan solo las ocho propiedades de
                // metadatos: así el UPDATE no nombra ninguna columna varbinary y EF no necesita
                // haber leído los blobs para escribir.
                //
                // <b>`Version` VA EN EL INICIALIZADOR Y NO DESPUÉS DE `Attach`.</b> `Attach` toma
                // la foto de los valores originales en ese instante; asignarla después la
                // contaría como un cambio en vez de como el valor que va al WHERE. Es la línea
                // que faltaba: sin ella el UPDATE llevaba
                // `WHERE Version = 0`, afectaba 0 filas, EF lanzaba DbUpdateConcurrencyException
                // y el arranque entero caía en modo degradado —también la poda del historial, que
                // va después—. No se vio antes porque este camino solo corre cuando algo del
                // catálogo cambió, y hasta ese día nunca había cambiado.
                var fantasma = new ImagenWebRow
                {
                    Id = actual.Id,
                    Key = ranura.Key,
                    Version = actual.Version,
                };
                var entrada = db.ImagenesWeb.Attach(fantasma);
                fantasma.GroupId = ranura.GroupId;
                fantasma.GroupLabel = ranura.GroupLabel;
                fantasma.Section = ranura.Section;
                fantasma.Label = ranura.Label;
                fantasma.Use = ranura.Use;
                fantasma.Editable = ranura.Editable;
                fantasma.SuggestedWidth = ranura.SuggestedWidth;
                fantasma.SuggestedHeight = ranura.SuggestedHeight;
                entrada.Property(x => x.GroupId).IsModified = true;
                entrada.Property(x => x.GroupLabel).IsModified = true;
                entrada.Property(x => x.Section).IsModified = true;
                entrada.Property(x => x.Label).IsModified = true;
                entrada.Property(x => x.Use).IsModified = true;
                entrada.Property(x => x.Editable).IsModified = true;
                entrada.Property(x => x.SuggestedWidth).IsModified = true;
                entrada.Property(x => x.SuggestedHeight).IsModified = true;
                refrescadas++;
                continue;
            }

            db.ImagenesWeb.Add(new ImagenWebRow
            {
                Key = ranura.Key,
                GroupId = ranura.GroupId,
                GroupLabel = ranura.GroupLabel,
                Section = ranura.Section,
                Label = ranura.Label,
                Use = ranura.Use,
                Editable = ranura.Editable,
                AltText = ranura.Alt ?? string.Empty,
                SuggestedWidth = ranura.SuggestedWidth,
                SuggestedHeight = ranura.SuggestedHeight,
                // Las catorce columnas de blob quedan en NULL. Ver el resumen de la clase.
                Version = 1,
                UpdatedBy = "Sistema",
                UpdatedAt = ahora,
            });
            insertadas++;
        }

        if (insertadas == 0 && refrescadas == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Catálogo de imágenes web sembrado: {Insertadas} ranuras nuevas, {Refrescadas} metadatos actualizados, {Total} en catálogo.",
                insertadas, refrescadas, catalogo.Count);
        }
    }
}
