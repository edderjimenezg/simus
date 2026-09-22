using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>Consultas institucionales transversales del modelo vigente.</summary>
/// <remarks>
/// No es un CMS genérico: mientras Festival sea el único proceso con modelo y
/// circuito aprobados, es el único registro administrable desde esta frontera.
/// </remarks>
public static class AdminDataEndpoints
{
    private static readonly string[] CamposRequeridosFestival = ["name", "department"];
    private static readonly string[] CamposFestival = ["id", "name", "department", "municipality", "description"];
    private static readonly string[] CamposRequeridosParticipacion = ["actorType", "actorName", "email", "department", "municipality", "consent"];
    private static readonly string[] CamposParticipacion = ["reference", "submittedAt", "payloadJson"];

    private sealed record ModuloEnMonitor(string Id, string Label, string Area, int Total, IReadOnlyList<object> Statuses);

    public static RouteGroupBuilder MapAdminDataEndpoints(this RouteGroupBuilder group)
    {
        var admin = group.MapGroup("/admin/data").WithTags("admin-data");
        admin.RequireAuthorization(SimusAuthentication.InstitutionalPolicy);

        admin.MapGet("/schema", () => Results.Ok(new
        {
            festivals = new
            {
                table = "Festivales",
                soloLectura = true,
                escrituraGobernada = "El contenido de Festivales se gestiona mediante el circuito versionado.",
                required = CamposRequeridosFestival,
                fields = CamposFestival
            },
            divipola = new { table = "Divipola" },
            participation = new
            {
                table = "Participaciones",
                required = CamposRequeridosParticipacion,
                fields = CamposParticipacion
            }
        }));

        admin.MapGet("/stats", async (PnmcDbContext db, CancellationToken ct) => Results.Ok(new
        {
            festivals = await db.FestivalRecords.CountAsync(ct),
            divipola = await db.DivipolaLocations.CountAsync(ct),
            participation = await db.Participations.CountAsync(ct),
            users = await db.Users.CountAsync(ct),
            statuses = await db.ContentStatuses.CountAsync(ct)
        }));

        admin.MapGet("/monitor", async (PnmcDbContext db, IWebHostEnvironment environment, CancellationToken ct) =>
        {
            var startedAt = DateTime.UtcNow;
            var canConnect = await db.Database.CanConnectAsync(ct);
            var labels = await db.ContentStatuses.AsNoTracking()
                .ToDictionaryAsync(item => item.Code, item => item.Name, StringComparer.OrdinalIgnoreCase, ct);
            var festivalStatuses = await db.FestivalRecords.AsNoTracking()
                .GroupBy(item => item.StatusCode)
                .Select(group => new { Status = group.Key, Total = group.Count() })
                .ToListAsync(ct);
            // MERCADOS ENTRA AL MONITOR JUNTO A FESTIVALES. Un modulo del Ecosistema se conecta en
            // todas las superficies donde ya esta Festivales, y esta alimenta Salud del sistema y la
            // composicion del Resumen operativo. Lo fijo la direccion de producto el 15 de septiembre
            // de 2026: «debe conectarse todo... de principio a fin como festivales».
            var mercadoStatuses = await db.Mercados.AsNoTracking()
                .GroupBy(item => item.EstadoRegistro)
                .Select(group => new { Status = group.Key, Total = group.Count() })
                .ToListAsync(ct);
            var modules = new[]
            {
                new ModuloEnMonitor(
                    "festivals", "Festivales", "Ecosistema", festivalStatuses.Sum(item => item.Total),
                    festivalStatuses.Select(item => (object)new
                    {
                        code = item.Status,
                        label = labels.GetValueOrDefault(item.Status, item.Status),
                        total = item.Total
                    }).ToList()),
                new ModuloEnMonitor(
                    "mercados", "Mercados musicales", "Ecosistema", mercadoStatuses.Sum(item => item.Total),
                    mercadoStatuses.Select(item => (object)new
                    {
                        code = item.Status,
                        label = labels.GetValueOrDefault(item.Status, item.Status),
                        total = item.Total
                    }).ToList())
            };
            var recentAudit = await db.AuditLogs.AsNoTracking()
                .OrderByDescending(item => item.CreatedAt).Take(8)
                .Select(item => new { id = item.Id, table = item.TableName, recordId = item.RecordId, action = item.Action, createdAt = item.CreatedAt })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                checkedAt = DateTime.UtcNow,
                environment = environment.EnvironmentName,
                api = new { status = "ok", latencyMs = Math.Max(1, (int)(DateTime.UtcNow - startedAt).TotalMilliseconds), serverTimeUtc = DateTime.UtcNow },
                database = new { status = canConnect ? "ok" : "error", provider = db.Database.ProviderName, canConnect },
                web = new { status = "client-loaded", note = "El estado del frontend se verifica desde el navegador; la API confirma backend y base de datos." },
                totals = new
                {
                    records = modules.Sum(item => item.Total), modules = modules.Length,
                    users = await db.Users.CountAsync(ct), territories = await db.DivipolaLocations.CountAsync(ct),
                    entities = await db.EntityProfiles.CountAsync(item => item.IsActive, ct)
                },
                modules,
                statuses = labels.Select(item => new { code = item.Key, label = item.Value }).OrderBy(item => item.code),
                recentAudit
            });
        });

        // ORDEN, DIRECCION Y TERRITORIO SE APLICAN AQUI, Y HASTA EL 14 DE SEPTIEMBRE DE 2026 NO SE
        // APLICABAN. El contrato ya los declaraba —`AdminRegistrosRespuestaDto` devuelve `Orden`,
        // `Direccion` y `Territorios` desde el 26 de agosto— y la consola ya los pedia, pero esta
        // ruta ignoraba los tres: siempre ordenaba por fecha, devolvia `territorios` vacio y decia
        // haber aplicado «actualizacion desc». La consola, que refleja lo que el servidor aplico y
        // no lo que pidio, ensenaba una tabla cuyas cabeceras no ordenaban nada y un filtro
        // territorial con «(0)». Medido en navegador.
        admin.MapGet("/records/{moduleId}", async (
            string moduleId, PnmcDbContext db, string? q, string? estado, string? departamento, string? orden, string? direccion,
            bool? incluirBorradores, int? limite, int? desplazamiento, CancellationToken ct) =>
        {
            if (!string.Equals(moduleId, "festivals", StringComparison.OrdinalIgnoreCase)) return Results.NotFound();

            var limit = Math.Clamp(limite ?? 100, 1, 500);
            var offset = Math.Max(desplazamiento ?? 0, 0);
            var festivals = await db.FestivalRecords.AsNoTracking()
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).ToListAsync(ct);
            var departments = await db.DivipolaLocations.AsNoTracking()
                .GroupBy(item => new { item.DepartmentCode, item.DepartmentName })
                .ToDictionaryAsync(group => group.Key.DepartmentCode, group => group.Key.DepartmentName, ct);
            var municipalities = await db.DivipolaLocations.AsNoTracking()
                .ToDictionaryAsync(item => item.MunicipalityCode, item => item.MunicipalityName, ct);
            // La tabla Festivales conserva el identificador de su organización administradora.
            // La consola no debe recibir solo ese número: sin este diccionario la interfaz cae
            // en «Sin organización administradora» aun cuando la relación exista y sea válida.
            var organizaciones = await db.EntityProfiles.AsNoTracking()
                .Where(item => item.IsActive)
                .ToDictionaryAsync(item => item.Id, item => item.Name, ct);
            var responsables = await db.EntidadesResponsable.AsNoTracking()
                .ToDictionaryAsync(item => item.IdEntidad, item => item.ResponsableNombre, ct);
            var mostrarBorradores = incluirBorradores ?? true;
            var filtered = festivals.Where(item =>
                (string.IsNullOrWhiteSpace(q) || item.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                 || (item.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                && (string.IsNullOrWhiteSpace(estado) || string.Equals(item.StatusCode, estado, StringComparison.OrdinalIgnoreCase))
                && (mostrarBorradores || !string.Equals(item.StatusCode, EstadosFestival.Borrador, StringComparison.OrdinalIgnoreCase))).ToList();
            // DE DONDE VINO CADA FESTIVAL. Va en `Metadata` y no en una columna nueva del DTO: la
            // procedencia es un dato del registro, no una propiedad del listado, y ahí ya viven
            // los demás datos propios de Festival —organización, responsable, cobertura—.
            var procedencias = await ProcedenciaDeRegistro.LeerVariasAsync(
                db,
                Modulos.Festivales,
                filtered.Select(item => item.Id.ToString(CultureInfo.InvariantCulture)).ToList(),
                ct);
            // LAS FACETAS SE CUENTAN ANTES DE APLICAR EL TERRITORIO, para que el desplegable siga
            // ofreciendo los demas departamentos —con su recuento— mientras uno esta elegido.
            var territorios = filtered
                .GroupBy(item => item.DepartmentCode ?? FacetaDto.SinTerritorio)
                .Select(group => new FacetaDto(
                    group.Key,
                    group.Key == FacetaDto.SinTerritorio ? "Sin territorio" : departments.GetValueOrDefault(group.Key, group.Key),
                    group.Count()))
                .OrderBy(item => item.Codigo == FacetaDto.SinTerritorio ? 1 : 0)
                .ThenBy(item => item.Etiqueta, StringComparer.CurrentCulture)
                .ToList();
            if (!string.IsNullOrWhiteSpace(departamento) && !string.Equals(departamento, "todos", StringComparison.OrdinalIgnoreCase))
            {
                filtered = string.Equals(departamento, FacetaDto.SinTerritorio, StringComparison.OrdinalIgnoreCase)
                    ? filtered.Where(item => item.DepartmentCode is null).ToList()
                    : filtered.Where(item => string.Equals(item.DepartmentCode, departamento, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // EL ORDEN QUE SE APLICA ES EL QUE SE DEVUELVE. Un valor desconocido cae en «prioridad»
            // —lo que espera a la consola, primero— y la respuesta lo dice, para que la cabecera
            // marcada sea la que de verdad ordeno.
            var ordenAplicado = (orden ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "titulo" or "estado" or "territorio" or "actualizacion" => orden!.Trim().ToLowerInvariant(),
                _ => "prioridad",
            };
            var descendente = string.Equals(direccion, "desc", StringComparison.OrdinalIgnoreCase);
            var direccionAplicada = descendente ? "desc" : "asc";
            string Territorio(FestivalRow item) =>
                (item.DepartmentCode is not null ? departments.GetValueOrDefault(item.DepartmentCode, string.Empty) : string.Empty)
                + " / " + (item.MunicipalityCode is not null ? municipalities.GetValueOrDefault(item.MunicipalityCode, string.Empty) : string.Empty);
            IOrderedEnumerable<FestivalRow> ordenados = ordenAplicado switch
            {
                "titulo" => descendente
                    ? filtered.OrderByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    : filtered.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
                "estado" => descendente
                    ? filtered.OrderByDescending(item => item.StatusCode, StringComparer.Ordinal)
                    : filtered.OrderBy(item => item.StatusCode, StringComparer.Ordinal),
                "territorio" => descendente
                    ? filtered.OrderByDescending(Territorio, StringComparer.CurrentCultureIgnoreCase)
                    : filtered.OrderBy(Territorio, StringComparer.CurrentCultureIgnoreCase),
                "actualizacion" => descendente
                    ? filtered.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                    : filtered.OrderBy(item => item.UpdatedAt ?? item.CreatedAt),
                // Prioridad: lo que espera una decision de la consola va delante; dentro de cada
                // grupo, lo mas reciente primero. «desc» invierte solo el grupo, no la fecha.
                _ => descendente
                    ? filtered.OrderByDescending(item => string.Equals(item.StatusCode, EstadosFestival.EnRevision, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    : filtered.OrderBy(item => string.Equals(item.StatusCode, EstadosFestival.EnRevision, StringComparison.OrdinalIgnoreCase) ? 0 : 1),
            };
            if (ordenAplicado != "actualizacion") ordenados = ordenados.ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt);
            var ordenadosLista = ordenados.ToList();

            var records = ordenadosLista.Select(item => ToFestivalRecord(item, departments, municipalities, organizaciones, responsables, procedencias)).ToList();
            var states = ordenadosLista.GroupBy(item => item.StatusCode)
                .Select(group => new FacetaDto(group.Key, group.Key, group.Count()))
                .OrderBy(item => item.Etiqueta, StringComparer.CurrentCulture).ToList();

            return Results.Ok(new AdminRegistrosRespuestaDto(
                records.Skip(offset).Take(limit).ToList(), limit, offset, records.Count, states, territorios, ordenAplicado, direccionAplicada));
        });

        admin.MapPost("/records/{moduleId}/{id:int}/status", (string moduleId) =>
            string.Equals(moduleId, "festivals", StringComparison.OrdinalIgnoreCase)
                ? Results.Conflict(new { message = "El estado de un Festival no se modifica desde la administración genérica. Use el circuito institucional de revisión y publicación." })
                : Results.NotFound());

        return admin;
    }

    private static AdminRecordDto ToFestivalRecord(
        FestivalRow festival,
        IReadOnlyDictionary<string, string> departments,
        IReadOnlyDictionary<string, string> municipalities,
        Dictionary<int, string> organizaciones,
        Dictionary<int, string> responsables,
        Dictionary<string, ProcedenciaDeRegistroDto> procedencias)
    {
        procedencias.TryGetValue(festival.Id.ToString(CultureInfo.InvariantCulture), out var procedencia);

        var updatedAt = festival.UpdatedAt ?? festival.CreatedAt;
        return new AdminRecordDto(
            festival.Id.ToString(CultureInfo.InvariantCulture), festival.Name, "Festivales",
            festival.DepartmentCode is not null ? departments.GetValueOrDefault(festival.DepartmentCode, string.Empty) : string.Empty,
            festival.MunicipalityCode is not null ? municipalities.GetValueOrDefault(festival.MunicipalityCode, string.Empty) : string.Empty,
            festival.StatusCode, festival.StatusCode,
            updatedAt.ToString("O", CultureInfo.InvariantCulture), updatedAt.ToString("O", CultureInfo.InvariantCulture), "UTC",
            new Dictionary<string, object?>
            {
                ["name"] = festival.Name,
                ["description"] = festival.Description,
                ["coverageLevel"] = festival.CoverageLevel,
                ["organizationId"] = festival.OrganizacionPrincipalId,
                ["organizationName"] = festival.OrganizacionPrincipalId is int organizacionId
                    && organizaciones.TryGetValue(organizacionId, out var nombreOrganizacion)
                    ? nombreOrganizacion : null,
                ["responsibleName"] = festival.OrganizacionPrincipalId is int idEntidad
                    && responsables.TryGetValue(idEntidad, out var nombreResponsable)
                    ? nombreResponsable : null,
                ["administrationType"] = festival.OrganizacionPrincipalId is null ? "sin_vincular" : "organizacion",
                // TRES DIMENSIONES DISTINTAS, Y AQUI SE VEN LAS TRES. `organizationName` dice quién
                // RESPONDE por el Festival; esto dice quién lo INCORPORÓ y con qué cuenta. Que el
                // PNMC lo haya registrado no lo convierte en su organización responsable.
                ["procedenciaContexto"] = procedencia?.ContextoOrigen,
                ["procedenciaEtiqueta"] = procedencia?.ContextoEtiqueta,
                ["procedenciaEsInstitucional"] = procedencia?.EsInstitucional,
                ["procedenciaOrganizacion"] = procedencia?.OrganizacionProcedenciaNombre,
                ["procedenciaUsuario"] = procedencia?.UsuarioCreadorNombre,
                ["procedenciaFecha"] = procedencia?.FechaRegistro
            });
    }
}
