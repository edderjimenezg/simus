using PDFtoImage;
using SkiaSharp;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// La portada de una publicación, sacada de su propio documento.
/// </summary>
/// <remarks>
/// <para>
/// <b>EN UN CATALOGO LA PORTADA ES EL DATO</b>, y hasta ahora había que conseguirla aparte: abrir el
/// PDF, capturar la primera página, recortarla y subirla. Renderizarla es determinista y cuesta
/// milisegundos. Comprobado sobre documentos reales del proyecto: la página 1 sale fiel, a
/// 935 × 1210 px con 110 puntos por pulgada.
/// </para>
/// <para>
/// <b>LOS 110 PPP NO SON UN NUMERO AL AZAR.</b> La miniatura del catálogo se pinta a 320 px de ancho
/// y la ficha la enseña hasta 640; a 110 ppp una página A4 da unos 935 px de ancho, que cubre las dos
/// con margen para pantallas de mucha densidad y deja un PNG que pesa lo que pesa una imagen, no lo
/// que pesa un escaneo. Subir a 300 multiplicaría por siete el peso para enseñar lo mismo.
/// </para>
/// <para>
/// <b>SE DEVUELVE PNG Y NO JPEG</b>: una portada es texto y trama sobre fondo plano, justo donde el
/// JPEG deja halos alrededor de las letras. Y es el formato que el banco de archivos ya reconoce por
/// firma, así que la portada entra por la misma puerta que cualquier otra imagen, sin excepciones.
/// </para>
/// </remarks>
public static class PortadaDelDocumento
{
    /// <summary>Densidad de render. Razonada arriba: cubre miniatura y ficha sin inflar el fichero.</summary>
    private const int PuntosPorPulgada = 110;

    /// <summary>
    /// Convierte una página del documento en una imagen.
    /// </summary>
    /// <param name="datos">El PDF completo.</param>
    /// <param name="pagina">
    /// Qué página renderizar, contando desde 1. <b>No siempre es la primera</b>: hay publicaciones
    /// cuya página 1 es una cubierta institucional genérica y la portada de verdad está en la 2 o la
    /// 3. Por eso se elige y no se impone.
    /// </param>
    /// <returns>El PNG, o <c>null</c> si la página no existe o el documento no se puede renderizar.</returns>
    public static byte[]? Renderizar(byte[] datos, int pagina = 1)
    {
        if (pagina < 1) { return null; }

        try
        {
            using var imagen = Conversion.ToImage(datos, page: pagina - 1, options: new RenderOptions(Dpi: PuntosPorPulgada));
            using var memoria = new MemoryStream();
            imagen.Encode(memoria, SKEncodedImageFormat.Png, 90);
            return memoria.ToArray();
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException or IOException)
        {
            // UN PDF QUE NO SE DEJA RENDERIZAR NO ES UN FALLO DEL SISTEMA: es un documento cifrado,
            // truncado o con una página que no existe. Quien cataloga sigue pudiendo subir la portada
            // a mano, así que se devuelve «no pude» y no una excepción que tumbe la carga entera.
            return null;
        }
    }
}
