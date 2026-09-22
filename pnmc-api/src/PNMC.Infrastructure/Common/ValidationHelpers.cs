using System.Net.Mail;
using System.Text;

namespace PNMC.Infrastructure.Common;

public static class ValidationHelpers
{
    public static bool IsMissing(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// ¿Es un correo que esta base va a aceptar?
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL PUNTO DESPUES DE LA ARROBA NO ES UN CAPRICHO: lo exige la base.
    /// <c>dbo.Usuarios</c> lleva <c>CK_Usuarios_CorreoElectronico_Formato CHECK (CorreoElectronico
    /// LIKE '%_@_%._%')</c>. <see cref="MailAddress"/> por su cuenta acepta <c>juan@gmail</c> y
    /// <c>juan@alcaldia</c> sin rechistar, asi que la errata mas comun del mundo —escribir el correo
    /// y comerse el «.com»— pasaba el navegador, pasaba el API, y la tumbaba la base al guardar: un
    /// 500 en vez de «el correo no es valido».
    /// </para>
    /// <para>
    /// Se comprueba aqui, en la funcion que ya usan todas las rutas, y no en cada manejador: un
    /// limite de la base que solo conoce la mitad de quienes escriben en ella es el mismo defecto
    /// contado otra vez.
    /// </para>
    /// </remarks>
    public static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            var recortado = value.Trim();
            var parsed = new MailAddress(recortado);
            if (!string.Equals(parsed.Address, recortado, StringComparison.OrdinalIgnoreCase)) return false;

            // El dominio tiene que tener al menos dos etiquetas, y la ultima no puede ir vacia.
            var dominio = parsed.Host;
            var punto = dominio.IndexOf('.', StringComparison.Ordinal);
            return punto > 0 && punto < dominio.Length - 1;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValidHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    public static string SanitizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (!char.IsControl(ch) || ch is '\n' or '\r' or '\t')
            {
                builder.Append(ch);
            }
        }

        var sanitized = builder.ToString().Trim();
        if (sanitized.Length <= maxLength)
        {
            return sanitized;
        }

        return sanitized[..maxLength].TrimEnd();
    }
}
