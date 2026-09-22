using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Siembra la tabla [ContenidoWeb] con el catálogo de claves editables.
/// <para>
/// El catálogo se genera desde el registro del front-end
/// (<c>pnmc-web/src/app/core/cms/registro-de-textos-web.ts</c>) con
/// <c>npm run cms:catalog</c> y viaja incrustado como recurso. Así no existe una
/// segunda copia de las 238 claves mantenida a mano en C#.
/// </para>
/// </summary>
public static class SembradorDeContenidoWeb
{
    private const string ResourceName = "PNMC.Infrastructure.Data.web-content-catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public sealed class CatalogEntry
    {
        public string Key { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupLabel { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Limit { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Una ranura de imagen del catálogo. La siembra la usa
    /// <see cref="MediosWebSeeder"/>; <c>DefaultUrl</c> no se guarda en la base —vive compilada
    /// en el front— y viaja aquí solo para que el exportador pueda comprobarla.
    /// </summary>
    public sealed class CatalogImage
    {
        public string Key { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string GroupLabel { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Use { get; set; } = "fondo";
        public bool Editable { get; set; } = true;
        public string? Alt { get; set; }
        public int? SuggestedWidth { get; set; }
        public int? SuggestedHeight { get; set; }
        public string DefaultUrl { get; set; } = string.Empty;
    }

    private sealed class Catalog
    {
        public int Version { get; set; }
        public string CatalogHash { get; set; } = string.Empty;
        [JsonPropertyName("entries")]
        public List<CatalogEntry> Entries { get; set; } = new();
        /// <summary>Nómina compilada del equipo (incremento 2B).</summary>
        [JsonPropertyName("team")]
        public List<MiembroDelEquipoWeb> Team { get; set; } = new();
        /// <summary>Ranuras de imagen administrable (29 ago 2026).</summary>
        [JsonPropertyName("images")]
        public List<CatalogImage> Images { get; set; } = new();
    }

    private static Catalog? cached;

    /// <summary>Catálogo incrustado, leído una sola vez por proceso.</summary>
    public static IReadOnlyList<CatalogEntry> LoadCatalog()
    {
        if (cached is not null)
        {
            return cached.Entries;
        }

        var assembly = typeof(SembradorDeContenidoWeb).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"El catálogo de contenido web no está incrustado como '{ResourceName}'. " +
                "Ejecute 'npm run cms:catalog' en pnmc-web y verifique el EmbeddedResource del csproj.");

        cached = JsonSerializer.Deserialize<Catalog>(stream, JsonOptions)
            ?? throw new InvalidOperationException("El catálogo de contenido web incrustado no es un JSON válido.");

        if (cached.Entries.Count == 0)
        {
            throw new InvalidOperationException("El catálogo de contenido web incrustado está vacío.");
        }

        return cached.Entries;
    }

    /// <summary>
    /// Las ranuras de imagen del mismo catálogo incrustado.
    /// <para>
    /// Devuelve una lista vacía, y no lanza, si el JSON no trae el bloque <c>images</c>: un
    /// catálogo exportado antes es válido para los textos, y la ausencia
    /// de imágenes no debe impedir el arranque. <see cref="MediosWebSeeder"/> no siembra nada en
    /// ese caso y el sitio sigue con sus imágenes compiladas.
    /// </para>
    /// </summary>
    public static IReadOnlyList<CatalogImage> LoadImageCatalog()
    {
        // Reutiliza la lectura y la memoria de LoadCatalog: el recurso es el mismo fichero.
        LoadCatalog();
        return cached!.Images;
    }

    public static string CatalogHash
    {
        get
        {
            LoadCatalog();
            return cached!.CatalogHash;
        }
    }

    /// <summary>
    /// Nómina compilada, del mismo catálogo incrustado. La genera el exportador a
    /// partir de <c>TEAM_DEFAULTS</c> del registro TypeScript, aplicando la misma
    /// regla del front-end: sin las vacantes, y con los identificadores fijos.
    /// </summary>
    public static IReadOnlyList<MiembroDelEquipoWeb> LoadTeamDefaults()
    {
        LoadCatalog();
        return cached!.Team;
    }

    /// <summary>
    /// Inserta las claves que faltan y actualiza los metadatos de las existentes.
    /// <para>
    /// <b>No destructivo por diseño:</b> nunca toca <c>Borrador</c> ni
    /// <c>Publicado</c> de una fila que ya existe. Cambiar una etiqueta o un
    /// límite en el registro se propaga; el texto que escribió un editor, no se
    /// pisa. Sembrar dos veces no produce ningún cambio.
    /// </para>
    /// </summary>
    public static async Task EnsureSeededAsync(
        PnmcDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CatalogEntry> catalog;
        try
        {
            catalog = LoadCatalog();
        }
        catch (Exception exception)
        {
            // Sin catálogo no hay nada que sembrar, pero es un fallo de empaquetado
            // que debe verse: el sitio serviría sus textos compilados y nadie lo notaría.
            logger.LogError(exception, "No fue posible leer el catálogo de contenido web; la siembra se omitió.");
            return;
        }

        var existing = await db.ContenidoWeb.ToDictionaryAsync(x => x.Key, cancellationToken);
        var now = DateTime.UtcNow;
        var inserted = 0;
        var refreshed = 0;

        foreach (var entry in catalog)
        {
            if (existing.TryGetValue(entry.Key, out var row))
            {
                // Solo metadatos. El contenido editado es intocable.
                if (row.GroupId == entry.GroupId
                    && row.GroupLabel == entry.GroupLabel
                    && row.Section == entry.Section
                    && row.Label == entry.Label
                    && row.CharacterLimit == entry.Limit)
                {
                    continue;
                }

                row.GroupId = entry.GroupId;
                row.GroupLabel = entry.GroupLabel;
                row.Section = entry.Section;
                row.Label = entry.Label;
                row.CharacterLimit = entry.Limit;
                refreshed++;
                continue;
            }

            db.ContenidoWeb.Add(new ContenidoWebRow
            {
                Key = entry.Key,
                GroupId = entry.GroupId,
                GroupLabel = entry.GroupLabel,
                Section = entry.Section,
                Label = entry.Label,
                CharacterLimit = entry.Limit,
                // El valor compilado entra como borrador, no como publicado: sembrar
                // no debe equivaler a publicar. El sitio sigue sirviendo su texto por
                // defecto hasta que alguien publique de verdad.
                Draft = entry.DefaultValue,
                Published = null,
                Version = 1,
                UpdatedBy = "Sistema",
                UpdatedAt = now,
            });
            inserted++;
        }

        if (inserted == 0 && refreshed == 0)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Catálogo de contenido web sembrado ({Hash}): {Inserted} claves nuevas, {Refreshed} metadatos actualizados, {Total} en catálogo.",
                CatalogHash, inserted, refreshed, catalog.Count);
        }
    }
}
