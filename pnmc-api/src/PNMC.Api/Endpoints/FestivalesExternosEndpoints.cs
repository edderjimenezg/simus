using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Correo;
using PNMC.Infrastructure.Data;

using PNMC.Domain.Entities;

namespace PNMC.Api.Endpoints;

public static class FestivalesExternosEndpoints
{
    public static RouteGroupBuilder MapFestivalesExternosEndpoints(this RouteGroupBuilder group)
    {
        var externo = group.MapGroup("/externo").WithTags("festivales-externos");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        externo.MapGet("/organizaciones/mis", async (ClaimsPrincipal principal, PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();

            // El identificador se formatea despues de materializar: ToString(CultureInfo)
            // no tiene traduccion a SQL y rompia la consulta completa.
            var filas = await dbContext.UserEntities.AsNoTracking()
                .Where(item => item.UserId == personaId && RolesDeEntidad.QueResponden.Contains(item.EntityRole) && item.IsActive)
                .Join(dbContext.EntityProfiles.AsNoTracking().Where(item => item.IsActive),
                    relacion => relacion.EntityId,
                    organizacion => organizacion.Id,
                    (_, organizacion) => new { organizacion.Id, organizacion.Name })
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            var organizaciones = filas
                .Select(item => new OrganizacionAdministradaDto(
                    item.Id.ToString(CultureInfo.InvariantCulture), item.Name))
                .ToList();
            return Results.Ok(organizaciones);
        });

        // LOS DOCE CATALOGOS DEL FORMULARIO DE FESTIVAL.
        //
        // EL MANEJADOR SE EXTRAJO EL 29 DE AGOSTO DE 2026 y ahora lo comparten dos puertas: esta,
        // para la organizacion que llena la ficha, y `/institucional/catalogos/festival`, para el
        // funcionario que la revisa. Los dos pintan LA MISMA ficha; con dos consultas distintas,
        // un catalogo nuevo aparece en una pantalla y no en la otra, y eso se ve como un
        // desplegable vacio en vez de como un error.
        externo.MapGet("/catalogos/festival", async (PnmcDbContext dbContext, CancellationToken cancellationToken) =>
            Results.Ok(await CatalogosDelFestivalAsync(dbContext, cancellationToken)));

        externo.MapPost("/organizaciones/{organizacionId:int}/festivales", async (
            int organizacionId,
            CrearFestivalBorradorSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "La solicitud de Festival no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            if (!await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken)) return Results.Forbid();

            // SIN CORREO CONFIRMADO NO SE CREAN PROCESOS. La regla la fijó el usuario el 12 de
            // septiembre de 2026: la puerta está ANTES del formulario, no al entregar. Registrar un
            // Festival es representar a la organización ante el Programa, y eso pide una dirección
            // comprobada a la que poder escribir.
            if (await AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync(
                    dbContext, organizacionId, "registrar Festivales", cancellationToken) is { } sinConfirmar)
            {
                return sinConfirmar;
            }

            var errores = await AltaDeFestival.ValidarAsync(solicitud, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var estadoBorradorDisponible = await dbContext.ContentStatuses.AsNoTracking()
                .AnyAsync(item => item.Code == EstadosFestival.Borrador, cancellationToken);
            if (!estadoBorradorDisponible)
            {
                return Results.Problem(
                    title: "Configuración incompleta del sistema",
                    detail: "No se puede crear el Festival porque falta el estado operativo de borrador. Actualiza el servicio o comunícate con la administración técnica.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            // SQL Server usa EnableRetryOnFailure. Por eso la transaccion debe vivir dentro de
            // su estrategia de ejecucion: abrirla directamente funciona en SQLite, pero el motor
            // real rechaza el primer SaveChanges y devuelve 500. La unidad incluye el Festival,
            // sus relaciones, la auditoria y el cierre del borrador temporal para que un reintento
            // no deje un alta incompleta.
            var estrategia = dbContext.Database.CreateExecutionStrategy();
            var festival = await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                var ahora = DateTime.UtcNow;

                // EL ALTA ES LA MISMA QUE USA LA CONSOLA INSTITUCIONAL. Lo unico que distingue a
                // los dos canales es de donde viene el registro y quien responde por el: aqui la
                // organizacion que tiene la sesion es las dos cosas a la vez.
                var nuevoFestival = await AltaDeFestival.CrearAsync(
                    dbContext,
                    solicitud,
                    organizacionResponsableId: organizacionId,
                    usuarioId: personaId.Value,
                    contexto: ProcedenciaDeRegistro.Externo,
                    organizacionProcedenciaId: organizacionId,
                    ahora: ahora,
                    cancellationToken: cancellationToken);

                // El autoguardado previo al alta no es otro Festival. Si permanece abierto, la
                // siguiente visita a «Registrar proceso» recupera los datos del Festival anterior y
                // hace que un segundo registro nazca sobre el primero. Se cierra solo el borrador
                // exacto, de la misma persona y organización; un identificador ajeno o ya cancelado
                // no bloquea la creación del Festival.
                if (long.TryParse(solicitud.BorradorProcesoId, NumberStyles.None, CultureInfo.InvariantCulture, out var borradorProcesoId))
                {
                    var borradorProceso = await dbContext.BorradoresDeProceso.FirstOrDefaultAsync(item =>
                        item.Id == borradorProcesoId
                        && item.ModuloId == Modulos.Festivales
                        && item.OrganizacionId == organizacionId
                        && item.PersonaId == personaId.Value
                        && item.Estado == "borrador",
                        cancellationToken);
                    if (borradorProceso is not null)
                    {
                        borradorProceso.Estado = "cerrado";
                        borradorProceso.FechaActualizacion = ahora;
                    }
                }

                RegistrarAuditoria(dbContext, personaId.Value, nuevoFestival.Id, organizacionId, "FestivalCreado");
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
                return nuevoFestival;
            });

            return Results.Created($"/api/v1/externo/festivales/{festival.Id}", await ADtoAsync(festival, dbContext, cancellationToken));
        });

        externo.MapGet("/organizaciones/{organizacionId:int}/festivales", async (
            int organizacionId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            if (!await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken)) return Results.Forbid();

            var festivales = await dbContext.FestivalRecords.AsNoTracking()
                // Algunos registros históricos guardaron la etiqueta de contrato en vez del
                // código interno; ambos representan el mismo retiro y no deben volver al panel.
                .Where(item => item.OrganizacionPrincipalId == organizacionId
                    && item.StatusCode != EstadosFestival.Archivado
                    && item.StatusCode != "Archivado")
                .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                .ToListAsync(cancellationToken);
            var resultado = new List<FestivalBorradorDto>();
            foreach (var festival in festivales) resultado.Add(await ADtoAsync(festival, dbContext, cancellationToken));

            // EL RECUENTO DE CAMBIOS PEDIDOS SE PIDE UNA VEZ PARA TODA LA LISTA y no dentro de
            // `ADtoAsync`. Ese metodo corre una vez por Festival, y la organizacion 115 tiene 40:
            // meterlo ahi seria cuarenta consultas mas para pintar cuarenta tarjetas.
            var pendientes = await RevisionDeCamposFestivalEndpoints.CambiosPendientesPorFestivalAsync(
                dbContext, festivales.Select(item => item.Id).ToArray(), cancellationToken);

            // NOMBRES DE TERRITORIO EN UNA SOLA CONSULTA, no una por Festival: mismo motivo que
            // `pendientes` de arriba. `VersionsCount`/`LastEditionDate` no necesitan consulta
            // alguna -ya viven en la fila del Festival, mantenidos por
            // `EdicionesFestivalExternosEndpoints.cs` cada vez que cambian sus ediciones-.
            var codigosDepartamento = festivales.Select(item => item.DepartmentCode).Where(codigo => !string.IsNullOrWhiteSpace(codigo)).Distinct().ToArray();
            var codigosMunicipio = festivales.Select(item => item.MunicipalityCode).Where(codigo => !string.IsNullOrWhiteSpace(codigo)).Distinct().ToArray();
            var nombresDepartamento = await dbContext.DivipolaLocations.AsNoTracking()
                .Where(item => codigosDepartamento.Contains(item.DepartmentCode))
                .Select(item => new { item.DepartmentCode, item.DepartmentName })
                .Distinct().ToDictionaryAsync(item => item.DepartmentCode, item => item.DepartmentName, cancellationToken);
            var nombresMunicipio = await dbContext.DivipolaLocations.AsNoTracking()
                .Where(item => codigosMunicipio.Contains(item.MunicipalityCode))
                .Select(item => new { item.MunicipalityCode, item.MunicipalityName })
                .Distinct().ToDictionaryAsync(item => item.MunicipalityCode, item => item.MunicipalityName, cancellationToken);
            var idsFestivales = festivales.Select(festival => festival.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
            var retiros = await dbContext.RecordLinkRequests.AsNoTracking()
                .Where(item => item.ModuloId == "festivales_retiro" && idsFestivales.Contains(item.RecordId)
                    && (item.Status == "pendiente" || item.Status == "en_revision" || item.Status == "ajustes_solicitados"))
                .ToDictionaryAsync(item => item.RecordId, item => item.Status, cancellationToken);

            for (var i = 0; i < resultado.Count; i++)
            {
                var festival = festivales[i];
                resultado[i] = resultado[i] with
                {
                    CambiosPedidos = pendientes.TryGetValue(festival.Id, out var cuantos) ? cuantos : 0,
                    NombreDepartamento = festival.DepartmentCode is not null && nombresDepartamento.TryGetValue(festival.DepartmentCode, out var nombreDepartamento) ? nombreDepartamento : null,
                    NombreMunicipio = festival.MunicipalityCode is not null && nombresMunicipio.TryGetValue(festival.MunicipalityCode, out var nombreMunicipio) ? nombreMunicipio : null,
                    CuantasEdiciones = festival.VersionsCount ?? 0,
                    FechaActualizacion = festival.UpdatedAt ?? festival.CreatedAt,
                    EstadoSolicitudRetiro = retiros.TryGetValue(festival.Id.ToString(CultureInfo.InvariantCulture), out var estadoRetiro) ? estadoRetiro : null,
                };
            }
            return Results.Ok(resultado);
        });

        externo.MapGet("/organizaciones/{organizacionId:int}/festivales/{festivalId:int}/historial", async (
            int organizacionId, int festivalId, ClaimsPrincipal principal, PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            if (!await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken)) return Results.Forbid();
            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == festivalId && item.OrganizacionPrincipalId == organizacionId, cancellationToken);
            if (festival is null) return Results.NotFound();

            var ediciones = await dbContext.EdicionesFestival.AsNoTracking().Where(item => item.FestivalId == festivalId)
                .Select(item => item.Id.ToString()).ToArrayAsync(cancellationToken);
            var registroFestival = festivalId.ToString(CultureInfo.InvariantCulture);
            var entradas = await dbContext.HistorialesRevisionRegistros.AsNoTracking()
                .Where(item => (item.ModuloId == Modulos.Festivales && item.RegistroId == registroFestival)
                    || (item.ModuloId == Modulos.EdicionesDeFestival && ediciones.Contains(item.RegistroId)))
                .OrderByDescending(item => item.Fecha).Take(100).ToListAsync(cancellationToken);
            return Results.Ok(entradas.Select(item => new
            {
                modulo = item.ModuloId == Modulos.EdicionesDeFestival ? "Edición" : "Festival",
                accion = item.Accion, estadoAnterior = item.EstadoAnterior, estadoNuevo = item.EstadoNuevo,
                comentario = item.Comentario, fecha = item.Fecha
            }));
        });

        externo.MapGet("/organizaciones/{organizacionId:int}/festivales/coincidencias", async (
            int organizacionId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            if (!await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken)) return Results.Forbid();

            var organizacion = await dbContext.EntityProfiles.AsNoTracking()
                .Where(item => item.Id == organizacionId && item.IsActive)
                .Select(item => new { item.Name, DepartmentCode = item.HeadquartersDepartmentCode, MunicipalityCode = item.HeadquartersMunicipalityCode })
                .FirstOrDefaultAsync(cancellationToken);
            if (organizacion is null) return Results.NotFound();

            // QUE SIGNIFICA «ELEGIBLE» DESDE (25 ago 2026). Antes era «no tiene
            // organizacion responsable», y sobre ese NULL vivia esta funcionalidad entera.
            // Ahora ningun festival esta sin nadie que responda por el: mientras ninguna
            // organizacion de la comunidad lo reclame, responde la institucion. Asi que
            // elegible pasa a ser «hoy lo tiene la institucion», y reclamarlo es un traspaso
            // y no la apropiacion de algo abandonado. Es la misma lista de festivales; lo que
            // cambia es lo que la ficha publica puede decir de ellos mientras tanto.
            //
            // Se resuelve por la marca y no por un identificador cableado: `EsInstitucional`
            // tiene indice unico filtrado, asi que solo puede haber una.
            var idInstitucional = await dbContext.EntityProfiles.AsNoTracking()
                .Where(item => item.IsInstitutional)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var festivalesElegibles = idInstitucional is null ? [] : await dbContext.FestivalRecords.AsNoTracking()
                // Las dos grafias, copiando el predicado de LecturaFestivalesPublicados.cs
                // letra por letra. No es redundancia: la base local es insensible a
                // mayusculas y SQLite no, asi que un solo literal se comporta distinto en
                // pruebas y contra SQL Server. Los 30 historicos estan en "publicado".
                .Where(item => (item.StatusCode == EstadosFestival.Publicado || item.StatusCode == "Publicado")
                    && item.OrganizacionPrincipalId == idInstitucional && item.Name != "")
                .Select(item => new
                {
                    item.Id,
                    item.Name,
                    item.Description,
                    item.OrganizerDisplayName,
                    item.DepartmentCode,
                    item.MunicipalityCode
                })
                .ToListAsync(cancellationToken);

            var fuentesHistoricas = await dbContext.ReferenciasHistoricasFestival.AsNoTracking()
                .Where(item => item.OrganizacionId == organizacionId)
                .Select(item => item.FestivalId)
                .ToListAsync(cancellationToken);
            var festivalesConFuenteHistorica = fuentesHistoricas.ToHashSet();

            var ubicaciones = await dbContext.DivipolaLocations.AsNoTracking()
                .Select(item => new { item.DepartmentCode, item.DepartmentName, item.MunicipalityCode, item.MunicipalityName })
                .ToListAsync(cancellationToken);
            var departamentos = ubicaciones
                .GroupBy(item => item.DepartmentCode)
                .ToDictionary(item => item.Key, item => item.First().DepartmentName, StringComparer.OrdinalIgnoreCase);
            var municipios = ubicaciones
                .ToDictionary(item => (item.DepartmentCode, item.MunicipalityCode), item => item.MunicipalityName);

            var nombreOrganizacion = NormalizarNombreCoincidencia(organizacion.Name);
            var coincidencias = new List<CoincidenciaFestivalHistoricoDto>();
            foreach (var festival in festivalesElegibles)
            {
                var coincidenciaNominal = nombreOrganizacion.Length > 0
                    && nombreOrganizacion == NormalizarNombreCoincidencia(festival.OrganizerDisplayName);
                var coincideMunicipio = !string.IsNullOrWhiteSpace(organizacion.DepartmentCode)
                    && !string.IsNullOrWhiteSpace(organizacion.MunicipalityCode)
                    && string.Equals(organizacion.DepartmentCode, festival.DepartmentCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(organizacion.MunicipalityCode, festival.MunicipalityCode, StringComparison.OrdinalIgnoreCase);
                var coincideDepartamento = !string.IsNullOrWhiteSpace(organizacion.DepartmentCode)
                    && string.Equals(organizacion.DepartmentCode, festival.DepartmentCode, StringComparison.OrdinalIgnoreCase);
                var coincideTerritorio = coincideMunicipio || coincideDepartamento;
                var tieneFuenteHistorica = festivalesConFuenteHistorica.Contains(festival.Id);

                if (!coincidenciaNominal && !(coincideTerritorio && tieneFuenteHistorica)) continue;

                var evidencias = new List<string>();
                string tipoCoincidencia;
                if (coincidenciaNominal && coincideTerritorio)
                {
                    tipoCoincidencia = "NominalYTerritorial";
                    evidencias.Add("El organizador histórico coincide con el nombre de tu organización.");
                }
                else if (coincidenciaNominal)
                {
                    tipoCoincidencia = "NominalExacta";
                    evidencias.Add("El organizador histórico coincide con el nombre de tu organización.");
                }
                else
                {
                    tipoCoincidencia = "EvidenciaHistoricaTerritorial";
                    evidencias.Add("Existe una referencia histórica de esta organización para el Festival.");
                }

                if (coincideMunicipio) evidencias.Add("La organización y el Festival están registrados en el mismo municipio.");
                else if (coincideDepartamento) evidencias.Add("La organización y el Festival están registrados en el mismo departamento.");

                var codigoDepartamento = festival.DepartmentCode ?? string.Empty;
                departamentos.TryGetValue(codigoDepartamento, out var nombreDepartamento);
                municipios.TryGetValue((codigoDepartamento, festival.MunicipalityCode ?? string.Empty), out var nombreMunicipio);
                coincidencias.Add(new CoincidenciaFestivalHistoricoDto(
                    festival.Id.ToString(CultureInfo.InvariantCulture), festival.Name, festival.Description,
                    string.IsNullOrWhiteSpace(festival.DepartmentCode) ? null : festival.DepartmentCode, nombreDepartamento,
                    festival.MunicipalityCode, nombreMunicipio, festival.OrganizerDisplayName, tipoCoincidencia, evidencias));
            }

            return Results.Ok(coincidencias
                .OrderBy(item => item.TipoCoincidencia == "NominalYTerritorial" ? 0 : item.TipoCoincidencia == "NominalExacta" ? 1 : 2)
                .ThenBy(item => item.NombreFestival)
                .ToList());
        });

        externo.MapGet("/festivales/{festivalId:int}", async (
            int festivalId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is null
                || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, festival.OrganizacionPrincipalId.Value, cancellationToken)) return Results.Forbid();

            return Results.Ok(await ADtoAsync(festival, dbContext, cancellationToken));
        });

        externo.MapPut("/festivales/{festivalId:int}", async (
            int festivalId,
            CrearFestivalBorradorSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "La actualización del Festival no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is null
                || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, festival.OrganizacionPrincipalId.Value, cancellationToken)) return Results.Forbid();
            if (!EsEditable(festival.StatusCode))
            {
                return Results.Conflict(new { message = "Este Festival no puede modificarse directamente en su estado actual.", estado = EstadosFestival.HaciaContrato(festival.StatusCode) });
            }

            var errores = await AltaDeFestival.ValidarAsync(solicitud, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            festival.Name = ValidationHelpers.SanitizeText(solicitud.Nombre, 240);
            festival.Description = LimpiarTexto(solicitud.Descripcion);
            festival.Periodicidad = PeriodicidadesFestival.Normalizar(solicitud.Periodicidad);
            festival.PeriodicidadDetalle = LimpiarTexto(solicitud.PeriodicidadDetalle);
            festival.ContactEmail = CorreoElectronico.Normalizar(solicitud.CorreoContacto);
            festival.InstagramUrl = LimpiarTexto(solicitud.Instagram);
            festival.FacebookUrl = LimpiarTexto(solicitud.Facebook);
            festival.WebsiteUrl = LimpiarTexto(solicitud.PaginaWeb);
            festival.OtherUrl = LimpiarTexto(solicitud.OtroEnlace);
            festival.ContactPhone = LimpiarTexto(solicitud.TelefonoCelular);
            festival.ObservacionesContacto = LimpiarTexto(solicitud.ObservacionesContacto);
            festival.CoverageLevel = NormalizarNivel(solicitud.NivelCobertura);
            festival.DepartmentCode = DepartamentoSegunNivel(solicitud);
            festival.MunicipalityCode = MunicipioSegunNivel(solicitud);
            festival.UpdatedAt = ahora;

            await dbContext.FijarAsync<PracticaMusicalDeRegistroRow>(
                Modulos.Festivales, festival.Id, solicitud.PracticasMusicalesIds, ahora, cancellationToken);
            await dbContext.FijarAsync<TerritorioSonoroDeRegistroRow>(
                Modulos.Festivales, festival.Id, solicitud.TerritoriosSonorosIds, ahora, cancellationToken);
            RegistrarAuditoria(dbContext, personaId.Value, festival.Id, festival.OrganizacionPrincipalId.Value, "FestivalBorradorActualizado", fecha: ahora);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(await ADtoAsync(festival, dbContext, cancellationToken));
        });

        // Un Festival publicado conserva su identidad y su perfil sujeto a revisión. Sus canales
        // de contacto son la excepción deliberada: una red social o correo desactualizado daña la
        // consulta pública, y no modifica nombre, territorio, descripción, periodicidad ni
        // catálogos. Esta ruta no acepta esos campos por forma de contrato.
        externo.MapPut("/festivales/{festivalId:int}/contacto-publico", async (
            int festivalId,
            ActualizarContactoPublicoFestivalSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La actualización de contacto no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is not int organizacionId
                || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken)) return Results.Forbid();
            if (!EstadosFestival.Es(festival.StatusCode, EstadosFestival.Publicado))
                return Results.Conflict(new { message = "Solo un Festival publicado puede actualizar sus datos públicos de contacto por esta vía.", estado = EstadosFestival.HaciaContrato(festival.StatusCode) });

            var correo = CorreoElectronico.Normalizar(solicitud.CorreoContacto);
            if (correo is not null && !ValidationHelpers.IsValidEmail(correo))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["correoContacto"] = ["Ingresa un correo electrónico válido."] });
            var enlaces = new Dictionary<string, string?>
            {
                ["instagram"] = solicitud.Instagram,
                ["facebook"] = solicitud.Facebook,
                ["paginaWeb"] = solicitud.PaginaWeb,
                ["otroEnlace"] = solicitud.OtroEnlace,
            };
            var enlaceInvalido = enlaces.FirstOrDefault(item => !ValidationHelpers.IsValidHttpUrl(item.Value));
            if (enlaceInvalido.Key is not null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { [enlaceInvalido.Key] = ["Ingresa una URL válida con http:// o https://."] });

            var versionVigente = await dbContext.VersionesFestival
                .FirstOrDefaultAsync(item => item.FestivalOrigenId == festival.Id && item.EsVigente, cancellationToken);
            var ahora = DateTime.UtcNow;
            festival.ContactEmail = correo;
            festival.ContactPhone = LimpiarTexto(solicitud.TelefonoCelular);
            festival.InstagramUrl = LimpiarTexto(solicitud.Instagram);
            festival.FacebookUrl = LimpiarTexto(solicitud.Facebook);
            festival.WebsiteUrl = LimpiarTexto(solicitud.PaginaWeb);
            festival.OtherUrl = LimpiarTexto(solicitud.OtroEnlace);
            festival.UpdatedAt = ahora;

            // El portal lee el perfil versionado vigente cuando existe. Reflejar el cambio menor
            // en ambas capas evita que el panel diga una cosa y el portal siga mostrando otra.
            if (versionVigente is not null)
            {
                versionVigente.CorreoContacto = correo;
                versionVigente.TelefonoContacto = festival.ContactPhone;
                versionVigente.Instagram = festival.InstagramUrl;
                versionVigente.Facebook = festival.FacebookUrl;
                versionVigente.SitioWeb = festival.WebsiteUrl;
                versionVigente.OtroEnlace = festival.OtherUrl;
            }

            RegistrarAuditoria(dbContext, personaId.Value, festival.Id, organizacionId,
                "FestivalContactoPublicoActualizado", EstadosFestival.Publicado, EstadosFestival.Publicado, ahora);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(await ADtoAsync(festival, dbContext, cancellationToken));
        });

        externo.MapPost("/festivales/{festivalId:int}/enviar-a-revision", async (
            int festivalId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IEnviadorDeCorreo correo,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "El envío a revisión no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is null
                || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, festival.OrganizacionPrincipalId.Value, cancellationToken)) return Results.Forbid();
            if (!EsEditable(festival.StatusCode))
            {
                return Results.Conflict(new { message = "Solo un Festival en Borrador o con ajustes solicitados puede enviarse a revisión.", estado = EstadosFestival.HaciaContrato(festival.StatusCode) });
            }

            // EL CORREO SIN CONFIRMAR NO ENTREGA NADA AL PROGRAMA, y la puerta está aquí y no solo
            // en el botón del panel. Confirmar no hace falta para entrar ni para preparar el
            // registro; hace falta justo cuando el dato empieza a tener efectos, porque una errata
            // en esa dirección deja a la organización sin forma de recuperar su cuenta nunca.
            if (await AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync(
                    dbContext, festival.OrganizacionPrincipalId.Value,
                    "enviar este Festival a revisión", cancellationToken) is { } sinConfirmar)
            {
                return sinConfirmar;
            }

            // NO SE REENVIA CON AJUSTES SUGERIDOS SIN RESOLVER, y la regla vive aqui y no solo en el
            // boton del panel. El boton apagado es una cortesia; esta ruta esta abierta a cualquiera
            // con sesion externa. Sin esta comprobacion, el Festival volveria a la bandeja del
            // funcionario con las mismas correcciones que el pidio, sin una sola marcada, y el
            // circuito de campo por campo perderia justo lo que aporta: saber que se atendio.
            //
            // VA ANTES DE `ValidarFestivalParaRevisionAsync` a proposito: los errores de validacion
            // hablan de campos vacios, y aqui lo que falta no es un dato sino un acto de la persona.
            var sinAtender = await RevisionDeCamposFestivalEndpoints.CuantosCambiosPendientesAsync(
                dbContext, festival.Id, cancellationToken);
            if (sinAtender > 0)
            {
                return Results.Conflict(new
                {
                    message = sinAtender == 1
                        ? "Queda 1 ajuste sugerido pendiente. Corrige el campo y guarda el ajuste antes de volver a enviar el Festival a revisión."
                        : $"Quedan {sinAtender} ajustes sugeridos pendientes. Corrige y guarda cada ajuste antes de volver a enviar el Festival a revisión.",
                    cambiosPedidos = sinAtender,
                });
            }

            var errores = await ValidarFestivalParaRevisionAsync(festival, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            var estadoAnterior = festival.StatusCode;
            // Este es EL cambio que resucita la cola de revision institucional. Escribir
            // "EnRevision" aqui violaba FK_Festivales_EstadosContenido y la fila nunca
            // llegaba a la bandeja del funcionario. Medido contra PNMC_LOCAL.
            festival.StatusCode = EstadosFestival.EnRevision;
            festival.UpdatedAt = ahora;
            RegistrarAuditoria(dbContext, personaId.Value, festival.Id, festival.OrganizacionPrincipalId.Value,
                "FestivalEnviadoARevision", estadoAnterior, EstadosFestival.EnRevision, ahora);
            await EnviosDeRevisionDeFestival.RegistrarAsync(
                dbContext, festival, personaId.Value, estadoAnterior, ahora, cancellationToken);
            await RegistrarHistorialEnvioRevisionAsync(dbContext, personaId.Value, festival, estadoAnterior, ahora, cancellationToken);
            // LA SOLICITUD DE CAMBIOS SE CIERRA AL REENVIAR, y tiene que ir DENTRO de esta misma
            // transaccion: si se guardara aparte, quedaria un instante con el Festival ya en
            // revision y su solicitud todavia abierta, es decir con el funcionario leyendo una
            // ficha que la pantalla de la organizacion sigue dando por corregible.
            await RevisionDeCamposFestivalEndpoints.CerrarSolicitudViva(dbContext, festival.Id, ahora, cancellationToken);
            await CrearNotificacionEnvioRevisionAsync(dbContext, correo, personaId.Value, festival, ahora, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(await ADtoAsync(festival, dbContext, cancellationToken));
        });

        // Un Festival publicado es parte del catálogo y de su historial: jamás se borra desde
        // este canal. En cambio, un borrador o un envío aún no decidido puede retirarse sin
        // destruir trazabilidad: se archiva y queda su bitácora. La decisión institucional de
        // despublicar un Festival requiere su propio expediente, no un DELETE externo.
        externo.MapDelete("/festivales/{festivalId:int}", async (
            int festivalId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "El Festival no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();

            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is null
                || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, festival.OrganizacionPrincipalId.Value, cancellationToken)) return Results.Forbid();

            if (!EstadosFestival.Es(festival.StatusCode, EstadosFestival.Borrador)
                && !EstadosFestival.Es(festival.StatusCode, EstadosFestival.AjustesSolicitados)
                && !EstadosFestival.Es(festival.StatusCode, EstadosFestival.EnRevision))
            {
                return Results.Conflict(new { message = "Un Festival publicado o ya decidido no puede eliminarse desde la organización. Solicita su retiro al equipo institucional.", estado = EstadosFestival.HaciaContrato(festival.StatusCode) });
            }

            var ahora = DateTime.UtcNow;
            var estadoAnterior = festival.StatusCode;
            // Retirar un envío no equivale a borrar el trabajo: el Festival vuelve al borrador
            // privado y puede corregirse o enviarse nuevamente. Solo los borradores eliminados
            // pasan a Archivado y desaparecen del centro de gestión.
            var retiradoDeRevision = EstadosFestival.Es(estadoAnterior, EstadosFestival.EnRevision);
            festival.StatusCode = retiradoDeRevision ? EstadosFestival.Borrador : EstadosFestival.Archivado;
            festival.UpdatedAt = ahora;
            RegistrarAuditoria(dbContext, personaId.Value, festival.Id, festival.OrganizacionPrincipalId.Value,
                retiradoDeRevision ? "FestivalRetiradoDeRevision" : "FestivalArchivado",
                estadoAnterior, festival.StatusCode, ahora);
            await RevisionDeCamposFestivalEndpoints.CerrarSolicitudViva(dbContext, festival.Id, ahora, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        });

        externo.MapPost("/festivales/{festivalId:int}/solicitudes-retiro", async (
            int festivalId, SolicitarRetiroFestivalSolicitud solicitud, ClaimsPrincipal principal,
            PnmcDbContext dbContext, IAntiforgery antiforgery, HttpContext httpContext, CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La solicitud no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is null || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, festival.OrganizacionPrincipalId.Value, cancellationToken)) return Results.Forbid();
            if (!EstadosFestival.Es(festival.StatusCode, EstadosFestival.Publicado))
                return Results.Conflict(new { message = "Solo un Festival publicado requiere una solicitud institucional de retiro." });
            var motivo = ValidationHelpers.SanitizeText(solicitud.Justificacion, 1200);
            if (string.IsNullOrWhiteSpace(motivo)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["justificacion"] = ["Explica por qué solicitas retirar este Festival."] });
            var viva = await dbContext.RecordLinkRequests.AnyAsync(item => item.ModuloId == "festivales_retiro" && item.RecordId == festivalId.ToString(CultureInfo.InvariantCulture) && (item.Status == "pendiente" || item.Status == "en_revision" || item.Status == "ajustes_solicitados"), cancellationToken);
            if (viva) return Results.Conflict(new { message = "Ya existe una solicitud de retiro en trámite para este Festival." });
            var ahora = DateTime.UtcNow;
            var fila = new RecordLinkRequestRow { ModuloId = "festivales_retiro", RecordId = festivalId.ToString(CultureInfo.InvariantCulture), RequestingUserId = personaId.Value, EntidadId = festival.OrganizacionPrincipalId, RequestedScope = "responsable", Reason = motivo, Status = "pendiente", CreatedAt = ahora, UpdatedAt = ahora };
            dbContext.RecordLinkRequests.Add(fila);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/admin/solicitudes-de-vinculacion/{fila.Id}", new { id = fila.Id, estado = fila.Status });
        });

        return group;
    }

    /// <summary>Los doce catalogos que llenan los desplegables de la ficha del Festival.</summary>
    /// <remarks>
    /// UNA SOLA RESPUESTA Y NO DOCE RUTAS: el formulario los necesita TODOS a la vez para poder
    /// pintarse, y doce peticiones para llenar un formulario es doce veces la latencia y doce
    /// sitios donde fallar a medias.
    /// </remarks>
    internal static async Task<object> CatalogosDelFestivalAsync(
        PnmcDbContext dbContext, CancellationToken cancellationToken)
    {

        var practicas = await dbContext.PracticasMusicales.AsNoTracking()
            .OrderBy(item => item.Nombre)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var territorios = await dbContext.TerritoriosSonoros.AsNoTracking()
            .OrderBy(item => item.Nombre)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);

        // -------------------------------------------------------------------------------
        // LOS ONCE CATALOGOS QUE VINIERON DE SIMUS. Anadidos.
        // -------------------------------------------------------------------------------
        // Existian en la base desde el 24 de agosto y ninguno estaba mapeado en EF, asi que
        // ninguna ruta podia devolverlos: el formulario de Festival no tenia de donde sacar
        // sus desplegables. Sus 54 filas se sembraron el 28 desde el volcado de SIMUS.
        //
        // SE ANADEN A LA MISMA RESPUESTA y no en once rutas nuevas: el formulario los necesita
        // TODOS a la vez para poder pintarse, y once peticiones para llenar un formulario es
        // once veces la latencia y once sitios donde fallar a medias. Las dos claves que ya
        // viajaban -`practicasMusicales` y `territoriosSonoros`- no se tocan, asi que quien ya
        // consume esta ruta no se entera del cambio.
        //
        // ORDENADOS POR `OrdenVisualizacion` Y NO POR NOMBRE: «Otra» y «Ninguna» son las
        // opciones de escape de cada catalogo y su sitio es el final de la lista; por alfabeto
        // caerian en mitad. Las dos listas de arriba siguen ordenando por nombre porque asi
        // estaban y no es esta la tarea que las cambia.
        var tipologias = await dbContext.TipologiasFestival.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var expresiones = await dbContext.ExpresionesArtisticas.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var fuentes = await dbContext.FuentesFinanciacion.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var modalidades = await dbContext.ModalidadesParticipacion.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var naturalezas = await dbContext.NaturalezasEntidad.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var tiposIngreso = await dbContext.TiposIngreso.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var tiposOrganizador = await dbContext.TiposOrganizador.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var zonas = await dbContext.ZonasUrbanoRural.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var titulaciones = await dbContext.TitulacionesColectivas.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var regiones = await dbContext.RegionesOcad.AsNoTracking()
            .OrderBy(item => item.Orden).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);

        // LA CORRESPONDENCIA DEPARTAMENTO -> REGION OCAD VIAJA CON LOS CATALOGOS, y son 32 filas.
        //
        // POR QUE VIAJA. La historia de usuario pide que la Region OCAD se rellene sola al elegir el
        // departamento —«campo automatico, lo definen los campos seleccionados en Departamento y
        // Municipio»—, y eso solo puede verse en el acto si el formulario tiene la correspondencia
        // delante. El servidor la deriva igual al leer la edicion; esto es para que quien diligencia
        // lo vea mientras escribe, no despues de guardar.
        //
        // LA TABLA EXISTIA SEMBRADA DESDE EL 28 DE AGOSTO DE 2026 Y NO LA CONSULTABA NADIE: la
        // region no aparecia en ninguna pantalla ni en ninguna respuesta.
        var regionPorDepartamento = await dbContext.DepartamentosRegionOcad.AsNoTracking()
            .OrderBy(item => item.CodigoDepartamento)
            .Select(item => new { codigoDepartamento = item.CodigoDepartamento, regionOcadId = item.RegionOcadId })
            .ToListAsync(cancellationToken);

        return new
        {
            practicasMusicales = practicas,
            territoriosSonoros = territorios,
            tipologias,
            expresionesArtisticas = expresiones,
            fuentesFinanciacion = fuentes,
            modalidadesParticipacion = modalidades,
            naturalezasEntidad = naturalezas,
            tiposIngreso,
            tiposOrganizador,
            zonasUrbanoRural = zonas,
            titulacionesColectivas = titulaciones,
            regionesOcad = regiones,
            regionOcadPorDepartamento = regionPorDepartamento,
        };
    }


    private static async Task<FestivalBorradorDto> ADtoAsync(FestivalRow festival, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var organizacionNombre = await dbContext.EntityProfiles.AsNoTracking()
            .Where(item => item.Id == festival.OrganizacionPrincipalId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var practicas = await dbContext.De<PracticaMusicalDeRegistroRow>(Modulos.Festivales, festival.Id).AsNoTracking()
            .Join(dbContext.PracticasMusicales.AsNoTracking(), relacion => relacion.ValorId, practica => practica.Id,
                (_, practica) => new { practica.Id, practica.Nombre })
            .OrderBy(item => item.Nombre)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var territorios = await dbContext.De<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, festival.Id).AsNoTracking()
            .Join(dbContext.TerritoriosSonoros.AsNoTracking(), relacion => relacion.ValorId, territorio => territorio.Id,
                (_, territorio) => new { territorio.Id, territorio.Nombre })
            .OrderBy(item => item.Nombre)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre))
            .ToListAsync(cancellationToken);
        var observacionRevision = await dbContext.HistorialesRevisionRegistros.AsNoTracking()
            .Where(item => item.ModuloId == Modulos.Festivales && item.RegistroId == festival.Id.ToString())
            .OrderByDescending(item => item.Fecha)
            .Select(item => item.Comentario ?? item.MotivoRechazo)
            .FirstOrDefaultAsync(cancellationToken);
        // EL EXPEDIENTE, EN LAS TABLAS GENERICAS desde. «La viva
        // primero» era la columna `Activa`; ahora se deduce del estado, que es lo mismo sin una
        // segunda verdad que mantener a mano.
        var registroDelFestival = festival.Id.ToString(CultureInfo.InvariantCulture);
        var propuesta = await dbContext.PropuestasDeCambio.AsNoTracking()
            .Where(item => item.ModuloId == Modulos.Festivales && item.RegistroId == registroDelFestival)
            .OrderByDescending(item => item.Estado != EstadosDePropuesta.Aplicada && item.Estado != EstadosDePropuesta.Rechazada)
            .ThenByDescending(item => item.FechaActualizacion)
            .FirstOrDefaultAsync(cancellationToken);
        var observacionPropuesta = propuesta is null ? null : await dbContext.HistorialesRevisionRegistros.AsNoTracking()
            .Where(item => item.ModuloId == Modulos.PropuestasDeCambioDeFestival && item.RegistroId == propuesta.Id.ToString())
            .OrderByDescending(item => item.Fecha).Select(item => item.Comentario ?? item.MotivoRechazo).FirstOrDefaultAsync(cancellationToken);

        // El contrato del canal externo sigue viajando en PascalCase: el front-end pinta
        // el aviso de «en revision» comparando contra 'EnRevision', y cambiarlo tambien
        // aqui apagaria ese aviso en silencio. La traduccion vive en EstadosFestival.
        return new FestivalBorradorDto(
            festival.Id.ToString(CultureInfo.InvariantCulture), festival.Name, festival.Description,
            EstadosFestival.HaciaContrato(festival.StatusCode ?? EstadosFestival.Borrador) ?? "Borrador",
            festival.OrganizacionPrincipalId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, organizacionNombre,
            festival.CoverageLevel, string.IsNullOrWhiteSpace(festival.DepartmentCode) ? null : festival.DepartmentCode,
            festival.MunicipalityCode, festival.Periodicidad, festival.ContactEmail, practicas, territorios, observacionRevision,
            propuesta?.Estado, observacionPropuesta,
            // El bloque de contacto de la cabecera. Va nombrado y no por posicion: son seis
            // parametros opcionales seguidos, todos `string?`, y equivocarse de orden compila.
            Instagram: festival.InstagramUrl,
            Facebook: festival.FacebookUrl,
            PaginaWeb: festival.WebsiteUrl,
            OtroEnlace: festival.OtherUrl,
            TelefonoCelular: festival.ContactPhone,
            ObservacionesContacto: festival.ObservacionesContacto,
            PeriodicidadDetalle: festival.PeriodicidadDetalle);
    }

    /// <summary>
    /// Estados desde los que la agrupacion responsable todavia puede tocar su Festival.
    /// </summary>
    /// <remarks>
    /// Compara con <see cref="EstadosFestival.Es"/> y no con literales porque en la base
    /// pueden convivir las dos grafias: los 30 Festivales historicos llegaron con
    /// <c>publicado</c> en minuscula y los borradores creados antes de esta correccion
    /// quedaron con <c>Borrador</c>. Comparar literalmente dejaria a esos borradores
    /// antiguos bloqueados para siempre, sin mensaje que lo explicara.
    /// </remarks>
    private static bool EsEditable(string? estado) =>
        EstadosFestival.Es(estado, EstadosFestival.Borrador)
        || EstadosFestival.Es(estado, EstadosFestival.AjustesSolicitados);

    // LA REGLA VIVE EN `AdministracionDeOrganizacion`, no aquí. Estaba escrita ocho veces y cinco
    // copias se habían quedado atrás sin comprobar si la organización sigue activa.
    private static Task<bool> PuedeAdministrarOrganizacionAsync(PnmcDbContext dbContext, int personaId, int organizacionId, CancellationToken cancellationToken) =>
        AdministracionDeOrganizacion.PuedeAdministrarAsync(dbContext, personaId, organizacionId, cancellationToken);

    private static int? ObtenerPersonaId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var personaId)
            ? personaId : null;

    private static string NormalizarNombreCoincidencia(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

        var resultado = new StringBuilder();
        foreach (var caracter in valor.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark) continue;
            resultado.Append(char.IsLetterOrDigit(caracter) ? char.ToLowerInvariant(caracter) : ' ');
        }

        return string.Join(' ', resultado.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static async Task<Dictionary<string, string[]>> ValidarFestivalParaRevisionAsync(
        FestivalRow festival,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (ValidationHelpers.IsMissing(festival.Name)) errores["nombre"] = ["El nombre del Festival es obligatorio."];
        if (festival.OrganizacionPrincipalId is null
            || !await dbContext.EntityProfiles.AsNoTracking().AnyAsync(item => item.Id == festival.OrganizacionPrincipalId && item.IsActive, cancellationToken))
            errores["organizacionPrincipal"] = ["La organización principal del Festival debe estar activa."];

        var nivel = NormalizarNivel(festival.CoverageLevel);
        if (nivel is not ("municipal" or "departamental" or "nacional"))
        {
            errores["nivelCobertura"] = ["El nivel territorial no es válido."];
            return errores;
        }

        if (nivel != "nacional")
        {
            var departamentoValido = !string.IsNullOrWhiteSpace(festival.DepartmentCode) && await dbContext.DivipolaLocations.AsNoTracking()
                .AnyAsync(item => item.DepartmentCode == festival.DepartmentCode, cancellationToken);
            if (!departamentoValido)
            {
                errores["codigoDepartamento"] = ["El departamento principal debe existir en DIVIPOLA."];
                return errores;
            }

            if (nivel == "municipal")
            {
                var municipioValido = !string.IsNullOrWhiteSpace(festival.MunicipalityCode) && await dbContext.DivipolaLocations.AsNoTracking()
                    .AnyAsync(item => item.DepartmentCode == festival.DepartmentCode && item.MunicipalityCode == festival.MunicipalityCode, cancellationToken);
                if (!municipioValido) errores["codigoMunicipio"] = ["El municipio principal debe existir en DIVIPOLA."];
            }
        }

        return errores;
    }

    /// <summary>
    /// Los avisos del envío a revisión: en la campana y, desde ahora, también por correo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EL CORREO LO PIDE LA HU 1 REQ 05, CON LAS DOS PLANTILLAS ESCRITAS</b> —una para quien
    /// envía y otra para quien va a validar— y hasta no se encolaba
    /// ninguna: medido, `EncolarAsync` se llamaba en un único sitio de todo el sistema, la
    /// confirmación de correo. El circuito de Festivales avisaba solo dentro de la aplicación.
    /// </para>
    /// <para>
    /// <b>SE ENCOLA AUNQUE NO HAYA PROVEEDOR, Y ESE ES EL PUNTO.</b> `IEnviadorDeCorreo` deja el
    /// mensaje en la cola con estado «pendiente» y sin fecha de envío —no finge que salió—, así que
    /// el día que se conecte el transporte estas filas son exactamente lo que hay que drenar. Y
    /// mientras tanto se puede comprobar que el texto y el destinatario son los correctos, que es
    /// justo lo que no se podría si los mensajes no se produjeran.
    /// </para>
    /// <para>
    /// EL AVISO EN LA CAMPANA NO SE SUSTITUYE: son dos canales para dos momentos. Quien tiene la
    /// aplicación abierta lo ve ahí; el correo alcanza a quien no.
    /// </para>
    /// </remarks>
    private static async Task CrearNotificacionEnvioRevisionAsync(
        PnmcDbContext dbContext,
        IEnviadorDeCorreo correo,
        int personaId,
        FestivalRow festival,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var persona = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == personaId && item.IsActive, cancellationToken);
        if (persona is null) return;

        var nombreOrganizacion = festival.OrganizacionPrincipalId is int organizacionId
            ? await dbContext.EntityProfiles.AsNoTracking()
                .Where(item => item.Id == organizacionId)
                .Select(item => item.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var remitente = string.IsNullOrWhiteSpace(nombreOrganizacion)
            ? "La organización administradora"
            : $"La organización “{nombreOrganizacion.Trim()}”";

        // La confirmación pertenece a quien envió el Festival. El aviso de trabajo, en cambio,
        // corresponde a las cuentas institucionales que pueden abrir la cola de revisión.
        dbContext.Notifications.Add(new NotificationRow
        {
            RecipientUserId = personaId,
            RecipientEmail = persona.Email,
            EventType = "FestivalEnviadoARevision",
            Channel = "internal",
            Title = "Festival enviado a revisión",
            Body = $"Tu Festival “{festival.Name}” fue enviado a revisión. Recibirás una notificación cuando se publique o si se solicitan ajustes.",
            Status = "enviada",
            ModuloId = Modulos.Festivales,
            RecordId = festival.Id.ToString(CultureInfo.InvariantCulture),
            MetadataJson = $"{{\"OrganizacionPrincipalId\":{festival.OrganizacionPrincipalId},\"OrganizacionNombre\":{System.Text.Json.JsonSerializer.Serialize(nombreOrganizacion)},\"FestivalNombre\":{System.Text.Json.JsonSerializer.Serialize(festival.Name)},\"Estado\":\"EnRevision\"}}",
            CreatedAt = ahora,
            SentAt = ahora,
            Attempts = 0
        });

        // A QUIEN ENVIO. Es la plantilla de la HU 1 Req 05, con su texto.
        await correo.EncolarAsync(new CorreoSaliente(
            persona.Email,
            $"SIMUS · Nueva solicitud · {festival.Name}",
            $"Estimado(a) {persona.FullName}.\n\n"
            + $"Le informamos que su solicitud de registro del Festival “{festival.Name}” ha sido "
            + "enviada correctamente.\n\n"
            + "Una vez sea gestionada, recibirá una notificación para consultar la respuesta emitida "
            + "por el Ministerio de las Culturas, las Artes y los Saberes.",
            "FestivalEnviadoARevision",
            personaId), cancellationToken);

        var destinatariosInstitucionales = await dbContext.Users.AsNoTracking()
            .Where(item => item.IsActive)
            .Join(
                dbContext.UsuariosRoles.AsNoTracking(),
                usuario => usuario.Id,
                asignacion => asignacion.UserId,
                (usuario, asignacion) => new { Usuario = usuario, asignacion.RoleId })
            .Join(
                dbContext.Roles.AsNoTracking(),
                fila => fila.RoleId,
                rol => rol.Id,
                (fila, rol) => new { fila.Usuario, rol.Name })
            .Where(fila => Permisos.RolesInternos.Contains(fila.Name))
            .Select(fila => new { fila.Usuario.Id, fila.Usuario.Email })
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var destinatario in destinatariosInstitucionales)
        {
            dbContext.Notifications.Add(new NotificationRow
            {
                RecipientUserId = destinatario.Id,
                RecipientEmail = destinatario.Email,
                EventType = "FestivalRecibidoParaRevision",
                AccessScope = SimusAuthentication.InstitutionalScope,
                Channel = "internal",
                Title = "Festival pendiente de revisión",
                Body = $"{remitente} envió a revisión para publicación el Festival “{festival.Name}”.",
                Status = "enviada",
                ModuloId = Modulos.Festivales,
                RecordId = festival.Id.ToString(CultureInfo.InvariantCulture),
                MetadataJson = $"{{\"OrganizacionPrincipalId\":{festival.OrganizacionPrincipalId},\"OrganizacionNombre\":{System.Text.Json.JsonSerializer.Serialize(nombreOrganizacion)},\"FestivalNombre\":{System.Text.Json.JsonSerializer.Serialize(festival.Name)},\"Estado\":\"EnRevision\"}}",
                CreatedAt = ahora,
                SentAt = ahora,
                Attempts = 0
            });

            // A QUIEN TIENE QUE VALIDARLO. La otra plantilla de la HU 1 Req 05, con el enlace a la
            // solicitud. EL AMBITO ES INSTITUCIONAL y no el de por omisión: este mensaje lo lee el
            // equipo del PNMC, y marcarlo como externo lo mandaría al buzón equivocado.
            await correo.EncolarAsync(new CorreoSaliente(
                destinatario.Email,
                $"SIMUS · Nueva solicitud · {festival.Name}",
                $"{remitente} envió a revisión el Festival “{festival.Name}”.\n\n"
                + "Le informamos que se le ha asignado una solicitud de preregistro para su "
                + "respectiva validación:\n\n"
                + $"{RutaDeLaRevision(festival.Id)}",
                "FestivalRecibidoParaRevision",
                destinatario.Id,
                SimusAuthentication.InstitutionalScope), cancellationToken);
        }
    }

    /// <summary>
    /// Dónde se abre la revisión de un Festival, para el correo del funcionario.
    /// </summary>
    /// <remarks>
    /// ES UNA RUTA RELATIVA A PROPOSITO. El dominio depende del despliegue y no lo sabe el API; el
    /// proveedor de correo, cuando se conecte, es quien antepone la base. Escribir aquí un
    /// «localhost» o un dominio fijo mandaría correos con un enlace que no abre.
    /// </remarks>
    private static string RutaDeLaRevision(int festivalId) =>
        $"/administracion/festivales?revisar={festivalId.ToString(CultureInfo.InvariantCulture)}";

    private static async Task RegistrarHistorialEnvioRevisionAsync(
        PnmcDbContext dbContext,
        int personaId,
        FestivalRow festival,
        string? estadoAnterior,
        DateTime fecha,
        CancellationToken cancellationToken)
    {
        var fila = new HistorialRevisionRegistroRow
        {
            ModuloId = Modulos.Festivales,
            RegistroId = festival.Id.ToString(CultureInfo.InvariantCulture),
            // CK_RegistrosRevisionHistorial_EstadoNuevo admite los mismos siete codigos en
            // minuscula. "EnRevision" tambien lo violaba: no bastaba con arreglar la FK
            // del Festival, esta fila caia por su cuenta.
            EstadoAnterior = EstadosFestival.DesdeContrato(estadoAnterior) ?? estadoAnterior,
            EstadoNuevo = EstadosFestival.EnRevision,
            // Esta columna NO tiene CHECK, y es el sitio del evento funcional: §39 separa
            // «que decision se tomo» (historial) de «quien hizo tecnicamente que» (bitacora).
            Accion = "FestivalEnviadoARevision",
            UsuarioId = personaId,
            Fecha = fecha
        };

        // La instantanea ANTES de guardar: quien era la organizacion y quien respondia por ella
        // hoy. Resolverlo al leer es lo que hacia que renombrar una organizacion reescribiera su
        // historial pasado.
        await InstantaneaDelHistorial.TomarAsync(
            dbContext, fila, festival.OrganizacionPrincipalId, personaId, cancellationToken);

        dbContext.HistorialesRevisionRegistros.Add(fila);
    }

    private static void RegistrarAuditoria(
        PnmcDbContext dbContext,
        int personaId,
        int festivalId,
        int organizacionId,
        string accion,
        string? estadoAnterior = null,
        string? estadoNuevo = null,
        DateTime? fecha = null)
    {
        dbContext.AuditLogs.Add(new AuditLogRow
        {
            UserId = personaId,
            TableName = "Festivales",
            RecordId = festivalId.ToString(CultureInfo.InvariantCulture),
            // CK_BitacoraAuditoria_Accion solo admite trece verbos tecnicos, y ninguno de
            // los nombres de evento del circuito ("FestivalCreado", "FestivalEnviadoARevision"...)
            // estaba entre ellos: TODA escritura externa de Festival fallaba contra SQL
            // Server, no solo el envio a revision. El evento no se pierde, viaja abajo en
            // ValoresNuevos. Ver AccionesAuditoria.
            Action = AccionesAuditoria.DeEvento(accion),
            PreviousValuesJson = estadoAnterior is null ? null : $"{{\"Estado\":\"{EstadosFestival.DesdeContrato(estadoAnterior) ?? estadoAnterior}\"}}",
            NewValuesJson = $"{{\"OrganizacionPrincipalId\":{organizacionId},\"Estado\":\"{EstadosFestival.DesdeContrato(estadoNuevo) ?? EstadosFestival.Borrador}\",\"Evento\":\"{accion}\"}}",
            CreatedAt = fecha ?? DateTime.UtcNow
        });
    }

    // LOS NORMALIZADORES DE TERRITORIO Y TEXTO VIVEN EN `AltaDeFestival`, que es donde nace un
    // Festival. Aqui se usan; repetirlos daria dos definiciones de «que territorio corresponde a
    // este nivel», y la segunda envejeceria sola.
    private static string NormalizarNivel(string? valor) => AltaDeFestival.NormalizarNivel(valor);
    /// <summary>
    /// Departamento que corresponde al nivel de cobertura declarado, o <c>null</c> si el nivel
    /// es nacional.
    /// </summary>
    /// <remarks>
    /// <para>
    /// POR QUE EXISTE. <c>CK_Festivales_NivelCobertura</c> no solo comprueba que el nivel sea uno
    /// de los tres: comprueba la COHERENCIA entre el nivel y el territorio —nacional exige
    /// departamento y municipio nulos; departamental, municipio nulo—. La validacion previa
    /// salta toda comprobacion territorial cuando el nivel es nacional, asi que un codigo que
    /// venga en el cuerpo pasa entera y muere en el guardado con un 500 sin explicacion. Y el
    /// formulario lo produce por su camino natural: oculta el selector al cambiar a Nacional,
    /// pero no limpia el modelo.
    /// </para>
    /// <para>
    /// EL DEFECTO TENIA DOS MITADES Y ESTA FUNCION SOLO ES UNA. La otra era el
    /// <c>?? string.Empty</c> en el sitio de escritura, que convertia en cadena vacia el NULL que
    /// esta funcion devuelve — y cadena vacia NO es NULL para la restriccion. Durante un rato la
    /// primera mitad estuvo puesta y la segunda no, de modo que el arreglo parecia hecho y el
    /// defecto seguia intacto: la normalizacion quedaba deshecha en la misma linea que la
    /// aplicaba. La causa de fondo era que <c>FestivalRow.DepartmentCode</c> era <c>string</c> no
    /// anulable; hasta que no paso a <c>string?</c> no habia forma de escribir NULL.
    /// </para>
    /// <para>
    /// <b>Si vuelve a aparecer un <c>?? string.Empty</c> sobre el retorno de esta funcion, el
    /// defecto ha vuelto.</b> Lo vigila <c>CoberturaNacionalSqlServerTests</c>, en el carril de
    /// SQL Server porque la restriccion solo existe en el motor real: el arnes de SQLite
    /// construye su esquema desde el modelo de EF, que no declara ningun CHECK, y alli este
    /// INSERT entra sin queja.
    /// </para>
    /// </remarks>
    private static string? DepartamentoSegunNivel(CrearFestivalBorradorSolicitud solicitud) =>
        AltaDeFestival.DepartamentoSegunNivel(solicitud);

    /// <summary>Municipio que corresponde al nivel: solo el nivel municipal lo lleva.</summary>
    private static string? MunicipioSegunNivel(CrearFestivalBorradorSolicitud solicitud) =>
        AltaDeFestival.MunicipioSegunNivel(solicitud);

    private static string? LimpiarTexto(string? valor) => AltaDeFestival.LimpiarTexto(valor);
}
