using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Las ediciones de un Festival, administradas por la organizacion que lo tiene a su cargo.
/// </summary>
/// <remarks>
/// <para>
/// QUE ES UNA EDICION AQUI: 2024, 2025, 2026, cada una con su nombre, sus fechas y su estado. Lo
/// pidio el usuario sobre el formulario de registro del Festival.
/// </para>
/// <para>
/// NO ES <c>dbo.VersionesFestival</c> Y LA DISTINCION IMPORTA. Aquella tabla es la version del
/// REGISTRO PUBLICADO: la escribe la aprobacion de una propuesta de cambios, lleva
/// <c>EsVigente</c>, y <c>LecturaFestivalesPublicados</c> lee esa fila para SUSTITUIR nombre,
/// descripcion, cobertura, practicas y territorios de la ficha publica. Escribir ahi las ediciones
/// por anyo habria cambiado lo que ve cualquier visitante del sitio. La medida completa esta en
/// <c>pnmc-database/schema/V20260827_01__ediciones_festival.sql</c>.
/// </para>
/// <para>
/// EL ESTADO OPERATIVO Y EL ESTADO DE REGISTRO NO SON LO MISMO. Una edición puede estar programada
/// y todavía ser un borrador institucional; también puede estar publicada y después realizada. El
/// primero vive en <c>Estado</c>; el segundo reutiliza <c>EstadosContenido</c>, sin crear otro
/// catálogo ni llamar «editorial» a un proceso que no lo es.
/// </para>
/// </remarks>
public static class EdicionesFestivalExternosEndpoints
{
    /// <summary>El vocabulario cerrado, el mismo que impone <c>CK_EdicionesFestival_Estado</c>.</summary>
    /// <remarks>
    /// Los dos primeros no se inventaron: son los unicos valores que hoy tiene
    /// <c>Festivales.EstadoVersionAnoActual</c>, medidos con un SELECT DISTINCT contra PNMC_LOCAL el
    /// 27 de agosto de 2026. Los otros dos cierran el ciclo.
    /// </remarks>
    internal static readonly Dictionary<string, string> EstadosDeEdicion = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en_preparacion"] = "En preparación",
        ["programada"] = "Programada",
        ["realizada"] = "Realizada",
        ["cancelada"] = "Cancelada",
    };

    private const string EstadoPorOmision = "en_preparacion";

    public static RouteGroupBuilder MapEdicionesFestivalExternosEndpoints(this RouteGroupBuilder group)
    {
        var externo = group.MapGroup("/externo").WithTags("ediciones-festival");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        externo.MapGet("/catalogos/estados-edicion", () =>
            Results.Ok(EstadosDeEdicion
                .Select(par => new CatalogoEstadoEdicionDto(par.Key, par.Value))
                .ToList()));

        externo.MapGet("/festivales/{festivalId:int}/ediciones", async (
            int festivalId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var acceso = await ResolverAccesoAsync(festivalId, principal, dbContext, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            var ediciones = await dbContext.EdicionesFestival.AsNoTracking()
                .Where(item => item.FestivalId == festivalId)
                // De la mas reciente a la mas antigua: quien administra trabaja sobre la proxima,
                // no sobre la de hace ocho anyos.
                .OrderByDescending(item => item.Anio).ThenByDescending(item => item.NumeroEdicion)
                .ToListAsync(cancellationToken);

            // CUANTAS OBSERVACIONES TIENE CADA UNA, PARA TODA LA LISTA Y EN UNA CONSULTA. Es lo que
            // decide si la acción «Consultar observaciones» existe en esa fila: ofrecerla en una
            // edición que nunca pasó por revisión abre un recuadro vacío, y eso no es una acción.
            var identificadores = ediciones.Select(item => item.Id).ToList();
            // LA REVISION SE NOMBRA POR MODULO Y CLAVE, COMO EN TODO EL PROYECTO. Desde el 18 de
            // septiembre de 2026 las tres revisiones por campos —Festival, Edición y Mercado—
            // comparten tabla, así que aquí la clave de la edición viaja como texto.
            var clavesDeEdicion = identificadores.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
            var observaciones = identificadores.Count == 0
                ? new Dictionary<int, int>()
                : (await dbContext.RevisionesDeRegistro.AsNoTracking()
                    .Where(revision => revision.ModuloId == RevisionDeCamposEdicionFestivalEndpoints.Modulo
                        && clavesDeEdicion.Contains(revision.RegistroId)
                        && revision.Estado == "enviada")
                    .Join(
                        dbContext.RevisionesDeRegistroObservaciones.AsNoTracking(),
                        revision => revision.Id,
                        nota => nota.IdRevision,
                        (revision, nota) => revision.RegistroId)
                    .GroupBy(clave => clave)
                    .Select(grupo => new { Clave = grupo.Key, Total = grupo.Count() })
                    .ToListAsync(cancellationToken))
                    .ToDictionary(fila => int.Parse(fila.Clave, CultureInfo.InvariantCulture), fila => fila.Total);

            // QUIEN SE PUEDE ELIMINAR DE VERDAD, PARA TODA LA LISTA Y EN DOS CONSULTAS.
            //
            // «El borrador registrado por error se elimina; lo que se publicó en algún momento queda
            // archivado», de la dirección de producto. La regla la aplica el
            // borrado; esto es para que la ACCION solo se ofrezca donde funciona —un botón que
            // contesta 409 obliga a descubrir por ensayo y error qué se puede hacer con cada fila—.
            //
            // LA MITAD QUE NO SE VE ES «llegó a publicarse alguna vez»: una edición publicada y
            // después despublicada vuelve a `borrador`, así que el estado de ahora diría que sí.
            // Solo el historial lo contesta.
            var claves = clavesDeEdicion;
            var publicadasAlgunaVez = identificadores.Count == 0
                ? []
                : await dbContext.HistorialesRevisionRegistros.AsNoTracking()
                    .Where(x => x.ModuloId == Modulos.EdicionesDeFestival
                        && claves.Contains(x.RegistroId)
                        && x.Accion == "EdicionPublicadaPorOrganizacion")
                    .Select(x => x.RegistroId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            var conRevision = identificadores.Count == 0
                ? []
                : (await dbContext.RevisionesDeRegistro.AsNoTracking()
                    .Where(x => x.ModuloId == RevisionDeCamposEdicionFestivalEndpoints.Modulo
                        && clavesDeEdicion.Contains(x.RegistroId))
                    .Select(x => x.RegistroId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                    .Select(clave => int.Parse(clave, CultureInfo.InvariantCulture))
                    .ToList();

            return Results.Ok(ediciones
                .Select(item => ADto(
                    item,
                    observaciones.GetValueOrDefault(item.Id),
                    sePuedeEliminar: EsBorrableSegunEstado(item)
                        && !publicadasAlgunaVez.Contains(item.Id.ToString(CultureInfo.InvariantCulture))
                        && !conRevision.Contains(item.Id)))
                .ToList());
        });

        // La lista anterior es deliberadamente breve. Esta lectura trae la estructura anual
        // completa y nunca consulta VersionesFestival: perfil público y edición son capas distintas.
        externo.MapGet("/ediciones/{edicionId:int}", async (
            int edicionId, ClaimsPrincipal principal, PnmcDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var fila = await dbContext.EdicionesFestival.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == edicionId, cancellationToken);
            if (fila is null) return Results.NotFound();
            var acceso = await ResolverAccesoAsync(fila.FestivalId, principal, dbContext, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            var practicas = await dbContext.ValoresAsync<PracticaMusicalDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
            var territorios = await dbContext.ValoresAsync<TerritorioSonoroDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
            var expresiones = await dbContext.ValoresAsync<ExpresionArtisticaDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
            var modalidades = await dbContext.ValoresAsync<ModalidadParticipacionDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
            var ingresos = await dbContext.ValoresAsync<TipoIngresoDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
            var localizaciones = await dbContext.Relacion<LocalizacionDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId)
                .AsNoTracking().ToListAsync(cancellationToken);
            var aliadas = await dbContext.Relacion<EntidadAliadaDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId)
                .AsNoTracking().ToListAsync(cancellationToken);
            var materiales = await dbContext.Relacion<ArchivoDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId)
                .AsNoTracking().OrderBy(x => x.OrdenVisualizacion).ToListAsync(cancellationToken);

            var localizacionesDto = await ProyectarLocalizacionesAsync(localizaciones, dbContext, cancellationToken);
            var naturalezas = await dbContext.NaturalezasEntidad.AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Nombre, cancellationToken);
            var (mes, duracion) = TiempoDeLaEdicion.De(fila.FechaInicio, fila.FechaFin);

            return Results.Ok(new EdicionFestivalDetalleDto(
                ADto(fila), mes, duracion,
                fila.TipologiaFestivalId, fila.OtraTipologia,
                fila.FuenteFinanciacionPrimariaId, fila.OtraFuenteFinanciacionPrimaria,
                fila.FuenteFinanciacionSecundariaId, fila.OtraFuenteFinanciacionSecundaria,
                fila.UsaEstampillaProcultura, fila.PracticasMusicalesQueCongrega,
                fila.OtraModalidadParticipacion, fila.OtraExpresionArtistica,
                practicas, territorios, expresiones, modalidades, ingresos,
                localizacionesDto,
                aliadas.Select(x => new EdicionFestivalEntidadAliadaDto(
                    x.Id.ToString(CultureInfo.InvariantCulture), x.Nombre, x.Correo,
                    x.NaturalezaEntidadId,
                    x.NaturalezaEntidadId is int n ? naturalezas.GetValueOrDefault(n) : null,
                    x.EntidadId?.ToString(CultureInfo.InvariantCulture))).ToList(),
                materiales.Select(x => new EdicionFestivalMaterialDto(
                    x.Id.ToString(CultureInfo.InvariantCulture), x.Url, x.DescripcionArchivo,
                    x.OrdenVisualizacion)).ToList()));
        });

        externo.MapPost("/festivales/{festivalId:int}/ediciones", async (
            int festivalId,
            EdicionFestivalSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "La edición no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var acceso = await ResolverAccesoAsync(festivalId, principal, dbContext, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            // La organización puede preparar ediciones desde el registro del Festival. No se
            // publican solas: la publicación directa exige que el Festival ya esté publicado.

            var errores = Validar(solicitud);
            await ValidarCatalogosAsync(errores, solicitud, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            var fila = new EdicionFestivalRow
            {
                FestivalId = festivalId,
                Anio = solicitud.Anio,
                NumeroEdicion = solicitud.NumeroEdicion,
                Nombre = LimpiarOpcional(solicitud.Nombre),
                Descripcion = LimpiarOpcional(solicitud.Descripcion),
                FechaInicio = LeerFecha(solicitud.FechaInicio),
                FechaFin = LeerFecha(solicitud.FechaFin),
                Director = LimpiarOpcional(solicitud.Director),
                TipologiaFestivalId = solicitud.TipologiaFestivalId,
                OtraTipologia = LimpiarOpcional(solicitud.OtraTipologia),
                FuenteFinanciacionPrimariaId = solicitud.FuenteFinanciacionPrimariaId,
                OtraFuenteFinanciacionPrimaria = LimpiarOpcional(solicitud.OtraFuenteFinanciacionPrimaria),
                FuenteFinanciacionSecundariaId = solicitud.FuenteFinanciacionSecundariaId,
                OtraFuenteFinanciacionSecundaria = LimpiarOpcional(solicitud.OtraFuenteFinanciacionSecundaria),
                UsaEstampillaProcultura = solicitud.UsaEstampillaProcultura,
                PracticasMusicalesQueCongrega = LimpiarOpcional(solicitud.PracticasMusicalesQueCongrega),
                OtraModalidadParticipacion = LimpiarOpcional(solicitud.OtraModalidadParticipacion),
                OtraExpresionArtistica = LimpiarOpcional(solicitud.OtraExpresionArtistica),
                EstadoRegistro = EstadosFestival.Borrador,
                EstadoVisibilidad = "borrador",
                Estado = NormalizarEstado(solicitud.Estado),
                FechaCreacion = ahora,
            };

            // SQL Server tiene reintentos habilitados. La transaccion manual debe formar parte de
            // la unidad que la estrategia puede repetir; abrirla por fuera hace que el primer
            // SaveChanges responda 500 con SqlServerRetryingExecutionStrategy.
            var estrategia = dbContext.Database.CreateExecutionStrategy();
            await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                dbContext.EdicionesFestival.Add(fila);
                await dbContext.SaveChangesAsync(cancellationToken);
                await ReemplazarRelacionesAsync(fila.Id, solicitud, dbContext, cancellationToken);
                await ActualizarResumenDelFestivalAsync(dbContext, festivalId, ahora, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
            });

            return Results.Created($"/api/v1/externo/ediciones/{fila.Id}", ADto(fila));
        });

        externo.MapPut("/ediciones/{edicionId:int}", async (
            int edicionId,
            EdicionFestivalSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "La edición no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var fila = await dbContext.EdicionesFestival.FirstOrDefaultAsync(item => item.Id == edicionId, cancellationToken);
            if (fila is null) return Results.NotFound();

            var acceso = await ResolverAccesoAsync(fila.FestivalId, principal, dbContext, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            if (!EsEditable(fila.EstadoVisibilidad))
            {
                return Results.Conflict(new { message = "Esta edición no puede modificarse directamente en su estado de visibilidad actual.", estadoVisibilidad = fila.EstadoVisibilidad });
            }

            var errores = Validar(solicitud);
            await ValidarCatalogosAsync(errores, solicitud, dbContext, cancellationToken);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            fila.Anio = solicitud.Anio;
            fila.NumeroEdicion = solicitud.NumeroEdicion;
            fila.Nombre = LimpiarOpcional(solicitud.Nombre);
            fila.Descripcion = LimpiarOpcional(solicitud.Descripcion);
            fila.FechaInicio = LeerFecha(solicitud.FechaInicio);
            fila.FechaFin = LeerFecha(solicitud.FechaFin);
            fila.Director = LimpiarOpcional(solicitud.Director);
            fila.TipologiaFestivalId = solicitud.TipologiaFestivalId;
            fila.OtraTipologia = LimpiarOpcional(solicitud.OtraTipologia);
            fila.FuenteFinanciacionPrimariaId = solicitud.FuenteFinanciacionPrimariaId;
            fila.OtraFuenteFinanciacionPrimaria = LimpiarOpcional(solicitud.OtraFuenteFinanciacionPrimaria);
            fila.FuenteFinanciacionSecundariaId = solicitud.FuenteFinanciacionSecundariaId;
            fila.OtraFuenteFinanciacionSecundaria = LimpiarOpcional(solicitud.OtraFuenteFinanciacionSecundaria);
            fila.UsaEstampillaProcultura = solicitud.UsaEstampillaProcultura;
            fila.PracticasMusicalesQueCongrega = LimpiarOpcional(solicitud.PracticasMusicalesQueCongrega);
            fila.OtraModalidadParticipacion = LimpiarOpcional(solicitud.OtraModalidadParticipacion);
            fila.OtraExpresionArtistica = LimpiarOpcional(solicitud.OtraExpresionArtistica);
            fila.Estado = NormalizarEstado(solicitud.Estado);
            fila.FechaActualizacion = ahora;

            var estrategia = dbContext.Database.CreateExecutionStrategy();
            await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                await ReemplazarRelacionesAsync(fila.Id, solicitud, dbContext, cancellationToken);
                await ActualizarResumenDelFestivalAsync(dbContext, fila.FestivalId, ahora, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
            });

            return Results.Ok(ADto(fila));
        });

        // La publicación es directa para la organización administradora, pero solo después de
        // publicar el Festival. La institución supervisa posteriormente; no aprueba cada edición.
        externo.MapPost("/ediciones/{edicionId:int}/publicar", async (
            int edicionId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
                return Results.BadRequest(new { message = "La publicación de la edición no pudo validarse. Actualiza la página e inténtalo nuevamente." });

            var fila = await dbContext.EdicionesFestival.FirstOrDefaultAsync(item => item.Id == edicionId, cancellationToken);
            if (fila is null) return Results.NotFound();

            var acceso = await ResolverAccesoAsync(fila.FestivalId, principal, dbContext, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            if (!EsEditable(fila.EstadoVisibilidad))
                return Results.Conflict(new { message = "Esta edición ya no puede publicarse directamente en su estado de visibilidad actual.", estadoVisibilidad = fila.EstadoVisibilidad });
            if (!EstadosFestival.Es(acceso.Festival!.StatusCode, EstadosFestival.Publicado))
                return Results.Conflict(new { message = "Publica primero el Festival para poder publicar una de sus ediciones.", estadoFestival = EstadosFestival.HaciaContrato(acceso.Festival.StatusCode) });

            // LA MISMA PUERTA QUE EL ENVIO A REVISION: publicar una edición es entregarle algo al
            // Programa, y eso no se hace desde una organización cuyo correo nadie ha comprobado.
            if (acceso.Festival.OrganizacionPrincipalId is int organizacionDelFestival
                && await AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync(
                    dbContext, organizacionDelFestival,
                    "publicar ediciones", cancellationToken) is { } sinConfirmar)
            {
                return sinConfirmar;
            }

            var personaId = ObtenerPersonaId(principal);
            if (personaId is null) return Results.Unauthorized();

            // El cambio de estado no basta: sin estos dos rastros, la bandeja institucional no
            // puede atribuir el envío ni una decisión posterior puede avisar a quien lo hizo.
            // Auditoría guarda el verbo técnico; el historial, el evento funcional.
            var estadoAnterior = fila.EstadoVisibilidad;
            var ahora = DateTime.UtcNow;
            // EstadoRegistro se conserva únicamente por la FK histórica de EstadosContenido;
            // el contrato y el flujo de Edición usan EstadoVisibilidad.
            fila.EstadoRegistro = EstadosFestival.Publicado;
            fila.EstadoVisibilidad = "publicada";
            fila.FechaActualizacion = ahora;
            var historial = new HistorialRevisionRegistroRow
            {
                ModuloId = Modulos.EdicionesDeFestival,
                RegistroId = fila.Id.ToString(CultureInfo.InvariantCulture),
                // EN EL VOCABULARIO DEL HISTORIAL, que es institucional y no el de la Edición. Ver
                // HaciaElHistorial: escribir «publicada» aquí devolvía 500 contra SQL Server.
                EstadoAnterior = HaciaElHistorial(estadoAnterior),
                EstadoNuevo = HaciaElHistorial("publicada"),
                Accion = "EdicionPublicadaPorOrganizacion",
                UsuarioId = personaId.Value,
                Fecha = ahora,
                MetadataJson = $"{{\"FestivalId\":{fila.FestivalId},\"Anio\":{fila.Anio},\"NumeroEdicion\":{fila.NumeroEdicion}}}",
            };
            await InstantaneaDelHistorial.TomarAsync(dbContext, historial, acceso.Festival!.OrganizacionPrincipalId, personaId.Value, cancellationToken);
            dbContext.HistorialesRevisionRegistros.Add(historial);
            dbContext.AuditLogs.Add(new AuditLogRow
            {
                UserId = personaId.Value,
                TableName = "EdicionesFestival",
                RecordId = fila.Id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.Actualizar,
                PreviousValuesJson = $"{{\"EstadoVisibilidad\":\"{estadoAnterior}\"}}",
                NewValuesJson = $"{{\"EstadoVisibilidad\":\"publicada\",\"Evento\":\"EdicionPublicadaPorOrganizacion\",\"FestivalId\":{fila.FestivalId}}}",
                CreatedAt = ahora,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(ADto(fila));
        });

        // ---------- Despublicar y archivar ----------------------------------------------------
        //
        // LAS DOS QUE FALTABAN, Y LA BASE YA LAS ADMITIA. `CK_EdicionesFestival_EstadoVisibilidad`
        // acepta `borrador`, `publicada` y `archivada` desde `V20260907_02`, pero no había forma de
        // llegar a `archivada` ni de volver de `publicada`: una edición publicada por error se
        // quedaba publicada, y la única salida era eliminarla, que es otra cosa.
        //
        // DESPUBLICAR NO ES ARCHIVAR, igual que en Agenda, Noticias y Catálogo editorial: retirar
        // del portal algo que hay que seguir editando devuelve a borrador; archivar cierra su ciclo.
        externo.MapPost("/ediciones/{edicionId:int}/despublicar", async (
            int edicionId, ClaimsPrincipal principal, PnmcDbContext dbContext,
            IAntiforgery antiforgery, HttpContext httpContext, CancellationToken cancellationToken) =>
            await CambiarVisibilidadAsync(
                edicionId, principal, dbContext, antiforgery, httpContext,
                desde: "publicada", hacia: "borrador",
                evento: "EdicionDespublicadaPorOrganizacion",
                siNoSePuede: "Solo una edición publicada puede despublicarse.",
                cancellationToken));

        externo.MapPost("/ediciones/{edicionId:int}/archivar", async (
            int edicionId, ClaimsPrincipal principal, PnmcDbContext dbContext,
            IAntiforgery antiforgery, HttpContext httpContext, CancellationToken cancellationToken) =>
            await CambiarVisibilidadAsync(
                edicionId, principal, dbContext, antiforgery, httpContext,
                desde: null, hacia: "archivada",
                evento: "EdicionArchivadaPorOrganizacion",
                siNoSePuede: "Esta edición ya está archivada.",
                cancellationToken));

        externo.MapDelete("/ediciones/{edicionId:int}", async (
            int edicionId,
            ClaimsPrincipal principal,
            PnmcDbContext dbContext,
            IAntiforgery antiforgery,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            {
                return Results.BadRequest(new { message = "La edición no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var fila = await dbContext.EdicionesFestival.FirstOrDefaultAsync(item => item.Id == edicionId, cancellationToken);
            if (fila is null) return Results.NotFound();

            var acceso = await ResolverAccesoAsync(fila.FestivalId, principal, dbContext, cancellationToken);
            if (acceso.Resultado is not null) return acceso.Resultado;

            var edicionIdTexto = edicionId.ToString(CultureInfo.InvariantCulture);

            // El estado de una edición describe la realización (preparación, programada,
            // realizada o cancelada), no su estado editorial. Una edición programada todavía es
            // privada y la organización debe poder retirarla; negar esa operación impedía borrar
            // el borrador de la edición del año en curso. Las realizaciones ya ocurridas o
            // canceladas conservan trazabilidad y pasan por la ruta institucional.
            if (!EsBorrableSegunEstado(fila))
            {
                return Results.Conflict(new { message = "Solo una edición en preparación o programada puede eliminarse directamente. Las ediciones realizadas o canceladas deben gestionarse mediante el flujo institucional." });
            }

            // LO QUE SE PUBLICO NO SE BORRA, SE ARCHIVA. La guarda de arriba mira `Estado` —el eje
            // de la REALIZACION— y no miraba `EstadoVisibilidad`, que es el eje EDITORIAL: son dos
            // ejes distintos y solo uno estaba vigilado. Medido contra PNMC_LOCAL el 13 de
            // septiembre de 2026 recorriendo el circuito entero: una edición publicada, luego
            // despublicada y archivada seguia teniendo `Estado = 'en_preparacion'`, asi que el
            // borrado la aceptaba —204— y se llevaba por delante un registro que el portal habia
            // mostrado. Quedaron TRES filas de `dbo.RegistrosRevisionHistorial` apuntando a un
            // `RegistroId` que ya no existe: el rastro de las transiciones sobrevivia y el registro
            // del que hablaban, no. A la pregunta «que fue la edicion 1004» ya no habia respuesta.
            //
            // SE PREGUNTA POR EL HECHO Y NO POR EL ESTADO DE AHORA. «Esta publicada» lo contesta
            // `EstadoVisibilidad`; «llego a publicarse alguna vez» solo lo contesta el historial, y
            // es la pregunta que importa: una edicion despublicada tiene `EstadoVisibilidad =
            // 'borrador'` y ya la vio cualquiera.
            //
            // ARCHIVAR ES EL CAMINO, y existe: `POST /externo/ediciones/{id}/archivar` la retira de
            // la vista publica conservando el registro y su historial.
            var estuvoPublicada = string.Equals(fila.EstadoVisibilidad, "publicada", StringComparison.OrdinalIgnoreCase)
                || await dbContext.HistorialesRevisionRegistros.AsNoTracking().AnyAsync(
                    x => x.ModuloId == Modulos.EdicionesDeFestival
                        && x.RegistroId == edicionIdTexto
                        && x.Accion == "EdicionPublicadaPorOrganizacion",
                    cancellationToken);
            if (estuvoPublicada)
            {
                // EL AVISO DICE QUE HACER, Y NO LO MISMO EN LOS DOS CASOS. «Archívala» a una edición
                // que YA está archivada manda a repetir algo hecho, que es la forma más rápida de
                // que un aviso correcto se lea como un fallo.
                var yaArchivada = string.Equals(fila.EstadoVisibilidad, "archivada", StringComparison.OrdinalIgnoreCase);
                return Results.Conflict(new
                {
                    message = yaArchivada
                        ? "Esta edición ya estuvo publicada, así que no se elimina: el público la vio y su historial forma parte del expediente. Ya está archivada, es decir fuera de la vista pública, y se conserva como registro."
                        : "Esta edición ya estuvo publicada y por eso no se elimina: el público la vio y su historial forma parte del expediente. Archívala para retirarla de la vista pública.",
                    estadoVisibilidad = fila.EstadoVisibilidad,
                });
            }

            // UNA EDICION QUE PASO POR REVISION NO SE BORRA, SE ARCHIVA. Sus observaciones son el
            // expediente de lo que el PNMC pidio y de lo que la organizacion atendio; borrarlas
            // para que la fila de la edicion pueda desaparecer cambia la pregunta «quiero retirar
            // este borrador» por «quiero borrar el historial de una revision», que no es lo mismo
            // y no es de este boton. Se contesta con un 409 que dice por donde va.
            var claveDeLaEdicion = RevisionDeCamposEdicionFestivalEndpoints.Clave(edicionId);
            if (await dbContext.RevisionesDeRegistro.AsNoTracking()
                    .AnyAsync(x => x.ModuloId == RevisionDeCamposEdicionFestivalEndpoints.Modulo
                        && x.RegistroId == claveDeLaEdicion, cancellationToken))
            {
                return Results.Conflict(new { message = "Esta edición tiene una revisión registrada y por eso no se elimina: su historial de observaciones forma parte del expediente. Gestiónala mediante el flujo institucional." });
            }

            var festivalId = fila.FestivalId;

            // LAS OCHO TABLAS HIJAS PRIMERO, Y EN SU PROPIO `SaveChanges`. Las dos cosas hacen
            // falta, y la segunda es la que no se ve venir:
            //
            //   · EL BORRADO NO LAS TOCABA. Quitaba la fila de la edicion y nada mas, asi que en
            //     cuanto la edicion tenia UN municipio la clave foranea paraba el borrado y la
            //     peticion moria con un 500. Medido contra PNMC_LOCAL.
            //
            //   · Y MARCARLAS PARA BORRAR EN LA MISMA TANDA NO BASTABA. Se probo, y volvio a dar
            //     500 con el mismo mensaje. El motivo es que el modelo NO DECLARA LA RELACION: en
            //     `PnmcDbContext`, `EdicionFestivalId` es una columna `int` corriente, sin
            //     `HasOne`/`HasForeignKey`. Sin relacion no hay grafo de dependencias, y EF ordena
            //     las ordenes como quiere: podia mandar el DELETE del padre antes que el de las
            //     hijas. La base si sabe que hay una foranea, y la rechazaba.
            //
            // ES OTRA VEZ EL MISMO DEFECTO DE FONDO: el modelo no sabe lo que la base sabe.
            // Declarar las ocho relaciones seria el arreglo de raiz y cambiaria como se generan
            // consultas y como se construye el esquema del arnes de SQLite; queda nombrado y no se
            // hace aqui. Lo de aqui es explicito: primero se van las hijas, y solo cuando ya se
            // fueron se quita el padre.
            //
            // LAS DOS TANDAS VAN EN UNA TRANSACCION. Sin ella, un fallo en la segunda dejaria la
            // edicion viva y sin ninguno de sus municipios, aliadas ni materiales: peor que el 500,
            // porque no se nota.
            var estrategiaDeBorrado = dbContext.Database.CreateExecutionStrategy();
            await estrategiaDeBorrado.ExecuteAsync(async () =>
            {
                await using var transaccion = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                await RetirarRelacionesAsync(edicionId, dbContext, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);

                dbContext.EdicionesFestival.Remove(fila);
                await ActualizarResumenDelFestivalAsync(dbContext, festivalId, DateTime.UtcNow, cancellationToken, edicionRetirada: edicionId);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
            });

            return Results.NoContent();
        });

        return group;
    }

    /// <summary>
    /// Comprueba de una vez que hay sesion, que el Festival existe y que quien pide lo administra.
    /// </summary>
    /// <remarks>
    /// Las cuatro rutas hacen la misma comprobacion y en el mismo orden. Repetirla cuatro veces es
    /// exactamente como se cuela una: basta con olvidar la tercera linea en una de ellas para que
    /// una organizacion administre las ediciones de un Festival ajeno, y nada en pantalla lo diria.
    /// </remarks>
    private static async Task<(IResult? Resultado, FestivalRow? Festival)> ResolverAccesoAsync(
        int festivalId,
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var personaId = ObtenerPersonaId(principal);
        if (personaId is null) return (Results.Unauthorized(), null);

        var festival = await dbContext.FestivalRecords.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
        if (festival is null) return (Results.NotFound(), null);

        if (festival.OrganizacionPrincipalId is not int organizacionId
            || !await PuedeAdministrarOrganizacionAsync(dbContext, personaId.Value, organizacionId, cancellationToken))
        {
            return (Results.Forbid(), null);
        }

        return (null, festival);
    }

    /// <summary>
    /// Mantiene al dia el contador y las fechas que `dbo.Festivales` ya tenia.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `NumeroVersiones`, `FechaUltimaVersion`, `TieneVersionVigenteAnoActual`,
    /// `EstadoVersionAnoActual`, `FechaInicioVersionActual` y `FechaFinVersionActual` existian antes
    /// que esta tabla y los leen el sitio publico y la consola. Si las ediciones se escribieran sin
    /// tocarlas, el Festival diria «4 ediciones» mientras la lista muestra seis, y ese desacuerdo
    /// entre dos numeros de la misma pantalla es peor que no tener ninguno.
    /// </para>
    /// <para>
    /// SE CALCULA, NO SE INCREMENTA. Un contador que suma uno por alta se desincroniza en cuanto
    /// alguien borra una edicion, y no hay forma de saber desde el propio numero que quedo mal.
    /// </para>
    /// </remarks>
    private static async Task ActualizarResumenDelFestivalAsync(
        PnmcDbContext dbContext,
        int festivalId,
        DateTime ahora,
        CancellationToken cancellationToken,
        int? edicionRetirada = null)
    {
        var festival = await dbContext.FestivalRecords.FirstOrDefaultAsync(item => item.Id == festivalId, cancellationToken);
        if (festival is null) return;

        // Las que ya estan en la base, mas las que este mismo `SaveChanges` va a escribir, menos la
        // que se esta retirando. Sin mirar el rastreador, un alta y su recuento irian desfasados una
        // peticion entera: el numero correcto no aparecia hasta la siguiente escritura.
        var persistidas = await dbContext.EdicionesFestival.AsNoTracking()
            .Where(item => item.FestivalId == festivalId)
            .ToListAsync(cancellationToken);

        var pendientes = dbContext.ChangeTracker.Entries<EdicionFestivalRow>()
            .Where(entrada => entrada.State == EntityState.Added && entrada.Entity.FestivalId == festivalId)
            .Select(entrada => entrada.Entity);

        var todas = persistidas
            .Where(item => edicionRetirada is null || item.Id != edicionRetirada)
            .Concat(pendientes)
            .ToList();

        festival.VersionsCount = todas.Count;

        // `dbo.Festivales` guarda estas fechas como `date` mapeado a `DateTime?`, mientras la
        // edicion las guarda como `DateOnly?`. La conversion va a medianoche y es deliberada: una
        // edicion empieza un dia, no a una hora.
        var ultimoInicio = todas
            .Select(item => item.FechaInicio)
            .Where(fecha => fecha is not null)
            .OrderByDescending(fecha => fecha!.Value)
            .FirstOrDefault();
        festival.LastEditionDate = ADateTime(ultimoInicio);

        var delAnyoEnCurso = todas.FirstOrDefault(item => item.Anio == ahora.Year);
        festival.HasCurrentYearEdition = delAnyoEnCurso is not null;
        festival.CurrentYearEditionStatus = delAnyoEnCurso?.Estado;
        festival.CurrentYearStartDate = ADateTime(delAnyoEnCurso?.FechaInicio);
        festival.CurrentYearEndDate = ADateTime(delAnyoEnCurso?.FechaFin);
        festival.UpdatedAt = ahora;
    }

    private static Dictionary<string, string[]> Validar(EdicionFestivalSolicitud solicitud)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (solicitud.Anio is null && solicitud.NumeroEdicion is null
            && ValidationHelpers.IsMissing(solicitud.Nombre)
            && string.IsNullOrWhiteSpace(solicitud.FechaInicio)
            && string.IsNullOrWhiteSpace(solicitud.FechaFin))
        {
            errores["identificacion"] = ["Indica al menos año, número, nombre o una fecha para identificar la edición."];
        }

        if (solicitud.Anio is int anio && (anio < 1900 || anio > 2200))
        {
            errores["anio"] = ["El año de la edición debe estar entre 1900 y 2200."];
        }
        if (solicitud.NumeroEdicion is int numero && numero <= 0)
            errores["numeroEdicion"] = ["El número de edición debe ser mayor que cero."];

        var inicio = LeerFecha(solicitud.FechaInicio);
        var fin = LeerFecha(solicitud.FechaFin);
        if (!string.IsNullOrWhiteSpace(solicitud.FechaInicio) && inicio is null)
        {
            errores["fechaInicio"] = ["La fecha de inicio no tiene un formato válido (aaaa-mm-dd)."];
        }
        if (!string.IsNullOrWhiteSpace(solicitud.FechaFin) && fin is null)
        {
            errores["fechaFin"] = ["La fecha de fin no tiene un formato válido (aaaa-mm-dd)."];
        }
        if (inicio is not null && fin is not null && fin < inicio)
        {
            errores["fechaFin"] = ["La fecha de fin no puede ser anterior a la de inicio."];
        }

        if (!string.IsNullOrWhiteSpace(solicitud.Estado) && !EstadosDeEdicion.ContainsKey(solicitud.Estado.Trim()))
        {
            // Se rechaza en vez de caer al valor por omision: adivinar un estado es lo que hizo
            // `en_evaluacion`, que vivio semanas porque la etiqueta en pantalla estaba bien escrita.
            errores["estado"] = [$"El estado «{solicitud.Estado}» no existe. Los válidos son: {string.Join(", ", EstadosDeEdicion.Keys)}."];
        }

        return errores;
    }

    private static async Task ValidarCatalogosAsync(
        Dictionary<string, string[]> errores,
        EdicionFestivalSolicitud solicitud,
        PnmcDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (solicitud.TipologiaFestivalId is int tipologia
            && !await dbContext.TipologiasFestival.AnyAsync(item => item.Id == tipologia, cancellationToken))
            errores["tipologiaFestivalId"] = ["La tipología seleccionada no existe."];
        if (solicitud.FuenteFinanciacionPrimariaId is int primaria
            && !await dbContext.FuentesFinanciacion.AnyAsync(item => item.Id == primaria, cancellationToken))
            errores["fuenteFinanciacionPrimariaId"] = ["La fuente de financiación primaria no existe."];
        if (solicitud.FuenteFinanciacionSecundariaId is int secundaria
            && !await dbContext.FuentesFinanciacion.AnyAsync(item => item.Id == secundaria, cancellationToken))
            errores["fuenteFinanciacionSecundariaId"] = ["La fuente de financiación secundaria no existe."];

        await ValidarListaCatalogoAsync(errores, "practicasMusicalesIds", solicitud.PracticasMusicalesIds,
            ids => dbContext.PracticasMusicales.CountAsync(x => ids.Contains(x.Id), cancellationToken));
        await ValidarListaCatalogoAsync(errores, "territoriosSonorosIds", solicitud.TerritoriosSonorosIds,
            ids => dbContext.TerritoriosSonoros.CountAsync(x => ids.Contains(x.Id), cancellationToken));
        await ValidarListaCatalogoAsync(errores, "expresionesArtisticasIds", solicitud.ExpresionesArtisticasIds,
            ids => dbContext.ExpresionesArtisticas.CountAsync(x => ids.Contains(x.Id), cancellationToken));
        await ValidarListaCatalogoAsync(errores, "modalidadesParticipacionIds", solicitud.ModalidadesParticipacionIds,
            ids => dbContext.ModalidadesParticipacion.CountAsync(x => ids.Contains(x.Id), cancellationToken));
        await ValidarListaCatalogoAsync(errores, "tiposIngresoIds", solicitud.TiposIngresoIds,
            ids => dbContext.TiposIngreso.CountAsync(x => ids.Contains(x.Id), cancellationToken));

        foreach (var lugar in solicitud.Localizaciones)
        {
            if (string.IsNullOrWhiteSpace(lugar.CodigoDepartamento) || string.IsNullOrWhiteSpace(lugar.CodigoMunicipio)
                || !await dbContext.DivipolaLocations.AsNoTracking().AnyAsync(x => x.DepartmentCode == lugar.CodigoDepartamento.Trim() && x.MunicipalityCode == lugar.CodigoMunicipio.Trim(), cancellationToken))
            {
                errores["localizaciones"] = ["Cada localización debe corresponder a un municipio válido de DIVIPOLA."];
                break;
            }
            if (lugar.ZonaUrbanoRuralId is int zona && !await dbContext.ZonasUrbanoRural.AnyAsync(x => x.Id == zona, cancellationToken))
                errores["localizaciones"] = ["Una localización tiene una zona urbano-rural inexistente."];
            if (lugar.TitulacionColectivaId is int titulacion && !await dbContext.TitulacionesColectivas.AnyAsync(x => x.Id == titulacion, cancellationToken))
                errores["localizaciones"] = ["Una localización tiene una titulación colectiva inexistente."];
        }
        // ── LAS CONDICIONALES DE LA HISTORIA DE USUARIO ────────────────────────────────────────
        //
        // LA TABLA DE CAMPOS DE LA HU 1 LAS DECLARA UNA A UNA —«Si "Otra", habilitar ¿Qué tipo de
        // evento es?; obligatorio en este caso»— y ninguna estaba puesta. El formulario habilitaba
        // la casilla de texto al elegir «Otra», pero se podia dejar vacia y el servidor la aceptaba:
        // quedaba una edicion cuya tipologia dice «Otra» y no dice cual, que es exactamente el dato
        // que la opcion «Otra» existe para capturar.
        //
        // SE RECONOCEN POR SLUG Y NO POR IDENTIFICADOR. Los identificadores los asigna la siembra y
        // cambian entre bases; el slug `otra` es el mismo en los cuatro catalogos y es lo que el
        // guion de siembra fija a proposito -«'Ninguna' y 'Otra' son centinelas del formulario»-.
        await ExigirDetalleDeOtraAsync(errores, dbContext, cancellationToken,
            solicitud.TipologiaFestivalId, dbContext.TipologiasFestival.AsNoTracking().Where(x => x.Slug == "otra").Select(x => x.Id),
            solicitud.OtraTipologia, "otraTipologia", "Indica qué tipo de evento es.");
        await ExigirDetalleDeOtraAsync(errores, dbContext, cancellationToken,
            solicitud.FuenteFinanciacionPrimariaId, dbContext.FuentesFinanciacion.AsNoTracking().Where(x => x.Slug == "otra").Select(x => x.Id),
            solicitud.OtraFuenteFinanciacionPrimaria, "otraFuenteFinanciacionPrimaria", "Indica cuál es la otra fuente de financiación principal.");
        await ExigirDetalleDeOtraAsync(errores, dbContext, cancellationToken,
            solicitud.FuenteFinanciacionSecundariaId, dbContext.FuentesFinanciacion.AsNoTracking().Where(x => x.Slug == "otra").Select(x => x.Id),
            solicitud.OtraFuenteFinanciacionSecundaria, "otraFuenteFinanciacionSecundaria", "Indica cuál es la otra fuente de financiación secundaria.");

        await ExigirDetalleDeListaAsync(errores, solicitud.ModalidadesParticipacionIds,
            dbContext.ModalidadesParticipacion.AsNoTracking().Where(x => x.Slug == "otra").Select(x => x.Id), cancellationToken,
            solicitud.OtraModalidadParticipacion, "otraModalidadParticipacion", "Indica qué otras modalidades de participación posee.");
        await ExigirDetalleDeListaAsync(errores, solicitud.ExpresionesArtisticasIds,
            dbContext.ExpresionesArtisticas.AsNoTracking().Where(x => x.Slug == "otra").Select(x => x.Id), cancellationToken,
            solicitud.OtraExpresionArtistica, "otraExpresionArtistica", "Indica qué otra expresión artística congrega el evento.");

        // «NINGUNA» EN TERRITORIOS SONOROS ABRE LA PREGUNTA ABIERTA, y es la que da sentido a la
        // seleccion: un evento que no se reconoce en ningun Territorio Sonoro igual congrega
        // practicas musicales, y esa es la unica forma de saber cuales.
        await ExigirDetalleDeListaAsync(errores, solicitud.TerritoriosSonorosIds,
            dbContext.TerritoriosSonoros.AsNoTracking().Where(x => x.Slug == "ninguna").Select(x => x.Id), cancellationToken,
            solicitud.PracticasMusicalesQueCongrega, "practicasMusicalesQueCongrega",
            "Si el evento no se inscribe en ningún Territorio Sonoro, indica qué prácticas musicales congrega.");

        // ZONA RURAL EXIGE TITULACION COLECTIVA, que es lo que la historia de usuario marca con
        // «Si "Rural", habilitar Titulación colectiva; obligatorio en este caso».
        var zonaRural = await dbContext.ZonasUrbanoRural.AsNoTracking()
            .Where(x => x.Slug == "rural").Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (zonaRural is int rural
            && solicitud.Localizaciones.Any(x => x.ZonaUrbanoRuralId == rural && x.TitulacionColectivaId is null))
        {
            errores["localizaciones"] = ["En una localización rural hay que indicar la zona con titulación colectiva."];
        }

        foreach (var aliada in solicitud.EntidadesAliadas)
        {
            if (aliada.NaturalezaEntidadId is int naturaleza && !await dbContext.NaturalezasEntidad.AnyAsync(x => x.Id == naturaleza, cancellationToken))
                errores["entidadesAliadas"] = ["Una entidad aliada tiene una naturaleza inexistente."];
            if (aliada.EntidadId is int entidad && !await dbContext.EntityProfiles.AnyAsync(x => x.Id == entidad, cancellationToken))
                errores["entidadesAliadas"] = ["Una entidad aliada seleccionada no existe."];
        }
        if (solicitud.Materiales.Any(x => !string.IsNullOrWhiteSpace(x.Url) && !Uri.TryCreate(x.Url, UriKind.Absolute, out _)))
            errores["materiales"] = ["Cada URL de material debe ser absoluta y válida."];
    }

    /// <summary>Si se eligio la opcion «Otra» de un combo, el texto que la explica es obligatorio.</summary>
    private static async Task ExigirDetalleDeOtraAsync(
        Dictionary<string, string[]> errores, PnmcDbContext dbContext, CancellationToken cancellationToken,
        int? elegido, IQueryable<int> consultaDeOtra, string? detalle, string campo, string mensaje)
    {
        if (elegido is null || !string.IsNullOrWhiteSpace(detalle)) return;
        var idDeOtra = await consultaDeOtra.FirstOrDefaultAsync(cancellationToken);
        if (idDeOtra != 0 && elegido == idDeOtra) errores[campo] = [mensaje];
    }

    /// <summary>Lo mismo para una casilla de una lista: «Otra» marcada exige su texto.</summary>
    private static async Task ExigirDetalleDeListaAsync(
        Dictionary<string, string[]> errores, IReadOnlyList<int> elegidos, IQueryable<int> consultaDelCentinela,
        CancellationToken cancellationToken, string? detalle, string campo, string mensaje)
    {
        if (elegidos.Count == 0 || !string.IsNullOrWhiteSpace(detalle)) return;
        var centinela = await consultaDelCentinela.FirstOrDefaultAsync(cancellationToken);
        if (centinela != 0 && elegidos.Contains(centinela)) errores[campo] = [mensaje];
    }

    private static async Task ValidarListaCatalogoAsync(Dictionary<string, string[]> errores, string campo,
        IReadOnlyList<int> valores, Func<int[], Task<int>> contar)
    {
        var distintos = valores.Distinct().ToArray();
        if (distintos.Length > 0 && await contar(distintos) != distintos.Length)
            errores[campo] = ["Uno o más valores seleccionados no existen en el catálogo."];
    }

    /// <summary>
    /// Quita las OCHO tablas hijas de una edicion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ESTABA ESCRITO DENTRO DE `ReemplazarRelacionesAsync` Y EL BORRADO NO LO LLAMABA.</b>
    /// `DELETE /externo/ediciones/{id}` quitaba la fila de la edicion y nada mas, asi que en cuanto
    /// la edicion tenia UNA localizacion —o una aliada, o un material, o una practica— la clave
    /// foranea paraba el borrado y la peticion moria con un 500 sin explicacion. Medido contra
    /// PNMC_LOCAL: <c>The DELETE statement conflicted with the
    /// REFERENCE constraint "FK_EdicionesFestivalLocalizaciones_Edicion"</c>.
    /// </para>
    /// <para>
    /// LAS OCHO SE NOMBRAN AQUI Y EN UN SOLO SITIO. Una tabla hija nueva que se olvide de anadir a
    /// esta lista vuelve a romper el borrado exactamente igual, asi que conviene que el sitio donde
    /// hay que acordarse sea uno.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Las localizaciones con su territorio en nombre y su Region OCAD resuelta.
    /// </summary>
    /// <remarks>
    /// <b>LA REGION OCAD SALE DE `dbo.DepartamentosRegionOcad`</b>, que es la correspondencia
    /// Departamento–Region del Sistema General de Regalias y estaba sembrada con sus 32
    /// departamentos desde <b>sin que nadie la consultara</b>. La historia
    /// de usuario la pide automatica y de solo lectura, asi que aqui se deriva: no hay columna que
    /// mantener ni casilla que equivocarse al elegir.
    /// </remarks>
    private static async Task<List<EdicionFestivalLocalizacionDto>> ProyectarLocalizacionesAsync(
        List<LocalizacionDeRegistroRow> filas, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        if (filas.Count == 0) return [];

        var departamentos = filas.Select(x => x.CodigoDepartamento).Distinct().ToList();
        var municipios = filas.Select(x => x.CodigoMunicipio).Distinct().ToList();

        var territorio = await dbContext.DivipolaLocations.AsNoTracking()
            .Where(x => municipios.Contains(x.MunicipalityCode))
            .Select(x => new { x.DepartmentCode, x.DepartmentName, x.MunicipalityCode, x.MunicipalityName })
            .ToListAsync(cancellationToken);
        var nombreDepartamento = territorio
            .GroupBy(x => x.DepartmentCode).ToDictionary(g => g.Key, g => g.First().DepartmentName);
        var nombreMunicipio = territorio
            .GroupBy(x => x.MunicipalityCode).ToDictionary(g => g.Key, g => g.First().MunicipalityName);

        var regionPorDepartamento = await dbContext.DepartamentosRegionOcad.AsNoTracking()
            .Where(x => departamentos.Contains(x.CodigoDepartamento))
            .ToDictionaryAsync(x => x.CodigoDepartamento, x => x.RegionOcadId, cancellationToken);
        var nombreRegion = await dbContext.RegionesOcad.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, cancellationToken);
        var zonas = await dbContext.ZonasUrbanoRural.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, cancellationToken);
        var titulaciones = await dbContext.TitulacionesColectivas.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, cancellationToken);

        return filas.Select(x =>
        {
            int? regionId = regionPorDepartamento.TryGetValue(x.CodigoDepartamento, out var r) ? r : null;
            return new EdicionFestivalLocalizacionDto(
                x.Id.ToString(CultureInfo.InvariantCulture),
                x.CodigoDepartamento, nombreDepartamento.GetValueOrDefault(x.CodigoDepartamento),
                x.CodigoMunicipio, nombreMunicipio.GetValueOrDefault(x.CodigoMunicipio),
                x.ZonaUrbanoRuralId,
                x.ZonaUrbanoRuralId is int z ? zonas.GetValueOrDefault(z) : null,
                x.TitulacionColectivaId,
                x.TitulacionColectivaId is int t ? titulaciones.GetValueOrDefault(t) : null,
                regionId,
                regionId is int rid ? nombreRegion.GetValueOrDefault(rid) : null);
        }).ToList();
    }

    /// <summary>
    /// El estado operativo admite el borrado: está en preparación o programada.
    /// </summary>
    /// <remarks>
    /// ES SOLO LA MITAD DE LA REGLA. La otra —que no se haya publicado nunca y que no tenga revisión
    /// registrada— la comprueba el borrado contra el historial, y la lista la repite para saber si
    /// ofrecer la acción. Aquí vive lo que se puede contestar mirando solo la fila, para que el
    /// borrado y la lista no escriban dos veces la misma condición con dos redacciones.
    /// </remarks>
    private static bool EsBorrableSegunEstado(EdicionFestivalRow fila) =>
        string.Equals(fila.Estado, "en_preparacion", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fila.Estado, "programada", StringComparison.OrdinalIgnoreCase);

    private static async Task RetirarRelacionesAsync(int edicionId, PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.BorrarAsync<PracticaMusicalDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
        await dbContext.BorrarAsync<TerritorioSonoroDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
        await dbContext.BorrarAsync<ExpresionArtisticaDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
        await dbContext.BorrarAsync<ModalidadParticipacionDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
        await dbContext.BorrarAsync<TipoIngresoDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
        await dbContext.BorrarRelacionAsync<LocalizacionDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
        await dbContext.BorrarRelacionAsync<EntidadAliadaDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
        await dbContext.BorrarRelacionAsync<ArchivoDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, cancellationToken);
    }

    private static async Task ReemplazarRelacionesAsync(int edicionId, EdicionFestivalSolicitud solicitud,
        PnmcDbContext dbContext, CancellationToken cancellationToken)
    {
        await RetirarRelacionesAsync(edicionId, dbContext, cancellationToken);

        var ahora = DateTime.UtcNow;
        dbContext.Agregar<PracticaMusicalDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, solicitud.PracticasMusicalesIds, ahora);
        dbContext.Agregar<TerritorioSonoroDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, solicitud.TerritoriosSonorosIds, ahora);
        dbContext.Agregar<ExpresionArtisticaDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, solicitud.ExpresionesArtisticasIds, ahora);
        dbContext.Agregar<ModalidadParticipacionDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, solicitud.ModalidadesParticipacionIds, ahora);
        dbContext.Agregar<TipoIngresoDeRegistroRow>(Modulos.EdicionesDeFestival, edicionId, solicitud.TiposIngresoIds, ahora);
        var clave = Clasificaciones.Clave(edicionId);
        dbContext.LocalizacionesDeRegistro.AddRange(solicitud.Localizaciones
            .GroupBy(x => new { Departamento = x.CodigoDepartamento.Trim(), Municipio = x.CodigoMunicipio.Trim() })
            .Select(x => x.First()).Select(x => new LocalizacionDeRegistroRow { ModuloId = Modulos.EdicionesDeFestival, RegistroId = clave, CodigoDepartamento = x.CodigoDepartamento.Trim(), CodigoMunicipio = x.CodigoMunicipio.Trim(), ZonaUrbanoRuralId = x.ZonaUrbanoRuralId, TitulacionColectivaId = x.TitulacionColectivaId, FechaCreacion = ahora }));
        dbContext.EntidadesAliadasDeRegistro.AddRange(solicitud.EntidadesAliadas.Select(x => new EntidadAliadaDeRegistroRow { ModuloId = Modulos.EdicionesDeFestival, RegistroId = clave, Nombre = LimpiarOpcional(x.Nombre), Correo = LimpiarOpcional(x.Correo), NaturalezaEntidadId = x.NaturalezaEntidadId, EntidadId = x.EntidadId, FechaCreacion = ahora }));
        // UN MATERIAL QUE NO ES NI ARCHIVO NI ENLACE NO ES UN MATERIAL. La tabla de versiones ya lo
        // exigía con `CK_..._Destino`; al quedar las dos en una, la comprobación vale para las dos, y
        // aquí se descartan antes de escribir para que la base no conteste con un 500 por una fila
        // que el formulario dejó vacía.
        dbContext.ArchivosDeRegistro.AddRange(solicitud.Materiales
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .Select((x, indice) => new ArchivoDeRegistroRow { ModuloId = Modulos.EdicionesDeFestival, RegistroId = clave, Url = LimpiarOpcional(x.Url), RolArchivo = RolesDeArchivo.Material, DescripcionArchivo = LimpiarOpcional(x.DescripcionArchivo), OrdenVisualizacion = indice + 1, FechaCreacion = ahora }));
    }

    private static DateTime? ADateTime(DateOnly? fecha) =>
        fecha is null ? null : fecha.Value.ToDateTime(TimeOnly.MinValue);

    private static string NormalizarEstado(string? estado) =>
        string.IsNullOrWhiteSpace(estado) ? EstadoPorOmision : estado.Trim().ToLowerInvariant();

    private static bool EsEditable(string? estadoRegistro) =>
        EstadosFestival.Es(estadoRegistro, EstadosFestival.Borrador)
        || EstadosFestival.Es(estadoRegistro, EstadosFestival.AjustesSolicitados);

    private static string? LimpiarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Lee una fecha `aaaa-mm-dd`. Devuelve `null` si no la reconoce, no la adivina.</summary>
    private static DateOnly? LeerFecha(string? valor) =>
        DateOnly.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha) ? fecha : null;

    /// <summary>
    /// El mapeo de una edicion, compartido con la bandeja de revision institucional.
    /// </summary>
    /// <remarks>
    /// SE COMPARTE EN VEZ DE COPIARSE. La bandeja del funcionario pinta las mismas ediciones que
    /// administra la organizacion; con dos mapeos, el dia que uno anyada un campo el otro lo omite
    /// y las dos pantallas dejan de decir lo mismo del mismo dato.
    /// </remarks>
    internal static EdicionFestivalDto ADto(EdicionFestivalRow fila, int cuantasObservaciones = 0, bool sePuedeEliminar = false) => new(
        fila.Id.ToString(CultureInfo.InvariantCulture),
        fila.FestivalId.ToString(CultureInfo.InvariantCulture),
        fila.Anio,
        fila.NumeroEdicion,
        fila.Nombre,
        fila.Descripcion,
        fila.FechaInicio?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        fila.FechaFin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        fila.Director,
        fila.EstadoVisibilidad,
        EtiquetaVisibilidad(fila.EstadoVisibilidad),
        EsEditable(fila.EstadoVisibilidad),
        fila.Estado,
        EstadosDeEdicion.GetValueOrDefault(fila.Estado, fila.Estado),
        cuantasObservaciones,
        sePuedeEliminar);

    /// <summary>
    /// La visibilidad de una Edición, dicha en el vocabulario del historial institucional.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ESTO ARREGLA UN 500 QUE LLEVABA AHI DESDE EL PRINCIPIO.</b> Medido contra la base local el
    /// 12 de septiembre de 2026: <c>POST /externo/ediciones/{id}/publicar</c> escribía
    /// <c>EstadoNuevo = "publicada"</c> en <c>dbo.RegistrosRevisionHistorial</c>, cuyo
    /// <c>CK_RegistrosRevisionHistorial_EstadoNuevo</c> solo admite los siete códigos del circuito de
    /// contenidos —<c>publicado</c>, en masculino—. La ruta reventaba con 500 SIEMPRE, y por eso el
    /// usuario decía «tengo una edición en estado borrador y no existe una ruta clara para
    /// publicarla»: la había, y estaba rota.
    /// </para>
    /// <para>
    /// <b>NO LO VIO NINGUNA PRUEBA</b> porque casi toda la suite corre sobre SQLite construido desde
    /// el modelo de EF, y ahí esa restricción no existe. Es la misma lección que ya dejó escrita
    /// <c>EstadosFestival</c> sobre <c>Publicada</c> en las propuestas de cambio: lo que impone el
    /// motor no se deduce leyendo el ORM.
    /// </para>
    /// <para>
    /// <b>LOS DOS VOCABULARIOS SE CONSERVAN, que es lo correcto.</b> La Edición nombra su visibilidad
    /// en femenino porque el sujeto es la edición; el historial es institucional y común a todos los
    /// módulos, así que ahí solo caben sus siete códigos. Lo que faltaba era la traducción, y ahora
    /// está en un sitio en vez de en cada ruta.
    /// </para>
    /// </remarks>
    internal static string HaciaElHistorial(string? visibilidad) => visibilidad?.ToLowerInvariant() switch
    {
        "publicada" => EstadosFestival.Publicado,
        "archivada" => EstadosFestival.Archivado,
        _ => EstadosFestival.Borrador,
    };

    private static string EtiquetaVisibilidad(string? estado) => estado?.ToLowerInvariant() switch
    {
        "publicada" => "Publicada",
        "archivada" => "Archivada",
        _ => "Borrador",
    };

    /// <summary>
    /// Mueve la visibilidad de una edición, con su rastro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UNO SOLO PARA LAS DOS TRANSICIONES. Despublicar y archivar hacen exactamente lo mismo salvo
    /// el valor de destino y el nombre del evento: escribirlas dos veces es como se llega a que una
    /// deje rastro en el historial y la otra no, y a que la ficha del proceso cuente media historia.
    /// </para>
    /// <para>
    /// DEJA DOS RASTROS Y HACEN FALTA LOS DOS: el historial del registro —que es lo que lee la
    /// organización y la bandeja institucional— y la bitácora, que es lo que lee la auditoría.
    /// </para>
    /// </remarks>
    private static async Task<IResult> CambiarVisibilidadAsync(
        int edicionId,
        ClaimsPrincipal principal,
        PnmcDbContext dbContext,
        IAntiforgery antiforgery,
        HttpContext httpContext,
        string? desde,
        string hacia,
        string evento,
        string siNoSePuede,
        CancellationToken cancellationToken)
    {
        if (!await TestigoDePeticion.ValidoAsync(antiforgery, httpContext))
            return Results.BadRequest(new { message = "El cambio no pudo validarse. Actualiza la página e inténtalo nuevamente." });

        var fila = await dbContext.EdicionesFestival.FirstOrDefaultAsync(item => item.Id == edicionId, cancellationToken);
        if (fila is null) return Results.NotFound();

        var acceso = await ResolverAccesoAsync(fila.FestivalId, principal, dbContext, cancellationToken);
        if (acceso.Resultado is not null) return acceso.Resultado;

        var actual = (fila.EstadoVisibilidad ?? string.Empty).ToLowerInvariant();
        if (string.Equals(actual, hacia, StringComparison.OrdinalIgnoreCase)
            || (desde is not null && !string.Equals(actual, desde, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.Conflict(new { message = siNoSePuede, estadoVisibilidad = fila.EstadoVisibilidad });
        }

        var personaId = ObtenerPersonaId(principal);
        if (personaId is null) return Results.Unauthorized();

        var ahora = DateTime.UtcNow;
        var anterior = fila.EstadoVisibilidad;
        fila.EstadoVisibilidad = hacia;
        // EstadoRegistro se conserva únicamente por la FK histórica de EstadosContenido; el contrato
        // y el flujo de Edición usan EstadoVisibilidad.
        fila.EstadoRegistro = hacia == "publicada" ? EstadosFestival.Publicado : EstadosFestival.Borrador;
        fila.FechaActualizacion = ahora;

        var historial = new HistorialRevisionRegistroRow
        {
            ModuloId = Modulos.EdicionesDeFestival,
            RegistroId = fila.Id.ToString(CultureInfo.InvariantCulture),
            EstadoAnterior = HaciaElHistorial(anterior),
            EstadoNuevo = HaciaElHistorial(hacia),
            Accion = evento,
            UsuarioId = personaId.Value,
            Fecha = ahora,
            MetadataJson = $"{{\"FestivalId\":{fila.FestivalId}}}",
        };
        await InstantaneaDelHistorial.TomarAsync(dbContext, historial, acceso.Festival!.OrganizacionPrincipalId, personaId.Value, cancellationToken);
        dbContext.HistorialesRevisionRegistros.Add(historial);
        dbContext.AuditLogs.Add(new AuditLogRow
        {
            UserId = personaId.Value,
            TableName = "EdicionesFestival",
            RecordId = fila.Id.ToString(CultureInfo.InvariantCulture),
            Action = AccionesAuditoria.Actualizar,
            PreviousValuesJson = $"{{\"EstadoVisibilidad\":\"{anterior}\"}}",
            NewValuesJson = $"{{\"EstadoVisibilidad\":\"{hacia}\",\"Evento\":\"{evento}\",\"FestivalId\":{fila.FestivalId}}}",
            CreatedAt = ahora,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(ADto(fila));
    }

    private static async Task<bool> PuedeAdministrarOrganizacionAsync(PnmcDbContext dbContext, int personaId, int organizacionId, CancellationToken cancellationToken) =>
        await AdministracionDeOrganizacion.PuedeAdministrarAsync(dbContext, personaId, organizacionId, cancellationToken);

    private static int? ObtenerPersonaId(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer, CultureInfo.InvariantCulture, out var personaId)
            ? personaId : null;
}

/// <summary>
/// Cuándo ocurre una Edición: en qué mes o meses, y cuántos días dura.
/// </summary>
/// <remarks>
/// <b>VIVE EN SU PROPIA CLASE PARA PODER FIJARSE CON PRUEBAS DIRECTAS.</b> Es una regla pura sobre
/// dos fechas, y probarla a través de una petición HTTP obligaría a montar organización, Festival y
/// edición para comprobar que enero más dos días son cuatro. La tabla de casos está en
/// <c>TiempoDeLaEdicionTests</c>, y es la misma que la del frontend en
/// <c>tiempo-de-la-edicion.spec.ts</c>: la regla está escrita dos veces —el formulario la necesita
/// en vivo mientras se teclean las fechas— y lo único que impide que se separen es que las dos
/// estén fijadas por los mismos casos.
/// </remarks>
public static class TiempoDeLaEdicion
{
    /// <summary>
    /// Los meses en que ocurre la Edicion y cuantos dias dura.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>LOS PIDE LA HISTORIA DE USUARIO COMO OBLIGATORIOS Y AUTOCALCULADOS</b> —«estos campos son
    /// automaticos y se diligencian teniendo en cuenta la fecha inicio y fecha fin del festival»— y
    /// no existian por ninguna parte: ni columna, ni calculo, ni casilla.
    /// </para>
    /// <para>
    /// <b>SE CALCULAN Y NO SE GUARDAN.</b> Un derivado almacenado es un derivado que se
    /// desincroniza: corregir una fecha por otro camino dejaria el mes diciendo lo que ya no es. Es
    /// el mismo criterio con el que la Region OCAD tampoco tiene columna.
    /// </para>
    /// <para>
    /// EL MES VA EN PLURAL CUANDO HACE FALTA —«Enero, Febrero»—, que es justo el ejemplo de la
    /// historia de usuario: un festival que empieza el 30 de enero y acaba el 2 de febrero ocurre en
    /// los dos, y decir solo «Enero» seria falso.
    /// </para>
    /// <para>
    /// LA DURACION CUENTA LOS DOS EXTREMOS: un evento de un solo dia dura 1, no 0.
    /// </para>
    /// </remarks>
    public static (string? Mes, int? DuracionDias) De(DateOnly? inicio, DateOnly? fin)
    {
        if (inicio is null && fin is null) return (null, null);
        // CON UNA SOLA FECHA SE CONTESTA LO QUE SE SABE: el mes de la que haya, y ninguna duracion.
        // Callar las dos cosas porque falta una obligaria a rellenar el formulario entero para ver
        // un dato que ya se puede decir.
        if (inicio is null || fin is null)
        {
            var unica = inicio ?? fin!.Value;
            return (MesesEnEspanol[unica.Month - 1], null);
        }
        if (fin < inicio) return (null, null);

        var meses = new List<string>();
        var cursor = new DateOnly(inicio.Value.Year, inicio.Value.Month, 1);
        var ultimo = new DateOnly(fin.Value.Year, fin.Value.Month, 1);
        // SE LIMITA A DOCE. Una edicion que cruce mas de un año es un dato equivocado, y una lista
        // de cuarenta meses en una casilla no ayuda a verlo.
        while (cursor <= ultimo && meses.Count < 12)
        {
            meses.Add(MesesEnEspanol[cursor.Month - 1]);
            cursor = cursor.AddMonths(1);
        }
        return (string.Join(", ", meses), fin.Value.DayNumber - inicio.Value.DayNumber + 1);
    }

    private static readonly string[] MesesEnEspanol =
        ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
         "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
}
