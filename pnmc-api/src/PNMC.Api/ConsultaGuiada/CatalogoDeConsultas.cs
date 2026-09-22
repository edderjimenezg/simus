using System.Globalization;
using System.Text;

namespace PNMC.Api.ConsultaGuiada;

/// <summary>Quién puede pedir una consulta.</summary>
/// <remarks>
/// <b>NO ES UN FILTRO DE PANTALLA, ES UNA FRONTERA DE DATOS.</b> Las consultas institucionales
/// miran las bandejas del Programa y el directorio entero; las de organización miran solo lo de
/// quien pregunta. Mezclarlas en una sola lista y confiar en que la interfaz esconda las que no
/// tocan es como se acaba respondiéndole a una organización cuántos Festivales hay en revisión en
/// todo el país.
/// </remarks>
public enum AmbitoDeConsulta
{
    /// <summary>El Programa mirando toda la operación.</summary>
    Institucional,

    /// <summary>Una organización mirando lo suyo.</summary>
    Organizacion,
}

/// <summary>
/// Una consulta del catálogo: qué responde, a quién y con qué palabras se pide.
/// </summary>
/// <param name="Pistas">
/// Las palabras que la eligen. Basta con que aparezca una.
/// </param>
/// <param name="PistasAdicionales">
/// Cuando hace falta que coincidan DOS cosas. «Municipios con más Festivales» necesita a la vez la
/// palabra del territorio y la de la comparación: sin esto, «cuántos municipios hay» se llevaría la
/// consulta de los municipios con más presencia, que es otra pregunta.
/// </param>
/// <remarks>
/// <b>LAS PISTAS VIVEN CON EL ROTULO, NO EN OTRO FICHERO.</b> Antes el catálogo declaraba los
/// rótulos y un método aparte declaraba las palabras que los eligen. Añadir una consulta eran dos
/// sitios, y olvidarse del segundo dejaba una consulta anunciada que ninguna pregunta alcanzaba.
/// </remarks>
/// <param name="PistasQueLaDescartan">
/// Palabras que, si aparecen, dejan esta consulta fuera por mucho que coincida lo demás.
/// <b>NO ES UN AJUSTE FINO, ES UNA FRONTERA ENTRE PROCESOS.</b> «¿Cuántos mercados hay en
/// borrador?» y «¿cuántos borradores hay?» comparten la palabra que más pesa, y sin esto la
/// consulta de Festivales por estado contestaría la primera: una cifra de Festivales bajo una
/// pregunta sobre mercados, que es la clase de error que este catálogo persigue. La puntuación no
/// puede arreglarlo —«borrador» mide más que «mercado» y ese es todo el problema—, y bajarle el peso
/// a una palabra para que gane otra sería un número mágico que nadie sabría mantener.
/// </param>
/// <param name="SeAcotaPorTerritorio">
/// Si esta consulta sabe responder solo de un departamento o municipio. No es una etiqueta
/// descriptiva: decide quién compite cuando la pregunta nombra un sitio.
/// </param>
/// <param name="SeAcotaPorAnio">Si esta consulta sabe responder solo de un año.</param>
/// <param name="SeAcotaPorEstado">Si esta consulta sabe responder solo de un estado del circuito.</param>
/// <param name="SoloMiraLoPublicado">
/// Si por definición solo cuenta lo que está publicado. No es lo mismo que no acotarse por estado:
/// una pregunta que dice «publicados» ya está contestada, y una que dice «en revisión» no puede
/// estarlo. Sin esta distinción, media consola contestaría con un aviso de que no se acota al estado
/// «Publicado» debajo de una cifra que es justamente de lo publicado.
/// </param>
public sealed record ConsultaDelCatalogo(
    string Id,
    string Rotulo,
    string Descripcion,
    AmbitoDeConsulta Ambito,
    IReadOnlyList<string> Pistas,
    IReadOnlyList<string>? PistasAdicionales = null,
    IReadOnlyList<string>? PistasQueLaDescartan = null,
    bool SeAcotaPorTerritorio = false,
    bool SeAcotaPorAnio = false,
    bool SeAcotaPorEstado = false,
    bool SoloMiraLoPublicado = false)
{
    /// <summary>Si la pregunta ya normalizada elige esta consulta, sin mirar entidades.</summary>
    public bool Coincide(string preguntaNormalizada) =>
        Puntuar(CatalogoDeConsultas.PalabrasDe(preguntaNormalizada), EntidadesDeLaPregunta.Ninguna) > 0;

    /// <summary>
    /// Cuánto responde esta consulta a la pregunta. Cero si no responde.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>POR QUE UNA PUNTUACION Y NO UN SI O NO.</b> Hasta ganaba la
    /// PRIMERA del catálogo que coincidiera, así que el orden de la lista decidía las respuestas.
    /// Eso ya había obligado a colocar a mano «mercados por departamento» antes que «festivales por
    /// departamento» y «municipios con más» antes que «cuántos municipios», con un comentario
    /// explicando por qué no se podía mover ninguna. Con veinte consultas ese orden es un campo de
    /// minas: añadir una puede robarle las preguntas a otra sin que nada avise.
    /// </para>
    /// <para>
    /// <b>PUNTUA LA ESPECIFICIDAD, NO LA CANTIDAD.</b> Cada pista que acierta vale lo que mide: una
    /// pista larga —«presencia territorial»— dice mucho más sobre la intención que una corta
    /// —«mas»—, y sumarlas por unidades haría ganar a la consulta con más pistas sueltas y no a la
    /// que de verdad describe la pregunta.
    /// </para>
    /// <para>
    /// <b>LAS PISTAS ADICIONALES SIGUEN SIENDO OBLIGATORIAS</b> y no un extra que suma: cuando una
    /// consulta declara que necesita dos cosas a la vez, es porque sin las dos responde otra
    /// pregunta.
    /// </para>
    /// </remarks>
    public int Puntuar(IReadOnlyList<string> palabrasDeLaPregunta, EntidadesDeLaPregunta entidades)
    {
        if (PistasQueLaDescartan is not null
            && PistasQueLaDescartan.Any(pista => Aparece(pista, palabrasDeLaPregunta)))
        {
            return 0;
        }

        var principales = Pistas.Where(pista => Aparece(pista, palabrasDeLaPregunta)).ToList();
        if (principales.Count == 0) { return 0; }

        if (PistasAdicionales is not null)
        {
            var adicionales = PistasAdicionales.Where(pista => Aparece(pista, palabrasDeLaPregunta)).ToList();
            // NOMBRAR UN SITIO ES LA PISTA TERRITORIAL, y por eso satisface la exigencia de las
            // pistas adicionales de una consulta que sabe acotarse. «¿En qué departamentos hay
            // mercados?» y «¿qué mercados hay en el Huila?» son la misma pregunta: la primera dice
            // la palabra «departamento» y la segunda dice un departamento.
            var territorioVale = SeAcotaPorTerritorio && entidades.Territorio is not null;
            if (adicionales.Count == 0 && !territorioVale) { return 0; }
            return principales.Sum(pista => pista.Length) + adicionales.Sum(pista => pista.Length);
        }

        return principales.Sum(pista => pista.Length);
    }

    /// <summary>Si la pista —una palabra o una secuencia de ellas— está en la pregunta.</summary>
    private static bool Aparece(string pista, IReadOnlyList<string> palabras)
    {
        var deLaPista = CatalogoDeConsultas.PalabrasDe(pista);
        if (deLaPista.Count == 0 || deLaPista.Count > palabras.Count) { return false; }

        for (var i = 0; i <= palabras.Count - deLaPista.Count; i++)
        {
            var casan = true;
            for (var j = 0; j < deLaPista.Count && casan; j++)
            {
                casan = CatalogoDeConsultas.PalabraCasa(deLaPista[j], palabras[i + j]);
            }
            if (casan) { return true; }
        }
        return false;
    }

}

/// <summary>
/// Lo único que la Consulta Guiada sabe responder, en un solo sitio y con su ámbito.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL ORDEN IMPORTA Y ES DELIBERADO.</b> Se recorre de arriba abajo y gana la primera que
/// coincide, así que las preguntas más específicas van antes que las más generales: «¿qué está
/// incompleto?» tiene que ganarle a «¿cómo está la plataforma?», o cualquier pregunta con la palabra
/// «cómo» acabaría devolviendo el resumen.
/// </para>
/// <para>
/// <b>AÑADIR UNA CONSULTA ES AÑADIR UNA ENTRADA AQUI</b> y su cálculo en el espacio que
/// corresponda. El identificador es el contrato: viaja en la respuesta como
/// <c>consultaElegida</c> y es lo que permite comprobar después qué se respondió.
/// </para>
/// </remarks>
public static class CatalogoDeConsultas
{
    /// <summary>La consulta que explica qué puede responder el asistente. No mira la base.</summary>
    public const string Ayuda = "ayuda";

    // ---------- Institucionales ------------------------------------------------------------
    public const string Resumen = "resumen_administrativo";
    public const string Departamentos = "festivales_por_departamento";
    public const string Municipios = "municipios_con_mas_festivales";
    public const string Cobertura = "cobertura_de_festivales";
    public const string SinFestival = "territorios_sin_festival";
    public const string Tematica = "distribucion_tematica";
    public const string Pendientes = "pendientes_de_gestion";
    public const string Calidad = "calidad_de_datos";
    public const string Mercados = "resumen_de_mercados";
    public const string MercadosPorDepartamento = "mercados_por_departamento";
    public const string Ediciones = "ediciones_por_anio";
    public const string Estados = "festivales_por_estado";

    // ---------- De la organización ---------------------------------------------------------
    public const string MisProcesos = "mis_procesos";
    public const string MisPendientes = "mis_pendientes";
    public const string MiCalidad = "mi_calidad_de_datos";
    public const string MiTerritorio = "mi_presencia_territorial";
    public const string MisMercados = "mis_mercados";

    private static readonly ConsultaDelCatalogo[] Todas =
    [
        new(Ayuda, "Qué puedo preguntar", "Explica el alcance del asistente y sus límites.",
            AmbitoDeConsulta.Institucional, ["ayuda", "que puedo preguntar", "como funciona", "que sabes hacer"]),

        new(Calidad, "Calidad de datos", "Conteos de campos incompletos y alertas abiertas; nunca identifica registros.",
            AmbitoDeConsulta.Institucional,
            // «sin organizacion», «sin responsable» y «sin contacto» entraron el 17 de septiembre de
            // 2026: son las tres formas en que de verdad se pregunta por un campo vacío, y ninguna
            // alcanzaba esta consulta.
            // «FALTA» Y «QUE LE FALTA» ENTRARON EL 17 DE SEPTIEMBRE DE 2026, con la lectura del
            // estado. «¿Qué le falta a los festivales en revisión?» se la llevaba la consulta de
            // Festivales por estado, porque «en revisión» mide once caracteres y aquí no había
            // ninguna pista que recogiera la forma más corriente de preguntar por un hueco.
            ["incomplet", "calidad", "sin diligenciar", "datos faltan", "informacion falta",
             "dato faltante", "sin organizacion", "sin responsable", "sin contacto",
             "falta", "que le falta", "que tan completa"],
            SeAcotaPorTerritorio: true, SeAcotaPorEstado: true),

        new(Pendientes, "Pendientes de gestión", "Conteos de revisiones, propuestas, solicitudes, reclamaciones y alertas abiertas.",
            AmbitoDeConsulta.Institucional, ["pendient", "solicitud", "bandeja", "necesita atencion", "necesitan atencion", "por revisar"]),

        new(SinFestival, "Territorios sin Festival", "Departamentos y total de municipios sin Festival publicado y ubicado.",
            AmbitoDeConsulta.Institucional, ["donde no hay", "sin festival", "no hay festival", "no tienen festival", "sin cobertura"],
            SeAcotaPorTerritorio: true, SoloMiraLoPublicado: true),

        new(Tematica, "Distribución temática", "Prácticas musicales, territorios sonoros y periodicidades de Festivales publicados.",
            // YA NO HACE FALTA DECLARAR EL PLURAL: desde que se compara por palabras, «práctica
            // musical» alcanza «prácticas musicales».
            AmbitoDeConsulta.Institucional, ["practica musical", "territorio sonoro", "periodicidad", "distribucion tematica"],
            SeAcotaPorTerritorio: true, SoloMiraLoPublicado: true),

        // ---------- Mercados musicales -------------------------------------------------------
        //
        // VAN ANTES QUE LAS DE FESTIVALES porque son más específicas: sin esto, «¿en qué
        // departamentos hay mercados?» se la llevaría «Festivales por departamento», que comparte
        // la palabra «departament». Es la misma razón por la que «municipios con más» va antes que
        // «cuántos municipios hay».
        new(MercadosPorDepartamento, "Mercados por departamento", "Distribución de los mercados publicados con ubicación departamental.",
            AmbitoDeConsulta.Institucional, ["mercado"], ["departament", "territorio", "donde", "distrib", "repart"],
            SeAcotaPorTerritorio: true, SoloMiraLoPublicado: true),

        new(Mercados, "Resumen de mercados musicales", "Cuántos mercados hay, cuántos están publicados y cuántos ocurren dentro de un festival.",
            AmbitoDeConsulta.Institucional, ["mercado"], SeAcotaPorEstado: true),

        new(Departamentos, "Festivales por departamento", "Distribución de Festivales publicados con ubicación departamental.",
            AmbitoDeConsulta.Institucional, ["departament"], ["mas", "por", "distrib", "repart", "mayor"],
            SeAcotaPorTerritorio: true, SoloMiraLoPublicado: true),

        new(Municipios, "Municipios con más Festivales", "Municipios con mayor presencia de Festivales publicados.",
            AmbitoDeConsulta.Institucional, ["municip"], ["mas", "top", "concentr", "mayor", "por"],
            SeAcotaPorTerritorio: true, SoloMiraLoPublicado: true),

        new(Cobertura, "Cobertura de Festivales", "Municipios y departamentos con al menos un Festival publicado y ubicado.",
            AmbitoDeConsulta.Institucional, ["cobertura", "cuantos municipios", "cuantos departamentos", "presencia territorial"],
            SeAcotaPorTerritorio: true, SoloMiraLoPublicado: true),

        // ---------- Estados del circuito ------------------------------------------------------
        //
        // NO DECLARA «PUBLICADO» ENTRE SUS PISTAS, Y ESA AUSENCIA ES LA QUE LA HACE POSIBLE. Todas
        // las demás consultas de Festivales miran lo publicado, así que una pregunta que dice
        // «publicados» ya está contestada por ellas; si «publicad» estuviera aquí, «¿qué
        // departamentos tienen más Festivales publicados?» y «¿cuántos mercados están publicados?»
        // acabarían las dos en esta tabla. Lo que ninguna otra consulta puede contestar es lo que
        // NO está publicado: los quince borradores y los ocho en revisión que hay en la base.
        new(Estados, "Festivales por estado", "Cuántos Festivales hay en cada estado del circuito, incluidos los que no están publicados.",
            AmbitoDeConsulta.Institucional,
            // LA FORMA POSITIVA ES DE LAS OTRAS CONSULTAS; LA NEGATIVA ES SOLO DE ESTA. «Publicados»
            // lo contestan las nueve consultas que parten de la lectura pública, y por eso no está
            // aquí. «Sin publicar» y «no están publicados» no los puede contestar ninguna de ellas:
            // preguntan justo por lo que se quedó fuera, que son los quince borradores y los ocho en
            // revisión. Declarar las dos formas negativas no reabre el choque que evitaba quitar
            // «publicad», porque ninguna pregunta sobre lo publicado las contiene.
            ["estado", "en revision", "borrador", "ajustes solicitados", "archivad", "rechazad",
             "sin publicar", "no publicado", "no estan publicado"],
            PistasQueLaDescartan: ["mercado"],
            SeAcotaPorTerritorio: true, SeAcotaPorEstado: true),

        // ---------- Ediciones -----------------------------------------------------------------
        //
        // ES LA UNICA CONSULTA QUE SABE LO QUE ES UN AÑO, y existe por eso: hasta que la hubo, el
        // año que alguien escribiera en la pregunta no tenía dónde aplicarse, y una entidad que se
        // reconoce y no se usa es peor que no reconocerla —promete un recorte que no ocurre—. La
        // Edición es el único sitio del modelo donde el año es un dato propio y no una fecha de
        // auditoría: `dbo.EdicionesFestival.Anio`.
        new(Ediciones, "Ediciones por año", "Cuántas Ediciones hay por año, de Festival y de mercado musical.",
            // «EDICION DE MERCADO» ES PISTA PROPIA porque «edicion» y «mercado» miden lo mismo —siete
            // caracteres— y el resumen de mercados ganaba el desempate por orden de catálogo. Esa
            // respuesta no era falsa, pero decía menos: el resumen trae una fila con el total de
            // Ediciones y esta consulta las reparte por año y las separa de las de Festival.
            AmbitoDeConsulta.Institucional, ["edicion", "edicion de mercado"], SeAcotaPorAnio: true),

        new(Resumen, "Resumen administrativo", "Totales de Festivales, publicaciones, organizaciones y asuntos pendientes.",
            AmbitoDeConsulta.Institucional,
            // «organizacion» y «cuentas registradas» entraron: el
            // resumen las cuenta desde siempre, pero ninguna pregunta que las nombrara lo alcanzaba.
            ["resumen", "panorama", "como esta", "cuantos festivales", "total de festivales",
             "plataforma hoy", "organizacion", "cuentas registradas"],
            SeAcotaPorTerritorio: true, SeAcotaPorEstado: true),

        // ---------- De la organización ------------------------------------------------------
        new(Ayuda, "Qué puedo preguntar", "Explica el alcance del asistente y sus límites.",
            AmbitoDeConsulta.Organizacion, ["ayuda", "que puedo preguntar", "como funciona", "que sabes hacer"]),

        // LAS CUATRO SE ACOTAN IGUAL QUE SUS EQUIVALENTES DE LA CONSOLA. Una organización pregunta
        // «¿dónde están mis Festivales en el Huila?» con las mismas palabras con las que un
        // funcionario pregunta por el país entero, y hasta recibía la
        // cifra de toda su organización con un aviso de que no se acotaba. El aviso era honesto y
        // era la mitad del trabajo.
        new(MisPendientes, "Qué necesita mi atención", "Lo que está esperando una acción de la organización, no del Programa.",
            AmbitoDeConsulta.Organizacion, ["pendient", "atencion", "que me falta", "que tengo que hacer", "ajustes", "esperando"],
            PistasQueLaDescartan: ["mercado"], SeAcotaPorTerritorio: true),

        // ESTA NO DECLARA LA FRONTERA DE MERCADOS, y es la única de las cuatro que no la declara:
        // «¿qué información me falta?» no pregunta por un módulo, pregunta por lo que hay que
        // completar, y contesta de los Festivales y de los mercados a la vez.
        new(MiCalidad, "Qué información me falta", "Campos vacíos en los Festivales y los mercados de la organización, nombrando cuáles.",
            // «QUE LE FALTA» PESA MAS QUE EL SUJETO, y tiene que pesar más: «¿qué le falta a mis
            // mercados?» y «¿qué le falta a mis festivales?» preguntan las dos por un hueco, no por
            // el estado del proceso que nombran. Sin esta pista, la primera acababa en la tabla de
            // estados de los mercados y la segunda en la de los procesos.
            AmbitoDeConsulta.Organizacion, ["incomplet", "calidad", "falta", "que le falta", "vacio", "sin diligenciar"],
            SeAcotaPorTerritorio: true, SeAcotaPorEstado: true),

        new(MiTerritorio, "Dónde están mis Festivales", "Territorios en los que la organización tiene Festivales registrados.",
            // «DONDE ESTAN» Y «EN QUE MUNICIPIOS» ENTRARON EL 17 DE SEPTIEMBRE DE 2026, y las
            // descubrió medir este ámbito por primera vez: «¿Dónde están mis Festivales?» —que es
            // el rótulo de esta consulta y una de las cuatro sugerencias que el propio asistente
            // ofrece escritas— llegaba a «Cómo van mis procesos», porque «festival» mide ocho
            // caracteres y «donde» cinco. La prueba de las sugerencias no lo veía: comprobaba que
            // resolvieran algo, no que resolvieran lo suyo.
            AmbitoDeConsulta.Organizacion,
            ["territorio", "donde", "donde estan", "municip", "en que municipios", "departament", "cobertura", "ubicacion"],
            PistasQueLaDescartan: ["mercado"], SeAcotaPorTerritorio: true, SeAcotaPorEstado: true),

        // LAS CUATRO CONSULTAS DE FESTIVAL DECLARAN QUE NO CONTESTAN POR MERCADOS, igual que en la
        // consola. Sin esa frontera, «¿cómo van mis mercados?» recibía «Tienes 1 Festival
        // registrado», que es contestar otra cosa con autoridad: la palabra «como van» mide ocho
        // caracteres y «mercado» siete.
        new(MisMercados, "Cómo van mis mercados", "Estado de los mercados musicales de la organización y de sus ediciones.",
            AmbitoDeConsulta.Organizacion, ["mercado"],
            SeAcotaPorTerritorio: true, SeAcotaPorEstado: true),

        new(MisProcesos, "Cómo van mis procesos", "Estado de los Festivales y los mercados de la organización, y de sus ediciones.",
            AmbitoDeConsulta.Organizacion, ["proceso", "festival", "edicion", "estado", "como van", "resumen", "como esta"],
            PistasQueLaDescartan: ["mercado"], SeAcotaPorTerritorio: true, SeAcotaPorEstado: true),
    ];

    /// <summary>Las consultas que ese ámbito puede pedir, en el orden en que se interpretan.</summary>
    public static IReadOnlyList<ConsultaDelCatalogo> De(AmbitoDeConsulta ambito) =>
        Todas.Where(consulta => consulta.Ambito == ambito).ToList();

    /// <summary>
    /// Las preguntas que se ofrecen escritas, para no obligar a adivinar la formulación.
    /// </summary>
    /// <remarks>
    /// SON EJEMPLOS REALES QUE EL INTERPRETE RESUELVE, no eslóganes: una sugerencia que al pulsarla
    /// devuelve «no encontré una consulta» destruye la confianza en todo el asistente. La prueba
    /// <c>Cada_sugerencia_la_resuelve_el_interprete</c> lo impide.
    /// </remarks>
    public static IReadOnlyList<string> SugerenciasDe(AmbitoDeConsulta ambito) => ambito switch
    {
        AmbitoDeConsulta.Organizacion =>
        [
            "¿Qué necesita mi atención?",
            "¿Qué información me falta?",
            "¿Cómo van mis procesos?",
            "¿Cómo van mis mercados musicales?",
            "¿Dónde están mis Festivales?",
        ],
        _ =>
        [
            "¿Cómo está la plataforma hoy?",
            "¿Qué departamentos tienen más Festivales publicados?",
            "¿Cuántos mercados musicales hay publicados?",
            "¿En qué departamentos hay mercados musicales?",
            "¿Qué cobertura territorial tienen los Festivales?",
            "¿Dónde no hay Festivales publicados?",
            "¿Cuántas solicitudes necesitan atención?",
            "¿Qué información está incompleta en los Festivales?",
            "¿Cuántas Ediciones hubo el año pasado?",
            "¿Cuántos Festivales hay en revisión?",
        ],
    };

    /// <summary>
    /// La pregunta sin tildes, en minúsculas y con la puntuación intacta.
    /// </summary>
    /// <remarks>
    /// SE QUITAN LAS TILDES A PROPOSITO. Nadie escribe «distribución temática» con tilde en un
    /// buscador tres de cada cuatro veces, y una pista que solo coincide con la forma acentuada
    /// convierte el asistente en una lotería ortográfica.
    /// </remarks>
    public static string Normalizar(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                constructor.Append(char.ToLowerInvariant(caracter));
            }
        }
        return constructor.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Cómo se escribe de verdad lo que el catálogo nombra de una sola manera.
    /// </summary>
    /// <remarks>
    /// NO ES UN DICCIONARIO DE SINONIMOS, SON ABREVIATURAS. «festivales x departamento» es como se
    /// teclea en un buscador, y sin esto la pregunta no alcanza ninguna consulta. Se mantiene corto
    /// a propósito: cada entrada es una forma que alguien escribió, no una que se nos ocurrió.
    /// </remarks>
    private static readonly Dictionary<string, string> Abreviaturas = new(StringComparer.Ordinal)
    {
        ["x"] = "por",
        ["depto"] = "departamento",
        ["deptos"] = "departamentos",
        ["dpto"] = "departamento",
        ["fest"] = "festival",
        ["info"] = "informacion",
        ["orgs"] = "organizaciones",
    };

    /// <summary>
    /// Las palabras de un texto ya normalizado, con las abreviaturas resueltas.
    /// </summary>
    /// <remarks>
    /// <b>SE COMPARA POR PALABRAS Y NO POR SUBCADENAS.</b> Antes una pista se buscaba dentro del
    /// texto entero, así que «practica musical» no alcanzaba «prácticas musicales» —hay una «s» en
    /// medio— y había que declarar las dos formas a mano. Cada variante que alguien olvidara escribir
    /// era una pregunta sin respuesta.
    /// </remarks>
    public static IReadOnlyList<string> PalabrasDe(string textoNormalizado)
    {
        // SE PARTE POR «NO ES LETRA NI DIGITO» Y NO POR UNA LISTA DE SIGNOS. La primera versión
        // construía esa lista con los 128 primeros caracteres, y el castellano abre sus preguntas
        // con «¿», que está fuera de ese rango: «¿que» quedaba pegado al signo y la pregunta
        // «¿qué puedo preguntarte?» dejó de alcanzar la ayuda. Lo cazó el banco de preguntas.
        var palabras = new List<string>();
        var palabra = new StringBuilder();
        foreach (var caracter in textoNormalizado)
        {
            if (char.IsLetterOrDigit(caracter)) { palabra.Append(caracter); continue; }
            if (palabra.Length > 0) { palabras.Add(Resolver(palabra.ToString())); palabra.Clear(); }
        }
        if (palabra.Length > 0) { palabras.Add(Resolver(palabra.ToString())); }
        return palabras;
    }

    private static string Resolver(string palabra) =>
        Abreviaturas.TryGetValue(palabra, out var completa) ? completa : palabra;

    /// <summary>
    /// Si la palabra de una pista casa con una de la pregunta.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TRES FORMAS DE CASAR, Y CADA UNA RESUELVE UNA FAMILIA DE FALLOS.</b>
    /// </para>
    /// <list type="number">
    ///   <item><b>Prefijo.</b> El catálogo declara pistas que ya son raíces a propósito
    ///   —«incomplet», «pendient», «municip»—, y así siguen funcionando.</item>
    ///   <item><b>Misma raíz.</b> Quita plurales y terminaciones corrientes, que es lo que hacía
    ///   falta declarar dos veces: «práctica musical» y «prácticas musicales».</item>
    ///   <item><b>Una letra de diferencia.</b> Solo en palabras de cinco letras o más, donde una
    ///   errata no puede convertir una palabra en otra distinta: «festivles» es «festivales», pero
    ///   con tres letras «mas» y «mes» son dos palabras.</item>
    /// </list>
    /// </remarks>
    public static bool PalabraCasa(string deLaPista, string deLaPregunta)
    {
        if (deLaPregunta.StartsWith(deLaPista, StringComparison.Ordinal)) { return true; }
        if (string.Equals(Raiz(deLaPregunta), Raiz(deLaPista), StringComparison.Ordinal)) { return true; }
        return deLaPista.Length >= 5 && ADistanciaDeUna(deLaPista, deLaPregunta);
    }

    /// <summary>Las terminaciones que se quitan, de la más larga a la más corta.</summary>
    /// <remarks>
    /// <para>
    /// SE EXIGE QUE QUEDEN CUATRO LETRAS. Sin ese suelo, «mas» se quedaría en «m» y cualquier
    /// palabra corta casaría con cualquier otra.
    /// </para>
    /// <para>
    /// <b>«ADO», «ADA» Y «AR» ENTRARON EL 17 DE SEPTIEMBRE DE 2026, y hacen falta para una cosa
    /// concreta: que «publicar» y «publicado» sean la misma palabra.</b> Sin ellas, «sin publicar»
    /// —que es como se pregunta por lo que falta por publicar— no alcanzaba el estado «Publicado»,
    /// y ninguna forma de negar un estado funcionaba.
    /// </para>
    /// <para>
    /// EL SUELO DE CUATRO LETRAS ES LO QUE LAS HACE SEGURAS, y conviene ver por qué con los dos
    /// casos que más importan: «estado» mide seis y quitarle «ado» dejaría tres, así que <b>no se
    /// toca</b> —era el riesgo real, porque «estado» es una pista del catálogo—; y «lugar» o
    /// «estar» tampoco, por lo mismo. Lo que sí cambia es «mercado», que pasa a «merc» mientras
    /// «mercados» pasa a «mercad»: las dos siguen casando por prefijo, que es la primera de las
    /// tres formas de comparar.
    /// </para>
    /// </remarks>
    private static readonly string[] Terminaciones =
        ["aciones", "acion", "ciones", "cion", "idades", "idad", "mente", "ales", "eses", "es", "os", "as", "s",
         "ado", "ada", "ar"];

    private static string Raiz(string palabra)
    {
        foreach (var terminacion in Terminaciones)
        {
            if (palabra.Length - terminacion.Length >= 4 && palabra.EndsWith(terminacion, StringComparison.Ordinal))
            {
                return palabra[..^terminacion.Length];
            }
        }
        return palabra;
    }

    /// <summary>Si las dos palabras se diferencian en una sola letra: una cambiada, puesta o quitada.</summary>
    private static bool ADistanciaDeUna(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1) { return false; }
        if (string.Equals(a, b, StringComparison.Ordinal)) { return true; }

        if (a.Length == b.Length)
        {
            var diferencias = 0;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i] && ++diferencias > 1) { return false; }
            }
            return diferencias == 1;
        }

        var corta = a.Length < b.Length ? a : b;
        var larga = a.Length < b.Length ? b : a;
        var i2 = 0;
        var j = 0;
        var saltada = false;
        while (i2 < corta.Length && j < larga.Length)
        {
            if (corta[i2] == larga[j]) { i2++; j++; continue; }
            if (saltada) { return false; }
            saltada = true;
            j++;
        }
        return true;
    }
}
