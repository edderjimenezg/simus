using System.Text.Json;
using System.Text.Json.Serialization;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Una persona de la nómina, tal y como viaja dentro del JSON de
/// <see cref="EquipoWebRow"/>.
/// <para>
/// Los nombres coinciden con los del front-end (<c>MiembroDelEquipoWeb</c>) para que el
/// respaldo exportado desde el panel y lo que guarda la API sean la misma forma:
/// si divergieran, el importador del incremento 2C tendría que traducir.
/// </para>
/// </summary>
public sealed record MiembroDelEquipoWeb(
    string Id,
    string Group,
    string Role,
    string Name,
    string Email,
    string Photo);

/// <summary>
/// Los topes de la nómina y la validación que los aplica.
/// <para>
/// Vive en Infrastructure y no en el endpoint porque la siembra también los
/// necesita: sembrar un dato que después nadie pueda volver a guardar desde el
/// panel dejaría la nómina bloqueada sin que nada lo avise.
/// </para>
/// </summary>
public static class ContratoDeEquipoWeb
{
    /// <summary>La nómina real tiene 9 personas. El tope deja margen sin ser ilimitado.</summary>
    public const int MaxMembers = 40;

    public const int MaxIdLength = 40;
    public const int MaxNameLength = 120;
    public const int MaxRoleLength = 160;
    public const int MaxEmailLength = 180;

    /// <summary>
    /// Tope del texto de toda la nómina —todo menos las fotografías— sumado.
    /// 40 personas con sus cuatro campos llenos no llegan a 20 000 caracteres;
    /// 16 KiB es holgado para el uso real y acotado para el abuso.
    /// </summary>
    public const int MaxTextChars = 16 * 1024;

    /// <summary>
    /// Tope de una fotografía en caracteres del data URI. El panel las reduce a
    /// 480×600 WebP, que pesan unos 25 KB; 300 000 caracteres son ~220 KB, así que
    /// deja pasar una foto mala sin dejar pasar un archivo cualquiera.
    /// </summary>
    public const int MaxPhotoChars = 300_000;

    /// <summary>
    /// Tope de la nómina serializada. Es el que acota de verdad lo que se guarda
    /// en la columna y lo que el sitio público descarga en cada visita.
    /// </summary>
    public const int MaxSerializedChars = 2_000_000;

    private static readonly string[] AllowedGroups = ["coordination", "leadership"];

    private static readonly string[] AllowedPhotoPrefixes =
    [
        "data:image/webp;base64,",
        "data:image/jpeg;base64,",
        "data:image/png;base64,",
    ];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Serialize(IReadOnlyList<MiembroDelEquipoWeb> members)
        => JsonSerializer.Serialize(members, JsonOptions);

    /// <summary>
    /// Lee una nómina guardada. Devuelve una lista vacía si el JSON está roto:
    /// una fila corrupta no debe tumbar la página de «Sobre el PNMC».
    /// </summary>
    public static IReadOnlyList<MiembroDelEquipoWeb> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<MiembroDelEquipoWeb>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Valida y normaliza la nómina recibida. Devuelve los errores por campo, con
    /// la misma forma que usa <c>Results.ValidationProblem</c>, o una lista vacía
    /// si todo está bien.
    /// </summary>
    /// <remarks>
    /// El orden importa: primero el número de personas, después la longitud de
    /// cada texto y de cada fotografía, y solo al final se serializa. Así ningún
    /// tope se comprueba sobre un dato que ya se copió entero en memoria.
    /// </remarks>
    public static Dictionary<string, string[]> Validate(
        IReadOnlyList<MiembroDelEquipoWeb?>? incoming,
        out List<MiembroDelEquipoWeb> normalized)
    {
        normalized = [];
        var errors = new Dictionary<string, string[]>();

        if (incoming is null)
        {
            errors["members"] = ["Debe enviar la nómina, aunque sea vacía."];
            return errors;
        }

        if (incoming.Count > MaxMembers)
        {
            errors["members"] = [$"La nómina admite hasta {MaxMembers} personas (se recibieron {incoming.Count})."];
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var textChars = 0;

        for (var index = 0; index < incoming.Count; index++)
        {
            var member = incoming[index];
            var field = $"members[{index}]";
            if (member is null)
            {
                errors[field] = ["La persona llegó vacía."];
                continue;
            }

            var id = (member.Id ?? string.Empty).Trim();
            var group = (member.Group ?? string.Empty).Trim();
            var role = (member.Role ?? string.Empty).Trim();
            var name = (member.Name ?? string.Empty).Trim();
            var email = (member.Email ?? string.Empty).Trim();
            var photo = member.Photo ?? string.Empty;

            var problems = new List<string>();

            if (id.Length == 0) { problems.Add("Falta el identificador."); }
            else if (id.Length > MaxIdLength) { problems.Add($"El identificador excede {MaxIdLength} caracteres."); }
            else if (!ids.Add(id)) { problems.Add($"El identificador '{id}' está repetido."); }

            if (!AllowedGroups.Contains(group, StringComparer.Ordinal))
            {
                problems.Add("El grupo debe ser 'coordination' o 'leadership'.");
            }

            if (name.Length == 0) { problems.Add("Falta el nombre."); }
            else if (name.Length > MaxNameLength) { problems.Add($"El nombre excede {MaxNameLength} caracteres."); }

            if (role.Length > MaxRoleLength) { problems.Add($"El cargo excede {MaxRoleLength} caracteres."); }
            if (email.Length > MaxEmailLength) { problems.Add($"El correo excede {MaxEmailLength} caracteres."); }

            if (email.Length > 0 && (!email.Contains('@') || email.Any(char.IsWhiteSpace)))
            {
                problems.Add("El correo no tiene una forma válida.");
            }

            // Mismo motivo que en los 238 textos: hoy se interpolan y Angular los
            // escapa, pero rechazar '<' cierra la via antes de que alguna plantilla
            // futura los renderice como marcado.
            if (name.Contains('<') || role.Contains('<') || email.Contains('<'))
            {
                problems.Add("El texto no admite el carácter '<'.");
            }

            var photoProblem = ValidatePhoto(photo);
            if (photoProblem is not null) { problems.Add(photoProblem); }

            textChars += id.Length + group.Length + role.Length + name.Length + email.Length;

            if (problems.Count > 0)
            {
                errors[field] = [.. problems];
                continue;
            }

            normalized.Add(new MiembroDelEquipoWeb(id, group, role, name, email, photo));
        }

        if (textChars > MaxTextChars)
        {
            errors["members"] = [$"El texto de la nómina excede {MaxTextChars} caracteres (tiene {textChars})."];
        }

        if (errors.Count > 0)
        {
            normalized = [];
            return errors;
        }

        var serialized = Serialize(normalized);
        if (serialized.Length > MaxSerializedChars)
        {
            normalized = [];
            errors["members"] = [
                $"La nómina completa pesa {serialized.Length} caracteres y el máximo es {MaxSerializedChars}. " +
                "Use fotografías más livianas."];
        }

        return errors;
    }

    /// <summary>
    /// Comprueba una fotografía sin decodificarla.
    /// <para>
    /// La longitud se mira <b>antes</b> que nada: decodificar para averiguar el
    /// tamaño reservaría en memoria lo que el cliente decida enviar, que es
    /// justamente lo que el tope debe impedir. Y una vez comprobada, tampoco hace
    /// falta decodificar: recorrer el alfabeto base64 dice si el dato es válido
    /// sin reservar el búfer.
    /// </para>
    /// </summary>
    private static string? ValidatePhoto(string photo)
    {
        if (photo.Length == 0)
        {
            return null;
        }

        if (photo.Length > MaxPhotoChars)
        {
            return $"La fotografía pesa {photo.Length} caracteres y el máximo es {MaxPhotoChars}.";
        }

        var prefix = AllowedPhotoPrefixes.FirstOrDefault(p => photo.StartsWith(p, StringComparison.Ordinal));
        if (prefix is null)
        {
            return "La fotografía debe ser un data URI WebP, JPEG o PNG en base64.";
        }

        const string malFormado = "La fotografía no es base64 válido.";
        var payload = photo.AsSpan(prefix.Length);
        if (payload.Length == 0 || payload.Length % 4 != 0)
        {
            return malFormado;
        }

        // El relleno son uno o dos '=' al final, nunca en medio. Se recorta primero
        // y lo que quede debe ser alfabeto base64 puro.
        var padding = 0;
        while (padding < payload.Length && payload[^(padding + 1)] == '=')
        {
            padding++;
        }

        if (padding > 2)
        {
            return malFormado;
        }

        foreach (var c in payload[..^padding])
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '+' && c != '/')
            {
                return malFormado;
            }
        }

        return null;
    }
}
