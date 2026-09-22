using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Los proyectos transversales del Programa: Celebra la Música y los que vengan.
/// </summary>
/// <remarks>
/// <para>
/// <b>QUÉ RESUELVEN.</b> El Programa tiene iniciativas que atraviesan varios módulos y necesitan
/// reunir su propio contenido: su calendario, su sala de prensa. Sin esto, la única forma sería
/// etiquetar a mano y confiar en que nadie escriba la etiqueta distinto, o duplicar los eventos en
/// un sitio aparte —que es como se acaba con dos calendarios que discrepan—.
/// </para>
/// <para>
/// <b>SE ENLAZAN EVENTOS Y NOTICIAS, NO FESTIVALES.</b> Lo decidió la dirección: un Festival es un
/// proceso del ecosistema con su propia organización responsable; lo que pertenece a una
/// iniciativa del Programa es el contenido que se publica sobre él, no el proceso.
/// </para>
/// <para>
/// <b>NO SE BORRAN, SE DESACTIVAN.</b> Un proyecto que termina deja de ofrecerse en los
/// formularios y sigue explicando el contenido que ya se publicó bajo él.
/// </para>
/// </remarks>
public static class ProyectosTransversalesEndpoints
{
    private static readonly string[] NombreObligatorio = ["El nombre del proyecto es obligatorio."];
    private static readonly string[] NombreRepetido = ["Ya existe un proyecto transversal con ese nombre."];

    public static RouteGroupBuilder MapProyectosTransversalesEndpoints(this RouteGroupBuilder api)
    {
        // ANONIMA A PROPOSITO: el portal de una iniciativa arma su calendario antes de que exista
        // sesión, y no expone más que el nombre de un proyecto que ya es público.
        var publico = api.MapGroup("/publico/proyectos-transversales").WithTags("proyectos-transversales-publico").AllowAnonymous();
        publico.MapGet(string.Empty, ListarActivos).WithName("ListarProyectosTransversalesPublicos");

        var consola = api.MapGroup("/institucional/proyectos-transversales")
            .WithTags("proyectos-transversales")
            .RequireAuthorization(Permisos.PoliticaFuncionario);
        consola.MapGet(string.Empty, Listar).WithName("ListarProyectosTransversales");
        consola.MapPost(string.Empty, Crear).WithName("CrearProyectoTransversal");
        consola.MapPut("/{id:int}", Guardar).WithName("GuardarProyectoTransversal");

        return api;
    }

    /// <summary>Los que siguen abiertos. Es lo que ofrecen los formularios y el portal.</summary>
    private static async Task<IResult> ListarActivos(PnmcDbContext db, CancellationToken ct) =>
        Results.Ok(new { items = await ProyectarAsync(db, db.ProyectosTransversales.Where(x => x.Activo), ct) });

    /// <summary>Todos, activos o no: la consola tiene que poder reabrir uno cerrado.</summary>
    private static async Task<IResult> Listar(PnmcDbContext db, CancellationToken ct) =>
        Results.Ok(new { items = await ProyectarAsync(db, db.ProyectosTransversales, ct) });

    private static async Task<List<ProyectoTransversalDto>> ProyectarAsync(
        PnmcDbContext db, IQueryable<ProyectoTransversalRow> consulta, CancellationToken ct)
    {
        var filas = await consulta.AsNoTracking()
            .OrderBy(x => x.OrdenVisualizacion).ThenBy(x => x.Nombre)
            .ToListAsync(ct);
        if (filas.Count == 0) return [];

        // CUANTO CONTENIDO LO LLEVA, EN DOS CONSULTAS. Es lo que permite decir en pantalla qué
        // consecuencias tiene desactivar uno, en vez de desactivarlo y descubrirlo después.
        var ids = filas.Select(x => x.Id).ToList();
        var porEventos = await db.EventosAgendaProyectosTransversales.AsNoTracking()
            .Where(x => ids.Contains(x.ProyectoTransversalId))
            .GroupBy(x => x.ProyectoTransversalId)
            .Select(g => new { Proyecto = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Proyecto, x => x.Total, ct);
        var porNoticias = await db.NoticiasProyectosTransversales.AsNoTracking()
            .Where(x => ids.Contains(x.ProyectoTransversalId))
            .GroupBy(x => x.ProyectoTransversalId)
            .Select(g => new { Proyecto = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Proyecto, x => x.Total, ct);

        return filas.Select(x => new ProyectoTransversalDto(
            x.Id, x.Codigo, x.Nombre, x.Descripcion, x.Activo, x.OrdenVisualizacion,
            porEventos.GetValueOrDefault(x.Id) + porNoticias.GetValueOrDefault(x.Id))).ToList();
    }

    private static async Task<IResult> Crear(
        PnmcDbContext db, ClaimsPrincipal principal, GuardarProyectoTransversalSolicitud solicitud, CancellationToken ct)
    {
        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        if (nombre.Length == 0) return Results.BadRequest(new { nombre = NombreObligatorio });

        if (await db.ProyectosTransversales.AnyAsync(x => x.Nombre == nombre, ct))
        {
            return Results.Conflict(new { nombre = NombreRepetido });
        }

        // EL CODIGO SALE DEL NOMBRE Y NO SE PIDE. Es la dirección estable con la que el portal
        // filtra, y dejar que se teclee produce dos proyectos que solo difieren en un guion.
        var codigo = ReglasDeNoticias.SlugDesde(nombre);
        if (codigo.Length == 0) return Results.BadRequest(new { nombre = NombreObligatorio });
        if (await db.ProyectosTransversales.AnyAsync(x => x.Codigo == codigo, ct))
        {
            return Results.Conflict(new { nombre = NombreRepetido });
        }

        var siguiente = await db.ProyectosTransversales.AnyAsync(ct)
            ? await db.ProyectosTransversales.MaxAsync(x => x.OrdenVisualizacion, ct) + 1
            : 1;

        var fila = new ProyectoTransversalRow
        {
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(solicitud.Descripcion) ? null : solicitud.Descripcion.Trim(),
            Activo = solicitud.Activo,
            OrdenVisualizacion = siguiente,
            FechaCreacion = DateTime.UtcNow,
        };
        db.ProyectosTransversales.Add(fila);
        Auditar(db, principal, fila, "crear");
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/v1/institucional/proyectos-transversales/{fila.Id}",
            new ProyectoTransversalDto(fila.Id, fila.Codigo, fila.Nombre, fila.Descripcion, fila.Activo, fila.OrdenVisualizacion, 0));
    }

    private static async Task<IResult> Guardar(
        PnmcDbContext db, ClaimsPrincipal principal, int id, GuardarProyectoTransversalSolicitud solicitud, CancellationToken ct)
    {
        var fila = await db.ProyectosTransversales.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (fila is null) return Results.NotFound();

        var nombre = (solicitud.Nombre ?? string.Empty).Trim();
        if (nombre.Length == 0) return Results.BadRequest(new { nombre = NombreObligatorio });
        if (await db.ProyectosTransversales.AnyAsync(x => x.Nombre == nombre && x.Id != id, ct))
        {
            return Results.Conflict(new { nombre = NombreRepetido });
        }

        // EL CODIGO NO CAMBIA AL RENOMBRAR. Es la dirección con la que el portal filtra, y
        // cambiarla rompería los enlaces del sitio de la iniciativa sin avisar a nadie.
        fila.Nombre = nombre;
        fila.Descripcion = string.IsNullOrWhiteSpace(solicitud.Descripcion) ? null : solicitud.Descripcion.Trim();
        fila.Activo = solicitud.Activo;
        if (solicitud.OrdenVisualizacion is { } orden && orden > 0) { fila.OrdenVisualizacion = orden; }
        fila.FechaActualizacion = DateTime.UtcNow;

        Auditar(db, principal, fila, "actualizar");
        await db.SaveChangesAsync(ct);

        var enlazados = await db.EventosAgendaProyectosTransversales.CountAsync(x => x.ProyectoTransversalId == id, ct)
            + await db.NoticiasProyectosTransversales.CountAsync(x => x.ProyectoTransversalId == id, ct);

        return Results.Ok(new ProyectoTransversalDto(
            fila.Id, fila.Codigo, fila.Nombre, fila.Descripcion, fila.Activo, fila.OrdenVisualizacion, enlazados));
    }

    private static void Auditar(PnmcDbContext db, ClaimsPrincipal principal, ProyectoTransversalRow fila, string accion) =>
        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0,
            TableName = "ProyectosTransversales",
            RecordId = fila.Id.ToString(CultureInfo.InvariantCulture),
            Action = accion,
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { fila.Codigo, fila.Nombre, fila.Activo }),
            CreatedAt = DateTime.UtcNow,
        });
}
