using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Lo que un documento dice de sí mismo.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUE ES ESTO Y QUE NO ES.</b> No es un lector de PDF ni un modelo de lenguaje: es el primer
/// tramo, determinista, de la carga de una publicación a partir de su documento. Un PDF lleva dentro
/// un diccionario de información —título, autor, asunto, palabras clave— que quien lo generó
/// escribió, y lo lleva en claro. Leerlo no requiere inteligencia de ninguna clase y ahorra
/// transcribir a mano lo que el fichero ya dice.
/// </para>
/// <para>
/// <b>EL PUNTO DE CONEXION DEL MODELO ESTA FUERA DE AQUI, Y ESO ES DELIBERADO.</b> El dueño del
/// proyecto planteó el asistente como «una especie de inteligencia artificial entrenada localmente»
/// para sacar del documento lo que los metadatos NO dicen: el resumen, la tipología, los créditos con
/// su papel, los identificadores. Eso entrará por un único sitio —<c>AmpliarConModelo</c>, que hoy no
/// existe—; mientras tanto, esta clase devuelve lo que de verdad hay y deja vacío lo que no, en vez
/// de adivinarlo. Una propuesta inventada es peor que una casilla vacía: la segunda se rellena, la
/// primera se acepta sin mirar.
/// </para>
/// <para>
/// <b>NO SE DESCOMPRIME NADA.</b> Un PDF puede llevar sus objetos comprimidos, y entonces el
/// diccionario no se ve en claro. Descomprimir exigiría un lector completo en el servidor —superficie
/// de ataque nueva— para ganar unos campos en unos cuantos ficheros. Cuando no se encuentra, se dice
/// que no se encontró.
/// </para>
/// </remarks>
public static class ExtractorDeDocumentos
{
    /// <summary>Lo que se pudo leer del documento. Vacío donde no había nada.</summary>
    /// <param name="CamposEncontrados">Qué se leyó de verdad, para poder decirlo en pantalla.</param>
    public sealed record PropuestaDeDocumento(
        string? Titulo,
        string? Autor,
        string? Asunto,
        IReadOnlyList<string> PalabrasClave,
        int? Paginas,
        IReadOnlyList<string> CamposEncontrados);

    private const int LargoMaximoDeCampo = 500;

    private static readonly TimeSpan TopeDeBusqueda = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Lee el diccionario de información y, si no está en claro, el XMP.
    /// </summary>
    /// <remarks>
    /// SE MIRAN LAS DOS FUENTES PORQUE LOS GENERADORES USAN UNA U OTRA. Word y Acrobat escriben el
    /// diccionario clásico <c>/Title (…)</c>; los flujos de publicación modernos escriben XMP, un
    /// bloque XML incrustado con <c>dc:title</c>. Un mismo acervo trae de los dos.
    /// </remarks>
    public static PropuestaDeDocumento Leer(ReadOnlySpan<byte> datos)
    {
        // Se lee como Latin-1 a propósito: el diccionario clásico es ASCII con escapes, y esta
        // codificación no falla ante bytes binarios, que los hay por todas partes en un PDF.
        var texto = Encoding.Latin1.GetString(datos);

        var titulo = Campo(texto, "Title") ?? CampoXmp(texto, "dc:title");
        var autor = Campo(texto, "Author") ?? CampoXmp(texto, "dc:creator");
        var asunto = Campo(texto, "Subject") ?? CampoXmp(texto, "dc:description");
        var claves = Campo(texto, "Keywords") ?? CampoXmp(texto, "pdf:Keywords");
        var paginas = ContarPaginas(texto);

        var encontrados = new List<string>();
        if (titulo is not null) { encontrados.Add("título"); }
        if (autor is not null) { encontrados.Add("autoría"); }
        if (asunto is not null) { encontrados.Add("asunto"); }
        if (claves is not null) { encontrados.Add("palabras clave"); }
        if (paginas is not null) { encontrados.Add("número de páginas"); }

        return new PropuestaDeDocumento(titulo, autor, asunto, SepararClaves(claves), paginas, encontrados);
    }

    /// <summary>Un campo del diccionario de información: <c>/Title (Guía de iniciación al fagot)</c>.</summary>
    private static string? Campo(string texto, string nombre)
    {
        // Los paréntesis pueden anidarse y escaparse: `\(` no cierra. Por eso el corte es sobre
        // paréntesis no escapados y no sobre el primero que aparezca.
        var patron = new Regex("/" + nombre + @"\s*\((?<valor>(?:\\.|[^\\()])*)\)", RegexOptions.None, TopeDeBusqueda);
        var coincidencia = patron.Match(texto);
        if (!coincidencia.Success) { return null; }

        var valor = coincidencia.Groups["valor"].Value
            .Replace("\\(", "(", StringComparison.Ordinal)
            .Replace("\\)", ")", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal)
            .Trim();

        return Limpio(valor);
    }

    /// <summary>El mismo dato, cuando el generador lo escribió como XMP.</summary>
    private static string? CampoXmp(string texto, string etiqueta)
    {
        var patron = new Regex(
            "<" + Regex.Escape(etiqueta) + "[^>]*>(?<valor>.*?)</" + Regex.Escape(etiqueta) + ">",
            RegexOptions.Singleline,
            TopeDeBusqueda);
        var coincidencia = patron.Match(texto);
        if (!coincidencia.Success) { return null; }

        // El XMP envuelve el valor en `<rdf:Alt><rdf:li …>`: se quita todo lo que sea etiqueta.
        var valor = Regex.Replace(coincidencia.Groups["valor"].Value, "<[^>]+>", " ", RegexOptions.None, TopeDeBusqueda);
        return Limpio(valor);
    }

    /// <summary>
    /// Cuántas páginas declara.
    /// </summary>
    /// <remarks>
    /// SE LEE DEL NODO RAIZ Y NO SE CUENTAN LOS OBJETOS. <c>/Count</c> en el árbol de páginas es el
    /// número que el propio documento declara; contar apariciones de <c>/Type /Page</c> se equivoca
    /// con los documentos que reutilizan objetos.
    /// </remarks>
    private static int? ContarPaginas(string texto)
    {
        var patron = new Regex(@"/Type\s*/Pages\b[^>]*?/Count\s+(?<n>\d+)", RegexOptions.Singleline, TopeDeBusqueda);
        var coincidencia = patron.Match(texto);
        if (!coincidencia.Success)
        {
            patron = new Regex(@"/Count\s+(?<n>\d+)[^>]*?/Type\s*/Pages\b", RegexOptions.Singleline, TopeDeBusqueda);
            coincidencia = patron.Match(texto);
        }

        return coincidencia.Success
            && int.TryParse(coincidencia.Groups["n"].Value, CultureInfo.InvariantCulture, out var n)
            && n is > 0 and < 100_000
            ? n
            : null;
    }

    private static List<string> SepararClaves(string? valor) =>
        (valor ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

    /// <summary>
    /// Descarta lo que no sirve.
    /// </summary>
    /// <remarks>
    /// MUCHOS PDF TRAEN BASURA EN ESOS CAMPOS: el nombre del fichero de Word, «untitled», o una ruta
    /// entera del disco de quien lo exportó. Proponer eso como título de una ficha catalográfica es
    /// peor que no proponer nada, porque una propuesta se acepta sin mirar y un hueco se rellena.
    /// </remarks>
    /// <summary>
    /// Una cadena de PDF puede venir en UTF-16, y hay que darse cuenta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL FORMATO ADMITE DOS CODIFICACIONES para el texto de su diccionario: PDFDocEncoding, que es
    /// casi Latin-1, y UTF-16BE, que se declara con la marca de orden de bytes «FE FF» al principio.
    /// Leerlo todo como Latin-1 —que es lo que hay que hacer para recorrer el fichero sin que
    /// reviente— convierte un título en UTF-16 en «þÿ I n f o r m e», con un NUL entre cada letra.
    /// </para>
    /// <para>
    /// SE VIO EN UN DOCUMENTO REAL DEL PROYECTO: el título del «Informe Final de Entrega» llegaba así
    /// y se habría propuesto tal cual a quien cataloga. El arreglo es mirar la marca y volver a armar
    /// los caracteres de dos en dos.
    /// </para>
    /// </remarks>
    private static string ReinterpretarSiEsUtf16(string valor)
    {
        if (valor.Length < 4 || valor[0] != '\u00FE' || valor[1] != '\u00FF') { return valor; }

        var cuerpo = valor[2..];
        var construido = new StringBuilder(cuerpo.Length / 2);
        for (var i = 0; i + 1 < cuerpo.Length; i += 2)
        {
            // Cada carácter real son dos bytes, el alto primero. Se leen como bytes porque venían de
            // una lectura Latin-1, donde cada carácter ES un byte.
            construido.Append((char)((cuerpo[i] << 8) | cuerpo[i + 1]));
        }
        return construido.ToString();
    }

    private static string? Limpio(string? valor)
    {
        var limpio = Regex.Replace(ReinterpretarSiEsUtf16((valor ?? string.Empty)).Trim(), @"\s+", " ", RegexOptions.None, TopeDeBusqueda);

        if (limpio.Length < 2 || limpio.Length > LargoMaximoDeCampo) { return null; }
        if (limpio.Contains(".doc", StringComparison.OrdinalIgnoreCase)
            || limpio.Contains(".pdf", StringComparison.OrdinalIgnoreCase)
            || limpio.Contains(":\\", StringComparison.Ordinal)
            || limpio.StartsWith("untitled", StringComparison.OrdinalIgnoreCase)
            || limpio.StartsWith("microsoft word", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return limpio;
    }
}
