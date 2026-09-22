using System.Text.RegularExpressions;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// ¿Esto que salió del PDF es texto de verdad, o son bytes disfrazados de texto?
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE HACE FALTA.</b> Probando con documentos reales del ordenador de quien usa la
/// herramienta, la pantalla llegó a proponer como título de una publicación esto:
/// </para>
/// <code>
/// \376\377\0004\0003\0006\000-\0004\0006\0009\000-\0001\000-\000P\000B\000.\000pdf
/// E±)\031Ù@Îø³üÙfïÁîõ+Ë¥\025È%¿\033\023\015;ýYk\024µpóm
/// </code>
/// <para>
/// Son dos averías distintas del PDF —un título de metadatos en UTF-16 mal recuperado y una fuente
/// sin tabla <c>ToUnicode</c>, que devuelve códigos de glifo en vez de letras—, y la herramienta no
/// tiene que saber cuál es cuál. Lo que tiene que saber es <b>que eso no se le propone a nadie</b>.
/// Un campo vacío se rellena; un campo con basura hay que darse cuenta de que es basura y borrarlo,
/// que es más trabajo que no haber propuesto nada.
/// </para>
/// <para>
/// <b>TRES COMPROBACIONES, NINGUNA ADIVINA EL IDIOMA.</b> No se valida español: un título puede
/// estar en wayuunaiki, en inglés o en latín. Se valida que sea <i>escritura</i>.
/// </para>
/// </remarks>
public static class TextoLegible
{
    private static readonly TimeSpan TopeDeBusqueda = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Cuántas letras seguidas hacen falta para que algo parezca una palabra.
    /// </summary>
    /// <remarks>
    /// TRES. Medido contra el caso real «0þí´Ñ Q», donde la racha más larga es de dos: no hay ninguna
    /// palabra ahí dentro. Y contra los títulos buenos del acervo, donde la primera palabra siempre
    /// pasa de tres letras. Bajarlo a dos dejaría entrar el caso malo; subirlo a cuatro dejaría fuera
    /// un título que empiece por «Voz» o «Son».
    /// </remarks>
    private const int LetrasSeguidasParaSerPalabra = 3;

    /// <summary>
    /// Qué proporción del texto debe ser escritura corriente.
    /// </summary>
    /// <remarks>
    /// Se cuenta como corriente una letra, una cifra, un espacio o un signo de puntuación de los que
    /// se usan al escribir. El resto —flechas, bloques, símbolos de control tipográfico— existe en
    /// documentos legítimos, pero no llena tres cuartas partes de un título.
    /// </remarks>
    private const double ProporcionMinimaDeEscritura = 0.75;

    private const string PuntuacionCorriente = " .,;:¡!¿?()[]{}«»\"'“”‘’-–—_/\\&%#*+=@…·|";

    /// <summary>
    /// ¿Se le puede enseñar esto a una persona como valor propuesto?
    /// </summary>
    public static bool EsPlausible(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim();
        if (texto.Length < 3) { return false; }

        // 1. NINGUN CARACTER DE CONTROL. Un título no lleva NUL, ni escape, ni retorno de carro
        //    suelto. Es lo que delata un UTF-16 leído como si fuera de un byte.
        foreach (var caracter in texto)
        {
            if (char.IsControl(caracter) && caracter is not ('\n' or '\r' or '\t')) { return false; }
        }

        // 2. AL MENOS UNA PALABRA. Una racha de letras seguidas: es lo que no tiene una cadena de
        //    códigos de glifo, por mucho que algunos de ellos caigan en letras acentuadas.
        var racha = 0;
        var mayorRacha = 0;
        foreach (var caracter in texto)
        {
            racha = char.IsLetter(caracter) ? racha + 1 : 0;
            if (racha > mayorRacha) { mayorRacha = racha; }
        }
        if (mayorRacha < LetrasSeguidasParaSerPalabra) { return false; }

        // 3. MAYORITARIAMENTE ESCRITURA, no símbolos.
        var corrientes = texto.Count(c => char.IsLetterOrDigit(c) || PuntuacionCorriente.Contains(c, StringComparison.Ordinal));
        return (double)corrientes / texto.Length >= ProporcionMinimaDeEscritura;
    }

    /// <summary>
    /// ¿Es esto el nombre de un archivo en vez del título de la obra?
    /// </summary>
    /// <remarks>
    /// PASA CONTINUAMENTE con los PDF de revistas y repositorios: el título de metadatos es
    /// «436-469-1-PB.pdf», que es como lo bautizó el gestor editorial, o «documento final v3.docx».
    /// Se propone como título de la publicación y no lo es de nada.
    /// </remarks>
    public static bool PareceNombreDeArchivo(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim();
        return Regex.IsMatch(
            texto,
            @"\.(pdf|doc|docx|indd|qxd|odt|rtf|txt|ai|psd|cdr)$",
            RegexOptions.IgnoreCase, TopeDeBusqueda);
    }
}
