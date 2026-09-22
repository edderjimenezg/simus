namespace PNMC.Contracts;

/// <summary>
/// Lo que la consola envía para dar de alta una organización.
/// </summary>
/// <remarks>
/// <para>
/// <b>NO ES UN FORMULARIO DISTINTO DEL EXTERNO: ES EL MISMO SIN LA CUENTA.</b> El alta externa
/// crea a la vez la organización y el usuario que la administrará, porque quien la registra es
/// esa persona. La consola registra una organización que todavía <b>no tiene cuenta</b>: el
/// funcionario deja su ficha y sus datos de responsable, y cuando la organización llegue al
/// sistema reclamará su administración por el circuito que ya existe.
/// </para>
/// <para>
/// POR ESO NO HAY CONTRASEÑA NI CORREO DE ACCESO. Pedirlos obligaría al funcionario a inventar
/// una credencial para una persona que no está delante.
/// </para>
/// </remarks>
public sealed class CrearOrganizacionAdministrativaSolicitud
{
    public string Nombre { get; set; } = string.Empty;
    public string? Identificacion { get; set; }
    public string? CorreoContacto { get; set; }
    public string? CodigoDepartamentoSede { get; set; }
    public string? CodigoMunicipioSede { get; set; }

    /// <summary>Quién responde por la organización. Se guarda aunque no tenga cuenta todavía.</summary>
    public string ResponsableNombre { get; set; } = string.Empty;
    public string? ResponsableTipoDocumento { get; set; }
    public string? ResponsableNumeroDocumento { get; set; }
    public string? ResponsableCorreo { get; set; }
    public string? ResponsableTelefono { get; set; }

    /// <summary>
    /// Ley 1581 de 2012: la autorización del titular se guarda, no se presume.
    /// </summary>
    /// <remarks>
    /// SE PIDE TAMBIÉN EN EL ALTA ADMINISTRATIVA. Que el dato lo teclee un funcionario no cambia
    /// que sea un dato personal de otra persona: si nadie autorizó, la ficha lo dice.
    /// </remarks>
    public bool ResponsableAutorizacionDatos { get; set; }
}

/// <summary>
/// Lo que la consola envía para dar de alta un Festival.
/// </summary>
/// <remarks>
/// <b>REUTILIZA EL MISMO CUERPO QUE EL ASISTENTE EXTERNO</b> y le añade una sola cosa: a qué
/// organización pertenece. En el canal externo eso no hace falta —es la de la sesión—; en la
/// consola es una decisión, y puede quedar sin resolver mientras nadie responda por el Festival.
/// </remarks>
public sealed class CrearFestivalAdministrativoSolicitud
{
    public CrearFestivalBorradorSolicitud Festival { get; set; } = new();

    /// <summary>
    /// La organización que gestiona el Festival.
    /// </summary>
    /// <remarks>
    /// <b>NO ES LA PROCEDENCIA.</b> Que el Programa incorpore el Festival no lo convierte en su
    /// organización responsable: eso sigue siendo la Fundación que lo organiza. Puede venir nulo:
    /// un Festival histórico puede no tener todavía quién responda por él.
    /// </remarks>
    public int? OrganizacionResponsableId { get; set; }
}

/// <summary>Un Festival ya registrado que se parece al que se está dando de alta.</summary>
/// <remarks>
/// EXISTE PARA QUE NADIE REGISTRE DOS VECES EL MISMO FESTIVAL. Se ofrece antes de guardar, con lo
/// justo para reconocerlo: nombre, territorio y estado. No decide nada por quien registra.
/// </remarks>
public sealed record CoincidenciaDeAltaDto(
    string Id,
    string Nombre,
    string? Territorio,
    string Estado,
    string? OrganizacionResponsable);

/// <summary>
/// La ficha administrativa de un Festival: sus datos permanentes, de dónde vino, quién responde
/// por él y sus ediciones.
/// </summary>
/// <remarks>
/// <para>
/// <b>EL FESTIVAL ES LA ENTIDAD PRINCIPAL Y LAS EDICIONES CUELGAN DE ÉL.</b> No son un módulo
/// equivalente: son el historial de realizaciones de ese Festival concreto. Por eso viajan dentro
/// de su ficha y no en una lista propia paralela.
/// </para>
/// <para>
/// <b>TRES DIMENSIONES SEPARADAS.</b> <c>Procedencia</c> dice quién lo incorporó;
/// <c>OrganizacionResponsable</c>, quién lo gestiona. Que el Programa lo haya registrado no lo
/// convierte en su responsable.
/// </para>
/// </remarks>
public sealed record FichaFestivalAdministrativaDto(
    string Id,
    string Nombre,
    string? Descripcion,
    string Estado,
    string NivelCobertura,
    string? Departamento,
    string? Municipio,
    string? Periodicidad,
    string? CorreoContacto,
    string? Telefono,
    string? SitioWeb,
    int? OrganizacionResponsableId,
    string? OrganizacionResponsableNombre,
    ProcedenciaDeRegistroDto? Procedencia,
    IReadOnlyList<ElementoDeClasificacionDto> PracticasMusicales,
    IReadOnlyList<ElementoDeClasificacionDto> TerritoriosSonoros,
    IReadOnlyList<EdicionDelFestivalDto> Ediciones,
    DateTime FechaCreacion,
    DateTime? FechaActualizacion);

/// <summary>Una realización del Festival, con lo justo para reconocerla y abrirla.</summary>
public sealed record EdicionDelFestivalDto(
    string Id,
    int? Anio,
    int? NumeroEdicion,
    string? Nombre,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string EstadoVisibilidad,
    ProcedenciaDeRegistroDto? Procedencia);

/// <summary>
/// La ficha de un mercado musical en la consola: lo permanente, quién responde y de dónde vino.
/// </summary>
/// <remarks>
/// <b>ES LA PAREJA DE <see cref="FichaFestivalAdministrativaDto"/>, Y NO LA MISMA.</b> Un mercado
/// tiene alcance, modalidad y puede realizarse en el marco de un festival; un festival no tiene
/// nada de eso. Lo que sí es igual es el armazón —estado, territorio, organización responsable,
/// procedencia, clasificación y realizaciones—, porque es lo que hace que las dos fichas se lean
/// igual.
/// </remarks>
public sealed record FichaMercadoAdministrativaDto(
    string Id,
    string Nombre,
    string? Descripcion,
    string Estado,
    string NivelCobertura,
    string? Departamento,
    string? Municipio,
    string? LugarEspecifico,
    string? Periodicidad,
    string? Alcance,
    string? Modalidad,
    string? CorreoContacto,
    string? Telefono,
    string? SitioWeb,
    int? OrganizacionResponsableId,
    string? OrganizacionResponsableNombre,
    bool SeRealizaEnElMarcoDeUnFestival,
    int? FestivalId,
    string? FestivalNombre,
    ProcedenciaDeRegistroDto? Procedencia,
    IReadOnlyList<ElementoDeClasificacionDto> PracticasMusicales,
    IReadOnlyList<ElementoDeClasificacionDto> TerritoriosSonoros,
    IReadOnlyList<EdicionDelMercadoDto> Ediciones,
    DateTime FechaCreacion,
    DateTime? FechaActualizacion);

/// <summary>Una realización del mercado, con lo justo para reconocerla.</summary>
/// <remarks>
/// LOS DOS EJES DE ESTADO NO SE MEZCLAN: <c>EstadoVisibilidad</c> dice si el portal la enseña y
/// <c>Estado</c> describe el acontecimiento —en preparación, programada, realizada, cancelada—.
/// </remarks>
public sealed record EdicionDelMercadoDto(
    string Id,
    int Anio,
    int? NumeroEdicion,
    string? Nombre,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string Estado,
    string EstadoVisibilidad,
    ProcedenciaDeRegistroDto? Procedencia);

/// <summary>Lo que la consola envía para publicar, archivar o devolver a borrador un Festival.</summary>
/// <remarks>
/// LOS TRES ESTADOS Y NO LOS SIETE: los demás —en revisión, ajustes solicitados, aprobado,
/// rechazado— pertenecen al circuito de revisión del canal externo y escribirlos desde aquí
/// dejaría la bandeja diciendo cosas que nadie decidió en ella.
/// </remarks>
public sealed class CambioDePublicacionDeFestival
{
    public string Estado { get; set; } = string.Empty;
}

/// <summary>Lo que la consola envía para devolver un proceso a custodia institucional.</summary>
/// <remarks>
/// EL MOTIVO NO ES OPCIONAL. Quitarle a una organización la administración de su propio proceso es
/// la decisión más fuerte de esa pantalla, y dentro de un año nadie recordará por qué se tomó.
/// </remarks>
public sealed class LiberacionDeAdministracion
{
    public string Motivo { get; set; } = string.Empty;
}
