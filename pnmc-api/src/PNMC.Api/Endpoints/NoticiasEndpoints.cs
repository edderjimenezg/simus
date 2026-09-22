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
/// Noticias: consola institucional y consulta pública.
///
/// <para>
/// LA PUERTA DE LO PUBLICO ESTA ESCRITA UNA SOLA VEZ, en
/// <see cref="ReglasDeNoticias.EsVisiblePublicamente"/>: estado publicado y fecha llegada.
/// Repartida entre el listado y el detalle, la primera que alguien olvidara abriría por la mitad
/// una noticia que no debía verse todavía.
/// </para>
/// <para>
/// PUBLICAR NO ES «GUARDAR CON OTRO CAMPO», y por eso el estado tiene su propia ruta. Son dos
/// operaciones con consecuencias distintas: guardar afecta a un borrador, publicar afecta al
/// portal.
/// </para>
/// </summary>
public static class NoticiasEndpoints
{
    private const int TamanoPorOmision = 12;
    private const int TamanoMaximo = 50;

    // Campos y no matrices en linea: el analizador lo exige (CA1861) y ademas el mensaje de un
    // campo obligatorio es el mismo en crear y en guardar, asi que tenerlo dos veces escrito era
    // una forma de que se separaran.
    private static readonly string[] TituloObligatorio = ["El título es obligatorio."];
    private static readonly string[] ResumenObligatorio = ["El resumen es obligatorio."];
    private static readonly string[] TituloSinDireccion = ["El título no produce una dirección válida."];

    public static RouteGroupBuilder MapNoticiasEndpoints(this RouteGroupBuilder api)
    {
        var publico = api.MapGroup("/publico/noticias").WithTags("noticias-publico").AllowAnonymous();
        publico.MapGet(string.Empty, ListarPublicas).WithName("ListarNoticiasPublicas");
        publico.MapGet("/{slug}", ObtenerPublica).WithName("ObtenerNoticiaPublica");

        var consola = api.MapGroup("/institucional/noticias")
            .WithTags("noticias")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("noticias");
        consola.MapGet(string.Empty, ListarInternas).WithName("ListarNoticias");
        consola.MapGet("/{id:long}", ObtenerInterna).WithName("ObtenerNoticia");
        consola.MapPost(string.Empty, Crear).WithName("CrearNoticia");
        consola.MapPut("/{id:long}", Guardar).WithName("GuardarNoticia");
        consola.MapPost("/{id:long}/estado", CambiarEstado).WithName("CambiarEstadoNoticia");

        // COMO SE VERA EN EL LISTADO, antes de publicarla. Ver `PrevisualizacionEnListado` para por
        // qué existe, por qué devuelve el DTO público y por qué exige sesión de consola.
        api.MapGroup(PrevisualizacionEnListado.Prefijo + "/noticias")
            .WithTags("previsualizacion")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .MapGet("/{id:long}", PrevisualizarEnListado)
            .WithName("PrevisualizarNoticiaEnListado")
            .Produces<NoticiaDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return api;
    }

    // ─────────────────────────── Consulta pública ───────────────────────────

    private static async Task<IResult> ListarPublicas(
        PnmcDbContext db, string? q, string? etiqueta, string? categoria, string? proyecto, int? pagina, int? tamano, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var consulta = db.Noticias.AsNoTracking()
            .Where(n => n.Estado == "publicado" && n.FechaPublicacion != null && n.FechaPublicacion <= hoy);

        consulta = AplicarBusqueda(consulta, q);

        // LA SALA DE PRENSA DE UNA INICIATIVA. Sin este filtro, enlazar una noticia con Celebra la
        // Música sería un dato que nadie puede consultar. Se filtra por código y no por
        // identificador: el código es lo que puede ir en una URL.
        var codigoDeProyecto = (proyecto ?? string.Empty).Trim();
        if (codigoDeProyecto.Length > 0)
        {
            var noticias = db.NoticiasProyectosTransversales.AsNoTracking()
                .Where(v => db.ProyectosTransversales.Any(p => p.Id == v.ProyectoTransversalId && p.Codigo == codigoDeProyecto))
                .Select(v => v.NoticiaId);
            consulta = consulta.Where(n => noticias.Contains(n.Id));
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            // SE FILTRA POR NOMBRE Y NO POR IDENTIFICADOR: el portal pone el nombre en la barra de
            // categorías y en la dirección, que es lo que una persona puede leer y compartir.
            var pedida = categoria.Trim();
            var ids = db.Categorias.AsNoTracking().Where(c => c.NombreCategoria == pedida).Select(c => c.Id);
            consulta = consulta.Where(n => n.CategoriaId != null && ids.Contains(n.CategoriaId.Value));
        }

        if (!string.IsNullOrWhiteSpace(etiqueta))
        {
            var termino = etiqueta.Trim();
            var ids = db.EtiquetasNoticia.AsNoTracking().Where(e => e.Termino == termino).Select(e => e.NoticiaId);
            consulta = consulta.Where(n => ids.Contains(n.Id));
        }

        // MAS RECIENTE PRIMERO, POR FECHA DE PUBLICACION y no por cuándo se escribió: es el orden
        // que espera quien entra al portal.
        return Results.Ok(await PaginarAsync(db, consulta.OrderByDescending(n => n.FechaPublicacion).ThenByDescending(n => n.Id), pagina, tamano, ct));
    }

    /// <summary>La noticia con la forma de la lectura pública, esté publicada o no.</summary>
    /// <remarks>
    /// EL MISMO MAPEADOR QUE LA RUTA PUBLICA —<c>ADtoAsync</c>—, y esa es toda la gracia: lo único
    /// que cambia es que aquí no se aplica <c>EsVisiblePublicamente</c>. Armar una forma propia
    /// convertiría la previsualización en una aproximación en cuanto la lectura pública cambiara un
    /// campo, y lo haría en silencio.
    /// </remarks>
    private static async Task<IResult> PrevisualizarEnListado(PnmcDbContext db, long id, CancellationToken ct)
    {
        var fila = await db.Noticias.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        return fila is null ? Results.NotFound() : Results.Ok(await ADtoAsync(db, fila, ct));
    }

    private static async Task<IResult> ObtenerPublica(PnmcDbContext db, string slug, CancellationToken ct)
    {
        var fila = await db.Noticias.AsNoTracking().FirstOrDefaultAsync(n => n.Slug == slug, ct);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        // UN 404 Y NO UN 403 para una noticia que existe pero no se publica todavía: decir «existe
        // pero no puedes verla» filtra el titular de algo que aún no se ha anunciado.
        if (fila is null || !ReglasDeNoticias.EsVisiblePublicamente(fila.Estado, fila.FechaPublicacion, hoy))
        {
            return Results.NotFound();
        }

        return Results.Ok(await ADtoAsync(db, fila, ct));
    }

    // ─────────────────────────── Consola ───────────────────────────

    /// <param name="categoria">
    /// El NOMBRE de la categoría temática, igual que en la Agenda y que en el portal: es lo que una
    /// persona puede leer y compartir, y lo que enseñan las pestañas de la consola.
    ///
    /// SE FILTRA AQUI Y NO EN EL NAVEGADOR porque la lista pagina. Separar la página que se tiene
    /// delante diría «no hay ninguna de esa categoría» cuando lo que pasa es que están en otra.
    /// </param>
    /// <param name="orden">
    /// Por qué columna de la tabla de la consola se ordena: <c>noticia</c>, <c>estado</c> o
    /// <c>fecha</c>. Cualquier otro valor —o ninguno— deja el orden de trabajo, sin publicar
    /// primero. Ver <see cref="OrdenarParaLaConsola"/> para por qué ordena el servidor.
    /// </param>
    /// <param name="direccion"><c>desc</c> invierte; cualquier otra cosa ordena ascendente.</param>
    private static async Task<IResult> ListarInternas(
        PnmcDbContext db, string? q, string? estado, string? categoria,
        string? orden, string? direccion, int? pagina, int? tamano, CancellationToken ct)
    {
        var consulta = AplicarBusqueda(db.Noticias.AsNoTracking(), q);

        if (!string.IsNullOrWhiteSpace(estado) && estado != "todos")
        {
            var pedido = estado.Trim();
            consulta = consulta.Where(n => n.Estado == pedido);
        }

        if (!string.IsNullOrWhiteSpace(categoria) && categoria != "todas")
        {
            // POR NOMBRE Y NO POR IDENTIFICADOR, igual que en la Agenda: ver `AplicarFiltros` allí.
            var pedida = categoria.Trim();
            var idsCategoria = db.Categorias.AsNoTracking().Where(c => c.NombreCategoria == pedida).Select(c => c.Id);
            consulta = consulta.Where(n => n.CategoriaId != null && idsCategoria.Contains(n.CategoriaId.Value));
        }

        return Results.Ok(await PaginarAsync(db, OrdenarParaLaConsola(consulta, orden, direccion), pagina, tamano, ct));
    }

    /// <summary>El orden de la tabla de la consola, resuelto en la base y no en la página.</summary>
    /// <remarks>
    /// <para>
    /// <b>ORDENA EL SERVIDOR PORQUE LA LISTA PAGINA.</b> Ordenar las filas que trae la página no
    /// reordena el listado: lo baraja dentro de su página, y afirma algo falso sobre el resto. El
    /// queda fijado para toda la consola, a partir de lo
    /// que encontró en la Agenda: «debe ser de todos, no solo de los de la página visible».
    /// </para>
    /// <para>
    /// <b>SIN COLUMNA PEDIDA, EL ORDEN DE TRABAJO:</b> sin publicar primero y dentro de eso lo
    /// último tocado. La consola es una mesa de trabajo y ese orden no es el de ninguna columna,
    /// así que la tabla arranca sin flecha en ninguna cabecera.
    /// </para>
    /// <para>
    /// <b>«ESTADO» ORDENA POR EL ESTADO EFECTIVO Y NO POR EL GUARDADO</b>, que es lo que la columna
    /// enseña: una noticia publicada con fecha futura se lee «Programada». Ordenar por el guardado
    /// la pondría entre las publicadas, y la columna parecería desordenada. La misma regla que
    /// <see cref="ReglasDeNoticias.EstadoEfectivo"/>, escrita aquí en forma de consulta porque en
    /// la base no hay columna que la guarde.
    /// </para>
    /// <para>
    /// <b>EL DESEMPATE SIEMPRE ES EL IDENTIFICADOR</b>, o dos noticias del mismo día pueden salir
    /// en distinto orden en dos peticiones iguales y al paginar una se repite y otra se pierde.
    /// </para>
    /// </remarks>
    private static IQueryable<NoticiaRow> OrdenarParaLaConsola(
        IQueryable<NoticiaRow> consulta, string? orden, string? direccion)
    {
        var ascendente = !string.Equals((direccion ?? string.Empty).Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        switch ((orden ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "noticia":
                return ascendente
                    ? consulta.OrderBy(n => n.Titulo).ThenBy(n => n.Id)
                    : consulta.OrderByDescending(n => n.Titulo).ThenBy(n => n.Id);

            case "estado":
                // LA EXPRESION VA ESCRITA AQUI Y NO EN UN METODO AUXILIAR: una llamada a un método
                // propio dentro de un árbol de expresión no se traduce a SQL, y EF la resolvería
                // trayéndose la tabla entera a memoria, que es justo lo que se está quitando.
                return ascendente
                    ? consulta.OrderBy(ClaveDeEstado(hoy)).ThenBy(n => n.Id)
                    : consulta.OrderByDescending(ClaveDeEstado(hoy)).ThenBy(n => n.Id);

            case "fecha":
                // LA FECHA QUE FALTA VA AL FINAL EN LOS DOS SENTIDOS: un borrador sin fecha no es
                // «la noticia más antigua», es una a la que le falta el dato.
                return ascendente
                    ? consulta.OrderBy(n => n.FechaPublicacion == null).ThenBy(n => n.FechaPublicacion).ThenBy(n => n.Id)
                    : consulta.OrderBy(n => n.FechaPublicacion == null).ThenByDescending(n => n.FechaPublicacion).ThenBy(n => n.Id);

            default:
                return consulta
                    .OrderBy(n => n.Estado == "publicado" ? 1 : 0)
                    .ThenByDescending(n => n.FechaActualizacion)
                    .ThenBy(n => n.Id);
        }
    }

    /// <summary>El estado efectivo como clave de orden, en forma de expresión que EF traduce.</summary>
    /// <remarks>
    /// ES UNA EXPRESION Y NO UNA FUNCION: `OrderBy` necesita un árbol que el proveedor sepa pasar a
    /// SQL. Una función corriente obligaría a EF a traerse la tabla y ordenarla en memoria.
    /// </remarks>
    private static System.Linq.Expressions.Expression<Func<NoticiaRow, string>> ClaveDeEstado(DateOnly hoy) =>
        n => n.Estado == "publicado" && n.FechaPublicacion != null && n.FechaPublicacion > hoy
            ? ReglasDeNoticias.Programada
            : n.Estado;

    private static async Task<IResult> ObtenerInterna(PnmcDbContext db, long id, CancellationToken ct)
    {
        var fila = await db.Noticias.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        return fila is null ? Results.NotFound() : Results.Ok(await ADtoAsync(db, fila, ct));
    }

    private static async Task<IResult> Crear(
        PnmcDbContext db, ClaimsPrincipal principal, GuardarNoticiaSolicitud solicitud, CancellationToken ct)
    {
        var titulo = Limpiar(solicitud.Titulo);
        var resumen = Limpiar(solicitud.Resumen);
        if (string.IsNullOrWhiteSpace(titulo)) return Results.BadRequest(new { titulo = TituloObligatorio });
        if (string.IsNullOrWhiteSpace(resumen)) return Results.BadRequest(new { resumen = ResumenObligatorio });

        var clasificacion = await ClasificacionDeContenido.ValidarAsync(
            db, solicitud.PracticasMusicalesIds, solicitud.TerritoriosSonorosIds, ct);
        if (clasificacion is not null) return clasificacion;

        var proyectosInvalidos = await ClasificacionDeContenido.ValidarProyectosAsync(db, solicitud.ProyectosTransversalesIds, ct);
        if (proyectosInvalidos is not null) return proyectosInvalidos;

        var slug = Limpiar(solicitud.Slug) is { Length: > 0 } pedido
            ? ReglasDeNoticias.SlugDesde(pedido)
            : ReglasDeNoticias.SlugDesde(titulo!);
        if (slug.Length == 0) return Results.BadRequest(new { slug = TituloSinDireccion });

        if (await db.Noticias.AnyAsync(n => n.Slug == slug, ct))
        {
            return Results.Conflict(new { slug = new[] { $"Ya existe una noticia en la dirección «{slug}»." } });
        }

        var ahora = DateTime.UtcNow;
        var fila = new NoticiaRow
        {
            Slug = slug,
            Titulo = titulo!,
            Resumen = resumen!,
            Cuerpo = Limpiar(solicitud.Cuerpo),
            FechaPublicacion = solicitud.FechaPublicacion,
            ImagenRuta = LimpiarRuta(solicitud.ImagenRuta),
            ImagenAlternativa = Limpiar(solicitud.ImagenAlternativa),
            AutoriaNombre = Limpiar(solicitud.AutoriaNombre),
            CategoriaId = solicitud.CategoriaId,
            Estado = "borrador",
            Version = 1,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
        };

        // TODO EN UNA TRANSACCION, y con la estrategia de reintento porque la base la usa. Sin
        // esto, un fallo al escribir las etiquetas dejaría la noticia creada con su dirección
        // ocupada para siempre y sin forma de reintentarlo con el mismo título.
        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            db.Noticias.Add(fila);
            await db.SaveChangesAsync(ct);
            await AplicarEtiquetasAsync(db, fila.Id, solicitud.Etiquetas, ct);
            await AplicarClasificacionAsync(db, fila.Id, solicitud, ct);
            await AplicarImagenAsync(db, fila.Id, solicitud.ImagenArchivoId, ct);
            // DE DONDE VIENE: la creó la consola. Misma transacción, para que ninguna noticia
            // pueda quedar sin procedencia.
            await ProcedenciaDeRegistro.AnotarAltaInstitucionalAsync(
                db, Modulos.Noticias, fila.Id.ToString(CultureInfo.InvariantCulture), Actor(principal), ct);
            Auditar(db, Actor(principal), fila.Id, "crear", null, new { fila.Slug, fila.Titulo });
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Created($"/api/v1/institucional/noticias/{fila.Id}", await ADtoAsync(db, fila, ct));
    }

    private static async Task<IResult> Guardar(
        PnmcDbContext db, ClaimsPrincipal principal, long id, GuardarNoticiaSolicitud solicitud, CancellationToken ct)
    {
        var fila = await db.Noticias.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (fila is null) return Results.NotFound();

        // LA VERSION QUE SE ESTABA MIRANDO. Sin esto, dos personas con la misma noticia abierta se
        // pisan y la segunda gana sin que ninguna se entere.
        if (solicitud.Version != fila.Version)
        {
            return Results.Conflict(new { version = new[] { $"La noticia cambió mientras la editabas (versión {fila.Version})." } });
        }

        var titulo = Limpiar(solicitud.Titulo);
        var resumen = Limpiar(solicitud.Resumen);
        if (string.IsNullOrWhiteSpace(titulo)) return Results.BadRequest(new { titulo = TituloObligatorio });
        if (string.IsNullOrWhiteSpace(resumen)) return Results.BadRequest(new { resumen = ResumenObligatorio });

        var clasificacion = await ClasificacionDeContenido.ValidarAsync(
            db, solicitud.PracticasMusicalesIds, solicitud.TerritoriosSonorosIds, ct);
        if (clasificacion is not null) return clasificacion;

        var proyectosInvalidos = await ClasificacionDeContenido.ValidarProyectosAsync(db, solicitud.ProyectosTransversalesIds, ct);
        if (proyectosInvalidos is not null) return proyectosInvalidos;

        var antes = new { fila.Titulo, fila.Resumen, fila.FechaPublicacion };

        // LA DIRECCION NO SE TOCA AL GUARDAR. Un enlace compartido no puede dejar de funcionar
        // porque alguien corrigiera una tilde del título.
        fila.Titulo = titulo!;
        fila.Resumen = resumen!;
        fila.Cuerpo = Limpiar(solicitud.Cuerpo);
        fila.FechaPublicacion = solicitud.FechaPublicacion;
        fila.ImagenRuta = LimpiarRuta(solicitud.ImagenRuta);
        fila.ImagenAlternativa = Limpiar(solicitud.ImagenAlternativa);
        fila.AutoriaNombre = Limpiar(solicitud.AutoriaNombre);
        fila.CategoriaId = solicitud.CategoriaId;
        fila.Version += 1;
        fila.FechaActualizacion = DateTime.UtcNow;

        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            await AplicarEtiquetasAsync(db, fila.Id, solicitud.Etiquetas, ct);
            await AplicarClasificacionAsync(db, fila.Id, solicitud, ct);
            // `ImagenArchivoId` nulo puede significar «retírala» o «no la toques»: solo el cuerpo
            // lo dice, y aquí se traduce a una intención explícita antes de escribir nada.
            if (solicitud.ImagenArchivoId is not null || solicitud.RetirarImagen)
            {
                await AplicarImagenAsync(db, fila.Id, solicitud.ImagenArchivoId, ct);
            }
            Auditar(db, Actor(principal), fila.Id, "actualizar", antes, new { fila.Titulo, fila.Resumen, fila.FechaPublicacion });
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Ok(await ADtoAsync(db, fila, ct));
    }

    private static async Task<IResult> CambiarEstado(
        PnmcDbContext db, ClaimsPrincipal principal, long id, CambiarEstadoNoticiaSolicitud solicitud, CancellationToken ct)
    {
        var estado = Limpiar(solicitud.Estado) ?? string.Empty;
        if (!ReglasDeNoticias.Estados.Contains(estado))
        {
            return Results.BadRequest(new { estado = new[] { $"«{estado}» no es un estado de noticia." } });
        }

        var fila = await db.Noticias.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (fila is null) return Results.NotFound();

        if (estado == "publicado")
        {
            var faltas = ReglasDeNoticias.LoQueFaltaParaPublicar(fila.Titulo, fila.Resumen, fila.Cuerpo, fila.FechaPublicacion);
            if (faltas.Count > 0)
            {
                return Results.BadRequest(new { estado = new[] { "No se puede publicar: " + string.Join("; ", faltas) + "." } });
            }
        }

        var anterior = fila.Estado;
        fila.Estado = estado;
        fila.FechaActualizacion = DateTime.UtcNow;

        // EL VERBO SALE DE LA LISTA BLANCA DE LA BITACORA. `CK_BitacoraAuditoria_Accion` no admite
        // «en_revision» ni «borrador»: un cambio a esos estados se registra como una actualización.
        var verbo = estado switch
        {
            "publicado" => "publicar",
            "archivado" => "archivar",
            _ => "actualizar",
        };
        Auditar(db, Actor(principal), fila.Id, verbo, new { Estado = anterior }, new { Estado = estado });
        await db.SaveChangesAsync(ct);

        return Results.Ok(await ADtoAsync(db, fila, ct));
    }

    // ─────────────────────────── Ayudas ───────────────────────────

    private static IQueryable<NoticiaRow> AplicarBusqueda(IQueryable<NoticiaRow> consulta, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return consulta;
        var termino = q.Trim();
        return consulta.Where(n => n.Titulo.Contains(termino) || n.Resumen.Contains(termino));
    }

    private static async Task<PaginaNoticiasDto> PaginarAsync(
        PnmcDbContext db, IQueryable<NoticiaRow> consulta, int? pagina, int? tamano, CancellationToken ct)
    {
        var paginaPedida = Math.Max(1, pagina ?? 1);
        var tamanoPedido = Math.Clamp(tamano ?? TamanoPorOmision, 1, TamanoMaximo);

        var total = await consulta.CountAsync(ct);
        var filas = await consulta.Skip((paginaPedida - 1) * tamanoPedido).Take(tamanoPedido).ToListAsync(ct);

        // LAS ETIQUETAS DE TODA LA PAGINA EN UNA CONSULTA, no una por noticia: con doce noticias
        // eso serían doce viajes a la base para pintar una lista.
        var ids = filas.Select(f => f.Id).ToList();
        var categorias = await ResolverCategoriasAsync(db, filas.Select(f => f.CategoriaId), ct);
        var etiquetas = await db.EtiquetasNoticia.AsNoTracking()
            .Where(e => ids.Contains(e.NoticiaId))
            .ToListAsync(ct);

        var (practicas, territoriosSonoros) = await ResolverClasificacionAsync(db, ids, ct);
        var imagenes = await ResolverImagenesAsync(db, ids, ct);
        var procedencias = await ProcedenciaDeRegistro.LeerVariasAsync(
            db, Modulos.Noticias, ids.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList(), ct);
        var proyectos = await ResolverProyectosAsync(db, ids, ct);

        var items = filas
            .Select(f => ADto(
                f,
                etiquetas.Where(e => e.NoticiaId == f.Id).Select(e => e.Termino).OrderBy(t => t, StringComparer.Ordinal).ToList(),
                categorias.TryGetValue(f.CategoriaId ?? 0, out var nombre) ? nombre : null,
                practicas.TryGetValue(f.Id, out var suyasPracticas) ? suyasPracticas : [],
                territoriosSonoros.TryGetValue(f.Id, out var suyosTerritorios) ? suyosTerritorios : [],
                // EL TERNARIO NO ES ADORNO: `TryGetValue` con `out var` sobre una tupla de VALOR
                // deja `(0, null)` cuando no encuentra nada, y eso viaja como un identificador 0
                // con una dirección muerta. Lo cazó un defecto real de la Agenda.
                imagenes.TryGetValue(f.Id, out var imagen) ? imagen : ((int, string?)?)null,
                procedencias.TryGetValue(f.Id.ToString(CultureInfo.InvariantCulture), out var suProcedencia) ? suProcedencia : null,
                proyectos.TryGetValue(f.Id, out var susProyectos) ? susProyectos : []))
            .ToList();

        var totalPaginas = total == 0 ? 0 : (int)Math.Ceiling(total / (double)tamanoPedido);
        return new PaginaNoticiasDto(items, paginaPedida, tamanoPedido, total, totalPaginas);
    }

    private static async Task<NoticiaDto> ADtoAsync(PnmcDbContext db, NoticiaRow fila, CancellationToken ct)
    {
        var etiquetas = await db.EtiquetasNoticia.AsNoTracking()
            .Where(e => e.NoticiaId == fila.Id)
            .Select(e => e.Termino)
            .OrderBy(t => t)
            .ToListAsync(ct);
        var categorias = await ResolverCategoriasAsync(db, [fila.CategoriaId], ct);
        var (practicas, territoriosSonoros) = await ResolverClasificacionAsync(db, [fila.Id], ct);
        var imagenes = await ResolverImagenesAsync(db, [fila.Id], ct);
        return ADto(
            fila, etiquetas, categorias.TryGetValue(fila.CategoriaId ?? 0, out var nombre) ? nombre : null,
            practicas.TryGetValue(fila.Id, out var suyasPracticas) ? suyasPracticas : [],
            territoriosSonoros.TryGetValue(fila.Id, out var suyosTerritorios) ? suyosTerritorios : [],
            imagenes.TryGetValue(fila.Id, out var imagen) ? imagen : ((int, string?)?)null,
            await ProcedenciaDeRegistro.LeerAsync(
                db, Modulos.Noticias, fila.Id.ToString(CultureInfo.InvariantCulture), ct),
            (await ResolverProyectosAsync(db, [fila.Id], ct)).TryGetValue(fila.Id, out var susProyectos) ? susProyectos : []);
    }

    private static NoticiaDto ADto(
        NoticiaRow fila,
        IReadOnlyList<string> etiquetas,
        string? categoria,
        IReadOnlyList<ElementoDeClasificacionDto>? practicas = null,
        IReadOnlyList<ElementoDeClasificacionDto>? territoriosSonoros = null,
        (int Id, string? Alt)? imagen = null,
        ProcedenciaDeRegistroDto? procedencia = null,
        IReadOnlyList<ElementoDeClasificacionDto>? proyectos = null) => new(
        fila.Id, fila.Slug, fila.Titulo, fila.Resumen, fila.Cuerpo, fila.FechaPublicacion,
        fila.ImagenRuta, fila.ImagenAlternativa, fila.AutoriaNombre,
        imagen?.Id,
        // LA DIRECCION SE CONSTRUYE AQUI y no se guarda: el identificador es el dato, la ruta es
        // cómo se sirve, y si mañana cambia la ruta no hay que reescribir ninguna fila.
        imagen is { } enlazada ? "/api/v1/publico/archivos/" + enlazada.Id.ToString(CultureInfo.InvariantCulture) : null,
        // El texto alternativo del banco manda; el suelto de la fila sirve de respaldo mientras
        // queden noticias cargadas con ruta a mano.
        imagen?.Alt ?? fila.ImagenAlternativa,
        fila.CategoriaId, categoria, fila.Estado,
        // EL ESTADO EFECTIVO SE CALCULA AQUI, con la fecha del servidor: una noticia publicada con
        // fecha futura está «programada», y el navegador no puede deducirlo porque no conoce esa
        // fecha. La regla vive junto a la que decide la visibilidad pública, que es la misma idea.
        ReglasDeNoticias.EstadoEfectivo(fila.Estado, fila.FechaPublicacion, DateOnly.FromDateTime(DateTime.UtcNow)),
        fila.Version,
        etiquetas, practicas ?? [], territoriosSonoros ?? [], procedencia, proyectos ?? [], fila.FechaActualizacion);

    private const string RolImagenPrincipal = "imagen_principal";

    /// <summary>
    /// La imagen principal de cada noticia de la página, en una consulta.
    /// </summary>
    private static async Task<Dictionary<long, (int Id, string? Alt)>> ResolverImagenesAsync(
        PnmcDbContext db, List<long> noticias, CancellationToken ct)
    {
        if (noticias.Count == 0) return [];

        var filas = await db.NoticiasArchivos.AsNoTracking()
            .Where(x => noticias.Contains(x.NoticiaId) && x.RolArchivo == RolImagenPrincipal)
            .Join(db.Files.AsNoTracking(), v => v.ArchivoId, a => a.Id, (v, a) => new { v.NoticiaId, a.Id, a.AltText })
            .ToListAsync(ct);

        return filas
            .GroupBy(x => x.NoticiaId)
            .ToDictionary(g => g.Key, g => (g.First().Id, (string?)g.First().AltText));
    }

    /// <summary>
    /// Deja vinculada la imagen principal de la noticia.
    /// </summary>
    /// <remarks>
    /// SE BORRA EL VINCULO, NO EL ARCHIVO. El archivo sigue en el banco: puede estar en uso en
    /// otra ficha, y borrarlo desde aquí dejaría un hueco en una pantalla que nadie está mirando.
    /// </remarks>
    private static async Task AplicarImagenAsync(
        PnmcDbContext db, long noticiaId, int? archivoId, CancellationToken ct)
    {
        var existentes = await db.NoticiasArchivos
            .Where(x => x.NoticiaId == noticiaId && x.RolArchivo == RolImagenPrincipal)
            .ToListAsync(ct);

        db.NoticiasArchivos.RemoveRange(existentes);

        if (archivoId is { } id && id > 0)
        {
            db.NoticiasArchivos.Add(new NoticiaArchivoRow
            {
                NoticiaId = noticiaId,
                ArchivoId = id,
                RolArchivo = RolImagenPrincipal,
                OrdenVisualizacion = 1,
            });
        }
    }

    /// <summary>
    /// Las prácticas musicales y los territorios sonoros de las noticias dadas.
    /// </summary>
    /// <remarks>
    /// DOS CONSULTAS PARA LA PAGINA ENTERA, igual que las etiquetas: la alternativa es dos viajes
    /// más a la base por cada noticia de la lista.
    /// </remarks>
    private static async Task<(
        Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>> Practicas,
        Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>> Territorios)> ResolverClasificacionAsync(
        PnmcDbContext db, List<long> noticias, CancellationToken ct)
    {
        if (noticias.Count == 0) return ([], []);

        var vinculosPracticas = await db.NoticiasPracticasMusicales.AsNoTracking()
            .Where(x => noticias.Contains(x.NoticiaId))
            .Select(x => new { x.NoticiaId, x.PracticaMusicalId }).ToListAsync(ct);
        var vinculosTerritorios = await db.NoticiasTerritoriosSonoros.AsNoTracking()
            .Where(x => noticias.Contains(x.NoticiaId))
            .Select(x => new { x.NoticiaId, x.TerritorioSonoroId }).ToListAsync(ct);

        return (
            await ClasificacionDeContenido.AgruparPracticasAsync(
                db, vinculosPracticas.Select(v => (v.NoticiaId, v.PracticaMusicalId)).ToList(), ct),
            await ClasificacionDeContenido.AgruparTerritoriosAsync(
                db, vinculosTerritorios.Select(v => (v.NoticiaId, v.TerritorioSonoroId)).ToList(), ct));
    }

    /// <summary>Las iniciativas del Programa a las que pertenece cada noticia de la página.</summary>
    private static async Task<Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>>> ResolverProyectosAsync(
        PnmcDbContext db, List<long> noticias, CancellationToken ct)
    {
        if (noticias.Count == 0) return [];

        var vinculos = await db.NoticiasProyectosTransversales.AsNoTracking()
            .Where(x => noticias.Contains(x.NoticiaId))
            .Select(x => new { x.NoticiaId, x.ProyectoTransversalId }).ToListAsync(ct);

        return await ClasificacionDeContenido.AgruparProyectosAsync(
            db, vinculos.Select(v => (v.NoticiaId, v.ProyectoTransversalId)).ToList(), ct);
    }

    /// <summary>Deja la clasificación de la noticia diciendo lo que pide la solicitud.</summary>
    private static async Task AplicarClasificacionAsync(
        PnmcDbContext db, long noticiaId, GuardarNoticiaSolicitud solicitud, CancellationToken ct)
    {
        var practicas = await db.NoticiasPracticasMusicales.Where(x => x.NoticiaId == noticiaId).ToListAsync(ct);
        ClasificacionDeContenido.Reconciliar(
            db.NoticiasPracticasMusicales, practicas, x => x.PracticaMusicalId, solicitud.PracticasMusicalesIds,
            id => new NoticiaPracticaMusicalRow { NoticiaId = noticiaId, PracticaMusicalId = id });

        var territorios = await db.NoticiasTerritoriosSonoros.Where(x => x.NoticiaId == noticiaId).ToListAsync(ct);
        ClasificacionDeContenido.Reconciliar(
            db.NoticiasTerritoriosSonoros, territorios, x => x.TerritorioSonoroId, solicitud.TerritoriosSonorosIds,
            id => new NoticiaTerritorioSonoroRow { NoticiaId = noticiaId, TerritorioSonoroId = id });

        var proyectos = await db.NoticiasProyectosTransversales.Where(x => x.NoticiaId == noticiaId).ToListAsync(ct);
        ClasificacionDeContenido.Reconciliar(
            db.NoticiasProyectosTransversales, proyectos, x => x.ProyectoTransversalId, solicitud.ProyectosTransversalesIds,
            id => new NoticiaProyectoTransversalRow { NoticiaId = noticiaId, ProyectoTransversalId = id });
    }

    /// <summary>
    /// Deja las etiquetas de la noticia exactamente como vienen.
    /// </summary>
    /// <remarks>
    /// SE BORRAN Y SE REESCRIBEN, que para un puñado de términos es más simple y más correcto que
    /// calcular diferencias. Un `null` significa «no toques las etiquetas»; una lista vacía
    /// significa «quítalas todas», y son dos cosas distintas.
    /// </remarks>
    private static async Task AplicarEtiquetasAsync(
        PnmcDbContext db, long noticiaId, IReadOnlyList<string>? etiquetas, CancellationToken ct)
    {
        if (etiquetas is null) return;

        var existentes = await db.EtiquetasNoticia.Where(e => e.NoticiaId == noticiaId).ToListAsync(ct);
        db.EtiquetasNoticia.RemoveRange(existentes);

        var limpias = etiquetas
            .Select(e => (e ?? string.Empty).Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var termino in limpias)
        {
            db.EtiquetasNoticia.Add(new EtiquetaNoticiaRow { NoticiaId = noticiaId, Termino = termino });
        }
    }


    /// <summary>
    /// El nombre de cada categoría de la página, en una consulta.
    /// </summary>
    /// <remarks>
    /// EL IDENTIFICADOR ES EL DATO Y EL NOMBRE ES COMO SE LEE. Se resuelve al servir, no se guarda
    /// junto al identificador: dos columnas con la misma verdad acaban discrepando, y la que
    /// envejece es siempre la que nadie mira.
    /// </remarks>
    private static async Task<Dictionary<int, string>> ResolverCategoriasAsync(
        PnmcDbContext db, IEnumerable<int?> identificadores, CancellationToken ct)
    {
        var ids = identificadores.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];

        return await db.Categorias.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.NombreCategoria, ct);
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Deja la ruta de la imagen referida a la raíz del sitio; una externa no se toca.</summary>
    private static string? LimpiarRuta(string? valor)
    {
        var limpio = Limpiar(valor);
        if (limpio is null || limpio.StartsWith('/')) return limpio;
        var esExterna = limpio.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || limpio.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || limpio.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        return esExterna ? limpio : "/" + limpio;
    }

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : 0;

    private static void Auditar(PnmcDbContext db, int actor, long id, string accion, object? antes, object? despues) =>
        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = actor,
            TableName = "Noticias",
            RecordId = id.ToString(CultureInfo.InvariantCulture),
            Action = accion,
            PreviousValuesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            NewValuesJson = despues is null ? null : JsonSerializer.Serialize(despues),
            CreatedAt = DateTime.UtcNow,
        });
}
