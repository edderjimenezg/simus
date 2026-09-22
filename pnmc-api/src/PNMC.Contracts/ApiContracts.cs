namespace PNMC.Contracts;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Limit, int Offset, int Total);

/// <summary>Cuantas filas hay bajo un codigo, contado como faceta.</summary>
/// <remarks>
/// FACETA QUIERE DECIR: contado con los DEMAS filtros puestos, pero no con el suyo. Un desplegable
/// de estado que se contara a si mismo dejaria en cero todas las opciones no elegidas; uno que
/// ignorara el filtro de territorio prometeria filas que ese filtro ya descarto.
/// </remarks>
public sealed record FacetaDto(string Codigo, string Etiqueta, int Total)
{
    /// <summary>El cajon de las filas sin territorio. No es un hueco: se puede pedir.</summary>
    public const string SinTerritorio = "sin_territorio";
}

/// <summary>Una fila de la tabla de un modulo de la consola.</summary>
/// <remarks>
/// TENIA NOMBRE DE NADIE HASTA EL 26 DE AGOSTO DE 2026: era un tipo anonimo devuelto como
/// <c>object</c>. Con un <c>object</c> no se puede ordenar ni filtrar el resultado sin reflexion,
/// asi que el orden acababa haciendose en el navegador sobre la pagina ya traida —cien filas de
/// hasta quinientas—, y la tabla afirmaba «primero la mas reciente» mientras la mas reciente podia
/// no haber venido. Los nombres van en PascalCase y salen en camelCase porque las opciones de JSON
/// de la API son las de <c>JsonSerializerDefaults.Web</c>: el JSON no cambia ni un caracter.
/// </remarks>
public sealed record AdminRecordDto(
    string Id,
    string Title,
    string Table,
    string Department,
    string Municipality,
    string Status,
    string StatusLabel,
    string UpdatedAt,
    string UpdatedAtUtc,
    string ZonaHoraria,
    IReadOnlyDictionary<string, object?> Metadata);

/// <summary>Una pagina de registros de un modulo, con sus facetas y el orden aplicado.</summary>
/// <remarks>
/// LOS CUATRO PRIMEROS CAMPOS SON LOS DE <see cref="PagedResponse{T}"/> Y EN EL MISMO ORDEN: la
/// consola ya leia <c>items</c> y <c>total</c>, y esta respuesta no le quita nada, solo le añade.
/// </remarks>
public sealed record AdminRegistrosRespuestaDto(
    IReadOnlyList<AdminRecordDto> Items,
    int Limit,
    int Offset,
    int Total,
    IReadOnlyList<FacetaDto> Estados,
    IReadOnlyList<FacetaDto> Territorios,
    string Orden,
    string Direccion);

public sealed record MapDepartmentSummaryDto(
    string Department,
    int Records,
    int Festivals
);

public sealed record MapSummaryResponseDto(
    string Layer,
    IReadOnlyList<MapDepartmentSummaryDto> Items
);

public sealed record FestivalDrilldownItemDto(string Id, string Name, string Municipality);
public sealed record DepartmentDrilldownResponseDto(
    string Department,
    IReadOnlyList<FestivalDrilldownItemDto> Festivals
);

public sealed class ParticipationSubmissionRequest
{
    public string? Reference { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorTypeLabel { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string IndividualFirstName { get; set; } = string.Empty;
    public string IndividualLastName { get; set; } = string.Empty;
    public string IdentificationType { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public bool HasArtisticName { get; set; }
    public string ArtisticName { get; set; } = string.Empty;
    public string ResponsibleEntity { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactRole { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string TerritoryScope { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string FacebookUrl { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public string MusicalFields { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Contribution { get; set; } = string.Empty;
    public string Needs { get; set; } = string.Empty;
    public string OrganizationSubtype { get; set; } = string.Empty;
    public string YearFounded { get; set; } = string.Empty;
    public string LegalStatus { get; set; } = string.Empty;
    public string MainPrograms { get; set; } = string.Empty;
    public string FestivalDurationDays { get; set; } = string.Empty;
    public string FestivalSetting { get; set; } = string.Empty;
    public string FestivalVenueMode { get; set; } = string.Empty;
    public List<ParticipationFestivalLocation> FestivalAdditionalLocations { get; set; } = [];
    public string FestivalFrequency { get; set; } = string.Empty;
    public string FestivalVersions { get; set; } = string.Empty;
    public List<string> FestivalHabitualMonths { get; set; } = [];
    public string FestivalTicketing { get; set; } = string.Empty;
    public string OpenCall { get; set; } = string.Empty;
    public string FestivalThisYearStatus { get; set; } = string.Empty;
    public string FestivalThisYearDate { get; set; } = string.Empty;
    public string FestivalThisYearStartDate { get; set; } = string.Empty;
    public string FestivalThisYearEndDate { get; set; } = string.Empty;
    public string FestivalCurrentOpenCall { get; set; } = string.Empty;
    public string FestivalOpenCallDeadline { get; set; } = string.Empty;
    public string MarketFrequency { get; set; } = string.Empty;
    public string MarketEditionsCount { get; set; } = string.Empty;
    public string AverageBuyers { get; set; } = string.Empty;
    public string LinkedFestival { get; set; } = string.Empty;
    public string LinkedFestivalName { get; set; } = string.Empty;
    public List<string> MarketHabitualMonths { get; set; } = [];
    public string MarketThisYearStatus { get; set; } = string.Empty;
    public string MarketThisYearMonth { get; set; } = string.Empty;
    public string MarketThisYearDate { get; set; } = string.Empty;
    public string IndividualProfile { get; set; } = string.Empty;
    public string TrajectoryYears { get; set; } = string.Empty;
    public string LinkedProcesses { get; set; } = string.Empty;
    public string Members { get; set; } = string.Empty;
    public string MusicalPractice { get; set; } = string.Empty;
    public string CirculationScope { get; set; } = string.Empty;
    public string CollectiveTrajectory { get; set; } = string.Empty;
    public string SpaceType { get; set; } = string.Empty;
    public string SpaceCapacity { get; set; } = string.Empty;
    public string SpaceUses { get; set; } = string.Empty;
    public string TechnicalEquipment { get; set; } = string.Empty;
    public bool Consent { get; set; }
}

public sealed class ParticipationFestivalLocation
{
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
}

public sealed record ParticipationSubmissionResponse(
    string Reference,
    string Status,
    DateTimeOffset SubmittedAt,
    string Message,
    string ExternalSyncStatus,
    string ExternalSyncMessage
);

public sealed record ParticipationSubmissionSummaryDto(
    string Reference,
    DateTimeOffset SubmittedAt,
    string ActorType,
    string ActorName,
    string Email,
    string Department,
    string Municipality,
    string ExternalSyncStatus
);

public sealed class MapFestivalUpsertRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VersionsCount { get; set; } = string.Empty;
    public string LastEditionDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public string OrganizerEmail { get; set; } = string.Empty;
    public string OrganizerPhone { get; set; } = string.Empty;
    public string OrganizerWebsiteUrl { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public string FacebookUrl { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string OtherUrl { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public bool HasCurrentYearEdition { get; set; }
    public string CurrentYearEditionStatus { get; set; } = string.Empty;
    public string CurrentYearStartDate { get; set; } = string.Empty;
    public string CurrentYearEndDate { get; set; } = string.Empty;
    public string CoverageLevel { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = string.Empty;
}

public sealed class MapSchoolUpsertRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SchoolCategory { get; set; } = string.Empty;
    public string SchoolType { get; set; } = string.Empty;
    public string ResponsibleEntity { get; set; } = string.Empty;
    public string DirectorName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public string FacebookUrl { get; set; } = string.Empty;
    public string OtherUrl { get; set; } = string.Empty;
    public string CoverageLevel { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string SpecificLocation { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string TrainingCapacity { get; set; } = string.Empty;
    public int Students { get; set; }
    public int ActiveGroupsCount { get; set; }
    public string TrainingProcesses { get; set; } = string.Empty;
    public string MusicalPractices { get; set; } = string.Empty;
    public bool IsActiveSchool { get; set; } = true;
    public string Observations { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = string.Empty;
}

public sealed class MapMarketUpsertRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int EditionsCount { get; set; }
    public string Periodicity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool HasCurrentYearEdition { get; set; }
    public string CurrentYearEditionStatus { get; set; } = string.Empty;
    public string CurrentYearStartDate { get; set; } = string.Empty;
    public string CurrentYearEndDate { get; set; } = string.Empty;
    public string ResponsibleEntity { get; set; } = string.Empty;
    public string ResponsibleEntityEmail { get; set; } = string.Empty;
    public string ResponsibleEntityPhone { get; set; } = string.Empty;
    public string ResponsibleEntityWebsiteUrl { get; set; } = string.Empty;
    public string AssociatedFestivalId { get; set; } = string.Empty;
    public string AssociatedFestivalDisplayName { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string MarketMode { get; set; } = string.Empty;
    public string CoverageLevel { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string SpecificLocation { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = string.Empty;
}

public sealed class OrganizationUpsertRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CoverageLevel { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string OrganizationType { get; set; } = string.Empty;
    public string TerritorialScope { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string FacebookUrl { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public string OtherUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = string.Empty;
}

public sealed class SpaceInfrastructureUpsertRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string WorkshopName { get; set; } = string.Empty;
    public string PrimaryFunction { get; set; } = string.Empty;
    public string Instruments { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string FacebookUrl { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public string OtherUrl { get; set; } = string.Empty;
    public string CoverageLevel { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = string.Empty;
}

public sealed class EscenarioUpsertRequest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VenueType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? MaxCapacityApprox { get; set; }

    /// <summary>
    /// Tres estados y no dos: si, no y —cuando llega nulo— no consta. Un booleano no anulable
    /// afirmaria que el escenario NO es accesible mientras nadie rellene el campo, y esa
    /// afirmacion saldria publicada.
    /// </summary>
    public bool? HasAccessibility { get; set; }

    public string Dotacion { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string FacebookUrl { get; set; } = string.Empty;
    public string InstagramUrl { get; set; } = string.Empty;
    public string OtherUrl { get; set; } = string.Empty;
    public string CoverageLevel { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = string.Empty;
}

public sealed class AdminRecordStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string? RejectionReason { get; set; }
    public string? ObservedFieldsJson { get; set; }
}

public sealed class AdminLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <param name="Role">
/// El rol PRINCIPAL, por precedencia declarada (webmaster &gt; gestor_interno &gt; externo).
/// Desde el modelo vigente una persona puede tener varios; este campo se
/// conserva porque media consola pinta una sola etiqueta, y quitarlo habria sido un cambio
/// rompiente sin necesidad. La verdad completa esta en <paramref name="Roles"/>.
/// </param>
/// <param name="Roles">
/// TODOS los roles de la persona, normalizados y en orden estable. Campo anadido, no sustituto:
/// quien solo entienda <paramref name="Role"/> sigue funcionando igual que antes.
/// </param>
public sealed record AdminUserDto(
    string Id,
    string FullName,
    string Email,
    string Role,
    string RoleLabel,
    bool IsActive,
    DateTime? LastLoginAt,
    string? Telefono = null,
    IReadOnlyList<string>? Roles = null,
    /// <summary>El número de documento de quien usa la cuenta.</summary>
    /// <remarks>
    /// SE AÑADE AL FINAL Y CON VALOR POR OMISION: quien no lo envíe sigue funcionando igual. Las
    /// columnas existían en la base desde antes y nadie las leía, así que la consola no tenía dónde
    /// pedir la información básica de una cuenta administrativa.
    /// </remarks>
    string? Identificacion = null,
    /// <summary>El código del tipo de documento, del catálogo del país.</summary>
    string? TipoDocumento = null,
    /// <summary>Cómo se lee ese tipo de documento. Solo lectura: lo resuelve el servidor.</summary>
    string? TipoDocumentoEtiqueta = null,
    /// <summary>La cuenta todavía usa la contraseña que le puso quien la creó.</summary>
    bool DebeCambiarContrasena = false,
    /// <summary>La persona ya dijo quién es: nombre y documento.</summary>
    /// <remarks>
    /// <b>LAS DOS VIAJAN EN EL LOGIN</b> porque la consola tiene que saber, antes de dibujar nada,
    /// si esta persona está en su primer ingreso. Sin ellas habría que preguntarlo aparte y la
    /// pantalla parpadearía entre la consola y el formulario.
    /// </remarks>
    bool PerfilCompletado = true,
    /// <summary>Las cuatro partes del nombre, como se piden en todo el proyecto.</summary>
    string? PrimerNombre = null,
    string? SegundoNombre = null,
    string? PrimerApellido = null,
    string? SegundoApellido = null,
    /// <summary>Cuántos apartados de la consola puede abrir esta cuenta, de cuántos hay.</summary>
    /// <remarks>
    /// <b>VIAJA EN LA LISTA PARA QUE NO HAYA QUE ABRIR VEINTIDOS FICHAS.</b> Quién puede abrir qué es
    /// la pregunta que se le hace a esta pantalla, y hasta solo se
    /// respondía cuenta por cuenta: había que abrir la ficha de cada una y contar casillas. La cifra
    /// se calcula con la MISMA regla que la ficha —concedidos ∪ siempre activados, y todos si es
    /// webmaster—, en una consulta fija más, no en una por fila.
    /// </remarks>
    int ApartadosActivos = 0,
    int ApartadosTotales = 0
);

public sealed record AdminAuthResponse(AdminUserDto User);

public sealed class UpdateProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Password { get; set; }
}

public sealed class AdminUserUpsertRequest
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Rol unico. Se sigue admitiendo, y es lo que envia cualquier cliente anterior a la transicion.
    /// </summary>
    /// <remarks>
    /// Si <see cref="Roles"/> viene con contenido, MANDA <see cref="Roles"/> y este campo se
    /// ignora. La alternativa —unirlos— convertiria un envio de dos roles en uno de tres sin que
    /// nadie lo pidiera, que es justo la clase de acumulacion silenciosa que el defecto U5 del
    /// plan de construccion describe.
    /// </remarks>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Los roles de la persona. Vacio o ausente significa «usa <see cref="Role"/>».
    /// </summary>
    public IReadOnlyList<string>? Roles { get; set; }

    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>El teléfono de contacto de quien usa la cuenta.</summary>
    public string? Telefono { get; set; }

    /// <summary>El número de documento. Opcional: no toda cuenta lo tiene todavía.</summary>
    /// <remarks>
    /// <b>NO SE EXIGE PARA NO BLOQUEAR LAS CUENTAS QUE YA EXISTEN.</b> Ninguna de las que hay lo
    /// tiene, y exigirlo convertiría cualquier edición —cambiar un rol, desactivar— en un formulario
    /// que no se puede guardar sin ir a buscar la cédula de esa persona.
    /// </remarks>
    public string? Identificacion { get; set; }

    /// <summary>El código del tipo de documento, del catálogo `TiposDocumento`.</summary>
    public string? TipoDocumento { get; set; }
}

public sealed record NotificationDto(
    string Id,
    string? RecipientUserId,
    string RecipientEmail,
    string EventType,
    string Channel,
    string Title,
    string Body,
    string Status,
    string ModuleId,
    string RecordId,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ReadAt,
    string? MetadataJson = null
);

public sealed class NotificationCreateRequest
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Channel { get; set; } = "internal";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = string.Empty;
    public string AmbitoAcceso { get; set; } = "externo";
}

public sealed record RecordLinkRequestDto(
    string Id,
    string ModuleId,
    string RecordId,
    string RequestingUserId,
    string? EntidadId,
    string RequestedScope,
    string Reason,
    string EvidenceText,
    string Status,
    string ReviewComment,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? RecordName = null,
    string? EntidadNombre = null
);

public sealed class RecordLinkRequestCreateRequest
{
    public string ModuleId { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string RequestedScope { get; set; } = "responsable";
    public string Reason { get; set; } = string.Empty;
    public string EvidenceText { get; set; } = string.Empty;
}

public sealed class RecordLinkRequestStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public sealed record RecordDuplicateCandidateDto(
    string Id,
    string ModuleId,
    string SourceRecordId,
    string CandidateRecordId,
    string SimilarityLevel,
    decimal? SimilarityScore,
    string EvidenceJson,
    string Status,
    string Decision,
    string DecisionComment,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed class RecordDuplicateCandidateCreateRequest
{
    public string ModuleId { get; set; } = string.Empty;
    public string SourceRecordId { get; set; } = string.Empty;
    public string CandidateRecordId { get; set; } = string.Empty;
    public string SimilarityLevel { get; set; } = "media";
    public decimal? SimilarityScore { get; set; }
    public string EvidenceJson { get; set; } = "{}";
}

public sealed class RecordDuplicateDecisionRequest
{
    public string Decision { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public sealed record RecordQualityFlagDto(
    string Id,
    string ModuleId,
    string RecordId,
    string FlagType,
    string Severity,
    string Status,
    string Detail,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed class RecordQualityFlagCreateRequest
{
    public string ModuleId { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public string FlagType { get; set; } = string.Empty;
    public string Severity { get; set; } = "media";
    public string Detail { get; set; } = string.Empty;
}

public sealed class RecordQualityFlagStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Lo que devuelve el alta: la cuenta y la organizacion que acaban de nacer.
/// </summary>
/// <remarks>
/// Los tres campos que se fueron —<c>VerificationStatus</c>, <c>CodeExpiresAt</c> y
/// <c>DebugVerificationCode</c>— describian un segundo paso que ya no existe. El ultimo ademas solo
/// traia el codigo en Development, Local y Test: en cualquier otro entorno viajaba vacio, de modo
/// que la pantalla mandaba a buscar al correo un codigo que ningun remitente enviaba, porque no hay
/// ni una linea de envio de correo en toda la API. La cuenta nace activa y se entra con correo y
/// contraseña.
/// </remarks>
public sealed record ExternalRegisterResponse(
    string UserId,
    string Email,
    string AccountStatus,
    string OrganizationId,
    string OrganizationName
);

/// <summary>
/// El alta externa: una organizacion, la persona que responde por ella, y el acceso. Un solo acto.
/// </summary>
/// <remarks>
/// <para>
/// NO HAY CUENTA PERSONAL SUELTA. Antes esta peticion creaba una persona y nada mas —traia
/// <c>ProfileType</c>, <c>ActorType</c> y <c>OrganizationName</c>, y el manejador se quedaba solo con
/// el nombre y el correo—, y la organizacion venia despues por otra ruta, con sesion ya iniciada.
/// Eso dejaba cuentas sin organizacion, que no podian hacer nada, y organizaciones sin nadie
/// identificado, que es lo que dejo <c>EntidadesResponsable</c> con cero filas frente a diecisiete
/// entidades. Ahora las tres cosas entran juntas o no entra ninguna.
/// </para>
/// <para>
/// La identidad de la persona se recoge segmentada, con tipo y número de documento. El nombre
/// completo se conserva solo como compatibilidad transitoria para clientes previos.
/// </para>
/// </remarks>
public sealed class ExternalRegisterRequest
{
    // ---- La organizacion ----------------------------------------------------------------
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>NIT u otra identificacion. Opcional: no toda organizacion del ecosistema tiene una.</summary>
    public string? OrganizationIdentificationNumber { get; set; }

    /// <summary>
    /// UN SOLO CORREO PARA LAS DOS COSAS: el institucional de la organizacion —se guarda en
    /// dbo.Entidades.CorreoContacto— y el de acceso de la cuenta —dbo.Usuarios.CorreoElectronico—.
    /// </summary>
    /// <remarks>
    /// ANTES ERAN DOS CAMPOS, y el comentario que los separaba afirmaba que fundirlos publicaria la
    /// credencial en la ficha publica del Festival. Se comprobo endpoint por endpoint y era falso:
    /// ninguna ruta anonima devuelve Entidades.CorreoContacto. Lo que sale en la ficha publica de un
    /// Festival es Festivales.CorreoContacto, que es OTRO campo —el que la organizacion rellena al
    /// crear el proceso—. Los dos unicos lectores del correo de la entidad exigen sesion:
    /// ExternalOrganizationEndpoints, la propia organización, y el contrato institucional de
    /// organizaciones.
    /// </remarks>
    public string Email { get; set; } = string.Empty;

    /// <summary>Ubicación principal de la organización. No declara su alcance territorial.</summary>
    public string HeadquartersDepartmentCode { get; set; } = string.Empty;
    public string HeadquartersMunicipalityCode { get; set; } = string.Empty;

    // ---- La persona que responde por ella -----------------------------------------------
    /// <summary>Primer nombre y primer apellido: identifican a quien administrará inicialmente.</summary>
    public string FirstName { get; set; } = string.Empty;
    public string? SecondName { get; set; }
    public string FirstSurname { get; set; } = string.Empty;
    public string? SecondSurname { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>Compatibilidad de clientes anteriores; los clientes nuevos usan los campos segmentados.</summary>
    public string FullName { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    // ---- La clave ------------------------------------------------------------------------
    public string Password { get; set; } = string.Empty;
    /// <summary>
    /// Las finalidades que la persona autoriza expresamente en este alta.
    /// </summary>
    /// <remarks>
    /// SON CLAVES DE FINALIDAD Y NO UN PAR DE BOOLEANOS, y ese es el cambio que importa. Dos
    /// casillas fijas no dejan distinguir entre lo que sostiene la cuenta y lo que es voluntario:
    /// quien quisiera registrarse sin recibir el boletín no tenía forma de decirlo, porque el
    /// boletín no era una finalidad aparte. La Ley 1581 art. 9 pide autorización para fines
    /// determinados; una lista de finalidades es lo que permite otorgar unas y no otras.
    /// </remarks>
    public IReadOnlyList<string> PoliticasAceptadas { get; set; } = [];
}

/// <summary>Una política vigente tal como la sirve el servidor para que la pantalla la muestre.</summary>
/// <remarks>
/// <b>VIAJA CON EL TEXTO ENTERO.</b> No con un enlace a un texto: con el texto. Es lo que la
/// pantalla enseña y lo que el servidor copiará dentro de la autorización, y tienen que ser lo
/// mismo o la evidencia no prueba nada. <c>UrlOficial</c> es un añadido —el documento institucional
/// completo del Ministerio—, no el sustituto del texto.
/// </remarks>
public sealed record PoliticaPublicaDto(
    string Clave,
    string Version,
    string Titulo,
    string Texto,
    string? UrlOficial,
    string? ReferenciaOficial);

/// <summary>Una política en el contexto del alta, donde además importa si es exigible.</summary>
public sealed record PoliticaParaRegistroDto(
    string Clave,
    string Version,
    string Titulo,
    string Texto,
    string? UrlOficial,
    string? ReferenciaOficial,
    bool Obligatoria);

public sealed record PreparacionDeRegistroDto(
    bool RegistroDisponible,
    IReadOnlyList<PoliticaParaRegistroDto> Politicas,
    IReadOnlyList<string> Impedimentos);

/// <summary>Lo que una cuenta ha autorizado, incluido lo que retiró.</summary>
/// <remarks>
/// <para>
/// <b><c>TextoAceptado</c> ES LA PRUEBA.</b> El art. 8 num. 2 de la Ley 1581 da al titular el
/// derecho a solicitar prueba de la autorización, y la prueba no es «aceptaste el documento 2»: es
/// el texto que tenía delante. Por eso viaja entero y no resumido.
/// </para>
/// <para>
/// <b><c>SePuedeRevocar</c> Y <c>MotivoSinRevocar</c> VAN JUNTOS.</b> Cuando no se puede retirar, la
/// pantalla no debe limitarse a no ofrecer el botón: tiene que decir por qué. El art. 9 del decreto
/// 1377 de 2013 admite que la revocatoria no proceda, pero eso no exime de explicarlo.
/// </para>
/// </remarks>
public sealed record AutorizacionDeDatosDto(
    long Id,
    string Finalidad,
    string Titulo,
    string Version,
    string TextoAceptado,
    bool TextoReconstruido,
    string Origen,
    DateTime FechaOtorgada,
    DateTime? FechaRevocacion,
    bool Vigente,
    bool SePuedeRevocar,
    string? MotivoSinRevocar);

/// <summary>
/// La persona que responde por una organizacion, venga de donde venga.
/// </summary>
/// <remarks>
/// <para>
/// <c>Origen</c> NO ES DECORACION. «declarado» significa que hay una fila en
/// <c>EntidadesResponsable</c>: una persona dio su nombre y su documento al dar de alta la
/// organizacion. «cuenta» significa que NO la hay y que el nombre se dedujo del vinculo de
/// <c>UsuariosEntidades</c> con rol de administrador o propietario. Las dos cosas se pintan igual
/// en una tabla y no valen lo mismo; sin este campo, la consola presentaria una deduccion con la
/// misma cara que un dato firmado.
/// </para>
/// <para>
/// EL NUMERO DE DOCUMENTO NO ESTA AQUI, Y ES DELIBERADO. <c>TieneDocumento</c> dice si la identidad
/// se capturo; el numero se queda en la base. Un listado no lo necesita, y repartir la cedula de un
/// ciudadano por cada fila de una tabla es justo lo que <c>EntidadesResponsable</c> existe para
/// evitar.
/// </para>
/// </remarks>
/// <param name="RolEntidad">Solo con origen «cuenta»: que rol tiene esa cuenta sobre la entidad.</param>
public sealed record OrganizacionResponsableDto(
    string Nombre,
    string? Correo,
    string? Telefono,
    string? TipoDocumento,
    bool TieneDocumento,
    bool AutorizacionDatos,
    DateTime? Desde,
    string? RolEntidad,
    string Origen
);

/// <summary>Cuántos procesos administra una organización, y en qué estado están.</summary>
/// <remarks>
/// <para>
/// <b>UN NUMERO SUELTO NO DICE NADA.</b> «3 procesos» puede ser tres borradores que nadie ha visto
/// o tres festivales publicados de los que depende media región, y quien decide si archivar una
/// organización necesita saber cuál de las dos cosas es. <c>Dependientes</c> es la cifra que
/// importa: los procesos que quedarían sin nadie que responda por ellos si la organización se
/// cierra —publicados, en revisión, aprobados o con ajustes pedidos—.
/// </para>
/// <para>
/// Festival es el único proceso habilitado hoy; cuando entren los demás, crecen los campos y no
/// el significado.
/// </para>
/// </remarks>
public sealed record OrganizacionProcesosDto(
    int Festivales,
    int Publicados,
    int EnCurso,
    int Dependientes
)
{
    public static OrganizacionProcesosDto Vacio { get; } = new(0, 0, 0, 0);

    /// <summary>Se conserva como total para la tabla, sin introducir procesos aún no habilitados.</summary>
    public int Total => Festivales;
}

/// <summary>Una organizacion del ecosistema, tal como la ve la consola institucional.</summary>
public sealed record OrganizacionAdminDto(
    string Id,
    string Nombre,
    string? NombreLegal,
    string? Identificacion,
    string? CorreoContacto,
    string? Telefono,
    string Estado,
    string EstadoEtiqueta,
    bool Activa,
    bool EsInstitucional,
    string Territorio,
    OrganizacionResponsableDto? Responsable,
    OrganizacionProcesosDto Procesos,
    /// <summary>
    /// De dónde vino la organización: quién la incorporó al sistema y con qué cuenta.
    /// </summary>
    /// <remarks>
    /// NO ES LO MISMO QUE «QUIÉN RESPONDE POR ELLA». Una organización registrada desde su propio
    /// espacio se aportó a sí misma; una creada desde la consola la aportó el Programa, sin que eso
    /// cambie quién la administra. Anulable: lo anterior a la tabla de procedencia puede no tenerla.
    /// </remarks>
    ProcedenciaDeRegistroDto? Procedencia,
    /// <summary>Si alguna de sus cuentas responsables ya comprobó que ese correo es suyo.</summary>
    /// <remarks>
    /// <para>
    /// <b>ES LA MISMA PREGUNTA QUE HACE LA PUERTA</b>
    /// —<c>AdministracionDeOrganizacion.TieneCorreoConfirmadoAsync</c>—, y por eso viaja: sin este
    /// dato la consola ve una organización «pendiente de confirmación» y no sabe si eso la está
    /// bloqueando de verdad ni qué acción ofrecer.
    /// </para>
    /// <para>
    /// NO ES EL ESTADO. Una organización puede estar <c>inactiva</c> con el correo comprobado —la
    /// desactivó el Programa— y estar <c>activa</c> es imposible sin él. Son dos cosas y se leen
    /// juntas.
    /// </para>
    /// </remarks>
    bool CorreoConfirmado,
    /// <summary>Las direcciones de las cuentas que responden por ella, para poder nombrar cuál se comprueba.</summary>
    /// <remarks>
    /// NO ES <c>CorreoContacto</c>. Aquel es el correo que la organización declaró en su ficha, y
    /// puede ser el de la oficina; estos son los de las cuentas con las que se entra, que son los
    /// que se confirman. Cuando hay más de uno, la consola tiene que decir cuál comprobó.
    /// </remarks>
    IReadOnlyList<string> CorreosDeCuenta,
    DateTime FechaCreacion,
    DateTime? FechaActualizacion
);

/// <summary>Cuantas organizaciones hay en un estado, contadas como faceta.</summary>
/// <remarks>
/// FACETA QUIERE DECIR: contado sobre los DEMAS filtros puestos, pero no sobre este. Con el filtro
/// de estado puesto en «registrada», «borrador» tiene que seguir diciendo cuantas hay o la pestaña
/// pareceria vacia; con un departamento elegido, en cambio, estas cifras SI se reducen a ese
/// departamento, porque si no la pestaña prometeria filas que el filtro ya descarto.
/// </remarks>
public sealed record OrganizacionEstadoDto(string Id, string Etiqueta, int Total);

/// <summary>Cuantas organizaciones hay en un departamento. Se cuenta como faceta, igual que el estado.</summary>
/// <param name="Codigo">
/// Codigo Divipola de departamento, o <see cref="OrganizacionTerritorioDto.SinTerritorio"/> para
/// las que no tienen ninguno. Ese cajon NO es decorativo: en la base local dos de las siete
/// organizaciones no tienen departamento, y sin una opcion propia no habria manera de aislarlas.
/// </param>
public sealed record OrganizacionTerritorioDto(string Codigo, string Etiqueta, int Total)
{
    /// <summary>El valor que pide las organizaciones sin departamento asignado.</summary>
    public const string SinTerritorio = "sin_territorio";
}

/// <param name="Orden">La columna por la que el servidor ORDENO de verdad, no la que se pidio.</param>
/// <param name="Direccion">«asc» o «desc», tambien la aplicada.</param>
/// <remarks>
/// <c>Orden</c> Y <c>Direccion</c> VIAJAN DE VUELTA A PROPOSITO. El orden se resuelve en SQL antes
/// de paginar, asi que la pantalla no puede deducirlo mirando lo que recibio: si pidiera una
/// columna que no existe, la flechita quedaria puesta sobre una cabecera que no ordeno nada.
/// </remarks>
public sealed record OrganizacionesRespuestaDto(
    int Total,
    int Pagina,
    int TamanoPagina,
    IReadOnlyList<OrganizacionEstadoDto> Estados,
    IReadOnlyList<OrganizacionAdminDto> Items,
    IReadOnlyList<OrganizacionTerritorioDto> Territorios,
    string Orden,
    string Direccion
);

/// <summary>
/// La ficha completa de una organización: identidad, festivales que administra -con nombre, no solo
/// el conteo- y las solicitudes y reclamaciones que la involucran.
/// </summary>
/// <remarks>
/// <b>EL HISTORIAL YA NO VIAJA AQUI.</b> Hasta esta ficha traía su
/// propia copia de la bitácora, que la pantalla pintaba con una lista escrita a mano y que además
/// enseñaba el verbo técnico —«actualizar»— en vez de lo que hizo la persona. Noticias, Agenda y
/// Catálogo Editorial ya leían ese historial por <c>/admin/auditoria</c> con el componente
/// compartido; esta era la excepción, y una excepción en cómo se lee la bitácora acaba siendo una
/// excepción en qué se entiende de ella.
///
/// POR QUÉ EXISTE APARTE DE <see cref="OrganizacionAdminDto"/>. La fila de la tabla ya trae la
/// identidad y CUÁNTOS festivales administra -<see cref="OrganizacionProcesosDto"/>-, y eso basta
/// para decidir qué filtrar o con quién hablar. No basta para «consultar festivales administrados»
/// ni «revisar solicitudes»: para eso hace falta CUÁLES, no cuántos. Traer esa lista en cada fila
/// de la tabla multiplicaría consultas innecesarias; aquí se pide una sola vez, para una organización.
/// </remarks>
public sealed record OrganizacionFichaDto(
    OrganizacionAdminDto Organizacion,
    IReadOnlyList<ProcesoDeOrganizacionDto> Festivales,
    IReadOnlyList<RecordLinkRequestDto> Solicitudes,
    IReadOnlyList<ReclamacionAdministracionDto> Reclamaciones
);

/// <summary>Quien hizo la operacion. Nulo solo si la fila de bitacora no guardo usuario.</summary>
public sealed record AuditoriaAutorDto(string Id, string Nombre, string Correo);

/// <summary>
/// Una linea de la bitacora, ya legible: quien, que hizo, y sobre que registro con su nombre.
/// </summary>
/// <remarks>
/// <c>NombreRegistro</c> es nulo cuando la tabla afectada no esta entre las que se saben leer. La
/// pantalla cae entonces al identificador —que es lo unico que se mostraba antes— en vez de
/// inventarse un nombre.
///
/// <c>Accion</c> es el verbo tal como lo guarda la base —«iniciar_sesion»—, y <c>AccionEtiqueta</c>
/// es ese mismo verbo en palabras —«Inició sesión»—. Se envian LOS DOS a proposito: la pantalla
/// enseña la etiqueta y filtra por el verbo, que es el que viaja en la consulta.
/// </remarks>
public sealed record AuditoriaItemDto(
    string Id,
    DateTime Fecha,
    string Accion,
    string AccionEtiqueta,
    string Grupo,
    string GrupoEtiqueta,
    string Tabla,
    string RegistroId,
    string? NombreRegistro,
    AuditoriaAutorDto? Autor);

/// <summary>Un grupo y cuantas operaciones tiene EN TODA la bitacora, no en la pagina.</summary>
public sealed record AuditoriaGrupoDto(string Id, string Etiqueta, int Total);

/// <summary>
/// Un verbo de la bitacora, en palabras, y cuantas actuaciones tiene EN TODA la bitacora.
/// </summary>
/// <remarks>
/// Es lo que llena la lista «Accion» de la pantalla. Hasta la accion se
/// filtraba escribiendo el codigo exacto —«iniciar_sesion»— en un campo de texto, que es un
/// vocabulario controlado pedido como texto libre.
/// </remarks>
public sealed record AuditoriaAccionDto(string Id, string Etiqueta, int Total);

public sealed record AuditoriaRespuestaDto(
    int Total,
    int Pagina,
    int TamanoPagina,
    IReadOnlyList<AuditoriaGrupoDto> Grupos,
    IReadOnlyList<AuditoriaAccionDto> Acciones,
    IReadOnlyList<AuditoriaItemDto> Items);

/// <summary>
/// Una actuacion entera: la linea, y lo que la fila guardo de antes y de despues, campo a campo.
/// </summary>
/// <remarks>
/// Los valores llegan como texto, sea cual sea su tipo en el JSON guardado: la pantalla los enseña
/// en dos columnas —antes, despues— y no opera con ellos. Un objeto anidado llega como su JSON.
/// </remarks>
public sealed record AuditoriaDetalleDto(
    AuditoriaItemDto Actuacion,
    IReadOnlyDictionary<string, string?> ValoresAnteriores,
    IReadOnlyDictionary<string, string?> ValoresNuevos);

public sealed class ExternalLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class ExternalOrganizationCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? IdentificationNumber { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string HeadquartersDepartmentCode { get; set; } = string.Empty;
    public string HeadquartersMunicipalityCode { get; set; } = string.Empty;
}

public sealed record ExternalOrganizationDto(
    string Id,
    string Name,
    string? IdentificationNumber,
    string ContactEmail,
    string HeadquartersDepartmentCode,
    string HeadquartersMunicipalityCode,
    string AdministratorRole,
    string Status);

public sealed record ExternalCsrfTokenResponse(string RequestToken);
public sealed record TokenAntiforgeryRespuesta(string RequestToken);

public sealed class CrearFestivalBorradorSolicitud
{
    public string? BorradorProcesoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Periodicidad { get; set; }
    public string? PeriodicidadDetalle { get; set; }
    public string? CorreoContacto { get; set; }
    public string NivelCobertura { get; set; } = "municipal";
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public IReadOnlyList<int> PracticasMusicalesIds { get; set; } = [];
    public IReadOnlyList<int> TerritoriosSonorosIds { get; set; } = [];

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // EL BLOQUE DE CONTACTO DE LA CABECERA, que `ART_MUS_FESTIVALES` tiene y el canal externo no
    // podia escribir. Las seis columnas existen en `dbo.Festivales` desde el primer dia
    // —InstagramFestival, FacebookFestival, SitioWebFestival, OtroEnlaceFestival, TelefonoFestival—
    // mas ObservacionesContacto, que llego en V20260828_02. Estaban en la base, no estaban en
    // ninguna solicitud, y por eso no habia forma de llenarlas desde fuera.
    //
    // LOS NOMBRES SON LOS DEL VOLCADO y no los nuestros: `PaginaWeb` y no `SitioWeb`,
    // `TelefonoCelular` y no `Telefono`. Lo pidio el usuario.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>`INSTAGRAM` de `ART_MUS_FESTIVALES`.</summary>
    public string? Instagram { get; set; }

    /// <summary>`FACEBOOK` de `ART_MUS_FESTIVALES`.</summary>
    public string? Facebook { get; set; }

    /// <summary>`PAGINA_WEB` de `ART_MUS_FESTIVALES`.</summary>
    public string? PaginaWeb { get; set; }

    /// <summary>`OTRO_ENLACE` de `ART_MUS_FESTIVALES`.</summary>
    public string? OtroEnlace { get; set; }

    /// <summary>`CELULAR` de `ART_MUS_FESTIVALES`.</summary>
    public string? TelefonoCelular { get; set; }

    /// <summary>`OBSERVACIONES_CONTACTO` de `ART_MUS_FESTIVALES`.</summary>
    public string? ObservacionesContacto { get; set; }
}

/// <summary>
/// Los únicos datos de un Festival publicado que la organización puede corregir sin abrir una
/// propuesta institucional. Son datos de contacto y presencia digital; no identifican, ubican ni
/// reclasifican el Festival.
/// </summary>
public sealed class ActualizarContactoPublicoFestivalSolicitud
{
    public string? CorreoContacto { get; set; }
    public string? TelefonoCelular { get; set; }
    public string? Instagram { get; set; }
    public string? Facebook { get; set; }
    public string? PaginaWeb { get; set; }
    public string? OtroEnlace { get; set; }
}

/// <summary>
/// Una edicion de un Festival: 2024, 2025, 2026, cada una con su nombre y sus fechas.
/// </summary>
/// <remarks>
/// NO ES <c>VersionFestivalRow</c>. Aquella es la version del REGISTRO PUBLICADO —la escribe la
/// aprobacion de una propuesta de cambios y el sitio publico lee su fila vigente para sustituir la
/// ficha entera—, y meter ahi ediciones por anyo cambiaria lo que ve cualquier visitante. El motivo
/// completo, con la medida, esta en <c>pnmc-database/schema/V20260827_01__ediciones_festival.sql</c>.
/// </remarks>
public sealed record EdicionFestivalDto(
    string Id,
    string FestivalId,
    int? Anio,
    int? NumeroEdicion,
    string? Nombre,
    string? Descripcion,
    string? FechaInicio,
    string? FechaFin,
    string? Director,
    string EstadoVisibilidad,
    string EstadoVisibilidadEtiqueta,
    bool EsEditable,
    string Estado,
    string EstadoEtiqueta,
    /// <summary>
    /// Cuántas observaciones institucionales tiene, sin abrirla.
    /// </summary>
    /// <remarks>
    /// VIAJA EN LA LISTA PORQUE DECIDE SI LA ACCION EXISTE. Hasta la
    /// lista ofrecía «Consultar observaciones» en todas las filas, también en ediciones que nunca
    /// pasaron por revisión y que por tanto no podían tener ninguna: pulsarlo abría un recuadro
    /// vacío. Una acción solo se muestra cuando de verdad se puede hacer.
    /// </remarks>
    int CuantasObservaciones,
    /// <summary>
    /// Esta edición se puede eliminar de verdad.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>LO DECIDE EL SERVIDOR PORQUE EL CLIENTE NO PUEDE SABERLO.</b> La regla es «un borrador que
    /// nunca llegó a publicarse y sobre el que no hay revisión registrada», y la mitad que no se ve
    /// es la primera: una edición publicada y después despublicada vuelve a <c>EstadoVisibilidad =
    /// borrador</c>, así que mirar el estado de ahora diría que sí se puede, y el público ya la vio.
    /// «Llegó a publicarse alguna vez» solo lo contesta el historial.
    /// </para>
    /// <para>
    /// VIAJA EN LA LISTA para que la acción «Eliminar» se ofrezca solo donde funciona: un botón que
    /// contesta 409 obliga a descubrir por ensayo y error qué se puede hacer con cada fila.
    /// </para>
    /// </remarks>
    bool SePuedeEliminar);

/// <summary>Un municipio donde ocurre la Edicion, con lo que de el se deriva.</summary>
/// <remarks>
/// <para>
/// <b>`RegionOcad` NO SE ELIGE: SE CALCULA.</b> La historia de usuario lo dice —«campo automatico,
/// lo definen los campos seleccionados en Departamento y Municipio»— y sale de
/// <c>dbo.DepartamentosRegionOcad</c>, la tabla de correspondencia Departamento–Region OCAD del
/// Sistema General de Regalias. Esa tabla existia sembrada con sus 32 departamentos desde el 28 de
/// agosto de 2026 y <b>no la consultaba nadie</b>: la region no aparecia por ninguna parte.
/// </para>
/// <para>
/// POR ESO NO HAY COLUMNA. Guardar la region junto al municipio seria guardar dos veces el mismo
/// dato, y el dia que el Sistema General de Regalias reagrupe un departamento quedarian ediciones
/// diciendo una region que ya no es la suya. Se deriva en la lectura, y se muestra de solo lectura.
/// </para>
/// </remarks>
public sealed record EdicionFestivalLocalizacionDto(
    string Id,
    string CodigoDepartamento,
    string? NombreDepartamento,
    string CodigoMunicipio,
    string? NombreMunicipio,
    int? ZonaUrbanoRuralId,
    string? ZonaUrbanoRural,
    int? TitulacionColectivaId,
    string? TitulacionColectiva,
    int? RegionOcadId,
    string? RegionOcad);

/// <summary>Una entidad aliada de la Edicion.</summary>
public sealed record EdicionFestivalEntidadAliadaDto(
    string Id,
    string? Nombre,
    string? Correo,
    int? NaturalezaEntidadId,
    string? NaturalezaEntidad,
    string? EntidadId);

/// <summary>Una pieza del material multimedia: programa, afiche o logo.</summary>
public sealed record EdicionFestivalMaterialDto(
    string Id,
    string? Url,
    string? DescripcionArchivo,
    int OrdenVisualizacion);

/// <summary>
/// La Edicion entera, tal como la pinta su formulario.
/// </summary>
/// <remarks>
/// <para>
/// <b>ANTES ESTA RUTA DEVOLVIA UN OBJETO ANONIMO CON FILAS DE EF DENTRO.</b> Las localizaciones,
/// las aliadas y los materiales viajaban tal como salen del ORM, con <c>edicionFestivalId</c> y
/// <c>fechaCreacion</c> incluidos: el modelo de persistencia puesto en el cable. Y al no haber tipo
/// de contrato, la respuesta no estaba en OpenAPI y nada comprobaba su forma.
/// </para>
/// <para>
/// <b>MES Y DURACION TAMPOCO SE GUARDAN.</b> La historia de usuario los marca obligatorios y
/// autocalculados a partir de fecha de inicio y fecha de fin. Un campo derivado que se almacena es
/// un campo que se puede desincronizar: basta con corregir una fecha por otro camino para que el
/// mes diga lo que ya no es. Se calculan aqui, en el unico sitio que los sirve.
/// </para>
/// </remarks>
public sealed record EdicionFestivalDetalleDto(
    EdicionFestivalDto Edicion,
    /// <summary>«Enero», o «Enero, Febrero» si la Edicion cruza de mes. Derivado.</summary>
    string? MesRealizacion,
    /// <summary>Dias entre inicio y fin, ambos incluidos. Derivado.</summary>
    int? DuracionDias,
    int? TipologiaFestivalId,
    string? OtraTipologia,
    int? FuenteFinanciacionPrimariaId,
    string? OtraFuenteFinanciacionPrimaria,
    int? FuenteFinanciacionSecundariaId,
    string? OtraFuenteFinanciacionSecundaria,
    bool? UsaEstampillaProcultura,
    string? PracticasMusicalesQueCongrega,
    string? OtraModalidadParticipacion,
    string? OtraExpresionArtistica,
    IReadOnlyList<int> PracticasMusicalesIds,
    IReadOnlyList<int> TerritoriosSonorosIds,
    IReadOnlyList<int> ExpresionesArtisticasIds,
    IReadOnlyList<int> ModalidadesParticipacionIds,
    IReadOnlyList<int> TiposIngresoIds,
    IReadOnlyList<EdicionFestivalLocalizacionDto> Localizaciones,
    IReadOnlyList<EdicionFestivalEntidadAliadaDto> EntidadesAliadas,
    IReadOnlyList<EdicionFestivalMaterialDto> Materiales);

/// <summary>El cuerpo del alta y de la edicion. Cinco campos y ni uno mas.</summary>
/// <remarks>
/// NO LLEVA <c>FestivalId</c>: el Festival lo dice la ruta. Un identificador en el cuerpo permitiria
/// mandar una edicion a un Festival distinto del que se esta administrando, y el servidor tendria
/// que decidir cual de los dos manda.
/// </remarks>
public sealed class EdicionFestivalSolicitud
{
    public int? Anio { get; set; }
    public int? NumeroEdicion { get; set; }
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public string? FechaInicio { get; set; }
    public string? FechaFin { get; set; }
    public string? Director { get; set; }
    public string? Estado { get; set; }
    public int? TipologiaFestivalId { get; set; }
    public string? OtraTipologia { get; set; }
    public int? FuenteFinanciacionPrimariaId { get; set; }
    public string? OtraFuenteFinanciacionPrimaria { get; set; }
    public int? FuenteFinanciacionSecundariaId { get; set; }
    public string? OtraFuenteFinanciacionSecundaria { get; set; }
    public bool? UsaEstampillaProcultura { get; set; }
    public string? PracticasMusicalesQueCongrega { get; set; }
    public string? OtraModalidadParticipacion { get; set; }
    public string? OtraExpresionArtistica { get; set; }
    public IReadOnlyList<int> PracticasMusicalesIds { get; init; } = [];
    public IReadOnlyList<int> TerritoriosSonorosIds { get; init; } = [];
    public IReadOnlyList<int> ExpresionesArtisticasIds { get; init; } = [];
    public IReadOnlyList<int> ModalidadesParticipacionIds { get; init; } = [];
    public IReadOnlyList<int> TiposIngresoIds { get; init; } = [];
    public IReadOnlyList<LocalizacionSolicitud> Localizaciones { get; init; } = [];
    public IReadOnlyList<EntidadAliadaSolicitud> EntidadesAliadas { get; init; } = [];
    public IReadOnlyList<ArchivoSolicitud> Materiales { get; init; } = [];
}

/// <summary>Un estado de edicion con su etiqueta, para el desplegable del formulario.</summary>
public sealed record CatalogoEstadoEdicionDto(string Codigo, string Nombre);

public sealed record CatalogoFestivalDto(int Id, string Nombre);

/// <summary>Una organización que la persona que pregunta administra.</summary>
public sealed record OrganizacionAdministradaDto(string Id, string Nombre);

/// <summary>
/// Un proceso que una organización administra, tal como lo lista su ficha en la consola.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES UN TIPO PROPIO Y ANTES NO LO ERA.</b> Hasta la ficha reusaba
/// <see cref="OrganizacionAdministradaDto"/>, que describe otra cosa —las organizaciones que
/// administra una persona—. Dos conceptos con la misma forma acaban con un campo añadido a uno que
/// no significa nada en el otro.
/// </para>
/// <para>
/// <b>LLEVA EL ESTADO.</b> La ficha enseñaba una lista de nombres, de modo que quien iba a archivar
/// la organización veía «3 festivales» sin poder saber si alguno estaba publicado.
/// <c>DejaHuerfano</c> es la consecuencia en una sola marca: si esta organización se cierra, este
/// proceso se queda sin nadie que responda por él.
/// </para>
/// </remarks>
public sealed record ProcesoDeOrganizacionDto(string Id, string Nombre, string Estado, bool DejaHuerfano);

/// <summary>
/// El perfil completo de una organización tal como lo lee y lo edita su propia responsable.
/// </summary>
/// <remarks>
/// <para>
/// POR QUE NO SIRVE <see cref="ExternalOrganizationDto"/>. Aquel devuelve nueve campos y ninguno
/// de los once descriptivos de <c>dbo.Entidades</c> —descripción, teléfono, sitio web, las tres
/// redes, dirección, nombre legal—, así que un panel construido sobre él no puede mostrar ni
/// editar lo que la organización escribió de sí misma. No se amplía aquel: lo llaman otras
/// pantallas y crecerlo les haría cargar datos que no piden.
/// </para>
/// <para>
/// LOS TRES ULTIMOS CAMPOS SON DE SOLO LECTURA Y ESO NO ES UNA CONVENCION DEL FRONT. No existen
/// en <see cref="PerfilOrganizacionSolicitud"/>: no hay por dónde escribirlos. El estado del
/// registro pertenece a un contrato institucional específico, separado de este perfil externo.
/// </para>
/// </remarks>
public sealed record PerfilOrganizacionDto(
    string Id,
    string Nombre,
    string? NombreLegal,
    string? NumeroIdentificacion,
    string? TipoIdentificacion,
    string? Descripcion,
    string? CorreoContacto,
    string? TelefonoContacto,
    string? SitioWeb,
    string? Facebook,
    string? Instagram,
    string? OtroEnlace,
    string? Direccion,
    string? CodigoDepartamentoSede,
    string? NombreDepartamentoSede,
    string? CodigoMunicipioSede,
    string? NombreMunicipioSede,
    string EstadoRegistro,
    string EstadoRegistroEtiqueta,
    DateTime? FechaActualizacion,
    /// <summary>
    /// La foto de perfil, servida por el banco de archivos, o <c>null</c> si no hay.
    /// </summary>
    /// <remarks>
    /// <b>ES PUBLICA</b> y por eso es una dirección de `/publico/archivos/{id}`, que se sirve sin
    /// cuenta y con ETag. Nulo es lo corriente: quien no sube foto sigue viéndose con sus
    /// iniciales, que es el respaldo y no un hueco.
    /// </remarks>
    string? FotoUrl = null,
    int? ArchivoFotoId = null);

/// <summary>
/// Las catorce columnas de <c>dbo.Entidades</c> que una organización puede escribir sobre sí misma.
/// </summary>
/// <remarks>
/// <para>
/// LO QUE NO ESTA AQUI ES LA MITAD QUE IMPORTA, y es una decisión, no un olvido. De las 29 columnas
/// de la tabla quedan fuera quince: <c>IdEntidad</c>, <c>TipoEntidad</c>, <c>EstadoRegistro</c>,
/// <c>EsInstitucional</c>, <c>Activo</c>, <c>IdUsuarioCreador</c>, <c>IdUsuarioResponsable</c>, las
/// cinco columnas de fecha, <c>Latitud</c>, <c>Longitud</c> y <c>TipoIdentificacion</c>. Un campo
/// que no existe en la solicitud no se puede escribir por accidente ni a propósito: no hay
/// asignación que revisar ni lista negra que mantener al día.
/// </para>
/// <para>
/// <c>EstadoRegistro</c> Y <c>EsInstitucional</c> SON LOS DOS PELIGROSOS. El primero es la escalera
/// de moderación: quien pudiera escribirlo se publicaría solo. El segundo tiene un índice único
/// filtrado (<c>UQ_Entidades_EsInstitucional</c>) con una sola fila hoy, y ponerlo a 1 revienta con
/// violación de índice o convierte a esa organización en la responsable por defecto de todo
/// registro sin dueño.
/// </para>
/// </remarks>
public sealed class PerfilOrganizacionSolicitud
{
    public string Nombre { get; set; } = string.Empty;
    public string? NombreLegal { get; set; }
    public string? NumeroIdentificacion { get; set; }
    public string? Descripcion { get; set; }
    public string CorreoContacto { get; set; } = string.Empty;
    public string? TelefonoContacto { get; set; }
    public string? SitioWeb { get; set; }
    public string? Facebook { get; set; }
    public string? Instagram { get; set; }
    public string? OtroEnlace { get; set; }
    public string? Direccion { get; set; }
    public string CodigoDepartamentoSede { get; set; } = string.Empty;
    public string CodigoMunicipioSede { get; set; } = string.Empty;

    /// <summary>
    /// El archivo del banco que se usa como foto de perfil, o <c>null</c> para quitarla.
    /// </summary>
    /// <remarks>
    /// VIAJA EL IDENTIFICADOR Y NO LA DIRECCION. Una dirección en texto es confiar en que alguien
    /// la escribió bien y en que el fichero siga ahí; el identificador apunta a un archivo que el
    /// banco ya validó por su firma y que lleva su texto alternativo.
    /// </remarks>
    public int? ArchivoFotoId { get; set; }
}

/// <summary>
/// La persona natural que responde por una organización, leída desde <c>dbo.EntidadesResponsable</c>.
/// </summary>
/// <remarks>
/// TODO CAMPO DE ESTE DTO ES DATO PERSONAL DE UN TERCERO (Ley 1581 de 2012). Va en una respuesta
/// aparte de <see cref="PerfilOrganizacionDto"/> a propósito: la tabla es un satélite 1:1 y
/// publicar la cédula tiene que exigir escribir un JOIN deliberado, no venir de regalo con el
/// perfil. <c>FichaPublicaSinDatosPersonalesTests</c> se pone en rojo si aparece en una ruta
/// anónima.
/// </remarks>
public sealed record ResponsableOrganizacionDto(
    string IdEntidad,
    string ResponsableNombre,
    string ResponsableTipoDocumento,
    string ResponsableTipoDocumentoEtiqueta,
    string ResponsableNumeroDocumento,
    string ResponsableCorreo,
    string? ResponsableTelefono,
    DateTime ResponsableDesde,
    bool ResponsableAutorizacionDatos);

/// <summary>
/// Los cuatro campos de la persona responsable que se pueden corregir desde el panel.
/// </summary>
/// <remarks>
/// <para>
/// EL CORREO NO ESTA, Y NO ES UN DESCUIDO. <c>AltaDeOrganizacion.CrearAsync</c> copia
/// <c>dbo.Usuarios.CorreoElectronico</c> en <c>ResponsableCorreo</c>: es la credencial con la que
/// esa persona entra. Dejarlo editable aquí desincronizaría el correo de la persona con el de su
/// cuenta, y no hay ninguna ruta que cambie el segundo.
/// </para>
/// <para>
/// <c>ResponsableDesde</c> Y <c>ResponsableAutorizacionDatos</c> TAMPOCO. La fecha marca el inicio
/// del período de responsabilidad; la autorización, revocada sin un flujo de borrado, dejaría el
/// nombre y la cédula guardados sin permiso, que es lo contrario de lo que exige la ley.
/// </para>
/// </remarks>
public sealed class ResponsableOrganizacionSolicitud
{
    public string ResponsableNombre { get; set; } = string.Empty;
    public string ResponsableTipoDocumento { get; set; } = string.Empty;
    public string ResponsableNumeroDocumento { get; set; } = string.Empty;
    public string? ResponsableTelefono { get; set; }
}

/// <summary>Una fila de <c>dbo.TiposDocumento</c>: el código que se guarda y el nombre que se lee.</summary>
public sealed record TipoDocumentoDto(string Codigo, string Nombre);

public sealed record CoincidenciaFestivalHistoricoDto(
    string FestivalId,
    string NombreFestival,
    string? Descripcion,
    string? CodigoDepartamento,
    string? NombreDepartamento,
    string? CodigoMunicipio,
    string? NombreMunicipio,
    string? OrganizadorHistorico,
    string TipoCoincidencia,
    IReadOnlyList<string> Evidencias);

public sealed class GuardarBorradorProcesoSolicitud { public string DatosJson { get; set; } = "{}"; public int? Version { get; set; } }
public sealed record BorradorProcesoDto(string Id, string Dominio, string OrganizacionId, string Estado, string DatosJson, int Version, DateTime FechaActualizacion);
public sealed class CrearReclamacionAdministracionSolicitud { public string FestivalId { get; set; } = string.Empty; public string? BorradorId { get; set; } public string Justificacion { get; set; } = string.Empty; public string? EvidenciasJson { get; set; } public string SenalesJson { get; set; } = "[]"; }
public sealed class SolicitarRetiroFestivalSolicitud { public string Justificacion { get; set; } = string.Empty; }
public sealed class ResponderAclaracionSolicitud { public string Respuesta { get; set; } = string.Empty; }
public sealed class DecidirReclamacionAdministracionSolicitud { public string Accion { get; set; } = string.Empty; public string? Motivo { get; set; } }
public sealed class ResolverConciliacionAdministracionSolicitud { public Dictionary<string, string> Selecciones { get; set; } = []; }
public sealed record ConciliacionAdministracionDto(string ReclamacionId, string ValoresHistoricosJson, string ValoresBorradorJson, string? SeleccionesJson, bool Resuelta);
public sealed record ReclamacionAdministracionDto(string Id, string Dominio, string RegistroCanonicoId, string OrganizacionSolicitanteId, string Estado, string Justificacion, string SenalesJson, string? MotivoDecision, DateTime FechaCreacion, DateTime? FechaEnvio, DateTime? FechaDecision, string? RegistroNombre = null, string? OrganizacionSolicitanteNombre = null);

public sealed record FestivalBorradorDto(
    string Id,
    string Nombre,
    string? Descripcion,
    string Estado,
    string OrganizacionPrincipalId,
    string OrganizacionPrincipalNombre,
    string NivelCobertura,
    string? CodigoDepartamento,
    string? CodigoMunicipio,
    string? Periodicidad,
    string? CorreoContacto,
    IReadOnlyList<CatalogoFestivalDto> PracticasMusicales,
    IReadOnlyList<CatalogoFestivalDto> TerritoriosSonoros,
    string? ObservacionRevision = null,
    string? EstadoPropuesta = null,
    string? ObservacionPropuesta = null,
    // Las seis de la cabecera de `ART_MUS_FESTIVALES` que hasta no salian
    // por el cable. Van al final y con valor por omision para no mover ninguna posicion de las que
    // ya se construyen en otros sitios.
    string? Instagram = null,
    string? Facebook = null,
    string? PaginaWeb = null,
    string? OtroEnlace = null,
    string? TelefonoCelular = null,
    string? ObservacionesContacto = null,
    /// <summary>
    /// Cuantos campos tienen un cambio pedido sin atender.
    /// </summary>
    /// <remarks>
    /// CERO SIGNIFICA DOS COSAS —no hay solicitud de cambios, o esta toda atendida— y a la tarjeta
    /// del panel le da igual: en los dos casos no pinta el aviso. Va al final y con valor por
    /// omision para no mover ninguna posicion de las que ya se construyen en otros sitios.
    /// </remarks>
    int CambiosPedidos = 0,
    // Territorio en nombre -no solo el código-, cuántas ediciones tiene y cuándo se actualizó por
    // última vez. Agregados para que la tarjeta de Mis procesos no
    // tenga que pedir nada más por Festival -mismo criterio que ya evitó el N+1 de CambiosPedidos-.
    string? NombreDepartamento = null,
    string? NombreMunicipio = null,
    int CuantasEdiciones = 0,
    DateTime? FechaActualizacion = null,
    string? EstadoSolicitudRetiro = null,
    string? PeriodicidadDetalle = null);

public sealed record PropuestaCambioFestivalDto(
    string Id,
    string FestivalOrigenId,
    string VersionOrigenId,
    string Estado,
    string Nombre,
    string? Descripcion,
    string NivelCobertura,
    string? CodigoDepartamento,
    string? CodigoMunicipio,
    string? Periodicidad,
    string? CorreoContacto,
    IReadOnlyList<CatalogoFestivalDto> PracticasMusicales,
    IReadOnlyList<CatalogoFestivalDto> TerritoriosSonoros,
    DateTime? FechaEnvioRevision = null,
    string? ObservacionRevision = null,
    // El bloque de contacto, agregado junto con el PUT que ya lo
    // guarda -ver el comentario en `PropuestasDeCambioFestivalExternosEndpoints.cs`-. Mismos nombres
    // que ya usa `GuardarFestivalSolicitud`/`FestivalDeLaOrganizacionDto` en el resto del contrato.
    string? Instagram = null,
    string? Facebook = null,
    string? PaginaWeb = null,
    string? OtroEnlace = null,
    string? TelefonoCelular = null,
    string? ObservacionesContacto = null,
    string? PeriodicidadDetalle = null);

public sealed record PropuestaCambioFestivalRevisionDto(
    string Id,
    string FestivalOrigenId,
    string NombreFestival,
    string OrganizacionNombre,
    string Estado,
    DateTime? FechaEnvioRevision);

public sealed record VersionFestivalInstitucionalDto(
    string Id,
    int NumeroVersion,
    bool EsVigente,
    string Nombre,
    DateTime FechaPublicacion);

public sealed record DistribucionAnaliticaFestivalDto(string Nombre, int Total);

public sealed record ResumenAnaliticoFestivalesDto(
    int TotalFestivales,
    IReadOnlyList<DistribucionAnaliticaFestivalDto> PorDepartamento,
    IReadOnlyList<DistribucionAnaliticaFestivalDto> PorMunicipio,
    IReadOnlyList<DistribucionAnaliticaFestivalDto> PorPracticaMusical,
    IReadOnlyList<DistribucionAnaliticaFestivalDto> PorTerritorioSonoro,
    IReadOnlyList<DistribucionAnaliticaFestivalDto> PorPeriodicidad);

public sealed class DecisionRevisionFestivalSolicitud
{
    public string Accion { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public string? MotivoRechazo { get; set; }
}

public sealed class ArchivarFestivalSolicitud
{
    public string? Motivo { get; set; }
}

public sealed record FestivalRevisionInstitucionalDto(
    string Id,
    string Nombre,
    string OrganizacionPrincipalNombre,
    string NivelCobertura,
    string? CodigoDepartamento,
    string? CodigoMunicipio,
    DateTime? FechaEnvioRevision,
    int NumeroEnvio);

/// <summary>Una edición visible para supervisión institucional posterior.</summary>
public sealed record EdicionRevisionInstitucionalDto(
    string Id,
    string FestivalId,
    string FestivalNombre,
    string OrganizacionPrincipalNombre,
    int? Anio,
    string? Nombre,
    DateTime? FechaEnvioRevision);

/// <summary>Una organizacion que la cuenta administra, tal como la ve su sesion.</summary>
public sealed record ExternalSessionOrganization(string Id, string Name, string Role);

/// <summary>
/// La sesion externa. LLEVA LAS ORGANIZACIONES DENTRO, y no es un adorno.
/// </summary>
/// <remarks>
/// El sitio publico tiene que decidir en cada carga si pinta «Iniciar sesion» o el panel de la
/// organizacion, y ese panel necesita el nombre. Sin las organizaciones aqui, toda pagina del
/// portal tendria que encadenar dos llamadas —/me y luego /externo/organizaciones/mis— solo para
/// dibujar una cabecera, y la segunda depende del resultado de la primera: no se pueden lanzar a
/// la vez. Ademas, desde que el alta es un solo acto, no existe cuenta externa sin organizacion:
/// una sesion que no la trae esta describiendo la cuenta a medias.
/// </remarks>
public sealed record ExternalSessionResponse(
    string UserId,
    string FullName,
    string Email,
    string AccountStatus,
    IReadOnlyList<ExternalSessionOrganization> Organizations,
    /// <summary>
    /// Si esta cuenta ya comprobó que el correo con el que entra es suyo.
    /// </summary>
    /// <remarks>
    /// VIAJA EN LA SESION porque es lo que decide si el panel avisa. Sin este dato, la organización
    /// descubriría el bloqueo al pulsar «Enviar a revisión» y sin saber por qué; con él, el aviso
    /// aparece antes de que lo intente y trae el botón para pedir otro enlace.
    /// </remarks>
    bool CorreoConfirmado,
    /// <summary>
    /// Si esta es la primera vez que la cuenta entra.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SALE DE UN DATO QUE YA ESTABA: <c>Usuarios.LastLoginAt</c>, leído ANTES de sobrescribirlo con
    /// la hora de ahora. No hace falta columna nueva ni migración; lo único que hacía falta era
    /// mirarlo antes de pisarlo.
    /// </para>
    /// <para>
    /// SOLO ES CIERTO EN LA RESPUESTA DEL INGRESO. <c>/externo/me</c> lo devuelve siempre en falso,
    /// y es correcto: para entonces la cuenta ya entró, y una bienvenida que reaparece en cada
    /// recarga deja de ser una bienvenida. El §15.2 pide enseñarla «tras el primer login», no cada
    /// vez que se abre el panel; lo que sí es permanente es el indicador del §15.3.
    /// </para>
    /// </remarks>
    bool EsPrimerIngreso
);

public sealed record FestivalDto(
    string Id,
    string Name,
    string DepartmentCode,
    string DepartmentName,
    string MunicipalityCode,
    string MunicipalityName,
    string CoverageLevel,
    string Description,
    string SpecificLocation,
    int VersionsCount,
    string LastEditionDate,
    string OrganizerDisplayName,
    // SIN CORREO NI TELEFONO, y no por olvido. Este DTO lo construye UNA sola ruta,
    // `GET /api/v1/festivals` (CatalogModuleEndpoints.cs), que esta declarada AllowAnonymous.
    // Llevaba `ContactEmail` y `ContactPhone` del Festival hasta, cuando
    // la direccion de producto decidio que la lectura publica no lleva datos personales. Se quitan
    // del CONTRATO y no se rellenan en blanco: un campo que siempre viaja vacio invita a que
    // alguien lo vuelva a rellenar. El sitio web se queda —es presencia publica del Festival.
    string WebsiteUrl,
    bool HasCurrentYearEdition,
    string CurrentYearEditionStatus,
    string CurrentYearStartDate,
    string CurrentYearEndDate,
    string SonorousTerritories,
    string MusicalPractices
);

public sealed record TerritorioPrincipalPublicoDto(
    string? Departamento,
    string? Municipio,
    string NivelCobertura);

/// <summary>La ficha publica de un Festival. ANONIMA: la lee cualquiera, sin sesion.</summary>
/// <remarks>
/// <para>
/// LO QUE NO ESTA AQUI ES LA PARTE IMPORTANTE. Hasta este DTO llevaba
/// <c>CorreoContacto</c>, <c>TelefonoContacto</c> y <c>Director</c> —el nombre de una persona
/// natural—, y el comentario que ocupaba este sitio los defendia diciendo que «nada de esto es
/// dato de una persona natural». Era falso de <c>Director</c>, y de los otros dos era discutible:
/// un celular y un correo identifican a alguien aunque los publique una organizacion.
/// </para>
/// <para>
/// LA REGLA LA FIJO EL DUENO DEL PROYECTO ese mismo dia, al pedir la ficha publica completa:
/// «todo dato personal o correo se quita de la version publica, solo se deja redes sociales del
/// festival, nada mas, el resto es privado de la organizacion». Los cuatro enlaces que quedan son
/// presencia publica del Festival, no de una persona.
/// </para>
/// <para>
/// QUIEN LO VIGILA: <c>FichaPublicaSinDatosPersonalesTests</c>, que busca el VALOR sembrado dentro
/// del JSON en crudo. Volver a colar un correo aqui —o dentro de un objeto anidado, o bajo otro
/// nombre— pone esa prueba en rojo.
/// </para>
/// </remarks>
public sealed record FestivalPublicoDto(
    string Id,
    string Nombre,
    string? Descripcion,
    string? OrganizacionResponsable,
    TerritorioPrincipalPublicoDto TerritorioPrincipal,
    string? Periodicidad,
    IReadOnlyList<CatalogoFestivalDto> PracticasMusicales,
    IReadOnlyList<CatalogoFestivalDto> TerritoriosSonoros,

    // Presencia publica del Festival. Sale de la VERSION vigente, no de la cabecera: la cabecera
    // es el presente y la version es lo publicado.
    string? Instagram,
    string? Facebook,
    string? SitioWeb,
    string? OtroEnlace,
    string? PeriodicidadDetalle = null,

    // CUANDO OCURRE, que es la otra mitad de la circulación cultural. El geovisor tenía en su ficha
    // un apartado «Mes de Realización» que no podía llenarse: el dato existe en la cabecera del
    // Festival —`CurrentYearStartDate`— y no cruzaba al DTO, así que la sección salía siempre vacía.
    //
    // NO ES UNA APERTURA NUEVA DE DATOS: las fechas de cada edición ya son públicas por
    // `EdicionFestivalPublicaDto`, que las sirve en la ruta de ediciones. Esto sólo evita tener que
    // pedir las ediciones de quinientos Festivales para saber en qué mes cae cada uno.
    DateOnly? FechaInicioVigente = null,
    DateOnly? FechaFinVigente = null);

/// <summary>Valores canónicos para describir la recurrencia de un Festival.</summary>
public static class PeriodicidadesFestival
{
    public const string Anual = "anual";
    public const string Semestral = "semestral";
    public const string Trimestral = "trimestral";
    public const string Bianual = "bianual";
    public const string Bienal = "bienal";
    public const string Trienal = "trienal";
    public const string OtraRegular = "otra_regular";
    public const string Intermitente = "intermitente";

    public static readonly IReadOnlySet<string> Todas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Anual, Semestral, Trimestral, Bianual, Bienal, Trienal, OtraRegular, Intermitente,
    };

    public static bool RequiereDetalle(string? periodicidad) =>
        string.Equals(periodicidad, OtraRegular, StringComparison.OrdinalIgnoreCase)
        || string.Equals(periodicidad, Intermitente, StringComparison.OrdinalIgnoreCase);

    public static string? Normalizar(string? periodicidad) =>
        string.IsNullOrWhiteSpace(periodicidad) ? null : periodicidad.Trim().ToLowerInvariant();
}

public sealed record MusicSchoolDto(
    string Id,
    string Name,
    string DepartmentCode,
    string DepartmentName,
    string MunicipalityCode,
    string MunicipalityName,
    string CoverageLevel,
    string SpecificLocation,
    string AddressText,
    string SchoolType,
    string SchoolCategory,
    bool IsActiveSchool,
    int StudentsTotal,
    int ActiveGroupsCount,
    bool HasCommunityOrganization,
    string TrainingProcesses,
    string MusicalPractices,
    string ResponsibleEntityDisplayName,
    string ContactEmail,
    string ContactPhone,
    string WebsiteUrl,
    string SonorousTerritories
);

public sealed record MusicMarketDto(
    string Id,
    string Name,
    string DepartmentCode,
    string DepartmentName,
    string MunicipalityCode,
    string MunicipalityName,
    string CoverageLevel,
    string Description,
    string Periodicity,
    int EditionsCount,
    bool HasAssociatedFestival,
    string AssociatedFestivalDisplayName,
    string ScopeType,
    string MarketMode,
    string ResponsibleEntityDisplayName,
    string ResponsibleEntityContactEmail,
    string ResponsibleEntityContactPhone,
    string ResponsibleEntityWebsiteUrl,
    bool HasCurrentYearEdition,
    string CurrentYearEditionStatus,
    string CurrentYearStartDate,
    string CurrentYearEndDate,
    string SpecificLocation,
    string SonorousTerritories,
    string MusicalPractices,
    /// <summary>
    /// El sitio web del mercado, con el nombre que el frontend lee (<c>websiteUrl</c>).
    /// Duplica <see cref="ResponsibleEntityWebsiteUrl"/> a proposito: hasta el 23 ago 2026
    /// el mapa y el directorio publico mostraban «sitio web» siempre vacio para los
    /// 30 mercados porque el front buscaba <c>websiteUrl</c> y el DTO solo traia el
    /// nombre largo. Renombrar habria roto a quien ya leyera el largo; anadir no rompe.
    /// </summary>
    string WebsiteUrl
);

public sealed record OrganizationDto(
    string Id,
    string Name,
    string DepartmentCode,
    string DepartmentName,
    string MunicipalityCode,
    string MunicipalityName,
    string OrganizationType,
    string TerritorialScope,
    string ContactEmail,
    string ContactPhone,
    string SonorousTerritories,
    string MusicalPractices,
    decimal? Latitude,
    decimal? Longitude,
    string Description,
    /// <summary>Sitio web de la red. La fila lo tenia (RedesDocumentacion.SitioWeb) y el DTO no lo serializaba: 30 URLs invisibles hasta el 23 ago 2026.</summary>
    string WebsiteUrl
);

public sealed record SpaceInfrastructureDto(
    string Id,
    string Name,
    string DepartmentCode,
    string DepartmentName,
    string MunicipalityCode,
    string MunicipalityName,
    string ActorType,
    string PrimaryFunction,
    int MaxCapacityApprox,
    string SonorousTerritories,
    string MusicalPractices,
    decimal? Latitude,
    decimal? Longitude,
    string Description,
    string ContactEmail,
    string ContactPhone,
    /// <summary>Sitio web del lutier o espacio. Misma historia que en OrganizationDto: la fila lo tenia y el DTO no lo exponia.</summary>
    string WebsiteUrl
);

public sealed record EscenarioDto(
    string Id,
    string Name,
    string DepartmentCode,
    string DepartmentName,
    string MunicipalityCode,
    string MunicipalityName,
    string CoverageLevel,
    string VenueType,
    string Description,
    /// <summary>Aforo aproximado. Nulo es «no consta»; cero seria una afirmacion distinta.</summary>
    int? MaxCapacityApprox,
    /// <summary>Si, no o —cuando es nulo— no consta. El front tiene que distinguir los tres.</summary>
    bool? HasAccessibility,
    string Dotacion,
    string AddressText,
    string SpecificLocation,
    decimal? Latitude,
    decimal? Longitude,
    /// <summary>Contacto que el escenario publica a proposito. No es dato de una persona natural en el sentido.</summary>
    string ContactEmail,
    string ContactPhone,
    string WebsiteUrl,
    string FacebookUrl,
    string InstagramUrl,
    string OtherUrl
);

public sealed record DivipolaLocationDto(
    string DepartmentCode,
    string DepartmentName,
    string MunicipalityCode,
    string MunicipalityName,
    string LocationType,
    decimal? Latitude,
    decimal? Longitude
);

// =================================================================================================
// LA FICHA COMPLETA DE UNA VERSION DE FESTIVAL
// =================================================================================================
//
// DE DONDE SALE CADA CAMPO. De `ART_MUS_FESTIVALES_VERSION` del volcado de SIMUS
// (`Scripts/script_festivales_simus.sql`), 31 columnas, mas sus seis tablas puente. Hasta el 28 de
// agosto de 2026 el canal externo solo sabia leer y escribir ONCE campos de un Festival —nombre,
// descripcion, correo, periodicidad, alcance, departamento, municipio, y las dos listas de
// practicas y territorios—, asi que veinte campos del modelo no tenian por donde entrar ni salir.
//
// ES LA VERSION Y NO LA CABECERA lo que lleva casi todo. La cabecera `dbo.Festivales` es el
// presente del registro; la version es una edicion concreta, con SUS fechas, SU nombre, SUS
// catalogos y SU estado de revision. Un festival que en 2024 fue municipal y en 2025 nacional no
// cabe en la cabecera y si cabe en dos versiones.

/// <summary>Un municipio de una version, con su zona y su titulacion colectiva.</summary>
/// <remarks>
/// SON VARIOS POR VERSION. <c>dbo.Festivales</c> tiene un solo par departamento/municipio atado al
/// alcance por <c>CK_Festivales_NivelCobertura</c>; un Festival que ocurre en tres municipios no
/// cabe ahi. <c>ART_MUS_LOCALIZACIONXVERSION</c> es la tabla que lo permite.
/// </remarks>
public sealed record LocalizacionVersionDto(
    long Id,
    string CodigoDepartamento,
    string? NombreDepartamento,
    string CodigoMunicipio,
    string? NombreMunicipio,
    int? ZonaUrbanoRuralId,
    string? ZonaUrbanoRural,
    int? TitulacionColectivaId,
    string? TitulacionColectiva);

/// <summary>Una entidad aliada del Festival.</summary>
/// <remarks>
/// <c>EntidadId</c> ES NULABLE: la mayoria de las aliadas no estan registradas en
/// <c>dbo.Entidades</c>, y exigirlo dejaria fuera al aliado real.
/// </remarks>
public sealed record EntidadAliadaVersionDto(
    long Id,
    string? Nombre,
    string? Correo,
    int? NaturalezaEntidadId,
    string? NaturalezaEntidad);

/// <summary>Afiche, programa o logo de una version.</summary>
/// <remarks>
/// <c>Url</c> Y NO UN FICHERO: el API no escribe en disco y no hay ninguna ruta de subida. Lo unico
/// que hoy se puede guardar es una direccion. <c>ArchivoId</c> existe en la tabla para el dia que
/// la haya.
/// </remarks>
public sealed record ArchivoVersionDto(
    long Id,
    string RolArchivo,
    string? Url,
    int OrdenVisualizacion,
    /// <summary>`DESCRIPCION_ARCHIVO` del volcado de SIMUS.</summary>
    string? DescripcionArchivo = null);

/// <summary>Una version en la lista, sin sus catalogos: lo justo para elegir cual abrir.</summary>
public sealed record ResumenVersionFestivalDto(
    int Id,
    int NumeroVersion,
    string Nombre,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    bool EsVigente,
    string? EstadoRegistro,
    string? EstadoRegistroEtiqueta);

/// <summary>Todo lo que el modelo de SIMUS guarda de una edicion de un Festival.</summary>
public sealed record FichaVersionFestivalDto(
    int Id,
    int FestivalId,
    string FestivalNombre,
    int NumeroVersion,
    bool EsVigente,
    string Nombre,
    string? Descripcion,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,

    // Que clase de evento es
    int? TipologiaFestivalId,
    string? Tipologia,
    string? OtraTipologia,

    // Con que se paga
    int? FuenteFinanciacionPrimariaId,
    string? FuenteFinanciacionPrimaria,
    string? OtraFuenteFinanciacionPrimaria,
    int? FuenteFinanciacionSecundariaId,
    string? FuenteFinanciacionSecundaria,
    string? OtraFuenteFinanciacionSecundaria,
    bool? UsaEstampillaProcultura,

    // Direccion artistica de ESTA edicion. La organizacion administradora y el contacto son del Festival.
    string? Director,

    // Que suena y quien participa
    string? PracticasMusicalesQueCongrega,
    string? OtraModalidadParticipacion,
    string? OtraExpresionArtistica,
    IReadOnlyList<CatalogoFestivalDto> PracticasMusicales,
    IReadOnlyList<CatalogoFestivalDto> TerritoriosSonoros,
    IReadOnlyList<CatalogoFestivalDto> ExpresionesArtisticas,
    IReadOnlyList<CatalogoFestivalDto> ModalidadesParticipacion,
    IReadOnlyList<CatalogoFestivalDto> TiposIngreso,

    // Donde ocurre, con quien, y con que material
    IReadOnlyList<LocalizacionVersionDto> Localizaciones,
    IReadOnlyList<EntidadAliadaVersionDto> EntidadesAliadas,
    IReadOnlyList<ArchivoVersionDto> Archivos,

    // En que punto de la revision va
    string? EstadoRegistro,
    string? EstadoRegistroEtiqueta,
    string? ObservacionesRechazo,
    bool EsEditable,
    DateTime FechaCreacion);

/// <summary>Lo que el canal externo puede escribir de una version.</summary>
/// <remarks>
/// NO LLEVA <c>EstadoRegistro</c> NI <c>EsVigente</c> NI <c>ObservacionesRechazo</c>, y esa
/// ausencia es la guarda: el estado lo mueve la revision institucional y la vigencia la decide la
/// aprobacion. Un campo que el formulario no arma no se puede colar en la solicitud, que es la
/// misma regla con la que <c>PerfilOrganizacionSolicitud</c> deja fuera el estado del registro.
/// </remarks>
public sealed record GuardarVersionFestivalSolicitud
{
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public DateOnly? FechaInicio { get; init; }
    public DateOnly? FechaFin { get; init; }

    public int? TipologiaFestivalId { get; init; }
    public string? OtraTipologia { get; init; }

    public int? FuenteFinanciacionPrimariaId { get; init; }
    public string? OtraFuenteFinanciacionPrimaria { get; init; }
    public int? FuenteFinanciacionSecundariaId { get; init; }
    public string? OtraFuenteFinanciacionSecundaria { get; init; }
    public bool? UsaEstampillaProcultura { get; init; }

    /// <summary>Director o directora de esta edicion, como texto publico libre.</summary>
    public string? Director { get; init; }

    public string? PracticasMusicalesQueCongrega { get; init; }
    public string? OtraModalidadParticipacion { get; init; }
    public string? OtraExpresionArtistica { get; init; }

    public IReadOnlyList<int> PracticasMusicalesIds { get; init; } = [];
    public IReadOnlyList<int> TerritoriosSonorosIds { get; init; } = [];
    public IReadOnlyList<int> ExpresionesArtisticasIds { get; init; } = [];
    public IReadOnlyList<int> ModalidadesParticipacionIds { get; init; } = [];
    public IReadOnlyList<int> TiposIngresoIds { get; init; } = [];

    public IReadOnlyList<LocalizacionSolicitud> Localizaciones { get; init; } = [];
    public IReadOnlyList<EntidadAliadaSolicitud> EntidadesAliadas { get; init; } = [];
    public IReadOnlyList<ArchivoSolicitud> Archivos { get; init; } = [];
}

public sealed record LocalizacionSolicitud(
    string CodigoDepartamento,
    string CodigoMunicipio,
    int? ZonaUrbanoRuralId,
    int? TitulacionColectivaId);

public sealed record EntidadAliadaSolicitud(
    string? Nombre,
    string? Correo,
    int? NaturalezaEntidadId,
    int? EntidadId);

/// <summary>Una pieza de material multimedia: su direccion y su descripcion.</summary>
/// <remarks>
/// <para>
/// SON LAS DOS COLUMNAS DE `ART_MUS_MATERIALMULTIMEDIA`, ni una mas: URL_ARCHIVO y
/// DESCRIPCION_ARCHIVO. `RolArchivo` es nuestro y no del volcado, asi que deja de ser obligatorio:
/// quien no lo mande recibe «material», que es el valor por omision que pone
/// `V20260828_03__festival_material_descripcion.sql`.
/// </para>
/// </remarks>
public sealed record ArchivoSolicitud(string? Url, string? DescripcionArchivo = null, string? RolArchivo = null);

/// <summary>Lo que envía quien cambia su propia contraseña en el primer ingreso.</summary>
/// <remarks>
/// LA ACTUAL SE PIDE AUNQUE HAYA SESION: una sesión abierta en un equipo prestado no puede bastar
/// para cambiar la contraseña de alguien.
/// </remarks>
public sealed class CambioDeContrasenaPropia
{
    public string? Actual { get; set; }
    public string? Nueva { get; set; }
}

/// <summary>Los datos con los que una persona dice quién es, en su primer ingreso.</summary>
/// <remarks>
/// EL NOMBRE VA PARTIDO EN CUATRO, igual que en el alta externa: el proyecto pide la misma
/// estructura de nombre en todos sus formularios.
/// </remarks>
public sealed class PerfilPropio
{
    public string? PrimerNombre { get; set; }
    public string? SegundoNombre { get; set; }
    public string? PrimerApellido { get; set; }
    public string? SegundoApellido { get; set; }
    public string? TipoDocumento { get; set; }
    public string? Identificacion { get; set; }
    public string? Telefono { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════════════════════
// Mercados musicales
// ═══════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Un mercado musical, tal como lo lee la consola.</summary>
/// <remarks>
/// <b>EL FESTIVAL VIAJA COMO IDENTIFICADOR Y COMO NOMBRE, PERO SOLO SE GUARDA UNO.</b> El nombre es
/// derivado: se lee del festival relacionado en cada consulta. Es lo contrario de la tabla heredada,
/// que guardaba los dos y dejaba que divergieran.
/// </remarks>
public sealed record MercadoDto(
    int Id,
    string Nombre,
    string? Descripcion,
    int? AlcanceId,
    string? Alcance,
    int? ModalidadId,
    string? Modalidad,
    string? Periodicidad,
    string? PeriodicidadDetalle,
    string? CorreoMercado,
    string? TelefonoMercado,
    string? SitioWebMercado,
    string? InstagramMercado,
    string? FacebookMercado,
    string? OtroEnlaceMercado,
    string? ObservacionesContacto,
    string NivelCobertura,
    string? CodigoDepartamento,
    string? NombreDepartamento,
    string? CodigoMunicipio,
    string? NombreMunicipio,
    string? LugarEspecifico,
    bool SeRealizaEnElMarcoDeUnFestival,
    int? FestivalId,
    string? FestivalNombre,
    int OrganizacionId,
    string? OrganizacionNombre,
    string EstadoRegistro,
    int NumeroDeEdiciones,
    DateTime FechaCreacion,
    DateTime? FechaActualizacion,
    DateTime? FechaPublicacion,
    /// <summary>Las prácticas musicales del mercado, del mismo catálogo que usa un Festival.</summary>
    IReadOnlyList<CatalogoDeMercadoDto> PracticasMusicales,
    /// <summary>Los territorios sonoros del mercado, del mismo catálogo que usa un Festival.</summary>
    IReadOnlyList<CatalogoDeMercadoDto> TerritoriosSonoros
);

/// <summary>Un valor de los catálogos compartidos del Ecosistema —prácticas musicales, territorios sonoros—.</summary>
public sealed record CatalogoDeMercadoDto(int Id, string Nombre);

/// <summary>
/// Un movimiento del mercado, tal como lo lee la organización en su ficha.
/// </summary>
/// <remarks>
/// <b>LA MISMA FORMA QUE EL HISTORIAL DE UN FESTIVAL</b>, para que la ficha de un mercado se lea
/// igual que la de un festival: qué módulo se movió, qué pasó, entre qué estados y con qué
/// comentario. Los estados y el comentario son opcionales porque no todo movimiento cambia de
/// estado ni lleva motivo escrito.
/// </remarks>
public sealed record EntradaDeHistorialDeMercadoDto(
    string Modulo,
    string Accion,
    string? EstadoAnterior,
    string? EstadoNuevo,
    string? Comentario,
    DateTime Fecha);

/// <summary>
/// La ficha pública de un mercado: lo que sale sin sesión.
/// </summary>
/// <remarks>
/// <b>SIN CORREO, TELEFONO NI OBSERVACIONES DE CONTACTO</b>, por la misma regla que la ficha pública
/// de un Festival: «todo dato personal o correo se quita de la versión pública, solo se deja redes
/// sociales». Hasta las rutas públicas de mercados devolvían el DTO
/// entero, correo y teléfono incluidos, aunque ninguna pantalla los pintara: el API los entregaba.
/// </remarks>
public sealed record MercadoPublicoDto(
    int Id,
    string Nombre,
    string? Descripcion,
    string? Alcance,
    string? Modalidad,
    string? Periodicidad,
    string? PeriodicidadDetalle,
    string? SitioWebMercado,
    string? InstagramMercado,
    string? FacebookMercado,
    string? OtroEnlaceMercado,
    string NivelCobertura,
    string? CodigoDepartamento,
    string? NombreDepartamento,
    string? CodigoMunicipio,
    string? NombreMunicipio,
    string? LugarEspecifico,
    bool SeRealizaEnElMarcoDeUnFestival,
    int? FestivalId,
    string? FestivalNombre,
    string? OrganizacionNombre,
    int NumeroDeEdiciones,
    DateTime? FechaPublicacion,
    IReadOnlyList<CatalogoDeMercadoDto> PracticasMusicales,
    IReadOnlyList<CatalogoDeMercadoDto> TerritoriosSonoros
);

/// <summary>Lo que la consola manda para crear o guardar un mercado.</summary>
public sealed class MercadoUpsertRequest
{
    /// <summary>Identificadores del catálogo de prácticas musicales. Vacío si no aplica.</summary>
    public List<int> PracticasMusicalesIds { get; set; } = [];
    /// <summary>Identificadores del catálogo de territorios sonoros. Vacío si no aplica.</summary>
    public List<int> TerritoriosSonorosIds { get; set; } = [];
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? AlcanceId { get; set; }
    public int? ModalidadId { get; set; }
    public string? Periodicidad { get; set; }
    public string? PeriodicidadDetalle { get; set; }
    public string? CorreoMercado { get; set; }
    public string? TelefonoMercado { get; set; }
    public string? SitioWebMercado { get; set; }
    public string? InstagramMercado { get; set; }
    public string? FacebookMercado { get; set; }
    public string? OtroEnlaceMercado { get; set; }
    public string? ObservacionesContacto { get; set; }
    public string NivelCobertura { get; set; } = string.Empty;
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? LugarEspecifico { get; set; }
    public bool SeRealizaEnElMarcoDeUnFestival { get; set; }
    public int? FestivalId { get; set; }
    public int OrganizacionId { get; set; }
}

/// <summary>Un valor de un vocabulario controlado de mercados.</summary>
public sealed record OpcionDeMercadoDto(int Id, string Nombre, string Slug);

/// <summary>Los vocabularios que necesita el formulario de un mercado.</summary>
public sealed record CatalogosDeMercadoDto(
    IReadOnlyList<OpcionDeMercadoDto> Alcances,
    IReadOnlyList<OpcionDeMercadoDto> Modalidades,
    IReadOnlyList<CatalogoDeMercadoDto> PracticasMusicales,
    IReadOnlyList<CatalogoDeMercadoDto> TerritoriosSonoros
);

/// <summary>Un festival que un mercado puede declarar como marco.</summary>
/// <remarks>
/// <b>LLEVA SU ESTADO Y NO LO USA PARA EXCLUIR.</b> Un festival en borrador se puede elegir: la
/// condición es existir y pertenecer a la misma organización. El estado viaja para que la pantalla
/// pueda decir «(en borrador)» al lado del nombre, que es información, no un filtro.
/// </remarks>
public sealed record FestivalElegibleDto(int Id, string Nombre, string EstadoRegistro);

/// <summary>La decisión del Programa sobre un mercado que llegó a revisión.</summary>
/// <remarks>
/// <b>EL MOTIVO ES OBLIGATORIO AL PEDIR AJUSTES Y SOBRA AL PUBLICAR.</b> «Ajustes solicitados» sin
/// decir cuáles devuelve el registro a la organización sin nada que corregir, y es el defecto que
/// convierte un circuito de revisión en un rebote.
/// </remarks>
public sealed class DecisionSobreMercado
{
    public string Decision { get; set; } = string.Empty;
    public string? Motivo { get; set; }
}

/// <summary>Lo que la consola envía para retirar del ecosistema un mercado publicado.</summary>
/// <remarks>
/// EL MOTIVO NO ES OPCIONAL, y por eso es una clase propia y no un <c>DecisionSobreMercado</c> con
/// una tercera decisión: ahí el motivo solo hace falta para pedir ajustes, y aquí siempre. Quitar
/// del ecosistema un proceso publicado es de las decisiones que dentro de un año nadie recordará
/// por qué se tomaron.
/// </remarks>
public sealed class ArchivarMercadoSolicitud
{
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>
/// Lo que una organización envía para corregir los datos públicos de contacto de su mercado.
/// </summary>
/// <remarks>
/// <b>ES UNA CORRECCION MENOR Y POR ESO TIENE PUERTA PROPIA.</b> Un mercado publicado no se edita
/// —los cambios sobre lo publicado tienen su propio circuito—, pero un correo que rebota o un
/// teléfono que ya no contesta no pueden esperar a ese circuito: quien intenta contactar al mercado
/// desde el portal no llega a nadie mientras tanto. Es el mismo trato que tiene un Festival.
/// </remarks>
public sealed class ContactoPublicoDeMercadoSolicitud
{
    public string? CorreoMercado { get; set; }
    public string? TelefonoMercado { get; set; }
    public string? SitioWebMercado { get; set; }
    public string? InstagramMercado { get; set; }
    public string? FacebookMercado { get; set; }
    public string? OtroEnlaceMercado { get; set; }
    public string? ObservacionesContacto { get; set; }
}

/// <summary>Lo que una organización envía para pedir el retiro de su mercado publicado.</summary>
/// <remarks>
/// LA JUSTIFICACION NO ES OPCIONAL: quien decide sobre la solicitud no conoce el mercado, y una
/// petición sin motivo no se puede aprobar ni negar con criterio.
/// </remarks>
public sealed class SolicitarRetiroDeMercadoSolicitud
{
    public string Justificacion { get; set; } = string.Empty;
}

/// <summary>Un evento que una organización envió a la agenda y espera decisión.</summary>
/// <remarks>
/// <b>LLEVA LO QUE LE FALTA PARA PUBLICARSE.</b> Quien revisa ve al abrir la lista cuáles puede
/// publicar y cuáles hay que devolver, en vez de enterarse al pulsar el botón.
/// </remarks>
public sealed record EventoEnRevisionDto(
    string Id,
    string Titulo,
    string Descripcion,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    string Modalidad,
    string? Lugar,
    string? Url,
    string NivelCobertura,
    int? FestivalId,
    string? FestivalNombre,
    string? OrganizacionNombre,
    DateTime FechaEnvio,
    string? LoQueFaltaParaPublicar
);

/// <summary>La decisión del Programa sobre un evento que llegó a revisión.</summary>
public sealed class DecisionSobreEvento
{
    public string Decision { get; set; } = string.Empty;
    public string? Motivo { get; set; }
}

/// <summary>Una realización concreta de un mercado musical.</summary>
/// <remarks>
/// <b>DOS ESTADOS, Y NO SON LO MISMO.</b> <c>Estado</c> es el ciclo real del acontecimiento —se
/// prepara, está programado, ya ocurrió, se canceló—; <c>EstadoVisibilidad</c> es si se enseña o no.
/// Un mercado cancelado que SIGUE publicado es información legítima, y con un solo eje habría que
/// elegir entre decir que se canceló y decir que se ve.
/// </remarks>
public sealed record EdicionDeMercadoDto(
    int Id,
    int MercadoId,
    int Anio,
    int? NumeroEdicion,
    string? Nombre,
    string? Descripcion,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    string? CodigoDepartamento,
    string? NombreDepartamento,
    string? CodigoMunicipio,
    string? NombreMunicipio,
    string? LugarEspecifico,
    string Estado,
    string EstadoVisibilidad,
    DateTime FechaCreacion,
    DateTime? FechaActualizacion,
    DateTime? FechaPublicacion
);

/// <summary>Lo que se manda para crear o guardar una edición de mercado.</summary>
public sealed class EdicionDeMercadoUpsertRequest
{
    public int Anio { get; set; }
    public int? NumeroEdicion { get; set; }
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public string? CodigoDepartamento { get; set; }
    public string? CodigoMunicipio { get; set; }
    public string? LugarEspecifico { get; set; }
    public string Estado { get; set; } = "en_preparacion";
    public string EstadoVisibilidad { get; set; } = "borrador";
}
