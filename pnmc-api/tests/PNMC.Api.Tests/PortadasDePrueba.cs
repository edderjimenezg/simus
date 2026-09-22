using System.Globalization;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace PNMC.Api.Tests;

/// <summary>
/// Fabrica portadas de publicación para poder <b>puntuar</b> la lectura de documentos.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE HACEN FALTA Y POR QUE SON SINTETICAS.</b> El acervo tiene 171 fichas catalogadas a
/// mano —verdad de referencia inmejorable— pero solo nueve traen enlace a un PDF, y esos enlaces
/// apuntan a servidores ajenos que hoy no responden. Sin documentos no hay nada que puntuar. Aquí se
/// construye una portada con los datos REALES de una ficha —su título, su subtítulo, su autoría, su
/// ISBN— maquetada como se maqueta una portada institucional, y después se comprueba cuánto de eso
/// recupera el lector.
/// </para>
/// <para>
/// <b>QUE MIDE ESTO Y QUE NO.</b> Mide si la tubería recupera valores conocidos de un documento con
/// la forma de los reales: título en grande arriba, subtítulo debajo, autoría en pequeño, y una
/// página de créditos con el ISBN y el año. NO mide qué pasa con una portada escaneada, con una
/// maquetación rara o con un PDF generado por un programa que escriba el texto en otro orden. Decirlo
/// importa: una puntuación alta aquí es condición necesaria y no suficiente.
/// </para>
/// </remarks>
public static class PortadasDePrueba
{
    /// <summary>Los datos con los que se fabrica una portada, que son los de una ficha real.</summary>
    public sealed record Ficha(
        string Titulo,
        string? Subtitulo,
        string? Autor,
        string? Isbn,
        int? Anio,
        string? Editorial = null);

    /// <summary>
    /// Construye un PDF de dos páginas: portada y créditos.
    /// </summary>
    /// <remarks>
    /// LOS TAMAÑOS IMITAN LOS MEDIDOS EN DOCUMENTOS REALES: 22 pt para el título, 14 para el
    /// subtítulo, 10 para la autoría. Es la proporción que se encontró en «Informe Final de Entrega»,
    /// y es la que hace que el lector pueda distinguirlos por tamaño de letra.
    /// </remarks>
    public static byte[] Construir(Ficha ficha)
    {
        var constructor = new PdfDocumentBuilder();
        var titular = constructor.AddStandard14Font(Standard14Font.HelveticaBold);
        var corriente = constructor.AddStandard14Font(Standard14Font.Helvetica);

        var portada = constructor.AddPage(PageSize.A4);
        var alto = portada.PageSize.Height;

        portada.AddText(Recortar(ficha.Titulo, 46), 22, new PdfPoint(60, alto - 160), titular);
        if (!string.IsNullOrWhiteSpace(ficha.Subtitulo))
        {
            portada.AddText(Recortar(ficha.Subtitulo!, 60), 14, new PdfPoint(60, alto - 200), titular);
        }
        if (!string.IsNullOrWhiteSpace(ficha.Autor))
        {
            portada.AddText(Recortar(ficha.Autor!, 70), 10, new PdfPoint(60, alto - 260), corriente);
        }
        if (!string.IsNullOrWhiteSpace(ficha.Editorial))
        {
            portada.AddText(Recortar(ficha.Editorial!, 70), 9, new PdfPoint(60, 90), corriente);
        }

        // La página de créditos: es donde viven de verdad el ISBN y el año en una publicación.
        var creditos = constructor.AddPage(PageSize.A4);
        var linea = alto - 120;
        if (!string.IsNullOrWhiteSpace(ficha.Autor))
        {
            creditos.AddText("Autor: " + Recortar(ficha.Autor!, 70), 10, new PdfPoint(60, linea), corriente);
            linea -= 20;
        }
        if (!string.IsNullOrWhiteSpace(ficha.Isbn))
        {
            creditos.AddText("ISBN: " + ficha.Isbn, 10, new PdfPoint(60, linea), corriente);
            linea -= 20;
        }
        if (ficha.Anio is { } anio)
        {
            creditos.AddText($"Bogota D.C., {anio}", 10, new PdfPoint(60, linea), corriente);
            linea -= 20;
        }
        creditos.AddText("Todos los derechos reservados", 9, new PdfPoint(60, linea), corriente);

        return constructor.Build();
    }

    /// <summary>
    /// Recorta al largo que cabe en la página.
    /// </summary>
    /// <remarks>
    /// LAS FUENTES ESTANDAR DE PDF NO LLEVAN ACENTOS EN SU CODIFICACION BASICA, y el constructor
    /// rechaza lo que no puede escribir. Como lo que se mide es la recuperación del texto y no la
    /// tipografía, se transcriben los acentos; el lector recibe exactamente lo que se escribió, así
    /// que la comparación sigue siendo justa.
    /// </remarks>
    /// <summary>
    /// Un afiche de evento: nombre grande, fechas y lugar.
    /// </summary>
    /// <remarks>
    /// UN AFICHE SE MAQUETA PARA LEERSE DE LEJOS, así que el nombre del evento va en el cuerpo más
    /// grande de la página —igual que el título en la portada de un libro— y debajo, en menor tamaño,
    /// cuándo y dónde. Es la forma que tiene que reconocer la lectura de la Agenda.
    /// </remarks>
    /// <summary>
    /// Convierte un PDF en otro que SOLO CONTIENE LA IMAGEN de su primera página.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>PARA QUE HACE FALTA.</b> Para poder probar el reconocimiento óptico con una prueba que no
    /// haga trampa. Todos los PDF que fabrica esta clase llevan capa de texto, así que la lectura
    /// normal los resuelve y el camino óptico nunca se ejercitaría: la prueba diría «pasa» sin haber
    /// ejecutado una sola línea de lo que dice comprobar.
    /// </para>
    /// <para>
    /// Rasterizando la portada y volviéndola a empaquetar se obtiene <b>exactamente</b> lo que llega
    /// de un escaneo: una página que es una fotografía de un texto. Si el reconocedor recupera el
    /// título de ahí, lo ha leído de verdad.
    /// </para>
    /// </remarks>
    public static byte[] SoloImagen(byte[] pdf)
    {
        var imagen = PDFtoImage.Conversion.ToImage(pdf, page: 0, options: new(Dpi: 150));
        using var datos = imagen.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var png = datos.ToArray();

        var constructor = new PdfDocumentBuilder();
        var pagina = constructor.AddPage(PageSize.A4);
        pagina.AddPng(png, new PdfRectangle(0, 0, pagina.PageSize.Width, pagina.PageSize.Height));
        return constructor.Build();
    }

    public static byte[] ConstruirAfiche(string evento, string cuando, string donde, string? organiza = null)
    {
        var constructor = new PdfDocumentBuilder();
        var titular = constructor.AddStandard14Font(Standard14Font.HelveticaBold);
        var corriente = constructor.AddStandard14Font(Standard14Font.Helvetica);

        var pagina = constructor.AddPage(PageSize.A4);
        var alto = pagina.PageSize.Height;

        pagina.AddText(Recortar(evento, 44), 26, new PdfPoint(50, alto - 180), titular);
        pagina.AddText(Recortar(cuando, 70), 14, new PdfPoint(50, alto - 240), corriente);
        pagina.AddText(Recortar(donde, 70), 12, new PdfPoint(50, alto - 280), corriente);
        if (!string.IsNullOrWhiteSpace(organiza))
        {
            pagina.AddText("Organiza: " + Recortar(organiza!, 60), 10, new PdfPoint(50, 100), corriente);
        }

        return constructor.Build();
    }

    /// <summary>
    /// Un artículo de revista: encabezado corriente, título, autoría, resumen y cuerpo en varias páginas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ESTA MAQUETA EXISTE PARA ROMPER UNA PRUEBA CIRCULAR.</b> El banco original solo fabricaba
    /// portadas de libro: título grande arriba, subtítulo debajo. Es decir, documentos hechos a la
    /// medida de la hipótesis que se quería comprobar —«el texto más grande es el título»—, así que
    /// puntuaba 8 de 8 sin medir nada. se detectó probando con un artículo real,
    /// donde el algoritmo tomó el encabezado de la revista por título y el nombre de la autora por
    /// subtítulo.
    /// </para>
    /// <para>
    /// LO QUE ESTA MAQUETA TIENE Y LA OTRA NO: un <b>encabezado que se repite en todas las páginas</b>,
    /// una <b>línea de autoría</b> con cuerpo intermedio —más grande que el texto, más pequeña que el
    /// título—, y varias páginas. Las tres cosas son las que hacían fallar al lector.
    /// </para>
    /// </remarks>
    public static byte[] ConstruirArticulo(
        string encabezado, string titulo, string autor, string bajada, string resumen)
    {
        var constructor = new PdfDocumentBuilder();
        var titular = constructor.AddStandard14Font(Standard14Font.HelveticaBold);
        var corriente = constructor.AddStandard14Font(Standard14Font.Helvetica);

        for (var numero = 1; numero <= 3; numero++)
        {
            var pagina = constructor.AddPage(PageSize.A4);
            var alto = pagina.PageSize.Height;

            // EL ENCABEZADO, EN TODAS LAS PAGINAS Y ARRIBA DEL TODO: es lo que un lector ingenuo
            // confunde con el título, y lo que la repetición delata.
            pagina.AddText(Recortar(encabezado, 60), 10, new PdfPoint(60, alto - 60), corriente);

            if (numero == 1)
            {
                pagina.AddText(Recortar(titulo, 46), 24, new PdfPoint(60, alto - 180), titular);
                pagina.AddText(Recortar(bajada, 76), 12, new PdfPoint(60, alto - 235), corriente);
                // LA AUTORIA A CUERPO INTERMEDIO: más grande que el texto, más pequeña que el título.
                // Es exactamente la trampa en la que cayó el lector.
                pagina.AddText(Recortar(autor, 50), 15, new PdfPoint(60, alto - 295), corriente);
                pagina.AddText("Resumen: " + Recortar(resumen, 78), 11, new PdfPoint(60, alto - 350), corriente);
            }

            pagina.AddText(
                Recortar("Cuerpo del articulo, pagina " + numero.ToString(CultureInfo.InvariantCulture)
                    + ", con texto corriente que no debe confundirse con ningun titulo.", 86),
                12, new PdfPoint(60, alto - 420), corriente);
        }

        return constructor.Build();
    }

    private static string Recortar(string texto, int largo)
    {
        var plano = texto
            .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u')
            .Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U')
            .Replace('ñ', 'n').Replace('Ñ', 'N').Replace('ü', 'u')
            .Replace('“', '"').Replace('”', '"').Replace('’', '\'').Replace('—', '-').Replace('–', '-')
            .Replace('¡', ' ').Replace('¿', ' ');

        plano = new string(plano.Where(c => c < 127).ToArray()).Trim();
        return plano.Length <= largo ? plano : plano[..largo].Trim();
    }
}
