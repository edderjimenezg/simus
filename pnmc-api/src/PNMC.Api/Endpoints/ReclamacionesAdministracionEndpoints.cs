using System.Security.Claims;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>Reclamaciones de administración: no modifica contenido ni versiones del Festival.</summary>
public static class ReclamacionesAdministracionEndpoints
{
    private const string Festival = Modulos.Festivales;
    private static readonly string[] Activas = ["borrador", "enviada", "en_revision", "requiere_aclaracion", "aclaracion_enviada", "aprobada"];

    public static RouteGroupBuilder MapReclamacionesAdministracionEndpoints(this RouteGroupBuilder api)
    {
        var externo = api.MapGroup("/externo").WithTags("reclamaciones-administracion").RequireAuthorization(SimusAuthentication.ExternalPolicy);
        externo.MapGet("/organizaciones/{organizacionId:int}/festivales/borrador", ObtenerOCrearBorrador);
        externo.MapGet("/organizaciones/{organizacionId:int}/festivales/coincidencias-historicas", BuscarCoincidencias);
        externo.MapPut("/borradores-proceso/{id:long}", GuardarBorrador);
        externo.MapPost("/organizaciones/{organizacionId:int}/reclamaciones-administracion", Crear);
        externo.MapGet("/organizaciones/{organizacionId:int}/reclamaciones-administracion", ListarPropias);
        externo.MapPost("/reclamaciones-administracion/{id:long}/aclaraciones/responder", ResponderAclaracion);
        externo.MapPost("/reclamaciones-administracion/{id:long}/cancelar", Cancelar);
        externo.MapGet("/reclamaciones-administracion/{id:long}/conciliacion", ObtenerConciliacion);
        externo.MapPost("/reclamaciones-administracion/{id:long}/conciliacion/resolver", ResolverConciliacion);

        var institucional = api.MapGroup("/institucional/reclamaciones-administracion").WithTags("reclamaciones-administracion").RequireAuthorization(Permisos.PoliticaFuncionario);
        institucional.MapGet(string.Empty, ListarInstitucional);
        institucional.MapPost("/{id:long}/aclaraciones", PedirAclaracion);
        institucional.MapPost("/{id:long}/decision", Decidir);
        return api;
    }

    /// <summary>
    /// Los Festivales que ya existen y se parecen al que se está registrando.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ANTES SOLO MIRABA LOS DE LA ENTIDAD INSTITUCIONAL, Y ESA ENTIDAD NO TIENE NINGUNO.</b>
    /// Medido contra PNMC_LOCAL: los 186 Festivales pertenecen a
    /// corporaciones reales y el Plan Nacional de Música para la Convivencia —la entidad
    /// institucional, id 1— es dueño de CERO. Es decir, esta búsqueda no podía devolver nada nunca,
    /// y el paso 3 del asistente enseñaba siempre «ninguna coincidencia» sobre una base donde ya
    /// había un duplicado: «Festival Huila» aparece dos veces en el municipio 11001.
    /// </para>
    /// <para>
    /// <b>SON DOS PREGUNTAS DISTINTAS Y ANTES SOLO SE HACIA UNA.</b>
    /// </para>
    /// <list type="bullet">
    /// <item><b>¿Este Festival ya existe como registro histórico sin dueño?</b> Entonces se puede
    /// RECLAMAR su administración, que es para lo que existe el resto de este fichero. Son los de la
    /// entidad institucional.</item>
    /// <item><b>¿Este Festival ya lo registró alguien?</b> Entonces NO se reclama —ya tiene quien lo
    /// administre— y lo que hace falta es no registrarlo dos veces. Esta mitad faltaba entera.</item>
    /// </list>
    /// <para>
    /// LA DISTINCION VIAJA EN <c>TipoCoincidencia</c> y decide lo que la pantalla ofrece: el
    /// formulario de reclamación solo tiene sentido en el primer caso.
    /// </para>
    /// <para>
    /// <b>QUE SE ENSEÑA DE CADA UNO.</b> De los ajenos, solo los PUBLICADOS: el borrador de otra
    /// organización es trabajo suyo y todavía no es público, y decir que existe filtraría lo que
    /// está preparando. De los PROPIOS se enseñan todos, publicados o no, porque son datos de quien
    /// pregunta y «ya lo tienes en borrador» es justo lo que evita el duplicado.
    /// </para>
    /// <para>
    /// NO BLOQUEA. Avisar no es impedir: puede haber dos Festivales distintos con nombres parecidos
    /// en el mismo municipio, y quien registra es quien sabe. El asistente ya obliga a mirar la
    /// lista antes de seguir, que es lo que corresponde.
    /// </para>
    /// </remarks>
    private static async Task<IResult> BuscarCoincidencias(int organizacionId, string? nombre, string? codigoDepartamento, string? codigoMunicipio, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        var persona = Persona(principal); if (persona is null) return Results.Unauthorized();
        if (!await Administra(db, persona.Value, organizacionId, ct)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(codigoDepartamento)) return Results.Ok(Array.Empty<CoincidenciaFestivalHistoricoDto>());
        var normalized = Normalizar(nombre); if (normalized.Length < 3) return Results.Ok(Array.Empty<CoincidenciaFestivalHistoricoDto>());

        var institucional = await db.EntityProfiles.Where(x => x.IsInstitutional).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);

        var candidatos = await db.FestivalRecords.AsNoTracking()
            .Where(x => x.DepartmentCode == codigoDepartamento)
            .Where(x => x.OrganizacionPrincipalId == organizacionId
                || x.StatusCode == EstadosFestival.Publicado
                || x.StatusCode == "Publicado")
            .Select(x => new
            {
                x.Id, x.Name, x.Description, x.DepartmentCode, x.MunicipalityCode,
                x.OrganizacionPrincipalId,
                Organizacion = db.EntityProfiles.Where(e => e.Id == x.OrganizacionPrincipalId).Select(e => e.Name).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var resultado = candidatos
            .Where(x => string.IsNullOrWhiteSpace(codigoMunicipio) || x.MunicipalityCode == codigoMunicipio)
            .Where(x => SeParecen(Normalizar(x.Name), normalized))
            .Select(x =>
            {
                var esInstitucional = institucional is not null && x.OrganizacionPrincipalId == institucional;
                var esPropio = x.OrganizacionPrincipalId == organizacionId;
                var (tipo, evidencia) = esInstitucional
                    ? ("HistoricoReclamable",
                       "Coincide en nombre y territorio con un Festival del registro histórico, que todavía no tiene organización administradora. Si es el tuyo, puedes solicitar su administración.")
                    : esPropio
                        ? ("YaRegistradoPorTuOrganizacion",
                           "Tu organización ya tiene registrado un Festival con este nombre en este territorio. Revísalo antes de crear otro.")
                        : ("YaRegistradoPorOtraOrganizacion",
                           $"Ya hay un Festival registrado con este nombre en este territorio, administrado por {x.Organizacion ?? "otra organización"}. Si es el mismo, no lo registres de nuevo.");
                return new CoincidenciaFestivalHistoricoDto(
                    x.Id.ToString(CultureInfo.InvariantCulture), x.Name, x.Description,
                    x.DepartmentCode, null, x.MunicipalityCode, null,
                    esInstitucional ? null : x.Organizacion,
                    tipo, [evidencia]);
            })
            // LOS RECLAMABLES PRIMERO: son los unicos sobre los que hay algo que hacer aqui.
            .OrderBy(x => x.TipoCoincidencia == "HistoricoReclamable" ? 0 : 1)
            .ThenBy(x => x.NombreFestival, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Results.Ok(resultado);
    }

    /// <summary>
    /// Dos nombres se parecen lo bastante como para avisar.
    /// </summary>
    /// <remarks>
    /// ES LA MISMA REGLA QUE YA HABIA —igualdad o que uno contenga al otro, sobre el nombre
    /// normalizado—, sacada a su propio sitio ahora que la usan los tres tipos de coincidencia. No
    /// se afina aqui: cambiarla es una decision sobre cuantos falsos positivos se aceptan, y merece
    /// su propia medicion.
    /// </remarks>
    private static bool SeParecen(string candidato, string buscado) =>
        candidato == buscado
        || candidato.Contains(buscado, StringComparison.Ordinal)
        || buscado.Contains(candidato, StringComparison.Ordinal);

    private static async Task<IResult> ObtenerOCrearBorrador(int organizacionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        var persona = Persona(principal); if (persona is null) return Results.Unauthorized();
        if (!await Administra(db, persona.Value, organizacionId, ct)) return Results.Forbid();
        var draft = await db.BorradoresDeProceso.FirstOrDefaultAsync(x => x.ModuloId == Festival && x.OrganizacionId == organizacionId && x.PersonaId == persona && x.Estado == "borrador", ct);
        if (draft is null) { var now = DateTime.UtcNow; draft = new BorradorDeProcesoRow { ModuloId = Festival, OrganizacionId = organizacionId, PersonaId = persona.Value, FechaCreacion = now, FechaActualizacion = now, Version = 1 }; db.BorradoresDeProceso.Add(draft); await db.SaveChangesAsync(ct); }
        return Results.Ok(Dto(draft));
    }

    private static async Task<IResult> GuardarBorrador(long id, GuardarBorradorProcesoSolicitud request, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext http, CancellationToken ct)
    {
        if (!await TestigoDePeticion.ValidoAsync(antiforgery, http)) return Results.BadRequest(new { message = "El testigo de seguridad no es válido." });
        var persona = Persona(principal); if (persona is null) return Results.Unauthorized();
        var draft = await db.BorradoresDeProceso.FirstOrDefaultAsync(x => x.Id == id && x.Estado == "borrador", ct);
        if (draft is null) return Results.NotFound();
        if (draft.PersonaId != persona || draft.OrganizacionId is null || !await Administra(db, persona.Value, draft.OrganizacionId.Value, ct)) return Results.Forbid();
        if (request.Version is int version && version != draft.Version) return Results.Conflict(new { message = "El borrador cambió en otra sesión. Recarga antes de guardar." });
        var datos = string.IsNullOrWhiteSpace(request.DatosJson) ? "{}" : request.DatosJson;
        try { using var _ = JsonDocument.Parse(datos); } catch (JsonException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["datosJson"] = ["El borrador debe contener JSON válido."] }); }
        draft.DatosJson = datos; draft.Version++; draft.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Results.Ok(Dto(draft));
    }

    private static async Task<IResult> Crear(int organizacionId, CrearReclamacionAdministracionSolicitud request, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext http, CancellationToken ct)
    {
        if (!await TestigoDePeticion.ValidoAsync(antiforgery, http)) return Results.BadRequest(new { message = "El testigo de seguridad no es válido." });
        var persona = Persona(principal); if (persona is null) return Results.Unauthorized();
        if (!await Administra(db, persona.Value, organizacionId, ct)) return Results.Forbid();
        // Reclamar la administración de un Festival es afirmar ante el Programa que es de esta
        // organización. Es representación activa, así que exige la misma dirección comprobada.
        if (await AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync(
                db, organizacionId, "reclamar la administración de un Festival", ct) is { } sinConfirmar) return sinConfirmar;
        if (!int.TryParse(request.FestivalId, out var festivalId) || string.IsNullOrWhiteSpace(request.Justificacion)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["festivalId"] = ["Indica un Festival y una justificación."] });
        var festival = await db.FestivalRecords.FirstOrDefaultAsync(x => x.Id == festivalId, ct);
        if (festival is null || !await EsReclamable(db, festival, ct)) return Results.Conflict(new { message = "El Festival ya no es elegible para reclamación." });
        var id = festivalId.ToString(CultureInfo.InvariantCulture);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.AdministrationClaims.AnyAsync(x => x.ModuloId == Festival && x.CanonicalRecordId == id && Activas.Contains(x.Status), ct)) return Results.Conflict(new { message = "Ya existe una reclamación activa para este Festival." });
        long? borrador = long.TryParse(request.BorradorId, out var borradorId) ? borradorId : null;
        if (borrador is not null && !await db.BorradoresDeProceso.AnyAsync(x => x.Id == borrador && x.OrganizacionId == organizacionId && x.PersonaId == persona, ct)) return Results.Forbid();
        var now = DateTime.UtcNow;
        var claim = new AdministrationClaimRow { ModuloId = Festival, CanonicalRecordId = id, RequestingOrganizationId = organizacionId, RequestingPersonId = persona.Value, ProcessDraftId = borrador, Status = "enviada", Justification = ValidationHelpers.SanitizeText(request.Justificacion, 2400), EvidenceJson = request.EvidenciasJson, SignalsJson = request.SenalesJson, CreatedAt = now, SubmittedAt = now, UpdatedAt = now, Version = 1 };
        db.AdministrationClaims.Add(claim);
        await db.SaveChangesAsync(ct);
        Auditar(db, persona.Value, claim.Id.ToString(CultureInfo.InvariantCulture), "ReclamacionEnviada", null, "enviada");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Created($"/api/v1/externo/reclamaciones-administracion/{claim.Id}", ClaimDto(claim));
    }

    private static async Task<IResult> ListarPropias(int organizacionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        var persona = Persona(principal);
        if (persona is null) return Results.Unauthorized();
        if (!await Administra(db, persona.Value, organizacionId, ct)) return Results.Forbid();

        var reclamaciones = await db.AdministrationClaims.AsNoTracking()
            .Where(x => x.RequestingOrganizationId == organizacionId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);

        var idsDeFestival = reclamaciones
            .Where(x => x.ModuloId == Festival && int.TryParse(x.CanonicalRecordId, out _))
            .Select(x => int.Parse(x.CanonicalRecordId, CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();
        var nombresDeFestival = idsDeFestival.Length == 0
            ? new Dictionary<int, string>()
            : await db.FestivalRecords.AsNoTracking()
                .Where(x => idsDeFestival.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return Results.Ok(reclamaciones.Select(reclamacion =>
        {
            var nombre = int.TryParse(reclamacion.CanonicalRecordId, out var festivalId)
                && nombresDeFestival.TryGetValue(festivalId, out var festival)
                    ? festival
                    : null;
            return ClaimDto(reclamacion) with { RegistroNombre = nombre };
        }));
    }
    private static async Task<IResult> ListarInstitucional(string? estado, PnmcDbContext db, CancellationToken ct)
    {
        var q = db.AdministrationClaims.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(x => x.Status == estado);
        var claims = await q.OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);

        // LOS NOMBRES, EN LOTE. La bandeja institucional decidia sobre «Registro #105» y
        // «Organización #12» -los mismos identificadores en crudo que ya se corrigieron en
        // record-link-requests-, porque `ClaimDto` nunca los llevaba. Mismo patron: se resuelven
        // aqui, una sola consulta por tabla, y se pegan con `with { ... }`.
        var idsDeFestival = claims.Where(x => x.ModuloId == Festival && int.TryParse(x.CanonicalRecordId, out _))
            .Select(x => int.Parse(x.CanonicalRecordId, CultureInfo.InvariantCulture)).Distinct().ToArray();
        var nombresDeFestival = idsDeFestival.Length == 0 ? new Dictionary<int, string>()
            : await db.FestivalRecords.AsNoTracking().Where(x => idsDeFestival.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var idsDeOrganizacion = claims.Select(x => x.RequestingOrganizationId).Distinct().ToArray();
        var nombresDeOrganizacion = idsDeOrganizacion.Length == 0 ? new Dictionary<int, string>()
            : await db.EntityProfiles.AsNoTracking().Where(x => idsDeOrganizacion.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        return Results.Ok(claims.Select(claim =>
        {
            var registroNombre = int.TryParse(claim.CanonicalRecordId, out var festivalId) && nombresDeFestival.TryGetValue(festivalId, out var nombre) ? nombre : null;
            var organizacionNombre = nombresDeOrganizacion.TryGetValue(claim.RequestingOrganizationId, out var nombreOrg) ? nombreOrg : null;
            return ClaimDto(claim) with { RegistroNombre = registroNombre, OrganizacionSolicitanteNombre = organizacionNombre };
        }));
    }

    private static async Task<IResult> PedirAclaracion(long id, ResponderAclaracionSolicitud request, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext http, CancellationToken ct)
    { if (!await TestigoDePeticion.ValidoAsync(antiforgery, http)) return Results.BadRequest(); var actor = Persona(principal); if (actor is null) return Results.Unauthorized(); var claim = await db.AdministrationClaims.FirstOrDefaultAsync(x => x.Id == id, ct); if (claim is null) return Results.NotFound(); if (string.IsNullOrWhiteSpace(request.Respuesta)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["respuesta"] = ["Escribe la aclaración solicitada."] }); if (claim.Status is not ("enviada" or "en_revision" or "aclaracion_enviada")) return Results.Conflict(); var now = DateTime.UtcNow; claim.Status = "requiere_aclaracion"; claim.UpdatedAt = now; claim.Version++; db.AdministrationClaimClarifications.Add(new AdministrationClaimClarificationRow { ClaimId = id, RequestedById = actor.Value, Comment = ValidationHelpers.SanitizeText(request.Respuesta, 2400), RequestedAt = now }); Notificar(db, claim.RequestingPersonId, "ReclamacionRequiereAclaracion", "Se requieren aclaraciones", "La institución solicitó información adicional sobre tu reclamación.", id.ToString()); Auditar(db, actor.Value, id.ToString(), "AclaracionSolicitada", "en_revision", claim.Status); await db.SaveChangesAsync(ct); return Results.Ok(ClaimDto(claim)); }
    private static async Task<IResult> ResponderAclaracion(long id, ResponderAclaracionSolicitud request, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext http, CancellationToken ct)
    { if (!await TestigoDePeticion.ValidoAsync(antiforgery, http)) return Results.BadRequest(); var actor = Persona(principal); if (actor is null) return Results.Unauthorized(); var claim = await db.AdministrationClaims.FirstOrDefaultAsync(x => x.Id == id, ct); if (claim is null) return Results.NotFound(); if (string.IsNullOrWhiteSpace(request.Respuesta)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["respuesta"] = ["Escribe la respuesta a la aclaración."] }); if (!await Administra(db, actor.Value, claim.RequestingOrganizationId, ct)) return Results.Forbid(); if (claim.Status != "requiere_aclaracion") return Results.Conflict(); var clarification = await db.AdministrationClaimClarifications.OrderByDescending(x => x.RequestedAt).FirstOrDefaultAsync(x => x.ClaimId == id && x.Response == null, ct); if (clarification is null) return Results.Conflict(); clarification.Response = ValidationHelpers.SanitizeText(request.Respuesta, 2400); clarification.RespondedById = actor; clarification.RespondedAt = DateTime.UtcNow; claim.Status = "aclaracion_enviada"; claim.UpdatedAt = DateTime.UtcNow; claim.Version++; Auditar(db, actor.Value, id.ToString(), "AclaracionRespondida", "requiere_aclaracion", claim.Status); await db.SaveChangesAsync(ct); return Results.Ok(ClaimDto(claim)); }
    private static async Task<IResult> Cancelar(long id, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext http, CancellationToken ct)
    { if (!await TestigoDePeticion.ValidoAsync(antiforgery, http)) return Results.BadRequest(); var actor = Persona(principal); if (actor is null) return Results.Unauthorized(); var claim = await db.AdministrationClaims.FirstOrDefaultAsync(x => x.Id == id, ct); if (claim is null) return Results.NotFound(); if (!await Administra(db, actor.Value, claim.RequestingOrganizationId, ct)) return Results.Forbid(); if (!Activas.Contains(claim.Status)) return Results.Conflict(); claim.Status = "cancelada"; claim.UpdatedAt = DateTime.UtcNow; claim.Version++; Auditar(db, actor.Value, id.ToString(), "ReclamacionCancelada", null, claim.Status); await db.SaveChangesAsync(ct); return Results.Ok(ClaimDto(claim)); }

    private static async Task<IResult> Decidir(long id, DecidirReclamacionAdministracionSolicitud request, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext http, CancellationToken ct)
    {
        if (!await TestigoDePeticion.ValidoAsync(antiforgery, http)) return Results.BadRequest();
        var actor = Persona(principal); if (actor is null) return Results.Unauthorized(); var claim = await db.AdministrationClaims.FirstOrDefaultAsync(x => x.Id == id, ct); if (claim is null) return Results.NotFound();
        var action = request.Accion.Trim().ToLowerInvariant(); if (action == "rechazar") { if (!Activas.Contains(claim.Status)) return Results.Conflict(); claim.Status = "rechazada"; claim.Decision = action; claim.DecisionReason = ValidationHelpers.SanitizeText(request.Motivo, 2400); claim.DeciderId = actor; claim.DecidedAt = claim.UpdatedAt = DateTime.UtcNow; Auditar(db, actor.Value, id.ToString(), "ReclamacionRechazada", null, claim.Status); Notificar(db, claim.RequestingPersonId, "ReclamacionRechazada", "Reclamación rechazada", claim.DecisionReason ?? "La reclamación fue rechazada.", id.ToString()); await db.SaveChangesAsync(ct); return Results.Ok(ClaimDto(claim)); }
        if (action != "aprobar" || claim.Status is not ("enviada" or "en_revision" or "aclaracion_enviada")) return Results.Conflict();
        if (!int.TryParse(claim.CanonicalRecordId, out var festivalId)) return Results.Conflict();
        var festival = await db.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == festivalId, ct); if (festival is null || !await EsReclamable(db, festival, ct) || claim.ProcessDraftId is null) return Results.Conflict();
        var borrador = await db.BorradoresDeProceso.AsNoTracking().FirstOrDefaultAsync(x => x.Id == claim.ProcessDraftId, ct); if (borrador is null) return Results.Conflict();
        claim.Status = "aprobada"; claim.Decision = "aprobar"; claim.DecisionReason = ValidationHelpers.SanitizeText(request.Motivo, 2400); claim.DeciderId = actor; claim.DecidedAt = claim.UpdatedAt = DateTime.UtcNow;
        db.AdministrationReconciliations.Add(new AdministrationReconciliationRow { ClaimId = claim.Id, HistoricalValuesJson = JsonSerializer.Serialize(new { festival.Name, festival.Description, festival.ContactEmail, festival.ContactPhone, festival.InstagramUrl, festival.FacebookUrl, festival.WebsiteUrl, festival.OtherUrl, festival.Periodicidad }), DraftValuesJson = borrador.DatosJson, CreatedAt = DateTime.UtcNow });
        Auditar(db, actor.Value, id.ToString(), "ReclamacionAprobadaPendienteConciliacion", null, "aprobada"); Notificar(db, claim.RequestingPersonId, "ReclamacionAprobadaPendienteConciliacion", "Administración aprobada", "Elige los datos que conservarás antes de ejecutar la transferencia.", id.ToString()); await db.SaveChangesAsync(ct); return Results.Ok(ClaimDto(claim));
    }
    private static async Task<IResult> ObtenerConciliacion(long id, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    { var actor = Persona(principal); if (actor is null) return Results.Unauthorized(); var claim = await db.AdministrationClaims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct); if (claim is null) return Results.NotFound(); if (!await Administra(db, actor.Value, claim.RequestingOrganizationId, ct)) return Results.Forbid(); var row = await db.AdministrationReconciliations.AsNoTracking().FirstOrDefaultAsync(x => x.ClaimId == id, ct); return row is null ? Results.NotFound() : Results.Ok(new ConciliacionAdministracionDto(id.ToString(), row.HistoricalValuesJson, row.DraftValuesJson, row.SelectionsJson, row.ResolvedAt is not null)); }

    private static async Task<IResult> ResolverConciliacion(long id, ResolverConciliacionAdministracionSolicitud request, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext http, CancellationToken ct)
    {
        if (!await TestigoDePeticion.ValidoAsync(antiforgery, http)) return Results.BadRequest();
        var actor = Persona(principal); if (actor is null) return Results.Unauthorized();
        var claim = await db.AdministrationClaims.FirstOrDefaultAsync(x => x.Id == id && x.Status == "aprobada", ct);
        if (claim is null) return Results.Conflict(new { message = "La reclamación no está pendiente de conciliación." });
        if (!await Administra(db, actor.Value, claim.RequestingOrganizationId, ct)) return Results.Forbid();
        var conciliacion = await db.AdministrationReconciliations.FirstOrDefaultAsync(x => x.ClaimId == id && x.ResolvedAt == null, ct);
        if (conciliacion is null || !int.TryParse(claim.CanonicalRecordId, out var festivalId)) return Results.NotFound();
        using var historico = JsonDocument.Parse(conciliacion.HistoricalValuesJson); using var borrador = JsonDocument.Parse(conciliacion.DraftValuesJson);
        var campos = new Dictionary<string, string> { ["Name"] = "nombre", ["Description"] = "descripcion", ["ContactEmail"] = "correoContacto", ["ContactPhone"] = "telefonoCelular", ["InstagramUrl"] = "instagram", ["FacebookUrl"] = "facebook", ["WebsiteUrl"] = "paginaWeb", ["OtherUrl"] = "otroEnlace", ["Periodicidad"] = "periodicidad" };
        var faltantes = campos.Keys.Where(campo => !request.Selecciones.TryGetValue(campo, out var eleccion) || (eleccion != "historico" && eleccion != "borrador")).ToArray();
        if (faltantes.Length > 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["selecciones"] = [$"Debes elegir histórico o borrador para: {string.Join(", ", faltantes)}."] });
        string? Valor(string historicoNombre, string borradorNombre) => request.Selecciones.TryGetValue(historicoNombre, out var eleccion) && eleccion == "borrador" && borrador.RootElement.TryGetProperty(borradorNombre, out var b) && b.ValueKind != JsonValueKind.Null ? b.GetString() : (historico.RootElement.TryGetProperty(historicoNombre, out var h) && h.ValueKind != JsonValueKind.Null ? h.GetString() : null);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var festival = await db.FestivalRecords.FirstOrDefaultAsync(x => x.Id == festivalId, ct); if (festival is null || festival.OrganizacionPrincipalId is null) return Results.NotFound();
        if (!await EsReclamable(db, festival, ct)) return Results.Conflict(new { message = "El Festival ya no está disponible para transferencia." });
        var anterior = festival.OrganizacionPrincipalId.Value;
        festival.Name = Valor("Name", "nombre") ?? festival.Name; festival.Description = Valor("Description", "descripcion"); festival.ContactEmail = Valor("ContactEmail", "correoContacto"); festival.ContactPhone = Valor("ContactPhone", "telefonoCelular"); festival.InstagramUrl = Valor("InstagramUrl", "instagram"); festival.FacebookUrl = Valor("FacebookUrl", "facebook"); festival.WebsiteUrl = Valor("WebsiteUrl", "paginaWeb"); festival.OtherUrl = Valor("OtherUrl", "otroEnlace"); festival.Periodicidad = Valor("Periodicidad", "periodicidad"); festival.OrganizacionPrincipalId = claim.RequestingOrganizationId; festival.UpdatedAt = DateTime.UtcNow;
        conciliacion.SelectionsJson = JsonSerializer.Serialize(request.Selecciones); conciliacion.ResolvedAt = DateTime.UtcNow; claim.Status = "transferencia_ejecutada"; claim.UpdatedAt = DateTime.UtcNow;
        db.AdministrationTransfers.Add(new AdministrationTransferRow { ModuloId = Festival, CanonicalRecordId = claim.CanonicalRecordId, PreviousOrganizationId = anterior, NewOrganizationId = claim.RequestingOrganizationId, ClaimId = id, DeciderId = claim.DeciderId ?? actor.Value, Reason = claim.DecisionReason, PreviousStatus = festival.StatusCode, NewStatus = festival.StatusCode, ExecutedAt = DateTime.UtcNow });
        Auditar(db, actor.Value, claim.CanonicalRecordId, "TransferenciaAdministracionConciliada", anterior.ToString(), JsonSerializer.Serialize(request.Selecciones)); Notificar(db, claim.RequestingPersonId, "TransferenciaAdministracionEjecutada", "Administración transferida", "La conciliación fue aplicada y ya puedes administrar el Festival.", id.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Ok(ClaimDto(claim));
    }
    private static async Task<bool> EsReclamable(PnmcDbContext db, FestivalRow festival, CancellationToken ct) { var institutional = await db.EntityProfiles.Where(x => x.IsInstitutional).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct); return institutional is not null && festival.OrganizacionPrincipalId == institutional && (festival.StatusCode == EstadosFestival.Publicado || festival.StatusCode == "Publicado"); }
    private static int? Persona(ClaimsPrincipal principal) => int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    // LA REGLA VIVE EN `AdministracionDeOrganizacion`. Esta copia NO comprobaba si la organización
    // sigue activa: una organización dada de baja seguía pudiendo reclamar la administración de
    // procesos ajenos, que es justo lo contrario de lo que una baja debería permitir.
    private static Task<bool> Administra(PnmcDbContext db, int user, int organization, CancellationToken ct) =>
        AdministracionDeOrganizacion.PuedeAdministrarAsync(db, user, organization, ct);
    private static BorradorProcesoDto Dto(BorradorDeProcesoRow x) => new(x.Id.ToString(), x.ModuloId, x.OrganizacionId?.ToString() ?? string.Empty, x.Estado, x.DatosJson, x.Version, x.FechaActualizacion);
    private static ReclamacionAdministracionDto ClaimDto(AdministrationClaimRow x) => new(x.Id.ToString(), x.ModuloId, x.CanonicalRecordId, x.RequestingOrganizationId.ToString(), x.Status, x.Justification, x.SignalsJson, x.DecisionReason, x.CreatedAt, x.SubmittedAt, x.DecidedAt);
    private static void Auditar(PnmcDbContext db, int actor, string record, string action, string? before, string? after) => db.AuditLogs.Add(new AuditLogRow { UserId = actor, TableName = "ReclamacionesAdministracion", RecordId = record, Action = action, PreviousValuesJson = before, NewValuesJson = after, CreatedAt = DateTime.UtcNow });
    private static void Notificar(PnmcDbContext db, int recipient, string type, string title, string body, string record) => db.Notifications.Add(new NotificationRow { RecipientUserId = recipient, EventType = type, Channel = "internal", Title = title, Body = body, Status = "enviada", ModuloId = "reclamaciones-administracion", RecordId = record, CreatedAt = DateTime.UtcNow, SentAt = DateTime.UtcNow });
    private static string Normalizar(string? value)
    {
        var decomposed = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(); foreach (var c in decomposed) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))) result.Append(c);
        return string.Join(' ', result.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
