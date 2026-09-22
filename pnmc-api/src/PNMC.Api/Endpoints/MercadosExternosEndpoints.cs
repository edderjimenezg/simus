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
/// El registro de un mercado musical desde la organización que lo administra.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES EL MISMO RECORRIDO QUE EL DE UN FESTIVAL, Y ESO ES DELIBERADO.</b> Una organización que ya
/// registró un festival no tiene que aprender un segundo procedimiento: borrador, se completa
/// cuando se puede, se envía a revisión, el Programa decide, se publica. Lo que cambia es la ficha,
/// no el circuito.
/// </para>
/// <para>
/// <b>LAS TRES PUERTAS QUE SE CRUZAN ANTES DE ESCRIBIR NADA</b>, en este orden y por este motivo:
/// que la petición venga de nuestra página —antiforgery—, que quien la manda administre esa
/// organización, y que la organización tenga su correo confirmado. La tercera es la regla que el
/// dirección de producto fijó: sin correo confirmado no se registran
/// procesos, y la puerta está ANTES del formulario.
/// </para>
/// <para>
/// <b>Y LO QUE NO SE HEREDA:</b> las ediciones de un mercado no pasan por revisión institucional,
/// así que aquí no hay circuito de ajustes por campo para ellas. La revisión es la del mercado.
/// </para>
/// </remarks>
public static class MercadosExternosEndpoints
{
    private static readonly string[] EditablePorLaOrganizacion = ["borrador", "ajustes_solicitados"];

    /// <summary>
    /// El módulo con el que viaja una solicitud de retiro de mercado por la bandeja de solicitudes.
    /// </summary>
    /// <remarks>
    /// SE ESCRIBE UNA VEZ Y SE LEE EN TRES SITIOS —aquí al crearla, en la gobernanza al decidirla y
    /// en la bandeja al listarla—. Un literal repetido en tres ficheros es la forma más barata de
    /// que una solicitud se cree y no la vea nadie.
    /// </remarks>
    internal const string ModuloDeRetiro = "mercados_retiro";

    public static RouteGroupBuilder MapMercadosExternosEndpoints(this RouteGroupBuilder group)
    {
        var externo = group.MapGroup("/externo").WithTags("mercados-externos");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        // LOS MISMOS CATALOGOS QUE VE LA CONSOLA. Dos consultas distintas para el mismo vocabulario
        // hacen que un valor nuevo aparezca en una pantalla y no en la otra, y eso se ve como un
        // desplegable incompleto en vez de como un error.
        externo.MapGet("/catalogos/mercado", async (PnmcDbContext db, CancellationToken ct) =>
            Results.Ok(await MercadosEndpoints.CatalogosAsync(db, ct)))
            .WithName("CatalogosDeMercadoExternos");

        externo.MapGet("/organizaciones/{organizacionId:int}/mercados", async (
            int organizacionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
        {
            var persona = SesionExterna.PersonaDe(principal);
            if (persona is null) return Results.Unauthorized();
            if (!await AdministracionDeOrganizacion.PuedeAdministrarAsync(db, persona.Value, organizacionId, ct)) return Results.Forbid();

            var filas = await db.Mercados.AsNoTracking()
                .Where(m => m.OrganizacionPrincipalId == organizacionId && m.Activo)
                .OrderBy(m => m.Nombre)
                .ToListAsync(ct);

            return Results.Ok(await MercadosEndpoints.ATarjetasAsync(db, filas, ct));
        }).WithName("ListarMercadosDeLaOrganizacion");

        /// Los festivales que ese mercado puede declarar como marco: los de la MISMA organización.
        externo.MapGet("/organizaciones/{organizacionId:int}/festivales-elegibles", async (
            int organizacionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
        {
            var persona = SesionExterna.PersonaDe(principal);
            if (persona is null) return Results.Unauthorized();
            if (!await AdministracionDeOrganizacion.PuedeAdministrarAsync(db, persona.Value, organizacionId, ct)) return Results.Forbid();

            return Results.Ok(await MercadosEndpoints.FestivalesDeLaOrganizacionAsync(db, organizacionId, ct));
        }).WithName("FestivalesElegiblesDeLaOrganizacion");

        externo.MapPost("/organizaciones/{organizacionId:int}/mercados", async (
            int organizacionId,
            MercadoUpsertRequest solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "El registro del mercado no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var persona = SesionExterna.PersonaDe(principal);
            if (persona is null) return Results.Unauthorized();
            if (!await AdministracionDeOrganizacion.PuedeAdministrarAsync(db, persona.Value, organizacionId, ct)) return Results.Forbid();

            if (await AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync(
                    db, organizacionId, "registrar mercados musicales", ct) is { } sinConfirmar)
            {
                return sinConfirmar;
            }

            // LA ORGANIZACION LA DECIDE LA RUTA, NO EL CUERPO. Si viniera del formulario, quien
            // manda la petición podría registrar un mercado a nombre de otra organización sin más
            // que cambiar un número, y la comprobación de arriba miraría a la suya.
            ArgumentNullException.ThrowIfNull(solicitud);
            solicitud.OrganizacionId = organizacionId;

            var errores = await MercadosEndpoints.ValidarAsync(solicitud, db, ct);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;

            /*
              EL MERCADO Y SU RASTRO SON UNA SOLA UNIDAD.

              HACEN FALTA DOS `SaveChanges` porque la bitácora necesita el identificador, que no
              existe hasta guardar. Sin transacción, un fallo en el segundo deja EL MERCADO CREADO Y
              SIN RASTRO, y quien lo intentó ve un 500 y vuelve a intentarlo: así aparecieron tres
              filas idénticas la primera vez que se recorrió esto en el navegador.

              Y VA DENTRO DE LA ESTRATEGIA DE EJECUCION porque SQL Server usa `EnableRetryOnFailure`:
              abrir la transacción directamente funciona en SQLite y el motor real la rechaza. Es el
              mismo patrón que ya usa el alta de un Festival.
            */
            var estrategia = db.Database.CreateExecutionStrategy();
            var mercado = await estrategia.ExecuteAsync(async () =>
            {
                await using var transaccion = await db.Database.BeginTransactionAsync(ct);

                var fila = new MercadoRow
                {
                    EstadoRegistro = "borrador",
                    Activo = true,
                    IdUsuarioCreador = persona.Value,
                    FechaCreacion = ahora,
                };
                MercadosEndpoints.Volcar(solicitud, fila);

                db.Mercados.Add(fila);
                await db.SaveChangesAsync(ct);
                await MercadosEndpoints.SincronizarCatalogosAsync(db, fila.Id, solicitud, ct);

                Auditar(db, persona.Value, fila.Id, "MercadoCreado", null, MercadosEndpoints.Resumen(fila), ahora);
                // DE DONDE VINO EL REGISTRO. Aquí la procedencia es la organización que lo registró
                // —no el Programa—, y va dentro de la transacción del alta para que no pueda quedar
                // una procedencia huérfana si el alta se deshace.
                await ProcedenciaDeRegistro.AnotarAsync(
                    db, Modulos.Mercados, fila.Id.ToString(CultureInfo.InvariantCulture),
                    ProcedenciaDeRegistro.Externo, organizacionId, persona.Value, ct);
                await db.SaveChangesAsync(ct);

                await transaccion.CommitAsync(ct);
                return fila;
            });

            var tarjetas = await MercadosEndpoints.ATarjetasAsync(db, [mercado], ct);
            return Results.Created($"/api/v1/externo/mercados/{mercado.Id}", tarjetas[0]);
        }).WithName("CrearMercadoDesdeLaOrganizacion");

        externo.MapGet("/mercados/{mercadoId:int}", async (
            int mercadoId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
        {
            var (mercado, fallo) = await MioAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;

            var tarjetas = await MercadosEndpoints.ATarjetasAsync(db, [mercado!], ct);
            return Results.Ok(tarjetas[0]);
        }).WithName("ObtenerMercadoDeLaOrganizacion");

        externo.MapPut("/mercados/{mercadoId:int}", async (
            int mercadoId,
            MercadoUpsertRequest solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "El cambio no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var (mercado, fallo) = await MioAsync(mercadoId, principal, db, ct, paraEscribir: true);
            if (fallo is not null) return fallo;

            if (!EsEditable(mercado!.EstadoRegistro))
            {
                return Results.Conflict(new
                {
                    message = "Solo un mercado en borrador o con ajustes solicitados puede editarse desde aquí. Sobre lo publicado se proponen cambios.",
                    estado = mercado.EstadoRegistro,
                });
            }

            ArgumentNullException.ThrowIfNull(solicitud);
            solicitud.OrganizacionId = mercado.OrganizacionPrincipalId;

            var errores = await MercadosEndpoints.ValidarAsync(solicitud, db, ct);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var antes = MercadosEndpoints.Resumen(mercado);
            MercadosEndpoints.Volcar(solicitud, mercado);
            await MercadosEndpoints.SincronizarCatalogosAsync(db, mercado.Id, solicitud, ct);
            mercado.FechaActualizacion = DateTime.UtcNow;

            Auditar(db, SesionExterna.PersonaDe(principal) ?? 0, mercado.Id, "MercadoActualizado",
                antes, MercadosEndpoints.Resumen(mercado), mercado.FechaActualizacion.Value);
            await db.SaveChangesAsync(ct);

            var tarjetas = await MercadosEndpoints.ATarjetasAsync(db, [mercado], ct);
            return Results.Ok(tarjetas[0]);
        }).WithName("GuardarMercadoDeLaOrganizacion");

        externo.MapPost("/mercados/{mercadoId:int}/enviar-a-revision", async (
            int mercadoId,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "El envío a revisión no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var (mercado, fallo) = await MioAsync(mercadoId, principal, db, ct, paraEscribir: true);
            if (fallo is not null) return fallo;

            if (!EsEditable(mercado!.EstadoRegistro))
            {
                return Results.Conflict(new
                {
                    message = "Solo un mercado en borrador o con ajustes solicitados puede enviarse a revisión.",
                    estado = mercado.EstadoRegistro,
                });
            }

            // EL CORREO SIN CONFIRMAR NO ENTREGA NADA AL PROGRAMA, y la puerta está aquí y no solo
            // en el botón: esta ruta está abierta a cualquiera con sesión externa.
            if (await AdministracionDeOrganizacion.ExigirCorreoConfirmadoAsync(
                    db, mercado.OrganizacionPrincipalId, "enviar este mercado a revisión", ct) is { } sinConfirmar)
            {
                return sinConfirmar;
            }

            var errores = await ValidarParaRevisionAsync(mercado, db, ct);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            var estadoAnterior = mercado.EstadoRegistro;
            mercado.EstadoRegistro = "en_revision";
            mercado.FechaEnvioARevision = ahora;
            mercado.FechaActualizacion = ahora;

            Auditar(db, SesionExterna.PersonaDe(principal) ?? 0, mercado.Id, "MercadoEnviadoARevision",
                new { estado = estadoAnterior }, new { estado = mercado.EstadoRegistro }, ahora);
            // REENVIAR ES DECIR «YA ESTA». La solicitud de cambios que estuviera abierta se cierra y
            // pasa a ser historial: dejarla viva haría que la siguiente revisión arrastrara las
            // notas de la anterior, y que el índice único de la base rechazara abrir la nueva.
            await RevisionDeCamposDeMercadoEndpoints.CerrarRevisionVivaAsync(
                db, mercado.Id, SesionExterna.PersonaDe(principal), ahora, ct);
            await AvisarAlProgramaAsync(db, mercado, ahora, ct);
            await db.SaveChangesAsync(ct);

            var tarjetas = await MercadosEndpoints.ATarjetasAsync(db, [mercado], ct);
            return Results.Ok(tarjetas[0]);
        }).WithName("EnviarMercadoARevision");

        // ────────────────── Lo que la organización puede hacer con lo suyo ──────────────────
        //
        // LAS TRES QUE TENIA UN FESTIVAL Y UN MERCADO NO. Un mercado solo se podía crear, guardar y
        // enviar. No había forma de deshacer un envío, de retirar un borrador que se abandonó, de
        // pedir que se retire uno publicado, ni de corregir un correo de contacto que rebota. Las
        // tres existen en Festivales desde hace cortes y son las mismas reglas.

        // RETIRAR: DESHACER UN ENVIO O ABANDONAR UN BORRADOR. No es lo mismo y por eso no hace lo
        // mismo: retirar de revisión devuelve el mercado a borrador —el trabajo se conserva y se
        // puede volver a enviar—, y abandonar un borrador lo archiva. Un mercado publicado no se
        // toca por aquí: para eso está la solicitud de retiro.
        externo.MapDelete("/mercados/{mercadoId:int}", async (
            int mercadoId,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "La acción no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var (mercado, fallo) = await MioAsync(mercadoId, principal, db, ct, paraEscribir: true);
            if (fallo is not null) return fallo;

            var estadoAnterior = mercado!.EstadoRegistro;
            var enRevision = string.Equals(estadoAnterior, "en_revision", StringComparison.OrdinalIgnoreCase);
            if (!enRevision && !EsEditable(estadoAnterior))
            {
                return Results.Conflict(new
                {
                    message = "Un mercado publicado o archivado no se retira desde la organización. Solicita su retiro al equipo del Programa.",
                    estado = estadoAnterior,
                });
            }

            var ahora = DateTime.UtcNow;
            mercado.EstadoRegistro = enRevision ? "borrador" : "archivado";
            mercado.FechaActualizacion = ahora;

            Auditar(db, SesionExterna.PersonaDe(principal) ?? 0, mercado.Id,
                enRevision ? "MercadoRetiradoDeRevision" : "MercadoArchivado",
                new { estado = estadoAnterior }, new { estado = mercado.EstadoRegistro }, ahora);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }).WithName("RetirarMercadoDeLaOrganizacion");

        // SOLICITAR EL RETIRO DE UNO PUBLICADO. La organización no lo quita: lo pide, y el Programa
        // decide, porque un mercado publicado es parte del catálogo público del Ecosistema.
        externo.MapPost("/mercados/{mercadoId:int}/solicitudes-retiro", async (
            int mercadoId,
            SolicitarRetiroDeMercadoSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(solicitud);
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "La solicitud no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var (mercado, fallo) = await MioAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;

            if (!string.Equals(mercado!.EstadoRegistro, "publicado", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new
                {
                    message = "Solo un mercado publicado necesita una solicitud de retiro. Los demás los retira la propia organización.",
                    estado = mercado.EstadoRegistro,
                });
            }

            var justificacion = ValidationHelpers.SanitizeText(solicitud.Justificacion, 1200);
            if (string.IsNullOrWhiteSpace(justificacion))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["justificacion"] = ["Explica por qué pides retirar este mercado del ecosistema."],
                });
            }

            // UNA SOLICITUD VIVA A LA VEZ: dos peticiones abiertas sobre el mismo mercado son dos
            // decisiones que pueden contradecirse, y quien revisa no sabría cuál manda.
            var registro = mercadoId.ToString(CultureInfo.InvariantCulture);
            var viva = await db.RecordLinkRequests.AsNoTracking().AnyAsync(
                item => item.ModuloId == ModuloDeRetiro && item.RecordId == registro
                    && (item.Status == "pendiente" || item.Status == "en_revision" || item.Status == "ajustes_solicitados"),
                ct);
            if (viva)
            {
                return Results.Conflict(new { message = "Ya hay una solicitud de retiro en trámite para este mercado." });
            }

            var ahora = DateTime.UtcNow;
            var fila = new RecordLinkRequestRow
            {
                ModuloId = ModuloDeRetiro,
                RecordId = registro,
                RequestingUserId = SesionExterna.PersonaDe(principal) ?? 0,
                EntidadId = mercado.OrganizacionPrincipalId,
                RequestedScope = "responsable",
                Reason = justificacion,
                Status = "pendiente",
                CreatedAt = ahora,
                UpdatedAt = ahora,
            };
            db.RecordLinkRequests.Add(fila);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/admin/solicitudes-de-vinculacion/{fila.Id}", new { id = fila.Id, estado = fila.Status });
        }).WithName("SolicitarRetiroDeMercado");

        // CORREGIR EL CONTACTO DE UNO PUBLICADO. Lo publicado no se edita, pero un correo que
        // rebota deja al mercado incontactable desde el portal mientras espera otro circuito.
        externo.MapPut("/mercados/{mercadoId:int}/contacto-publico", async (
            int mercadoId,
            ContactoPublicoDeMercadoSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(solicitud);
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "La actualización de contacto no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var (mercado, fallo) = await MioAsync(mercadoId, principal, db, ct, paraEscribir: true);
            if (fallo is not null) return fallo;

            if (!string.Equals(mercado!.EstadoRegistro, "publicado", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new
                {
                    message = "Esta vía es para corregir el contacto de un mercado publicado. Mientras no lo esté, edítalo con el resto de su información.",
                    estado = mercado.EstadoRegistro,
                });
            }

            var correo = ValidationHelpers.SanitizeText(solicitud.CorreoMercado, 180);
            if (!string.IsNullOrWhiteSpace(correo) && !ValidationHelpers.IsValidEmail(correo))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["correoMercado"] = ["Escribe un correo electrónico válido."],
                });
            }

            var enlaces = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["sitioWebMercado"] = solicitud.SitioWebMercado,
                ["instagramMercado"] = solicitud.InstagramMercado,
                ["facebookMercado"] = solicitud.FacebookMercado,
                ["otroEnlaceMercado"] = solicitud.OtroEnlaceMercado,
            };
            foreach (var enlace in enlaces)
            {
                if (!ValidationHelpers.IsValidHttpUrl(enlace.Value))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        [enlace.Key] = ["Escribe una dirección válida, con http:// o https://."],
                    });
                }
            }

            var ahora = DateTime.UtcNow;
            var antes = MercadosEndpoints.Resumen(mercado);
            mercado.CorreoMercado = string.IsNullOrWhiteSpace(correo) ? null : correo;
            mercado.TelefonoMercado = ValidationHelpers.SanitizeText(solicitud.TelefonoMercado, 80);
            mercado.SitioWebMercado = ValidationHelpers.SanitizeText(solicitud.SitioWebMercado, 300);
            mercado.InstagramMercado = ValidationHelpers.SanitizeText(solicitud.InstagramMercado, 300);
            mercado.FacebookMercado = ValidationHelpers.SanitizeText(solicitud.FacebookMercado, 300);
            mercado.OtroEnlaceMercado = ValidationHelpers.SanitizeText(solicitud.OtroEnlaceMercado, 300);
            mercado.ObservacionesContacto = ValidationHelpers.SanitizeText(solicitud.ObservacionesContacto, 600);
            mercado.FechaActualizacion = ahora;

            Auditar(db, SesionExterna.PersonaDe(principal) ?? 0, mercado.Id,
                "MercadoContactoPublicoActualizado", antes, MercadosEndpoints.Resumen(mercado), ahora);
            await db.SaveChangesAsync(ct);

            var tarjetas = await MercadosEndpoints.ATarjetasAsync(db, [mercado], ct);
            return Results.Ok(tarjetas[0]);
        }).WithName("ActualizarContactoPublicoDeMercado");

        // EL MISMO HISTORIAL QUE VE UN FESTIVAL EN SU FICHA, con la misma forma y el mismo orden.
        //
        // <b>DE DONDE SALE.</b> El circuito del mercado no escribe en `HistorialesRevisionRegistros`
        // —el festival sí, porque su revisión es por campos y necesita su propia bitácora—, pero
        // audita cada movimiento desde el primer día: creación, guardado, envío, recepción,
        // ajustes, aprobación, publicación y archivo. Leer la auditoría en vez de abrir una segunda
        // bitácora evita justo lo que este proyecto llama dos copias que divergen, y además da
        // historial a los mercados que ya existen, sin migrar nada.
        //
        // <b>LO QUE SALE Y LO QUE NO.</b> Sale el movimiento —qué pasó, cuándo, entre qué estados y
        // con qué comentario—. No sale quién lo hizo: hacia la organización el interlocutor es el
        // Programa, igual que en el historial del festival, que tampoco nombra a la persona.
        externo.MapGet("/organizaciones/{organizacionId:int}/mercados/{mercadoId:int}/historial", async (
            int organizacionId,
            int mercadoId,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            CancellationToken ct) =>
        {
            var (mercado, fallo) = await MioAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;
            if (mercado!.OrganizacionPrincipalId != organizacionId) return Results.NotFound();

            var registro = mercadoId.ToString(CultureInfo.InvariantCulture);
            var ediciones = await db.EdicionesMercado.AsNoTracking()
                .Where(e => e.MercadoId == mercadoId)
                .Select(e => e.Id.ToString())
                .ToArrayAsync(ct);

            var filas = await db.AuditLogs.AsNoTracking()
                .Where(a => (a.TableName == "Mercados" && a.RecordId == registro)
                    || (a.TableName == "EdicionesMercado" && ediciones.Contains(a.RecordId)))
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .ToListAsync(ct);

            return Results.Ok(filas.Select(fila => AEntradaDelHistorial(fila)).ToArray());
        }).WithName("HistorialDeMercadoDeLaOrganizacion");

        return group;
    }

    /// <summary>El mercado pedido, si quien pide administra la organización que responde por él.</summary>
    private static async Task<(MercadoRow? Mercado, IResult? Fallo)> MioAsync(
        int mercadoId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct, bool paraEscribir = false)
    {
        var persona = SesionExterna.PersonaDe(principal);
        if (persona is null) return (null, Results.Unauthorized());

        var consulta = paraEscribir ? db.Mercados : db.Mercados.AsNoTracking();
        var mercado = await consulta.FirstOrDefaultAsync(m => m.Id == mercadoId && m.Activo, ct);
        if (mercado is null) return (null, Results.NotFound());

        return await AdministracionDeOrganizacion.PuedeAdministrarAsync(db, persona.Value, mercado.OrganizacionPrincipalId, ct)
            ? (mercado, null)
            : (null, Results.Forbid());
    }

    private static bool EsEditable(string? estado) =>
        estado is not null && Array.Exists(EditablePorLaOrganizacion, e => string.Equals(e, estado, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Lo que un mercado necesita tener antes de que el Programa lo mire.
    /// </summary>
    /// <remarks>
    /// <b>ES MAS EXIGENTE QUE EL BORRADOR, Y ESA ES LA IDEA.</b> Un borrador se guarda con lo que
    /// haya —para eso es un borrador—; lo que se entrega a revisión tiene que poder revisarse. Pedir
    /// esto al crear convertiría el primer guardado en un muro; no pedirlo nunca dejaría al
    /// funcionario delante de una ficha sin territorio ni forma de contacto.
    /// </remarks>
    private static async Task<Dictionary<string, string[]>> ValidarParaRevisionAsync(
        MercadoRow mercado, PnmcDbContext db, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(mercado.Descripcion))
        {
            errores["descripcion"] = ["Describe el mercado antes de enviarlo a revisión: es lo que el Programa lee primero."];
        }

        if (mercado.AlcanceMercadoId is null) errores["alcanceId"] = ["Indica el alcance del mercado."];
        if (mercado.ModalidadMercadoId is null) errores["modalidadId"] = ["Indica la modalidad del mercado."];

        if (string.IsNullOrWhiteSpace(mercado.CorreoMercado) && string.IsNullOrWhiteSpace(mercado.TelefonoMercado))
        {
            errores["correoMercado"] = ["Deja al menos una forma de contacto: un correo o un teléfono."];
        }

        if (!await db.EntityProfiles.AsNoTracking()
                .AnyAsync(e => e.Id == mercado.OrganizacionPrincipalId && e.IsActive, ct))
        {
            errores["organizacionId"] = ["La organización responsable debe estar activa."];
        }

        return errores;
    }

    /// <summary>
    /// Deja el aviso en la bandeja del Programa.
    /// </summary>
    /// <remarks>
    /// VA AL AMBITO INSTITUCIONAL Y CON SU MODULO. El buzón de la consola resta los avisos de los
    /// módulos que cada cuenta no tiene, y este se atiende en «Solicitudes y revisiones», que toda
    /// cuenta de consola tiene: ver <see cref="ModuloDeUnAviso"/>.
    /// </remarks>
    private static async Task AvisarAlProgramaAsync(
        PnmcDbContext db, MercadoRow mercado, DateTime ahora, CancellationToken ct)
    {
        var organizacion = await db.EntityProfiles.AsNoTracking()
            .Where(e => e.Id == mercado.OrganizacionPrincipalId)
            .Select(e => e.Name)
            .FirstOrDefaultAsync(ct) ?? "una organización";

        var destinatarios = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && db.UsuariosRoles.Any(ur => ur.UserId == u.Id
                && db.Roles.Any(r => r.Id == ur.RoleId && (r.Name == "webmaster" || r.Name == "gestor_interno"))))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);

        foreach (var destinatario in destinatarios)
        {
            db.Notifications.Add(new NotificationRow
            {
                RecipientUserId = destinatario.Id,
                RecipientEmail = destinatario.Email,
                EventType = "MercadoRecibidoParaRevision",
                AccessScope = SimusAuthentication.InstitutionalScope,
                Channel = "internal",
                Title = "Mercado pendiente de revisión",
                Body = $"La organización {organizacion} envió a revisión el mercado musical «{mercado.Nombre}».",
                Status = "enviada",
                ModuloId = Modulos.Mercados,
                RecordId = mercado.Id.ToString(CultureInfo.InvariantCulture),
                CreatedAt = ahora,
                SentAt = ahora,
                Attempts = 0,
            });
        }
    }

    /// <summary>
    /// Deja el rastro del acto en la bitácora institucional.
    /// </summary>
    /// <remarks>
    /// <b>EL VERBO SE TRADUCE, Y NO ES UN CAPRICHO.</b> <c>CK_BitacoraAuditoria_Accion</c> admite
    /// trece verbos técnicos y ninguno de los nombres de evento del circuito —«MercadoCreado»,
    /// «MercadoEnviadoARevision»— está entre ellos: escribirlos tal cual hace que SQL Server
    /// rechace el INSERT y la operación entera muera con un 500. Es exactamente el mismo defecto
    /// que ya se corrigió en el circuito de Festival, y por eso se reutiliza
    /// <see cref="AccionesAuditoria"/> en vez de volver a descubrirlo. El evento no se pierde:
    /// viaja íntegro en <c>ValoresNuevos</c>.
    /// </remarks>
    /// <summary>
    /// Una fila de la bitácora de auditoría, leída como un movimiento del mercado.
    /// </summary>
    /// <remarks>
    /// <b>LOS ESTADOS VIENEN DEL JSON, Y PUEDEN NO VENIR.</b> Cada punto del circuito guarda lo que
    /// tiene sentido guardar: el envío a revisión guarda el estado de antes y el de después, y un
    /// guardado de campos no guarda ninguno porque no cambia el estado. Cuando faltan se devuelve
    /// `null` y la ficha no pinta esa línea, en vez de inventar un «No aplica» que parecería un
    /// estado. Un JSON ilegible tampoco tumba el historial: se devuelve el movimiento sin estados,
    /// porque perder la fecha de lo que pasó es peor que perder el detalle.
    /// </remarks>
    private static EntradaDeHistorialDeMercadoDto AEntradaDelHistorial(AuditLogRow fila)
    {
        var esEdicion = fila.TableName == "EdicionesMercado";
        var estadoNuevo = LeerEstadoDelDetalle(fila.NewValuesJson);
        return new EntradaDeHistorialDeMercadoDto(
            Modulo: esEdicion ? "Edición" : "Mercado",
            Accion: NombreDelMovimiento(LeerTexto(fila.NewValuesJson, "evento") ?? fila.Action, estadoNuevo, esEdicion),
            EstadoAnterior: LeerEstado(fila.PreviousValuesJson),
            EstadoNuevo: estadoNuevo,
            Comentario: LeerComentarioDelDetalle(fila.NewValuesJson),
            Fecha: DateTime.SpecifyKind(fila.CreatedAt, DateTimeKind.Utc));
    }

    /// <summary>
    /// Un solo nombre para cada movimiento, venga del canal que venga.
    /// </summary>
    /// <remarks>
    /// <b>LOS DOS LADOS DEL CIRCUITO NOMBRAN LO MISMO DE DOS MANERAS.</b> El espacio externo audita
    /// eventos con nombre propio —<c>MercadoEnviadoARevision</c>—, y la consola audita verbos
    /// genéricos —<c>crear</c>, <c>guardar</c>, <c>decidir</c>—. Sin traducir, el movimiento más
    /// importante de todos, la publicación, llegaba a la ficha como «decidir» y se leía como una
    /// actualización cualquiera: se perdía justo lo que la organización viene a mirar.
    ///
    /// <b>QUE DECIDIO LA CONSOLA LO DICE EL ESTADO RESULTANTE</b>, que la propia auditoría guarda.
    /// No hace falta un campo nuevo ni reescribir lo ya auditado: la decisión ES el estado al que
    /// llevó.
    /// </remarks>
    private static string NombreDelMovimiento(string evento, string? estadoNuevo, bool esEdicion) => evento switch
    {
        "crear" => esEdicion ? "EdicionDeMercadoCreada" : "MercadoCreado",
        "guardar" => esEdicion ? "EdicionDeMercadoActualizada" : "MercadoActualizado",
        "decidir" => estadoNuevo switch
        {
            "publicado" => "MercadoPublicado",
            "ajustes_solicitados" => "MercadoConAjustesSolicitados",
            "archivado" => "MercadoArchivado",
            _ => "MercadoDecidido",
        },
        _ => evento,
    };

    /// <summary>El valor de una propiedad de texto del objeto raíz, o <c>null</c>.</summary>
    private static string? LeerTexto(string? json, string propiedad)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var documento = JsonDocument.Parse(json);
            return documento.RootElement.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
                ? valor.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? LeerEstado(string? json) => LeerTexto(json, "estado");

    /// <summary>El estado resultante vive dentro de <c>detalle</c>, que es donde se guarda el después.</summary>
    private static string? LeerEstadoDelDetalle(string? json) => LeerDelDetalle(json, "estado");

    /// <summary>El motivo que escribió el Programa al pedir ajustes o al no aprobar.</summary>
    private static string? LeerComentarioDelDetalle(string? json)
        => LeerDelDetalle(json, "comentario") ?? LeerDelDetalle(json, "motivo");

    private static string? LeerDelDetalle(string? json, string propiedad)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var documento = JsonDocument.Parse(json);
            if (!documento.RootElement.TryGetProperty("detalle", out var detalle) || detalle.ValueKind != JsonValueKind.Object) return null;
            return detalle.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
                ? valor.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void Auditar(
        PnmcDbContext db, int persona, int mercadoId, string evento, object? antes, object? despues, DateTime ahora) =>
        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = persona,
            TableName = "Mercados",
            RecordId = mercadoId.ToString(CultureInfo.InvariantCulture),
            Action = AccionesAuditoria.DeMercado(evento),
            PreviousValuesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            NewValuesJson = JsonSerializer.Serialize(new { evento, detalle = despues }),
            CreatedAt = ahora,
        });
}
