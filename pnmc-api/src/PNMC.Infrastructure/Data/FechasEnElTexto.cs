using System.Globalization;
using System.Text.RegularExpressions;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Las fechas que un afiche anuncia, leídas como las escribe la gente.
/// </summary>
/// <remarks>
/// <para>
/// <b>PARA QUE SIRVE.</b> La misma tubería que lee una publicación tiene que servir para un afiche o
/// un volante de la Agenda, y lo que un afiche dice es <b>cuándo y dónde</b>. El cuándo va casi
/// siempre en español corriente —«12 de octubre de 2025», «Del 3 al 7 de agosto»— y eso se reconoce
/// sin modelo: es un patrón, no una interpretación.
/// </para>
/// <para>
/// <b>SE ACEPTAN LAS TRES FORMAS QUE SE USAN DE VERDAD:</b> la escrita con el mes en letra, la
/// numérica con barras o guiones, y el rango «del … al …», que es como se anuncia un festival. Lo que
/// NO se acepta es el formato de otros países —mes antes que día— porque aquí el 3/4 es el tres de
/// abril y adivinar la convención sobre un afiche colombiano sería inventar.
/// </para>
/// <para>
/// <b>Y SE DEVUELVEN TODAS, NO UNA.</b> Un afiche lleva la fecha del evento, a veces la de cierre de
/// inscripciones y casi siempre el año de la convocatoria. Elegir por cuenta propia cuál es «la»
/// fecha es justo el tipo de decisión que le toca a quien registra.
/// </para>
/// </remarks>
public static class FechasEnElTexto
{
    /// <summary>Una fecha reconocida, con el trozo de texto del que salió.</summary>
    /// <param name="Fin">Cuando el texto anunciaba un rango, el día en que termina.</param>
    public sealed record FechaReconocida(DateOnly Inicio, DateOnly? Fin, string Origen);

    private static readonly TimeSpan TopeDeBusqueda = TimeSpan.FromSeconds(2);

    private static readonly Dictionary<string, int> Meses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enero"] = 1, ["febrero"] = 2, ["marzo"] = 3, ["abril"] = 4,
        ["mayo"] = 5, ["junio"] = 6, ["julio"] = 7, ["agosto"] = 8,
        ["septiembre"] = 9, ["setiembre"] = 9, ["octubre"] = 10,
        ["noviembre"] = 11, ["diciembre"] = 12,
    };

    private const string NombresDeMes =
        "enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre";

    /// <summary>
    /// Todas las fechas que el texto anuncia, sin repetir y en orden.
    /// </summary>
    public static List<FechaReconocida> Leer(string texto, int? anioPorDefecto = null)
    {
        var limpio = CotejoConElAcervo.Normalizar(texto ?? string.Empty);
        var encontradas = new List<FechaReconocida>();

        // ── «Del 3 al 7 de agosto de 2025» ──────────────────────────────────────────
        // El rango va PRIMERO a propósito: si se buscaran antes las fechas sueltas, «3 al 7 de
        // agosto» daría dos fechas independientes y se perdería que son un mismo evento.
        var rango = new Regex(
            @"del?\s+(\d{1,2})\s+al\s+(\d{1,2})\s+de\s+(" + NombresDeMes + @")(?:\s+de\s+(\d{4}))?",
            RegexOptions.IgnoreCase, TopeDeBusqueda);

        // EL TRAMO RECONOCIDO SE TAPA. Buscar el rango primero no basta: el patrón de fecha suelta
        // vería después «7 de agosto de 2024» dentro del mismo texto y añadiría un segundo evento de
        // un día. Lo dice el comentario de arriba y la prueba demostró que el código no lo cumplía.
        var sinRangos = limpio;

        foreach (Match coincidencia in rango.Matches(limpio))
        {
            var mes = Meses[coincidencia.Groups[3].Value];
            var anio = Anio(coincidencia.Groups[4].Value, anioPorDefecto);
            if (anio is null) { continue; }

            var inicio = Construir(int.Parse(coincidencia.Groups[1].Value, CultureInfo.InvariantCulture), mes, anio.Value);
            var fin = Construir(int.Parse(coincidencia.Groups[2].Value, CultureInfo.InvariantCulture), mes, anio.Value);
            if (inicio is not null && fin is not null)
            {
                Agregar(encontradas, new FechaReconocida(inicio.Value, fin, coincidencia.Value.Trim()));
                sinRangos = sinRangos.Replace(coincidencia.Value, new string(' ', coincidencia.Value.Length), StringComparison.Ordinal);
            }
        }

        // ── «12 de octubre de 2025» ─────────────────────────────────────────────────
        var conMesEnLetra = new Regex(
            @"(\d{1,2})\s+de\s+(" + NombresDeMes + @")(?:\s+de[l]?\s+(\d{4}))?",
            RegexOptions.IgnoreCase, TopeDeBusqueda);

        foreach (Match coincidencia in conMesEnLetra.Matches(sinRangos))
        {
            var anio = Anio(coincidencia.Groups[3].Value, anioPorDefecto);
            if (anio is null) { continue; }

            var fecha = Construir(
                int.Parse(coincidencia.Groups[1].Value, CultureInfo.InvariantCulture),
                Meses[coincidencia.Groups[2].Value],
                anio.Value);
            if (fecha is not null) { Agregar(encontradas, new FechaReconocida(fecha.Value, null, coincidencia.Value.Trim())); }
        }

        // ── «12/10/2025» y «12-10-2025» ─────────────────────────────────────────────
        // DIA PRIMERO, SIEMPRE. Es la convención de aquí, y adivinar la del documento sobre un afiche
        // colombiano sería inventar una lectura que nadie escribió.
        var numerica = new Regex(@"\b(\d{1,2})[/\-](\d{1,2})[/\-](\d{2,4})\b", RegexOptions.None, TopeDeBusqueda);
        foreach (Match coincidencia in numerica.Matches(sinRangos))
        {
            var anioTexto = coincidencia.Groups[3].Value;
            var anio = anioTexto.Length == 2
                ? 2000 + int.Parse(anioTexto, CultureInfo.InvariantCulture)
                : int.Parse(anioTexto, CultureInfo.InvariantCulture);

            var fecha = Construir(
                int.Parse(coincidencia.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(coincidencia.Groups[2].Value, CultureInfo.InvariantCulture),
                anio);
            if (fecha is not null) { Agregar(encontradas, new FechaReconocida(fecha.Value, null, coincidencia.Value.Trim())); }
        }

        return encontradas.OrderBy(f => f.Inicio).ToList();
    }

    /// <summary>
    /// El año declarado, o el que se pasó por defecto.
    /// </summary>
    /// <remarks>
    /// UN AFICHE MUCHAS VECES NO REPITE EL AÑO —«12 de octubre», y ya—, porque quien lo lee está
    /// dentro de ese año. Se acepta uno por defecto (el que se haya encontrado en otra parte del
    /// documento) en vez de descartar la fecha, pero NO se inventa el año en curso: eso pondría en la
    /// agenda un evento del año equivocado sin que nadie lo decidiera.
    /// </remarks>
    private static int? Anio(string declarado, int? porDefecto)
    {
        if (!string.IsNullOrWhiteSpace(declarado)
            && int.TryParse(declarado, CultureInfo.InvariantCulture, out var anio))
        {
            return anio;
        }
        return porDefecto;
    }

    private static DateOnly? Construir(int dia, int mes, int anio)
    {
        if (mes is < 1 or > 12 || dia < 1 || anio is < 1900 or > 2100) { return null; }
        if (dia > DateTime.DaysInMonth(anio, mes)) { return null; }
        return new DateOnly(anio, mes, dia);
    }

    /// <summary>Sin repetir: un afiche anuncia la misma fecha en varios sitios.</summary>
    private static void Agregar(List<FechaReconocida> lista, FechaReconocida fecha)
    {
        if (!lista.Any(x => x.Inicio == fecha.Inicio && x.Fin == fecha.Fin))
        {
            lista.Add(fecha);
        }
    }
}
