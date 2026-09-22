namespace PNMC.Api.Security;

/// <summary>
/// Regla de claves para altas externas. Privilegia longitud y listas de bloqueo sobre exigir una
/// combinación artificial de símbolos, que reduce frases de paso memorables sin impedir claves
/// previsibles.
/// </summary>
internal static class PoliticaDeContrasena
{
    internal const int LongitudMinima = 12;
    internal const int LongitudMaxima = 128;

    private static readonly HashSet<string> Bloqueadas = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password123", "password1234", "123456789012", "1234567890",
        "qwertyuiop", "qwerty123", "contrasena", "contraseña", "clave123456",
        "simus123456", "pnmc123456", "admin123456"
    };

    internal static string? Error(string? clave, string? correo, IEnumerable<string?> datosPersonales)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Length < LongitudMinima)
            return $"La contraseña debe tener al menos {LongitudMinima} caracteres.";
        if (clave.Length > LongitudMaxima)
            return $"La contraseña no puede superar {LongitudMaxima} caracteres.";
        if (Bloqueadas.Contains(clave.Trim()))
            return "Esta contraseña es demasiado común. Elige una frase de paso distinta.";
        if (EsRepetitivaOConsecutiva(clave))
            return "Esta contraseña es predecible. Elige una frase de paso distinta.";

        var partes = datosPersonales.Append(correo?.Split('@')[0])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Where(item => item.Length >= 3);
        if (partes.Any(item => clave.Contains(item, StringComparison.OrdinalIgnoreCase)))
            return "La contraseña no debe contener tu nombre ni tu correo electrónico.";
        return null;
    }

    private static bool EsRepetitivaOConsecutiva(string clave)
    {
        if (clave.Distinct().Count() == 1) return true;
        const string ascendentes = "0123456789abcdefghijklmnopqrstuvwxyz";
        var minuscula = clave.ToLowerInvariant();
        return Enumerable.Range(0, Math.Max(0, minuscula.Length - 3))
            .Any(indice => ascendentes.Contains(minuscula.Substring(indice, 4), StringComparison.Ordinal)
                       || ascendentes.Contains(new string(minuscula.Substring(indice, 4).Reverse().ToArray()), StringComparison.Ordinal));
    }
}
