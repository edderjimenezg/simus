using PNMC.Api.Security;
using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;
using PNMC.Infrastructure.Common;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public static class NotificationEndpoints
{
    private static readonly string[] AllowedChannels = ["internal", "email", "whatsapp"];
    private static readonly string[] AllowedStatuses = ["pendiente", "enviada", "leida", "fallida", "cancelada"];
    /// <summary>
    /// Quita del buzón institucional los avisos de módulos que esa cuenta no tiene activados.
    /// </summary>
    /// <remarks>
    /// <b>SOLO EL BUZON INSTITUCIONAL.</b> Los módulos son la unidad de permiso de la consola;
    /// quien entra por el espacio externo no tiene ninguno, y aplicarle esta resta le vaciaría el
    /// buzón entero. Ver <see cref="ModuloDeUnAviso"/> para por qué se filtra por lo vetado y no
    /// por lo permitido.
    /// </remarks>
    private static async Task<IQueryable<NotificationRow>> SinLosModulosCerradosAsync(
        IQueryable<NotificationRow> consulta,
        ClaimsPrincipal principal,
        string alcance,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(alcance, SimusAuthentication.InstitutionalScope, StringComparison.Ordinal)) return consulta;

        var cuenta = PermisosDeModulo.CuentaDe(principal);
        if (cuenta is null) return consulta;

        var suyos = await PermisosDeModulo.DeLaCuentaAsync(
            dbContext, cuenta.Value, principal.IsInRole(Permisos.Webmaster), cancellationToken);
        var vetados = ModuloDeUnAviso.VetadosPara(suyos);
        if (vetados.Count == 0) return consulta;

        return consulta.Where(item => item.ModuloId == null || !vetados.Contains(item.ModuloId));
    }

    private static readonly Dictionary<string, string> AmbitosDeAcceso = new()
    {
        ["externo"] = SimusAuthentication.ExternalScope,
        ["institucional"] = SimusAuthentication.InstitutionalScope
    };

    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        var notifications = group.MapGroup("/notificaciones").WithTags("notificaciones");

        notifications.MapGet(string.Empty, async (
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            string ambito,
            int? limite,
            int? desplazamiento,
            CancellationToken cancellationToken) =>
        {
            var alcance = ResolveRequestedScope(principal, ambito);
            if (alcance is null)
            {
                return Results.BadRequest(new { message = "Indica el ámbito autorizado del buzón." });
            }

            var currentUser = await ResolveCurrentUserAsync(principal, alcance, dbContext, cancellationToken);
            if (currentUser is null) return Results.Unauthorized();

            var safeLimit = Math.Clamp(limite ?? 50, 1, 200);
            var safeOffset = Math.Max(desplazamiento ?? 0, 0);
            var email = CorreoElectronico.Normalizar(currentUser.Email);
            var query = dbContext.Notifications.AsNoTracking()
                .Where(item => (item.RecipientUserId == currentUser.Id || item.RecipientEmail == email)
                    && item.AccessScope == alcance
                    && item.DismissedAt == null);

            query = await SinLosModulosCerradosAsync(query, principal, alcance, dbContext, cancellationToken);
            query = query.OrderByDescending(item => item.CreatedAt);

            var total = await query.CountAsync(cancellationToken);
            var rows = await query.Skip(safeOffset).Take(safeLimit).ToListAsync(cancellationToken);
            return Results.Ok(new PagedResponse<NotificationDto>(rows.Select(ToDto).ToList(), safeLimit, safeOffset, total));
        // Ambas sesiones pueden consultar esta ruta. El ámbito solicitado debe coincidir con una
        // de las identidades autenticadas y con la fila persistida; así dos cookies simultáneas no
        // mezclan el trabajo institucional con los avisos de la organización.
        }).RequireAuthorization(SimusAuthentication.PoliticaCualquierSesion);

        // Limpiar el buzón es una ocultación personal, no un borrado del registro institucional.
        // Solo se permite sobre avisos ya leídos para que una acción masiva no oculte trabajo vivo.
        notifications.MapPost("/leidas/ocultar", async (
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            string ambito,
            CancellationToken cancellationToken) =>
        {
            var alcance = ResolveRequestedScope(principal, ambito);
            if (alcance is null) return Results.BadRequest(new { message = "Indica el ámbito autorizado del buzón." });
            var currentUser = await ResolveCurrentUserAsync(principal, alcance, dbContext, cancellationToken);
            if (currentUser is null) return Results.Unauthorized();

            var email = CorreoElectronico.Normalizar(currentUser.Email);
            var now = DateTime.UtcNow;
            var porOcultar = dbContext.Notifications
                .Where(item => (item.RecipientUserId == currentUser.Id || item.RecipientEmail == email)
                    && item.AccessScope == alcance
                    && item.ReadAt != null
                    && item.DismissedAt == null);
            // Limpiar el buzón limpia EL BUZON QUE SE VE: si un aviso de un módulo cerrado no
            // aparece en la lista, esta acción tampoco puede tocarlo.
            porOcultar = await SinLosModulosCerradosAsync(porOcultar, principal, alcance, dbContext, cancellationToken);
            var rows = await porOcultar.ToListAsync(cancellationToken);
            foreach (var row in rows) row.DismissedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { ocultadas = rows.Count });
        }).RequireAuthorization(SimusAuthentication.PoliticaCualquierSesion);

        notifications.MapPost("/{id:long}/lectura", async (
            long id,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            string ambito,
            CancellationToken cancellationToken) =>
        {
            var alcance = ResolveRequestedScope(principal, ambito);
            if (alcance is null)
            {
                return Results.BadRequest(new { message = "Indica el ámbito autorizado del buzón." });
            }

            var currentUser = await ResolveCurrentUserAsync(principal, alcance, dbContext, cancellationToken);
            if (currentUser is null) return Results.Unauthorized();

            var email = CorreoElectronico.Normalizar(currentUser.Email);
            var row = await dbContext.Notifications.FirstOrDefaultAsync(item =>
                item.Id == id
                && item.AccessScope == alcance
                && (item.RecipientUserId == currentUser.Id || item.RecipientEmail == email),
                cancellationToken);
            if (row is null)
            {
                return Results.NotFound();
            }

            row.ReadAt ??= DateTime.UtcNow;
            row.Status = "leida";
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToDto(row));
        }).RequireAuthorization(SimusAuthentication.PoliticaCualquierSesion);

        var admin = group.MapGroup("/admin/notificaciones").WithTags("admin-notificaciones");
        admin.MapPost(string.Empty, async (
            NotificationCreateRequest request,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (!CanCreateNotification(principal))
            {
                return Results.Forbid();
            }

            var errors = ValidateCreateRequest(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var recipientEmail = CorreoElectronico.Normalizar(request.RecipientEmail);
            var recipient = await dbContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Email == recipientEmail, cancellationToken);
            var now = DateTime.UtcNow;
            var channel = Clean(request.Channel);
            var row = new NotificationRow
            {
                RecipientUserId = recipient?.Id,
                RecipientEmail = recipientEmail,
                EventType = ValidationHelpers.SanitizeText(request.EventType, 100),
                AccessScope = ResolveAccessScopeValue(request.AmbitoAcceso)!,
                Channel = channel,
                Title = ValidationHelpers.SanitizeText(request.Title, 240),
                Body = ValidationHelpers.SanitizeText(request.Body, 2000),
                Status = channel == "internal" ? "enviada" : "pendiente",
                ModuloId = ValidationHelpers.SanitizeText(request.ModuleId, 80),
                RecordId = ValidationHelpers.SanitizeText(request.RecordId, 120),
                MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? null : request.MetadataJson,
                CreatedAt = now,
                SentAt = channel == "internal" ? now : null,
                Attempts = 0
            };

            dbContext.Notifications.Add(row);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/notificaciones/{row.Id}", ToDto(row));
        }).RequireAuthorization(SimusAuthentication.InstitutionalPolicy);

        return group;
    }

    private static Dictionary<string, string[]> ValidateCreateRequest(NotificationCreateRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ValidationHelpers.IsValidEmail(request.RecipientEmail))
        {
            errors["recipientEmail"] = ["Indica un correo destinatario valido."];
        }

        if (string.IsNullOrWhiteSpace(request.EventType) || request.EventType.Length > 100)
        {
            errors["eventType"] = ["Indica un tipo de evento claro."];
        }

        if (!AllowedChannels.Contains(Clean(request.Channel)))
        {
            errors["channel"] = ["Canal no valido. Usa internal, email o whatsapp."];
        }

        if (ResolveAccessScopeValue(request.AmbitoAcceso) is null)
        {
            errors["ambitoAcceso"] = ["Ámbito no válido. Usa externo o institucional."];
        }

        if (Clean(request.Channel) == "whatsapp")
        {
            errors["channel"] = ["WhatsApp esta preparado como canal futuro, pero aun no tiene proveedor configurado."];
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 240)
        {
            errors["title"] = ["Indica un titulo de notificacion de maximo 240 caracteres."];
        }

        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 2000)
        {
            errors["body"] = ["Indica un mensaje de notificacion de maximo 2000 caracteres."];
        }

        return errors;
    }

    private static bool CanCreateNotification(ClaimsPrincipal principal)
    {
        return principal.Identity?.IsAuthenticated == true
            && (principal.IsInRole("webmaster") || principal.IsInRole("gestor_interno"));
    }

    private static string? ResolveAccessScopeValue(string? value) =>
        AmbitosDeAcceso.TryGetValue(Clean(value), out var scope) ? scope : null;

    private static string? ResolveRequestedScope(ClaimsPrincipal principal, string? requestedScope)
    {
        var scope = ResolveAccessScopeValue(requestedScope);
        return scope is not null
            && principal.Identities.Any(identity => identity.IsAuthenticated
                && identity.HasClaim(SimusAuthentication.AccessScopeClaim, scope))
            ? scope
            : null;
    }

    private static async Task<UserRow?> ResolveCurrentUserAsync(
        ClaimsPrincipal principal,
        string accessScope,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var identity = principal.Identities.FirstOrDefault(item => item.IsAuthenticated
            && item.HasClaim(SimusAuthentication.AccessScopeClaim, accessScope));
        var rawUserId = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(rawUserId, out var userId))
        {
            return null;
        }

        return await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken);
    }

    private static NotificationDto ToDto(NotificationRow row)
    {
        return new NotificationDto(
            row.Id.ToString(CultureInfo.InvariantCulture),
            row.RecipientUserId?.ToString(CultureInfo.InvariantCulture),
            row.RecipientEmail ?? string.Empty,
            row.EventType,
            row.Channel,
            row.Title,
            row.Body,
            AllowedStatuses.Contains(row.Status) ? row.Status : "pendiente",
            row.ModuloId ?? string.Empty,
            row.RecordId ?? string.Empty,
            row.CreatedAt,
            row.SentAt,
            row.ReadAt,
            row.MetadataJson);
    }


    private static string Clean(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
