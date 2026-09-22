using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>Ajustes generales y por campo de una edición temporal.</summary>
/// <remarks>
/// <b>ESCRIBE EN LAS MISMAS DOS TABLAS QUE FESTIVALES Y MERCADOS.</b> Hasta el 18 de septiembre de
/// 2026 este circuito tenía su propio par —<c>RevisionesEdicionesFestival</c> y sus observaciones—,
/// que era el único de los tres que no había pasado a la forma genérica. Estaban vacías, así que
/// absorberlas no movió un solo dato: solo dejó de haber dos maneras de guardar lo mismo. El
/// registro se identifica aquí, como en todas partes, por <c>ModuloId</c> + <c>RegistroId</c>.
/// </remarks>
public static class RevisionDeCamposEdicionFestivalEndpoints
{
    /// <summary>El módulo con el que estas revisiones se guardan y se recuperan.</summary>
    /// <remarks>
    /// ES EL MISMO QUE YA ESCRIBIA LA BITACORA. <c>HistorialesRevisionRegistros</c> venía anotando
    /// <c>ediciones_festival</c> desde antes; ahora la revisión y su historial se nombran igual.
    /// </remarks>
    internal const string Modulo = Modulos.EdicionesDeFestival;

    private const string Borrador = "borrador", Enviada = "enviada", Cerrada = "cerrada", Pendiente = "pendiente", Atendida = "atendida";
    private const string AmbitoPrincipal = "principal";

    public static RouteGroupBuilder MapRevisionDeCamposEdicionFestivalEndpoints(this RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional/ediciones-festival").WithTags("revision-por-campos-ediciones");
        institucional.RequireAuthorization(Permisos.PoliticaFuncionario);
        institucional.MapGet("/{id:int}/revision", async (int id, PnmcDbContext db, CancellationToken ct) =>
        {
            if (!await db.EdicionesFestival.AsNoTracking().AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
            return Results.Ok(await DtoAsync(db, id, ct));
        });
        institucional.MapPut("/{id:int}/revision", async (int id, GuardarRevisionDeCamposDeRegistroSolicitud solicitud, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery anti, HttpContext ctx, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(anti, ctx)) return Results.BadRequest(new { message = "El borrador de revisión no pudo validarse." });
            var usuario = Usuario(principal); if (usuario is null) return Results.Unauthorized();
            var edicion = await db.EdicionesFestival.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct); if (edicion is null) return Results.NotFound();
            var notas = Normalizar(solicitud, out var errores); if (errores.Count > 0) return Results.ValidationProblem(errores);
            var revision = await VivaAsync(db, id, true, ct); if (revision is { Estado: Enviada }) return Results.Conflict(new { message = "La solicitud ya fue enviada." });
            var ahora = DateTime.UtcNow;
            revision ??= NuevaRevision(id, usuario.Value, ahora);
            if (revision.Id == 0) db.RevisionesDeRegistro.Add(revision);
            revision.ObservacionGeneral = Limpiar(solicitud.ObservacionGeneral); revision.FechaActualizacion = ahora;
            await ReemplazarAsync(db, revision, notas, ahora, ct); await db.SaveChangesAsync(ct);
            return Results.Ok(await DtoAsync(db, id, ct));
        });
        institucional.MapPost("/{id:int}/revision/enviar", async (int id, GuardarRevisionDeCamposDeRegistroSolicitud solicitud, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery anti, HttpContext ctx, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(anti, ctx)) return Results.BadRequest(new { message = "El envío no pudo validarse." });
            var usuario = Usuario(principal); if (usuario is null) return Results.Unauthorized();
            var edicion = await db.EdicionesFestival.FirstOrDefaultAsync(x => x.Id == id, ct); if (edicion is null) return Results.NotFound();
            if (!string.Equals(edicion.EstadoVisibilidad, "publicada", StringComparison.OrdinalIgnoreCase)) return Results.Conflict(new { message = "Solo una edición publicada puede recibir observaciones de supervisión." });
            var notas = Normalizar(solicitud, out var errores); if (errores.Count > 0) return Results.ValidationProblem(errores);
            if (notas.Count == 0 && Limpiar(solicitud.ObservacionGeneral) is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["observaciones"] = ["Indica una observación general o al menos un campo."] });
            var revision = await VivaAsync(db, id, true, ct); if (revision is { Estado: Enviada }) return Results.Conflict(new { message = "La solicitud ya fue enviada." });
            var ahora = DateTime.UtcNow;
            revision ??= NuevaRevision(id, usuario.Value, ahora);
            if (revision.Id == 0) db.RevisionesDeRegistro.Add(revision);
            revision.IdUsuarioRevisor = usuario.Value; revision.ObservacionGeneral = Limpiar(solicitud.ObservacionGeneral); revision.Estado = Enviada; revision.FechaEnvio = ahora; revision.FechaActualizacion = ahora;
            await ReemplazarAsync(db, revision, notas, ahora, ct);
            edicion.FechaActualizacion = ahora;
            db.HistorialesRevisionRegistros.Add(new HistorialRevisionRegistroRow { ModuloId = Modulo, RegistroId = Clave(id), EstadoAnterior = EdicionesFestivalExternosEndpoints.HaciaElHistorial(edicion.EstadoVisibilidad), EstadoNuevo = EdicionesFestivalExternosEndpoints.HaciaElHistorial(edicion.EstadoVisibilidad), Accion = "EdicionObservadaPorCampo", Comentario = revision.ObservacionGeneral, UsuarioId = usuario.Value, Fecha = ahora });
            await db.SaveChangesAsync(ct); return Results.Ok(await DtoAsync(db, id, ct));
        });

        var externo = group.MapGroup("/externo/ediciones").WithTags("revision-por-campos-ediciones");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);
        externo.MapGet("/{id:int}/cambios-pedidos", async (int id, ClaimsPrincipal p, PnmcDbContext db, CancellationToken ct) =>
        { if (!await PuedeEditarAsync(id, p, db, ct)) return Results.Forbid(); return Results.Ok(await DtoAsync(db, id, ct, soloEnviada: true)); });
        externo.MapPost("/cambios-pedidos/{notaId:long}/atender", async (long notaId, AtenderCambioSolicitud s, ClaimsPrincipal p, PnmcDbContext db, IAntiforgery anti, HttpContext ctx, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(anti, ctx)) return Results.BadRequest(new { message = "La marca no pudo validarse." });
            var usuario = Usuario(p); if (usuario is null) return Results.Unauthorized();
            var nota = await db.RevisionesDeRegistroObservaciones.FirstOrDefaultAsync(x => x.Id == notaId, ct); if (nota is null) return Results.NotFound();
            // LA TABLA ES COMPARTIDA, ASI QUE EL MODULO SE COMPRUEBA. Sin este filtro, el identificador
            // de una observación de Mercados o de Festival entraría por esta ruta y la atendería
            // alguien de otra organización.
            var revision = await db.RevisionesDeRegistro.AsNoTracking().FirstOrDefaultAsync(x => x.Id == nota.IdRevision && x.ModuloId == Modulo && x.Estado == Enviada, ct); if (revision is null) return Results.Forbid();
            if (!int.TryParse(revision.RegistroId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var edicionId) || !await PuedeEditarAsync(edicionId, p, db, ct)) return Results.Forbid();
            var ahora = DateTime.UtcNow; nota.Estado = s.Atendida ? Atendida : Pendiente; nota.FechaAtencion = s.Atendida ? ahora : null; nota.IdUsuarioAtiende = s.Atendida ? usuario : null; nota.FechaActualizacion = ahora; await db.SaveChangesAsync(ct); return Results.Ok(NotaDto(nota));
        });
        return group;
    }

    /// <summary>La clave con la que una edición se nombra dentro de su módulo.</summary>
    internal static string Clave(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static RevisionDeRegistroRow NuevaRevision(int id, int revisor, DateTime ahora) =>
        new() { ModuloId = Modulo, RegistroId = Clave(id), IdUsuarioRevisor = revisor, FechaCreacion = ahora };

    private static async Task<RevisionEdicionDeCamposDto> DtoAsync(PnmcDbContext db, int id, CancellationToken ct, bool soloEnviada = false) { var clave = Clave(id); var r = await db.RevisionesDeRegistro.AsNoTracking().Where(x => x.ModuloId == Modulo && x.RegistroId == clave && (!soloEnviada || x.Estado == Enviada)).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct); if (r is null) return new(0, clave, Borrador, null, null, []); var n = await db.RevisionesDeRegistroObservaciones.AsNoTracking().Where(x => x.IdRevision == r.Id).OrderBy(x => x.Id).ToListAsync(ct); return new(r.Id, clave, r.Estado, r.ObservacionGeneral, r.FechaEnvio, n.Select(NotaDto).ToList()); }
    private static async Task<RevisionDeRegistroRow?> VivaAsync(PnmcDbContext db, int id, bool track, CancellationToken ct) { var clave = Clave(id); var consulta = track ? db.RevisionesDeRegistro : db.RevisionesDeRegistro.AsNoTracking(); return await consulta.FirstOrDefaultAsync(x => x.ModuloId == Modulo && x.RegistroId == clave && x.Estado != Cerrada, ct); }
    private static List<RevisionDeRegistroObservacionRow> Normalizar(GuardarRevisionDeCamposDeRegistroSolicitud s, out Dictionary<string,string[]> e) { e = new(StringComparer.OrdinalIgnoreCase); var salida = new List<RevisionDeRegistroObservacionRow>(); foreach (var x in s.Observaciones ?? []) { var nota = Limpiar(x.Nota); var campo = Limpiar(x.CampoId); if (nota is null || campo is null || Limpiar(x.SeccionId) is null || Limpiar(x.CampoEtiqueta) is null) { e["observaciones"] = ["Cada observación requiere sección, campo, etiqueta y nota."]; continue; } if (salida.Any(y => y.CampoId == campo)) { e["observaciones"] = ["No repitas un campo en la misma revisión."]; continue; } salida.Add(new() { Ambito = AmbitoPrincipal, SeccionId = x.SeccionId!.Trim(), CampoId = campo, CampoEtiqueta = x.CampoEtiqueta!.Trim(), ValorObservado = Limpiar(x.ValorObservado), Nota = nota }); } return salida; }
    private static async Task ReemplazarAsync(PnmcDbContext db, RevisionDeRegistroRow r, List<RevisionDeRegistroObservacionRow> n, DateTime ahora, CancellationToken ct) { var viejas = r.Id == 0 ? [] : await db.RevisionesDeRegistroObservaciones.Where(x => x.IdRevision == r.Id).ToListAsync(ct); db.RevisionesDeRegistroObservaciones.RemoveRange(viejas); foreach (var x in n) { x.IdRevision = r.Id; x.Revision = r; x.FechaCreacion = ahora; } db.RevisionesDeRegistroObservaciones.AddRange(n); }
    private static ObservacionEdicionDeCampoDto NotaDto(RevisionDeRegistroObservacionRow x) => new(x.Id, x.SeccionId, x.CampoId, x.CampoEtiqueta, x.ValorObservado, x.Nota, x.Estado, x.FechaAtencion);
    /// <summary>
    /// Si esta persona puede editar los campos de esta Edición.
    /// </summary>
    /// <remarks>
    /// SE RESUELVE LA ORGANIZACION Y DESPUES SE PREGUNTA POR ELLA. Esta comprobación miraba el
    /// vínculo de la persona con la organización del Festival, pero NO si la organización sigue
    /// activa: una dada de baja seguía pudiendo proponer correcciones sobre sus Ediciones. Ahora
    /// usa la misma regla que el resto del canal externo.
    /// </remarks>
    private static async Task<bool> PuedeEditarAsync(int id, ClaimsPrincipal p, PnmcDbContext db, CancellationToken ct)
    {
        var usuario = Usuario(p);
        if (usuario is null) return false;

        var organizacion = await (from edicion in db.EdicionesFestival.AsNoTracking()
                                  join festival in db.FestivalRecords.AsNoTracking() on edicion.FestivalId equals festival.Id
                                  where edicion.Id == id
                                  select festival.OrganizacionPrincipalId).FirstOrDefaultAsync(ct);
        if (organizacion is null) return false;

        return await AdministracionDeOrganizacion.PuedeAdministrarAsync(db, usuario.Value, organizacion.Value, ct);
    }
    private static string? Limpiar(string? x) => string.IsNullOrWhiteSpace(x) ? null : x.Trim(); private static int? Usuario(ClaimsPrincipal p) => int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
