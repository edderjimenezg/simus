using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Los eventos que una organización envió a la agenda y esperan decisión del Programa.
/// </summary>
/// <remarks>
/// <para>
/// <b>VIVE FUERA DEL MODULO «AGENDA», Y ESO ES DELIBERADO.</b> Revisar lo que una organización
/// entrega es trabajo de «Solicitudes y revisiones», que toda cuenta de consola tiene activado.
/// Colgarlo del módulo Agenda dejaría los eventos esperando en una bandeja que media plantilla no
/// puede abrir, exactamente como el circuito de revisión de ediciones de Festival, que por la misma
/// razón tampoco está detrás de un módulo.
/// </para>
/// <para>
/// <b>PUBLICAR COMPRUEBA LO QUE LA BASE EXIGE, ANTES DE INTENTARLO.</b>
/// <c>CK_EventosAgenda_DatosDeModalidad</c> solo aprieta cuando el estado es «publicado»: un
/// presencial necesita lugar, un virtual enlace, un mixto los dos. Sin comprobarlo aquí, publicar un
/// evento al que le falta el dato saldría como un 500 sin explicación —es el mismo defecto que se
/// corrigió en el alta externa— en vez de decir qué falta.
/// </para>
/// </remarks>
public static class RevisionInstitucionalEventosEndpoints
{
    private static readonly string[] DecisionDesconocida = ["La decisión debe ser publicar o devolver."];
    private static readonly string[] MotivoObligatorio = ["Di qué hay que corregir: sin motivo, el evento vuelve igual."];
    private static readonly string[] SoloEnRevision = ["Solo se decide sobre un evento que esté en revisión."];

    public static RouteGroupBuilder MapRevisionInstitucionalEventosEndpoints(this RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional/revision-de-eventos")
            .WithTags("supervision-institucional-eventos");
        institucional.RequireAuthorization(Permisos.PoliticaFuncionario);

        institucional.MapGet(string.Empty, ColaAsync).WithName("ColaDeEventosEnRevision");
        institucional.MapPost("/{id:long}/decision", DecidirAsync).WithName("DecidirSobreEventoEnRevision");

        return group;
    }

    /// <summary>Lo que espera decisión, lo más antiguo primero.</summary>
    /// <remarks>
    /// EL ORDEN ES EL DE LA ESPERA y no el de creación: una bandeja de trabajo se atiende por
    /// antigüedad, y lo que lleva más tiempo esperando es lo que más urge.
    /// </remarks>
    private static async Task<IResult> ColaAsync(PnmcDbContext db, CancellationToken ct)
    {
        var filas = await db.EventosAgenda.AsNoTracking()
            .Where(e => e.Estado == "en_revision")
            .OrderBy(e => e.FechaCreacion)
            .ToListAsync(ct);

        if (filas.Count == 0) return Results.Ok(Array.Empty<EventoEnRevisionDto>());

        var idsDeFestival = filas.Where(e => e.FestivalId != null).Select(e => e.FestivalId!.Value).Distinct().ToList();
        var festivales = await db.FestivalRecords.AsNoTracking()
            .Where(f => idsDeFestival.Contains(f.Id))
            .Select(f => new { f.Id, f.Name, f.OrganizacionPrincipalId })
            .ToListAsync(ct);

        var idsDeOrganizacion = festivales.Where(f => f.OrganizacionPrincipalId != null)
            .Select(f => f.OrganizacionPrincipalId!.Value).Distinct().ToList();
        var organizaciones = await db.EntityProfiles.AsNoTracking()
            .Where(e => idsDeOrganizacion.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var lista = filas.Select(e =>
        {
            var festival = e.FestivalId is null ? null : festivales.Find(f => f.Id == e.FestivalId.Value);
            var organizacion = festival?.OrganizacionPrincipalId is { } id && organizaciones.TryGetValue(id, out var nombre)
                ? nombre
                : null;
            return new EventoEnRevisionDto(
                e.Id.ToString(CultureInfo.InvariantCulture),
                e.Titulo,
                e.Descripcion,
                e.FechaInicio,
                e.FechaFin,
                e.Modalidad,
                e.Lugar,
                e.Url,
                e.NivelCobertura,
                e.FestivalId,
                festival?.Name,
                organizacion,
                e.FechaCreacion,
                LoQueFaltaParaPublicar(e));
        }).ToList();

        return Results.Ok(lista);
    }

    /// <summary>
    /// Qué le falta al evento para poder publicarse, dicho antes de intentarlo.
    /// </summary>
    /// <remarks>
    /// <b>VIAJA EN LA COLA Y NO SOLO AL DECIDIR.</b> Quien revisa tiene que poder ver, al abrir la
    /// lista, cuáles puede publicar y cuáles hay que devolver: enterarse al pulsar «publicar» es lo
    /// que convierte una bandeja en una sucesión de intentos.
    /// </remarks>
    private static string? LoQueFaltaParaPublicar(EventoAgendaRow evento)
    {
        var sinLugar = string.IsNullOrWhiteSpace(evento.Lugar);
        var sinEnlace = string.IsNullOrWhiteSpace(evento.Url);

        return evento.Modalidad switch
        {
            "presencial" when sinLugar => "Falta el lugar: un evento presencial no se publica sin decir dónde ocurre.",
            "virtual" when sinEnlace => "Falta el enlace: un evento virtual no se publica sin decir dónde conectarse.",
            "mixta" when sinLugar && sinEnlace => "Faltan el lugar y el enlace: un evento mixto necesita los dos.",
            "mixta" when sinLugar => "Falta el lugar: un evento mixto necesita el lugar y el enlace.",
            "mixta" when sinEnlace => "Falta el enlace: un evento mixto necesita el lugar y el enlace.",
            _ => null,
        };
    }

    private static async Task<IResult> DecidirAsync(
        long id,
        DecisionSobreEvento decision,
        ClaimsPrincipal principal,
        PnmcDbContext db,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var evento = await db.EventosAgenda.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (evento is null) return Results.NotFound();

        if (!string.Equals(evento.Estado, "en_revision", StringComparison.Ordinal))
        {
            return Results.Conflict(new { message = SoloEnRevision[0], estado = evento.Estado });
        }

        var pedida = (decision.Decision ?? string.Empty).Trim().ToLowerInvariant();
        var motivo = string.IsNullOrWhiteSpace(decision.Motivo) ? null : decision.Motivo.Trim();

        if (pedida == "devolver" && motivo is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["motivo"] = MotivoObligatorio,
            });
        }

        var ahora = DateTime.UtcNow;
        var estadoAnterior = evento.Estado;

        switch (pedida)
        {
            case "publicar":
                // LO QUE LA BASE EXIGE, COMPROBADO ANTES DE INTENTARLO.
                if (LoQueFaltaParaPublicar(evento) is { } falta)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["modalidad"] = [falta],
                    });
                }
                evento.Estado = "publicado";
                break;
            case "devolver":
                // VUELVE A BORRADOR, que es el único estado desde el que su organización puede
                // seguir trabajándolo. La agenda no tiene «ajustes solicitados»; el motivo viaja en
                // el aviso, que es donde la organización lo lee.
                evento.Estado = "borrador";
                break;
            default:
                return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["decision"] = DecisionDesconocida,
                });
        }

        evento.FechaActualizacion = ahora;
        evento.Version += 1;

        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = Actor(principal),
            TableName = "EventosAgenda",
            RecordId = evento.Id.ToString(CultureInfo.InvariantCulture),
            Action = pedida == "publicar" ? AccionesAuditoria.Publicar : AccionesAuditoria.Rechazar,
            PreviousValuesJson = JsonSerializer.Serialize(new { estado = estadoAnterior }),
            NewValuesJson = JsonSerializer.Serialize(new { estado = evento.Estado, motivo }),
            CreatedAt = ahora,
        });

        await AvisarALaOrganizacionAsync(db, evento, motivo, ahora, ct);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { id = evento.Id.ToString(CultureInfo.InvariantCulture), estado = evento.Estado });
    }

    /// <summary>
    /// Deja el aviso en el buzón de quien administra la organización del evento.
    /// </summary>
    /// <remarks>
    /// VA AL AMBITO EXTERNO: quien lo lee entra por el panel de su organización. Si el evento no
    /// cuelga de un festival con organización identificada no hay a quién avisar, y se deja así en
    /// vez de inventar un destinatario.
    /// </remarks>
    private static async Task AvisarALaOrganizacionAsync(
        PnmcDbContext db, EventoAgendaRow evento, string? motivo, DateTime ahora, CancellationToken ct)
    {
        if (evento.FestivalId is null) return;

        var organizacionId = await db.FestivalRecords.AsNoTracking()
            .Where(f => f.Id == evento.FestivalId.Value)
            .Select(f => f.OrganizacionPrincipalId)
            .FirstOrDefaultAsync(ct);
        if (organizacionId is null) return;

        var publicado = string.Equals(evento.Estado, "publicado", StringComparison.Ordinal);
        var cuerpo = publicado
            ? $"El evento «{evento.Titulo}» ya aparece en la agenda pública."
            : $"El Programa devolvió el evento «{evento.Titulo}» para que lo corrijas. {motivo}";

        var personas = await db.UserEntities.AsNoTracking()
            .Where(ue => ue.EntityId == organizacionId.Value && ue.IsActive)
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
                EventType = publicado ? "EventoPublicado" : "EventoDevuelto",
                AccessScope = SimusAuthentication.ExternalScope,
                Channel = "internal",
                Title = publicado ? "Tu evento está publicado" : "Tu evento volvió para corregir",
                Body = cuerpo,
                Status = "enviada",
                ModuloId = Modulos.Agenda,
                RecordId = evento.Id.ToString(CultureInfo.InvariantCulture),
                CreatedAt = ahora,
                SentAt = ahora,
                Attempts = 0,
            });
        }
    }

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : 0;
}
