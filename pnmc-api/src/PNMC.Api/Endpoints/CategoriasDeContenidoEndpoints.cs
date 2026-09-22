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
/// Las categorías temáticas de Agenda, Noticias y Catálogo Editorial.
///
/// <para>
/// <b>POR QUE EXISTEN.</b> Los tres módulos guardaban su categoría como texto libre. Nadie podía
/// corregir un nombre mal escrito sin editar cada ficha, y dos personas escribían «Convocatorias»
/// y «convocatoria» sin que nada lo impidiera.
/// </para>
/// <para>
/// <b>EL MODULO `comun` ES LA DECISION QUE SE TOMO AL PLANTEARLO.</b> Una categoría puede
/// declararse compartida por los tres sin obligar a que todas lo sean: «Bandas» sirve para una
/// noticia, un evento y una publicación; «Partitura» solo para el catálogo. Se decide categoría a
/// categoría en vez de de una vez para siempre.
/// </para>
/// <para>
/// <b>LO QUE SE PIDE PARA UN MODULO INCLUYE SIEMPRE LAS COMUNES</b>, y por eso quien administra no
/// tiene que duplicarlas: declarar «Bandas» como común la deja disponible en los tres formularios.
/// </para>
/// </summary>
public static class CategoriasDeContenidoEndpoints
{
    /// <summary>Los módulos que pueden tener categorías propias, más el compartido.</summary>
    public static readonly string[] ModulosValidos = ["agenda", "noticias", "editorial", "comun"];

    private const string ModuloComun = "comun";

    private static readonly string[] NombreObligatorio = ["El nombre de la categoría es obligatorio."];
    private static readonly string[] OrdenPositivo = ["El orden de visualización empieza en 1."];
    private static readonly string[] EnUso =
        ["Esta categoría está en uso y no se puede eliminar. Fusiónala con otra o deja de usarla primero."];
    private static readonly string[] MismaCategoria = ["Una categoría no se puede fusionar consigo misma."];
    private static readonly string[] DestinoDesconocido = ["La categoría que va a absorber el contenido no existe."];

    public static RouteGroupBuilder MapCategoriasDeContenidoEndpoints(this RouteGroupBuilder api)
    {
        // ANONIMA A PROPOSITO: el portal construye con ellas sus barras de filtro antes de que
        // exista sesión, y no expone más que el nombre de una categoría publicada.
        var publico = api.MapGroup("/publico/categorias-contenido").WithTags("categorias-publico").AllowAnonymous();
        publico.MapGet(string.Empty, Listar).WithName("ListarCategoriasPublicas");

        var consola = api.MapGroup("/institucional/categorias-contenido")
            .WithTags("categorias")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("categorias");
        consola.MapGet(string.Empty, Listar).WithName("ListarCategorias");
        consola.MapPost(string.Empty, Crear).WithName("CrearCategoria");
        consola.MapPut("/{id:int}", Guardar).WithName("GuardarCategoria");
        consola.MapDelete("/{id:int}", Eliminar).WithName("EliminarCategoria");
        consola.MapPost("/{id:int}/fusionar", Fusionar).WithName("FusionarCategoria");
        return api;
    }

    /// <summary>
    /// Las categorías de un módulo, o todas.
    /// </summary>
    /// <remarks>
    /// PEDIR UN MODULO TRAE TAMBIEN LAS COMUNES. Es lo que hace que declarar una categoría como
    /// compartida sirva de algo: aparece en los tres formularios sin duplicarla tres veces.
    /// </remarks>
    private static async Task<IResult> Listar(PnmcDbContext db, string? modulo, CancellationToken ct)
    {
        var consulta = db.Categorias.AsNoTracking();

        var pedido = (modulo ?? string.Empty).Trim().ToLowerInvariant();
        if (pedido.Length > 0 && pedido != "todos")
        {
            consulta = consulta.Where(c => c.CodigoModulo == pedido || c.CodigoModulo == ModuloComun);
        }

        var filas = await consulta
            .OrderBy(c => c.CodigoModulo)
            .ThenBy(c => c.OrdenVisualizacion)
            .ToListAsync(ct);

        var usos = await ContarUsosAsync(db, ct);
        var items = filas.Select(c => new CategoriaDeContenidoDto(
            c.Id, c.CodigoModulo, c.NombreCategoria, c.Slug, c.Descripcion, c.OrdenVisualizacion,
            usos.GetValueOrDefault(c.Id))).ToList();

        return Results.Ok(new { items, total = items.Count });
    }

    private static async Task<IResult> Crear(
        PnmcDbContext db, ClaimsPrincipal principal, GuardarCategoriaSolicitud solicitud, CancellationToken ct)
    {
        var validacion = Validar(solicitud, out var modulo, out var nombre, out var slug);
        if (validacion is not null) return validacion;

        if (await db.Categorias.AnyAsync(c => c.CodigoModulo == modulo && (c.NombreCategoria == nombre || c.Slug == slug), ct))
        {
            return Results.Conflict(new { nombre = new[] { $"«{nombre}» ya existe en {modulo}." } });
        }

        // EL ORDEN POR OMISION VA AL FINAL, que es donde espera aparecer algo recién creado. Pedirlo
        // a mano en el alta obligaría a saber cuántas hay antes de escribir la primera letra.
        var orden = solicitud.OrdenVisualizacion ?? await SiguienteOrdenAsync(db, modulo, ct);

        var fila = new CategoriaRow
        {
            CodigoModulo = modulo,
            NombreCategoria = nombre,
            Slug = slug,
            Descripcion = Limpiar(solicitud.Descripcion),
            OrdenVisualizacion = orden,
        };

        db.Categorias.Add(fila);
        Auditar(db, Actor(principal), fila, "crear", null);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/institucional/categorias-contenido/{fila.Id.ToString(CultureInfo.InvariantCulture)}", ADto(fila));
    }

    private static async Task<IResult> Guardar(
        PnmcDbContext db, ClaimsPrincipal principal, int id, GuardarCategoriaSolicitud solicitud, CancellationToken ct)
    {
        var fila = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (fila is null) return Results.NotFound();

        var validacion = Validar(solicitud, out var modulo, out var nombre, out var slug);
        if (validacion is not null) return validacion;

        if (await db.Categorias.AnyAsync(c => c.Id != id && c.CodigoModulo == modulo && (c.NombreCategoria == nombre || c.Slug == slug), ct))
        {
            return Results.Conflict(new { nombre = new[] { $"«{nombre}» ya existe en {modulo}." } });
        }

        var antes = new { fila.CodigoModulo, fila.NombreCategoria, fila.OrdenVisualizacion };

        // RENOMBRAR ARRASTRA A TODO LO QUE LA USA, y es justamente para lo que existe este catálogo:
        // corregir un nombre mal escrito sin abrir una por una las fichas que lo llevaban.
        fila.CodigoModulo = modulo;
        fila.NombreCategoria = nombre;
        fila.Slug = slug;
        fila.Descripcion = Limpiar(solicitud.Descripcion);
        fila.OrdenVisualizacion = solicitud.OrdenVisualizacion ?? fila.OrdenVisualizacion;

        Auditar(db, Actor(principal), fila, "actualizar", antes);
        await db.SaveChangesAsync(ct);

        return Results.Ok(ADto(fila));
    }

    /// <summary>
    /// Elimina una categoría que no esté en uso.
    /// </summary>
    /// <remarks>
    /// NO SE BORRA LO QUE ALGUIEN ESTA USANDO. La foránea lo impediría igual, pero con un error de
    /// base de datos que no explica nada; aquí se dice qué hacer: renombrarla o dejar de usarla.
    /// </remarks>
    private static async Task<IResult> Eliminar(
        PnmcDbContext db, ClaimsPrincipal principal, int id, CancellationToken ct)
    {
        var fila = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (fila is null) return Results.NotFound();

        var enUso = await db.Noticias.AnyAsync(x => x.CategoriaId == id, ct)
            || await db.EventosAgenda.AnyAsync(x => x.CategoriaId == id, ct)
            || await db.PublicacionesEditoriales.AnyAsync(x => x.CategoriaId == id, ct);

        if (enUso) return Results.BadRequest(new { categoria = EnUso });

        Auditar(db, Actor(principal), fila, "eliminar", new { fila.CodigoModulo, fila.NombreCategoria });
        db.Categorias.Remove(fila);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ─────────────────────────── Ayudas ───────────────────────────

    private static IResult? Validar(
        GuardarCategoriaSolicitud solicitud, out string modulo, out string nombre, out string slug)
    {
        modulo = (Limpiar(solicitud.CodigoModulo) ?? string.Empty).ToLowerInvariant();
        nombre = Limpiar(solicitud.NombreCategoria) ?? string.Empty;
        slug = ReglasDeNoticias.SlugDesde(Limpiar(solicitud.Slug) is { Length: > 0 } pedido ? pedido : nombre);

        if (nombre.Length == 0) return Results.BadRequest(new { nombreCategoria = NombreObligatorio });
        if (!ModulosValidos.Contains(modulo))
        {
            return Results.BadRequest(new
            {
                codigoModulo = new[] { $"«{modulo}» no es un módulo de categorías. Son: {string.Join(", ", ModulosValidos)}." },
            });
        }
        if (slug.Length == 0) return Results.BadRequest(new { nombreCategoria = NombreObligatorio });
        if (solicitud.OrdenVisualizacion is { } orden && orden <= 0) return Results.BadRequest(new { ordenVisualizacion = OrdenPositivo });

        return null;
    }

    private static async Task<int> SiguienteOrdenAsync(PnmcDbContext db, string modulo, CancellationToken ct)
    {
        var maximo = await db.Categorias.Where(c => c.CodigoModulo == modulo)
            .Select(c => (int?)c.OrdenVisualizacion).MaxAsync(ct);
        return (maximo ?? 0) + 1;
    }

    private static CategoriaDeContenidoDto ADto(CategoriaRow fila, int usos = 0) => new(
        fila.Id, fila.CodigoModulo, fila.NombreCategoria, fila.Slug, fila.Descripcion, fila.OrdenVisualizacion, usos);

    /// <summary>
    /// Cuántos contenidos usa cada categoría, en tres consultas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ES LO QUE PERMITE DECIDIR entre categorías propias de cada módulo y categorías comunes. Sin
    /// la cifra, esa elección es una discusión sin datos; con ella se ve cuál se está usando en dos
    /// sitios y merece compartirse, y cuál está vacía y sobra.
    /// </para>
    /// <para>
    /// TRES CONSULTAS Y NO UNA POR CATEGORIA: la alternativa, con una docena de categorías, son una
    /// docena de viajes a la base para pintar una pantalla de administración.
    /// </para>
    /// </remarks>
    private static async Task<Dictionary<int, int>> ContarUsosAsync(PnmcDbContext db, CancellationToken ct)
    {
        var conteos = new Dictionary<int, int>();

        void Sumar(IEnumerable<KeyValuePair<int, int>> parciales)
        {
            foreach (var (categoria, total) in parciales)
            {
                conteos[categoria] = conteos.GetValueOrDefault(categoria) + total;
            }
        }

        Sumar(await db.Noticias.AsNoTracking().Where(x => x.CategoriaId != null)
            .GroupBy(x => x.CategoriaId!.Value).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct));
        Sumar(await db.EventosAgenda.AsNoTracking().Where(x => x.CategoriaId != null)
            .GroupBy(x => x.CategoriaId!.Value).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct));
        Sumar(await db.PublicacionesEditoriales.AsNoTracking().Where(x => x.CategoriaId != null)
            .GroupBy(x => x.CategoriaId!.Value).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct));

        return conteos;
    }

    /// <summary>
    /// Pasa todo el contenido de una categoría a otra y retira la que queda vacía.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ES LO QUE HACE REVERSIBLE LA DECISION.</b> Hasta ahora una categoría en uso no se podía
    /// retirar —borrarla dejaría fichas apuntando a nada— y renombrarla no sirve cuando el problema
    /// es que hay dos que significan lo mismo. Sin fusionar, elegir mal entre categorías propias y
    /// comunes era una decisión definitiva: había que reasignar a mano ficha por ficha.
    /// </para>
    /// <para>
    /// <b>LA QUE ABSORBE PUEDE SER DE OTRO MODULO, Y ESE ES EL CASO INTERESANTE:</b> fusionar
    /// «Encuentros» de Agenda dentro de «Encuentros» común es exactamente cómo se converge a un
    /// vocabulario compartido sin perder lo ya publicado.
    /// </para>
    /// <para>
    /// TODO EN UNA TRANSACCION. Si la reasignación se hiciera a medias y el borrado sí, quedaría
    /// contenido apuntando a una categoría que ya no existe.
    /// </para>
    /// </remarks>
    private static async Task<IResult> Fusionar(
        PnmcDbContext db, ClaimsPrincipal principal, int id, FusionarCategoriaSolicitud solicitud, CancellationToken ct)
    {
        var origen = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (origen is null) return Results.NotFound();

        if (solicitud.DestinoId == id)
        {
            return Results.BadRequest(new { destinoId = MismaCategoria });
        }

        var destino = await db.Categorias.FirstOrDefaultAsync(c => c.Id == solicitud.DestinoId, ct);
        if (destino is null) return Results.BadRequest(new { destinoId = DestinoDesconocido });

        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);

            foreach (var noticia in await db.Noticias.Where(x => x.CategoriaId == id).ToListAsync(ct))
            {
                noticia.CategoriaId = destino.Id;
            }
            foreach (var evento in await db.EventosAgenda.Where(x => x.CategoriaId == id).ToListAsync(ct))
            {
                evento.CategoriaId = destino.Id;
            }
            foreach (var publicacion in await db.PublicacionesEditoriales.Where(x => x.CategoriaId == id).ToListAsync(ct))
            {
                publicacion.CategoriaId = destino.Id;
            }

            db.Categorias.Remove(origen);
            Auditar(db, Actor(principal), destino, "actualizar", new { fusionada = origen.NombreCategoria });
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        var usos = await ContarUsosAsync(db, ct);
        return Results.Ok(ADto(destino, usos.GetValueOrDefault(destino.Id)));
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : 0;

    private static void Auditar(PnmcDbContext db, int actor, CategoriaRow fila, string accion, object? antes) =>
        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = actor,
            TableName = "Categorias",
            RecordId = fila.Id.ToString(CultureInfo.InvariantCulture),
            Action = accion,
            PreviousValuesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            NewValuesJson = JsonSerializer.Serialize(new { fila.CodigoModulo, fila.NombreCategoria, fila.OrdenVisualizacion }),
            CreatedAt = DateTime.UtcNow,
        });
}
