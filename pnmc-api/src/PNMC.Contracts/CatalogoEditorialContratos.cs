namespace PNMC.Contracts;

/// <summary>
/// Vocabulario estable del contrato inicial del Catálogo Editorial.
/// Los vocabularios descriptivos —tipos de publicación, prácticas, líneas y programas—
/// se administrarán como datos y no se fijan aquí sin una fuente institucional aprobada.
/// </summary>
public static class CatalogoEditorialContrato
{
    public const int Version = 1;

    public static readonly IReadOnlyList<string> EstadosCatalogacion =
    [
        "pendiente_revision",
        "en_revision",
        "validada",
        "observada",
    ];

    public static readonly IReadOnlyList<string> EstadosPublicacion =
    [
        "borrador",
        "en_revision",
        "publicado",
        "retirado",
    ];

    public static readonly IReadOnlyList<string> TiposAgente = ["persona", "entidad"];
    public static readonly IReadOnlyList<string> EsquemasIdentificador = ["ISBN", "ISMN"];
    public static readonly IReadOnlyList<string> TiposAcceso = ["archivo", "enlace", "ubicacion"];
    public static readonly IReadOnlyList<string> EstadosDerechos = ["pendiente", "verificado", "restringido"];

    /// <summary>
    /// Un acceso tiene exactamente un destino y ese destino corresponde con su tipo.
    /// </summary>
    public static bool TieneDestinoValido(AccesoEditorialSolicitud acceso)
    {
        var conArchivo = acceso.ArchivoId.HasValue;
        var conEnlace = !string.IsNullOrWhiteSpace(acceso.Url);
        var conUbicacion = !string.IsNullOrWhiteSpace(acceso.UbicacionFisica);

        return acceso.Tipo switch
        {
            "archivo" => conArchivo && !conEnlace && !conUbicacion,
            "enlace" => !conArchivo && conEnlace && !conUbicacion,
            "ubicacion" => !conArchivo && !conEnlace && conUbicacion,
            _ => false,
        };
    }
}

/// <summary>Fuente verificable que respalda uno o varios datos de la ficha.</summary>
public sealed record FuenteEditorialDto(
    long Id,
    string Nombre,
    string? Referencia,
    string? Url,
    DateTime? FechaFuente,
    DateTime FechaConsulta,
    string VerificadaPor);

/// <summary>Decisión de derechos separada de la calidad de la catalogación.</summary>
public sealed record DerechosEditorialesDto(
    string Estado,
    bool PermitePublicarFicha,
    bool PermitePublicarArchivo,
    string? LicenciaONota,
    long? FuenteId,
    DateTime? FechaVerificacion,
    string? VerificadoPor);

public sealed record AgenteEditorialDto(
    long Id,
    string Codigo,
    string Tipo,
    string NombrePreferido,
    string? FormaNormalizada,
    string? Seudonimo,
    string? Acronimo,
    bool RequiereRevision);

public sealed record CreditoEditorialDto(
    long Id,
    long AgenteId,
    string AgenteNombre,
    /// <summary>Persona o Entidad. El diseño aprobado separa «Autor» de «Autor corporativo».</summary>
    string AgenteTipo,
    string RolCodigo,
    string RolEtiqueta,
    bool Principal,
    int Orden);

public sealed record IdentificadorEditorialDto(
    long Id,
    string Esquema,
    string CodigoRecibido,
    string? Cualificador,
    bool? Valido,
    string? ObservacionValidacion);

public sealed record AccesoEditorialDto(
    long Id,
    string Tipo,
    long? ArchivoId,
    string? Url,
    string? UbicacionFisica,
    string? Etiqueta,
    string? Nota,
    int Orden,
    DerechosEditorialesDto Derechos);

/// <summary>
/// Un término de tipología: de qué está hecha la publicación, según las cuatro facetas del
/// documento de estándares —recurso (DCMI), contenido (RDA 336), medio (RDA 337), soporte (RDA 338)—.
/// </summary>
/// <remarks>
/// VIAJA CON SU NORMA. Una ficha bibliográfica que dice «disco de audio» sin decir de qué lista sale
/// no es citable, y el catálogo se construyó precisamente para serlo.
/// </remarks>
public sealed record TipologiaEditorialDto(string Eje, string Codigo, string Etiqueta, string Norma);

public sealed record ProgramaEditorialDto(long Id, string Codigo, string Nombre, bool Activo);

public sealed record RelacionProgramaEditorialDto(
    long Id,
    ProgramaEditorialDto Programa,
    long FuenteId,
    DateOnly? VigenteDesde,
    DateOnly? VigenteHasta,
    string VerificadaPor);

public sealed record PublicacionEditorialDto(
    long Id,
    string Codigo,
    string Titulo,
    string? Subtitulo,
    string? DesignacionVolumen,
    string? SerieOColeccion,
    string? Resumen,
    string? FechaEdtf,
    int? AnioInicio,
    int? AnioFin,
    string? Idioma,
    /// <summary>Lo que la fuente escribió cuando no pudo fijar la fecha.</summary>
    string? NotaFecha,
    // Descriptores de presentación. Texto libre: sus vocabularios siguen TBC ().
    string? TipoPublicacion,
    int? CategoriaId,
    string? Categoria,
    string? Ambito,
    /// <summary>El ámbito escrito a mano cuando no entra en el vocabulario.</summary>
    string? AmbitoTexto,
    /// <summary>Físico, Digital o Mixto. Dato propio: NO se deduce del soporte.</summary>
    string? Formato,
    /// <summary>La segunda práctica, cuando la obra toca dos.</summary>
    string? CategoriaSecundaria,
    IReadOnlyList<string> PalabrasClave,
    string? MiniaturaRuta,
    string? SeccionPrincipal,
    string? RutaSeccion,
    string? PracticaMusical,
    string? Subcategoria,
    string? TamanoFormato,
    string? Paginas,
    string? Duracion,
    string? CamposAdicionales,
    string? TextoPortada,
    /// <summary>Las cuatro facetas RDA/DCMI. Multivaluada: un libro con CD declara dos soportes.</summary>
    IReadOnlyList<TipologiaEditorialDto> Tipologia,
    /// <summary>Cuánta confianza da la ficha de origen. Ordena la cola de catalogación.</summary>
    string? Confianza,
    bool RevisarClasificacion,
    bool RevisarCreditos,
    /// <summary>La nota de quien catalogó.</summary>
    string? NotasCatalogacion,
    /// <summary>De qué lámina del catálogo original salió la ficha.</summary>
    string? DiapositivaOrigen,
    string EstadoCatalogacion,
    string EstadoPublicacion,
    int Version,
    DerechosEditorialesDto Derechos,
    IReadOnlyList<FuenteEditorialDto> Fuentes,
    IReadOnlyList<CreditoEditorialDto> Creditos,
    IReadOnlyList<IdentificadorEditorialDto> Identificadores,
    IReadOnlyList<AccesoEditorialDto> Accesos,
    IReadOnlyList<RelacionProgramaEditorialDto> Programas,
    /// <summary>Las prácticas musicales con las que se clasificó. Opcional: puede venir vacía.</summary>
    IReadOnlyList<ElementoDeClasificacionDto> PracticasMusicales,
    /// <summary>Los territorios sonoros con los que se clasificó. Opcional: puede venir vacía.</summary>
    IReadOnlyList<ElementoDeClasificacionDto> TerritoriosSonoros,
    /// <summary>De dónde vino la ficha. Anulable: lo anterior a la tabla puede no tenerla.</summary>
    ProcedenciaDeRegistroDto? Procedencia,
    DateTime FechaActualizacion);

public sealed class GuardarPublicacionEditorialSolicitud
{
    public string? Codigo { get; set; }
    public string? Titulo { get; set; }
    public string? Subtitulo { get; set; }
    public string? DesignacionVolumen { get; set; }
    public string? SerieOColeccion { get; set; }
    public string? Resumen { get; set; }
    public string? FechaEdtf { get; set; }
    public int? AnioInicio { get; set; }
    public int? AnioFin { get; set; }
    public string? Idioma { get; set; }
    public string? TipoPublicacion { get; set; }
    public int? CategoriaId { get; set; }
    public string? Ambito { get; set; }
    public IReadOnlyList<string>? PalabrasClave { get; set; }
    public string? MiniaturaRuta { get; set; }
    public string? SeccionPrincipal { get; set; }
    public string? RutaSeccion { get; set; }
    public string? PracticaMusical { get; set; }
    public string? Subcategoria { get; set; }
    public string? TamanoFormato { get; set; }
    public string? Paginas { get; set; }
    public string? Duracion { get; set; }
    public string? CamposAdicionales { get; set; }
    public string? TextoPortada { get; set; }

    /*
     * LO QUE EL FORMULARIO NO PODIA GUARDAR AUNQUE LA FICHA LO ENSEÑE.
     *
     * Estos cuatro campos existen en la tabla y viajan en el DTO de lectura, pero no había forma de
     * escribirlos desde la consola: se llenaron en la carga del acervo y quedaron congelados. Una
     * ficha nueva nacía sin formato, sin nota sobre la fecha y sin ámbito en letra, y nadie podía
     * corregir los del acervo.
     */
    public string? Formato { get; set; }
    public string? NotaFecha { get; set; }
    public string? CategoriaSecundaria { get; set; }
    public string? AmbitoTexto { get; set; }

    /*
     * Y LO QUE NO ERA UN CAMPO SINO UNA LISTA. Créditos, identificadores, accesos y tipología son lo
     * que distingue una ficha catalográfica de un formulario cualquiera —la autoría con su papel,
     * los ISBN con su cualificador, las vías de consulta, las cuatro facetas—, y no había manera de
     * editarlos. Se cargaron con el acervo y punto.
     *
     * AUSENTE NO ES VACIO, la misma regla que las fuentes y las palabras clave: `null` deja la lista
     * como estaba y una lista vacía la retira. Sin esa distinción, guardar un cambio de título
     * borraría los créditos de la ficha.
     */
    public IReadOnlyList<CreditoEditorialSolicitud>? Creditos { get; set; }
    public IReadOnlyList<IdentificadorEditorialSolicitud>? Identificadores { get; set; }
    public IReadOnlyList<AccesoEditorialSolicitud>? Accesos { get; set; }
    /** Los códigos de tipología, uno por faceta: `texto`, `audio`, `volumen`… */
    public IReadOnlyList<string>? Tipologias { get; set; }

    public string? EstadoCatalogacion { get; set; }
    public int Version { get; set; }
    public IReadOnlyList<long>? FuenteIds { get; set; }

    /// <summary>
    /// Las prácticas musicales y los territorios sonoros con los que se clasifica la publicación.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUSENTE NO ES VACIO: <c>null</c> deja la clasificación como estaba y una lista vacía la
    /// retira. Es la misma regla que aplican las fuentes y las palabras clave.
    /// </para>
    /// <para>
    /// NO SUSTITUYE A <c>PracticaMusical</c>, el texto suelto que llega del acervo heredado. Ese
    /// campo transcribe lo que dijo la fuente; esto vincula con el catálogo del sistema, y
    /// mezclarlos perdería la transcripción original.
    /// </para>
    /// </remarks>
    public IReadOnlyList<int>? PracticasMusicalesIds { get; set; }
    public IReadOnlyList<int>? TerritoriosSonorosIds { get; set; }
}

/// <summary>Un crédito tal como lo escribe quien cataloga: a quién y en qué papel.</summary>
/// <remarks>
/// EL AGENTE SE IDENTIFICA POR NOMBRE Y TIPO, no por identificador. Quien cataloga escribe «Ibis
/// Amador Martelo», no el número 137. El servidor busca el agente por su nombre normalizado y lo
/// crea si no existe, que es como funciona un fichero de autoridades.
/// </remarks>
/// <summary>Un valor de una lista, con cuántas fichas lo usan ya.</summary>
/// <remarks>
/// EL RECUENTO NO ES ADORNO: ordena la lista por lo que de verdad se usa y deja a la vista las
/// variantes que sobran. Con 57 «tipos de publicación» distintos para 171 fichas, ver que «Libro»
/// tiene 12 y «Libro impreso» 14 es lo que permite decidir cuál es el término bueno.
/// </remarks>
public sealed record TerminoDeVocabularioDto(string Valor, int Usos);

/// <summary>
/// Las listas con las que se rellena el formulario de una publicación.
/// </summary>
/// <remarks>
/// <para>
/// SALEN DE LAS PROPIAS TABLAS, NO DE UNA LISTA ESCRITA A MANO. Es la misma regla que rige DIVIPOLA:
/// la fuente es el dato del proyecto. Una lista fija en el código se desincroniza del acervo en
/// cuanto alguien cataloga algo nuevo, y entonces el formulario impide escribir lo que la ficha de
/// al lado ya dice.
/// </para>
/// <para>
/// POR QUE HACEN FALTA. El formulario capturaba idioma, tipo de publicación y ámbito como TEXTO
/// LIBRE, y el resultado se puede contar: el idioma guarda `es`, pero también `es ; (lengua nativa)`
/// y `es ; en`; el tipo de publicación tiene más de cincuenta valores para 171 fichas, con
/// «Libro», «Libro impreso» y «Libro Cuaderno de ejercicios» significando casi lo mismo. Con esos
/// datos no se puede filtrar, ni agrupar, ni contar. Un desplegable lo impide al escribir.
/// </para>
/// </remarks>
public sealed record VocabulariosEditorialesDto(
    /// <summary>El código que le tocaría a la siguiente publicación, por orden de registro.</summary>
    string SiguienteCodigo,
    /// <summary>
    /// Los idiomas, uno por uno, para elegir varios.
    /// </summary>
    /// <remarks>
    /// NO SON «LOS VALORES QUE HAY ESCRITOS». El campo guarda COMBINACIONES —`es`, `es ; en`,
    /// `es ; (lengua nativa)`—, así que listarlas tal cual ponía en el desplegable tres opciones que
    /// parecían tres españoles distintos. Lo señaló la dirección de producto. Aquí viaja el vocabulario
    /// de idiomas, y la combinación la arma el formulario marcando los que correspondan.
    /// </remarks>
    IReadOnlyList<IdiomaEditorialDto> Idiomas,
    IReadOnlyList<TerminoDeVocabularioDto> TiposDePublicacion,
    IReadOnlyList<TerminoDeVocabularioDto> Ambitos,
    IReadOnlyList<TerminoDeVocabularioDto> Formatos,
    /// <summary>
    /// Dónde vive cada publicación dentro del acervo: la ruta completa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ES UNA TAXONOMIA PROPIA Y CERRADA, no la suma de las categorías. Medido sobre el acervo: hay
    /// <b>22 rutas distintas</b> para 9 secciones, con tramos como «Repertorio &gt; Prácticas vocales
    /// &gt; Cancioneros &gt; Palabra ritmada &gt; Coro». La subcategoría aparece en la ruta en 17 de
    /// 170 fichas y la categoría secundaria en <b>0</b>: no se puede calcular una de otra.
    /// </para>
    /// <para>
    /// LA SECCION SI SALE DE LA RUTA, y en las 170. El primer tramo de la ruta coincide con
    /// `SeccionPrincipal` en TODAS. Por eso se elige la ruta y la sección se deriva, y no al revés:
    /// pedir las dos por separado deja que discrepen.
    /// </para>
    /// </remarks>
    IReadOnlyList<RutaDelAcervoDto> Rutas,
    IReadOnlyList<TerminoDeVocabularioDto> PracticasMusicales,
    IReadOnlyList<TerminoDeVocabularioDto> Subcategorias,
    IReadOnlyList<TerminoDeVocabularioDto> CategoriasSecundarias,
    IReadOnlyList<TerminoDeVocabularioDto> EsquemasIdentificador,
    IReadOnlyList<RolEditorialDto> RolesDeCredito,
    IReadOnlyList<TipologiaEditorialDto> Tipologias,
    IReadOnlyList<string> TiposDeAgente);

/// <summary>Un papel de crédito: su código MARC y cómo se lee.</summary>
public sealed record RolEditorialDto(string Codigo, string Etiqueta, int Usos);

/// <summary>Un valor propuesto a partir de un documento, con el porqué de la propuesta.</summary>
/// <param name="Origen">
/// De dónde salió: `metadatos`, `portada`, `texto`, `acervo`. <b>Viaja siempre</b>, porque quien
/// cataloga necesita saber si un título lo escribió quien generó el PDF o lo dedujo el sistema del
/// tamaño de la letra: la confianza que merece cada uno no es la misma.
/// </param>
public sealed record ValorPropuestoDto(string Valor, string Origen, string? Nota = null);

/// <summary>
/// Lo que un documento propone para una ficha nueva.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES UNA PROPUESTA Y NO UNA FICHA.</b> No se guarda nada: se devuelve para que el formulario de
/// alta se abra con estos valores puestos y quien cataloga acepte, corrija o borre cada uno. Es la
/// restricción del proyecto sobre la importación asistida, cumplida por construcción —este endpoint
/// no escribe en ninguna tabla—.
/// </para>
/// <para>
/// <b>LO QUE NO SE SABE SE DEJA VACIO.</b> Un campo propuesto a la ligera es peor que uno vacío: el
/// vacío se rellena y el propuesto se acepta sin mirar.
/// </para>
/// </remarks>
/// <summary>Un lugar reconocido en el documento, ya resuelto contra DIVIPOLA.</summary>
public sealed record LugarPropuestoDto(
    string Municipio,
    string CodigoMunicipio,
    string Departamento,
    string CodigoDepartamento);

/// <summary>Una fecha anunciada en el documento.</summary>
public sealed record FechaPropuestaDto(string Inicio, string? Fin, string Origen);

/// <summary>
/// Lo que un afiche o un volante proponen para un evento de la Agenda.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES LA MISMA TUBERIA QUE LA DEL CATALOGO, CON OTRA SALIDA.</b> Un afiche y una publicación son
/// documentos distintos, pero lo que hay que hacer con ellos es lo mismo: leer el texto, reconocer lo
/// que el sistema ya conoce y proponer. Lo que cambia es qué se reconoce —aquí fechas y lugares, allí
/// identificadores y autoría— y en qué formulario desemboca.
/// </para>
/// <para>
/// <b>EL LUGAR SE RESUELVE CONTRA DIVIPOLA, no se propone como texto.</b> Un afiche que dice «Ibagué»
/// vale poco si llega como una cadena; resuelto contra el catálogo territorial del proyecto llega con
/// su código de municipio y su departamento, que es lo que el evento necesita para salir en el mapa.
/// </para>
/// <para>
/// <b>Y NACE EN BORRADOR, SIEMPRE.</b> Igual que en el Catálogo Editorial: lo obtenido de un documento
/// se propone, una persona lo revisa y lo aprueba. Este endpoint no crea ningún evento.
/// </para>
/// </remarks>
public sealed record PropuestaDeEventoDto(
    IReadOnlyList<ValorPropuestoDto> Titulos,
    IReadOnlyList<FechaPropuestaDto> Fechas,
    IReadOnlyList<LugarPropuestoDto> Lugares,
    IReadOnlyList<ValorPropuestoDto> Organizaciones,
    int? AficheArchivoId,
    string? AficheUrl,
    bool RequiereOcr,
    /// <summary>
    /// Lo propuesto salió de MIRAR la página, no de su capa de texto.
    /// </summary>
    /// <remarks>
    /// SE DICE EN PANTALLA. Un reconocimiento óptico acierta mucho pero no siempre, y quien cataloga
    /// tiene derecho a saber con qué fiabilidad le llega cada propuesta —es el mismo principio que
    /// obliga a guardar la procedencia de todo registro—. Presentar como equivalente un título
    /// extraído del PDF y uno leído de una imagen sería esconder una diferencia que importa.
    /// </remarks>
    bool LeidoConReconocimientoOptico,
    IReadOnlyList<string> LoQueSePudoLeer);

public sealed record PropuestaDeFichaDto(
    IReadOnlyList<ValorPropuestoDto> Titulos,
    IReadOnlyList<ValorPropuestoDto> Subtitulos,
    IReadOnlyList<ValorPropuestoDto> Identificadores,
    IReadOnlyList<ValorPropuestoDto> Anios,
    IReadOnlyList<ValorPropuestoDto> Agentes,
    IReadOnlyList<ValorPropuestoDto> Rutas,
    IReadOnlyList<ValorPropuestoDto> Practicas,
    IReadOnlyList<ValorPropuestoDto> TiposDePublicacion,
    /// <summary>El resumen, cuando el documento lo rotula. Vacío si no lo hace: no se inventa.</summary>
    string? Resumen,
    string? Paginas,
    /// <summary>El identificador del archivo del banco donde quedó la portada renderizada.</summary>
    int? PortadaArchivoId,
    string? PortadaUrl,
    /// <summary>La primera página no tenía texto y el reconocimiento óptico tampoco pudo con ella.</summary>
    bool RequiereOcr,
    /// <summary>
    /// Lo propuesto salió de MIRAR la página, no de su capa de texto.
    /// </summary>
    /// <remarks>
    /// SE DICE EN PANTALLA. Un reconocimiento óptico acierta mucho pero no siempre, y quien cataloga
    /// tiene derecho a saber con qué fiabilidad le llega cada propuesta —es el mismo principio que
    /// obliga a guardar la procedencia de todo registro—. Presentar como equivalente un título
    /// extraído del PDF y uno leído de una imagen sería esconder una diferencia que importa.
    /// </remarks>
    bool LeidoConReconocimientoOptico,
    /// <summary>Qué capas dieron algo, para poder decirlo en pantalla sin adivinar.</summary>
    IReadOnlyList<string> LoQueSePudoLeer);

/// <summary>Un idioma: su código ISO 639 y cómo se lee.</summary>
/// <remarks>
/// `(lengua nativa)` NO ES UN CODIGO ISO Y AQUI VIAJA IGUAL. Es lo que escribió la fuente en cinco
/// fichas —publicaciones bilingües en español y una lengua indígena que el volcado no identifica—, y
/// es información real: decir que la obra es bilingüe vale más que callarlo por no tener el código.
/// Se marca como pendiente de precisar en vez de inventarle un idioma.
/// </remarks>
/// <summary>Un sitio del acervo: su ruta completa, la sección que la encabeza y cuántas fichas hay.</summary>
public sealed record RutaDelAcervoDto(string Ruta, string Seccion, int Usos);

public sealed record IdiomaEditorialDto(string Codigo, string Nombre, int Usos, bool PendienteDePrecisar);

public sealed class CreditoEditorialSolicitud
{
    public string? Nombre { get; set; }
    /// <summary>`persona` o `entidad`, en minúscula, como lo fija el CHECK de la tabla.</summary>
    public string? Tipo { get; set; }
    /// <summary>El código MARC Relators: `aut`, `cmp`, `arr`…</summary>
    public string? RolCodigo { get; set; }
    /// <summary>Cómo se lee el papel. Un mismo código carga etiquetas distintas según la obra.</summary>
    public string? RolEtiqueta { get; set; }
    public bool Principal { get; set; }
    public int Orden { get; set; }
}

public sealed class IdentificadorEditorialSolicitud
{
    /// <summary>`ISBN` o `ISMN`.</summary>
    public string? Esquema { get; set; }
    public string? Codigo { get; set; }
    /// <summary>Qué distingue este identificador de los otros de la misma obra: «PDF», «EPUB».</summary>
    public string? Cualificador { get; set; }
    public int Orden { get; set; }
}

public sealed class AccesoEditorialSolicitud
{
    public string? Tipo { get; set; }
    public long? ArchivoId { get; set; }
    public string? Url { get; set; }
    public string? UbicacionFisica { get; set; }
    public string? Etiqueta { get; set; }
    public string? Nota { get; set; }
    public int Orden { get; set; }
}

public sealed record PaginaPublicacionesEditorialesDto(
    IReadOnlyList<PublicacionEditorialDto> Items,
    int Pagina,
    int Tamano,
    int Total,
    int TotalPaginas);

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// LO QUE SE PUBLICA, QUE NO ES LO MISMO QUE LO QUE SE GUARDA
// ═══════════════════════════════════════════════════════════════════════════════════════════════
//
// EL CRITERIO ES EL DE UN CATALOGO DE BIBLIOTECA. Lo fijó la dirección de producto // de 2026: «revisa todos los datos que conviene dejar públicos como se suele publicar en
// bibliotecas, páginas de venta de libros, hemerotecas». Un OPAC, una librería y una hemeroteca
// publican exactamente lo que sirve para IDENTIFICAR, ENCONTRAR y CONSEGUIR una obra —quién la
// hizo, cómo se llama, de qué está hecha, cuánto mide, qué identificadores tiene, de qué trata,
// dónde se consulta y bajo qué licencia— y no publican NADA del trabajo de catalogarla.
//
// QUE SE QUEDA FUERA, Y POR QUE CADA COSA:
//
//   · `estadoCatalogacion`, `estadoPublicacion`, `version` — el flujo de trabajo interno. Que una
//     ficha esté «validada» es una afirmación del equipo sobre sí mismo, no un dato de la obra.
//   · `confianza`, `revisarClasificacion`, `revisarCreditos`, `notasCatalogacion` — la valoración
//     que el catalogador hace de su propia ficha. Publicar «Confianza: Media» invita a dudar de un
//     dato sin dar a nadie con qué resolver la duda.
//   · `diapositivaOrigen` y `procedencia` — de qué lámina y de qué carga salió el registro. Es
//     trazabilidad administrativa: importa para responder «quién metió esto», no para consultar.
//   · `derechos.verificadoPor`, `derechos.fechaVerificacion`, `derechos.fuenteId` y la lista de
//     `fuentes` con su `verificadaPor` — llevan NOMBRES DE FUNCIONARIOS. Una biblioteca publica la
//     licencia de la obra, no el nombre de quien la comprobó.
//   · `identificador.valido` y su observación — el resultado de validar el dígito de control. Es
//     control de calidad del catálogo; el ISBN sí se publica, el veredicto sobre él no.
//   · el identificador numérico interno — el público navega por `codigo`, que es la signatura.
//
// SE PUBLICAN LOS DERECHOS COMO LOS PUBLICA UNA BIBLIOTECA: la licencia o la nota, que es lo que
// dice qué puede hacer quien consulta. Las banderas que deciden si la ficha sale ya hicieron su
// trabajo antes de llegar aquí; repetirlas en la respuesta no le sirve a nadie de fuera.
//
// ES UN CONTRATO APARTE Y NO UN FILTRO SOBRE EL DE LA CONSOLA. Un filtro se olvida: basta añadir un
// campo al DTO interno para que salga al portal sin que nadie lo decida, que es exactamente lo que
// había pasado —la ruta pública devolvía el DTO de la consola entero—. Con dos contratos, publicar
// un campo nuevo es un acto deliberado: hay que escribirlo aquí.

/// <summary>Un crédito tal como lo publica un catálogo: quién, y en qué papel.</summary>
public sealed record CreditoPublicoDto(
    long AgenteId,
    string Nombre,
    /// <summary>Persona o Entidad: un catálogo distingue autor de autor corporativo.</summary>
    string Tipo,
    /// <summary>El código MARC Relators, que es lo que hace citable el papel.</summary>
    string RolCodigo,
    string Rol,
    bool Principal);

/// <summary>Un identificador normalizado. Sin el veredicto de validación, que es interno.</summary>
public sealed record IdentificadorPublicoDto(string Esquema, string Codigo, string? Cualificador);

/// <summary>Dónde se consigue la obra: el enlace, o dónde se conserva el ejemplar.</summary>
public sealed record AccesoPublicoDto(string Tipo, string? Url, string? UbicacionFisica, string? Etiqueta, string? Nota);

/// <summary>
/// La ficha tal como la publica el catálogo. Ver la nota de arriba para el criterio.
/// </summary>
public sealed record PublicacionEditorialPublicaDto(
    // ── Identidad ──
    /// <summary>La signatura: es como se cita y como se enlaza.</summary>
    string Codigo,
    string Titulo,
    string? Subtitulo,
    string? DesignacionVolumen,
    string? SerieOColeccion,
    string? Resumen,
    // ── Fecha ──
    string? FechaEdtf,
    int? AnioInicio,
    int? AnioFin,
    string? NotaFecha,
    string? Idioma,
    // ── De qué está hecha ──
    string? TipoPublicacion,
    string? Formato,
    IReadOnlyList<TipologiaEditorialDto> Tipologia,
    // ── Descripción física ──
    string? TamanoFormato,
    string? Paginas,
    string? Duracion,
    // ── Materia ──
    string? Categoria,
    string? CategoriaSecundaria,
    string? PracticaMusical,
    string? Subcategoria,
    IReadOnlyList<string> PalabrasClave,
    // ── Cobertura ──
    string? Ambito,
    string? AmbitoTexto,
    // ── Dónde vive en el catálogo ──
    string? SeccionPrincipal,
    string? RutaSeccion,
    // ── Cómo se ve ──
    string? MiniaturaRuta,
    string? TextoPortada,
    // ── Quién la hizo, qué identificadores tiene y dónde se consigue ──
    IReadOnlyList<CreditoPublicoDto> Creditos,
    IReadOnlyList<IdentificadorPublicoDto> Identificadores,
    IReadOnlyList<AccesoPublicoDto> Accesos,
    /// <summary>La licencia o la nota de derechos. Sin quién la verificó ni cuándo.</summary>
    string? Licencia,
    // ── Clasificación contra los catálogos del sistema ──
    IReadOnlyList<ElementoDeClasificacionDto> PracticasMusicales,
    IReadOnlyList<ElementoDeClasificacionDto> TerritoriosSonoros,
    DateTime FechaActualizacion);

public sealed record PaginaPublicacionesEditorialesPublicasDto(
    IReadOnlyList<PublicacionEditorialPublicaDto> Items,
    int Pagina,
    int Tamano,
    int Total,
    int TotalPaginas);

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// EL HILO DE TRABAJO DE UNA FICHA
// ═══════════════════════════════════════════════════════════════════════════════════════════════
//
// DECISIONES Y ANOTACIONES EN UN SOLO ORDEN. No hay una tabla de comentarios aparte: una anotación
// es una entrada más del hilo de revisión con la acción `Anotacion` y el estado sin moverse, así
// que lo que alguien escribió y la decisión que comenta se leen juntos y en el mismo sitio. Es
// además el hilo que ya usa el circuito de Festivales, `dbo.RegistrosRevisionHistorial`.
//
// ESTO NO SALE AL PORTAL. Es exactamente lo que el criterio de biblioteca deja dentro: el trabajo
// de catalogar. Ver `PublicacionEditorialPublicaDto`.

/// <summary>Una entrada del hilo: una decisión sobre la ficha, o una anotación sobre ella.</summary>
public sealed record AnotacionEditorialDto(
    long Id,
    /// <summary>`CatalogacionCambiada`, `PublicacionCambiada` o `Anotacion`.</summary>
    string Accion,
    string? EstadoAnterior,
    string EstadoNuevo,
    string? Comentario,
    int? UsuarioId,
    /// <summary>Quién, en letra. Un historial que dice «usuario 12» obliga a ir a buscar quién es.</summary>
    string? UsuarioNombre,
    DateTime Fecha);

public sealed class AnotacionEditorialSolicitud
{
    public string? Comentario { get; set; }
}
