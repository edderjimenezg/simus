using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using PNMC.Api.Security;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

public static class NormalizacionVersionesFestivalEndpoints
{
    public static RouteGroupBuilder MapNormalizacionVersionesFestivalEndpoints(this RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional/festivales").WithTags("normalizacion-versiones-festival");
        // Migracion masiva de datos: crea una version vigente para cada Festival publicado que
        // no la tenga. Estaba cerrada solo con «hay cookie de consola» y sin ninguna
        // comprobacion de rol en sus 116 lineas.
        institucional.RequireAuthorization(Permisos.PoliticaFuncionario);
        institucional.MapGet("/diagnostico-normalizacion-versiones-historicas", async (PnmcDbContext db, CancellationToken ct) =>
        {
            var candidatos = await ConsultarCandidatosPublicosAsync(db, ct);
            var vigentes = await db.VersionesFestival.AsNoTracking()
                .Where(item => item.EsVigente)
                .Select(item => item.FestivalOrigenId)
                .ToListAsync(ct);
            var pendientes = candidatos.Where(item => !vigentes.Contains(item.Id)).OrderBy(item => item.Id).ToList();
            var idsPendientes = pendientes.Select(item => item.Id).ToArray();
            var conPracticas = (await db.ValoresPorRegistroAsync<PracticaMusicalDeRegistroRow>(
                Modulos.Festivales, idsPendientes, ct)).Keys.ToList();
            var conTerritorios = (await db.ValoresPorRegistroAsync<TerritorioSonoroDeRegistroRow>(
                Modulos.Festivales, idsPendientes, ct)).Keys.ToList();
            return Results.Ok(new
            {
                totalFestivalesPublicos = candidatos.Count,
                conVersionVigente = vigentes.Count,
                pendientesNormalizacion = pendientes.Count,
                pendientes = pendientes.Select(item => new
                {
                    idFestival = item.Id,
                    nombre = item.Name,
                    estadoRegistro = item.StatusCode,
                    tienePracticasMusicales = conPracticas.Contains(item.Id),
                    tieneTerritoriosSonoros = conTerritorios.Contains(item.Id),
                    inconsistencias = ObtenerInconsistencias(item)
                })
            });
        });
        institucional.MapPost("/normalizar-versiones-historicas", async (PnmcDbContext db, IAntiforgery antiforgery, HttpContext context, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, context))
                return Results.BadRequest(new { message = "La normalización no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            var candidatos = await ConsultarCandidatosPublicosAsync(db, ct);
            var existentes = await db.VersionesFestival.AsNoTracking().Where(item => item.EsVigente).Select(item => item.FestivalOrigenId).ToListAsync(ct);
            var pendientes = candidatos.Where(item => !existentes.Contains(item.Id)).ToList();
            if (pendientes.Count == 0) return Results.Ok(new { normalizados = 0, message = "No hay Festivales públicos sin versión vigente." });
            // DOS BLOQUEOS ENCADENADOS que hacian que este POST muriera SIEMPRE contra SQL
            // Server, medidos el 23 ago 2026:
            //
            //  1. `BeginTransactionAsync` a pelo bajo `EnableRetryOnFailure`. EF lo
            //     prohibe —una transaccion iniciada por el usuario no se puede reintentar—
            //     y el primer `SaveChangesAsync` lanzaba antes de llegar a la bitacora.
            //     La forma correcta es envolver la unidad de trabajo en la estrategia de
            //     ejecucion, que reintenta el bloque entero.
            //  2. `Action = "FestivalHistoricoNormalizado"`: un nombre de evento funcional
            //     en una columna que `CK_BitacoraAuditoria_Accion` cierra a trece verbos
            //     tecnicos. Se traduce con `AccionesAuditoria.DeEvento`, como los otros seis
            //     escritores del circuito, y el nombre del evento viaja en ValoresNuevos.
            //
            // La suite no lo veia porque corre sobre SQLite, que no tiene estrategia de
            // reintento ni aplica ese CHECK. Ver el carril de SQL Server en las pruebas.
            const string evento = "FestivalHistoricoNormalizado";
            var estrategia = db.Database.CreateExecutionStrategy();
            var normalizados = await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await db.Database.BeginTransactionAsync(ct);
                var ahora = DateTime.UtcNow;
                var versiones = pendientes.Select(festival => new VersionFestivalRow
                {
                    FestivalOrigenId = festival.Id, NumeroVersion = 1, EsVigente = true, Nombre = festival.Name,
                    // NACE PUBLICADA: esta ruta solo alcanza Festivales publicados, y la versión que
                    // crea ES lo que el público ya está leyendo. Sin estado no era ni borrador ni
                    // publicada, y la organización no podía tocarla ni sabía por qué.
                    EstadoRegistro = EstadosFestival.Publicado,
                    Descripcion = festival.Description, NivelCobertura = festival.CoverageLevel, CodigoDepartamento = festival.DepartmentCode,
                    CodigoMunicipio = festival.MunicipalityCode, Periodicidad = festival.Periodicidad, PeriodicidadDetalle = festival.PeriodicidadDetalle, CorreoContacto = festival.ContactEmail,
                    FechaPublicacion = festival.UpdatedAt ?? festival.CreatedAt, FechaCreacion = ahora
                }).ToList();
                db.VersionesFestival.AddRange(versiones);
                await db.SaveChangesAsync(ct);
                var ids = pendientes.Select(item => item.Id).ToArray();
                var practicas = await db.ValoresPorRegistroAsync<PracticaMusicalDeRegistroRow>(Modulos.Festivales, ids, ct);
                var territorios = await db.ValoresPorRegistroAsync<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, ids, ct);
                var versionesPorFestival = versiones.ToDictionary(item => item.FestivalOrigenId, item => item.Id);
                foreach (var (festivalId, valores) in practicas)
                {
                    db.Agregar<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, versionesPorFestival[festivalId], valores, ahora);
                }
                foreach (var (festivalId, valores) in territorios)
                {
                    db.Agregar<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, versionesPorFestival[festivalId], valores, ahora);
                }
                var personaInstitucionalId = ObtenerPersonaInstitucionalId(context.User);
                foreach (var festival in pendientes)
                {
                    db.AuditLogs.Add(new AuditLogRow
                    {
                        UserId = personaInstitucionalId,
                        TableName = "Festivales",
                        RecordId = festival.Id.ToString(),
                        Action = AccionesAuditoria.DeEvento(evento),
                        PreviousValuesJson = null,
                        NewValuesJson = JsonSerializer.Serialize(new
                        {
                            evento,
                            versionFestivalId = versionesPorFestival[festival.Id],
                            numeroVersion = 1,
                            esVigente = true,
                            origen = "normalizacion-historica"
                        }),
                        CreatedAt = ahora
                    });
                }
                await db.SaveChangesAsync(ct);
                await transaccion.CommitAsync(ct);
                return pendientes.Count;
            });
            return Results.Ok(new { normalizados, message = "Se crearon instantáneas técnicas de versiones públicas sin modificar estados institucionales." });
        });
        return group;
    }

    private static Task<List<FestivalRow>> ConsultarCandidatosPublicosAsync(PnmcDbContext db, CancellationToken ct) =>
        db.FestivalRecords.Where(item => item.StatusCode == null || item.StatusCode == "publicado" || item.StatusCode == "Publicado").ToListAsync(ct);

    private static int? ObtenerPersonaInstitucionalId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var personaId) ? personaId : null;

    private static List<string> ObtenerInconsistencias(FestivalRow festival)
    {
        var inconsistencias = new List<string>();
        if (string.IsNullOrWhiteSpace(festival.Name)) inconsistencias.Add("Sin nombre.");
        if (string.IsNullOrWhiteSpace(festival.DepartmentCode)) inconsistencias.Add("Sin código DIVIPOLA de departamento.");
        return inconsistencias;
    }
}
