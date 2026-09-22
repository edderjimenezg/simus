using Microsoft.EntityFrameworkCore;
using PNMC.Contracts;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Lo que comparten Agenda, Noticias y Catálogo Editorial al clasificarse por prácticas musicales
/// y territorios sonoros.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE ES UNA CLASE Y NO TRES COPIAS.</b> Los tres módulos guardan lo mismo en tablas
/// puente distintas —EF necesita un tipo por tabla—, pero las reglas son idénticas: los
/// identificadores que no existan se rechazan con un mensaje legible en vez de dejar que reviente
/// la foránea, los repetidos se colapsan, y guardar reconcilia contra lo que ya había. Escritas
/// tres veces, esas reglas divergen a la primera corrección que alguien aplique solo en una.
/// </para>
/// <para>
/// <b>UN IDENTIFICADOR INVENTADO SE RECHAZA, NO SE IGNORA.</b> Descartarlo en silencio guardaría
/// una clasificación distinta de la que se envió sin decírselo a nadie; el error de la foránea, en
/// cambio, llega como un 500 que no dice qué número estaba mal. En medio está este aviso.
/// </para>
/// </remarks>
internal static class ClasificacionDeContenido
{
    /// <summary>
    /// Comprueba que las prácticas y los territorios pedidos existan.
    /// </summary>
    /// <returns><c>null</c> si todo está bien; la respuesta de error si algo no existe.</returns>
    public static async Task<IResult?> ValidarAsync(
        PnmcDbContext db,
        IReadOnlyList<int>? practicasMusicalesIds,
        IReadOnlyList<int>? territoriosSonorosIds,
        CancellationToken ct)
    {
        var practicas = Depurar(practicasMusicalesIds);
        if (practicas.Count > 0)
        {
            var existentes = await db.PracticasMusicales.AsNoTracking()
                .Where(p => practicas.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
            var faltan = practicas.Except(existentes).ToList();
            if (faltan.Count > 0)
            {
                return Results.BadRequest(new
                {
                    practicasMusicalesIds = new[] { $"Estas prácticas musicales no existen: {string.Join(", ", faltan)}." },
                });
            }
        }

        var territorios = Depurar(territoriosSonorosIds);
        if (territorios.Count > 0)
        {
            var existentes = await db.TerritoriosSonoros.AsNoTracking()
                .Where(t => territorios.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
            var faltan = territorios.Except(existentes).ToList();
            if (faltan.Count > 0)
            {
                return Results.BadRequest(new
                {
                    territoriosSonorosIds = new[] { $"Estos territorios sonoros no existen: {string.Join(", ", faltan)}." },
                });
            }
        }

        return null;
    }

    /// <summary>
    /// Deja la tabla puente diciendo exactamente lo que pide la solicitud.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SOLO SE TOCA LA DIFERENCIA: se borra lo que sobra y se añade lo que falta, en vez de vaciar
    /// y reescribir. Vaciar y reescribir cambia todas las filas aunque no cambie nada, y en una
    /// tabla auditada eso convierte «no tocó la clasificación» en «la reemplazó entera».
    /// </para>
    /// <para>
    /// UNA LISTA AUSENTE NO ES UNA LISTA VACIA. <c>null</c> significa «este cliente no habla de
    /// clasificación» y no cambia nada; una lista vacía significa «quítalas todas». Sin esa
    /// distinción, el primer cliente que no conociera el campo borraría la clasificación de todo
    /// lo que guardara.
    /// </para>
    /// </remarks>
    public static void Reconciliar<TVinculo>(
        DbSet<TVinculo> conjunto,
        IReadOnlyList<TVinculo> existentes,
        Func<TVinculo, int> elementoDe,
        IReadOnlyList<int>? deseados,
        Func<int, TVinculo> crear)
        where TVinculo : class
    {
        if (deseados is null) return;

        var pedidos = Depurar(deseados);

        foreach (var fila in existentes.Where(f => !pedidos.Contains(elementoDe(f))))
        {
            conjunto.Remove(fila);
        }

        var yaEstaban = existentes.Select(elementoDe).ToHashSet();
        foreach (var id in pedidos.Where(id => !yaEstaban.Contains(id)))
        {
            conjunto.Add(crear(id));
        }
    }

    /// <summary>
    /// Los elementos de un catálogo, con su nombre, para los identificadores dados.
    /// </summary>
    public static async Task<IReadOnlyList<ElementoDeClasificacionDto>> PracticasAsync(
        PnmcDbContext db, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.PracticasMusicales.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Orden).ThenBy(p => p.Nombre)
            .Select(p => new ElementoDeClasificacionDto(p.Id, p.Nombre, p.Slug))
            .ToListAsync(ct);
    }

    public static async Task<IReadOnlyList<ElementoDeClasificacionDto>> TerritoriosAsync(
        PnmcDbContext db, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.TerritoriosSonoros.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .OrderBy(t => t.Orden).ThenBy(t => t.Nombre)
            .Select(t => new ElementoDeClasificacionDto(t.Id, t.Nombre, t.Slug))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Las prácticas de cada contenido de una página, resueltas en dos consultas.
    /// </summary>
    /// <remarks>
    /// DOS CONSULTAS PARA LA PAGINA ENTERA Y NO DOS POR FICHA: con veinte eventos por página, la
    /// alternativa son cuarenta viajes a la base para pintar una tabla. Es el mismo criterio con
    /// el que ya se resuelven las etiquetas y los territorios Divipola de la Agenda.
    /// </remarks>
    public static Task<Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>>> AgruparPracticasAsync(
        PnmcDbContext db, IReadOnlyList<(long Contenido, int Elemento)> vinculos, CancellationToken ct) =>
        AgruparAsync(vinculos, ids => PracticasAsync(db, ids, ct));

    public static Task<Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>>> AgruparTerritoriosAsync(
        PnmcDbContext db, IReadOnlyList<(long Contenido, int Elemento)> vinculos, CancellationToken ct) =>
        AgruparAsync(vinculos, ids => TerritoriosAsync(db, ids, ct));

    private static async Task<Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>>> AgruparAsync(
        IReadOnlyList<(long Contenido, int Elemento)> vinculos,
        Func<IReadOnlyList<int>, Task<IReadOnlyList<ElementoDeClasificacionDto>>> leerCatalogo)
    {
        if (vinculos.Count == 0) return [];

        var elementos = await leerCatalogo(vinculos.Select(v => v.Elemento).Distinct().ToList());
        var porId = elementos.ToDictionary(e => e.Id);
        var orden = elementos.Select((e, posicion) => (e.Id, posicion)).ToDictionary(x => x.Id, x => x.posicion);

        return vinculos
            .GroupBy(v => v.Contenido)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => (IReadOnlyList<ElementoDeClasificacionDto>)grupo
                    // EL ORDEN ES EL DEL CATALOGO, no el de inserción: así dos fichas con las
                    // mismas prácticas las enseñan en la misma secuencia y se pueden comparar.
                    .Select(v => v.Elemento)
                    .Where(porId.ContainsKey)
                    .Distinct()
                    .OrderBy(id => orden[id])
                    .Select(id => porId[id])
                    .ToList());
    }

    /// <summary>
    /// Los proyectos transversales con ese identificador, activos o no.
    /// </summary>
    /// <remarks>
    /// SE DEVUELVEN TAMBIEN LOS DESACTIVADOS. Un proyecto que terminó deja de ofrecerse en los
    /// formularios, pero el contenido que se publicó bajo él tiene que seguir diciendo a qué
    /// pertenecía: ocultarlo al leer haría desaparecer la explicación de lo ya publicado.
    /// </remarks>
    public static async Task<IReadOnlyList<ElementoDeClasificacionDto>> ProyectosAsync(
        PnmcDbContext db, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.ProyectosTransversales.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.OrdenVisualizacion).ThenBy(p => p.Nombre)
            .Select(p => new ElementoDeClasificacionDto(p.Id, p.Nombre, p.Codigo))
            .ToListAsync(ct);
    }

    public static Task<Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>>> AgruparProyectosAsync(
        PnmcDbContext db, IReadOnlyList<(long Contenido, int Elemento)> vinculos, CancellationToken ct) =>
        AgruparAsync(vinculos, ids => ProyectosAsync(db, ids, ct));

    /// <summary>
    /// Comprueba que los proyectos transversales pedidos existan y sigan abiertos.
    /// </summary>
    /// <remarks>
    /// SE EXIGE QUE ESTEN ACTIVOS AL ESCRIBIR, no al leer. Enlazar contenido nuevo a una iniciativa
    /// que ya terminó es casi siempre un descuido, y decirlo aquí es más barato que descubrirlo
    /// cuando el calendario de esa iniciativa empiece a crecer sola.
    /// </remarks>
    public static async Task<IResult?> ValidarProyectosAsync(
        PnmcDbContext db, IReadOnlyList<int>? proyectosIds, CancellationToken ct)
    {
        var pedidos = Depurar(proyectosIds);
        if (pedidos.Count == 0) return null;

        var abiertos = await db.ProyectosTransversales.AsNoTracking()
            .Where(p => pedidos.Contains(p.Id) && p.Activo).Select(p => p.Id).ToListAsync(ct);
        var faltan = pedidos.Except(abiertos).ToList();
        if (faltan.Count == 0) return null;

        return Results.BadRequest(new
        {
            proyectosTransversalesIds = new[]
            {
                $"Estos proyectos transversales no existen o ya no están activos: {string.Join(", ", faltan)}.",
            },
        });
    }

    /// <summary>Sin repetidos, sin ceros ni negativos y en orden estable.</summary>
    public static List<int> Depurar(IReadOnlyList<int>? ids) =>
        ids is null ? [] : ids.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
}
