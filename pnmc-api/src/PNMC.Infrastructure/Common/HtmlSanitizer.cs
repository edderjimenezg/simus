using GanssHtmlSanitizer = Ganss.Xss.HtmlSanitizer;

namespace PNMC.Infrastructure.Common;

/// <summary>
/// Saneamiento de HTML enriquecido (cuerpo de noticias) con enfoque de LISTA BLANCA.
///
/// PNMC-004: la implementación anterior era una lista negra a base de expresiones
/// regulares (quitaba &lt;script&gt;…&lt;/script&gt;, atributos " on*=" y "javascript:"
/// literal). Ese patrón se sortea de forma trivial porque una expresión regular no
/// analiza HTML como lo hace un navegador: etiqueta sin cierre (&lt;script&gt;alert(1)),
/// separador que no es espacio (&lt;img/onerror=…&gt;), etiquetas anidadas que se
/// reconstruyen al borrar la interna (&lt;scr&lt;script&gt;ipt&gt;), o entidades HTML en el
/// atributo (href="&amp;#106;avascript:…") que el navegador decodifica después.
///
/// Aquí se delega en la biblioteca HtmlSanitizer (Ganss.Xss), que analiza el marcado
/// con AngleSharp —el mismo algoritmo de análisis HTML5 del navegador— y reconstruye
/// la salida dejando pasar SOLO lo declarado en las listas de abajo. Todo lo demás
/// (etiquetas, atributos, esquemas de URL) se elimina.
///
/// Decisiones conservadoras tomadas sin poder consultar al dirección de producto:
///  - No se permite el atributo <c>style</c> ni ninguna propiedad CSS.
///  - No se permiten <c>data:</c> URIs (ni siquiera de imagen).
///  - No se permite <c>iframe</c> (los vídeos incrustados viajan por el campo
///    PrimaryEmbedUrl, no por el cuerpo).
///  - No se permiten <c>id</c> ni <c>name</c> (evita DOM clobbering).
/// </summary>
public static class HtmlSanitizer
{
    /// <summary>Etiquetas permitidas: texto enriquecido editorial, sin marcado activo.</summary>
    private static readonly string[] AllowedTags =
    [
        "p", "br", "hr", "span", "div",
        "strong", "b", "em", "i", "u", "s", "strike", "small", "mark", "sub", "sup",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "blockquote", "q", "cite", "abbr", "time", "address",
        "code", "pre", "kbd", "samp", "var",
        "ul", "ol", "li", "dl", "dt", "dd",
        "a", "img", "figure", "figcaption",
        "table", "caption", "colgroup", "col", "thead", "tbody", "tfoot", "tr", "th", "td",
        "section", "article", "header", "footer", "aside"
    ];

    /// <summary>
    /// Atributos permitidos. La lista es global (se aplica a cualquier etiqueta permitida),
    /// así que se deja fuera todo lo que pueda ejecutar código o reescribir el DOM:
    /// cualquier <c>on*</c>, <c>style</c>, <c>srcdoc</c>, <c>srcset</c>, <c>formaction</c>,
    /// <c>id</c>, <c>name</c> y los <c>data-*</c>.
    /// </summary>
    private static readonly string[] AllowedAttributes =
    [
        "class", "title", "dir", "lang",
        "href", "target", "rel",
        "src", "alt", "width", "height", "loading",
        "colspan", "rowspan", "scope", "span", "start", "reversed", "type", "value",
        "datetime", "cite"
    ];

    /// <summary>Esquemas admitidos en href/src. Sin <c>data:</c>, sin <c>javascript:</c>.</summary>
    private static readonly string[] AllowedSchemes = ["http", "https", "mailto", "tel"];

    /// <summary>Atributos tratados como URL (se validan contra <see cref="AllowedSchemes"/>).</summary>
    private static readonly string[] UriAttributes = ["href", "src", "cite"];

    // El saneador de Ganss.Xss no está documentado como seguro entre hilos, así que se
    // guarda una instancia por hilo en lugar de compartir una sola o construir una por
    // llamada (el cuerpo de cada noticia se sanea también en lectura).
    [ThreadStatic]
    private static GanssHtmlSanitizer? _sanitizer;

    public static string SanitizeRichHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitizer = _sanitizer ??= CreateSanitizer();
        return sanitizer.Sanitize(value.Trim()).Trim();
    }

    private static GanssHtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new GanssHtmlSanitizer
        {
            // Una etiqueta no permitida se elimina con su contenido: si alguien manda
            // <script>alert(1)</script> no queremos dejar "alert(1)" como texto suelto.
            KeepChildNodes = false,
            AllowDataAttributes = false,
            AllowCssCustomProperties = false
        };

        Replace(sanitizer.AllowedTags, AllowedTags);
        Replace(sanitizer.AllowedAttributes, AllowedAttributes);
        Replace(sanitizer.AllowedSchemes, AllowedSchemes);
        Replace(sanitizer.UriAttributes, UriAttributes);

        // Sin atributo style no hay CSS que sanear; se vacían igualmente las listas de
        // CSS y de reglas @ para que ninguna configuración futura las reabra por descuido.
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedAtRules.Clear();
        sanitizer.AllowedClasses.Clear(); // vacío = se permite cualquier class (no se filtra por nombre)
        sanitizer.UriListAttributes.Clear();

        return sanitizer;
    }

    private static void Replace(ISet<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}
