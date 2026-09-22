using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// El sobre del respaldo que exporta el panel desde <c>localStorage</c>.
/// <para>
/// Se corresponde con <c>RespaldoDeContenidoWeb</c> de
/// <c>pnmc-web/src/app/core/services/textos-web.service.ts</c>. El botón de
/// exportar es trabajo de la Fase 1, así que <b>todo respaldo que pueda existir
/// lo produjo esa implementación</b>: no hay formatos anteriores que soportar.
/// </para>
/// </summary>
public sealed class RespaldoDeContenidoWeb
{
    public const string ExpectedApp = "pnmc-cms";
    public const int SupportedSchemaVersion = 1;

    public const string TextStoreKey = "pnmc_web_texts";
    public const string MediaStoreKey = "pnmc_web_media";
    public const string TeamStoreKey = "pnmc_web_team_members";

    public int SchemaVersion { get; set; }
    public string? App { get; set; }
    public string? ExportedAt { get; set; }
    public string? ExportedBy { get; set; }
    public Dictionary<string, JsonElement>? Stores { get; set; }
}

/// <summary>Un registro de texto tal y como lo guarda el navegador.</summary>
public sealed class BackupTextRecord
{
    public string? Content { get; set; }
    public string? PublishedContent { get; set; }
    public string? Status { get; set; }
    public string? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public List<JsonElement>? History { get; set; }
}

/// <summary>El bloque de nómina del respaldo.</summary>
public sealed class BackupTeamRecord
{
    public List<MiembroDelEquipoWeb?>? Content { get; set; }
    public List<MiembroDelEquipoWeb?>? PublishedContent { get; set; }
    public string? Status { get; set; }
}

public sealed record PlannedText(
    string Key,
    string IncomingDraft,
    string CurrentDraft,
    /// <summary>Version observada al planear; se vuelve a comprobar al aplicar.</summary>
    int Version);

public sealed record SkippedText(string Key, string Reason);

public sealed record SkippedMember(int Index, string Id, string Reason);

/// <summary>
/// El plan de una importación: qué se escribiría y qué no, calculado sin tocar
/// una sola fila.
/// </summary>
public sealed class ImportPlan
{
    public required string PlanHash { get; init; }

    // --- Textos ---
    public List<PlannedText> TextsToApply { get; init; } = [];
    /// <summary>Claves cuyo borrador entrante es idéntico al que ya hay.</summary>
    public int TextsUnchanged { get; init; }
    public List<SkippedText> TextsRejected { get; init; } = [];
    /// <summary>Claves ya editadas en la base que el respaldo pisaría.</summary>
    public List<SkippedText> TextsConflicting { get; init; } = [];
    public List<string> UnknownKeys { get; init; } = [];

    // --- Nómina ---
    public List<MiembroDelEquipoWeb>? TeamToApply { get; init; }
    public bool TeamUnchanged { get; init; }
    public string? TeamRejectedReason { get; init; }
    public List<SkippedMember> MembersRejected { get; init; } = [];
    public int? TeamVersion { get; init; }

    // --- Lo que nunca se importa, pero se cuenta ---
    public int PublishedIdenticalToDraft { get; init; }
    public List<string> PublishedDiffering { get; init; } = [];
    public int DiscardedRevisions { get; init; }
    public int MediaEntries { get; init; }
    public bool TeamPublishedPresent { get; init; }

    public bool WritesAnything => TextsToApply.Count > 0 || TeamToApply is not null;
}

/// <summary>
/// Calcula qué haría una importación. Es deliberadamente <b>puro</b>: recibe el
/// archivo y el estado actual, y devuelve un plan. No conoce el
/// <see cref="PnmcDbContext"/> ni escribe nada, de modo que la ruta de
/// simulación no puede escribir por accidente aunque alguien añada un
/// <c>SaveChanges</c> más adelante en el llamador.
/// </summary>
public static class PlanificadorDeImportacionDeContenidoWeb
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Tope de nombres que se listan; el resto solo se cuenta.</summary>
    public const int MaxNamesReported = 100;

    /// <summary>
    /// Separador de la huella. Es el «unit separator» de ASCII, que no puede
    /// aparecer dentro de una clave ni de un texto validado, para que dos planes
    /// distintos no puedan producir la misma cadena por concatenación.
    /// </summary>
    private const char Separator = '';

    public static string? ValidateEnvelope(RespaldoDeContenidoWeb? file)
    {
        if (file is null)
        {
            return "El cuerpo no contiene un respaldo.";
        }

        if (!string.Equals(file.App, RespaldoDeContenidoWeb.ExpectedApp, StringComparison.Ordinal))
        {
            return "El archivo no es un respaldo del CMS del PNMC.";
        }

        if (file.SchemaVersion <= 0 || file.SchemaVersion > RespaldoDeContenidoWeb.SupportedSchemaVersion)
        {
            return $"El respaldo usa el formato {file.SchemaVersion}, que esta versión de la API no sabe leer " +
                   $"(soporta hasta {RespaldoDeContenidoWeb.SupportedSchemaVersion}).";
        }

        if (file.Stores is null)
        {
            return "El respaldo no contiene almacenes de contenido.";
        }

        return null;
    }

    /// <param name="overwriteEdited">
    /// Si es falso, una clave cuyo borrador en base ya dejó de ser el valor
    /// compilado se considera trabajo de alguien y no se pisa.
    /// </param>
    public static ImportPlan Plan(
        RespaldoDeContenidoWeb file,
        IReadOnlyList<ContenidoWebRow> rows,
        EquipoWebRow? teamRow,
        IReadOnlyDictionary<string, string> compiledDefaults,
        bool overwriteEdited)
    {
        var byKey = rows.ToDictionary(x => x.Key, StringComparer.Ordinal);

        var toApply = new List<PlannedText>();
        var rejected = new List<SkippedText>();
        var conflicting = new List<SkippedText>();
        var unknown = new List<string>();
        var publishedDiffering = new List<string>();
        var unchanged = 0;
        var publishedIdentical = 0;
        var discardedRevisions = 0;

        var texts = ReadStore<Dictionary<string, BackupTextRecord>>(file, RespaldoDeContenidoWeb.TextStoreKey) ?? [];

        foreach (var (key, record) in texts.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (record is null)
            {
                rejected.Add(new SkippedText(key, "El registro llegó vacío."));
                continue;
            }

            discardedRevisions += record.History?.Count ?? 0;

            var incoming = record.Content ?? string.Empty;

            if (record.PublishedContent is not null)
            {
                // Importar no publica. Se separan los dos casos porque significan
                // cosas distintas para el operador: si lo publicado coincide con el
                // borrador no se pierde nada, y si difiere hay un texto que estaba
                // visible en el navegador de alguien y aquí no lo estará.
                if (string.Equals(record.PublishedContent, incoming, StringComparison.Ordinal))
                {
                    publishedIdentical++;
                }
                else
                {
                    publishedDiffering.Add(key);
                }
            }

            if (!byKey.TryGetValue(key, out var row))
            {
                // Incluye las claves heredadas 'about_team_N_*'. No se traducen: se
                // nombran. Verificado en el repositorio que nada las escribe nunca,
                // así que traducirlas sería código para un caso que no ocurre; y si
                // ocurriera, el informe las nombra y no se pierden en silencio.
                unknown.Add(key);
                continue;
            }

            // Se valida contra el límite VIGENTE de la fila, no contra el que
            // regía cuando se exportó: el catálogo pudo cambiar desde entonces.
            if (incoming.Length > row.CharacterLimit)
            {
                rejected.Add(new SkippedText(
                    key, $"Excede el límite de {row.CharacterLimit} caracteres (trae {incoming.Length})."));
                continue;
            }

            if (incoming.Contains('<'))
            {
                rejected.Add(new SkippedText(
                    key, $"Contiene el carácter '<' en la posición {incoming.IndexOf('<') + 1}."));
                continue;
            }

            if (string.Equals(row.Draft, incoming, StringComparison.Ordinal))
            {
                unchanged++;
                continue;
            }

            // «Ya editada» se deduce comparando con el valor compilado, sin
            // ninguna columna extra: una fila cuyo borrador sigue siendo el del
            // catálogo es una fila que nadie tocó.
            var compiled = compiledDefaults.GetValueOrDefault(key, string.Empty);
            var alreadyEdited = !string.Equals(row.Draft, compiled, StringComparison.Ordinal);
            if (alreadyEdited && !overwriteEdited)
            {
                conflicting.Add(new SkippedText(
                    key, "El borrador en la base ya fue editado y no coincide con el respaldo."));
                continue;
            }

            toApply.Add(new PlannedText(key, incoming, row.Draft, row.Version));
        }

        var (teamToApply, teamUnchanged, teamReason, membersRejected, teamPublishedPresent) =
            PlanTeam(file, teamRow);

        var media = ReadStore<Dictionary<string, JsonElement>>(file, RespaldoDeContenidoWeb.MediaStoreKey);

        var plan = new ImportPlan
        {
            PlanHash = string.Empty,
            TextsToApply = toApply,
            TextsUnchanged = unchanged,
            TextsRejected = rejected,
            TextsConflicting = conflicting,
            UnknownKeys = unknown,
            TeamToApply = teamToApply,
            TeamUnchanged = teamUnchanged,
            TeamRejectedReason = teamReason,
            MembersRejected = membersRejected,
            TeamVersion = teamRow?.Version,
            PublishedIdenticalToDraft = publishedIdentical,
            PublishedDiffering = publishedDiffering,
            DiscardedRevisions = discardedRevisions,
            MediaEntries = media?.Count ?? 0,
            TeamPublishedPresent = teamPublishedPresent,
        };

        return CloneWithHash(plan, ComputeHash(plan));
    }

    private static (List<MiembroDelEquipoWeb>? ToApply, bool Unchanged, string? Reason, List<SkippedMember> Rejected, bool PublishedPresent)
        PlanTeam(RespaldoDeContenidoWeb file, EquipoWebRow? teamRow)
    {
        BackupTeamRecord? team;
        try
        {
            team = ReadStore<BackupTeamRecord>(file, RespaldoDeContenidoWeb.TeamStoreKey);
        }
        catch (JsonException)
        {
            return (null, false, "El bloque de equipo del respaldo está dañado.", [], false);
        }

        if (team is null)
        {
            return (null, false, "El respaldo no trae bloque de equipo.", [], false);
        }

        var publishedPresent = team.PublishedContent is not null;

        if (team.Content is null)
        {
            return (null, false, "El bloque de equipo no trae la lista de personas.", [], publishedPresent);
        }

        if (teamRow is null)
        {
            return (null, false, "La nómina no está inicializada en la base de datos.", [], publishedPresent);
        }

        // La nómina es UN valor: entra entera o no entra. Rechazar solo a las
        // personas invalidas dejaria una nomina a medias que nadie pidio, y el
        // operador no podria distinguir «faltaba esa persona» de «se cayo al
        // importar». Por eso se informa persona por persona pero no se aplica.
        var errors = ContratoDeEquipoWeb.Validate(team.Content, out var normalized);
        if (errors.Count > 0)
        {
            var rejected = errors
                .Select(entry => new SkippedMember(
                    IndexFromField(entry.Key),
                    IdAt(team.Content, IndexFromField(entry.Key)),
                    string.Join(" ", entry.Value)))
                .OrderBy(x => x.Index)
                .ToList();

            return (null, false, "Hay personas que no cumplen el contrato de la nómina.", rejected, publishedPresent);
        }

        var incoming = ContratoDeEquipoWeb.Serialize(normalized);
        if (string.Equals(teamRow.Draft, incoming, StringComparison.Ordinal))
        {
            return (null, true, null, [], publishedPresent);
        }

        return (normalized, false, null, [], publishedPresent);
    }

    private static int IndexFromField(string field)
    {
        var open = field.IndexOf('[');
        var close = field.IndexOf(']');
        if (open < 0 || close < open) { return -1; }
        return int.TryParse(field.AsSpan(open + 1, close - open - 1), out var index) ? index : -1;
    }

    private static string IdAt(List<MiembroDelEquipoWeb?> members, int index)
        => index >= 0 && index < members.Count ? members[index]?.Id ?? string.Empty : string.Empty;

    private static T? ReadStore<T>(RespaldoDeContenidoWeb file, string storeKey) where T : class
    {
        if (file.Stores is null || !file.Stores.TryGetValue(storeKey, out var element))
        {
            return null;
        }

        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return element.Deserialize<T>(JsonOptions);
    }

    /// <summary>
    /// Huella de lo que el plan escribiría. El operador confirma con ella, y al
    /// aplicar se recalcula: si no coincide, el archivo o el estado de la base
    /// cambiaron entre mirar y apretar el botón, y no se escribe nada.
    /// <para>
    /// Incluye las versiones observadas, así que una edición ajena entre los dos
    /// pasos también mueve la huella.
    /// </para>
    /// </summary>
    private static string ComputeHash(ImportPlan plan)
    {
        var builder = new StringBuilder();
        foreach (var text in plan.TextsToApply)
        {
            builder.Append(text.Key).Append(Separator)
                   .Append(text.Version).Append(Separator)
                   .Append(text.IncomingDraft).Append(Separator);
        }

        builder.Append("::equipo::").Append(Separator).Append(plan.TeamVersion?.ToString() ?? "-").Append(Separator);
        if (plan.TeamToApply is not null)
        {
            builder.Append(ContratoDeEquipoWeb.Serialize(plan.TeamToApply));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }

    private static ImportPlan CloneWithHash(ImportPlan plan, string hash) => new()
    {
        PlanHash = hash,
        TextsToApply = plan.TextsToApply,
        TextsUnchanged = plan.TextsUnchanged,
        TextsRejected = plan.TextsRejected,
        TextsConflicting = plan.TextsConflicting,
        UnknownKeys = plan.UnknownKeys,
        TeamToApply = plan.TeamToApply,
        TeamUnchanged = plan.TeamUnchanged,
        TeamRejectedReason = plan.TeamRejectedReason,
        MembersRejected = plan.MembersRejected,
        TeamVersion = plan.TeamVersion,
        PublishedIdenticalToDraft = plan.PublishedIdenticalToDraft,
        PublishedDiffering = plan.PublishedDiffering,
        DiscardedRevisions = plan.DiscardedRevisions,
        MediaEntries = plan.MediaEntries,
        TeamPublishedPresent = plan.TeamPublishedPresent,
    };
}
