using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Common;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Lo que un funcionario puede hacer por una organización que no es suya.
/// </summary>
/// <remarks>
/// <para>
/// <b>ASISTIR NO ES EDITAR.</b> La dirección lo decidió: un funcionario
/// no corrige los datos de una organización ajena ni toca sus credenciales. Le <b>escribe</b> para
/// que ella los corrija, y <b>administra el estado</b> de su ficha cuando la organización ya no
/// opera. Es la misma distinción que sostiene la procedencia: quien incorporó un registro no es
/// quien responde por él.
/// </para>
/// <para>
/// <b>POR QUÉ EL ESTADO SÍ Y LOS DATOS NO.</b> El estado no es un dato de la organización: es cómo
/// el sistema la trata. Una organización disuelta que se queda «registrada» para siempre sigue
/// contando en los informes y ofreciéndose en los desplegables, y nadie puede corregirlo porque
/// justamente ya no hay nadie dentro. Hasta hoy la consola <b>no tenía ninguna escritura</b> sobre
/// organizaciones: solo leía.
/// </para>
/// </remarks>
public static class AsistenciaAOrganizacionEndpoints
{
    /// <summary>El tipo de evento con el que viajan estos mensajes. Los distingue de los del sistema.</summary>
    private const string TipoDeMensaje = "MensajeDeLaConsola";

    /// <summary>El aviso de que el Programa dio por comprobada la dirección de una cuenta.</summary>
    /// <remarks>
    /// TIPO PROPIO Y NO «MensajeDeLaConsola»: no es alguien escribiendo, es un acto con efectos
    /// sobre lo que la organización puede hacer, y tiene que poder buscarse por separado.
    /// </remarks>
    private const string TipoDeConfirmacion = "CorreoConfirmadoPorElPrograma";

    /// <summary>El módulo con el que se enlaza el mensaje a su organización.</summary>
    private const string ModuloOrganizaciones = Modulos.Organizaciones;

    private static readonly string[] AsuntoObligatorio = ["Escribe un asunto para el mensaje."];
    private static readonly string[] MensajeObligatorio = ["Escribe el mensaje."];
    private static readonly string[] SinDestinatarios =
        ["Esta organización no tiene ninguna cuenta activa que pueda leer el mensaje. Su administración todavía no ha sido reclamada."];
    private static readonly string[] EstadoNoValido = ["Ese no es un estado de organización."];
    private static readonly string[] MotivoObligatorio =
        ["Escribe por qué se cierra o se desactiva. Dentro de seis meses nadie recordará el motivo."];
    private static readonly string[] SinInstitucionalParaLiberar =
        ["No hay una entidad institucional activa a la que devolver la custodia de estos procesos."];

    private static readonly string[] InstitucionalIntocable =
        ["La entidad institucional responde por todo registro que nadie haya reclamado: no se puede archivar ni desactivar."];
    private static readonly string[] NadaQueCambiar = ["No se pidió ningún cambio."];
    private static readonly string[] MotivoDeLaComprobacion =
        ["Escribe cómo comprobaste la dirección: «verificado por teléfono con la directora», por ejemplo. Es lo que distingue una comprobación real de un clic para desbloquear."];
    private static readonly string[] SinCuentaQueComprobar =
        ["Esta organización no tiene ninguna cuenta activa cuyo correo se pueda dar por comprobado. Su administración todavía no ha sido reclamada."];
    private static readonly string[] YaEstabaComprobado =
        ["Ese correo ya estaba confirmado. No hace falta volver a hacerlo."];
    private static readonly string[] ActivarSinCorreoComprobado =
        ["Esta organización no puede activarse mientras su correo siga sin confirmar. Usa «Confirmar el correo» si ya comprobaste la dirección por otra vía: eso la activa y deja el rastro de quién lo comprobó y por qué."];

    public static RouteGroupBuilder MapAsistenciaAOrganizacionEndpoints(this RouteGroupBuilder api)
    {
        var consola = api.MapGroup("/admin/organizaciones/{organizacionId:int}")
            .WithTags("asistencia-organizaciones")
            .RequireAuthorization(Permisos.PoliticaFuncionario);

        consola.MapGet("/mensajes", ListarMensajesAsync).WithName("ListarMensajesAOrganizacion");
        consola.MapPost("/mensajes", EscribirAsync).WithName("EscribirAOrganizacion");
        consola.MapPost("/estado", CambiarEstadoAsync).WithName("CambiarEstadoDeOrganizacion");
        consola.MapPost("/confirmar-correo", ConfirmarCorreoAsync).WithName("ConfirmarCorreoDeOrganizacion");

        return api;
    }

    // ---------- Escribirle ----------------------------------------------------------------

    /// <summary>
    /// Le escribe a quienes administran la organización.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>EL SERVIDOR RESUELVE A QUIÉN LE LLEGA.</b> Quien escribe no tiene por qué conocer los
    /// correos de la organización, y pedírselos abriría la puerta a escribirle a cualquiera desde
    /// una pantalla que dice «organización».
    /// </para>
    /// <para>
    /// <b>SI NADIE PUEDE LEERLO, SE DICE Y NO SE ESCRIBE.</b> Una notificación dirigida a una
    /// organización sin cuentas activas se queda en la tabla sin que nadie la vea nunca: dar por
    /// enviado un mensaje que nadie va a recibir es peor que no poder mandarlo. Y esa respuesta es
    /// en sí misma información de gestión: dice que su administración no se ha reclamado.
    /// </para>
    /// <para>
    /// SE REUTILIZA <c>dbo.Notificaciones</c>, que ya tiene módulo, registro, lectura y ámbito. Una
    /// tabla propia de mensajes daría dos bandejas distintas en el panel de la organización.
    /// </para>
    /// </remarks>
    private static async Task<IResult> EscribirAsync(
        int organizacionId,
        MensajeAOrganizacionSolicitud solicitud,
        ClaimsPrincipal principal,
        PnmcDbContext db,
        CancellationToken ct)
    {
        var organizacion = await db.EntityProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == organizacionId && x.EntityType == "organizacion", ct);
        if (organizacion is null) return Results.NotFound();

        var asunto = (solicitud.Asunto ?? string.Empty).Trim();
        var cuerpo = (solicitud.Mensaje ?? string.Empty).Trim();
        var errores = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (asunto.Length == 0) errores["asunto"] = AsuntoObligatorio;
        if (cuerpo.Length == 0) errores["mensaje"] = MensajeObligatorio;
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        var destinatarios = await DestinatariosAsync(db, organizacionId, ct);

        if (destinatarios.Count == 0) return Results.Conflict(new { destinatarios = SinDestinatarios });

        var actor = await ActorAsync(db, principal, ct);
        var ahora = DateTime.UtcNow;
        var metadatos = JsonSerializer.Serialize(new
        {
            remitenteId = actor?.Id,
            remitenteNombre = actor?.FullName,
            organizacionId,
        });

        foreach (var destinatario in destinatarios)
        {
            db.Notifications.Add(new NotificationRow
            {
                RecipientUserId = destinatario.Id,
                RecipientEmail = destinatario.Email,
                EventType = TipoDeMensaje,
                // EXTERNO: lo lee la organización en su panel, no el funcionario en la consola.
                AccessScope = "external",
                Channel = "internal",
                Title = ValidationHelpers.SanitizeText(asunto, 200),
                Body = ValidationHelpers.SanitizeText(cuerpo, 2000),
                Status = "enviada",
                ModuloId = ModuloOrganizaciones,
                RecordId = organizacionId.ToString(CultureInfo.InvariantCulture),
                MetadataJson = metadatos,
                CreatedAt = ahora,
                SentAt = ahora,
            });
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new { destinatarios = destinatarios.Count });
    }

    /// <summary>
    /// Lo que la consola le ha escrito a esta organización, y si lo han leído.
    /// </summary>
    /// <remarks>
    /// SE AGRUPAN POR ENVIO Y NO POR DESTINATARIO. Un mensaje a tres personas son tres filas en la
    /// tabla, pero es un solo acto de gestión: listarlo tres veces haría creer que se insistió.
    /// </remarks>
    private static async Task<IResult> ListarMensajesAsync(
        int organizacionId, PnmcDbContext db, CancellationToken ct)
    {
        var filas = await db.Notifications.AsNoTracking()
            .Where(n => n.ModuloId == ModuloOrganizaciones
                && n.RecordId == organizacionId.ToString()
                && n.EventType == TipoDeMensaje)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        var mensajes = filas
            .GroupBy(n => new { n.CreatedAt, n.Title })
            .Select(grupo =>
            {
                var primera = grupo.First();
                return new MensajeAOrganizacionDto(
                    primera.Id,
                    primera.Title,
                    primera.Body,
                    RemitenteDe(primera.MetadataJson),
                    // A CUANTOS LES LLEGO, no a quién: la consola no necesita los correos para
                    // saber que el mensaje salió, y enseñarlos los expondría sin motivo.
                    grupo.Count() == 1 ? null : $"{grupo.Count()} destinatarios",
                    primera.CreatedAt,
                    // LEIDO SI ALGUIEN DEL GRUPO LO LEYO: basta con que una persona responsable lo
                    // haya visto para que la organización esté enterada.
                    grupo.Select(n => n.ReadAt).Where(f => f.HasValue).OrderBy(f => f).FirstOrDefault());
            })
            .ToList();

        return Results.Ok(new { items = mensajes, total = mensajes.Count });
    }

    /// <summary>El nombre de quien escribió, leído de los metadatos del mensaje.</summary>
    private static string? RemitenteDe(string? metadatos)
    {
        if (string.IsNullOrWhiteSpace(metadatos)) return null;
        try
        {
            using var documento = JsonDocument.Parse(metadatos);
            return documento.RootElement.TryGetProperty("remitenteNombre", out var nombre) ? nombre.GetString() : null;
        }
        catch (JsonException)
        {
            // UN METADATO ILEGIBLE NO PUEDE TUMBAR LA LISTA. Se pierde el nombre de quien escribió,
            // que es mucho menos que perder el historial entero de mensajes.
            return null;
        }
    }

    // ---------- Estado --------------------------------------------------------------------

    /// <summary>
    /// Cambia el estado de una organización ante la institución, o la marca inactiva.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NO ES EDITAR SUS DATOS.</b> El estado no es un dato de la organización: es cómo el sistema
    /// la trata. Una organización disuelta que se queda «registrada» para siempre sigue contando en
    /// los informes y ofreciéndose en los desplegables, y nadie puede corregirlo porque ya no hay
    /// nadie dentro.
    /// </para>
    /// <para>
    /// <b>CUATRO ESTADOS Y NO OCHO.</b> Hasta esta ruta admitía los ocho
    /// códigos de <c>dbo.EstadosContenido</c>, que son los de un contenido publicable. Seis no los
    /// leía nadie. Los que quedan viven en <see cref="EstadosDeOrganizacion"/> y cada uno hace algo:
    /// <c>verificada</c> cierra la edición del NIT, <c>ajustes_solicitados</c> obliga a decir qué
    /// corregir, <c>archivado</c> obliga a resolver los procesos.
    /// </para>
    /// <para>
    /// <b>LA ENTIDAD INSTITUCIONAL NO SE TOCA.</b> Es la que responde por todo registro que nadie
    /// haya reclamado: archivarla o desactivarla dejaría huérfano medio ecosistema, y la base lo
    /// descubriría tarde y con un error ilegible.
    /// </para>
    /// <para>
    /// <b>CERRAR UNA ORGANIZACION MIRA SUS PROCESOS.</b> Es la mitad que faltaba: cortarle el acceso
    /// a quien administra un Festival publicado deja ese Festival en la página pública sin nadie que
    /// pueda editarlo ni reclamarlo. Se responde 409 nombrándolos, y la consola decide —con
    /// <c>queHacerConLosProcesos</c>— entre <c>liberar</c>, que devuelve su custodia al Programa
    /// dejándolos publicados, y <c>archivar</c>, que los saca del sitio público conservando su
    /// historial. Lo que se elija se aplica en la misma transacción que el cierre.
    /// </para>
    /// </remarks>
    private static async Task<IResult> CambiarEstadoAsync(
        int organizacionId,
        CambioDeEstadoDeOrganizacion solicitud,
        ClaimsPrincipal principal,
        PnmcDbContext db,
        CancellationToken ct)
    {
        var organizacion = await db.EntityProfiles
            .FirstOrDefaultAsync(x => x.Id == organizacionId && x.EntityType == "organizacion", ct);
        if (organizacion is null) return Results.NotFound();

        var estado = (solicitud.Estado ?? string.Empty).Trim().ToLowerInvariant();
        var pideEstado = estado.Length > 0;
        var pideActiva = solicitud.Activa.HasValue;

        if (!pideEstado && !pideActiva) return Results.BadRequest(new { estado = NadaQueCambiar });
        if (pideEstado && !EstadosDeOrganizacion.EsValido(estado))
        {
            return Results.BadRequest(new { estado = EstadoNoValido });
        }

        // CIERRA = LE CORTA EL ACCESO. Desactivar y eliminar son dos decisiones distintas —una es
        // reversible y la otra saca del ecosistema— pero las dos dejan a la organización fuera, así
        // que las dos exigen motivo y las dos tienen que resolver antes qué pasa con sus procesos.
        var cierra = pideEstado
            ? EstadosDeOrganizacion.SinAcceso.Contains(estado, StringComparer.OrdinalIgnoreCase)
            : pideActiva && solicitud.Activa == false;
        var motivo = (solicitud.Motivo ?? string.Empty).Trim();

        if (cierra && motivo.Length == 0) return Results.BadRequest(new { motivo = MotivoObligatorio });
        if (cierra && organizacion.IsInstitutional) return Results.Conflict(new { estado = InstitucionalIntocable });

        // UNA ORGANIZACION NO SE ACTIVA A MANO SI SU CORREO SIGUE SIN CONFIRMAR. Saltaría la única
        // comprobación que evita que el Programa reciba registros atados a una dirección que nadie
        // controla, y dejaría además a la organización en un estado que ella misma no sabría
        // explicar: el sello diría «activa» y las puertas seguirían cerradas.
        //
        // NO ES UN CALLEJON: la consola tiene «Confirmar el correo», que hace exactamente esto y
        // además deja constancia de quién comprobó la dirección y cómo. Lo que aquí se rechaza no es
        // activar, es activar sin decir por qué se da por buena la dirección.
        var abre = pideEstado
            ? string.Equals(estado, EstadosDeOrganizacion.Activa, StringComparison.OrdinalIgnoreCase)
            : pideActiva && solicitud.Activa == true;
        if (abre && !await AdministracionDeOrganizacion.TieneCorreoConfirmadoAsync(db, organizacionId, ct))
        {
            // DEVOLVER EL ACCESO NO ES LO MISMO QUE SALTARSE LA CONFIRMACION. A una organización que
            // el Programa desactivó y quiere recuperar se le devuelve el acceso —y cae en el estado
            // que le corresponde por su correo, que es «pendiente de confirmación»—; lo que no se
            // puede es usar «Activar» para darla por comprobada sin haber comprobado nada.
            if (EstadosDeOrganizacion.PermiteAcceso(organizacion.StatusCode))
            {
                return Results.Conflict(new { estado = ActivarSinCorreoComprobado });
            }

            estado = EstadosDeOrganizacion.PendienteDeConfirmacion;
            pideEstado = true;
        }

        var actor = await ActorAsync(db, principal, ct);
        var ahora = DateTime.UtcNow;

        // LOS PROCESOS QUE QUEDARIAN SIN QUIEN RESPONDA. Solo se miran al cerrar: verificar una
        // organización o pedirle ajustes no le quita a nadie su administradora.
        var huerfanos = cierra
            ? (await CustodiaDeProcesos.DeLaOrganizacionAsync(db, organizacionId, ct))
                .Where(proceso => proceso.DejaHuerfano)
                .ToList()
            : [];

        // QUE SE DECIDIO HACER CON ELLOS. Dos salidas, no una: ver `CambioDeEstadoDeOrganizacion`.
        var decision = (solicitud.QueHacerConLosProcesos ?? string.Empty).Trim().ToLowerInvariant();
        var libera = decision == "liberar";
        var archiva = decision == "archivar";
        if (huerfanos.Count > 0 && !libera && !archiva)
        {
            return Results.Conflict(new
            {
                procesos = huerfanos.Select(proceso => new
                {
                    id = proceso.Id.ToString(CultureInfo.InvariantCulture),
                    nombre = proceso.Nombre,
                    estado = proceso.Estado,
                }).ToList(),
                mensaje = huerfanos.Count == 1
                    ? "Esta organización todavía administra un proceso que quedaría sin nadie que responda por él."
                    : $"Esta organización todavía administra {huerfanos.Count} procesos que quedarían sin nadie que responda por ellos.",
            });
        }

        // SOLO LIBERAR NECESITA LA ENTIDAD INSTITUCIONAL, porque es a quien pasa la custodia.
        // Archivar no cambia de manos nada: el proceso sale del sitio público donde está.
        int? institucional = null;
        if (huerfanos.Count > 0 && libera)
        {
            institucional = await ProcedenciaDeRegistro.IdInstitucionalAsync(db, ct);
            if (institucional is null)
            {
                return Results.Problem(
                    SinInstitucionalParaLiberar[0], statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }

        var antes = new { estado = organizacion.StatusCode, activa = organizacion.IsActive };

        // TODO EN UNA TRANSACCION, con la estrategia de reintento porque la base la usa. Liberar la
        // mitad de los procesos y fallar al cerrar deja un ecosistema peor que el de partida.
        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            if (huerfanos.Count > 0)
            {
                var identificadores = huerfanos.Select(proceso => proceso.Id).ToList();
                var festivales = await db.FestivalRecords
                    .Where(festival => identificadores.Contains(festival.Id))
                    .ToListAsync(ct);
                foreach (var festival in festivales)
                {
                    if (libera)
                    {
                        CustodiaDeProcesos.Liberar(db, festival, institucional!.Value, actor?.Id ?? 0, motivo, ahora);
                    }
                    else
                    {
                        CustodiaDeProcesos.Archivar(db, festival, actor?.Id ?? 0, motivo, ahora);
                    }
                }
            }

            if (pideEstado) organizacion.StatusCode = estado;
            if (pideActiva && !pideEstado)
            {
                // LA MARCA SUELTA SIGUE ADMITIENDOSE POR COMPATIBILIDAD, pero ya no es un eje
                // propio: se traduce al estado que le corresponde. `CK_Entidades_VigenciaCoherente`
                // rechazaría cualquier otra combinación, y con razón.
                organizacion.StatusCode = solicitud.Activa!.Value
                    ? EstadosDeOrganizacion.Activa
                    : EstadosDeOrganizacion.Inactiva;
            }

            // NO SE ESCRIBE A MANO: se deriva del estado, que es el único eje. Así las dos columnas
            // no pueden discrepar ni siquiera por un camino escrito mañana.
            organizacion.IsActive = EstadosDeOrganizacion.VigenciaDe(organizacion.StatusCode);
            organizacion.UpdatedAt = ahora;

            db.AuditLogs.Add(new AuditLogRow
            {
                UserId = actor?.Id ?? 0,
                TableName = "Entidades",
                RecordId = organizacionId.ToString(CultureInfo.InvariantCulture),
                // EL VERBO SALE DE LA LISTA BLANCA DE LA BITACORA: «desactivar» no está entre los trece,
                // y el detalle viaja en el cuerpo.
                Action = cierra ? AccionesAuditoria.Archivar : AccionesAuditoria.Actualizar,
                PreviousValuesJson = JsonSerializer.Serialize(antes),
                NewValuesJson = JsonSerializer.Serialize(new
                {
                    estado = organizacion.StatusCode,
                    activa = organizacion.IsActive,
                    motivo = motivo.Length == 0 ? null : ValidationHelpers.SanitizeText(motivo, 600),
                    queSeHizoConLosProcesos = huerfanos.Count == 0 ? null : decision,
                    procesosAfectados = huerfanos.Count == 0
                        ? null
                        : huerfanos.Select(proceso => proceso.Id).ToList(),
                }),
                CreatedAt = ahora,
            });

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Ok(new
        {
            estado = organizacion.StatusCode,
            activa = organizacion.IsActive,
            // SE DICE QUE SE HIZO Y CON CUANTOS: el aviso de la consola lo escribe con esto, y
            // «se liberaron 3» y «se archivaron 3» no son la misma frase.
            queSeHizoConLosProcesos = huerfanos.Count == 0 ? null : decision,
            procesosAfectados = huerfanos.Count,
        });
    }

    // ---------- Confirmar el correo por otra vía ------------------------------------------

    /// <summary>
    /// Da por comprobada la dirección de una organización, con el motivo y el rastro de quién lo hizo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>POR QUE EXISTE.</b> Mientras SIMUS no tenga proveedor de correo, el enlace de confirmación
    /// nunca sale del sistema: se queda en la cola de salida con estado <c>pendiente</c>. Una
    /// organización que llama por teléfono para registrar su Festival se quedaría bloqueada
    /// indefinidamente por un mensaje que nadie puede recibir. La salida no es apagar la regla
    /// —eso la volvería un adorno— sino que una persona del Programa compruebe la dirección por otra
    /// vía y lo <b>declare</b>, con su nombre, la fecha y el motivo.
    /// </para>
    /// <para>
    /// <b>NO ES UN BYPASS DE DESARROLLO.</b> No se activa con una variable de entorno, no confirma
    /// en lote, no se dispara sola y no se retira cuando llegue el proveedor: seguirá haciendo falta
    /// el día que una organización no reciba el correo del Programa, que es un caso real y frecuente
    /// —filtros de correo institucional, buzones llenos, direcciones de dominio propio—.
    /// </para>
    /// <para>
    /// <b>CONFIRMAR EL CORREO NO ES VALIDAR LA ORGANIZACION.</b> Da por bueno el canal de contacto y
    /// nada más. No aprueba sus datos, no verifica su existencia jurídica y no autoriza a publicar:
    /// lo que se publica sigue pasando por el circuito de revisión.
    /// </para>
    /// <para>
    /// <b>SE CONFIRMA UNA CUENTA, NO «LA ORGANIZACION».</b> La comprobación recae sobre una persona
    /// y su dirección, que es lo que de verdad se verificó en la llamada. Si esa cuenta responde por
    /// varias organizaciones, todas las que estuvieran esperando pasan a activas, porque la puerta
    /// pregunta justamente por la cuenta.
    /// </para>
    /// </remarks>
    private static async Task<IResult> ConfirmarCorreoAsync(
        int organizacionId,
        ConfirmacionDesdeLaConsola solicitud,
        ClaimsPrincipal principal,
        PnmcDbContext db,
        CancellationToken ct)
    {
        var organizacion = await db.EntityProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == organizacionId && x.EntityType == "organizacion", ct);
        if (organizacion is null) return Results.NotFound();

        var motivo = (solicitud?.Motivo ?? string.Empty).Trim();
        if (motivo.Length < 10) return Results.BadRequest(new { motivo = MotivoDeLaComprobacion });

        var responsables = await db.UserEntities.AsNoTracking()
            .Where(v => v.EntityId == organizacionId && v.IsActive && RolesDeEntidad.QueResponden.Contains(v.EntityRole))
            .Select(v => v.UserId)
            .Distinct()
            .ToListAsync(ct);
        var candidatas = await db.Users
            .Where(u => responsables.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
        if (candidatas.Count == 0) return Results.Conflict(new { correo = SinCuentaQueComprobar });

        // LA DIRECCION QUE SE COMPROBO SE NOMBRA. Con una sola cuenta responsable no hay ambigüedad
        // y se puede omitir; con varias, dar por buena «la primera» confirmaría una dirección que
        // quien llamó no verificó.
        var pedido = (solicitud?.Correo ?? string.Empty).Trim();
        UserRow cuenta;
        if (pedido.Length > 0)
        {
            var encontrada = candidatas.Find(u => string.Equals(u.Email, pedido, StringComparison.OrdinalIgnoreCase));
            if (encontrada is null) return Results.Conflict(new { correo = new[] { EnFichaEstan(candidatas, "Esa dirección no es de ninguna cuenta responsable de esta organización.") } });
            cuenta = encontrada;
        }
        else if (candidatas.Count > 1)
        {
            return Results.Conflict(new { correo = new[] { EnFichaEstan(candidatas, "Esta organización tiene varias cuentas responsables: indica cuál dirección comprobaste.") } });
        }
        else
        {
            cuenta = candidatas[0];
        }

        if (cuenta.CorreoConfirmado) return Results.Conflict(new { correo = YaEstabaComprobado });

        var actor = await ActorAsync(db, principal, ct);
        var ahora = DateTime.UtcNow;
        var antes = new { estado = organizacion.StatusCode, correoConfirmado = false };
        List<EntityProfileRow> activadas = [];

        // TODO EN UNA TRANSACCION, con la estrategia de reintento porque la base la usa. Confirmar
        // la cuenta y no llegar a activar la organización deja el sello diciendo «pendiente» con la
        // puerta ya abierta, que es justo la incoherencia que esto viene a cerrar.
        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            cuenta.CorreoConfirmado = true;
            cuenta.FechaConfirmacionCorreo = ahora;

            // EL ENLACE QUE SIGUIERA VIVO SE VENCE. Ya no confirma nada, y dejarlo válido significa
            // que una dirección antigua podría confirmarse sola meses después.
            await ConfirmacionDeCorreo.VencerLosEnlacesVivosAsync(db, cuenta.Id, ahora, ct);

            activadas = await ConfirmacionDeCorreo.ActivarLasQueEsperabanAsync(db, cuenta.Id, ahora, ct);

            // LA BITACORA GUARDA EL ACTO SOBRE LA CUENTA, que es sobre lo que recae: el correo es de
            // una persona. La organización es el contexto y viaja en el cuerpo.
            db.AuditLogs.Add(new AuditLogRow
            {
                UserId = actor?.Id ?? 0,
                TableName = "Usuarios",
                RecordId = cuenta.Id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.Actualizar,
                PreviousValuesJson = JsonSerializer.Serialize(antes),
                NewValuesJson = JsonSerializer.Serialize(new
                {
                    correoConfirmado = true,
                    correo = cuenta.Email,
                    // EL COMO ES EL DATO. Sin esto la bitácora solo diría que alguien lo dio por bueno.
                    comprobadoPor = "consola",
                    motivo = ValidationHelpers.SanitizeText(motivo, 600),
                    organizacionId,
                    organizacionesActivadas = activadas.Select(o => o.Id).ToList(),
                }),
                CreatedAt = ahora,
            });

            // Y UNA ENTRADA POR ORGANIZACION ACTIVADA, porque el historial de una organización se lee
            // en su propia ficha y ahí no aparecería un acto registrado solo contra la cuenta.
            foreach (var activada in activadas)
            {
                db.AuditLogs.Add(new AuditLogRow
                {
                    UserId = actor?.Id ?? 0,
                    TableName = "Entidades",
                    RecordId = activada.Id.ToString(CultureInfo.InvariantCulture),
                    Action = AccionesAuditoria.Actualizar,
                    PreviousValuesJson = JsonSerializer.Serialize(new
                    {
                        estado = EstadosDeOrganizacion.PendienteDeConfirmacion,
                        activa = false,
                    }),
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        estado = activada.StatusCode,
                        activa = activada.IsActive,
                        motivo = ValidationHelpers.SanitizeText(motivo, 600),
                        correoComprobado = cuenta.Email,
                    }),
                    CreatedAt = ahora,
                });
            }

            // SE LE DICE A LA ORGANIZACION. Que el Programa dé por buena su dirección sin avisarle la
            // dejaría sin saber por qué se le abrieron las puertas, y sin oportunidad de corregir si
            // la comprobación se hizo sobre la dirección equivocada.
            db.Notifications.Add(new NotificationRow
            {
                RecipientUserId = cuenta.Id,
                RecipientEmail = cuenta.Email,
                EventType = TipoDeConfirmacion,
                AccessScope = "external",
                Channel = "internal",
                Title = "Confirmamos tu correo desde el Programa",
                Body = $"Comprobamos por otra vía que {cuenta.Email} es tu dirección de contacto y la dimos por confirmada. "
                    + "Ya puedes registrar procesos y entregarlos al Programa. "
                    + "Si esta no es tu dirección, escríbenos antes de continuar.",
                Status = "enviada",
                ModuloId = ModuloOrganizaciones,
                RecordId = organizacionId.ToString(CultureInfo.InvariantCulture),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    remitenteId = actor?.Id,
                    remitenteNombre = actor?.FullName,
                    organizacionId,
                }),
                CreatedAt = ahora,
                SentAt = ahora,
            });

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Ok(new
        {
            correoConfirmado = cuenta.Email,
            organizacionesActivadas = activadas.Count,
            estado = activadas.Find(o => o.Id == organizacionId)?.StatusCode ?? organizacion.StatusCode,
        });
    }

    /// <summary>El aviso que nombra las direcciones que la organización sí tiene en ficha.</summary>
    /// <remarks>
    /// SE NOMBRAN PORQUE QUIEN LAS VE YA PODIA VERLAS. Es la consola del Programa mirando la ficha
    /// de una organización cuyos responsables ya lista; ocultarlas aquí solo obligaría a cerrar el
    /// diálogo para ir a leerlas a dos centímetros.
    /// </remarks>
    private static string EnFichaEstan(List<UserRow> candidatas, string encabezado) =>
        $"{encabezado} En ficha están: {string.Join(", ", candidatas.Select(u => u.Email))}.";

    /// <summary>Las cuentas activas que pueden leer lo que la consola le escriba a la organización.</summary>
    private static Task<List<Destinatario>> DestinatariosAsync(
        PnmcDbContext db, int organizacionId, CancellationToken ct) =>
        db.UserEntities.AsNoTracking()
            .Where(v => v.EntityId == organizacionId && v.IsActive && RolesDeEntidad.QueResponden.Contains(v.EntityRole))
            .Join(
                db.Users.AsNoTracking().Where(u => u.IsActive),
                v => v.UserId,
                u => u.Id,
                (_, u) => new Destinatario(u.Id, u.Email))
            .Distinct()
            .ToListAsync(ct);

    /// <summary>Una cuenta que recibe lo que la consola escribe.</summary>
    private sealed record Destinatario(int Id, string Email);

    private static async Task<UserRow?> ActorAsync(PnmcDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    }
}
