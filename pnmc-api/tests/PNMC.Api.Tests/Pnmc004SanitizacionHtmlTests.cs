using PNMC.Infrastructure.Common;
using Xunit;

namespace PNMC.Api.Tests;

/// <summary>
/// PNMC-004 — XSS almacenado: el saneador del cuerpo de las noticias.
/// </summary>
/// <remarks>
/// <para>
/// El saneador anterior era una lista negra hecha de expresiones regulares: borraba
/// <c>&lt;script&gt;…&lt;/script&gt;</c>, los atributos que empezaran por <c>on</c>
/// precedidos de espacio, y el literal <c>javascript:</c> dentro de href/src.
/// Una expresion regular no analiza HTML como un navegador, y por ahi se cuela todo:
/// </para>
/// <list type="bullet">
///   <item><description>etiqueta sin cierre: <c>&lt;script&gt;alert(1)</c> nunca casaba con el par apertura/cierre;</description></item>
///   <item><description>separador que no es espacio: <c>&lt;img/onerror=…&gt;</c> no lleva <c>\s+</c> delante del <c>on</c>;</description></item>
///   <item><description>anidamiento: al borrar el <c>&lt;script&gt;</c> interno de <c>&lt;scr&lt;script&gt;ipt&gt;</c> el navegador vuelve a ver una etiqueta valida;</description></item>
///   <item><description>entidades HTML: <c>href="&amp;#106;avascript:…"</c> no contiene el literal, pero el navegador lo decodifica antes de navegar;</description></item>
///   <item><description>y todo lo que la lista negra ni nombraba: <c>style</c>, <c>srcset</c>, <c>&lt;form&gt;</c>, <c>&lt;math&gt;</c>, <c>data:text/html</c>…</description></item>
/// </list>
/// <para>
/// El reemplazo es lista blanca sobre un analizador HTML5 real (biblioteca
/// HtmlSanitizer / Ganss.Xss, que analiza con AngleSharp): se reconstruye la salida
/// dejando pasar solo etiquetas, atributos y esquemas declarados.
/// </para>
/// <para>
/// Las pruebas afirman dos mitades. La primera, que ningun vector sobrevive. La
/// segunda —igual de importante— que el texto enriquecido legitimo sigue saliendo:
/// un saneador que devolviera cadena vacia pasaria la primera mitad en verde.
/// </para>
/// </remarks>
public sealed class Pnmc004SanitizacionHtmlTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public Pnmc004SanitizacionHtmlTests(TestWebApplicationFactory factory) => _factory = factory;

    /// <summary>Vectores reales de evasion: (marcado de entrada, fragmento que NO puede sobrevivir).</summary>
    public static TheoryData<string, string> VectoresDeEvasion() => new()
    {
        // Atributos de evento.
        { "<img src=x onerror=alert(1)>", "onerror" },
        { "<img/onerror=\"alert(1)\" src=x>", "onerror" },          // sin espacio antes del atributo
        { "<img src=x on error=alert(1)>", "error=alert" },          // atributo partido
        { "<p title=\"a\" onmouseover =\t'alert(1)'>x</p>", "onmouseover" },
        { "<body onload=alert(1)>hola</body>", "onload" },
        { "<noscript><p title=\"</noscript><img src=x onerror=alert(1)>\">", "onerror" },

        // <svg onload=...>, con y sin espacio.
        { "<svg onload=alert(1)></svg>", "onload" },
        { "<svg/onload=alert(1)></svg>", "onload" },
        { "<svg><script>alert(1)</script></svg>", "<script" },

        // Etiquetas de script: sin cierre, anidadas, con mayusculas mezcladas.
        { "<script>alert(1)", "<script" },
        { "<script>alert(1)", "alert(1)" },
        { "<scr<script>ipt>alert(1)</scr</script>ipt>", "<script" },
        { "<ScRiPt SrC=//malo.example/x.js></ScRiPt>", "script" },
        { "<ScRiPt SrC=//malo.example/x.js></ScRiPt>", "malo.example" },
        { "<template><script>alert(1)</script></template>", "<script" },
        { "<!--[if IE]><script>alert(1)</script><![endif]-->", "<script" },

        // javascript: en href, tal cual, con mayusculas mezcladas y codificado en entidades.
        { "<a href=\"javascript:alert(1)\">x</a>", "javascript:" },
        { "<a href=\"jAvAsCrIpT:alert(1)\">x</a>", "javascript" },
        { "<a href=\"&#106;avascript:alert(1)\">x</a>", "avascript" },
        { "<a href=\"&#x6a;avascript:alert(1)\">x</a>", "avascript" },
        { "<a href=\"java&#9;script:alert(1)\">x</a>", "script:" },
        { "<a href=\"javascript&colon;alert(1)\">x</a>", "javascript" },
        { "<math><maction xlink:href=\"javascript:alert(1)\">x</maction></math>", "javascript:" },
        { "<img srcset=\"javascript:alert(1)\" src=\"/ok.png\">", "srcset" },

        // data: (un data:text/html es un documento con permisos del sitio si se navega a el).
        { "<a href=\"data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==\">x</a>", "data:" },
        { "<img src=\"data:image/svg+xml;base64,PHN2Zy8+\">", "data:" },

        // Marcado activo que la lista negra ni nombraba.
        { "<div style=\"background:url(javascript:alert(1))\">x</div>", "style" },
        { "<form action=\"/x\"><button formaction=\"javascript:alert(1)\">go</button></form>", "formaction" },
        { "<object data=\"x.swf\"></object><embed src=\"x.swf\">", "<embed" },
        { "<iframe srcdoc=\"&lt;script&gt;alert(1)&lt;/script&gt;\"></iframe>", "srcdoc" },
        { "<meta http-equiv=\"refresh\" content=\"0;url=javascript:alert(1)\">", "<meta" },
        { "<base href=\"//malo.example/\">", "<base" },
        { "<p data-x=\"<script>alert(1)</script>\">x</p>", "data-x" },
    };

    [Theory]
    [MemberData(nameof(VectoresDeEvasion))]
    public void El_Saneador_No_Deja_Pasar_Ningun_Vector(string entrada, string prohibido)
    {
        var salida = HtmlSanitizer.SanitizeRichHtml(entrada);

        Assert.DoesNotContain(prohibido, salida, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>La otra mitad: el texto editorial legitimo tiene que sobrevivir intacto.</summary>
    public static TheoryData<string, string> ContenidoLegitimo() => new()
    {
        { "<p>Hola <strong>mundo</strong> y <em>algo mas</em></p>", "<strong>mundo</strong>" },
        { "<a href=\"https://pnmc.gov.co/a\" title=\"t\">link</a>", "href=\"https://pnmc.gov.co/a\"" },
        { "<a href=\"/noticias/1\">link</a>", "href=\"/noticias/1\"" },
        { "<a href=\"mailto:prensa@pnmc.gov.co\">correo</a>", "mailto:prensa@pnmc.gov.co" },
        { "<img src=\"https://cdn.example/a.png\" alt=\"foto\">", "alt=\"foto\"" },
        { "<ul><li>a</li></ul>", "<li>a</li>" },
        { "<table><tr><td colspan=\"2\">c</td></tr></table>", "colspan=\"2\"" },
        { "<h2 class=\"t\">Titulo</h2>", "<h2 class=\"t\">Titulo</h2>" },
        { "<blockquote>cita</blockquote>", "<blockquote>cita</blockquote>" },
    };

    [Theory]
    [MemberData(nameof(ContenidoLegitimo))]
    public void El_Saneador_Conserva_El_Texto_Enriquecido(string entrada, string esperado)
    {
        var salida = HtmlSanitizer.SanitizeRichHtml(entrada);

        Assert.Contains(esperado, salida, StringComparison.Ordinal);
    }

    [Fact]
    public void El_Texto_Plano_Se_Escapa_En_Lugar_De_Perderse()
    {
        var salida = HtmlSanitizer.SanitizeRichHtml("5 < 7 & 8 > 3");

        Assert.Contains("&lt;", salida, StringComparison.Ordinal);
        Assert.Contains("&amp;", salida, StringComparison.Ordinal);
        Assert.Contains("5", salida, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_Contenido_Devuelve_Cadena_Vacia(string? entrada)
    {
        Assert.Equal(string.Empty, HtmlSanitizer.SanitizeRichHtml(entrada));
    }

}
