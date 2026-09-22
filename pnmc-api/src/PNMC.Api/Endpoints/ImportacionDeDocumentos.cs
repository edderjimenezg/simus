using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// El tramo común de leer un documento, sea para el Catálogo Editorial o para la Agenda.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE ESTA EXTRAIDO.</b> Un afiche y una publicación son documentos distintos, pero lo que
/// hay que hacerles es lo mismo: comprobar que es un PDF de verdad, leer sus metadatos, sacar el
/// texto con sus tamaños de letra y renderizar la primera página. Lo único que cambia es qué se
/// reconoce después y en qué formulario desemboca. Duplicar esta parte en cada módulo sería duplicar
/// la superficie donde una de las copias se queda corta —que es justo lo que le pasó al banco de
/// archivos con el canal externo, y por eso allí se reutiliza el mismo manejador—.
/// </para>
/// <para>
/// <b>LA PORTADA SE GUARDA Y LO DEMAS NO.</b> Es la única excepción, y tiene motivo: sin un
/// identificador de archivo no hay forma de enseñar la imagen en el formulario ni de asociarla al
/// registro después. Todo lo demás se devuelve como propuesta y no toca ninguna tabla.
/// </para>
/// </remarks>
internal static class ImportacionDeDocumentos
{
    internal static readonly string[] AdjunteUnDocumento = ["Adjunte un documento."];
    internal static readonly string[] DocumentoDemasiadoGrande = ["El documento excede el tamaño máximo."];
    internal static readonly string[] SoloSeLeenPdf = ["Por ahora solo se leen documentos PDF."];

    /// <summary>
    /// Deja que la petición llegue hasta la comprobación de tamaño de esta clase.
    /// </summary>
    /// <remarks>
    /// <b>SIN ESTO, UN DOCUMENTO GRANDE DEVUELVE UN 500 OPACO.</b> El marco corta las peticiones de
    /// formulario en unos 28 MiB por su cuenta, así que un PDF de 37 MB —un método de 208 páginas,
    /// medido— reventaba ANTES de llegar al código que sabe decir «el documento excede el tamaño
    /// máximo». Quien lo subía veía «Unexpected error» y no tenía forma de saber qué había pasado.
    ///
    /// Se sube el límite del marco al del contrato para que <b>quien conteste sea siempre esta clase</b>,
    /// con un mensaje que dice qué pasó y cuál es el tope.
    /// </remarks>
    internal static RouteHandlerBuilder AdmiteDocumentos(this RouteHandlerBuilder ruta) =>
        ruta.WithMetadata(new RequestFormLimitsAttribute
        {
            MultipartBodyLengthLimit = MediosWebContrato.MaxBytesDocumento,
        })
        .WithMetadata(new RequestSizeLimitAttribute(MediosWebContrato.MaxBytesDocumento + (1024 * 1024)));

    /// <summary>Lo que se pudo leer del documento, antes de interpretarlo para un módulo concreto.</summary>
    internal sealed record DocumentoLeido(
        byte[] Datos,
        string NombreOriginal,
        ExtractorDeDocumentos.PropuestaDeDocumento Metadatos,
        LecturaDelDocumento.LecturaDeDocumento? Lectura);

    /// <summary>
    /// Comprueba el archivo y lo lee. Devuelve el error listo para responder si algo falla.
    /// </summary>
    internal static async Task<(DocumentoLeido? Leido, IResult? Error)> LeerAsync(
        HttpRequest peticion, CancellationToken ct)
    {
        if (!peticion.HasFormContentType)
        {
            return (null, Results.BadRequest(new { file = AdjunteUnDocumento }));
        }

        var formulario = await peticion.ReadFormAsync(ct);
        var archivo = formulario.Files["file"];
        if (archivo is null || archivo.Length == 0)
        {
            return (null, Results.BadRequest(new { file = AdjunteUnDocumento }));
        }

        // EL TOPE, ANTES DE LEER NADA: medir después de cargar es reservar la memoria que se quería
        // evitar. Es la misma cadena de comprobaciones del banco de archivos, en el mismo orden.
        if (archivo.Length > MediosWebContrato.MaxBytesDocumento)
        {
            return (null, Results.BadRequest(new { file = DocumentoDemasiadoGrande }));
        }

        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, ct);
        var datos = memoria.ToArray();

        // EL FORMATO, POR SU FIRMA y no por lo que declare el cliente.
        if (!MediosWebContrato.EsPdf(datos))
        {
            return (null, Results.BadRequest(new { file = SoloSeLeenPdf }));
        }

        LecturaDelDocumento.LecturaDeDocumento? lectura = null;
        try
        {
            lectura = LecturaDelDocumento.Leer(datos);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // UN PDF ILEGIBLE NO TUMBA LA CARGA: se sigue con lo que dieran los metadatos, que es más
            // que nada, y la pantalla dirá qué se pudo leer y qué no.
        }

        return (new DocumentoLeido(
            datos,
            archivo.FileName ?? "documento.pdf",
            ExtractorDeDocumentos.Leer(datos),
            lectura), null);
    }

    /// <summary>
    /// Renderiza la primera página y la guarda en el banco de archivos.
    /// </summary>
    /// <remarks>
    /// SE DEDUPLICA POR HUELLA: leer dos veces el mismo documento no debe dejar dos portadas iguales
    /// ocupando sitio. Es la misma regla que el banco aplica a cualquier imagen.
    /// </remarks>
    internal static async Task<(int? Id, string? Url)> GuardarPortadaAsync(
        DocumentoLeido documento, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        var portada = PortadaDelDocumento.Renderizar(documento.Datos);
        if (portada is null) { return (null, null); }

        var huella = MediosWebContrato.Huella(portada);
        var fila = await db.Files.FirstOrDefaultAsync(x => x.Fingerprint == huella && x.OrganizacionId == null, ct);

        if (fila is null)
        {
            var formato = MediosWebContrato.Reconocer(portada);
            var nombre = Recortar(documento.NombreOriginal, 200) ?? "documento";
            fila = new FileRow
            {
                OriginalName = "portada-" + nombre + ".png",
                StoredName = "pnmc-" + huella + ".png",
                MimeType = "image/png",
                Extension = ".png",
                FileSizeBytes = portada.Length,
                StoragePath = "/api/v1/publico/archivos/pnmc-" + huella + ".png",
                AltText = "Portada de " + nombre,
                UploadedByUserId = Actor(principal),
                CreatedAt = DateTime.UtcNow,
                Content = portada,
                Fingerprint = huella,
                Width = formato?.Width,
                Height = formato?.Height,
            };
            db.Files.Add(fila);
            await db.SaveChangesAsync(ct);
        }

        return (fila.Id, "/api/v1/publico/archivos/" + fila.Id.ToString(CultureInfo.InvariantCulture));
    }

    private static string? Recortar(string? valor, int largo)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length == 0) { return null; }
        return limpio.Length <= largo ? limpio : limpio[..largo];
    }

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : 0;
}
