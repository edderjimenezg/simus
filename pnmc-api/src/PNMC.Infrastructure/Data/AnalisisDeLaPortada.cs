using System.Text.RegularExpressions;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Decide qué es cada bloque de una primera página: título, subtítulo, autoría o cuerpo.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE EXISTE ESTA CLASE.</b> La primera versión ordenaba los bloques por tamaño de letra y
/// daba el mayor por título y el segundo por subtítulo. Con una portada de libro funciona; con un
/// artículo de revista, no. Probado sobre uno real —«La dialéctica hegeliana del amo y el esclavo»—
/// el segundo bloque más grande era <b>el nombre de la autora</b>, y acabó propuesto como subtítulo.
/// </para>
/// <para>
/// <b>Y HABIA UN SEGUNDO FALLO, PEOR.</b> Los metadatos se ponían por delante del análisis de la
/// página, como si quien generó el PDF supiera siempre mejor. En ese artículo, el título de metadatos
/// era «Seccio Clinica de Barcelona»: el nombre de la plantilla de la revista. El título bueno estaba
/// en la página y quedó relegado. Aquí los candidatos <b>compiten</b>, con motivos que se pueden leer.
/// </para>
/// <para>
/// <b>TRES SEÑALES DETERMINISTAS, Y NINGUNA ES UN MODELO:</b>
/// </para>
/// <list type="number">
///   <item><b>Repetición entre páginas.</b> Lo que aparece igual en la página 1 y en la 2 es un
///   encabezado corriente, no un título. Es la señal más fuerte y no falla.</item>
///   <item><b>Posición vertical.</b> Un encabezado vive en el margen superior; un título, más abajo.</item>
///   <item><b>Forma de nombre propio.</b> «Irene Domínguez Díaz» son tres palabras capitalizadas sin
///   una sola palabra funcional. Un título casi siempre lleva alguna: «del», «para», «entre».</item>
/// </list>
/// </remarks>
public static class AnalisisDeLaPortada
{
    /// <summary>Un bloque de la página, con lo que hace falta para juzgarlo.</summary>
    /// <param name="AlturaRelativa">Dónde está, de 0 (pie) a 1 (cabecera).</param>
    public sealed record Bloque(double Tamano, double AlturaRelativa, string Texto);

    /// <summary>Lo que el análisis concluye, con el motivo de cada decisión.</summary>
    public sealed record Analisis(
        string? Titulo,
        string? MotivoDelTitulo,
        string? Subtitulo,
        IReadOnlyList<string> Autores,
        string? Resumen);

    private static readonly TimeSpan TopeDeBusqueda = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Hasta dónde llega el margen de cabecera.
    /// </summary>
    /// <remarks>
    /// Medido en el artículo real: el encabezado «NODVS XVIII / Setembre de 2006» está al 90 % de
    /// altura y el título al 79 %. El corte en 88 % los separa sin tocar el título.
    /// </remarks>
    private const double AlturaDeCabecera = 0.88;

    /// <summary>
    /// Palabras que aparecen en títulos y nunca dentro de un nombre propio.
    /// </summary>
    /// <remarks>
    /// SE DEJAN FUERA «DE», «DEL», «LA» Y «Y» A PROPOSITO: están en nombres reales —«De la Torre»,
    /// «García y Ruiz»—, así que usarlas como señal descartaría personas de verdad. Las de esta lista
    /// no aparecen en un nombre.
    /// </remarks>
    private static readonly string[] PalabrasQueDelatanUnTitulo =
    [
        "el", "los", "las", "un", "una", "para", "con", "por", "sobre", "entre", "como",
        "que", "su", "sus", "este", "esta", "desde", "hacia", "ante", "segun", "sin",
    ];

    /// <summary>
    /// Analiza la primera página sabiendo qué se repite en las demás.
    /// </summary>
    /// <param name="portada">Los bloques de la página 1.</param>
    /// <param name="tituloDeMetadatos">Lo que declara el diccionario del PDF, si declara algo.</param>
    /// <param name="textoDeOtrasPaginas">
    /// El texto del resto del documento. <b>Es lo que permite reconocer un encabezado</b>: si el
    /// candidato también está ahí, se repite en cada página y no es el título de nada.
    /// </param>
    /// <param name="textoCompleto">
    /// Todo lo leído, PRIMERA PAGINA INCLUIDA. Va aparte porque sirve para otra cosa: el resumen de
    /// un artículo está rotulado en la página 1, y buscarlo solo en las demás —que es lo que hacía la
    /// primera versión— no lo encontraba nunca.
    /// </param>
    public static Analisis Analizar(
        IReadOnlyList<Bloque> portada,
        string? tituloDeMetadatos,
        string textoDeOtrasPaginas,
        string? textoCompleto = null)
    {
        if (portada.Count == 0)
        {
            // Sin texto en la página solo queda lo que declare el PDF de sí mismo.
            var soloMetadatos = Limpio(tituloDeMetadatos);
            return new Analisis(soloMetadatos, soloMetadatos is null ? null : "metadatos", null, [], null);
        }

        var otras = CotejoConElAcervo.Normalizar(textoDeOtrasPaginas);

        // EL CUERPO MAS GRANDE DE LA PAGINA, para no castigar por repetición al propio título.
        // Hay documentos cuya página 2 repite la portada entera —«Teoría del Jazz» lo hace— y, sin
        // esta salvedad, el título se penalizaría por aparecer dos veces mientras que un encabezado
        // pequeño no. Un encabezado corriente NUNCA es el texto más grande de la cubierta.
        var cuerpoMayor = portada.Max(b => b.Tamano);

        // ── Los candidatos a título, puntuados ──────────────────────────────────────
        //
        // LOS PENALIZADORES RESTAN PUNTOS Y NO ESCRIBEN EL MOTIVO. Es una distinción que costó un
        // fallo visible: el bucle iba sobrescribiendo el motivo con el último castigo aplicado, así
        // que «Teoría del Jazz» —que gana con holgura— se anunciaba en pantalla con la nota «tiene
        // forma de nombre propio», o sea, justo la razón que había jugado EN SU CONTRA. Quien
        // cataloga leía como argumento lo que era una objeción descartada. El motivo del ganador se
        // redacta abajo, cuando ya se sabe quién ganó y por qué.
        var candidatos = new List<(string Texto, double Puntos, double Tamano, double Altura, bool DeMetadatos)>();

        foreach (var bloque in portada)
        {
            var texto = Limpio(bloque.Texto);
            if (texto is null) { continue; }

            // El tamaño manda, pero no decide solo.
            var puntos = bloque.Tamano;

            if (bloque.AlturaRelativa > AlturaDeCabecera)
            {
                // UN ENCABEZADO VIVE EN EL MARGEN SUPERIOR. Restar en vez de descartar: hay portadas
                // donde el título sí está arriba del todo.
                puntos -= 12;
            }

            if (bloque.Tamano < cuerpoMayor && SeRepite(texto, otras))
            {
                // LA SEÑAL MAS FUERTE: lo que se repite en otras páginas es un encabezado corriente.
                // Solo se aplica a lo que NO es el texto mayor, por lo dicho arriba.
                puntos -= 30;
            }

            if (EsNombreDePersona(texto))
            {
                // Un nombre propio no es el título de nada.
                puntos -= 25;
            }

            candidatos.Add((texto, puntos, bloque.Tamano, bloque.AlturaRelativa, false));
        }

        // El título de metadatos compite, no gana por serlo.
        var deMetadatos = Limpio(tituloDeMetadatos);
        if (deMetadatos is not null)
        {
            var puntos = 16.0;

            // BOILERPLATE DE PLANTILLA: si el título de metadatos también aparece como encabezado en
            // la página, es el nombre de la plantilla y no el del documento. Pasó de verdad: un
            // artículo declaraba «Seccio Clinica de Barcelona».
            if (portada.Any(b => b.AlturaRelativa > AlturaDeCabecera
                                 && CotejoConElAcervo.Normalizar(b.Texto).Contains(CotejoConElAcervo.Normalizar(deMetadatos), StringComparison.Ordinal)))
            {
                puntos -= 24;
            }

            if (SeRepite(deMetadatos, otras)) { puntos -= 20; }
            if (deMetadatos.Length < 12) { puntos -= 6; }

            candidatos.Add((deMetadatos, puntos, 0, -1, true));
        }

        var ganador = candidatos.OrderByDescending(c => c.Puntos).FirstOrDefault();
        var titulo = ganador.Texto;
        var motivoDelTitulo = titulo is null ? null : MotivoDe(ganador.DeMetadatos, ganador.Tamano, cuerpoMayor);

        // LA PRIMERA LINEA DEL TITULO, CUANDO VA EN OTRO CUERPO. Ver ArrancaMasArriba.
        var arranque = ganador.DeMetadatos ? null : ArrancaMasArriba(portada, ganador.Altura, TamanoDelCuerpo(portada));
        if (arranque is not null && titulo is not null)
        {
            titulo = arranque + " " + titulo;
            motivoDelTitulo = "ocupa dos líneas de la primera página, en cuerpos distintos";
        }

        // ── La autoría: bloques con forma de nombre propio ──────────────────────────
        // SE EXCLUYE LO QUE YA ES PARTE DEL TITULO, no solo el título entero. «LARGOMETRAJES
        // COLOMBIANOS» tiene forma de nombre propio —tres palabras capitalizadas, ninguna funcional—
        // y, mientras la comparación fue por igualdad exacta, se proponía a la vez como primera línea
        // del título y como autor de la obra.
        var tituloNormalizado = titulo is null ? string.Empty : CotejoConElAcervo.Normalizar(titulo);
        var autores = portada
            .Select(b => Limpio(b.Texto))
            .Where(t => t is not null
                        && (tituloNormalizado.Length == 0
                            || !tituloNormalizado.Contains(CotejoConElAcervo.Normalizar(t!), StringComparison.Ordinal))
                        && EsNombreDePersona(t!))
            .Select(t => t!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();

        // ── El subtítulo: el bloque que va JUSTO DEBAJO del título ──────────────────
        var subtitulo = SubtituloBajo(portada, titulo, ganador.Altura);

        return new Analisis(titulo, motivoDelTitulo, subtitulo, autores, Resumen(textoCompleto ?? textoDeOtrasPaginas));
    }

    /// <summary>
    /// El tamaño de letra del cuerpo del documento, medido por cuánto texto hay en cada cuerpo.
    /// </summary>
    /// <remarks>
    /// NO ES EL TAMAÑO MAS FRECUENTE POR NUMERO DE BLOQUES sino por número de caracteres: una cubierta
    /// puede tener seis bloques pequeños de una palabra y uno grande de treinta, y el cuerpo es lo que
    /// más se lee, no lo que más veces aparece. Sirve para distinguir la tipografía de display —la que
    /// puede formar parte de un título— de la de lectura.
    /// </remarks>
    private static double TamanoDelCuerpo(IReadOnlyList<Bloque> portada) =>
        portada.Count == 0
            ? 0
            : portada.GroupBy(b => b.Tamano)
                     .OrderByDescending(g => g.Sum(b => b.Texto.Length))
                     .First().Key;

    /// <summary>
    /// Cuánto más grande que el cuerpo tiene que ser algo para poder formar parte de un título.
    /// </summary>
    private const double VecesElCuerpoParaSerDisplay = 1.5;

    /// <summary>
    /// La primera línea del título, cuando está compuesta en otro cuerpo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>CASO REAL.</b> La cubierta de «Largometrajes colombianos en cine y video» reparte el título
    /// en dos líneas de tamaños muy distintos —«LARGOMETRAJES COLOMBIANOS» a 23 pt y «EN CINE Y VIDEO»
    /// a 46 pt—, que es un recurso de diseño corriente. La fusión de líneas del lector une líneas del
    /// MISMO cuerpo, así que aquí no unía nada: ganaba la línea grande y se proponía «EN CINE Y VIDEO»
    /// como título, mientras la primera línea acababa propuesta como autora de la obra.
    /// </para>
    /// <para>
    /// <b>SOLO SE MIRA HACIA ARRIBA, Y ES DELIBERADO.</b> Hacia abajo está el subtítulo, y pegarlo al
    /// título sería convertir dos datos correctos en uno equivocado —el error más caro de los dos,
    /// porque destruye información que ya estaba bien—. Un título que sigue hacia abajo en otro cuerpo
    /// se queda sin unir, y eso es una pérdida asumible frente a esa confusión.
    /// </para>
    /// </remarks>
    private static string? ArrancaMasArriba(IReadOnlyList<Bloque> portada, double alturaDelTitulo, double cuerpo)
    {
        if (alturaDelTitulo < 0 || cuerpo <= 0) { return null; }

        var encima = portada
            .Where(b => b.AlturaRelativa > alturaDelTitulo)
            .Where(b => b.AlturaRelativa - alturaDelTitulo <= DistanciaMaximaDelSubtitulo)
            .Where(b => b.Tamano >= cuerpo * VecesElCuerpoParaSerDisplay)
            .OrderBy(b => b.AlturaRelativa - alturaDelTitulo)
            .Select(b => Limpio(b.Texto))
            .FirstOrDefault(t => t is not null);

        // Sin punto final: una línea que termina en punto ya cerró su frase y no continúa en la
        // siguiente. Y sin forma de nombre: ahí arriba puede ir la autoría de una cubierta.
        if (encima is null || encima.EndsWith('.') || encima.EndsWith('!') || encima.EndsWith('?')) { return null; }
        return EsNombreDePersona(encima) && encima.Split(' ').Length <= 3 && encima.Any(char.IsLower) ? null : encima;
    }

    /// <summary>
    /// A qué distancia del título puede estar todavía su subtítulo.
    /// </summary>
    /// <remarks>
    /// UN DECIMO DE LA ALTURA DE LA PAGINA, medido sobre documentos reales: en «La dialéctica
    /// hegeliana del amo y el esclavo» la bajada está a un 6,6 % por debajo del título, y en las
    /// cubiertas maquetadas del acervo, entre un 4 y un 7 %. Más abajo de un 10 % ya no es el
    /// subtítulo de nada: es el cuerpo del documento.
    /// </remarks>
    private const double DistanciaMaximaDelSubtitulo = 0.10;

    /// <summary>
    /// Rótulos del aparato editorial que no son el subtítulo de la obra.
    /// </summary>
    /// <remarks>
    /// CASO REAL: en «Aproximación al concepto de gobernanza en Colombia» el bloque que sigue al
    /// título es «Recibido: marzo 11 de 2011 Aprobado: agosto 5 de 2011» —las fechas del proceso
    /// editorial de la revista—, y se proponía como subtítulo. NO SE FILTRA POR «CONTIENE UNA FECHA»,
    /// que sería lo cómodo, porque la bajada legítima del artículo de Hegel también lleva una:
    /// «presentada en el Seminario del Campo Freudiano de Barcelona de mayo de 2006».
    /// </remarks>
    private static readonly string[] AparatoEditorial =
    [
        "recibido", "aprobado", "aceptado", "recepcion", "aceptacion", "publicado",
        "issn", "isbn", "doi", "vol.", "volumen", "num.", "numero", "pp.", "pags",
        "derechos reservados", "copyright",
    ];

    /// <summary>
    /// El subtítulo, si el documento tiene uno de verdad.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SE ELIGE POR POSICION, NO POR TAMAÑO.</b> La versión anterior tomaba el segundo bloque más
    /// grande de la página, y en un artículo a dos columnas eso es un trozo cualquiera del cuerpo: en
    /// «Estrategias de la gestión comunitaria» propuso «Objetivos y estrategias de la gestión
    /// Comunitaria», que es un epígrafe interior, solo porque empataba en tamaño con el resto del
    /// cuerpo y el desempate quedaba al azar del orden de lectura.
    /// </para>
    /// <para>
    /// <b>SE MIRA UN SOLO CANDIDATO Y NO SE SIGUE BAJANDO.</b> Es la decisión importante. Si el
    /// bloque que sigue al título no sirve, <b>no hay subtítulo</b>; seguir descendiendo hasta
    /// encontrar algo que pase los filtros es exactamente cómo se acaba proponiendo el primer párrafo
    /// del resumen. Proponer de menos es barato —quien cataloga lo escribe—; proponer de más obliga a
    /// darse cuenta del error y deshacerlo, y eso es lo que hace que una herramienta así deje de
    /// usarse.
    /// </para>
    /// </remarks>
    private static string? SubtituloBajo(IReadOnlyList<Bloque> portada, string? titulo, double alturaDelTitulo)
    {
        // Un título que vino de los metadatos no tiene sitio en la página: sin posición no hay
        // «lo que va debajo», y se prefiere no proponer nada.
        if (titulo is null || alturaDelTitulo < 0) { return null; }

        var candidato = portada
            .Where(b => b.AlturaRelativa < alturaDelTitulo)
            .Where(b => alturaDelTitulo - b.AlturaRelativa <= DistanciaMaximaDelSubtitulo)
            .Where(b => Limpio(b.Texto) is { } t && t != titulo && t.Length >= 12 && !EsNombreDePersona(t))
            .OrderByDescending(b => b.AlturaRelativa)
            .Select(b => Limpio(b.Texto))
            .FirstOrDefault();

        if (candidato is null || candidato.Length > 220) { return null; }

        // UN SUBTITULO EMPIEZA COMO EMPIEZA UNA FRASE. Lo que arranca en minúscula es un trozo de
        // cuerpo partido por la maquetación a dos columnas: «tos de planeación. Por lo general…».
        if (!char.IsUpper(candidato[0]) && !char.IsDigit(candidato[0])) { return null; }

        var normalizado = CotejoConElAcervo.Normalizar(candidato);
        if (AparatoEditorial.Any(x => normalizado.Contains(x, StringComparison.Ordinal))) { return null; }

        return candidato;
    }

    /// <summary>
    /// Por qué este candidato acabó siendo el título, dicho para quien cataloga.
    /// </summary>
    /// <remarks>
    /// SE REDACTA SOBRE EL GANADOR Y NO DURANTE LA PUNTUACION. Un candidato puede ganar habiendo
    /// recibido castigos —el texto mayor de una cubierta puede además parecer un nombre propio—, y en
    /// ese caso el castigo no es la razón de nada: es una objeción que el resto de señales superó.
    /// Lo que se enseña es la señal que de verdad lo sostiene.
    /// </remarks>
    private static string MotivoDe(bool deMetadatos, double tamano, double cuerpoMayor)
    {
        if (deMetadatos) { return "lo declara el documento en sus metadatos"; }
        if (tamano >= cuerpoMayor) { return "es el texto más grande de la primera página"; }
        return "es el texto destacado de la primera página que no se repite en el resto";
    }

    /// <summary>
    /// ¿Tiene forma de nombre propio?
    /// </summary>
    /// <remarks>
    /// DOS A CINCO PALABRAS, casi todas capitalizadas, sin cifras y sin ninguna palabra que solo
    /// aparece en títulos. «Irene Domínguez Díaz» pasa; «La dialéctica hegeliana del amo y el
    /// esclavo» no, porque lleva «el». Es una heurística y se equivocará alguna vez: por eso la
    /// autoría se PROPONE y quien cataloga la confirma.
    /// </remarks>
    public static bool EsNombreDePersona(string texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        if (limpio.Length is < 5 or > 60) { return false; }
        if (limpio.Any(char.IsDigit)) { return false; }

        var palabras = limpio.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length is < 2 or > 5) { return false; }

        foreach (var palabra in palabras)
        {
            var normal = CotejoConElAcervo.Normalizar(palabra).Trim('.', ',', ';');
            if (PalabrasQueDelatanUnTitulo.Contains(normal, StringComparer.Ordinal)) { return false; }
        }

        // Al menos dos palabras empiezan por mayúscula: es lo que distingue un nombre de una frase.
        var capitalizadas = palabras.Count(p => p.Length > 0 && char.IsUpper(p[0]));
        return capitalizadas >= 2;
    }

    /// <summary>
    /// El resumen, cuando el documento lo rotula.
    /// </summary>
    /// <remarks>
    /// SE TOMA LO QUE SIGUE AL ROTULO y hasta donde empieza otro. Un artículo académico casi siempre
    /// escribe «Resumen» o «Abstract»; una publicación institucional, no, y entonces no se propone
    /// nada en vez de inventar un resumen cortando el primer párrafo, que podría ser una dedicatoria.
    /// </remarks>
    private static string? Resumen(string texto)
    {
        var patron = new Regex(
            @"\b(?:resumen|abstract|sinopsis)\b\s*[:.\-]?\s*(?<cuerpo>.{60,900}?)(?=\b(?:palabras\s+clave|keywords|introducci[oó]n|abstract)\b|$)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline, TopeDeBusqueda);

        var coincidencia = patron.Match(texto ?? string.Empty);
        return coincidencia.Success ? Limpio(coincidencia.Groups["cuerpo"].Value) : null;
    }

    /// <summary>¿Aparece este texto también fuera de la primera página?</summary>
    private static bool SeRepite(string texto, string otrasPaginas)
    {
        var aguja = CotejoConElAcervo.Normalizar(texto);
        // Muy corto no dice nada: «v» o «2006» se repiten en cualquier documento.
        return aguja.Length >= 8 && otrasPaginas.Contains(aguja, StringComparison.Ordinal);
    }

    /// <summary>
    /// Normaliza los espacios y DESCARTA lo que no sea texto legible.
    /// </summary>
    /// <remarks>
    /// EL FILTRO ESTA AQUI Y NO EN LA PANTALLA porque este es el embudo: todo candidato —venga de un
    /// bloque de la página o del diccionario del PDF— pasa por esta función. Ver <see cref="TextoLegible"/>
    /// para los casos reales que obligaron a ponerlo.
    /// </remarks>
    private static string? Limpio(string? valor)
    {
        var limpio = Regex.Replace((valor ?? string.Empty).Trim(), @"\s+", " ", RegexOptions.None, TopeDeBusqueda);
        if (limpio.Length < 3) { return null; }
        return TextoLegible.EsPlausible(limpio) && !TextoLegible.PareceNombreDeArchivo(limpio) ? limpio : null;
    }
}
