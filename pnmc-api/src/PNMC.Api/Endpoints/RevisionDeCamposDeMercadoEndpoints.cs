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
/// La revisión institucional de un mercado, con los cambios pedidos campo por campo.
/// </summary>
/// <remarks>
/// <para>
/// <b>ES EL MISMO CIRCUITO QUE EL DE UN FESTIVAL, Y ESO ES TODO EL PUNTO.</b> Quien revisa abre la
/// ficha, anota qué hay que cambiar EN CADA CAMPO, guarda el borrador tantas veces como quiera y
/// cuando termina envía la solicitud; la organización ve cada nota al lado del campo que la motivó,
/// corrige y la marca como atendida. Antes de esto, un mercado devuelto llevaba <b>un párrafo</b>:
/// todo lo que hubiera que decir sobre treinta campos cabía ahí, y no había forma de saber a cuál se
/// refería cada frase ni de contar cuántas quedaban.
/// </para>
/// <para>
/// <b>SOBRE TABLAS GENERICAS, NO SOBRE UNAS DE MERCADO.</b> <c>RevisionesDeRegistro</c> identifica
/// el registro por módulo + identificador, así que Escuelas, Escenarios, Redes y Lutería heredan
/// esta maquinaria sin un juego de tablas por proceso. Lo que es de Mercados en este fichero es
/// solo: qué estado tiene que tener el registro para recibir la devolución, a dónde lo mueve, y de
/// qué tabla sale su nombre.
/// </para>
/// <para>
/// <b>LAS DOS PUERTAS, Y POR QUE SON DOS.</b> La institucional escribe y envía; la externa lee y
/// atiende. Nadie de una organización puede escribir una nota, y nadie del Programa puede darla por
/// atendida: quien corrige es quien dice que corrigió.
/// </para>
/// </remarks>
public static class RevisionDeCamposDeMercadoEndpoints
{
    /// <summary>El módulo con el que viajan estas revisiones. El mismo vocabulario de la auditoría.</summary>
    internal const string Modulo = Modulos.Mercados;

    private const string Borrador = "borrador";
    private const string Enviada = "enviada";
    private const string Cerrada = "cerrada";
    private const string Pendiente = "pendiente";
    private const string Atendida = "atendida";

    private static readonly string[] AmbitosAdmitidos = ["principal", "subregistro"];

    public static RouteGroupBuilder MapRevisionDeCamposDeMercadoEndpoints(this RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional/mercados")
            .WithTags("revision-por-campos-de-mercado")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("mercados");

        institucional.MapGet("/{mercadoId:int}/revision", async (
            int mercadoId, PnmcDbContext db, CancellationToken ct) =>
        {
            var mercado = await db.Mercados.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mercadoId && m.Activo, ct);
            if (mercado is null) return Results.NotFound();

            var revision = await RevisionVivaAsync(db, mercadoId, seguimiento: false, ct);
            return Results.Ok(await ADtoAsync(db, mercado, revision, ct));
        }).WithName("RevisionDeCamposDeMercado");

        institucional.MapPut("/{mercadoId:int}/revision", async (
            int mercadoId,
            GuardarRevisionDeCamposDeRegistroSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(solicitud);
            var revisorId = Actor(principal);
            if (revisorId == 0) return Results.Unauthorized();

            var mercado = await db.Mercados.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mercadoId && m.Activo, ct);
            if (mercado is null) return Results.NotFound();

            var (notas, errores) = Normalizar(solicitud);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            var revision = await RevisionVivaAsync(db, mercadoId, seguimiento: true, ct);
            if (revision is { Estado: Enviada })
            {
                // GUARDAR SOBRE LO YA ENVIADO CAMBIARIA LO QUE LA ORGANIZACION ESTA LEYENDO, sin que
                // se entere. Si hay que decir algo más, se decide sobre el reenvío.
                return Results.Conflict(new
                {
                    message = "Esta solicitud de cambios ya se envió a la organización y no se puede reescribir.",
                    estado = revision.Estado,
                });
            }

            revision ??= await AbrirRevisionAsync(db, mercado, revisorId, ahora, ct);
            revision.ObservacionGeneral = LimpiarNota(solicitud.ObservacionGeneral);
            revision.FechaActualizacion = ahora;
            await ReemplazarObservacionesAsync(db, revision, notas, ahora, ct);
            await db.SaveChangesAsync(ct);

            var guardada = await RevisionVivaAsync(db, mercadoId, seguimiento: false, ct);
            return Results.Ok(await ADtoAsync(db, mercado, guardada, ct));
        }).WithName("GuardarRevisionDeCamposDeMercado");

        institucional.MapPost("/{mercadoId:int}/revision/enviar", async (
            int mercadoId,
            GuardarRevisionDeCamposDeRegistroSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(solicitud);
            var revisorId = Actor(principal);
            if (revisorId == 0) return Results.Unauthorized();

            var mercado = await db.Mercados.FirstOrDefaultAsync(m => m.Id == mercadoId && m.Activo, ct);
            if (mercado is null) return Results.NotFound();

            // SOLO SE DEVUELVE LO QUE ESTA EN REVISION. Un mercado publicado o en borrador no está
            // esperando a nadie, y moverlo a «ajustes solicitados» desde ahí lo sacaría del sitio
            // donde su organización lo dejó.
            if (!string.Equals(mercado.EstadoRegistro, "en_revision", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Conflict(new
                {
                    message = "Solo un mercado en revisión puede recibir una solicitud de cambios.",
                    estado = mercado.EstadoRegistro,
                });
            }

            var (notas, errores) = Normalizar(solicitud);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            // AL MENOS UN CAMPO SEÑALADO, y esta es la diferencia con la decisión de la bandeja.
            // Aquella pide un párrafo; esta pide señalar qué hay que cambiar. Enviarla vacía dejaría
            // a la organización con un mercado devuelto y ninguna instrucción, que es justo el caso
            // que este circuito existe para cerrar.
            if (notas.Count == 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["observaciones"] = ["Señala al menos un campo antes de enviar la solicitud de cambios."],
                });
            }

            var ahora = DateTime.UtcNow;
            var revision = await RevisionVivaAsync(db, mercadoId, seguimiento: true, ct);
            if (revision is { Estado: Enviada })
            {
                return Results.Conflict(new
                {
                    message = "Esta solicitud de cambios ya se envió a la organización.",
                    estado = revision.Estado,
                });
            }

            revision ??= await AbrirRevisionAsync(db, mercado, revisorId, ahora, ct);
            revision.ObservacionGeneral = LimpiarNota(solicitud.ObservacionGeneral);
            revision.FechaActualizacion = ahora;
            await ReemplazarObservacionesAsync(db, revision, notas, ahora, ct);

            // QUIEN ENVIA SE SELLA AQUI Y NO AL ABRIR EL BORRADOR: un borrador puede pasar de un
            // funcionario a otro, y quien firma la devolución es quien pulsa enviar.
            revision.IdUsuarioRevisor = revisorId;
            revision.RevisorNombre = await NombreDeUsuarioAsync(db, revisorId, ct);
            revision.Estado = Enviada;
            revision.FechaEnvio = ahora;

            var estadoAnterior = mercado.EstadoRegistro;
            mercado.EstadoRegistro = "ajustes_solicitados";
            mercado.FechaActualizacion = ahora;

            // LA MISMA FORMA QUE EL RESTO DEL CIRCUITO DEL MERCADO —`evento` y `detalle`—, que es de
            // donde lee el historial que la organización ve en su ficha.
            MercadosEndpoints.Auditar(db, revisorId, mercado.Id, "decidir",
                new { estado = estadoAnterior },
                new { estado = mercado.EstadoRegistro, motivo = ResumenDeLoPedido(notas) });
            await AvisarALaOrganizacionAsync(db, mercado, notas.Count, ahora, ct);
            await db.SaveChangesAsync(ct);

            var enviada = await RevisionVivaAsync(db, mercadoId, seguimiento: false, ct);
            return Results.Ok(await ADtoAsync(db, mercado, enviada, ct));
        }).WithName("EnviarRevisionDeCamposDeMercado");

        // ── Lo que ve y atiende la organización ─────────────────────────────────────────────────
        var externo = group.MapGroup("/externo/mercados")
            .WithTags("revision-por-campos-de-mercado")
            .RequireAuthorization(SimusAuthentication.ExternalPolicy);

        externo.MapGet("/{mercadoId:int}/cambios-pedidos", async (
            int mercadoId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
        {
            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;

            var revision = await RevisionVivaAsync(db, mercadoId, seguimiento: false, ct);
            // SOLO LO ENVIADO. Un borrador que el funcionario está escribiendo no es una petición:
            // enseñarlo haría que la organización corrigiera cosas que todavía nadie le ha pedido.
            if (revision is null || revision.Estado != Enviada)
            {
                return Results.Ok(Array.Empty<ObservacionDeCampoDeRegistroDto>());
            }

            var notas = await db.RevisionesDeRegistroObservaciones.AsNoTracking()
                .Where(o => o.IdRevision == revision.Id)
                .OrderBy(o => o.SeccionId).ThenBy(o => o.Id)
                .ToListAsync(ct);
            return Results.Ok(notas.Select(ADto).ToArray());
        }).WithName("CambiosPedidosDeMiMercado");

        externo.MapPost("/cambios-pedidos/{observacionId:long}/atender", async (
            long observacionId,
            AtenderCambioSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(solicitud);
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "El cambio no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var nota = await db.RevisionesDeRegistroObservaciones
                .Include(o => o.Revision)
                .FirstOrDefaultAsync(o => o.Id == observacionId, ct);
            if (nota?.Revision is null || nota.Revision.ModuloId != Modulo) return Results.NotFound();
            if (!int.TryParse(nota.Revision.RegistroId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mercadoId))
            {
                return Results.NotFound();
            }

            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;

            // SOBRE UNA REVISION CERRADA NO SE MARCA NADA: el expediente ya es historial, y marcar
            // en él cambiaría lo que dice que pasó.
            if (nota.Revision.Estado != Enviada)
            {
                return Results.Conflict(new
                {
                    message = "Esta solicitud de cambios ya no está abierta.",
                    estado = nota.Revision.Estado,
                });
            }

            var ahora = DateTime.UtcNow;
            var persona = SesionExterna.PersonaDe(principal);
            nota.Estado = solicitud.Atendida ? Atendida : Pendiente;
            nota.FechaActualizacion = ahora;
            // SE PUEDE DESMARCAR, y por eso la fecha y quién la atendió se limpian al volver atrás:
            // dejarlas diría que alguien la resolvió cuando vuelve a estar pendiente.
            nota.FechaAtencion = solicitud.Atendida ? ahora : null;
            nota.IdUsuarioAtiende = solicitud.Atendida ? persona : null;
            await db.SaveChangesAsync(ct);

            return Results.Ok(ADto(nota));
        }).WithName("AtenderCambioPedidoDeMiMercado");

        return group;
    }

    // ─────────────────────────── El expediente ───────────────────────────

    /// <summary>
    /// La revisión abierta de este mercado, o <c>null</c>.
    /// </summary>
    /// <remarks>
    /// UNA VIVA POR REGISTRO, y lo sostiene un índice único filtrado en la base: sin él, dos
    /// funcionarios con la misma bandeja abierta dejarían dos borradores y nada diría cuál se envía.
    /// </remarks>
    private static async Task<RevisionDeRegistroRow?> RevisionVivaAsync(
        PnmcDbContext db, int mercadoId, bool seguimiento, CancellationToken ct)
    {
        var registro = mercadoId.ToString(CultureInfo.InvariantCulture);
        var consulta = seguimiento ? db.RevisionesDeRegistro : db.RevisionesDeRegistro.AsNoTracking();
        return await consulta
            .Where(r => r.ModuloId == Modulo && r.RegistroId == registro && r.Estado != Cerrada)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<RevisionDeRegistroRow> AbrirRevisionAsync(
        PnmcDbContext db, MercadoRow mercado, int revisorId, DateTime ahora, CancellationToken ct)
    {
        var organizacion = await db.EntityProfiles.AsNoTracking()
            .Where(e => e.Id == mercado.OrganizacionPrincipalId)
            .Select(e => e.Name)
            .FirstOrDefaultAsync(ct);

        var fila = new RevisionDeRegistroRow
        {
            ModuloId = Modulo,
            RegistroId = mercado.Id.ToString(CultureInfo.InvariantCulture),
            Estado = Borrador,
            IdUsuarioRevisor = revisorId,
            RevisorNombre = await NombreDeUsuarioAsync(db, revisorId, ct),
            // QUIEN RECIBE: quien creó el mercado desde la organización. Nulo cuando no consta, que
            // es mejor que poner al primer usuario de la organización e inventar un destinatario.
            IdUsuarioDestinatario = mercado.IdUsuarioCreador,
            DestinatarioNombre = mercado.IdUsuarioCreador is { } destinatario
                ? await NombreDeUsuarioAsync(db, destinatario, ct)
                : null,
            IdOrganizacion = mercado.OrganizacionPrincipalId,
            OrganizacionNombre = organizacion,
            FechaCreacion = ahora,
        };
        db.RevisionesDeRegistro.Add(fila);
        return fila;
    }

    /// <summary>
    /// Deja las notas de la revisión EXACTAMENTE como llegaron: crea, actualiza y borra.
    /// </summary>
    /// <remarks>
    /// SEMANTICA DE REEMPLAZO. Guardar el borrador es una sola llamada idempotente: lo que no venga
    /// en la lista se borra. La alternativa —una ruta para crear, otra para editar y otra para
    /// borrar— deja al cliente llevando la cuenta de qué mandó, y esa cuenta se desincroniza.
    /// </remarks>
    private static async Task ReemplazarObservacionesAsync(
        PnmcDbContext db, RevisionDeRegistroRow revision,
        List<ObservacionDeCampoDeRegistroSolicitud> notas, DateTime ahora, CancellationToken ct)
    {
        var existentes = revision.Id == 0
            ? []
            : await db.RevisionesDeRegistroObservaciones
                .Where(o => o.IdRevision == revision.Id)
                .ToListAsync(ct);

        var vistas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nota in notas)
        {
            var ambito = (nota.Ambito ?? "principal").Trim().ToLowerInvariant();
            var subregistro = ambito == "subregistro" ? LimpiarTexto(nota.SubregistroId) : null;
            var campoId = LimpiarTexto(nota.CampoId) ?? string.Empty;
            var clave = $"{ambito}|{subregistro}|{campoId}";
            vistas.Add(clave);

            var previa = existentes.Find(o =>
                string.Equals(o.Ambito, ambito, StringComparison.Ordinal)
                && string.Equals(o.SubregistroId, subregistro, StringComparison.Ordinal)
                && string.Equals(o.CampoId, campoId, StringComparison.Ordinal));

            if (previa is null)
            {
                db.RevisionesDeRegistroObservaciones.Add(new RevisionDeRegistroObservacionRow
                {
                    Revision = revision,
                    IdRevision = revision.Id,
                    Ambito = ambito,
                    SubregistroId = subregistro,
                    SeccionId = LimpiarTexto(nota.SeccionId) ?? "general",
                    CampoId = campoId,
                    CampoEtiqueta = LimpiarTexto(nota.CampoEtiqueta) ?? campoId,
                    ValorObservado = nota.ValorObservado,
                    Nota = LimpiarNota(nota.Nota) ?? string.Empty,
                    Estado = Pendiente,
                    FechaCreacion = ahora,
                });
                continue;
            }

            // LA NOTA CAMBIA, EL ESTADO NO SE TOCA: si la organización ya la marcó como atendida y
            // el funcionario corrige una tilde, volver a ponerla pendiente le haría repetir trabajo.
            previa.SeccionId = LimpiarTexto(nota.SeccionId) ?? previa.SeccionId;
            previa.CampoEtiqueta = LimpiarTexto(nota.CampoEtiqueta) ?? previa.CampoEtiqueta;
            previa.ValorObservado = nota.ValorObservado;
            previa.Nota = LimpiarNota(nota.Nota) ?? previa.Nota;
            previa.FechaActualizacion = ahora;
        }

        foreach (var sobrante in existentes)
        {
            var clave = $"{sobrante.Ambito}|{sobrante.SubregistroId}|{sobrante.CampoId}";
            if (!vistas.Contains(clave)) db.RevisionesDeRegistroObservaciones.Remove(sobrante);
        }
    }

    /// <summary>
    /// Cierra la revisión abierta de un mercado. La llama el reenvío a revisión de la organización.
    /// </summary>
    /// <remarks>
    /// <b>REENVIAR ES DECIR «YA ESTA».</b> El expediente se cierra y pasa a ser historial, y las
    /// notas que quedaran pendientes se dan por atendidas: la organización ha vuelto a entregar, y
    /// dejarlas pendientes haría que la siguiente revisión arrastrara las de la anterior.
    /// </remarks>
    internal static async Task CerrarRevisionVivaAsync(
        PnmcDbContext db, int mercadoId, int? persona, DateTime ahora, CancellationToken ct)
    {
        var revision = await RevisionVivaAsync(db, mercadoId, seguimiento: true, ct);
        if (revision is null || revision.Estado == Cerrada) return;

        var pendientes = await db.RevisionesDeRegistroObservaciones
            .Where(o => o.IdRevision == revision.Id && o.Estado == Pendiente)
            .ToListAsync(ct);
        foreach (var nota in pendientes)
        {
            nota.Estado = Atendida;
            nota.FechaAtencion = ahora;
            nota.FechaActualizacion = ahora;
            nota.IdUsuarioAtiende = persona;
        }

        // UN BORRADOR QUE NUNCA SE ENVIO NO SE PUEDE CERRAR: la base exige fecha de envío para
        // cualquier estado que no sea borrador. Se borra, que es lo que de verdad era: una nota que
        // el funcionario no llegó a mandar.
        if (revision.Estado == Borrador)
        {
            db.RevisionesDeRegistroObservaciones.RemoveRange(
                await db.RevisionesDeRegistroObservaciones.Where(o => o.IdRevision == revision.Id).ToListAsync(ct));
            db.RevisionesDeRegistro.Remove(revision);
            return;
        }

        revision.Estado = Cerrada;
        revision.FechaCierre = ahora;
        revision.FechaActualizacion = ahora;
    }

    // ─────────────────────────── Reglas y formas ───────────────────────────

    private static (List<ObservacionDeCampoDeRegistroSolicitud> Notas, Dictionary<string, string[]> Errores) Normalizar(
        GuardarRevisionDeCamposDeRegistroSolicitud solicitud)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var notas = new List<ObservacionDeCampoDeRegistroSolicitud>();
        if (solicitud.Observaciones is null) return (notas, errores);

        var vistas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nota in solicitud.Observaciones)
        {
            // LA NOTA VACIA NO EXISTE: es la diferencia entre pedir un cambio y no pedirlo. El
            // formulario borra la fila al vaciar el texto; aquí se sostiene también contra el API.
            var texto = LimpiarNota(nota.Nota);
            if (texto is null) continue;

            var campoId = LimpiarTexto(nota.CampoId);
            if (campoId is null)
            {
                errores["observaciones"] = ["Cada nota tiene que decir sobre qué campo se pide el cambio."];
                continue;
            }

            var ambito = (nota.Ambito ?? "principal").Trim().ToLowerInvariant();
            if (!Array.Exists(AmbitosAdmitidos, a => a == ambito))
            {
                errores["ambito"] = ["El ámbito de una nota es «principal» o «subregistro»."];
                continue;
            }

            var subregistro = ambito == "subregistro" ? LimpiarTexto(nota.SubregistroId) : null;
            if (ambito == "subregistro" && subregistro is null)
            {
                errores["subregistro"] = ["Una nota sobre una edición tiene que decir sobre cuál."];
                continue;
            }

            // UN CAMPO, UNA NOTA. Dos sobre el mismo campo son dos instrucciones que pueden
            // contradecirse, y la pantalla solo tiene sitio para una.
            if (!vistas.Add($"{ambito}|{subregistro}|{campoId}"))
            {
                errores["observaciones"] = ["Hay dos notas sobre el mismo campo."];
                continue;
            }

            notas.Add(new ObservacionDeCampoDeRegistroSolicitud
            {
                Ambito = ambito,
                SubregistroId = subregistro,
                SeccionId = LimpiarTexto(nota.SeccionId) ?? "general",
                CampoId = campoId,
                CampoEtiqueta = LimpiarTexto(nota.CampoEtiqueta) ?? campoId,
                ValorObservado = nota.ValorObservado,
                Nota = texto,
            });
        }

        return (notas, errores);
    }

    private static async Task<RevisionDeCamposDeRegistroDto> ADtoAsync(
        PnmcDbContext db, MercadoRow mercado, RevisionDeRegistroRow? revision, CancellationToken ct)
    {
        var notas = revision is null
            ? []
            : await db.RevisionesDeRegistroObservaciones.AsNoTracking()
                .Where(o => o.IdRevision == revision.Id)
                .OrderBy(o => o.SeccionId).ThenBy(o => o.Id)
                .ToListAsync(ct);

        return new RevisionDeCamposDeRegistroDto(
            revision?.Id ?? 0,
            Modulo,
            mercado.Id.ToString(CultureInfo.InvariantCulture),
            mercado.Nombre,
            revision?.Estado ?? Borrador,
            revision?.ObservacionGeneral,
            revision?.RevisorNombre,
            revision?.DestinatarioNombre,
            revision?.OrganizacionNombre,
            revision?.FechaActualizacion,
            revision?.FechaEnvio,
            notas.Select(ADto).ToArray());
    }

    private static ObservacionDeCampoDeRegistroDto ADto(RevisionDeRegistroObservacionRow fila) => new(
        fila.Id, fila.Ambito, fila.SubregistroId, fila.SeccionId, fila.CampoId,
        fila.CampoEtiqueta, fila.ValorObservado, fila.Nota, fila.Estado, fila.FechaAtencion);

    /// <summary>Lo que se le dice a la organización en el aviso, sin repetirle las notas una a una.</summary>
    private static string ResumenDeLoPedido(List<ObservacionDeCampoDeRegistroSolicitud> notas) =>
        notas.Count == 1
            ? "El Programa pidió un cambio en un campo."
            : $"El Programa pidió cambios en {notas.Count} campos.";

    private static async Task AvisarALaOrganizacionAsync(
        PnmcDbContext db, MercadoRow mercado, int cuantas, DateTime ahora, CancellationToken ct)
    {
        var cuerpo = cuantas == 1
            ? $"El Programa pidió un cambio en el mercado musical «{mercado.Nombre}». Ábrelo para ver cuál."
            : $"El Programa pidió cambios en {cuantas} campos del mercado musical «{mercado.Nombre}». Ábrelo para verlos.";

        var personas = await db.UserEntities.AsNoTracking()
            .Where(ue => ue.EntityId == mercado.OrganizacionPrincipalId && ue.IsActive)
            .Join(db.Users.AsNoTracking().Where(u => u.IsActive),
                relacion => relacion.UserId, usuario => usuario.Id,
                (_, usuario) => new { usuario.Id, usuario.Email })
            .ToListAsync(ct);

        foreach (var persona in personas)
        {
            db.Notifications.Add(new NotificationRow
            {
                RecipientUserId = persona.Id,
                RecipientEmail = persona.Email,
                EventType = "MercadoConAjustesSolicitados",
                AccessScope = SimusAuthentication.ExternalScope,
                Channel = "internal",
                Title = "Ajustes solicitados en tu mercado",
                Body = cuerpo,
                Status = "enviada",
                ModuloId = Modulos.Mercados,
                RecordId = mercado.Id.ToString(CultureInfo.InvariantCulture),
                CreatedAt = ahora,
                SentAt = ahora,
                Attempts = 0,
            });
        }
    }

    private static async Task<IResult?> NoEsSuyoAsync(
        int mercadoId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        var persona = SesionExterna.PersonaDe(principal);
        if (persona is null) return Results.Unauthorized();

        var organizacion = await db.Mercados.AsNoTracking()
            .Where(m => m.Id == mercadoId && m.Activo)
            .Select(m => (int?)m.OrganizacionPrincipalId)
            .FirstOrDefaultAsync(ct);
        if (organizacion is null) return Results.NotFound();

        return await AdministracionDeOrganizacion.PuedeAdministrarAsync(db, persona.Value, organizacion.Value, ct)
            ? null
            : Results.Forbid();
    }

    private static async Task<string?> NombreDeUsuarioAsync(PnmcDbContext db, int usuarioId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : 0;

    private static string? LimpiarTexto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? LimpiarNota(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : ValidationHelpers.SanitizeText(valor, 2400);
}
