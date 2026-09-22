using System.Globalization;
using System.Text;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Reconoce, dentro del texto de un documento, lo que el catálogo <b>ya sabe</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>ESTA ES LA PIEZA QUE NINGUN MODELO GENERAL PODRIA TENER, y por eso es la más valiosa.</b> Un
/// modelo entrenado fuera puede adivinar que «Ministerio de Cultura» es una entidad; lo que no puede
/// saber es que en ESTE catálogo ese agente ya existe con ese nombre exacto, que tiene 127 obras y
/// que su identificador es tal. El acervo trae <b>410 agentes, 22 rutas, 54 tipos de publicación y 7
/// prácticas musicales</b>: buscar esos términos dentro del texto extraído es determinista, es
/// instantáneo y acierta donde un modelo conjeturaría.
/// </para>
/// <para>
/// <b>SE COTEJA SIN TILDES Y SIN MAYUSCULAS</b>, porque una portada escribe «MINISTERIO DE CULTURA» y
/// el fichero de autoridades «Ministerio de Cultura». Comparar tal cual perdería la mitad de las
/// coincidencias por un detalle tipográfico.
/// </para>
/// <para>
/// <b>NO SE ACEPTAN TERMINOS CORTOS.</b> Un vocabulario tiene entradas como «Coro» o «Banda», y
/// buscarlas sueltas dentro de un texto largo las encuentra siempre: «banda» aparece en «banda
/// sonora», en «banda ancha» y en cualquier frase. Por debajo de cierto largo, una coincidencia no
/// dice nada, así que no se propone.
/// </para>
/// <para>
/// <b>Y NADA DE ESTO DECIDE: PROPONE.</b> Cada coincidencia viaja con el término encontrado para que
/// quien cataloga vea qué se reconoció y por qué, y pueda quitarlo de un clic.
/// </para>
/// </remarks>
public static class CotejoConElAcervo
{
    /// <summary>Un término del catálogo reconocido dentro del documento.</summary>
    /// <param name="Valor">El término tal como lo tiene el catálogo, no como aparecía en el texto.</param>
    public sealed record Reconocido(string Valor, int Usos);

    /// <summary>
    /// El largo mínimo por omisión para que una coincidencia signifique algo.
    /// </summary>
    /// <remarks>
    /// Medido sobre el vocabulario editorial: con cinco caracteres entran «Banda» y «Coro y música
    /// vocal», pero también «Libro», que aparece en cualquier página de créditos. Con ocho se quedan
    /// fuera los términos que son palabras corrientes del español y dentro los que de verdad nombran
    /// algo: «Ministerio de Cultura», «Pedagogía Instrumental», «Cuaderno de ejercicios».
    ///
    /// <b>PERO NO VALE PARA TODOS LOS VOCABULARIOS, y eso lo destapó una prueba.</b> Con un afiche de
    /// «Festival de Bandas de Paipa», el municipio NO se reconocía: «Paipa» tiene cinco letras. Y como
    /// esa hay decenas —Cali, Tunja, Neiva, Pasto, Mocoa—. Un topónimo corto sí es distintivo cuando
    /// aparece, al revés que «Libro». Por eso el umbral es un parámetro y cada vocabulario pone el
    /// suyo, en vez de un número que sirva a medias para todos.
    /// </remarks>
    private const int LargoMinimoPorOmision = 8;

    /// <summary>
    /// El carácter con el que se aparta la eñe mientras se quitan las tildes.
    /// </summary>
    /// <remarks>
    /// ES UN PUNTO DE USO PRIVADO DE UNICODE, elegido justamente porque no puede aparecer en un texto
    /// real: si se usara una letra corriente como marca, un documento que la contuviera saldría con
    /// eñes donde no las hay. Se escribe como escape y no como carácter literal para que no quede
    /// invisible en el código fuente.
    /// </remarks>
    private const char Centinela = '\uE000';

    /// <summary>
    /// Qué términos de una lista aparecen en el texto.
    /// </summary>
    /// <remarks>
    /// SE DEVUELVEN ORDENADOS DEL MAS LARGO AL MAS CORTO. Si el texto dice «Pedagogía Instrumental» y
    /// el vocabulario tiene también «Pedagogía», las dos coinciden; la larga es la que informa, y la
    /// corta solo ruido que empuja a la buena fuera de la lista.
    /// </remarks>
    /// <param name="largoMinimo">
    /// Por debajo de esto no se propone. Ocho para vocabularios de palabras corrientes; menos para
    /// nombres propios, que son distintivos aunque sean cortos.
    /// </param>
    /// <param name="exigirPalabraCompleta">
    /// <b>Imprescindible cuando se bajan los términos cortos.</b> Sin esto, «Cali» se reconocería
    /// dentro de «California» y «calidad», y «Paz» dentro de «capaz». Con el umbral en ocho el riesgo
    /// es despreciable; con el umbral en cuatro, no.
    /// </param>
    public static List<Reconocido> Reconocer(
        string texto,
        IEnumerable<(string Valor, int Usos)> vocabulario,
        int largoMinimo = LargoMinimoPorOmision,
        bool exigirPalabraCompleta = false)
    {
        var aguja = Normalizar(texto);
        if (aguja.Length == 0) { return []; }

        return vocabulario
            .Where(termino => termino.Valor.Length >= largoMinimo)
            .Where(termino => exigirPalabraCompleta
                ? ContienePalabra(aguja, Normalizar(termino.Valor))
                : aguja.Contains(Normalizar(termino.Valor), StringComparison.Ordinal))
            .Select(termino => new Reconocido(termino.Valor, termino.Usos))
            .OrderByDescending(r => r.Valor.Length)
            .Take(12)
            .ToList();
    }

    /// <summary>
    /// ¿Aparece el término como palabra entera y no dentro de otra?
    /// </summary>
    /// <remarks>
    /// SE MIRAN LOS BORDES A MANO y no con una expresión regular: el término viene de la base de
    /// datos y puede traer paréntesis, puntos o guiones —«Bogotá, D.C.»—, que en una expresión regular
    /// habría que escapar uno a uno y se olvidaría justo el que rompe.
    /// </remarks>
    private static bool ContienePalabra(string texto, string termino)
    {
        if (termino.Length == 0) { return false; }

        var desde = 0;
        while (true)
        {
            var donde = texto.IndexOf(termino, desde, StringComparison.Ordinal);
            if (donde < 0) { return false; }

            var antes = donde == 0 || !char.IsLetterOrDigit(texto[donde - 1]);
            var fin = donde + termino.Length;
            var despues = fin >= texto.Length || !char.IsLetterOrDigit(texto[fin]);
            if (antes && despues) { return true; }

            desde = donde + 1;
        }
    }

    /// <summary>
    /// Deja el texto comparable: sin tildes, sin mayúsculas y sin espacios de más.
    /// </summary>
    /// <remarks>
    /// LA EÑE SE CONSERVA. Quitar diacríticos a lo bruto convierte «Muñoz» en «Munoz», y entonces un
    /// apellido del fichero de autoridades deja de coincidir consigo mismo. Es el mismo cuidado que
    /// pide el índice alfabético del catálogo.
    /// </remarks>
    public static string Normalizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) { return string.Empty; }

        var conservada = texto.Replace('ñ', Centinela).Replace('Ñ', Centinela);
        var descompuesto = conservada.Normalize(NormalizationForm.FormD);
        var sinMarcas = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sinMarcas.Append(caracter);
            }
        }

        return sinMarcas.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace(Centinela, 'ñ')
            .ToLowerInvariant();
    }
}
