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

public static class PropuestasDeCambioFestivalExternosEndpoints
{
    /// <summary>El módulo con el que viajan estas propuestas, en el vocabulario de la auditoría.</summary>
    internal const string Modulo = Modulos.Festivales;

    /// <summary>
    /// La propuesta que todavía espera algo: ni aplicada ni rechazada.
    /// </summary>
    /// <remarks>
    /// SUSTITUYE A LA COLUMNA `Activa` de la tabla vieja, que decía lo mismo con un booleano que
    /// había que mantener a mano en cada transición. Aquí se deduce del estado, así que no puede
    /// contradecirlo.
    /// </remarks>
    internal static Task<PropuestaDeCambioRow?> PropuestaVivaAsync(
        PnmcDbContext dbContext, int festivalId, bool seguimiento, CancellationToken cancellationToken)
    {
        var registroId = festivalId.ToString(CultureInfo.InvariantCulture);
        var consulta = seguimiento ? dbContext.PropuestasDeCambio : dbContext.PropuestasDeCambio.AsNoTracking();
        return consulta
            .Where(item => item.ModuloId == Modulo && item.RegistroId == registroId
                && item.Estado != EstadosDePropuesta.Aplicada && item.Estado != EstadosDePropuesta.Rechazada)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static RouteGroupBuilder MapPropuestasDeCambioFestivalExternosEndpoints(this RouteGroupBuilder group)
    {
        var externo = group.MapGroup("/externo/festivales").WithTags("propuestas-cambio-festival");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        externo.MapPost("/{festivalId:int}/propuestas-cambio", async (
            int festivalId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La propuesta de cambio no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is null
                || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, festival.OrganizacionPrincipalId.Value, cancellationToken)) return Results.Forbid();
            // NUNCA COMPARE ESTADOS CON UN LITERAL. Esta linea decia `!= "Publicado"`, con
            // mayuscula, y era una comparacion EN MEMORIA sobre la fila ya cargada: sin la
            // collation insensible de la base que perdona los literales en las consultas. El
            // valor almacenado es siempre `publicado` en minuscula —lo escribe la decision
            // institucional y asi estan los 30 Festivales historicos—, de modo que la condicion
            // era CIERTA SIEMPRE y ningun Festival publicado podia recibir una propuesta de
            // cambio. La respuesta lo delataba sin que nadie lo leyera: 409 diciendo «solo un
            // Festival publicado» y `estado: "publicado"` en el mismo cuerpo.
            //
            // Es la tercera regla del ecosistema —nadie edita lo publicado, hay que proponer un
            // cambio— sin ningun camino abierto. La suite no lo veia porque sus pruebas escribian
            // `StatusCode = "Publicado"` a mano, en una grafia que ningun camino de produccion
            // produce: verde sobre una ruta inalcanzable.
            if (!EstadosFestival.Es(festival.StatusCode, EstadosFestival.Publicado))
                return Results.Conflict(new
                {
                    message = "Solo un Festival publicado puede recibir una propuesta de cambio.",
                    estado = EstadosFestival.HaciaContrato(festival.StatusCode)
                });

            var existente = await PropuestaVivaAsync(dbContext, festival.Id, seguimiento: false, cancellationToken);
            if (existente is not null) return Results.Ok(await ADtoAsync(existente, dbContext, cancellationToken));

            // BAJO LA ESTRATEGIA DE EJECUCION, no a pelo. Produccion registra el contexto con
            // `EnableRetryOnFailure` (DependencyInjection.cs) y EF prohibe una transaccion
            // iniciada por el usuario fuera de la estrategia: el primer SaveChangesAsync lanza
            // InvalidOperationException y este POST respondia 500 SIEMPRE contra SQL Server.
            // Es el mismo bloqueo que se diagnostico el 23 ago 2026 en la normalizacion de
            // versiones; aqui lo encontro la documentacion del modulo, no una prueba, porque
            // el arnes de SQL Server no reintentaba y el de SQLite no tiene estrategia.
            var ahora = DateTime.UtcNow;
            var estrategia = dbContext.Database.CreateExecutionStrategy();
            var propuesta = await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                var version = await ObtenerOCrearVersionVigenteAsync(festival, dbContext, cancellationToken);
                var nueva = new PropuestaDeCambioRow
                {
                    ModuloId = Modulo,
                    RegistroId = festival.Id.ToString(CultureInfo.InvariantCulture),
                    // SOBRE QUE VERSION SE PROPONE. Es el «antes» del expediente, y no cambia aunque
                    // el Festival publique otra: una propuesta se comparó contra lo que el público
                    // leía cuando se hizo.
                    SubregistroId = version.Id.ToString(CultureInfo.InvariantCulture),
                    Estado = EstadosDePropuesta.Borrador,
                    IdOrganizacion = festival.OrganizacionPrincipalId.Value,
                    IdUsuarioProponente = personaId.Value,
                    FechaCreacion = ahora,
                    FechaActualizacion = ahora,
                };
                dbContext.PropuestasDeCambio.Add(nueva);
                // SIN NINGUNA FILA DE CAMPO, y eso es lo correcto: una propuesta recién abierta no
                // propone nada todavía. En la forma vieja nacía como copia exacta de la versión, que
                // era la misma idea dicha con dieciséis columnas que decían lo mismo que el origen.
                //
                // SE GUARDA ANTES DE AUDITAR porque la línea de la bitácora se archiva POR EL
                // IDENTIFICADOR del expediente, y hasta este `SaveChanges` vale cero: auditar antes
                // dejaba el rastro colgado de un registro que no existe.
                await dbContext.SaveChangesAsync(cancellationToken);
                RegistrarAuditoria(dbContext, personaId.Value, nueva, festival.Id, version.Id, "FestivalCambioPropuesto", ahora);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
                return nueva;
            });

            return Results.Created($"/api/v1/externo/festivales/{festival.Id}/propuesta-cambio", await ADtoAsync(propuesta, dbContext, cancellationToken));
        });

        externo.MapGet("/{festivalId:int}/propuesta-cambio", async (
            int festivalId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is null
                || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, festival.OrganizacionPrincipalId.Value, cancellationToken)) return Results.Forbid();
            var registroId = festival.Id.ToString(CultureInfo.InvariantCulture);
            var propuesta = await dbContext.PropuestasDeCambio.AsNoTracking()
                .Where(item => item.ModuloId == Modulo && item.RegistroId == registroId)
                .OrderByDescending(item => item.Estado != EstadosDePropuesta.Aplicada && item.Estado != EstadosDePropuesta.Rechazada)
                .ThenByDescending(item => item.FechaActualizacion)
                .FirstOrDefaultAsync(cancellationToken);
            return propuesta is null ? Results.NotFound() : Results.Ok(await ADtoAsync(propuesta, dbContext, cancellationToken));
        });

        externo.MapPut("/{festivalId:int}/propuesta-cambio", async (
            int festivalId,
            CrearFestivalBorradorSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La propuesta de cambio no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is null
                || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, festival.OrganizacionPrincipalId.Value, cancellationToken)) return Results.Forbid();
            var propuesta = await PropuestaVivaAsync(dbContext, festival.Id, seguimiento: true, cancellationToken);
            if (propuesta is null) return Results.NotFound();
            if (propuesta.Estado is not (EstadosDePropuesta.Borrador or EstadosDePropuesta.AjustesSolicitados))
                return Results.Conflict(new { message = "Esta propuesta no puede modificarse directamente en su estado actual.", estado = propuesta.Estado });

            var errores = await ValidarSolicitudAsync(solicitud, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var proyectada = await PropuestaDeFestivalProyectada.DeAsync(dbContext, propuesta, cancellationToken);
            if (proyectada is null)
                return Results.Conflict(new { message = "La propuesta se basa en una versión que ya no existe. Crea una nueva propuesta desde la versión pública actual." });

            var ahora = DateTime.UtcNow;

            // EL PERFIL COMPLETO QUE LLEGA DEL FORMULARIO, con los nombres de campo del catálogo.
            // Lo que se GUARDA es solo aquello en que se aparta de la versión de origen: calcular la
            // diferencia aquí, y no en el cliente, impide que quien propone decida contra qué se le
            // compara.
            var perfil = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["nombre"] = ValidationHelpers.SanitizeText(solicitud.Nombre, 240),
                ["descripcion"] = LimpiarTexto(solicitud.Descripcion),
                ["periodicidad"] = PeriodicidadesFestival.Normalizar(solicitud.Periodicidad),
                ["periodicidadDetalle"] = LimpiarTexto(solicitud.PeriodicidadDetalle),
                ["correoContacto"] = NormalizarCorreo(solicitud.CorreoContacto),
                ["nivelCobertura"] = NormalizarNivel(solicitud.NivelCobertura),
                ["codigoDepartamento"] = LimpiarTexto(solicitud.CodigoDepartamento),
                ["codigoMunicipio"] = LimpiarTexto(solicitud.CodigoMunicipio),
                ["instagram"] = LimpiarTexto(solicitud.Instagram),
                ["facebook"] = LimpiarTexto(solicitud.Facebook),
                ["sitioWeb"] = LimpiarTexto(solicitud.PaginaWeb),
                ["otroEnlace"] = LimpiarTexto(solicitud.OtroEnlace),
                ["telefonoContacto"] = LimpiarTexto(solicitud.TelefonoCelular),
                ["observacionesContacto"] = LimpiarTexto(solicitud.ObservacionesContacto),
            };

            await PropuestaDeFestivalProyectada.GuardarDiferenciasAsync(
                dbContext, propuesta, proyectada.VersionOrigen, perfil,
                solicitud.PracticasMusicalesIds.Distinct().ToList(),
                solicitud.TerritoriosSonorosIds.Distinct().ToList(),
                ahora, cancellationToken);
            propuesta.FechaActualizacion = ahora;
            RegistrarAuditoria(dbContext, personaId.Value, propuesta, festival.Id, proyectada.VersionOrigen.Id, "FestivalCambioPropuestoActualizado", ahora);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(await ADtoAsync(propuesta, dbContext, cancellationToken));
        });

        externo.MapPost("/{festivalId:int}/propuesta-cambio/enviar-a-revision", async (
            int festivalId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "El envío de la propuesta no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();
            var festival = await dbContext.FestivalRecords.AsNoTracking().FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival?.OrganizacionPrincipalId is not int organizacionId) return festival is null ? Results.NotFound() : Results.Forbid();
            if (!await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken)) return Results.Forbid();
            var propuesta = await PropuestaVivaAsync(dbContext, festivalId, seguimiento: true, cancellationToken);
            if (propuesta is null) return Results.NotFound();
            if (propuesta.Estado is not (EstadosDePropuesta.Borrador or EstadosDePropuesta.AjustesSolicitados))
                return Results.Conflict(new { message = "Solo una propuesta en borrador o con ajustes solicitados puede enviarse a revisión.", estado = propuesta.Estado });

            var proyectada = await PropuestaDeFestivalProyectada.DeAsync(dbContext, propuesta, cancellationToken);
            if (proyectada is null)
                return Results.Conflict(new { message = "La propuesta se basa en una versión que ya no existe. Crea una nueva propuesta desde la versión pública actual." });

            // UNA PROPUESTA QUE NO CAMBIA NADA NO ES UNA PROPUESTA. En la forma vieja esto no se
            // podía comprobar —la sombra siempre tenía los dieciséis campos, iguales o no—, así que
            // el Programa recibía expedientes que no pedían nada.
            if (proyectada.Propuestos.Count == 0)
                return Results.Conflict(new { message = "La propuesta no cambia ningún campo frente a lo publicado. Modifica algo antes de enviarla." });

            var errores = await ValidarPropuestaParaRevisionAsync(proyectada, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var estadoAnterior = propuesta.Estado;
            var ahora = DateTime.UtcNow;
            propuesta.Estado = EstadosDePropuesta.EnRevision;
            propuesta.FechaEnvio = ahora;
            propuesta.FechaActualizacion = ahora;
            var filaHistorial = new HistorialRevisionRegistroRow
            {
                ModuloId = Modulos.PropuestasDeCambioDeFestival, RegistroId = propuesta.Id.ToString(CultureInfo.InvariantCulture),
                // CK_RegistrosRevisionHistorial_EstadoNuevo admite los siete codigos de
                // EstadosContenido en minuscula, y "EnRevision" no es uno de ellos. La
                // propuesta conserva su propio estado en PropuestasCambioFestival.Estado
                // —esa columna no tiene CHECK y es otro ciclo, el de §30—, pero la fila del
                // historial institucional viaja en el vocabulario comun.
                EstadoAnterior = EstadosFestival.DesdeContrato(estadoAnterior) ?? estadoAnterior,
                EstadoNuevo = EstadosFestival.EnRevision, Accion = "FestivalPropuestaEnviadaARevision",
                UsuarioId = personaId.Value, Fecha = ahora,
                MetadataJson = $"{{\"FestivalOrigenId\":{festivalId},\"VersionOrigenId\":{proyectada.VersionOrigen.Id},\"OrganizacionId\":{propuesta.IdOrganizacion}}}"
            };
            await InstantaneaDelHistorial.TomarAsync(
                dbContext, filaHistorial, propuesta.IdOrganizacion, personaId.Value, cancellationToken);
            dbContext.HistorialesRevisionRegistros.Add(filaHistorial);
            RegistrarAuditoria(dbContext, personaId.Value, propuesta, festivalId, proyectada.VersionOrigen.Id, "FestivalPropuestaEnviadaARevision", ahora);
            await CrearNotificacionAsync(dbContext, personaId.Value, propuesta, festivalId, festival.Name, "FestivalPropuestaEnviadaARevision", "Propuesta enviada a revisión", "Tu propuesta de cambios para el Festival", ahora, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(await ADtoAsync(propuesta, dbContext, cancellationToken));
        });

        return group;
    }

    private static async Task<VersionFestivalRow> ObtenerOCrearVersionVigenteAsync(FestivalRow festival, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var vigente = await dbContext.VersionesFestival.FirstOrDefaultAsync(item => item.FestivalOrigenId == festival.Id && item.EsVigente, cancellationToken);
        if (vigente is not null) return vigente;

        // `MAX(NumeroVersion) + 1` Y NO `1` A SECAS: un Festival histórico puede llegar aquí con
        // filas en VersionesFestival ya creadas por otro camino (p.ej. una edición registrada
        // antes de que existiera este atajo) y ninguna marcada vigente. Escribir `1` a ciegas
        // choca con `UQ_VersionesFestival_Festival_Numero` y esta ruta respondía 500 siempre que
        // el Festival ya tuviera al menos una versión numerada.
        var ultimoNumero = await dbContext.VersionesFestival
            .Where(item => item.FestivalOrigenId == festival.Id)
            .Select(item => (int?)item.NumeroVersion)
            .MaxAsync(cancellationToken);

        var ahora = DateTime.UtcNow;
        vigente = new VersionFestivalRow
        {
            FestivalOrigenId = festival.Id,
            NumeroVersion = (ultimoNumero ?? 0) + 1,
            EsVigente = true,
            // PUBLICADA: es la fotografía de lo que el público ya está leyendo, tomada para que la
            // propuesta tenga de dónde partir. No es un borrador de nadie.
            EstadoRegistro = EstadosFestival.Publicado,
            Nombre = festival.Name,
            Descripcion = festival.Description,
            NivelCobertura = festival.CoverageLevel,
            CodigoDepartamento = festival.DepartmentCode,
            CodigoMunicipio = festival.MunicipalityCode,
            Periodicidad = festival.Periodicidad,
            PeriodicidadDetalle = festival.PeriodicidadDetalle,
            CorreoContacto = festival.ContactEmail,
            // La version inicial de un festival historico hereda el contacto que la cabecera tenia
            // en ese momento. A partir de aqui manda la version: la cabecera puede cambiar sin que
            // la ficha publicada se mueva, que es todo el punto.
            TelefonoContacto = festival.ContactPhone,
            Instagram = festival.InstagramUrl,
            Facebook = festival.FacebookUrl,
            SitioWeb = festival.WebsiteUrl,
            OtroEnlace = festival.OtherUrl,
            FechaPublicacion = festival.UpdatedAt ?? festival.CreatedAt,
            FechaCreacion = ahora
        };
        dbContext.VersionesFestival.Add(vigente);
        await dbContext.SaveChangesAsync(cancellationToken);

        var practicas = await dbContext.ValoresAsync<PracticaMusicalDeRegistroRow>(Modulos.Festivales, festival.Id, cancellationToken);
        var territorios = await dbContext.ValoresAsync<TerritorioSonoroDeRegistroRow>(Modulos.Festivales, festival.Id, cancellationToken);
        dbContext.Agregar<PracticaMusicalDeRegistroRow>(Modulos.VersionesDeFestival, vigente.Id, practicas, ahora);
        dbContext.Agregar<TerritorioSonoroDeRegistroRow>(Modulos.VersionesDeFestival, vigente.Id, territorios, ahora);
        await dbContext.SaveChangesAsync(cancellationToken);
        return vigente;
    }

    /// <summary>La propuesta como la lee su organización: el perfil entero que propondría.</summary>
    /// <remarks>
    /// EL CONTRATO NO CAMBIA aunque la tabla sí: el expediente guarda solo lo que cambia, y aquí se
    /// reconstruye poniéndolo encima de la versión sobre la que se propuso. Ver
    /// <see cref="PropuestaDeFestivalProyectada"/>.
    /// </remarks>
    private static async Task<PropuestaCambioFestivalDto?> ADtoAsync(
        PropuestaDeCambioRow expediente, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var proyectada = await PropuestaDeFestivalProyectada.DeAsync(dbContext, expediente, cancellationToken);
        if (proyectada is null) return null;

        var practicas = await dbContext.PracticasMusicales.AsNoTracking()
            .Where(item => proyectada.PracticasMusicales.Contains(item.Id)).OrderBy(item => item.Nombre)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre)).ToListAsync(cancellationToken);
        var territorios = await dbContext.TerritoriosSonoros.AsNoTracking()
            .Where(item => proyectada.TerritoriosSonoros.Contains(item.Id)).OrderBy(item => item.Nombre)
            .Select(item => new CatalogoFestivalDto(item.Id, item.Nombre)).ToListAsync(cancellationToken);

        var expedienteId = expediente.Id.ToString(CultureInfo.InvariantCulture);
        return new PropuestaCambioFestivalDto(
            expedienteId, expediente.RegistroId,
            proyectada.VersionOrigen.Id.ToString(CultureInfo.InvariantCulture), expediente.Estado,
            proyectada.Nombre, proyectada.Descripcion,
            proyectada.NivelCobertura, proyectada.CodigoDepartamento, proyectada.CodigoMunicipio, proyectada.Periodicidad,
            proyectada.CorreoContacto, practicas, territorios, expediente.FechaEnvio,
            await dbContext.HistorialesRevisionRegistros.AsNoTracking()
                .Where(item => item.ModuloId == Modulos.PropuestasDeCambioDeFestival && item.RegistroId == expedienteId)
                .OrderByDescending(item => item.Fecha).Select(item => item.Comentario ?? item.MotivoRechazo).FirstOrDefaultAsync(cancellationToken),
            proyectada.Instagram, proyectada.Facebook, proyectada.SitioWeb, proyectada.OtroEnlace,
            proyectada.TelefonoContacto, proyectada.ObservacionesContacto,
            PeriodicidadDetalle: proyectada.PeriodicidadDetalle);
    }

    private static async Task<Dictionary<string, string[]>> ValidarSolicitudAsync(CrearFestivalBorradorSolicitud solicitud, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (ValidationHelpers.IsMissing(solicitud.Nombre)) errores["nombre"] = ["El nombre del Festival es obligatorio."];
        var periodicidad = PeriodicidadesFestival.Normalizar(solicitud.Periodicidad);
        if (periodicidad is not null && !PeriodicidadesFestival.Todas.Contains(periodicidad))
            errores["periodicidad"] = ["Selecciona una periodicidad de la lista disponible."];
        if (PeriodicidadesFestival.RequiereDetalle(periodicidad) && ValidationHelpers.IsMissing(solicitud.PeriodicidadDetalle))
            errores["periodicidadDetalle"] = ["Explica brevemente la periodicidad seleccionada."];
        if (!string.IsNullOrWhiteSpace(solicitud.CorreoContacto) && !ValidationHelpers.IsValidEmail(solicitud.CorreoContacto))
            errores["correoContacto"] = ["El correo de contacto no es válido."];
        var nivel = NormalizarNivel(solicitud.NivelCobertura);
        if (nivel is not ("municipal" or "departamental" or "nacional"))
        {
            errores["nivelCobertura"] = ["El nivel territorial no es válido."];
            return errores;
        }
        if (nivel != "nacional")
        {
            var departamento = LimpiarTexto(solicitud.CodigoDepartamento);
            if (departamento is null || !await dbContext.DivipolaLocations.AsNoTracking().AnyAsync(item => item.DepartmentCode == departamento, cancellationToken))
            {
                errores["codigoDepartamento"] = ["El departamento indicado no existe en DIVIPOLA."];
                return errores;
            }
            if (nivel == "municipal")
            {
                var municipio = LimpiarTexto(solicitud.CodigoMunicipio);
                if (municipio is null || !await dbContext.DivipolaLocations.AsNoTracking().AnyAsync(item => item.DepartmentCode == departamento && item.MunicipalityCode == municipio, cancellationToken))
                    errores["codigoMunicipio"] = ["El municipio indicado no existe en DIVIPOLA."];
            }
        }
        var practicas = solicitud.PracticasMusicalesIds.Distinct().ToArray();
        if (practicas.Length > 0 && await dbContext.PracticasMusicales.AsNoTracking().CountAsync(item => practicas.Contains(item.Id), cancellationToken) != practicas.Length)
            errores["practicasMusicalesIds"] = ["Una o más prácticas musicales no existen en el catálogo."];
        var territorios = solicitud.TerritoriosSonorosIds.Distinct().ToArray();
        if (territorios.Length > 0 && await dbContext.TerritoriosSonoros.AsNoTracking().CountAsync(item => territorios.Contains(item.Id), cancellationToken) != territorios.Length)
            errores["territoriosSonorosIds"] = ["Uno o más territorios sonoros no existen en el catálogo."];
        return errores;
    }

    private static Task<Dictionary<string, string[]>> ValidarPropuestaParaRevisionAsync(
        PropuestaDeFestivalProyectada proyectada, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        // SE VALIDA LO QUE SE PUBLICARIA, no lo que se escribió: el perfil proyectado, que es la
        // versión de origen con lo propuesto encima. Validar solo el diff dejaría pasar una
        // propuesta que borra el correo de contacto sin que nadie lo note.
        var solicitud = new CrearFestivalBorradorSolicitud
        {
            Nombre = proyectada.Nombre, Descripcion = proyectada.Descripcion,
            Periodicidad = proyectada.Periodicidad, PeriodicidadDetalle = proyectada.PeriodicidadDetalle,
            CorreoContacto = proyectada.CorreoContacto, NivelCobertura = proyectada.NivelCobertura,
            CodigoDepartamento = proyectada.CodigoDepartamento, CodigoMunicipio = proyectada.CodigoMunicipio,
            PracticasMusicalesIds = proyectada.PracticasMusicales,
            TerritoriosSonorosIds = proyectada.TerritoriosSonoros,
        };
        return ValidarSolicitudAsync(solicitud, dbContext, cancellationToken);
    }

    private static async Task<bool> PuedeAdministrarOrganizacionAsync(PnmcDbContext dbContext, int personaId, int organizacionId, CancellationToken cancellationToken) =>
        await AdministracionDeOrganizacion.PuedeAdministrarAsync(dbContext, personaId, organizacionId, cancellationToken);

    private static void RegistrarAuditoria(
        PnmcDbContext dbContext, int personaId, PropuestaDeCambioRow propuesta,
        int festivalId, int versionOrigenId, string accion, DateTime fecha) =>
        dbContext.AuditLogs.Add(new AuditLogRow
        {
            UserId = personaId,
            // LA BITACORA SIGUE NOMBRANDO LA TABLA VIEJA A PROPOSITO: `AdminAuditoriaEndpoints`
            // traduce ese nombre a «Festivales» para la pantalla, y las líneas ya escritas dicen
            // eso. Cambiarlo partiría el historial en dos mitades que no se buscan juntas.
            TableName = "Festivales",
            RecordId = propuesta.Id.ToString(CultureInfo.InvariantCulture),
            // Mismo motivo que en FestivalesExternosEndpoints: CK_BitacoraAuditoria_Accion
            // no admite "FestivalCambioPropuesto" ni sus hermanos. El nombre del evento se
            // conserva abajo, en ValoresNuevos.
            Action = AccionesAuditoria.DeEvento(accion),
            NewValuesJson = $"{{\"FestivalOrigenId\":{festivalId},\"VersionOrigenId\":{versionOrigenId},\"OrganizacionId\":{propuesta.IdOrganizacion},\"Estado\":\"{propuesta.Estado}\",\"Evento\":\"{accion}\"}}",
            CreatedAt = fecha
        });

    private static async Task CrearNotificacionAsync(PnmcDbContext dbContext, int personaId, PropuestaDeCambioRow propuesta, int festivalId, string nombreFestival, string evento, string titulo, string inicioCuerpo, DateTime fecha, CancellationToken cancellationToken)
    {
        var persona = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == personaId && item.IsActive, cancellationToken);
        if (persona is null) return;
        dbContext.Notifications.Add(new NotificationRow
        {
            RecipientUserId = persona.Id, RecipientEmail = persona.Email, EventType = evento, Channel = "internal", Title = titulo,
            Body = $"{inicioCuerpo} “{nombreFestival}” fue enviada a revisión institucional.", Status = "enviada",
            ModuloId = Modulos.PropuestasDeCambioDeFestival, RecordId = propuesta.Id.ToString(CultureInfo.InvariantCulture),
            MetadataJson = $"{{\"FestivalOrigenId\":{festivalId},\"Estado\":\"{propuesta.Estado}\"}}", CreatedAt = fecha, SentAt = fecha, Attempts = 0
        });
    }

    private static int? ObtenerPersonaId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var personaId) ? personaId : null;

    private static string NormalizarNivel(string? valor) => (valor ?? string.Empty).Trim().ToLowerInvariant();
    private static string? LimpiarTexto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : ValidationHelpers.SanitizeText(valor, 1200);
    private static string? NormalizarCorreo(string? valor) => CorreoElectronico.Normalizar(valor);
}
