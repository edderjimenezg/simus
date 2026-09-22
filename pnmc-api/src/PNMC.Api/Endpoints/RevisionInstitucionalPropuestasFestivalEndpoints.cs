using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

using PNMC.Domain.Entities;

namespace PNMC.Api.Endpoints;

public static class RevisionInstitucionalPropuestasFestivalEndpoints
{
    private const string Modulo = Modulos.PropuestasDeCambioDeFestival;

    /// <summary>
    /// El módulo del REGISTRO sobre el que se propone, que no es el del expediente.
    /// </summary>
    /// <remarks>
    /// <see cref="Modulo"/> es el del historial institucional —«propuestas-cambio-festival»— y
    /// nombra al expediente; este nombra al Festival. Son dos cosas distintas y por eso son dos
    /// constantes: confundirlas haría que la cola buscara expedientes donde están los registros.
    /// </remarks>
    private const string ModuloDelRegistro = Modulos.Festivales;

    /// <summary>El Festival sobre el que se propone, leído del expediente genérico.</summary>
    private static int FestivalDe(PropuestaDeCambioRow propuesta) =>
        int.TryParse(propuesta.RegistroId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;

    public static RouteGroupBuilder MapRevisionInstitucionalPropuestasFestivalEndpoints(this RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional/propuestas-cambio-festival").WithTags("revision-institucional-propuestas-festival");
        // Mismo cierre que su gemelo de Festivales, y por el mismo motivo: 171 lineas sin una
        // sola comprobacion de rol. Decidir una propuesta genera la version publica N+1.
        institucional.RequireAuthorization(Permisos.PoliticaFuncionario);

        institucional.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);
            return Results.Ok(new TokenAntiforgeryRespuesta(tokens.RequestToken ?? string.Empty));
        });

        institucional.MapGet("/en-revision", async (PnmcDbContext db, CancellationToken ct) =>
        {
            var propuestas = await db.PropuestasDeCambio.AsNoTracking()
                .Where(item => item.ModuloId == ModuloDelRegistro && item.Estado == EstadosDePropuesta.EnRevision)
                .OrderBy(item => item.FechaEnvio ?? item.FechaActualizacion).ToListAsync(ct);
            var registros = propuestas.Select(item => FestivalDe(item)).Where(id => id > 0).Distinct().ToList();
            var festivales = await db.FestivalRecords.AsNoTracking().Where(item => registros.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, ct);
            var organizaciones = await db.EntityProfiles.AsNoTracking().Where(item => propuestas.Select(p => p.IdOrganizacion).Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, ct);
            return Results.Ok(propuestas.Select(item => new PropuestaCambioFestivalRevisionDto(
                item.Id.ToString(CultureInfo.InvariantCulture), item.RegistroId,
                festivales.GetValueOrDefault(FestivalDe(item), "Festival no disponible"),
                organizaciones.GetValueOrDefault(item.IdOrganizacion, "Organización no disponible"),
                item.Estado, item.FechaEnvio)));
        });

        institucional.MapGet("/{propuestaId:long}", async (long propuestaId, PnmcDbContext db, CancellationToken ct) =>
        {
            var propuesta = await db.PropuestasDeCambio.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == propuestaId && item.ModuloId == ModuloDelRegistro, ct);
            if (propuesta is null) return Results.NotFound();
            var proyectada = await PropuestaDeFestivalProyectada.DeAsync(db, propuesta, ct);
            if (proyectada is null) return Results.Conflict(new { message = "La versión de origen de la propuesta no está disponible." });
            var version = proyectada.VersionOrigen;
            var historial = await db.HistorialesRevisionRegistros.AsNoTracking().Where(item => item.ModuloId == Modulo && item.RegistroId == propuesta.Id.ToString())
                .OrderByDescending(item => item.Fecha).Select(item => new { item.EstadoAnterior, item.EstadoNuevo, item.Accion, item.Comentario, item.MotivoRechazo, item.Fecha }).ToListAsync(ct);
            return Results.Ok(new
            {
                propuesta = await CrearDetallePropuestaAsync(proyectada, db, ct),
                versionVigente = await CrearDetalleVersionAsync(version, db, ct),
                historial
            });
        });

        institucional.MapGet("/festivales/{festivalId:int}/versiones", async (int festivalId, PnmcDbContext db, CancellationToken ct) =>
        {
            var versiones = await db.VersionesFestival.AsNoTracking().Where(item => item.FestivalOrigenId == festivalId).OrderByDescending(item => item.NumeroVersion).ToListAsync(ct);
            return Results.Ok(versiones.Select(item => new VersionFestivalInstitucionalDto(item.Id.ToString(CultureInfo.InvariantCulture), item.NumeroVersion, item.EsVigente, item.Nombre, item.FechaPublicacion)));
        });

        // Punto de entrada institucional para registros históricos: no escribe el Festival ni
        // crea una segunda forma de editarlo. Abre la misma propuesta versionada que luego se
        // revisa y publica, pero solo cuando el registro no pertenece a una organización externa.
        institucional.MapPost("/festivales/{festivalId:int}/borrador-institucional", async (
            int festivalId, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext httpContext, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext)) return Results.BadRequest(new { message = "El borrador institucional no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await db.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, ct);
            if (festival is null) return Results.NotFound();
            var entidad = festival.OrganizacionPrincipalId is int id
                ? await db.EntityProfiles.FirstOrDefaultAsync(item => item.Id == id, ct)
                : await db.EntityProfiles.FirstOrDefaultAsync(item => item.IsInstitutional && item.IsActive, ct);
            if (entidad is null) return Results.Conflict(new { message = "No hay una organización institucional configurada para gestionar este registro histórico." });
            if (!entidad.IsInstitutional) return Results.Conflict(new { message = "Este Festival está administrado por una organización externa. Sus cambios deben seguir el flujo de propuesta de esa organización." });
            var existente = await PropuestasDeCambioFestivalExternosEndpoints.PropuestaVivaAsync(db, festivalId, seguimiento: true, ct);
            if (existente is not null) return Results.Ok(new { id = existente.Id, estado = existente.Estado, existente = true });

            var ahora = DateTime.UtcNow;
            var version = await db.VersionesFestival.FirstOrDefaultAsync(item => item.FestivalOrigenId == festivalId && item.EsVigente, ct);
            if (version is null)
            {
                var numero = (await db.VersionesFestival.Where(item => item.FestivalOrigenId == festivalId).Select(item => (int?)item.NumeroVersion).MaxAsync(ct) ?? 0) + 1;
                version = new VersionFestivalRow { EstadoRegistro = EstadosFestival.Publicado, FestivalOrigenId = festivalId, NumeroVersion = numero, EsVigente = true, Nombre = festival.Name, Descripcion = festival.Description, NivelCobertura = festival.CoverageLevel, CodigoDepartamento = festival.DepartmentCode, CodigoMunicipio = festival.MunicipalityCode, Periodicidad = festival.Periodicidad, PeriodicidadDetalle = festival.PeriodicidadDetalle, CorreoContacto = festival.ContactEmail, TelefonoContacto = festival.ContactPhone, Instagram = festival.InstagramUrl, Facebook = festival.FacebookUrl, SitioWeb = festival.WebsiteUrl, OtroEnlace = festival.OtherUrl, FechaPublicacion = festival.UpdatedAt ?? festival.CreatedAt, FechaCreacion = ahora };
                db.VersionesFestival.Add(version);
                await db.SaveChangesAsync(ct);
            }
            var propuesta = new PropuestaDeCambioRow
            {
                ModuloId = ModuloDelRegistro,
                RegistroId = festivalId.ToString(CultureInfo.InvariantCulture),
                SubregistroId = version.Id.ToString(CultureInfo.InvariantCulture),
                Estado = EstadosDePropuesta.Borrador,
                IdOrganizacion = entidad.Id,
                IdUsuarioProponente = personaId.Value,
                FechaCreacion = ahora,
                FechaActualizacion = ahora,
            };
            db.PropuestasDeCambio.Add(propuesta);
            db.AuditLogs.Add(new AuditLogRow { UserId = personaId.Value, TableName = "Festivales", RecordId = festivalId.ToString(CultureInfo.InvariantCulture), Action = AccionesAuditoria.Crear, NewValuesJson = "{\"Evento\":\"BorradorInstitucionalFestival\"}", CreatedAt = ahora });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/institucional/propuestas-cambio-festival/{propuesta.Id}", new { id = propuesta.Id, estado = propuesta.Estado, existente = false });
        });

        institucional.MapPut("/{propuestaId:int}/borrador-institucional", async (
            long propuestaId, CrearFestivalBorradorSolicitud solicitud, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext httpContext, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext)) return Results.BadRequest(new { message = "El borrador institucional no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var propuesta = await db.PropuestasDeCambio.FirstOrDefaultAsync(
                item => item.Id == propuestaId && item.ModuloId == ModuloDelRegistro
                    && item.Estado != EstadosDePropuesta.Aplicada && item.Estado != EstadosDePropuesta.Rechazada, ct);
            if (propuesta is null) return Results.NotFound();
            var entidad = await db.EntityProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == propuesta.IdOrganizacion, ct);
            if (entidad?.IsInstitutional != true) return Results.Forbid();
            if (propuesta.Estado is not (EstadosDePropuesta.Borrador or EstadosDePropuesta.AjustesSolicitados)) return Results.Conflict(new { message = "Este borrador ya fue enviado a revisión y no puede modificarse directamente.", estado = propuesta.Estado });
            if (ValidationHelpers.IsMissing(solicitud.Nombre)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["nombre"] = ["El nombre del Festival es obligatorio."] });
            if (!string.IsNullOrWhiteSpace(solicitud.CorreoContacto) && !ValidationHelpers.IsValidEmail(solicitud.CorreoContacto)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["correoContacto"] = ["Ingresa un correo electrónico válido."] });
            var proyectada = await PropuestaDeFestivalProyectada.DeAsync(db, propuesta, ct);
            if (proyectada is null) return Results.Conflict(new { message = "La versión de origen de la propuesta no está disponible." });

            var practicas = solicitud.PracticasMusicalesIds.Distinct().ToList();
            var territorios = solicitud.TerritoriosSonorosIds.Distinct().ToList();
            if (practicas.Count > 0 && await db.PracticasMusicales.AsNoTracking().CountAsync(item => practicas.Contains(item.Id), ct) != practicas.Count) return Results.ValidationProblem(new Dictionary<string, string[]> { ["practicasMusicalesIds"] = ["Selecciona prácticas musicales válidas."] });
            if (territorios.Count > 0 && await db.TerritoriosSonoros.AsNoTracking().CountAsync(item => territorios.Contains(item.Id), ct) != territorios.Count) return Results.ValidationProblem(new Dictionary<string, string[]> { ["territoriosSonorosIds"] = ["Selecciona territorios sonoros válidos."] });

            var perfil = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["nombre"] = ValidationHelpers.SanitizeText(solicitud.Nombre, 240),
                ["descripcion"] = ValidationHelpers.SanitizeText(solicitud.Descripcion, 12000),
                ["periodicidad"] = ValidationHelpers.SanitizeText(solicitud.Periodicidad, 120),
                ["correoContacto"] = ValidationHelpers.SanitizeText(solicitud.CorreoContacto, 320),
                ["codigoDepartamento"] = ValidationHelpers.SanitizeText(solicitud.CodigoDepartamento, 12),
                ["codigoMunicipio"] = ValidationHelpers.SanitizeText(solicitud.CodigoMunicipio, 12),
                ["telefonoContacto"] = ValidationHelpers.SanitizeText(solicitud.TelefonoCelular, 80),
                ["instagram"] = ValidationHelpers.SanitizeText(solicitud.Instagram, 500),
                ["facebook"] = ValidationHelpers.SanitizeText(solicitud.Facebook, 500),
                ["sitioWeb"] = ValidationHelpers.SanitizeText(solicitud.PaginaWeb, 500),
                ["otroEnlace"] = ValidationHelpers.SanitizeText(solicitud.OtroEnlace, 500),
                ["observacionesContacto"] = ValidationHelpers.SanitizeText(solicitud.ObservacionesContacto, 1000),
            };
            // EL NIVEL SOLO SI ES UNO DE LOS TRES: enviar otra cosa no cambia el que ya hay, que es
            // lo que hacía la versión anterior de esta ruta.
            if (solicitud.NivelCobertura is "municipal" or "departamental" or "nacional")
            {
                perfil["nivelCobertura"] = solicitud.NivelCobertura;
            }

            propuesta.FechaActualizacion = DateTime.UtcNow;
            await PropuestaDeFestivalProyectada.GuardarDiferenciasAsync(
                db, propuesta, proyectada.VersionOrigen, perfil, practicas, territorios, propuesta.FechaActualizacion.Value, ct);
            db.AuditLogs.Add(new AuditLogRow { UserId = personaId.Value, TableName = "Festivales", RecordId = propuesta.RegistroId, Action = AccionesAuditoria.Actualizar, NewValuesJson = "{\"Evento\":\"BorradorInstitucionalActualizado\"}", CreatedAt = propuesta.FechaActualizacion!.Value });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { id = propuesta.Id, estado = propuesta.Estado });
        });

        institucional.MapPost("/{propuestaId:int}/enviar-borrador-institucional", async (
            long propuestaId, ClaimsPrincipal principal, PnmcDbContext db, IAntiforgery antiforgery, HttpContext httpContext, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext)) return Results.BadRequest(new { message = "El envío no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var propuesta = await db.PropuestasDeCambio.FirstOrDefaultAsync(
                item => item.Id == propuestaId && item.ModuloId == ModuloDelRegistro
                    && item.Estado != EstadosDePropuesta.Aplicada && item.Estado != EstadosDePropuesta.Rechazada, ct);
            if (propuesta is null) return Results.NotFound();
            var entidad = await db.EntityProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == propuesta.IdOrganizacion, ct);
            if (entidad?.IsInstitutional != true) return Results.Forbid();
            if (propuesta.Estado is not (EstadosDePropuesta.Borrador or EstadosDePropuesta.AjustesSolicitados)) return Results.Conflict(new { message = "Solo un borrador institucional puede enviarse a revisión.", estado = propuesta.Estado });
            var proyectadaAlEnviar = await PropuestaDeFestivalProyectada.DeAsync(db, propuesta, ct);
            if (proyectadaAlEnviar is null) return Results.Conflict(new { message = "La versión de origen de la propuesta no está disponible." });
            if (ValidationHelpers.IsMissing(proyectadaAlEnviar.Nombre)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["nombre"] = ["El nombre del Festival es obligatorio antes de enviarlo a revisión."] });
            propuesta.Estado = EstadosDePropuesta.EnRevision;
            propuesta.FechaEnvio = DateTime.UtcNow;
            propuesta.FechaActualizacion = propuesta.FechaEnvio.Value;
            db.HistorialesRevisionRegistros.Add(new HistorialRevisionRegistroRow { ModuloId = Modulo, RegistroId = propuesta.Id.ToString(CultureInfo.InvariantCulture), EstadoAnterior = EstadosFestival.Borrador, EstadoNuevo = EstadosFestival.EnRevision, Accion = "BorradorInstitucionalEnviadoARevision", UsuarioId = personaId.Value, Fecha = propuesta.FechaEnvio.Value, MetadataJson = $"{{\"FestivalOrigenId\":{propuesta.RegistroId}}}" });
            db.AuditLogs.Add(new AuditLogRow { UserId = personaId.Value, TableName = "Festivales", RecordId = propuesta.RegistroId, Action = AccionesAuditoria.Actualizar, NewValuesJson = "{\"Evento\":\"BorradorInstitucionalEnviadoARevision\"}", CreatedAt = propuesta.FechaEnvio.Value });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { id = propuesta.Id, estado = propuesta.Estado });
        });

        institucional.MapPost("/{propuestaId:int}/decisiones", async (
            long propuestaId, DecisionRevisionFestivalSolicitud solicitud, ClaimsPrincipal principal, PnmcDbContext db,
            IAntiforgery antiforgery, HttpContext httpContext, CancellationToken ct) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext)) return Results.BadRequest(new { message = "La decisión institucional no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var propuesta = await db.PropuestasDeCambio.FirstOrDefaultAsync(
                item => item.Id == propuestaId && item.ModuloId == ModuloDelRegistro, ct);
            if (propuesta is null) return Results.NotFound();
            if (propuesta.Estado != EstadosDePropuesta.EnRevision) return Results.Conflict(new { message = "Solo una propuesta en revisión puede recibir una decisión institucional.", estado = propuesta.Estado });
            var decision = ResolverDecision(solicitud);
            if (decision.Errores.Count > 0) return Results.ValidationProblem(decision.Errores);
            var proyectada = await PropuestaDeFestivalProyectada.DeAsync(db, propuesta, ct);
            if (proyectada is null) return Results.Conflict(new { message = "La versión de origen de la propuesta no está disponible." });
            var festivalOrigenId = FestivalDe(propuesta);
            var festival = await db.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalOrigenId, ct);
            if (festival is null) return Results.Conflict(new { message = "El Festival de la propuesta no está disponible." });
            var ahora = DateTime.UtcNow;

            // BAJO LA ESTRATEGIA DE EJECUCION. Mismo bloqueo que la normalizacion de versiones y
            // que el POST de propuestas: con `EnableRetryOnFailure` (produccion) una transaccion
            // iniciada fuera de la estrategia lanza en el primer SaveChangesAsync, y la decision
            // institucional respondia 500 SIEMPRE contra SQL Server. Los `return` tempranos de
            // dentro salen sin Commit: la transaccion se descarta al disponerse, como antes.
            var estrategia = db.Database.CreateExecutionStrategy();
            return await estrategia.ExecuteAsync<IResult>(async () =>
            {
                await using var transaccion = await db.Database.BeginTransactionAsync(ct);
                if (decision.EstadoNuevo == "Publicada")
                {
                    var versionOrigen = await db.VersionesFestival.FirstOrDefaultAsync(item => item.Id == proyectada.VersionOrigen.Id, ct);
                    if (versionOrigen is null || !versionOrigen.EsVigente)
                        return Results.Conflict(new { message = "La propuesta se basa en una versión que ya no está vigente. Crea una nueva propuesta desde la versión pública actual." });
                    var nuevaVersion = new VersionFestivalRow
                    {
                        FestivalOrigenId = festivalOrigenId, NumeroVersion = versionOrigen.NumeroVersion + 1, EsVigente = true,
                        // PUBLICADA, PORQUE ESO ES LO QUE ACABA DE PASAR. Esta versión nace de una
                        // propuesta aprobada y pasa a ser la que lee el público; sin estado quedaba
                        // fuera de todo vocabulario. Era el origen de la única versión sin estado
                        // que había en la base.
                        EstadoRegistro = EstadosFestival.Publicado,
                        // LO QUE SE PUBLICA ES LA VERSION DE ORIGEN CON LO PROPUESTO ENCIMA. La
                        // proyección lo resuelve campo a campo: lo que la propuesta no toca se
                        // hereda, y lo que toca se escribe. Antes esto eran dieciséis asignaciones
                        // desde la sombra, y bastaba olvidar una para publicar una versión coja.
                        Nombre = proyectada.Nombre, Descripcion = proyectada.Descripcion, NivelCobertura = proyectada.NivelCobertura,
                        CodigoDepartamento = proyectada.CodigoDepartamento, CodigoMunicipio = proyectada.CodigoMunicipio,
                        Periodicidad = proyectada.Periodicidad, PeriodicidadDetalle = proyectada.PeriodicidadDetalle, CorreoContacto = proyectada.CorreoContacto,
                        ObservacionesContacto = proyectada.ObservacionesContacto,
                        Director = proyectada.Director, TipoOrganizadorId = proyectada.TipoOrganizadorId,
                        // El contacto viaja con la propuesta hasta la version que se publica. Sin
                        // estas siete lineas, aprobar una propuesta publicaria una version sin
                        // telefono ni redes: la ficha publica perderia datos al avanzar.
                        TelefonoContacto = proyectada.TelefonoContacto,
                        Instagram = proyectada.Instagram,
                        Facebook = proyectada.Facebook,
                        SitioWeb = proyectada.SitioWeb,
                        OtroEnlace = proyectada.OtroEnlace,
                        FechaPublicacion = ahora, FechaCreacion = ahora
                    };
                    // LA SUSTITUIDA PASA A SER HISTORIA, Y SU ESTADO TIENE QUE DECIRLO.
                    //
                    // Antes solo se le quitaba la vigencia y se quedaba en `publicado`, de modo que
                    // dos versiones del mismo Festival decían estar publicadas y solo una lo estaba.
                    // `archivado` es lo que significa en este catálogo «esto ya pasó», y además no
                    // es editable: una versión sustituida no debe poder retocarse, porque lo que se
                    // retocaría no es lo que nadie está leyendo.
                    versionOrigen.EsVigente = false;
                    versionOrigen.EstadoRegistro = EstadosFestival.Archivado;
                    db.VersionesFestival.Add(nuevaVersion);
                    try
                    {
                        await db.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateException)
                    {
                        return Results.Conflict(new { message = "La propuesta no puede publicarse porque la versión vigente cambió. Revise la propuesta antes de intentarlo nuevamente." });
                    }
                    // LAS DOS LISTAS SALEN TAMBIEN DE LA PROYECCION: las propuestas si la propuesta
                    // las toca, y las de la versión de origen si no.
                    db.Agregar<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, nuevaVersion.Id, proyectada.PracticasMusicales, ahora);
                    db.Agregar<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, nuevaVersion.Id, proyectada.TerritoriosSonoros, ahora);
                    propuesta.SubregistroResultanteId = nuevaVersion.Id.ToString(CultureInfo.InvariantCulture);
                }

                propuesta.Estado = decision.EstadoNuevo switch
                {
                    "Publicada" => EstadosDePropuesta.Aplicada,
                    "Rechazada" => EstadosDePropuesta.Rechazada,
                    _ => EstadosDePropuesta.AjustesSolicitados,
                };
                // LA COLUMNA `Activa` NO EXISTE AQUI, y no hace falta: «sigue viva» es «ni aplicada
                // ni rechazada», deducido del estado en vez de mantenido a mano en cada transición.
                propuesta.FechaActualizacion = ahora;
                propuesta.FechaDecision = EstadosDePropuesta.EstaCerrada(propuesta.Estado) ? ahora : null;
                var filaHistorial = new HistorialRevisionRegistroRow
                {
                    // Aqui nacia el HTTP_500 que midio la sonda de botones: la propuesta nombra sus
                    // estados en femenino ("Publicada", "Rechazada") y este historial es institucional
                    // y comun a todos los modulos, con CK_RegistrosRevisionHistorial_EstadoNuevo
                    // cerrado a los siete codigos. El SaveChangesAsync caia fuera del try/catch de
                    // DbUpdateException, asi que salia como 500 en vez de como un rechazo explicado.
                    ModuloId = Modulo, RegistroId = propuesta.Id.ToString(CultureInfo.InvariantCulture),
                    EstadoAnterior = EstadosFestival.EnRevision,
                    EstadoNuevo = EstadosFestival.DesdeContrato(decision.EstadoNuevo) ?? EstadosFestival.EnRevision,
                    Accion = decision.Evento!, Comentario = decision.Observacion, MotivoRechazo = decision.MotivoRechazo, UsuarioId = personaId.Value, Fecha = ahora,
                    MetadataJson = $"{{\"FestivalOrigenId\":{festivalOrigenId},\"VersionOrigenId\":{proyectada.VersionOrigen.Id},\"VersionNuevaId\":{propuesta.SubregistroResultanteId ?? "null"}}}"
                };
                await InstantaneaDelHistorial.TomarAsync(
                    db, filaHistorial, propuesta.IdOrganizacion, personaId.Value, ct);
                db.HistorialesRevisionRegistros.Add(filaHistorial);
                db.AuditLogs.Add(new AuditLogRow
                {
                    // El otro CHECK del mismo 500: "FestivalPropuestaPublicada" no esta entre los
                    // trece verbos tecnicos de CK_BitacoraAuditoria_Accion.
                    UserId = personaId.Value, TableName = "Festivales", RecordId = propuesta.Id.ToString(CultureInfo.InvariantCulture),
                    Action = AccionesAuditoria.DeEvento(decision.Evento),
                    PreviousValuesJson = $"{{\"Estado\":\"{EstadosFestival.EnRevision}\"}}",
                    NewValuesJson = $"{{\"Estado\":\"{decision.EstadoNuevo}\",\"FestivalOrigenId\":{festivalOrigenId},\"Evento\":\"{decision.Evento}\"}}", CreatedAt = ahora
                });
                await CrearNotificacionDecisionAsync(db, propuesta, festival.Name, decision, ahora, ct);
                await db.SaveChangesAsync(ct);
                await transaccion.CommitAsync(ct);
                return Results.Ok(new
                {
                    id = propuesta.Id,
                    estado = propuesta.Estado,
                    versionNuevaId = propuesta.SubregistroResultanteId is null
                        ? (int?)null
                        : int.Parse(propuesta.SubregistroResultanteId, NumberStyles.Integer, CultureInfo.InvariantCulture),
                    observacion = decision.Observacion ?? decision.MotivoRechazo,
                });
            });
        });
        return group;
    }

    /// <summary>
    /// La propuesta como la compara la consola: el perfil entero que propondría.
    /// </summary>
    /// <remarks>
    /// EL CONTRATO NO CAMBIA aunque la tabla sí. La pantalla enseña también los campos que NO
    /// cambian —para que quien aprueba vea que se revisaron—, así que necesita el perfil completo:
    /// la versión de origen con lo propuesto encima.
    /// </remarks>
    private static async Task<object> CrearDetallePropuestaAsync(PropuestaDeFestivalProyectada p, PnmcDbContext db, CancellationToken ct) => new
    {
        Id = p.Expediente.Id,
        FestivalOrigenId = FestivalDe(p.Expediente),
        VersionOrigenId = p.VersionOrigen.Id,
        VersionNuevaId = p.Expediente.SubregistroResultanteId,
        p.Expediente.Estado,
        p.Nombre, p.Descripcion, p.NivelCobertura, p.CodigoDepartamento, p.CodigoMunicipio,
        p.Periodicidad, p.CorreoContacto, p.TelefonoContacto, p.Instagram, p.Facebook,
        paginaWeb = p.SitioWeb, p.OtroEnlace, p.ObservacionesContacto,
        FechaEnvioRevision = p.Expediente.FechaEnvio,
        practicasMusicales = await db.PracticasMusicales.AsNoTracking().Where(x => p.PracticasMusicales.Contains(x.Id))
            .OrderBy(x => x.Nombre).Select(x => new CatalogoFestivalDto(x.Id, x.Nombre)).ToListAsync(ct),
        territoriosSonoros = await db.TerritoriosSonoros.AsNoTracking().Where(x => p.TerritoriosSonoros.Contains(x.Id))
            .OrderBy(x => x.Nombre).Select(x => new CatalogoFestivalDto(x.Id, x.Nombre)).ToListAsync(ct),
    };
    private static async Task<object> CrearDetalleVersionAsync(VersionFestivalRow v, PnmcDbContext db, CancellationToken ct) => new { v.Id, v.NumeroVersion, v.EsVigente, v.Nombre, v.Descripcion, v.NivelCobertura, v.CodigoDepartamento, v.CodigoMunicipio, v.Periodicidad, v.CorreoContacto, v.TelefonoContacto, v.Instagram, v.Facebook, paginaWeb = v.SitioWeb, v.OtroEnlace, v.ObservacionesContacto, v.FechaPublicacion, practicasMusicales = await CatalogosPropuestaAsync(db.De<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, v.Id).AsNoTracking().Select(x => x.ValorId), db.PracticasMusicales, ct), territoriosSonoros = await CatalogosPropuestaAsync(db.De<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, v.Id).AsNoTracking().Select(x => x.ValorId), db.TerritoriosSonoros, ct) };
    private static async Task<List<CatalogoFestivalDto>> CatalogosPropuestaAsync(IQueryable<int> ids, DbSet<PracticaMusicalRow> catalogo, CancellationToken ct) => await catalogo.AsNoTracking().Where(item => ids.Contains(item.Id)).OrderBy(item => item.Nombre).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre)).ToListAsync(ct);
    private static async Task<List<CatalogoFestivalDto>> CatalogosPropuestaAsync(IQueryable<int> ids, DbSet<TerritorioSonoroRow> catalogo, CancellationToken ct) => await catalogo.AsNoTracking().Where(item => ids.Contains(item.Id)).OrderBy(item => item.Nombre).Select(item => new CatalogoFestivalDto(item.Id, item.Nombre)).ToListAsync(ct);

    private static (string? EstadoNuevo, string? Evento, string? Observacion, string? MotivoRechazo, Dictionary<string, string[]> Errores) ResolverDecision(DecisionRevisionFestivalSolicitud s)
    {
        var accion = s.Accion?.Trim(); var observacion = Limpiar(s.Observacion); var motivo = Limpiar(s.MotivoRechazo);
        return accion switch
        {
            "SolicitarAjustes" when string.IsNullOrWhiteSpace(observacion) => (null, null, null, null, new() { ["observacion"] = ["La observación es obligatoria para solicitar ajustes."] }),
            "SolicitarAjustes" => ("AjustesSolicitados", "FestivalPropuestaAjustesSolicitados", observacion, null, new()),
            "Rechazar" when string.IsNullOrWhiteSpace(motivo) => (null, null, null, null, new() { ["motivoRechazo"] = ["El motivo es obligatorio para rechazar la propuesta."] }),
            "Rechazar" => ("Rechazada", "FestivalPropuestaRechazada", null, motivo, new()),
            "Publicar" => ("Publicada", "FestivalPropuestaPublicada", null, null, new()),
            _ => (null, null, null, null, new() { ["accion"] = ["La acción institucional no es válida."] })
        };
    }
    private static async Task CrearNotificacionDecisionAsync(PnmcDbContext db, PropuestaDeCambioRow p, string festival, (string? EstadoNuevo, string? Evento, string? Observacion, string? MotivoRechazo, Dictionary<string, string[]> Errores) d, DateTime fecha, CancellationToken ct)
    {
        var persona = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == p.IdUsuarioProponente && item.IsActive, ct); if (persona is null) return;
        // Quien decide es el equipo del PNMC. SIMUS es una plataforma externa
        // del Ministerio y no interviene en esta revision.
        var texto = d.EstadoNuevo switch { "AjustesSolicitados" => $"El equipo del PNMC solicitó ajustes para tu propuesta sobre “{festival}”. {d.Observacion}", "Rechazada" => $"El equipo del PNMC rechazó tu propuesta sobre “{festival}”. Motivo: {d.MotivoRechazo}", _ => $"Tu propuesta de cambios para “{festival}” fue publicada como una nueva versión." };
        db.Notifications.Add(new NotificationRow { RecipientUserId = persona.Id, RecipientEmail = persona.Email, EventType = d.Evento!, Channel = "internal", Title = d.EstadoNuevo == "Publicada" ? "Propuesta publicada" : "Actualización de propuesta", Body = texto, Status = "enviada", ModuloId = Modulo, RecordId = p.Id.ToString(CultureInfo.InvariantCulture), MetadataJson = $"{{\"FestivalOrigenId\":{p.RegistroId},\"Estado\":\"{d.EstadoNuevo}\"}}", CreatedAt = fecha, SentAt = fecha, Attempts = 0 });
    }
    private static int? ObtenerPersonaId(ClaimsPrincipal p) => int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    private static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
