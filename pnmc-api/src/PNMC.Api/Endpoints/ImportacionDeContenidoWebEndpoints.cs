using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Importar a la base el respaldo que el panel exporta desde <c>localStorage</c>
/// (incremento 2C).
/// <para>
/// Tres rutas y un principio: <b>importar no publica</b>. Se escribe únicamente
/// la columna <c>Borrador</c> de <c>[ContenidoWeb]</c> y de <c>[EquipoWeb]</c>.
/// Nunca se tocan <c>Publicado</c> ni <c>FechaRetiro</c>, de modo que el estado
/// derivado del incremento 2D se conserva por construcción y ninguna
/// importación puede cambiar lo que ve un visitante.
/// </para>
/// <para>
/// El flujo es de tres pasos y en ese orden: descargar la instantánea actual
/// (que es la vuelta atrás), simular, y aplicar citando la huella que devolvió
/// la simulación.
/// </para>
/// </summary>
public static class ImportacionDeContenidoWebEndpoints
{
    private static readonly string[] EditorRoles = ["webmaster", "gestor_interno"];

    /// <summary>
    /// Aplicar exige webmaster, aunque solo escriba borradores y por la regla de
    /// 2A bastaría con el rol de edición. Es una divergencia deliberada y el
    /// motivo es el radio de daño, no la visibilidad: es la única ruta del CMS
    /// que pisa 238 borradores ajenos y la nómina entera en una sola llamada.
    /// <b>No lo "corrija" a EditorRoles.</b> gestor_interno conserva su edición
    /// por grupo, que es reversible mirando una pantalla.
    /// </summary>
    private static readonly string[] ImporterRoles = ["webmaster"];

    public sealed record ImportRequest(
        RespaldoDeContenidoWeb? Backup,
        string? PlanHash,
        bool OverwriteEdited);

    public static RouteGroupBuilder MapImportacionDeContenidoWebEndpoints(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin/contenido-web").WithTags("admin-importacion-de-contenido-web");
        admin.RequireAuthorization();

        // ---------- 1. La vuelta atrás ----------
        admin.MapGet("/backup", async (
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
        {
            if (!HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            // Se emite en el MISMO formato que exporta el panel, para que la
            // instantánea de hoy se pueda volver a importar mañana. Sin esto, una
            // importación equivocada no tendría vuelta atrás: la siembra no
            // restaura el borrador de una fila que ya existe.
            var rows = await dbContext.ContenidoWeb.AsNoTracking()
                .OrderBy(x => x.Key)
                .Select(x => new { x.Key, x.Draft, x.Published })
                .ToListAsync(cancellationToken);

            var teamRow = await dbContext.EquipoWeb.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            var now = DateTime.UtcNow.ToString("o");

            var texts = rows.ToDictionary(
                x => x.Key,
                x => new
                {
                    content = x.Draft,
                    publishedContent = x.Published,
                    status = x.Published is null ? "borrador" : "publicado",
                    updatedAt = now,
                    updatedBy = "API",
                });

            return Results.Ok(new
            {
                schemaVersion = RespaldoDeContenidoWeb.SupportedSchemaVersion,
                app = RespaldoDeContenidoWeb.ExpectedApp,
                exportedAt = now,
                exportedBy = ResolveAuthor(principal),
                stores = new Dictionary<string, object?>
                {
                    [RespaldoDeContenidoWeb.TextStoreKey] = texts,
                    // Vacío a propósito: no existe tabla de medios y nada en el
                    // front-end escribe ese almacén. Va presente y vacío para que
                    // el archivo tenga la forma completa que espera el panel.
                    [RespaldoDeContenidoWeb.MediaStoreKey] = new Dictionary<string, object>(),
                    [RespaldoDeContenidoWeb.TeamStoreKey] = teamRow is null ? null : new
                    {
                        content = ContratoDeEquipoWeb.Deserialize(teamRow.Draft),
                        publishedContent = teamRow.Published is null
                            ? null
                            : ContratoDeEquipoWeb.Deserialize(teamRow.Published),
                        status = teamRow.Published is null ? "borrador" : "publicado",
                        updatedAt = now,
                        updatedBy = "API",
                    },
                },
            });
        })
        .WithName("GetWebContentBackup");

        // ---------- 2. El ensayo en seco ----------
        admin.MapPost("/import/preview", async (
            RespaldoDeContenidoWeb? backup,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            // Anulable a propósito: omitir la bandera debe significar «no pises
            // nada editado», que es el valor prudente, no un 400 por su ausencia.
            bool? overwriteEdited,
            CancellationToken cancellationToken) =>
        {
            // La simulacion no escribe, pero se protege igual: sin token seria una
            // via para que otro sitio leyera el contenido de la base a traves del
            // navegador de un editor con sesion abierta.
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            if (!HasAnyRole(principal, EditorRoles))
            {
                return Results.Forbid();
            }

            var envelopeError = PlanificadorDeImportacionDeContenidoWeb.ValidateEnvelope(backup);
            if (envelopeError is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["backup"] = [envelopeError],
                });
            }

            // Ruta de solo lectura: AsNoTracking y ningún SaveChanges. Que el plan
            // se calcule en una función pura hace que esto siga siendo cierto
            // aunque alguien añada código aquí más adelante.
            var plan = await BuildPlanAsync(dbContext, backup!, overwriteEdited ?? false, tracked: false, cancellationToken);
            return Results.Ok(BuildReport(backup!, plan, applied: false));
        })
        .WithName("PreviewWebContentImport")
        .RequireRateLimiting("web-content-import");

        // ---------- 3. La aplicación ----------
        admin.MapPost("/import", async (
            ImportRequest request,
            PnmcDbContext dbContext,
            ClaimsPrincipal principal,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, httpContext);
            if (csrf is not null) { return csrf; }

            if (!HasAnyRole(principal, ImporterRoles))
            {
                return Results.Forbid();
            }

            var envelopeError = PlanificadorDeImportacionDeContenidoWeb.ValidateEnvelope(request.Backup);
            if (envelopeError is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["backup"] = [envelopeError],
                });
            }

            if (string.IsNullOrWhiteSpace(request.PlanHash))
            {
                // Igual que la versión en 2B: es un fallo distinto de «alguien se
                // le adelantó», y merece una respuesta distinta.
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["planHash"] = ["Simule la importación primero y envíe la huella que devuelve."],
                });
            }

            var plan = await BuildPlanAsync(dbContext, request.Backup!, request.OverwriteEdited, tracked: true, cancellationToken);

            // La huella cubre las claves, sus valores entrantes y las versiones
            // observadas. Si no coincide, o el archivo no es el que se simuló, o
            // alguien editó entre mirar y apretar el botón. En ambos casos lo
            // correcto es no escribir y pedir que se vuelva a mirar.
            if (!string.Equals(plan.PlanHash, request.PlanHash, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new
                {
                    message = "El plan cambió desde la simulación: el archivo es otro, o alguien editó mientras tanto. " +
                              "Vuelva a simular y revise antes de aplicar.",
                    planHashEsperado = request.PlanHash,
                    planHashActual = plan.PlanHash,
                });
            }

            if (!plan.WritesAnything)
            {
                return Results.Ok(BuildReport(request.Backup!, plan, applied: true, changed: 0));
            }

            var author = ResolveAuthor(principal);
            var now = DateTime.UtcNow;
            var rows = await dbContext.ContenidoWeb
                .Where(x => plan.TextsToApply.Select(t => t.Key).Contains(x.Key))
                .ToDictionaryAsync(x => x.Key, cancellationToken);

            foreach (var planned in plan.TextsToApply)
            {
                if (!rows.TryGetValue(planned.Key, out var row))
                {
                    continue;
                }

                row.Draft = planned.IncomingDraft;
                await GuardasDelCms.RecordHistoryAsync(
                    dbContext, planned.Key, GuardasDelCms.AccionImportado,
                    planned.IncomingDraft, author, now, cancellationToken);
                // Publicado y FechaRetiro NO se tocan. Una clave retirada cuyo
                // borrador cambie sigue retirada: para volver al sitio hace falta
                // la acción explícita de republicar, del incremento 2D.
                row.Version++;
                row.UpdatedBy = author;
                row.UpdatedAt = now;
            }

            if (plan.TeamToApply is not null)
            {
                var teamRow = await dbContext.EquipoWeb.FirstOrDefaultAsync(cancellationToken);
                if (teamRow is not null)
                {
                    teamRow.Draft = ContratoDeEquipoWeb.Serialize(plan.TeamToApply);
                    teamRow.Version++;
                    teamRow.UpdatedBy = author;
                    teamRow.UpdatedAt = now;
                }
            }

            try
            {
                // Un solo SaveChanges: una transacción, todo o nada. No existe un
                // resultado a medio importar.
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new
                {
                    message = "Alguien modificó el contenido mientras se aplicaba la importación. " +
                              "No se escribió nada. Vuelva a simular.",
                });
            }

            var changed = plan.TextsToApply.Count + (plan.TeamToApply is not null ? 1 : 0);
            return Results.Ok(BuildReport(request.Backup!, plan, applied: true, changed));
        })
        .WithName("ApplyWebContentImport")
        .RequireRateLimiting("web-content-import");

        return group;
    }

    private static async Task<ImportPlan> BuildPlanAsync(
        PnmcDbContext dbContext,
        RespaldoDeContenidoWeb backup,
        bool overwriteEdited,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = tracked ? dbContext.ContenidoWeb : dbContext.ContenidoWeb.AsNoTracking();
        var rows = await query.ToListAsync(cancellationToken);

        var teamQuery = tracked ? dbContext.EquipoWeb : dbContext.EquipoWeb.AsNoTracking();
        var teamRow = await teamQuery.FirstOrDefaultAsync(cancellationToken);

        var compiled = SembradorDeContenidoWeb.LoadCatalog()
            .ToDictionary(entry => entry.Key, entry => entry.DefaultValue, StringComparer.Ordinal);

        return PlanificadorDeImportacionDeContenidoWeb.Plan(backup, rows, teamRow, compiled, overwriteEdited);
    }

    /// <summary>
    /// El informe. Es la única salida del import, así que dice tanto lo que entró
    /// como lo que <b>no</b> entró y por qué: un import que descarta en silencio
    /// es indistinguible de uno que funcionó.
    /// </summary>
    private static object BuildReport(
        RespaldoDeContenidoWeb backup,
        ImportPlan plan,
        bool applied,
        int changed = 0)
    {
        var cap = PlanificadorDeImportacionDeContenidoWeb.MaxNamesReported;

        return new
        {
            aplicado = applied,
            planHash = plan.PlanHash,
            archivo = new
            {
                schemaVersion = backup.SchemaVersion,
                exportedAt = backup.ExportedAt,
                exportedBy = backup.ExportedBy,
            },
            textos = new
            {
                porAplicar = plan.TextsToApply.Count,
                aplicados = applied ? plan.TextsToApply.Count : 0,
                sinCambio = plan.TextsUnchanged,
                rechazados = plan.TextsRejected.Take(cap),
                rechazadosTotal = plan.TextsRejected.Count,
                enConflicto = plan.TextsConflicting.Take(cap),
                enConflictoTotal = plan.TextsConflicting.Count,
            },
            equipo = new
            {
                porAplicar = plan.TeamToApply is not null,
                personas = plan.TeamToApply?.Count,
                sinCambio = plan.TeamUnchanged,
                motivoRechazo = plan.TeamRejectedReason,
                personasRechazadas = plan.MembersRejected.Take(cap),
            },
            noImportado = new
            {
                // Nombradas, no traducidas: nada se pierde en silencio.
                clavesDesconocidas = plan.UnknownKeys.Take(cap),
                clavesDesconocidasTotal = plan.UnknownKeys.Count,
                publicadoIgualAlBorrador = plan.PublishedIdenticalToDraft,
                publicadoDistinto = plan.PublishedDiffering.Take(cap),
                publicadoDistintoTotal = plan.PublishedDiffering.Count,
                revisionesDescartadas = plan.DiscardedRevisions,
                entradasDeMedios = plan.MediaEntries,
                nominaPublicadaPresente = plan.TeamPublishedPresent,
            },
            cambios = changed,
            aviso = "Importar no publica. El sitio público no cambia con esta operación: " +
                    "lo importado queda como borrador y hay que publicarlo desde el panel.",
        };
    }

    private static bool HasAnyRole(ClaimsPrincipal principal, string[] roles)
    {
        return principal.Identity?.IsAuthenticated == true
            && roles.Any(principal.IsInRole);
    }

    private static string ResolveAuthor(ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(name) ? "Desconocido" : name.Trim();
    }
}
