using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using PNMC.Infrastructure.Data;

using PNMC.Api.Security;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Lo que comparten las tres rutas de escritura del CMS: la comprobación
/// antiforgery y el registro del historial.
/// </summary>
public static class GuardasDelCms
{
    // --- Antiforgery -------------------------------------------------------------

    /// <summary>
    /// Comprueba el token antiforgery de una petición de escritura.
    /// <para>
    /// La cookie de sesión ya es <c>SameSite=Lax</c>, que por sí sola impide el
    /// envío desde otro sitio en los navegadores actuales. El token es la segunda
    /// capa: cubre lo que <c>Lax</c> no cubre —un subdominio comprometido, que es
    /// «mismo sitio» para la cookie— y no depende de la versión del navegador.
    /// </para>
    /// <para>
    /// Devuelve <c>null</c> si la petición es válida; si no, el resultado que hay
    /// que responder.
    /// </para>
    /// </summary>
    public static async Task<IResult?> ValidateAntiforgeryAsync(
        IAntiforgery antiforgery,
        HttpContext httpContext)
    {
        // LA COMPROBACION ES LA MISMA QUE EN TODAS PARTES; lo propio de esta guarda es la respuesta,
        // que explica a quien edita el sitio qué hacer. Por eso delega y no vuelve a escribirla.
        if (await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
        {
            return null;
        }

        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["antiforgery"] = [
                    "Falta el token de seguridad o no es válido. " +
                    "Recargue la consola y vuelva a intentarlo."],
            });
        }
    }

    // --- Historial ---------------------------------------------------------------

    public const string AccionGuardado = "guardado";
    public const string AccionPublicado = "publicado";
    public const string AccionRetirado = "retirado";
    public const string AccionRepublicado = "republicado";
    public const string AccionImportado = "importado";

    /// <summary>
    /// Añade una entrada al historial y poda las que sobren de esa clave.
    /// <b>No guarda</b>: se apunta al mismo <see cref="PnmcDbContext"/> para que
    /// todo entre en el mismo <c>SaveChanges</c> que el cambio que describe.
    /// <para>
    /// Que sean la misma transacción es la propiedad que importa: un historial
    /// que se escribe aparte puede quedar contando un cambio que no ocurrió, o
    /// callar uno que sí.
    /// </para>
    /// <para>
    /// La poda va aquí y no en cada ruta porque este método es el <b>único</b>
    /// sitio por el que el panel escribe historial. Puesta en las rutas, la
    /// quinta que alguien añadiera se olvidaría, y esa clave crecería sin techo
    /// sin que nada lo delatara. El tope y su porqué están en
    /// <see cref="PodaDelHistorialContenidoWeb"/>.
    /// </para>
    /// </summary>
    public static async Task RecordHistoryAsync(
        PnmcDbContext dbContext,
        string key,
        string action,
        string? value,
        string user,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        dbContext.HistorialDeContenidoWeb.Add(new HistorialDeContenidoWebRow
        {
            Key = key,
            Action = action,
            Value = value,
            User = user,
            At = at,
        });

        await PodaDelHistorialContenidoWeb.MarcarSobrantesAsync(dbContext, key, cancellationToken);
    }

    /// <summary>
    /// Añade una entrada al historial de una IMAGEN y poda las que sobren de esa clave.
    /// <b>No guarda</b>, por el mismo motivo que <see cref="RecordHistoryAsync"/>: entra en el
    /// mismo <c>SaveChanges</c> que el cambio que describe.
    /// <para>
    /// <b>NO GUARDA EL ARCHIVO.</b> Criterio de producto: «si se remplaza
    /// la imagen, desaparece, no se guarda». Se registra quién, cuándo, qué acción y las señas de
    /// lo que estuvo ahí —tipo, peso, medidas, huella—, que es lo que hace legible el registro sin
    /// conservar megabytes. La consecuencia es que no hay «restaurar».
    /// </para>
    /// <para>
    /// <c>bytes</c> y <c>formato</c> son nulos cuando la acción no trae archivo: retirar quita del
    /// sitio y no reescribe el borrador.
    /// </para>
    /// </summary>
    public static async Task RecordMediaHistoryAsync(
        PnmcDbContext dbContext,
        string key,
        string action,
        int? bytes,
        MediosWebContrato.Formato? formato,
        string? huella,
        string user,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        dbContext.HistorialDeImagenesWeb.Add(new HistorialDeImagenWebRow
        {
            Key = key,
            Action = action,
            Mime = formato?.Mime,
            Bytes = bytes,
            Width = formato?.Width,
            Height = formato?.Height,
            Hash = huella,
            User = user,
            At = at,
        });

        await PodaDelHistorialMediosWeb.MarcarSobrantesAsync(dbContext, key, cancellationToken);
    }

    public static string ResolveAuthor(ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(name) ? "Desconocido" : name.Trim();
    }

    public static bool HasAnyRole(ClaimsPrincipal principal, string[] roles)
    {
        return principal.Identity?.IsAuthenticated == true
            && roles.Any(principal.IsInRole);
    }
}
