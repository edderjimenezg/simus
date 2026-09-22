namespace PNMC.Contracts;

/// <summary>
/// Los contratos de la Agenda.
/// </summary>
/// <remarks>
/// <c>Situacion</c> —próximo, en curso, finalizado— LA CALCULA EL SERVIDOR y viaja con el evento.
/// Calcularla en cada pantalla daría tantas definiciones de «en curso» como pantallas, y la
/// primera que se olvidara del evento de varios días diría «finalizado» en su segunda jornada.
/// </remarks>
public sealed record EventoAgendaDto(
    long Id,
    string Slug,
    string Titulo,
    string Descripcion,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    TimeOnly? HoraInicio,
    string Modalidad,
    string? Lugar,
    string? CodigoDepartamento,
    string? NombreDepartamento,
    string? CodigoMunicipio,
    string? NombreMunicipio,
    string? Url,
    string? ImagenRuta,
    string? ImagenAlternativa,
    /// <summary>El identificador de la categoría: es el dato que se guarda y con el que se edita.</summary>
    int? CategoriaId,
    /// <summary>Su nombre, resuelto al servir. Es lo que el portal enseña y por lo que filtra.</summary>
    string? Categoria,
    string? Organizador,
    string? DescripcionLarga,
    TimeOnly? HoraFin,
    string NivelCobertura,
    int? OrdenVisualizacion,
    int? FestivalId,
    /// <summary>El archivo vinculado con rol «imagen_principal», si lo hay.</summary>
    int? ImagenArchivoId,
    string? ImagenUrl,
    string? ImagenAlt,
    string Estado,
    string Situacion,
    int Version,
    IReadOnlyList<string> Etiquetas,
    /// <summary>Las prácticas musicales con las que se clasificó. Opcional: puede venir vacía.</summary>
    IReadOnlyList<ElementoDeClasificacionDto> PracticasMusicales,
    /// <summary>Los territorios sonoros con los que se clasificó. Opcional: puede venir vacía.</summary>
    IReadOnlyList<ElementoDeClasificacionDto> TerritoriosSonoros,
    /// <summary>
    /// De dónde vino el registro: contexto, entidad que lo aportó y cuenta que lo creó.
    /// </summary>
    /// <remarks>
    /// ES ANULABLE porque los registros anteriores a la tabla de procedencia pueden no tenerla, y
    /// decir «no consta» es más honesto que inventar una.
    /// </remarks>
    ProcedenciaDeRegistroDto? Procedencia,
    /// <summary>
    /// Las iniciativas del Programa a las que pertenece: Celebra la Música y las que vengan.
    /// </summary>
    /// <remarks>
    /// NO ES SU CATEGORIA. La categoría dice de qué trata; esto dice a qué iniciativa pertenece, y
    /// son dos preguntas distintas: un evento puede ser de «Encuentros» y además de Celebra.
    /// </remarks>
    IReadOnlyList<ElementoDeClasificacionDto> ProyectosTransversales,
    DateTime FechaActualizacion);

public sealed record PaginaEventosAgendaDto(
    IReadOnlyList<EventoAgendaDto> Items,
    int Pagina,
    int Tamano,
    int Total,
    int TotalPaginas);

public sealed class GuardarEventoAgendaSolicitud
{
    public string? Slug { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public string Modalidad { get; set; } = "presencial";
    public string? Lugar { get; set; }
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? Url { get; set; }
    public string? ImagenRuta { get; set; }
    public string? ImagenAlternativa { get; set; }
    public int? CategoriaId { get; set; }
    public string? Organizador { get; set; }
    public string? DescripcionLarga { get; set; }
    public TimeOnly? HoraFin { get; set; }
    /// <summary>nacional · departamental · municipal. Vacío se deduce de los códigos.</summary>
    public string? NivelCobertura { get; set; }
    public int? OrdenVisualizacion { get; set; }
    public int? FestivalId { get; set; }
    /// <summary>
    /// El archivo del banco que hace de imagen principal.
    /// </summary>
    /// <remarks>
    /// AUSENTE Y VACIO NO SIGNIFICAN LO MISMO, y la diferencia es la que evita borrar la imagen de
    /// todos los eventos con el primer cliente que no conozca el campo. La clave ausente no toca
    /// la imagen que hubiera; un cero o un nulo explícito la retira, que es lo que espera quien
    /// vacía el campo del formulario y guarda.
    /// </remarks>
    public int? ImagenArchivoId { get; set; }

    /// <summary>
    /// Marca explícita de «quítale la imagen».
    /// </summary>
    /// <remarks>
    /// EXISTE PORQUE UN NULO NO BASTA. `ImagenArchivoId` nulo es indistinguible de «no mandé ese
    /// campo», y sin distinguirlos un cliente que guarde un subconjunto de campos le quitaría la
    /// imagen a todos los eventos. Con esta marca, retirar es una decisión que alguien tomó.
    /// </remarks>
    public bool RetirarImagen { get; set; }
    public IReadOnlyList<string>? Etiquetas { get; set; }

    /// <summary>
    /// Las prácticas musicales y los territorios sonoros con los que se clasifica el evento.
    /// </summary>
    /// <remarks>
    /// AUSENTE NO ES VACIO, igual que con la imagen: <c>null</c> deja la clasificación como estaba
    /// y una lista vacía la retira. Sin esa distinción, cualquier cliente que guardara sin conocer
    /// el campo borraría en silencio una clasificación que alguien se tomó el trabajo de hacer.
    /// </remarks>
    public IReadOnlyList<int>? PracticasMusicalesIds { get; set; }
    public IReadOnlyList<int>? TerritoriosSonorosIds { get; set; }

    /// <summary>
    /// Las iniciativas del Programa con las que se enlaza.
    /// </summary>
    /// <remarks>
    /// AUSENTE NO ES VACIO, igual que la clasificación: <c>null</c> deja los enlaces como estaban y
    /// una lista vacía los retira.
    /// </remarks>
    public IReadOnlyList<int>? ProyectosTransversalesIds { get; set; }

    public int Version { get; set; }
}

public sealed class CambiarEstadoEventoAgendaSolicitud
{
    public string Estado { get; set; } = string.Empty;
}

/// <summary>
/// Las reglas de la Agenda, en un sitio comprobable sin base de datos.
/// </summary>
public static class ReglasDeAgenda
{
    public static readonly IReadOnlyList<string> Estados = ["borrador", "en_revision", "publicado", "archivado"];
    public static readonly IReadOnlyList<string> Modalidades = ["presencial", "virtual", "mixta"];

    /// <summary>
    /// Qué le falta a un evento para poder publicarse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LA MODALIDAD DECIDE QUE ES OBLIGATORIO. Un presencial sin lugar no se puede anunciar —nadie
    /// sabría a dónde ir— y un virtual sin enlace tampoco. Un mixto necesita los dos: quien elige
    /// «mixta» está prometiendo ambas formas de asistir.
    /// </para>
    /// <para>
    /// DEVUELVE TODO LO QUE FALTA, no lo primero: quien organiza prefiere una lista a tres intentos.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> LoQueFaltaParaPublicar(
        string? titulo, string? descripcion, string modalidad, string? lugar, string? url)
    {
        var faltas = new List<string>();
        if (string.IsNullOrWhiteSpace(titulo)) faltas.Add("no tiene título");
        if (string.IsNullOrWhiteSpace(descripcion)) faltas.Add("no tiene descripción");

        var necesitaLugar = modalidad is "presencial" or "mixta";
        var necesitaEnlace = modalidad is "virtual" or "mixta";

        if (necesitaLugar && string.IsNullOrWhiteSpace(lugar)) faltas.Add("es presencial y no dice dónde");
        if (necesitaEnlace && string.IsNullOrWhiteSpace(url)) faltas.Add("es virtual y no tiene enlace");
        return faltas;
    }

    /// <summary>
    /// En qué punto está un evento respecto de hoy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL EVENTO DE VARIOS DIAS ESTA «EN CURSO» TODOS ELLOS, y por eso se compara contra la fecha
    /// de fin y no solo contra la de inicio. Un festival de una semana que apareciera como
    /// finalizado en su segunda jornada es el fallo que esta función existe para evitar.
    /// </para>
    /// <para>
    /// UNA FECHA DE FIN NULA SIGNIFICA «el mismo día», que es lo que espera quien anuncia un
    /// concierto de una tarde y no rellena el segundo campo.
    /// </para>
    /// </remarks>
    public static string SituacionDe(DateOnly inicio, DateOnly? fin, DateOnly hoy)
    {
        var ultimo = fin ?? inicio;
        if (hoy < inicio) return "proximo";
        if (hoy <= ultimo) return "en_curso";
        return "finalizado";
    }

    /// <summary>
    /// Si un evento se ve en el portal.
    /// </summary>
    /// <remarks>
    /// NO HAY CORTE POR FECHA, a diferencia de Noticias. Lo que ya ocurrió sigue siendo información
    /// pública: el portal lo presenta como finalizado en vez de esconderlo, porque quien busca «qué
    /// hubo el año pasado en el Cauca» tiene tanto derecho a encontrarlo como quien busca qué viene.
    /// </remarks>
    public static bool EsVisiblePublicamente(string estado) => estado == "publicado";

    /// <summary>La dirección pública a partir del título. Se calcula una vez, al crear.</summary>
    public static string SlugDesde(string titulo) => ReglasDeNoticias.SlugDesde(titulo);

    public static readonly IReadOnlyList<string> NivelesDeCobertura = ["nacional", "departamental", "municipal"];

    /// <summary>
    /// El nivel de cobertura que corresponde a unos códigos territoriales.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SE DEDUCE DE LOS CODIGOS Y NO SE PIDE APARTE, porque son la misma información dicha dos
    /// veces y dos formas de decir lo mismo acaban discrepando. La base lo impone con
    /// <c>CK_EventosAgenda_NivelCobertura</c>: nacional sin códigos, departamental con
    /// departamento, municipal con los dos.
    /// </para>
    /// <para>
    /// UN MUNICIPIO SIN DEPARTAMENTO NO ES NADA, así que se ignora: el nivel lo manda el dato más
    /// completo que esté bien formado.
    /// </para>
    /// </remarks>
    public static string NivelQueCorresponde(string? codigoDepartamento, string? codigoMunicipio)
    {
        var departamento = (codigoDepartamento ?? string.Empty).Trim();
        var municipio = (codigoMunicipio ?? string.Empty).Trim();

        if (departamento.Length == 0) return "nacional";
        return municipio.Length == 0 ? "departamental" : "municipal";
    }
}
