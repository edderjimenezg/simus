using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public static class RecordGovernanceEndpoints
{
    private static readonly string[] LinkStatuses = ["pendiente", "en_revision", "ajustes_solicitados", "aprobada", "rechazada", "cancelada"];
    private static readonly string[] DuplicateLevels = ["alta", "media", "baja"];
    private static readonly string[] DuplicateDecisions = ["fusionar", "mantener_separados", "no_duplicado", "pendiente"];
    private static readonly string[] FlagSeverities = ["baja", "media", "alta"];
    private static readonly string[] FlagStatuses = ["abierta", "en_revision", "resuelta", "descartada"];

    public static RouteGroupBuilder MapRecordGovernanceEndpoints(this RouteGroupBuilder group)
    {
        // CANAL EXTERNO, no institucional. Hasta esta ruta se cerraba
        // con .RequireAuthorization() a secas, es decir contra el esquema institucional por
        // defecto, y funcionaba solo porque los tres roles aliado_* obtenian cookie de consola.
        // Al retirarlos habria quedado accesible unicamente para webmaster y gestor_interno
        // —el mismo que luego la revisa mas abajo—, sin que nada fallara: la prueba que la
        // cubria entraba como webmaster. Quien pide vincularse a un registro es alguien del
        // ecosistema, asi que la puerta es la externa.
        group.MapPost("/solicitudes-de-vinculacion", CreateRecordLinkRequest)
            .RequireAuthorization(SimusAuthentication.ExternalPolicy);

        var adminLinkRequests = group.MapGroup("/admin/solicitudes-de-vinculacion").WithTags("admin-solicitudes-de-vinculacion");
        adminLinkRequests.MapGet(string.Empty, ListRecordLinkRequests).RequireAuthorization();
        adminLinkRequests.MapPost("/{id:long}/status", UpdateRecordLinkRequestStatus).RequireAuthorization();

        var duplicates = group.MapGroup("/admin/duplicates").WithTags("admin-duplicates");
        duplicates.MapGet(string.Empty, ListDuplicateCandidates).RequireAuthorization();
        duplicates.MapPost(string.Empty, CreateDuplicateCandidate).RequireAuthorization();
        duplicates.MapPost("/{id:long}/decision", DecideDuplicateCandidate).RequireAuthorization();

        var quality = group.MapGroup("/admin/data-quality/flags").WithTags("admin-data-quality");
        quality.MapGet(string.Empty, ListQualityFlags).RequireAuthorization();
        quality.MapPost(string.Empty, CreateQualityFlag).RequireAuthorization();
        quality.MapPost("/{id:long}/status", UpdateQualityFlagStatus).RequireAuthorization();

        return group;
    }

    private static async Task<IResult> CreateRecordLinkRequest(
        RecordLinkRequestCreateRequest request,
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var currentUser = await ResolveCurrentUserAsync(principal, dbContext, cancellationToken);
        if (currentUser is null)
        {
            return Results.Unauthorized();
        }

        var errors = ValidateLinkCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // ALCANCE = la entidad que la persona representa, si representa a alguna. Una persona
        // registrada que todavia no representa a ningun actor tambien puede pedir vincularse a
        // un registro —es justamente como se empieza—, y entonces la solicitud va sin entidad.
        // No es un error: es el caso "persona registrada" del modelo de roles.
        var entidadId = await ResolveEntidadDelUsuarioAsync(currentUser.Id, dbContext, cancellationToken);

        var now = DateTime.UtcNow;
        var row = new RecordLinkRequestRow
        {
            ModuloId = Clean(request.ModuleId),
            RecordId = Clean(request.RecordId),
            RequestingUserId = currentUser.Id,
            EntidadId = entidadId,
            RequestedScope = Clean(request.RequestedScope) is "editor" ? "editor" : "responsable",
            Reason = ValidationHelpers.SanitizeText(request.Reason, 1200),
            EvidenceText = ValidationHelpers.SanitizeText(request.EvidenceText, 2000),
            Status = "pendiente",
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.RecordLinkRequests.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/solicitudes-de-vinculacion/{row.Id}", ToDto(row));
    }

    private static async Task<IResult> ListRecordLinkRequests(
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        string? status,
        int? limit,
        int? offset,
        CancellationToken cancellationToken)
    {
        if (!CanReview(principal))
        {
            return Results.Forbid();
        }

        var query = dbContext.RecordLinkRequests.AsNoTracking().OrderByDescending(item => item.UpdatedAt).AsQueryable();
        var statusCode = Clean(status);
        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            query = query.Where(item => item.Status == statusCode);
        }

        var (safeLimit, safeOffset) = SafePaging(limit, offset);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip(safeOffset).Take(safeLimit).ToListAsync(cancellationToken);
        var dtos = await ConNombresAsync(rows, dbContext, cancellationToken);
        return Results.Ok(new PagedResponse<RecordLinkRequestDto>(dtos, safeLimit, safeOffset, total));
    }

    // MODULOID -> DE QUE TABLA SALE EL NOMBRE. 'festivales_retiro' (retiro de un Festival
    // publicado) y 'festivals' (vinculacion a un Festival) apuntan al Id de dbo.Festivales;
    // 'mercados_retiro' apunta al de dbo.Mercados.
    //
    // MERCADOS SE AÑADIO EL 15 DE SEPTIEMBRE DE 2026, cuando el modulo dejo de ser una promesa: el
    // comentario anterior decia que el resto del ecosistema «todavia no tiene tabla propia de la
    // que sacar un nombre», y desde ese dia Mercados si la tiene. Sin esto, quien revisa la bandeja
    // veria «Registro #7» y tendria que ir a buscar de que mercado le estan hablando.
    private static readonly string[] ModulosDeFestival = ["festivales_retiro", "festivals"];
    private const string RetiroDeMercado = "mercados_retiro";

    private static async Task<List<RecordLinkRequestDto>> ConNombresAsync(
        IReadOnlyList<RecordLinkRequestRow> rows, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var idsDeFestival = rows
            .Where(item => ModulosDeFestival.Contains(item.ModuloId) && int.TryParse(item.RecordId, out _))
            .Select(item => int.Parse(item.RecordId, CultureInfo.InvariantCulture))
            .Distinct().ToArray();
        var nombresDeFestival = idsDeFestival.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.FestivalRecords.AsNoTracking()
                .Where(item => idsDeFestival.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var idsDeMercado = rows
            .Where(item => item.ModuloId == RetiroDeMercado && int.TryParse(item.RecordId, out _))
            .Select(item => int.Parse(item.RecordId, CultureInfo.InvariantCulture))
            .Distinct().ToArray();
        var nombresDeMercado = idsDeMercado.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.Mercados.AsNoTracking()
                .Where(item => idsDeMercado.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Nombre, cancellationToken);

        var idsDeEntidad = rows
            .Where(item => item.EntidadId is not null)
            .Select(item => item.EntidadId!.Value)
            .Distinct().ToArray();
        var nombresDeEntidad = idsDeEntidad.Length == 0
            ? new Dictionary<int, string>()
            : await dbContext.EntityProfiles.AsNoTracking()
                .Where(item => idsDeEntidad.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        return rows.Select(row =>
        {
            // EL NOMBRE SALE DE LA TABLA QUE LE CORRESPONDE A SU MODULO. Buscar el mismo Id en las
            // dos tablas devolveria el nombre del festival 7 para la solicitud de retiro del
            // mercado 7, que es peor que no decir nada.
            var recordName = int.TryParse(row.RecordId, out var registroId)
                ? (row.ModuloId == RetiroDeMercado
                    ? (nombresDeMercado.TryGetValue(registroId, out var nombreMercado) ? nombreMercado : null)
                    : (nombresDeFestival.TryGetValue(registroId, out var nombre) ? nombre : null))
                : null;
            var entidadNombre = row.EntidadId is not null && nombresDeEntidad.TryGetValue(row.EntidadId.Value, out var nombreEntidad) ? nombreEntidad : null;
            return ToDto(row) with { RecordName = recordName, EntidadNombre = entidadNombre };
        }).ToList();
    }

    private static async Task<IResult> UpdateRecordLinkRequestStatus(
        long id,
        RecordLinkRequestStatusRequest request,
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanReview(principal))
        {
            return Results.Forbid();
        }

        var status = Clean(request.Status);
        if (!LinkStatuses.Contains(status))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Estado de solicitud de vinculacion no valido."] });
        }

        var row = await dbContext.RecordLinkRequests.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return Results.NotFound();
        }

        row.Status = status;
        row.ReviewComment = ValidationHelpers.SanitizeText(request.Comment, 1200);
        row.ReviewerUserId = await ResolveCurrentUserIdAsync(principal);
        row.UpdatedAt = DateTime.UtcNow;
        // APROBAR UNA SOLICITUD DE RETIRO LA EJECUTA, no solo la marca. Una solicitud aprobada que
        // deja el registro publicado es una decisión que nadie aplicó, y la organización ve que le
        // dijeron que sí mientras su mercado sigue en el portal.
        if (row.ModuloId == RetiroDeMercado && status == "aprobada" && int.TryParse(row.RecordId, out var mercadoId))
        {
            var mercado = await dbContext.Mercados.FirstOrDefaultAsync(item => item.Id == mercadoId && item.Activo, cancellationToken);
            if (mercado is not null && string.Equals(mercado.EstadoRegistro, "publicado", StringComparison.OrdinalIgnoreCase))
            {
                mercado.EstadoRegistro = "archivado";
                mercado.FechaActualizacion = row.UpdatedAt;
                dbContext.AuditLogs.Add(new AuditLogRow
                {
                    UserId = row.ReviewerUserId ?? 0,
                    TableName = "Mercados",
                    RecordId = mercado.Id.ToString(CultureInfo.InvariantCulture),
                    Action = AccionesAuditoria.Archivar,
                    PreviousValuesJson = "{\"estado\":\"publicado\"}",
                    // LA MISMA FORMA QUE EL RESTO DEL CIRCUITO DEL MERCADO —`evento` y `detalle`—,
                    // que es de donde lee el historial que ve la organización en su ficha.
                    NewValuesJson = "{\"evento\":\"decidir\",\"detalle\":{\"estado\":\"archivado\"}}",
                    CreatedAt = row.UpdatedAt,
                });
            }
        }

        if (row.ModuloId == "festivales_retiro" && status == "aprobada" && int.TryParse(row.RecordId, out var festivalId))
        {
            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is not null && EstadosFestival.Es(festival.StatusCode, EstadosFestival.Publicado))
            {
                festival.StatusCode = EstadosFestival.Archivado;
                festival.UpdatedAt = row.UpdatedAt;
                dbContext.AuditLogs.Add(new AuditLogRow { UserId = row.ReviewerUserId ?? 0, TableName = "Festivales", RecordId = festival.Id.ToString(CultureInfo.InvariantCulture), Action = AccionesAuditoria.Archivar, NewValuesJson = "{\"Evento\":\"FestivalRetiroAprobado\",\"Estado\":\"archivado\"}", CreatedAt = row.UpdatedAt });
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(row));
    }

    private static async Task<IResult> ListDuplicateCandidates(
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        string? status,
        int? limit,
        int? offset,
        CancellationToken cancellationToken)
    {
        if (!CanReview(principal))
        {
            return Results.Forbid();
        }

        var query = dbContext.RecordDuplicateCandidates.AsNoTracking().OrderByDescending(item => item.UpdatedAt).AsQueryable();
        var statusCode = Clean(status);
        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            query = query.Where(item => item.Status == statusCode);
        }

        var (safeLimit, safeOffset) = SafePaging(limit, offset);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip(safeOffset).Take(safeLimit).ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<RecordDuplicateCandidateDto>(rows.Select(ToDto).ToList(), safeLimit, safeOffset, total));
    }

    private static async Task<IResult> CreateDuplicateCandidate(
        RecordDuplicateCandidateCreateRequest request,
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanReview(principal))
        {
            return Results.Forbid();
        }

        var errors = ValidateDuplicateCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var now = DateTime.UtcNow;
        var row = new RecordDuplicateCandidateRow
        {
            ModuloId = Clean(request.ModuleId),
            SourceRecordId = Clean(request.SourceRecordId),
            CandidateRecordId = Clean(request.CandidateRecordId),
            SimilarityLevel = Clean(request.SimilarityLevel),
            SimilarityScore = request.SimilarityScore,
            EvidenceJson = string.IsNullOrWhiteSpace(request.EvidenceJson) ? "{}" : request.EvidenceJson,
            Status = "pendiente",
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.RecordDuplicateCandidates.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/admin/duplicates/{row.Id}", ToDto(row));
    }

    private static async Task<IResult> DecideDuplicateCandidate(
        long id,
        RecordDuplicateDecisionRequest request,
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanReview(principal))
        {
            return Results.Forbid();
        }

        var decision = Clean(request.Decision);
        if (!DuplicateDecisions.Contains(decision))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["decision"] = ["Decision de duplicado no valida."] });
        }

        var row = await dbContext.RecordDuplicateCandidates.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return Results.NotFound();
        }

        row.Decision = decision;
        row.DecisionComment = ValidationHelpers.SanitizeText(request.Comment, 1200);
        row.ReviewerUserId = await ResolveCurrentUserIdAsync(principal);
        row.Status = decision == "pendiente" ? "pendiente" : "resuelto";
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(row));
    }

    private static async Task<IResult> ListQualityFlags(
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        string? status,
        int? limit,
        int? offset,
        CancellationToken cancellationToken)
    {
        if (!CanReview(principal))
        {
            return Results.Forbid();
        }

        var query = dbContext.RecordQualityFlags.AsNoTracking().OrderByDescending(item => item.UpdatedAt).AsQueryable();
        var statusCode = Clean(status);
        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            query = query.Where(item => item.Status == statusCode);
        }

        var (safeLimit, safeOffset) = SafePaging(limit, offset);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.Skip(safeOffset).Take(safeLimit).ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<RecordQualityFlagDto>(rows.Select(ToDto).ToList(), safeLimit, safeOffset, total));
    }

    private static async Task<IResult> CreateQualityFlag(
        RecordQualityFlagCreateRequest request,
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanReview(principal))
        {
            return Results.Forbid();
        }

        var errors = ValidateQualityFlagCreateRequest(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var now = DateTime.UtcNow;
        var row = new RecordQualityFlagRow
        {
            ModuloId = Clean(request.ModuleId),
            RecordId = Clean(request.RecordId),
            FlagType = Clean(request.FlagType),
            Severity = Clean(request.Severity),
            Status = "abierta",
            Detail = ValidationHelpers.SanitizeText(request.Detail, 1200),
            CreatedByUserId = await ResolveCurrentUserIdAsync(principal),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.RecordQualityFlags.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/admin/data-quality/flags/{row.Id}", ToDto(row));
    }

    private static async Task<IResult> UpdateQualityFlagStatus(
        long id,
        RecordQualityFlagStatusRequest request,
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!CanReview(principal))
        {
            return Results.Forbid();
        }

        var status = Clean(request.Status);
        if (!FlagStatuses.Contains(status))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Estado de bandera de calidad no valido."] });
        }

        var row = await dbContext.RecordQualityFlags.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return Results.NotFound();
        }

        row.Status = status;
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(row));
    }

    private static Dictionary<string, string[]> ValidateLinkCreateRequest(RecordLinkRequestCreateRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ModuleId) || request.ModuleId.Length > 80)
        {
            errors["moduleId"] = ["Indica un modulo valido."];
        }

        if (string.IsNullOrWhiteSpace(request.RecordId) || request.RecordId.Length > 120)
        {
            errors["recordId"] = ["Indica el registro que quieres vincular."];
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1200)
        {
            errors["reason"] = ["Explica brevemente por que solicitas la vinculacion."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateDuplicateCreateRequest(RecordDuplicateCandidateCreateRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ModuleId) || request.ModuleId.Length > 80)
        {
            errors["moduleId"] = ["Indica un modulo valido."];
        }

        if (string.IsNullOrWhiteSpace(request.SourceRecordId) || request.SourceRecordId.Length > 120)
        {
            errors["sourceRecordId"] = ["Indica el registro origen."];
        }

        if (string.IsNullOrWhiteSpace(request.CandidateRecordId) || request.CandidateRecordId.Length > 120)
        {
            errors["candidateRecordId"] = ["Indica el registro candidato."];
        }

        if (!DuplicateLevels.Contains(Clean(request.SimilarityLevel)))
        {
            errors["similarityLevel"] = ["El nivel de coincidencia debe ser alta, media o baja."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateQualityFlagCreateRequest(RecordQualityFlagCreateRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ModuleId) || request.ModuleId.Length > 80)
        {
            errors["moduleId"] = ["Indica un modulo valido."];
        }

        if (string.IsNullOrWhiteSpace(request.RecordId) || request.RecordId.Length > 120)
        {
            errors["recordId"] = ["Indica el registro asociado."];
        }

        if (string.IsNullOrWhiteSpace(request.FlagType) || request.FlagType.Length > 80)
        {
            errors["flagType"] = ["Indica el tipo de alerta de calidad."];
        }

        if (!FlagSeverities.Contains(Clean(request.Severity)))
        {
            errors["severity"] = ["La severidad debe ser baja, media o alta."];
        }

        return errors;
    }

    private static bool CanReview(ClaimsPrincipal principal)
    {
        return Permisos.EsFuncionario(principal);
    }

    private static async Task<UserRow?> ResolveCurrentUserAsync(
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveCurrentUserIdAsync(principal);
        if (userId is null)
        {
            return null;
        }

        return await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken);
    }

    private static Task<int?> ResolveCurrentUserIdAsync(ClaimsPrincipal principal)
    {
        var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Task.FromResult<int?>(int.TryParse(rawUserId, out var userId) ? userId : null);
    }

    private static async Task<string> ResolveRoleAsync(int roleId, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.Roles.AsNoTracking()
            .Where(item => item.Id == roleId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
    }

    /// <summary>
    /// Entidad del ecosistema que la persona representa, o <c>null</c> si solo esta registrada.
    /// </summary>
    /// <remarks>
    /// Es el reemplazo del antiguo ambito por entidad aliada. Lee <c>UsuariosEntidades</c>, que
    /// es la tabla puente persona-entidad del modelo definitivo: la misma que
    /// <c>ExternalOrganizationEndpoints</c> escribe al dar de alta una organizacion.
    /// </remarks>
    private static async Task<int?> ResolveEntidadDelUsuarioAsync(int userId, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.UserEntities.AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive)
            .OrderBy(item => item.EntityId)
            .Select(item => (int?)item.EntityId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static (int Limit, int Offset) SafePaging(int? limit, int? offset)
    {
        return (Math.Clamp(limit ?? 50, 1, 200), Math.Max(offset ?? 0, 0));
    }

    private static string Clean(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static RecordLinkRequestDto ToDto(RecordLinkRequestRow row)
    {
        return new RecordLinkRequestDto(
            row.Id.ToString(CultureInfo.InvariantCulture),
            row.ModuloId,
            row.RecordId,
            row.RequestingUserId.ToString(CultureInfo.InvariantCulture),
            row.EntidadId?.ToString(CultureInfo.InvariantCulture),
            row.RequestedScope,
            row.Reason,
            row.EvidenceText ?? string.Empty,
            row.Status,
            row.ReviewComment ?? string.Empty,
            row.CreatedAt,
            row.UpdatedAt);
    }

    private static RecordDuplicateCandidateDto ToDto(RecordDuplicateCandidateRow row)
    {
        return new RecordDuplicateCandidateDto(
            row.Id.ToString(CultureInfo.InvariantCulture),
            row.ModuloId,
            row.SourceRecordId,
            row.CandidateRecordId,
            row.SimilarityLevel,
            row.SimilarityScore,
            row.EvidenceJson,
            row.Status,
            row.Decision ?? string.Empty,
            row.DecisionComment ?? string.Empty,
            row.CreatedAt,
            row.UpdatedAt);
    }

    private static RecordQualityFlagDto ToDto(RecordQualityFlagRow row)
    {
        return new RecordQualityFlagDto(
            row.Id.ToString(CultureInfo.InvariantCulture),
            row.ModuloId,
            row.RecordId,
            row.FlagType,
            row.Severity,
            row.Status,
            row.Detail ?? string.Empty,
            row.CreatedAt,
            row.UpdatedAt);
    }
}
