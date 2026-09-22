using System.Globalization;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Presentacion de texto en espanol de Colombia. Hoy, una sola funcion.
/// </summary>
/// <remarks>
/// Salio de <c>AdminDataEndpoints</c> el 24 ago 2026 porque dejo de tener un solo usuario:
/// <see cref="CacheDivipola"/> necesita exactamente la misma capitalizacion para poder servir los
/// nombres del DANE ya presentables, y la alternativa —que el cache llamara a un metodo privado
/// de un fichero de rutas— habria puesto la dependencia al reves.
/// </remarks>
internal static class TextoEnEspanol
{
    /// <summary>
    /// Capitaliza como se muestra en pantalla: «VALLE DEL CAUCA» pasa a «Valle Del Cauca».
    /// </summary>
    /// <remarks>
    /// <para>
    /// HACE TRES COSAS Y LAS TRES IMPORTAN. Recorta; baja TODO a minusculas antes de capitalizar
    /// —sin ese paso, <c>ToTitleCase</c> respeta las palabras que ya vienen en mayusculas y
    /// «VALLE DEL CAUCA» se quedaria igual, que es justo el caso de la base—; y arregla a mano
    /// «D.c.», porque la capitalizacion por palabras no entiende las abreviaturas con punto y
    /// convierte «bogota, d.c.» en «Bogota, D.c.».
    /// </para>
    /// <para>
    /// Depende de ICU: el proyecto declara <c>InvariantGlobalization=false</c> a proposito, de
    /// modo que <c>es-CO</c> se resuelve de verdad y no cae en la cultura invariante.
    /// </para>
    /// </remarks>
    /// <summary>La cifra con su palabra en el número que le toca.</summary>
    /// <remarks>
    /// <b>UNA SOLA FORMA DE HACERLO, Y VIVE AQUI PORQUE LOS DOS ESPACIOS ESCRIBEN FRASES.</b> El
    /// ternario del plural estaba escrito a mano allí donde alguien se acordó, y las dos veces que
    /// falló se vieron igual: preguntándole a la API levantada, no en una prueba. «1 Ediciones
    /// registradas, de 1 Festivales distintos» en la consola, y «Tienes Festivales registrados en 1
    /// municipios» en el espacio externo. Con una organización pequeña o un territorio poco poblado,
    /// el uno es el caso corriente y no el raro.
    /// </remarks>
    public static string Plural(int cuantos, string singular, string plural) =>
        $"{cuantos.ToString(CultureInfo.InvariantCulture)} {(cuantos == 1 ? singular : plural)}";

    public static string ATituloDeColombia(string? value)
    {
        var limpio = (value ?? string.Empty).Trim().ToLowerInvariant();
        return CultureInfo.GetCultureInfo("es-CO").TextInfo.ToTitleCase(limpio)
            .Replace("D.c.", "D.C.", StringComparison.OrdinalIgnoreCase);
    }
}
