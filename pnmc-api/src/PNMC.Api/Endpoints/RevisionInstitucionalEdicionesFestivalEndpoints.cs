using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>Supervisión posterior de las Ediciones publicadas por sus organizaciones.</summary>
/// <remarks>La institución no aprueba Ediciones antes de publicarse: puede observar, despublicar o archivar con trazabilidad.</remarks>
public static class RevisionInstitucionalEdicionesFestivalEndpoints
{
    private const string Modulo = Modulos.EdicionesDeFestival;

    public static RouteGroupBuilder MapRevisionInstitucionalEdicionesFestivalEndpoints(this RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional/ediciones-festival").WithTags("supervision-institucional-ediciones-festival");
        institucional.RequireAuthorization(Permisos.PoliticaFuncionario);
        institucional.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new TokenAntiforgeryRespuesta(tokens.RequestToken ?? string.Empty));
        });

        institucional.MapGet("/publicadas", async (PnmcDbContext db, CancellationToken ct) =>
        {
            var filas = await (from edicion in db.EdicionesFestival.AsNoTracking()
                               join festival in db.FestivalRecords.AsNoTracking() on edicion.FestivalId equals festival.Id
                               where edicion.EstadoVisibilidad == "publicada"
                               orderby edicion.FechaActualizacion ?? edicion.FechaCreacion descending
                               select new { Edicion = edicion, Festival = festival }).ToListAsync(ct);
            var organizaciones = await db.EntityProfiles.AsNoTracking().Where(x => x.IsActive).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
            return Results.Ok(filas.Select(x => new EdicionRevisionInstitucionalDto(
                x.Edicion.Id.ToString(CultureInfo.InvariantCulture), x.Festival.Id.ToString(CultureInfo.InvariantCulture), x.Festival.Name,
                x.Festival.OrganizacionPrincipalId is int id && organizaciones.TryGetValue(id, out var nombre) ? nombre : "Organización no disponible",
                x.Edicion.Anio, x.Edicion.Nombre, x.Edicion.FechaActualizacion ?? x.Edicion.FechaCreacion)));
        });

        institucional.MapGet("/{edicionId:int}", async (int edicionId, PnmcDbContext db, CancellationToken ct) =>
        {
            var edicion = await db.EdicionesFestival.AsNoTracking().FirstOrDefaultAsync(x => x.Id == edicionId, ct);
            if (edicion is null) return Results.NotFound();
            var festival = await db.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == edicion.FestivalId, ct);
            if (festival is null) return Results.NotFound();
            var historial = await db.HistorialesRevisionRegistros.AsNoTracking()
                .Where(x => x.ModuloId == Modulo && x.RegistroId == edicionId.ToString())
                .OrderByDescending(x => x.Fecha)
                .Select(x => new { x.EstadoAnterior, x.EstadoNuevo, x.Accion, x.Comentario, x.MotivoRechazo, x.Fecha, x.ActorNombre }).ToListAsync(ct);
            return Results.Ok(new { edicion = EdicionesFestivalExternosEndpoints.ADto(edicion), festivalId = festival.Id, festivalNombre = festival.Name, historial });
        });

        institucional.MapPost("/{edicionId:int}/supervision", async (int edicionId, DecisionRevisionFestivalSolicitud solicitud,
            ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext context, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, context)) return Results.BadRequest(new { message = "La acción de supervisión no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            var funcionarioId = ObtenerPersonaId(principal); if (funcionarioId is null) return Results.Unauthorized();
            var edicion = await db.EdicionesFestival.FirstOrDefaultAsync(x => x.Id == edicionId, ct); if (edicion is null) return Results.NotFound();
            var decision = ResolverDecision(solicitud, edicion.EstadoVisibilidad); if (decision.Errores.Count > 0) return Results.ValidationProblem(decision.Errores);
            var ahora = DateTime.UtcNow; var anterior = edicion.EstadoVisibilidad;
            if (decision.EstadoNuevo is not null)
            {
                edicion.EstadoVisibilidad = decision.EstadoNuevo;
                edicion.EstadoRegistro = decision.EstadoNuevo == "publicada" ? EstadosFestival.Publicado : EstadosFestival.Borrador;
            }
            edicion.FechaActualizacion = ahora;
            var historial = new HistorialRevisionRegistroRow
            {
                ModuloId = Modulo, RegistroId = edicion.Id.ToString(CultureInfo.InvariantCulture),
                // EN EL VOCABULARIO DEL HISTORIAL. `CK_RegistrosRevisionHistorial_EstadoNuevo` solo
                // admite los siete códigos del circuito de contenidos, en masculino: escribir
                // «publicada» aquí devolvía 500 contra SQL Server y en SQLite pasaba en verde.
                EstadoAnterior = EdicionesFestivalExternosEndpoints.HaciaElHistorial(anterior),
                EstadoNuevo = EdicionesFestivalExternosEndpoints.HaciaElHistorial(decision.EstadoNuevo ?? anterior),
                Accion = decision.Evento!, Comentario = decision.Observacion,
                UsuarioId = funcionarioId.Value, Fecha = ahora, MetadataJson = $"{{\"FestivalId\":{edicion.FestivalId},\"Motivo\":\"supervision_posterior\"}}",
            };
            var festival = await db.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == edicion.FestivalId, ct);
            await InstantaneaDelHistorial.TomarAsync(db, historial, festival?.OrganizacionPrincipalId, funcionarioId.Value, ct);
            db.HistorialesRevisionRegistros.Add(historial);
            db.AuditLogs.Add(new AuditLogRow { UserId = funcionarioId.Value, TableName = "EdicionesFestival", RecordId = edicion.Id.ToString(CultureInfo.InvariantCulture), Action = AccionesAuditoria.Actualizar, PreviousValuesJson = $"{{\"EstadoVisibilidad\":\"{anterior}\"}}", NewValuesJson = $"{{\"EstadoVisibilidad\":\"{decision.EstadoNuevo ?? anterior}\",\"Evento\":\"{decision.Evento}\"}}", CreatedAt = ahora });
            await db.SaveChangesAsync(ct);
            return Results.Ok(EdicionesFestivalExternosEndpoints.ADto(edicion));
        });
        return group;
    }

    private static (string? EstadoNuevo, string? Evento, string? Observacion, Dictionary<string, string[]> Errores) ResolverDecision(DecisionRevisionFestivalSolicitud solicitud, string estadoActual)
    {
        var accion = solicitud.Accion?.Trim(); var observacion = Limpiar(solicitud.Observacion) ?? Limpiar(solicitud.MotivoRechazo);
        if (accion is "Observar" or "Despublicar" or "Archivar" && observacion is null) return (null, null, null, new() { ["observacion"] = ["La justificación es obligatoria para una acción de supervisión."] });
        return accion switch
        {
            "Observar" => (null, "EdicionObservada", observacion, new()),
            "Despublicar" when estadoActual != "publicada" => (null, null, null, new() { ["accion"] = ["Solo una edición publicada puede despublicarse."] }),
            "Despublicar" => ("borrador", "EdicionDespublicada", observacion, new()),
            "Archivar" when estadoActual == "archivada" => (null, null, null, new() { ["accion"] = ["La edición ya está archivada."] }),
            "Archivar" => ("archivada", "EdicionArchivada", observacion, new()),
            _ => (null, null, null, new() { ["accion"] = ["La acción válida es Observar, Despublicar o Archivar."] }),
        };
    }

    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    private static int? ObtenerPersonaId(ClaimsPrincipal principal) => int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
}
