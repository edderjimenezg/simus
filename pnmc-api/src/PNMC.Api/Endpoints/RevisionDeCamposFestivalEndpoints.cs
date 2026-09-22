using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// La devolución de un Festival campo por campo: el funcionario abre la ficha completa, anota qué
/// hay que cambiar en cada campo, guarda el borrador, y al enviarlo la organización recibe la lista
/// para corregirla.
/// </summary>
/// <remarks>
/// <para>
/// QUE HABIA, MEDIDO. Un solo párrafo. <c>RevisionInstitucionalFestivalesEndpoints</c> recibe
/// <c>Observacion</c> y la guarda en <c>RegistrosRevisionHistorial.Comentario</c>, nvarchar(2400),
/// una fila por decisión. Todo lo que hubiera que decir sobre cuarenta y ocho campos cabía ahí, y
/// llegaba a la organización como un aviso suelto en la tarjeta del Festival. Nada decía a qué
/// campo se refería cada frase.
/// </para>
/// <para>
/// LAS DOS MITADES VIVEN EN EL MISMO FICHERO, y no es descuido. Las rutas institucionales escriben
/// las notas y las externas las leen: es una sola conversación con dos puertas. Partirla en dos
/// ficheros hace que un cambio de vocabulario —un estado nuevo, un campo más— se aplique en uno y
/// se olvide en el otro, que es exactamente como <c>en_evaluacion</c> sobrevivió semanas.
/// </para>
/// <para>
/// POR QUE LA LECTURA INSTITUCIONAL REPITE LAS RUTAS EXTERNAS. El funcionario tiene cookie
/// <c>pnmc.admin</c> y la organización <c>pnmc.external</c>: son dos autenticaciones distintas, así
/// que <c>/externo/versiones/{id}</c> le contesta 401 aunque el dato sea el mismo. Lo que NO se
/// repite es la proyección: las cuatro lecturas de aquí llaman a los mismos métodos que sirven a la
/// organización (<c>VersionesFestivalExternosEndpoints.ADtoAsync</c>,
/// <c>FestivalesExternosEndpoints.CatalogosDelFestivalAsync</c>). Es lo que hace que las dos
/// pantallas sean la misma ficha y no dos que se parecen.
/// </para>
/// <para>
/// EL ALCANCE INSTITUCIONAL ES TODO. Un funcionario no pertenece a ninguna organización, así que
/// aquí no hay comprobación de pertenencia: la hay de ROL, en
/// <see cref="Permisos.PoliticaFuncionario"/>. En el carril externo sí se comprueba pertenencia,
/// preguntando por la organización DEL FESTIVAL y no por una que venga en la petición.
/// </para>
/// </remarks>
public static class RevisionDeCamposFestivalEndpoints
{
    /// <summary>Los tres estados de una revisión. Los mismos que <c>CK_RevisionesDeRegistro_Estado</c>.</summary>
    private const string Borrador = "borrador";
    private const string Enviada = "enviada";
    private const string Cerrada = "cerrada";

    private const string Pendiente = "pendiente";
    private const string Atendida = "atendida";

    /// <summary>El módulo de estas revisiones, en el mismo vocabulario que la auditoría.</summary>
    private const string Modulo = Modulos.Festivales;

    /// <summary>
    /// Los dos ámbitos de una nota, con los nombres genéricos.
    /// </summary>
    /// <remarks>
    /// <b>ANTES SE LLAMABAN `festival` Y `perfil_versionado`.</b> El 17 de septiembre de 2026, al
    /// pasar estas revisiones a las tablas genéricas, se alinearon con los que ya usaba Mercados:
    /// son los mismos dos conceptos —la ficha del registro y una de sus realizaciones— y tenerlos
    /// con dos nombres obligaba a traducir en medio para que dos módulos dijeran lo mismo.
    /// </remarks>
    private const string AmbitoPrincipal = "principal";
    private const string AmbitoSubregistro = "subregistro";

    /// <summary>El evento funcional que se escribe en el historial. Sin CHECK: es su sitio, según §39.</summary>
    private const string EventoCambiosPedidos = "FestivalCambiosPedidosPorCampo";

    public static RouteGroupBuilder MapRevisionDeCamposFestivalEndpoints(this RouteGroupBuilder group)
    {
        MapLadoInstitucional(group);
        MapLadoDeLaOrganizacion(group);
        return group;
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // EL LADO DEL FUNCIONARIO
    // ═══════════════════════════════════════════════════════════════════════════════════════

    private static void MapLadoInstitucional(RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional").WithTags("revision-por-campos");
        institucional.RequireAuthorization(Permisos.PoliticaFuncionario);

        // ── Las cuatro lecturas que la ficha necesita para pintarse igual que la de la organización

        institucional.MapGet("/catalogos/festival", async (PnmcDbContext dbContext, CancellationToken cancellationToken) =>
            Results.Ok(await FestivalesExternosEndpoints.CatalogosDelFestivalAsync(dbContext, cancellationToken)));

        institucional.MapGet("/festivales/{festivalId:int}/perfiles-versionados", async (
            int festivalId, PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            if (!await dbContext.FestivalRecords.AsNoTracking().AnyAsync(item => item.Id == festivalId, cancellationToken))
                return Results.NotFound();

            var etiquetas = await VersionesFestivalExternosEndpoints.EtiquetasDeEstadoAsync(dbContext, cancellationToken);
            var versiones = await dbContext.VersionesFestival.AsNoTracking()
                .Where(item => item.FestivalOrigenId == festivalId)
                .OrderByDescending(item => item.NumeroVersion)
                .ToListAsync(cancellationToken);

            return Results.Ok(versiones.Select(item => new ResumenVersionFestivalDto(
                item.Id, item.NumeroVersion, item.Nombre, item.FechaInicio, item.FechaFin,
                item.EsVigente, item.EstadoRegistro,
                VersionesFestivalExternosEndpoints.Etiqueta(etiquetas, item.EstadoRegistro))).ToList());
        });

        institucional.MapGet("/perfiles-versionados/{versionId:int}", async (
            int versionId, PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var version = await dbContext.VersionesFestival.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == versionId, cancellationToken);
            if (version is null) return Results.NotFound();
            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == version.FestivalOrigenId, cancellationToken);
            if (festival is null) return Results.NotFound();

            return Results.Ok(await VersionesFestivalExternosEndpoints.ADtoAsync(
                version, festival.Name, dbContext, cancellationToken));
        });

        // ── El borrador de la revisión

        institucional.MapGet("/festivales/{festivalId:int}/revision", async (
            int festivalId, PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();

            var revision = await RevisionVivaAsync(dbContext, festivalId, seguimiento: false, cancellationToken);
            return Results.Ok(await ADtoAsync(dbContext, festival, revision, cancellationToken));
        });

        institucional.MapGet("/festivales/{festivalId:int}/comparacion-envios", async (
            int festivalId, PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            if (!await dbContext.FestivalRecords.AsNoTracking()
                .AnyAsync(item => item.Id == festivalId, cancellationToken))
                return Results.NotFound();

            var comparacion = await EnviosDeRevisionDeFestival.CompararActualAsync(
                dbContext, festivalId, cancellationToken);
            return comparacion is null ? Results.NoContent() : Results.Ok(comparacion);
        });

        institucional.MapPut("/festivales/{festivalId:int}/revision", async (
            int festivalId,
            GuardarRevisionDeCamposDeRegistroSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "El borrador de la revisión no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var revisorId = ObtenerPersonaId(principal);
            if (revisorId is null) return Results.Unauthorized();

            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();

            var revision = await RevisionVivaAsync(dbContext, festivalId, seguimiento: true, cancellationToken);
            if (revision is { Estado: Enviada })
            {
                return Results.Conflict(new
                {
                    message = "Esta solicitud de cambios ya se envió a la organización. Espera a que la atienda para abrir otra.",
                    estado = revision.Estado,
                });
            }

            var (notas, errores) = await NormalizarAsync(dbContext, festivalId, solicitud, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            revision ??= await AbrirRevisionAsync(dbContext, festival, revisorId.Value, ahora, cancellationToken);
            revision.ObservacionGeneral = LimpiarNota(solicitud.ObservacionGeneral);
            revision.FechaActualizacion = ahora;
            await ReemplazarObservacionesAsync(dbContext, revision, notas, ahora, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(await ADtoAsync(dbContext, festival, revision, cancellationToken));
        });

        // ── El envío: es lo que devuelve el Festival a la organización

        institucional.MapPost("/festivales/{festivalId:int}/revision/enviar", async (
            int festivalId,
            GuardarRevisionDeCamposDeRegistroSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "El envío de la solicitud de cambios no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var revisorId = ObtenerPersonaId(principal);
            if (revisorId is null) return Results.Unauthorized();

            var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
            if (festival is null) return Results.NotFound();

            // SOLO SE DEVUELVE LO QUE ESTA EN REVISION. Es la misma condición que exige la ruta de
            // decisiones: un Festival publicado o en borrador no está esperando a nadie, y moverlo
            // a `ajustes_solicitados` desde ahí lo sacaría del sitio donde su organización lo dejó.
            if (!EstadosFestival.Es(festival.StatusCode, EstadosFestival.EnRevision))
            {
                return Results.Conflict(new
                {
                    message = "Solo un Festival en revisión puede recibir una solicitud de cambios.",
                    estado = EstadosFestival.HaciaContrato(festival.StatusCode),
                });
            }

            var (notas, errores) = await NormalizarAsync(dbContext, festivalId, solicitud, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            // AL MENOS UN CAMPO SEÑALADO, y esta es la diferencia con la ruta de decisiones. Aquella
            // pide un párrafo; esta pide señalar qué hay que cambiar. Enviarla vacía dejaría a la
            // organización con un Festival devuelto y ninguna instrucción, que es el caso que este
            // circuito existe para cerrar.
            if (notas.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["observaciones"] = ["Señala al menos un campo antes de enviar la solicitud de cambios."],
                });
            }

            var ahora = DateTime.UtcNow;
            var revision = await RevisionVivaAsync(dbContext, festivalId, seguimiento: true, cancellationToken);
            if (revision is { Estado: Enviada })
            {
                return Results.Conflict(new
                {
                    message = "Esta solicitud de cambios ya se envió a la organización.",
                    estado = revision.Estado,
                });
            }

            revision ??= await AbrirRevisionAsync(dbContext, festival, revisorId.Value, ahora, cancellationToken);
            revision.ObservacionGeneral = LimpiarNota(solicitud.ObservacionGeneral);
            revision.FechaActualizacion = ahora;
            await ReemplazarObservacionesAsync(dbContext, revision, notas, ahora, cancellationToken);

            // QUIEN ENVIA SE SELLA AQUI Y NO AL ABRIR EL BORRADOR. Un borrador puede pasar de un
            // funcionario a otro; quien firma la devolución es quien pulsa enviar.
            revision.IdUsuarioRevisor = revisorId.Value;
            revision.RevisorNombre = await NombreDeUsuarioAsync(dbContext, revisorId.Value, cancellationToken);
            revision.Estado = Enviada;
            revision.FechaEnvio = ahora;

            var estadoAnterior = festival.StatusCode;
            festival.StatusCode = EstadosFestival.AjustesSolicitados;
            festival.UpdatedAt = ahora;

            await RegistrarHistorialAsync(dbContext, festival, revision, notas, revisorId.Value, estadoAnterior, ahora, cancellationToken);
            await NotificarAsync(dbContext, festival, revision, notas.Count, ahora, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(await ADtoAsync(dbContext, festival, revision, cancellationToken));
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // EL LADO DE LA ORGANIZACION
    // ═══════════════════════════════════════════════════════════════════════════════════════

    private static void MapLadoDeLaOrganizacion(RouteGroupBuilder group)
    {
        var externo = group.MapGroup("/externo").WithTags("revision-por-campos");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        externo.MapGet("/festivales/{festivalId:int}/cambios-pedidos", async (
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
            if (festival.OrganizacionPrincipalId is not int organizacionId
                || !await RespondePorLaOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken))
                return Results.Forbid();

            // SOLO LAS ENVIADAS. Un borrador es trabajo del funcionario a medias: enseñarlo sería
            // pedirle a la organización que corrija sobre notas que todavía se están escribiendo.
            var revision = await dbContext.RevisionesDeRegistro.AsNoTracking()
                .Where(item => item.ModuloId == Modulo
                    && item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture)
                    && item.Estado == Enviada)
                .FirstOrDefaultAsync(cancellationToken);

            return Results.Ok(await ADtoAsync(dbContext, festival, revision, cancellationToken));
        });

        externo.MapPost("/cambios-pedidos/{observacionId:long}/atender", async (
            long observacionId,
            AtenderCambioSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La marca no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();

            var observacion = await dbContext.RevisionesDeRegistroObservaciones
                .FirstOrDefaultAsync(item => item.Id == observacionId, cancellationToken);
            if (observacion is null) return Results.NotFound();

            var revision = await dbContext.RevisionesDeRegistro.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == observacion.IdRevision, cancellationToken);
            if (revision is null) return Results.NotFound();
            if (revision.Estado != Enviada)
            {
                return Results.Conflict(new
                {
                    message = "Esta solicitud de cambios ya no está abierta.",
                    estado = revision.Estado,
                });
            }

            // EL REGISTRO ES TEXTO: si no es un entero, esa revisión no es de un Festival y no
            // hay ficha que abrir. Contestar 404 es más honesto que reventar al convertir.
            if (!int.TryParse(revision.RegistroId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var festivalDeLaRevision))
            {
                return Results.NotFound();
            }

            var festival = await dbContext.FestivalRecords.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == festivalDeLaRevision, cancellationToken);
            if (festival is null) return Results.NotFound();
            if (festival.OrganizacionPrincipalId is not int organizacionId
                || !await RespondePorLaOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken))
                return Results.Forbid();

            var ahora = DateTime.UtcNow;
            var atendida = solicitud.Atendida;
            observacion.Estado = atendida ? Atendida : Pendiente;
            observacion.FechaAtencion = atendida ? ahora : null;
            observacion.IdUsuarioAtiende = atendida ? personaId.Value : null;
            observacion.FechaActualizacion = ahora;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(ADto(observacion));
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // LO QUE LLAMA EL RESTO DEL CIRCUITO
    // ═══════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cierra la solicitud de cambios cuando la organización vuelve a mandar el Festival a revisión.
    /// </summary>
    /// <remarks>
    /// <para>
    /// POR QUE AQUI Y NO EN LA RUTA DE ENVIO. Porque es una regla de este circuito y no del envío a
    /// revisión: lo que sabe la ruta de envío es que el Festival cambió de estado. Si mañana hay un
    /// segundo camino que devuelva el Festival —por ejemplo un cierre administrativo—, tendrá que
    /// llamar aquí también, y el sitio donde buscarlo es este fichero.
    /// </para>
    /// <para>
    /// LAS NOTAS SIN MARCAR QUEDAN COMO ESTAN. No se marcan como atendidas al cerrar: el expediente
    /// tiene que poder decir que dos de los siete puntos nadie los dio por resueltos. Falsificar
    /// eso con un UPDATE masivo convertiría el historial en una lista de éxitos.
    /// </para>
    /// <para>
    /// NO LLAMA A <c>SaveChangesAsync</c>: quien la invoca ya está componiendo una transacción con
    /// el cambio de estado, el historial, la auditoría y la notificación. Guardar aquí partiría esa
    /// unidad en dos y dejaría el Festival en revisión con su solicitud todavía abierta.
    /// </para>
    /// </remarks>
    internal static async Task CerrarSolicitudViva(
        PnmcDbContext dbContext, int festivalId, DateTime ahora, CancellationToken cancellationToken)
    {
        var revision = await dbContext.RevisionesDeRegistro
            .FirstOrDefaultAsync(item => item.ModuloId == Modulo
                && item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture)
                && item.Estado != Cerrada, cancellationToken);
        if (revision is null) return;

        revision.Estado = Cerrada;
        revision.FechaCierre = ahora;
        revision.FechaActualizacion = ahora;
    }

    /// <summary>Cuántos cambios quedan pendientes en la solicitud enviada de un Festival.</summary>
    /// <remarks>
    /// CERO SIGNIFICA DOS COSAS DISTINTAS —no hay solicitud, o está toda atendida— y da igual: la
    /// tarjeta del panel usa este número para decidir si pinta el aviso, y en los dos casos no lo
    /// pinta.
    /// </remarks>
    internal static async Task<Dictionary<int, int>> CambiosPendientesPorFestivalAsync(
        PnmcDbContext dbContext, IReadOnlyCollection<int> festivalIds, CancellationToken cancellationToken)
    {
        if (festivalIds.Count == 0) return new Dictionary<int, int>();

        // EL REGISTRO ES TEXTO EN LA TABLA GENERICA, así que la pertenencia se pregunta sobre los
        // identificadores ya convertidos y el resultado se devuelve a entero al final.
        var registros = festivalIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
        return await dbContext.RevisionesDeRegistro.AsNoTracking()
            .Where(revision => revision.ModuloId == Modulo && revision.Estado == Enviada
                && registros.Contains(revision.RegistroId))
            .Join(dbContext.RevisionesDeRegistroObservaciones.AsNoTracking().Where(nota => nota.Estado == Pendiente),
                revision => revision.Id, nota => nota.IdRevision,
                (revision, _) => revision.RegistroId)
            .GroupBy(registroId => registroId)
            .Select(grupo => new { Registro = grupo.Key, Cuantos = grupo.Count() })
            .ToDictionaryAsync(
                item => int.Parse(item.Registro, NumberStyles.Integer, CultureInfo.InvariantCulture),
                item => item.Cuantos,
                cancellationToken);
    }

    /// <summary>Cuántos cambios quedan pendientes en la solicitud enviada de UN Festival.</summary>
    /// <remarks>
    /// <para>
    /// Es la puerta de <c>POST /externo/festivales/{id}/enviar-a-revision</c>: mientras devuelva un
    /// número mayor que cero, el Festival no vuelve a la bandeja del funcionario. La regla es de este
    /// circuito y por eso se escribe aquí, junto a <see cref="CerrarSolicitudViva"/>, y no en la ruta
    /// de envío.
    /// </para>
    /// <para>
    /// DOS CONSULTAS Y NO UNA JOIN. La de arriba resuelve cuarenta Festivales de un golpe y por eso
    /// paga la complejidad; esta responde por uno solo, en la ruta de envío, donde una consulta más
    /// no se nota y una condición legible sí.
    /// </para>
    /// <para>
    /// SOLO CUENTA LA SOLICITUD <c>Enviada</c>. Un borrador que el funcionario aún no ha mandado no
    /// puede bloquear a nadie: la organización ni siquiera sabe que existe.
    /// </para>
    /// </remarks>
    internal static async Task<int> CuantosCambiosPendientesAsync(
        PnmcDbContext dbContext, int festivalId, CancellationToken cancellationToken)
    {
        var revision = await dbContext.RevisionesDeRegistro.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ModuloId == Modulo
                && item.RegistroId == festivalId.ToString(CultureInfo.InvariantCulture)
                && item.Estado == Enviada, cancellationToken);
        if (revision is null) return 0;

        return await dbContext.RevisionesDeRegistroObservaciones.AsNoTracking()
            .CountAsync(nota => nota.IdRevision == revision.Id && nota.Estado == Pendiente, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // LAS PIEZAS
    // ═══════════════════════════════════════════════════════════════════════════════════════

    private static Task<RevisionDeRegistroRow?> RevisionVivaAsync(
        PnmcDbContext dbContext, int festivalId, bool seguimiento, CancellationToken cancellationToken)
    {
        var consulta = seguimiento ? dbContext.RevisionesDeRegistro : dbContext.RevisionesDeRegistro.AsNoTracking();
        var registroId = festivalId.ToString(CultureInfo.InvariantCulture);
        return consulta.FirstOrDefaultAsync(
            item => item.ModuloId == Modulo && item.RegistroId == registroId && item.Estado != Cerrada,
            cancellationToken);
    }

    /// <summary>
    /// Abre la revisión y sella en ella las dos puntas de la conversación.
    /// </summary>
    /// <remarks>
    /// A QUIEN SE LE DEVUELVE: a quien mandó el Festival a revisión, buscado en
    /// <c>RegistrosRevisionHistorial</c> y no en la bitácora de auditoría. Es la misma fuente que ya
    /// usa <c>CrearNotificacionDecisionAsync</c>, y por el mismo motivo: el verbo funcional
    /// <c>FestivalEnviadoARevision</c> no está entre los trece que admite
    /// <c>CK_BitacoraAuditoria_Accion</c>, así que contra SQL Server esa fila no llega a escribirse
    /// nunca y la consulta devuelve vacío.
    /// </remarks>
    private static async Task<RevisionDeRegistroRow> AbrirRevisionAsync(
        PnmcDbContext dbContext, FestivalRow festival, int revisorId, DateTime ahora, CancellationToken cancellationToken)
    {
        var registroId = festival.Id.ToString(CultureInfo.InvariantCulture);
        var destinatarioId = await dbContext.HistorialesRevisionRegistros.AsNoTracking()
            .Where(item => item.ModuloId == Modulos.Festivales && item.RegistroId == registroId
                && item.Accion == "FestivalEnviadoARevision")
            .OrderByDescending(item => item.Fecha)
            .Select(item => (int?)item.UsuarioId)
            .FirstOrDefaultAsync(cancellationToken);

        var revision = new RevisionDeRegistroRow
        {
            ModuloId = Modulo,
            RegistroId = registroId,
            Estado = Borrador,
            IdUsuarioRevisor = revisorId,
            RevisorNombre = await NombreDeUsuarioAsync(dbContext, revisorId, cancellationToken),
            IdUsuarioDestinatario = destinatarioId,
            DestinatarioNombre = destinatarioId is int id ? await NombreDeUsuarioAsync(dbContext, id, cancellationToken) : null,
            IdOrganizacion = festival.OrganizacionPrincipalId,
            OrganizacionNombre = festival.OrganizacionPrincipalId is int organizacionId
                ? await dbContext.EntityProfiles.AsNoTracking().Where(item => item.Id == organizacionId)
                    .Select(item => item.Name).FirstOrDefaultAsync(cancellationToken)
                : null,
            FechaCreacion = ahora,
        };
        dbContext.RevisionesDeRegistro.Add(revision);
        return revision;
    }

    /// <summary>
    /// Lee la lista que llega del formulario y la deja lista para escribir, o devuelve los errores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LAS NOTAS VACIAS SE CAEN AQUI Y NO SON UN ERROR. Vaciar el texto de un campo es como se
    /// retira una nota desde la pantalla: la fila deja de venir en la lista y por tanto se borra.
    /// Contestar 400 obligaría al front a limpiar la lista antes de mandarla, que es una segunda
    /// verdad sobre la misma regla.
    /// </para>
    /// <para>
    /// EL PERFIL VERSIONADO SE COMPRUEBA CONTRA EL FESTIVAL. Sin eso, una nota podría colgarse de un perfil
    /// de OTRO Festival y aparecer en la ficha equivocada: el identificador viene del cliente.
    /// </para>
    /// </remarks>
    private static async Task<(List<RevisionDeRegistroObservacionRow> Notas, Dictionary<string, string[]> Errores)> NormalizarAsync(
        PnmcDbContext dbContext, int festivalId, GuardarRevisionDeCamposDeRegistroSolicitud solicitud, CancellationToken cancellationToken)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var notas = new List<RevisionDeRegistroObservacionRow>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        var perfilesVersionadosDelFestival = await dbContext.VersionesFestival.AsNoTracking()
            .Where(item => item.FestivalOrigenId == festivalId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        foreach (var entrada in solicitud.Observaciones ?? [])
        {
            var nota = LimpiarNota(entrada.Nota);
            if (nota is null) continue;

            var ambito = (entrada.Ambito ?? string.Empty).Trim().ToLowerInvariant();
            if (ambito is not (AmbitoPrincipal or AmbitoSubregistro))
            {
                errores["ambito"] = ["El ámbito de la observación no es válido."];
                continue;
            }

            var campoId = LimpiarClave(entrada.CampoId, 120);
            var seccionId = LimpiarClave(entrada.SeccionId, 80);
            if (campoId is null || seccionId is null)
            {
                errores["campoId"] = ["Cada observación tiene que decir sobre qué campo y en qué sección se pide el cambio."];
                continue;
            }

            if (ambito == AmbitoSubregistro)
            {
                if (!int.TryParse(entrada.SubregistroId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var perfilVersionadoId)
                    || !perfilesVersionadosDelFestival.Contains(perfilVersionadoId))
                {
                    errores["subregistroId"] = ["El perfil versionado señalado no pertenece a este Festival."];
                    continue;
                }
            }
            else if (!string.IsNullOrWhiteSpace(entrada.SubregistroId))
            {
                errores["subregistroId"] = ["Una observación sobre el Festival no puede señalar un perfil versionado."];
                continue;
            }

            var llave = $"{ambito}|{entrada.SubregistroId}|{campoId}";
            if (!vistos.Add(llave))
            {
                errores["campoId"] = ["Hay dos observaciones sobre el mismo campo."];
                continue;
            }

            notas.Add(new RevisionDeRegistroObservacionRow
            {
                Ambito = ambito,
                // EL SUBREGISTRO, COMO TEXTO. La tabla es genérica y no todos los procesos tienen
                // clave entera; el contrato sigue hablando de identificadores y aquí se convierte.
                SubregistroId = ambito == AmbitoSubregistro && entrada.SubregistroId is { } sub
                    ? sub.ToString(CultureInfo.InvariantCulture)
                    : null,
                SeccionId = seccionId,
                CampoId = campoId,
                CampoEtiqueta = LimpiarClave(entrada.CampoEtiqueta, 240) ?? campoId,
                ValorObservado = string.IsNullOrWhiteSpace(entrada.ValorObservado)
                    ? null : ValidationHelpers.SanitizeText(entrada.ValorObservado, 4000),
                Nota = nota,
            });
        }

        return (notas, errores);
    }

    /// <summary>
    /// Deja las notas de la revisión igual a lo que llegó: crea las nuevas, actualiza las que
    /// cambiaron y borra las que ya no vienen.
    /// </summary>
    /// <remarks>
    /// LO QUE NO SE PIERDE AL ACTUALIZAR: la marca de atendida. Si el funcionario reescribe la nota
    /// de un campo que la organización ya había dado por resuelto, el estado vuelve a
    /// <c>pendiente</c> —el texto es otro, así que lo atendido no vale— pero la fila sigue siendo la
    /// misma y conserva su identificador, que es lo que sostiene el enlace desde la pantalla.
    /// </remarks>
    private static async Task ReemplazarObservacionesAsync(
        PnmcDbContext dbContext,
        RevisionDeRegistroRow revision,
        List<RevisionDeRegistroObservacionRow> notas,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        List<RevisionDeRegistroObservacionRow> existentes = revision.Id == 0
            ? []
            : await dbContext.RevisionesDeRegistroObservaciones
                .Where(item => item.IdRevision == revision.Id)
                .ToListAsync(cancellationToken);

        static string Llave(RevisionDeRegistroObservacionRow fila) =>
            $"{fila.Ambito}|{fila.SubregistroId}|{fila.CampoId}";

        var porLlave = existentes.ToDictionary(Llave, StringComparer.Ordinal);
        var conservadas = new HashSet<string>(StringComparer.Ordinal);

        foreach (var nota in notas)
        {
            var llave = Llave(nota);
            conservadas.Add(llave);
            if (porLlave.TryGetValue(llave, out var fila))
            {
                if (fila.Nota == nota.Nota && fila.CampoEtiqueta == nota.CampoEtiqueta
                    && fila.ValorObservado == nota.ValorObservado && fila.SeccionId == nota.SeccionId)
                    continue;

                fila.Nota = nota.Nota;
                fila.CampoEtiqueta = nota.CampoEtiqueta;
                fila.ValorObservado = nota.ValorObservado;
                fila.SeccionId = nota.SeccionId;
                fila.Estado = Pendiente;
                fila.FechaAtencion = null;
                fila.IdUsuarioAtiende = null;
                fila.FechaActualizacion = ahora;
                continue;
            }

            nota.Revision = revision;
            nota.FechaCreacion = ahora;
            dbContext.RevisionesDeRegistroObservaciones.Add(nota);
        }

        foreach (var fila in existentes.Where(item => !conservadas.Contains(Llave(item))))
            dbContext.RevisionesDeRegistroObservaciones.Remove(fila);
    }

    /// <summary>
    /// El expediente: una fila en el historial de revisión, con el JSON de lo pedido dentro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AQUI ES DONDE <c>CamposObservados</c> DEJA DE ESTAR VACIA. La columna existe desde el 25 de
    /// mayo de 2026 y ninguna pantalla la llenaba; el modelo preveía la revisión por campos y no
    /// llegó a existir. Ahora el historial que ya consulta la bandeja lleva dentro qué se pidió,
    /// sin cambiar de forma para nadie que lo lea hoy.
    /// </para>
    /// <para>
    /// EL COMENTARIO SIGUE SIENDO TEXTO PLANO, y no es redundancia. Es lo que lee la organización en
    /// la tarjeta del Festival y lo que sale en el aviso; un JSON en su sitio se vería tal cual en
    /// pantalla. La instantánea estructurada va aparte, en su columna.
    /// </para>
    /// </remarks>
    private static async Task RegistrarHistorialAsync(
        PnmcDbContext dbContext,
        FestivalRow festival,
        RevisionDeRegistroRow revision,
        List<RevisionDeRegistroObservacionRow> notas,
        int revisorId,
        string? estadoAnterior,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        var registroId = festival.Id.ToString(CultureInfo.InvariantCulture);
        var resumen = ResumenParaLaOrganizacion(revision, notas);

        var fila = new HistorialRevisionRegistroRow
        {
            ModuloId = Modulos.Festivales,
            RegistroId = registroId,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = EstadosFestival.AjustesSolicitados,
            Accion = EventoCambiosPedidos,
            Comentario = resumen,
            UsuarioId = revisorId,
            Fecha = ahora,
            CamposObservados = JsonSerializer.Serialize(notas.Select(nota => new
            {
                ambito = nota.Ambito,
                subregistroId = nota.SubregistroId,
                seccionId = nota.SeccionId,
                campoId = nota.CampoId,
                campoEtiqueta = nota.CampoEtiqueta,
                valorObservado = nota.ValorObservado,
                nota = nota.Nota,
            })),
            MetadataJson = JsonSerializer.Serialize(new
            {
                revisionId = revision.Id,
                cuantosCampos = notas.Count,
                destinatarioId = revision.IdUsuarioDestinatario,
                destinatarioNombre = revision.DestinatarioNombre,
                organizacionPrincipalId = festival.OrganizacionPrincipalId,
            }),
        };
        await InstantaneaDelHistorial.TomarAsync(
            dbContext, fila, festival.OrganizacionPrincipalId, revisorId, cancellationToken);
        dbContext.HistorialesRevisionRegistros.Add(fila);

        dbContext.AuditLogs.Add(new AuditLogRow
        {
            UserId = revisorId,
            TableName = "Festivales",
            RecordId = registroId,
            // EL VERBO TECNICO Y NO EL EVENTO: `CK_BitacoraAuditoria_Accion` solo admite trece, y
            // `FestivalCambiosPedidosPorCampo` no es uno. El evento viaja dentro de ValoresNuevos.
            Action = AccionesAuditoria.Actualizar,
            PreviousValuesJson = JsonSerializer.Serialize(new { Estado = estadoAnterior }),
            NewValuesJson = JsonSerializer.Serialize(new
            {
                Estado = EstadosFestival.AjustesSolicitados,
                Evento = EventoCambiosPedidos,
                CuantosCampos = notas.Count,
            }),
            CreatedAt = ahora,
        });
    }

    /// <summary>El aviso que le llega a quien tiene que corregir.</summary>
    /// <remarks>
    /// SIN DESTINATARIO NO HAY AVISO, y se sale en silencio. Es lo mismo que ya hace
    /// <c>CrearNotificacionDecisionAsync</c>: escribir la notificación a nombre de nadie llenaría
    /// <c>dbo.Notificaciones</c> de filas que ninguna bandeja puede mostrar.
    /// </remarks>
    private static async Task NotificarAsync(
        PnmcDbContext dbContext,
        FestivalRow festival,
        RevisionDeRegistroRow revision,
        int cuantosCampos,
        DateTime ahora,
        CancellationToken cancellationToken)
    {
        if (revision.IdUsuarioDestinatario is not int destinatarioId) return;
        var persona = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == destinatarioId && item.IsActive, cancellationToken);
        if (persona is null) return;

        var cuerpo = cuantosCampos == 1
            ? $"El equipo del PNMC sugirió un ajuste en tu Festival “{festival.Name}”. Ábrelo para revisarlo."
            : $"El equipo del PNMC sugirió ajustes en {cuantosCampos} campos de tu Festival “{festival.Name}”. Ábrelo para revisarlos uno a uno.";

        dbContext.Notifications.Add(new NotificationRow
        {
            RecipientUserId = persona.Id,
            RecipientEmail = persona.Email,
            EventType = EventoCambiosPedidos,
            Channel = "internal",
            Title = "Sugerencias de ajuste en tu Festival",
            Body = cuerpo,
            Status = "enviada",
            ModuloId = Modulos.Festivales,
            RecordId = festival.Id.ToString(CultureInfo.InvariantCulture),
            MetadataJson = JsonSerializer.Serialize(new
            {
                Estado = EstadosFestival.AjustesSolicitados,
                RevisionId = revision.Id,
                CuantosCampos = cuantosCampos,
            }),
            CreatedAt = ahora,
            SentAt = ahora,
            Attempts = 0,
        });
    }

    /// <summary>
    /// El párrafo que ve la organización en su tarjeta, armado desde las notas.
    /// </summary>
    /// <remarks>
    /// SE RECORTA A LOS PRIMEROS ROTULOS. `Comentario` es nvarchar(2400) y cuarenta y ocho notas no
    /// caben; la lista entera está a un clic, en la ficha. Lo que este texto tiene que hacer es
    /// decir cuántos son y dónde mirar.
    /// </remarks>
    private static string ResumenParaLaOrganizacion(RevisionDeRegistroRow revision, List<RevisionDeRegistroObservacionRow> notas)
    {
        var rotulos = notas.Select(nota => nota.CampoEtiqueta).Take(6).ToList();
        var lista = string.Join(", ", rotulos);
        if (notas.Count > rotulos.Count) lista += $" y {notas.Count - rotulos.Count} más";

        var texto = notas.Count == 1
            ? $"Se sugiere un ajuste en: {lista}."
            : $"Se sugieren ajustes en {notas.Count} campos: {lista}.";
        if (!string.IsNullOrWhiteSpace(revision.ObservacionGeneral))
            texto += $" {revision.ObservacionGeneral}";

        return texto.Length <= 2400 ? texto : texto[..2400];
    }

    private static async Task<RevisionDeCamposDeRegistroDto> ADtoAsync(
        PnmcDbContext dbContext, FestivalRow festival, RevisionDeRegistroRow? revision, CancellationToken cancellationToken)
    {
        if (revision is null)
        {
            return new RevisionDeCamposDeRegistroDto(
                0, Modulo, festival.Id.ToString(CultureInfo.InvariantCulture), festival.Name, Borrador,
                null, null, null, null, null, null, []);
        }

        // POR ID Y NO POR LA COLECCION DE NAVEGACION: en el PUT las filas recien anadidas todavia no
        // tienen identificador, asi que se consulta despues de guardar. Aqui basta con leer.
        List<RevisionDeRegistroObservacionRow> observaciones = revision.Id == 0
            ? []
            : await dbContext.RevisionesDeRegistroObservaciones.AsNoTracking()
                .Where(item => item.IdRevision == revision.Id)
                .OrderBy(item => item.SeccionId).ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);

        return new RevisionDeCamposDeRegistroDto(
            revision.Id,
            Modulo,
            festival.Id.ToString(CultureInfo.InvariantCulture),
            festival.Name,
            revision.Estado,
            revision.ObservacionGeneral,
            revision.RevisorNombre,
            revision.DestinatarioNombre,
            revision.OrganizacionNombre,
            revision.FechaActualizacion,
            revision.FechaEnvio,
            observaciones.Select(ADto).ToList());
    }

    private static ObservacionDeCampoDeRegistroDto ADto(RevisionDeRegistroObservacionRow fila) => new(
        fila.Id, fila.Ambito, fila.SubregistroId, fila.SeccionId, fila.CampoId,
        fila.CampoEtiqueta, fila.ValorObservado, fila.Nota, fila.Estado, fila.FechaAtencion);

    private static Task<string?> NombreDeUsuarioAsync(PnmcDbContext dbContext, int usuarioId, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().Where(item => item.Id == usuarioId)
            .Select(item => item.FullName).FirstOrDefaultAsync(cancellationToken);

    private static Task<bool> RespondePorLaOrganizacionAsync(
        PnmcDbContext dbContext, int personaId, int organizacionId, CancellationToken cancellationToken) =>
    // LA REGLA VIVE EN `AdministracionDeOrganizacion`. Esta copia NO comprobaba si la organización
    // sigue activa: una organización dada de baja seguía pudiendo actuar por aquí.
        AdministracionDeOrganizacion.PuedeAdministrarAsync(dbContext, personaId, organizacionId, cancellationToken);

    private static string? LimpiarNota(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : ValidationHelpers.SanitizeText(valor, 2400);

    private static string? LimpiarClave(string? valor, int largo) =>
        string.IsNullOrWhiteSpace(valor) ? null : ValidationHelpers.SanitizeText(valor, largo);

    private static int? ObtenerPersonaId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var personaId)
            ? personaId : null;
}
