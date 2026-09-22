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
/// Agenda: consola institucional y consulta pública.
///
/// <para>
/// LO PROXIMO PRIMERO EN EL PORTAL, Y LO RECIENTE PRIMERO ENTRE LO PASADO. Quien entra a una agenda
/// quiere saber qué viene; quien consulta lo que ya ocurrió quiere lo último. Son dos órdenes
/// distintos y el servidor los aplica según lo que se pida, en vez de dejar un único orden que
/// sirva a medias para los dos.
/// </para>
/// <para>
/// UN EVENTO PASADO NO DESAPARECE, a diferencia de una noticia futura que espera su fecha. Lo que
/// ya ocurrió sigue siendo información pública.
/// </para>
/// </summary>
public static class AgendaEndpoints
{
    private const int TamanoPorOmision = 12;
    private const int TamanoMaximo = 50;

    private static readonly string[] TituloObligatorio = ["El título es obligatorio."];
    private static readonly string[] DescripcionObligatoria = ["La descripción es obligatoria."];
    private static readonly string[] TituloSinDireccion = ["El título no produce una dirección válida."];
    private static readonly string[] FinAntesDelInicio = ["El evento no puede terminar antes de empezar."];
    private static readonly string[] HoraFinAntes = ["La hora de fin no puede ser anterior a la de inicio."];
    private static readonly string[] OrdenPositivo = ["El orden de visualización empieza en 1."];
    private static readonly string[] ImagenInvalida = ["El identificador de la imagen no es válido."];

    public static RouteGroupBuilder MapAgendaEndpoints(this RouteGroupBuilder api)
    {
        var publico = api.MapGroup("/publico/agenda").WithTags("agenda-publico").AllowAnonymous();
        publico.MapGet(string.Empty, ListarPublicos).WithName("ListarEventosAgendaPublicos");
        publico.MapGet("/{slug}", ObtenerPublico).WithName("ObtenerEventoAgendaPublico");

        var consola = api.MapGroup("/institucional/agenda")
            .WithTags("agenda")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("agenda");
        consola.MapGet(string.Empty, ListarInternos).WithName("ListarEventosAgenda");
        consola.MapGet("/{id:long}", ObtenerInterno).WithName("ObtenerEventoAgenda");
        consola.MapPost(string.Empty, Crear).WithName("CrearEventoAgenda");
        // LEER UN AFICHE Y PROPONER. No crea ningún evento: devuelve con qué abrir el formulario.
        consola.MapPost("/desde-documento", LeerDocumento).WithName("LeerDocumentoAgenda").DisableAntiforgery().AdmiteDocumentos();
        consola.MapPut("/{id:long}", Guardar).WithName("GuardarEventoAgenda");
        consola.MapPost("/{id:long}/estado", CambiarEstado).WithName("CambiarEstadoEventoAgenda");

        // COMO SE VERA EN EL LISTADO, antes de publicarlo. Ver `PrevisualizacionEnListado`.
        api.MapGroup(PrevisualizacionEnListado.Prefijo + "/agenda")
            .WithTags("previsualizacion")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .MapGet("/{id:long}", PrevisualizarEnListado)
            .WithName("PrevisualizarEventoAgendaEnListado")
            .Produces<EventoAgendaDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return api;
    }

    // ─────────────────────────── Consulta pública ───────────────────────────

    private static async Task<IResult> ListarPublicos(
        PnmcDbContext db, string? q, string? etiqueta, string? departamento, string? categoria, string? proyecto, string? cuando,
        int? pagina, int? tamano, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var consulta = db.EventosAgenda.AsNoTracking().Where(e => e.Estado == "publicado");

        consulta = AplicarFiltros(consulta, db, q, etiqueta, departamento, categoria);
        consulta = AplicarProyecto(consulta, db, proyecto);

        // `cuando` REPARTE EL TIEMPO EN DOS MITADES Y CAMBIA EL ORDEN CON ELLAS. «proximos» incluye
        // los que están en curso: un festival que empezó ayer y acaba el domingo sigue siendo algo
        // a lo que se puede ir.
        var pedido = (cuando ?? "proximos").Trim().ToLowerInvariant();
        IQueryable<EventoAgendaRow> ordenada;
        if (pedido == "pasados")
        {
            ordenada = consulta
                .Where(e => (e.FechaFin ?? e.FechaInicio) < hoy)
                .OrderByDescending(e => e.FechaInicio).ThenByDescending(e => e.Id);
        }
        else if (pedido == "todos")
        {
            ordenada = consulta.OrderByDescending(e => e.FechaInicio).ThenByDescending(e => e.Id);
        }
        else
        {
            ordenada = consulta
                .Where(e => (e.FechaFin ?? e.FechaInicio) >= hoy)
                .OrderBy(e => e.FechaInicio).ThenBy(e => e.Id);
        }

        return Results.Ok(await PaginarAsync(db, ordenada, pagina, tamano, hoy, ct));
    }

    private static async Task<IResult> ObtenerPublico(PnmcDbContext db, string slug, CancellationToken ct)
    {
        var fila = await db.EventosAgenda.AsNoTracking().FirstOrDefaultAsync(e => e.Slug == slug, ct);

        // UN 404 Y NO UN 403 para un evento que existe sin publicar: decir «existe pero no puedes
        // verlo» filtra que se está preparando algo que no se ha anunciado.
        if (fila is null || !ReglasDeAgenda.EsVisiblePublicamente(fila.Estado)) return Results.NotFound();

        return Results.Ok(await ADtoAsync(db, fila, ct));
    }

    // ─────────────────────────── Consola ───────────────────────────

    /// <param name="incluirArchivados">
    /// Si la lista trae también lo archivado. Por omisión SI, para no cambiar lo que ya responde a
    /// quien no lo pida; la consola pide explícitamente que no, porque un archivo cerrado no es
    /// trabajo pendiente y con 471 eventos ocupa sitio sin decir nada.
    /// </param>
    /// <param name="procedencia">
    /// De quién es el evento: <c>institucional</c> —todo lo que no entró desde el espacio de una
    /// organización— o <c>externo</c>. Es la misma separación que ya usa el Banco de archivos, y
    /// la pidió criterio: «el apartado de agenda debería
    /// tener dos pestañas, la institucional o administrativa y la de externos».
    ///
    /// SE FILTRA AQUI Y NO EN EL NAVEGADOR porque la lista pagina de doce en doce: separar la
    /// página que se tiene delante diría «no hay ninguno externo» cuando lo que pasa es que están
    /// en otra página.
    /// </param>
    /// <param name="orden">
    /// Por qué columna de la tabla de la consola se ordena: <c>actividad</c>, <c>cuando</c>,
    /// <c>donde</c> o <c>estado</c>. Cualquier otro valor —o ninguno— deja el orden de trabajo.
    ///
    /// ORDENA EL SERVIDOR Y NO EL NAVEGADOR, y esa es la razón de que exista este parámetro. La
    /// consola pinta doce filas de cuarenta páginas: ordenar las doce que se tienen delante no
    /// reordena la lista, la baraja dentro de su página. se detectó el 15 de
    /// septiembre de 2026: «al cliquear en el nombre de la columna debe ser de todos, no solo de
    /// los de la página visible, por eso en la página 1 no me aparecía nunca ningún publicado».
    /// </param>
    /// <param name="direccion"><c>desc</c> invierte; cualquier otra cosa ordena ascendente.</param>
    private static async Task<IResult> ListarInternos(
        PnmcDbContext db, string? q, string? estado, string? departamento, string? categoria,
        string? procedencia, bool? incluirArchivados, string? orden, string? direccion,
        int? pagina, int? tamano, CancellationToken ct)
    {
        var consulta = AplicarFiltros(db.EventosAgenda.AsNoTracking(), db, q, null, departamento, categoria);

        var quien = (procedencia ?? string.Empty).Trim().ToLowerInvariant();
        if (quien is "externo" or "institucional")
        {
            // LO EXTERNO ES LO QUE DICE LA PROCEDENCIA; el resto —administrativo, importación,
            // siembra, histórico y lo que todavía no tiene fila— es del Programa. Se decide así y
            // no listando contextos «institucionales» para que un contexto nuevo no se cuele en la
            // pestaña equivocada por olvido.
            var deOrganizaciones = db.ProcedenciasDeRegistro.AsNoTracking()
                .Where(x => x.ModuloId == Modulos.Agenda && x.ContextoOrigen == ProcedenciaDeRegistro.Externo)
                .Select(x => x.RegistroId);

            consulta = quien == "externo"
                ? consulta.Where(e => deOrganizaciones.Contains(e.Id.ToString()))
                : consulta.Where(e => !deOrganizaciones.Contains(e.Id.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(estado) && estado != "todos")
        {
            var pedido = estado.Trim();
            consulta = consulta.Where(e => e.Estado == pedido);
        }
        else if (incluirArchivados == false)
        {
            // SOLO CUANDO NO SE PIDE UN ESTADO CONCRETO: quien pide «archivado» expresamente quiere
            // verlos, y ocultárselos sería devolver una lista vacía sin explicación.
            consulta = consulta.Where(e => e.Estado != "archivado");
        }

        var ordenada = OrdenarParaLaConsola(db, consulta, orden, direccion);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return Results.Ok(await PaginarAsync(db, ordenada, pagina, tamano, hoy, ct));
    }

    /// <summary>El orden de la tabla de la consola, resuelto en la base y no en la página.</summary>
    /// <remarks>
    /// <para>
    /// <b>SIN COLUMNA PEDIDA, EL ORDEN DE TRABAJO:</b> sin publicar primero y dentro de eso lo más
    /// próximo. La consola es una mesa de trabajo y ese orden no es el de ninguna columna, por lo
    /// que la tabla arranca sin flecha en ninguna cabecera; poner una diría que la lista está
    /// ordenada por ese dato, y no lo está.
    /// </para>
    /// <para>
    /// <b>UNA COLUMNA QUE NO EXISTE NO ES UN 400.</b> Es un enlace guardado, una petición vieja o
    /// un nombre mal escrito, y la agenda tiene que seguir respondiendo. Cae al orden de trabajo.
    /// </para>
    /// <para>
    /// <b>EL DESEMPATE SIEMPRE ES EL IDENTIFICADOR.</b> Sin él, dos eventos del mismo día pueden
    /// salir en distinto orden en dos peticiones iguales, y al paginar eso repite una fila en una
    /// página y se salta otra.
    /// </para>
    /// <para>
    /// <b>«DONDE» ORDENA POR LO QUE SE LEE EN LA CELDA</b>, que es el lugar cuando lo hay y el
    /// departamento cuando no: ordenar solo por el lugar dejaría desordenadas todas las filas
    /// virtuales, que es justo donde la columna enseña el departamento.
    /// </para>
    /// <para>
    /// <b>«ESTADO» ORDENA POR EL CODIGO</b> —archivado, borrador, en_revision, publicado—, que en
    /// castellano cae en el mismo orden que los rótulos que se ven: Archivado, Borrador, En
    /// revisión, Publicado. Se comprueba en las pruebas para que un estado nuevo no lo rompa en
    /// silencio.
    /// </para>
    /// </remarks>
    private static IQueryable<EventoAgendaRow> OrdenarParaLaConsola(
        PnmcDbContext db, IQueryable<EventoAgendaRow> consulta, string? orden, string? direccion)
    {
        var ascendente = !string.Equals((direccion ?? string.Empty).Trim(), "desc", StringComparison.OrdinalIgnoreCase);

        switch ((orden ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "actividad":
                return ascendente
                    ? consulta.OrderBy(e => e.Titulo).ThenBy(e => e.Id)
                    : consulta.OrderByDescending(e => e.Titulo).ThenBy(e => e.Id);

            case "cuando":
                return ascendente
                    ? consulta.OrderBy(e => e.FechaInicio).ThenBy(e => e.Id)
                    : consulta.OrderByDescending(e => e.FechaInicio).ThenBy(e => e.Id);

            case "donde":
                return ascendente
                    ? consulta.OrderBy(ClaveDeDonde(db)).ThenBy(e => e.Id)
                    : consulta.OrderByDescending(ClaveDeDonde(db)).ThenBy(e => e.Id);

            case "estado":
                return ascendente
                    ? consulta.OrderBy(e => e.Estado).ThenBy(e => e.Id)
                    : consulta.OrderByDescending(e => e.Estado).ThenBy(e => e.Id);

            default:
                return consulta
                    .OrderBy(e => e.Estado == "publicado" ? 1 : 0)
                    .ThenBy(e => e.FechaInicio)
                    .ThenBy(e => e.Id);
        }
    }

    /// <summary>Lo que la columna «Dónde» enseña: el lugar, y si no lo hay, el departamento.</summary>
    /// <remarks>
    /// DEVUELVE CADENA VACIA Y NO NULO A PROPOSITO: SQL Server y SQLite no colocan los nulos en el
    /// mismo sitio al ordenar. Con la cadena vacía, los eventos sin sitio quedan juntos al
    /// principio en ascendente y al final en descendente, igual en los dos motores.
    /// </remarks>
    private static System.Linq.Expressions.Expression<Func<EventoAgendaRow, string>> ClaveDeDonde(PnmcDbContext db) =>
        e => e.Lugar != null && e.Lugar != string.Empty
            ? e.Lugar
            : (db.DivipolaLocations
                .Where(l => l.DepartmentCode == e.CodigoDepartamento)
                .Select(l => l.DepartmentName)
                .FirstOrDefault() ?? string.Empty);

    /// <summary>El evento con la forma de la lectura pública, esté publicado o no.</summary>
    /// <remarks>EL MISMO MAPEADOR QUE LA RUTA PUBLICA. Ver <see cref="PrevisualizacionEnListado"/>.</remarks>
    private static async Task<IResult> PrevisualizarEnListado(PnmcDbContext db, long id, CancellationToken ct)
    {
        var fila = await db.EventosAgenda.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return fila is null ? Results.NotFound() : Results.Ok(await ADtoAsync(db, fila, ct));
    }

    private static async Task<IResult> ObtenerInterno(PnmcDbContext db, long id, CancellationToken ct)
    {
        var fila = await db.EventosAgenda.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return fila is null ? Results.NotFound() : Results.Ok(await ADtoAsync(db, fila, ct));
    }

    private static async Task<IResult> Crear(
        PnmcDbContext db, ClaimsPrincipal principal, GuardarEventoAgendaSolicitud solicitud, CancellationToken ct)
    {
        var validacion = Validar(solicitud);
        if (validacion is not null) return validacion;

        var clasificacion = await ClasificacionDeContenido.ValidarAsync(
            db, solicitud.PracticasMusicalesIds, solicitud.TerritoriosSonorosIds, ct);
        if (clasificacion is not null) return clasificacion;

        var proyectosInvalidos = await ClasificacionDeContenido.ValidarProyectosAsync(db, solicitud.ProyectosTransversalesIds, ct);
        if (proyectosInvalidos is not null) return proyectosInvalidos;

        var titulo = Limpiar(solicitud.Titulo)!;
        var slug = Limpiar(solicitud.Slug) is { Length: > 0 } pedido
            ? ReglasDeAgenda.SlugDesde(pedido)
            : ReglasDeAgenda.SlugDesde(titulo);
        if (slug.Length == 0) return Results.BadRequest(new { slug = TituloSinDireccion });

        if (await db.EventosAgenda.AnyAsync(e => e.Slug == slug, ct))
        {
            return Results.Conflict(new { slug = new[] { $"Ya existe un evento en la dirección «{slug}»." } });
        }

        var ahora = DateTime.UtcNow;
        var fila = new EventoAgendaRow
        {
            Slug = slug,
            Estado = "borrador",
            Version = 1,
            FechaCreacion = ahora,
            FechaActualizacion = ahora,
        };
        Volcar(fila, solicitud);

        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            db.EventosAgenda.Add(fila);
            await db.SaveChangesAsync(ct);
            await AplicarEtiquetasAsync(db, fila.Id, solicitud.Etiquetas, ct);
            await AplicarImagenAsync(db, fila.Id, solicitud.ImagenArchivoId, ct);
            await AplicarClasificacionAsync(db, fila.Id, solicitud, ct);
            // DE DONDE VIENE: lo creó la consola, así que la procedencia es la entidad
            // institucional y el ejecutor, la cuenta que tiene la sesión. Se anota dentro de la
            // misma transacción para que no pueda quedar un evento sin procedencia.
            await ProcedenciaDeRegistro.AnotarAltaInstitucionalAsync(
                db, Modulos.Agenda, fila.Id.ToString(CultureInfo.InvariantCulture), Actor(principal), ct);
            Auditar(db, Actor(principal), fila.Id, "crear", null, new { fila.Slug, fila.Titulo });
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Created($"/api/v1/institucional/agenda/{fila.Id}", await ADtoAsync(db, fila, ct));
    }

    private static async Task<IResult> Guardar(
        PnmcDbContext db, ClaimsPrincipal principal, long id, GuardarEventoAgendaSolicitud solicitud, CancellationToken ct)
    {
        var fila = await db.EventosAgenda.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (fila is null) return Results.NotFound();

        if (solicitud.Version != fila.Version)
        {
            return Results.Conflict(new { version = new[] { $"El evento cambió mientras lo editabas (versión {fila.Version})." } });
        }

        var validacion = Validar(solicitud);
        if (validacion is not null) return validacion;

        var clasificacion = await ClasificacionDeContenido.ValidarAsync(
            db, solicitud.PracticasMusicalesIds, solicitud.TerritoriosSonorosIds, ct);
        if (clasificacion is not null) return clasificacion;

        var proyectosInvalidos = await ClasificacionDeContenido.ValidarProyectosAsync(db, solicitud.ProyectosTransversalesIds, ct);
        if (proyectosInvalidos is not null) return proyectosInvalidos;

        var antes = new { fila.Titulo, fila.FechaInicio, fila.Modalidad };
        // `ImagenArchivoId` nulo puede significar «retírala» o «no la toques»: solo el cuerpo lo
        // dice, y aquí se traduce a una intención explícita antes de escribir nada.
        var pideCambiarImagen = solicitud.ImagenArchivoId is not null || solicitud.RetirarImagen;

        // LA DIRECCION NO SE TOCA AL GUARDAR: un enlace compartido no puede dejar de funcionar.
        Volcar(fila, solicitud);
        fila.Version += 1;
        fila.FechaActualizacion = DateTime.UtcNow;

        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            await AplicarEtiquetasAsync(db, fila.Id, solicitud.Etiquetas, ct);
            if (pideCambiarImagen) { await AplicarImagenAsync(db, fila.Id, solicitud.ImagenArchivoId, ct); }
            await AplicarClasificacionAsync(db, fila.Id, solicitud, ct);
            Auditar(db, Actor(principal), fila.Id, "actualizar", antes, new { fila.Titulo, fila.FechaInicio, fila.Modalidad });
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Ok(await ADtoAsync(db, fila, ct));
    }

    private static async Task<IResult> CambiarEstado(
        PnmcDbContext db, ClaimsPrincipal principal, long id, CambiarEstadoEventoAgendaSolicitud solicitud, CancellationToken ct)
    {
        var estado = Limpiar(solicitud.Estado) ?? string.Empty;
        if (!ReglasDeAgenda.Estados.Contains(estado))
        {
            return Results.BadRequest(new { estado = new[] { $"«{estado}» no es un estado de evento." } });
        }

        var fila = await db.EventosAgenda.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (fila is null) return Results.NotFound();

        if (estado == "publicado")
        {
            var faltas = ReglasDeAgenda.LoQueFaltaParaPublicar(fila.Titulo, fila.Descripcion, fila.Modalidad, fila.Lugar, fila.Url);
            if (faltas.Count > 0)
            {
                return Results.BadRequest(new { estado = new[] { "No se puede publicar: " + string.Join("; ", faltas) + "." } });
            }
        }

        var anterior = fila.Estado;
        fila.Estado = estado;
        fila.FechaActualizacion = DateTime.UtcNow;

        // El verbo sale de la lista blanca de la bitácora: «en_revision» y «borrador» no están en
        // `CK_BitacoraAuditoria_Accion` y se registran como una actualización.
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

    private static IResult? Validar(GuardarEventoAgendaSolicitud solicitud)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Titulo)) return Results.BadRequest(new { titulo = TituloObligatorio });
        if (string.IsNullOrWhiteSpace(solicitud.Descripcion)) return Results.BadRequest(new { descripcion = DescripcionObligatoria });

        var modalidad = (Limpiar(solicitud.Modalidad) ?? "presencial").ToLowerInvariant();
        if (!ReglasDeAgenda.Modalidades.Contains(modalidad))
        {
            return Results.BadRequest(new { modalidad = new[] { $"«{modalidad}» no es una modalidad de evento." } });
        }

        if (solicitud.FechaFin is { } fin && fin < solicitud.FechaInicio)
        {
            return Results.BadRequest(new { fechaFin = FinAntesDelInicio });
        }

        // UN EVENTO DE UN SOLO DIA NO PUEDE ACABAR ANTES DE EMPEZAR. En uno de varios días la
        // comparación de horas no significa nada —acabar a las 9 del último día es normal—, y por
        // eso solo se comprueba cuando empieza y acaba el mismo día.
        var mismoDia = solicitud.FechaFin is null || solicitud.FechaFin == solicitud.FechaInicio;
        if (mismoDia && solicitud.HoraInicio is { } inicio && solicitud.HoraFin is { } finHora && finHora < inicio)
        {
            return Results.BadRequest(new { horaFin = HoraFinAntes });
        }

        if (solicitud.OrdenVisualizacion is { } orden && orden <= 0)
        {
            return Results.BadRequest(new { ordenVisualizacion = OrdenPositivo });
        }

        return null;
    }

    private static void Volcar(EventoAgendaRow fila, GuardarEventoAgendaSolicitud solicitud)
    {
        fila.Titulo = Limpiar(solicitud.Titulo)!;
        fila.Descripcion = Limpiar(solicitud.Descripcion)!;
        fila.FechaInicio = solicitud.FechaInicio;
        fila.FechaFin = solicitud.FechaFin;
        fila.HoraInicio = solicitud.HoraInicio;
        fila.Modalidad = (Limpiar(solicitud.Modalidad) ?? "presencial").ToLowerInvariant();
        fila.Lugar = Limpiar(solicitud.Lugar);
        fila.CodigoDepartamento = Limpiar(solicitud.CodigoDepartamento);
        fila.CodigoMunicipio = Limpiar(solicitud.CodigoMunicipio);
        fila.Url = Limpiar(solicitud.Url);
        fila.ImagenRuta = LimpiarRuta(solicitud.ImagenRuta);
        fila.ImagenAlternativa = Limpiar(solicitud.ImagenAlternativa);
        fila.CategoriaId = solicitud.CategoriaId;
        fila.Organizador = Limpiar(solicitud.Organizador);
        fila.DescripcionLarga = Limpiar(solicitud.DescripcionLarga);
        fila.HoraFin = solicitud.HoraFin;
        fila.OrdenVisualizacion = solicitud.OrdenVisualizacion;
        fila.FestivalId = solicitud.FestivalId;

        // EL NIVEL SE DEDUCE DE LOS CODIGOS y no se cree lo que venga: son la misma información
        // dicha dos veces, y la base rechaza el par que no case con su nivel.
        fila.NivelCobertura = ReglasDeAgenda.NivelQueCorresponde(fila.CodigoDepartamento, fila.CodigoMunicipio);
    }

    /// <summary>
    /// Deja solo los eventos de una iniciativa del Programa.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ES LO QUE HACE UTIL EL ENLACE. Sin este filtro, marcar un evento como de Celebra la Música
    /// sería un dato que nadie puede consultar; con él, el sitio de la iniciativa arma su propio
    /// calendario sin duplicar ni un evento —que es como se acaba con dos calendarios que
    /// discrepan—.
    /// </para>
    /// <para>
    /// SE FILTRA POR CODIGO Y NO POR IDENTIFICADOR: el código es la dirección estable del proyecto
    /// y es lo que puede ir en una URL sin obligar a nadie a saber números de base de datos.
    /// </para>
    /// </remarks>
    private static IQueryable<EventoAgendaRow> AplicarProyecto(
        IQueryable<EventoAgendaRow> consulta, PnmcDbContext db, string? proyecto)
    {
        var codigo = (proyecto ?? string.Empty).Trim();
        if (codigo.Length == 0) return consulta;

        var eventos = db.EventosAgendaProyectosTransversales.AsNoTracking()
            .Where(v => db.ProyectosTransversales.Any(p => p.Id == v.ProyectoTransversalId && p.Codigo == codigo))
            .Select(v => v.EventoAgendaId);

        return consulta.Where(e => eventos.Contains(e.Id));
    }

    private static IQueryable<EventoAgendaRow> AplicarFiltros(
        IQueryable<EventoAgendaRow> consulta, PnmcDbContext db, string? q, string? etiqueta, string? departamento, string? categoria)
    {
        if (!string.IsNullOrWhiteSpace(q))
        {
            var termino = q.Trim();
            consulta = consulta.Where(e => e.Titulo.Contains(termino) || e.Descripcion.Contains(termino) || (e.Lugar != null && e.Lugar.Contains(termino)));
        }

        if (!string.IsNullOrWhiteSpace(departamento))
        {
            var codigo = departamento.Trim();
            consulta = consulta.Where(e => e.CodigoDepartamento == codigo);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            // POR NOMBRE Y NO POR IDENTIFICADOR: es lo que el portal pone en su filtro y en la
            // dirección, y lo que una persona puede leer y compartir.
            var pedida = categoria.Trim();
            var idsCategoria = db.Categorias.AsNoTracking().Where(c => c.NombreCategoria == pedida).Select(c => c.Id);
            consulta = consulta.Where(e => e.CategoriaId != null && idsCategoria.Contains(e.CategoriaId.Value));
        }

        if (!string.IsNullOrWhiteSpace(etiqueta))
        {
            var termino = etiqueta.Trim();
            var ids = db.EtiquetasEventoAgenda.AsNoTracking().Where(x => x.Termino == termino).Select(x => x.EventoAgendaId);
            consulta = consulta.Where(e => ids.Contains(e.Id));
        }

        return consulta;
    }

    private static async Task<PaginaEventosAgendaDto> PaginarAsync(
        PnmcDbContext db, IQueryable<EventoAgendaRow> consulta, int? pagina, int? tamano, DateOnly hoy, CancellationToken ct)
    {
        var paginaPedida = Math.Max(1, pagina ?? 1);
        var tamanoPedido = Math.Clamp(tamano ?? TamanoPorOmision, 1, TamanoMaximo);

        var total = await consulta.CountAsync(ct);
        var filas = await consulta.Skip((paginaPedida - 1) * tamanoPedido).Take(tamanoPedido).ToListAsync(ct);

        // LAS ETIQUETAS Y LOS TERRITORIOS DE TODA LA PAGINA EN DOS CONSULTAS, no dos por evento.
        var ids = filas.Select(f => f.Id).ToList();
        var etiquetas = await db.EtiquetasEventoAgenda.AsNoTracking().Where(x => ids.Contains(x.EventoAgendaId)).ToListAsync(ct);
        var territorios = await ResolverTerritoriosAsync(db, filas, ct);
        var imagenes = await ResolverImagenesAsync(db, ids, ct);
        var categorias = await ResolverCategoriasAsync(db, filas.Select(f => f.CategoriaId), ct);
        var (practicas, territoriosSonoros) = await ResolverClasificacionAsync(db, ids, ct);
        var proyectos = await ResolverProyectosAsync(db, ids, ct);
        var procedencias = await ProcedenciaDeRegistro.LeerVariasAsync(
            db, Modulos.Agenda, ids.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList(), ct);

        var items = filas.Select(f => ADto(
            f,
            etiquetas.Where(x => x.EventoAgendaId == f.Id).Select(x => x.Termino).OrderBy(t => t, StringComparer.Ordinal).ToList(),
            territorios,
            hoy,
            imagenes.TryGetValue(f.Id, out var imagen) ? imagen : null,
            categorias.TryGetValue(f.CategoriaId ?? 0, out var nombreCategoria) ? nombreCategoria : null,
            practicas.TryGetValue(f.Id, out var suyasPracticas) ? suyasPracticas : [],
            territoriosSonoros.TryGetValue(f.Id, out var suyosTerritorios) ? suyosTerritorios : [],
            procedencias.TryGetValue(f.Id.ToString(CultureInfo.InvariantCulture), out var suProcedencia) ? suProcedencia : null,
            proyectos.TryGetValue(f.Id, out var susProyectos) ? susProyectos : [])).ToList();

        var totalPaginas = total == 0 ? 0 : (int)Math.Ceiling(total / (double)tamanoPedido);
        return new PaginaEventosAgendaDto(items, paginaPedida, tamanoPedido, total, totalPaginas);
    }

    /// <summary>
    /// El nombre del departamento y del municipio de cada evento, en una consulta.
    /// </summary>
    /// <remarks>
    /// SE GUARDA EL CODIGO Y SE RESUELVE EL NOMBRE, no al revés: `Divipola` es el catálogo
    /// territorial canónico y un nombre escrito a mano hace imposible agrupar por departamento.
    /// </remarks>
    private static async Task<Dictionary<string, (string? Departamento, string? Municipio)>> ResolverTerritoriosAsync(
        PnmcDbContext db, List<EventoAgendaRow> filas, CancellationToken ct)
    {
        var departamentos = filas.Select(f => f.CodigoDepartamento).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var municipios = filas.Select(f => f.CodigoMunicipio).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (departamentos.Count == 0 && municipios.Count == 0) return [];

        var filasDivipola = await db.DivipolaLocations.AsNoTracking()
            .Where(d => departamentos.Contains(d.DepartmentCode) || municipios.Contains(d.MunicipalityCode))
            .Select(d => new { d.DepartmentCode, d.DepartmentName, d.MunicipalityCode, d.MunicipalityName })
            .ToListAsync(ct);

        var resultado = new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);
        foreach (var d in filasDivipola)
        {
            resultado["dep:" + d.DepartmentCode] = (d.DepartmentName, null);
            resultado["mun:" + d.MunicipalityCode] = (d.DepartmentName, d.MunicipalityName);
        }
        return resultado;
    }

    private static async Task<EventoAgendaDto> ADtoAsync(PnmcDbContext db, EventoAgendaRow fila, CancellationToken ct)
    {
        var etiquetas = await db.EtiquetasEventoAgenda.AsNoTracking()
            .Where(x => x.EventoAgendaId == fila.Id).Select(x => x.Termino).OrderBy(t => t).ToListAsync(ct);
        var territorios = await ResolverTerritoriosAsync(db, [fila], ct);
        var imagenes = await ResolverImagenesAsync(db, [fila.Id], ct);

        // EL TERNARIO NO ES ADORNO. `TryGetValue` con `out var` sobre una tupla de VALOR deja
        // `(0, null)` cuando no encuentra nada, y al pasarla a un parámetro anulable se convierte
        // en un nulo que NO es nulo: el evento sin imagen respondía `imagenArchivoId: 0` y una
        // dirección muerta, `/api/v1/publico/archivos/0`. Lo cazó la prueba de retirar la imagen.
        var imagen = imagenes.TryGetValue(fila.Id, out var encontrada) ? encontrada : ((int, string?)?)null;
        var categorias = await ResolverCategoriasAsync(db, [fila.CategoriaId], ct);
        var categoria = categorias.TryGetValue(fila.CategoriaId ?? 0, out var nombre) ? nombre : null;
        var (practicas, territoriosSonoros) = await ResolverClasificacionAsync(db, [fila.Id], ct);
        var procedencia = await ProcedenciaDeRegistro.LeerAsync(
            db, Modulos.Agenda, fila.Id.ToString(CultureInfo.InvariantCulture), ct);
        return ADto(
            fila, etiquetas, territorios, DateOnly.FromDateTime(DateTime.UtcNow), imagen, categoria,
            practicas.TryGetValue(fila.Id, out var suyasPracticas) ? suyasPracticas : [],
            territoriosSonoros.TryGetValue(fila.Id, out var suyosTerritorios) ? suyosTerritorios : [],
            procedencia,
            (await ResolverProyectosAsync(db, [fila.Id], ct)).TryGetValue(fila.Id, out var susProyectos) ? susProyectos : []);
    }

    private static EventoAgendaDto ADto(
        EventoAgendaRow fila,
        IReadOnlyList<string> etiquetas,
        Dictionary<string, (string? Departamento, string? Municipio)> territorios,
        DateOnly hoy,
        (int Id, string? Alt)? imagen = null,
        string? categoria = null,
        IReadOnlyList<ElementoDeClasificacionDto>? practicas = null,
        IReadOnlyList<ElementoDeClasificacionDto>? territoriosSonoros = null,
        ProcedenciaDeRegistroDto? procedencia = null,
        IReadOnlyList<ElementoDeClasificacionDto>? proyectos = null)
    {
        string? nombreDepartamento = null;
        string? nombreMunicipio = null;

        if (fila.CodigoMunicipio is { Length: > 0 } municipio && territorios.TryGetValue("mun:" + municipio, out var porMunicipio))
        {
            nombreDepartamento = porMunicipio.Departamento;
            nombreMunicipio = porMunicipio.Municipio;
        }
        else if (fila.CodigoDepartamento is { Length: > 0 } departamento && territorios.TryGetValue("dep:" + departamento, out var porDepartamento))
        {
            nombreDepartamento = porDepartamento.Departamento;
        }

        return new EventoAgendaDto(
            fila.Id, fila.Slug, fila.Titulo, fila.Descripcion,
            fila.FechaInicio, fila.FechaFin, fila.HoraInicio,
            fila.Modalidad, fila.Lugar,
            fila.CodigoDepartamento, nombreDepartamento,
            fila.CodigoMunicipio, nombreMunicipio,
            fila.Url, fila.ImagenRuta, fila.ImagenAlternativa, fila.CategoriaId, categoria, fila.Organizador,
            fila.DescripcionLarga, fila.HoraFin, fila.NivelCobertura, fila.OrdenVisualizacion, fila.FestivalId,
            imagen?.Id,
            // LA DIRECCION SE CONSTRUYE AQUI y no se guarda: el identificador es el dato, la ruta
            // es cómo se sirve, y si mañana cambia la ruta no hay que reescribir ninguna fila.
            imagen is { } enlazada ? "/api/v1/publico/archivos/" + enlazada.Id.ToString(CultureInfo.InvariantCulture) : null,
            // El texto alternativo del banco manda; el suelto de la fila sirve de respaldo mientras
            // queden eventos cargados con ruta a mano.
            imagen?.Alt ?? fila.ImagenAlternativa,
            fila.Estado,
            ReglasDeAgenda.SituacionDe(fila.FechaInicio, fila.FechaFin, hoy),
            fila.Version, etiquetas, practicas ?? [], territoriosSonoros ?? [], procedencia, proyectos ?? [], fila.FechaActualizacion);
    }

    /// <summary>
    /// Deja vinculada la imagen principal del evento.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUSENTE Y VACIO NO SIGNIFICAN LO MISMO, y la diferencia es la que evita borrar la imagen de
    /// todos los eventos con el primer cliente que no conozca el campo:
    /// </para>
    /// <list type="bullet">
    ///   <item>la clave NO viene → no se toca la imagen que hubiera;</item>
    ///   <item>la clave viene vacía → se RETIRA, que es lo que espera quien vacía el campo del
    ///   formulario y guarda.</item>
    /// </list>
    /// <para>
    /// Es la misma regla que aplica el desarrollo de septiembre, y allí está documentado el fallo
    /// que la trajo: guardar mandando un subconjunto de campos quitaba la foto.
    /// </para>
    /// </remarks>
    /// <summary>
    /// La imagen principal de cada evento de la página, en una consulta.
    /// </summary>
    /// <remarks>
    /// UNA CONSULTA Y NO UNA POR EVENTO: con doce eventos en pantalla eso serían doce viajes a la
    /// base para pintar una lista. Es la misma decisión que ya se tomó con las etiquetas y los
    /// territorios unas líneas más arriba.
    /// </remarks>
    private static async Task<Dictionary<long, (int Id, string? Alt)>> ResolverImagenesAsync(
        PnmcDbContext db, List<long> eventos, CancellationToken ct)
    {
        if (eventos.Count == 0) return [];

        var filas = await db.EventosAgendaArchivos.AsNoTracking()
            .Where(x => eventos.Contains(x.EventoAgendaId) && x.RolArchivo == RolImagenPrincipal)
            .Join(db.Files.AsNoTracking(), v => v.ArchivoId, a => a.Id, (v, a) => new { v.EventoAgendaId, a.Id, a.AltText })
            .ToListAsync(ct);

        return filas
            .GroupBy(x => x.EventoAgendaId)
            .ToDictionary(g => g.Key, g => (g.First().Id, (string?)g.First().AltText));
    }

    private static async Task AplicarImagenAsync(
        PnmcDbContext db, long eventoId, int? archivoId, CancellationToken ct)
    {
        var existentes = await db.EventosAgendaArchivos
            .Where(x => x.EventoAgendaId == eventoId && x.RolArchivo == RolImagenPrincipal)
            .ToListAsync(ct);

        db.EventosAgendaArchivos.RemoveRange(existentes);

        if (archivoId is { } id && id > 0)
        {
            db.EventosAgendaArchivos.Add(new EventoAgendaArchivoRow
            {
                EventoAgendaId = eventoId,
                ArchivoId = id,
                RolArchivo = RolImagenPrincipal,
                OrdenVisualizacion = 1,
            });
        }
    }

    private const string RolImagenPrincipal = "imagen_principal";

    private static async Task AplicarEtiquetasAsync(
        PnmcDbContext db, long eventoId, IReadOnlyList<string>? etiquetas, CancellationToken ct)
    {
        if (etiquetas is null) return;

        var existentes = await db.EtiquetasEventoAgenda.Where(x => x.EventoAgendaId == eventoId).ToListAsync(ct);
        db.EtiquetasEventoAgenda.RemoveRange(existentes);

        foreach (var termino in etiquetas.Select(e => (e ?? string.Empty).Trim()).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            db.EtiquetasEventoAgenda.Add(new EtiquetaEventoAgendaRow { EventoAgendaId = eventoId, Termino = termino });
        }
    }


    /// <summary>
    /// Las prácticas musicales y los territorios sonoros de los eventos dados.
    /// </summary>
    private static async Task<(
        Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>> Practicas,
        Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>> Territorios)> ResolverClasificacionAsync(
        PnmcDbContext db, List<long> eventos, CancellationToken ct)
    {
        if (eventos.Count == 0) return ([], []);

        var vinculosPracticas = await db.EventosAgendaPracticasMusicales.AsNoTracking()
            .Where(x => eventos.Contains(x.EventoAgendaId))
            .Select(x => new { x.EventoAgendaId, x.PracticaMusicalId }).ToListAsync(ct);
        var vinculosTerritorios = await db.EventosAgendaTerritoriosSonoros.AsNoTracking()
            .Where(x => eventos.Contains(x.EventoAgendaId))
            .Select(x => new { x.EventoAgendaId, x.TerritorioSonoroId }).ToListAsync(ct);

        return (
            await ClasificacionDeContenido.AgruparPracticasAsync(
                db, vinculosPracticas.Select(v => (v.EventoAgendaId, v.PracticaMusicalId)).ToList(), ct),
            await ClasificacionDeContenido.AgruparTerritoriosAsync(
                db, vinculosTerritorios.Select(v => (v.EventoAgendaId, v.TerritorioSonoroId)).ToList(), ct));
    }

    /// <summary>
    /// Las iniciativas del Programa a las que pertenece cada evento de la página.
    /// </summary>
    private static async Task<Dictionary<long, IReadOnlyList<ElementoDeClasificacionDto>>> ResolverProyectosAsync(
        PnmcDbContext db, List<long> eventos, CancellationToken ct)
    {
        if (eventos.Count == 0) return [];

        var vinculos = await db.EventosAgendaProyectosTransversales.AsNoTracking()
            .Where(x => eventos.Contains(x.EventoAgendaId))
            .Select(x => new { x.EventoAgendaId, x.ProyectoTransversalId }).ToListAsync(ct);

        return await ClasificacionDeContenido.AgruparProyectosAsync(
            db, vinculos.Select(v => (v.EventoAgendaId, v.ProyectoTransversalId)).ToList(), ct);
    }

    /// <summary>
    /// Deja la clasificación del evento diciendo lo que pide la solicitud.
    /// </summary>
    private static async Task AplicarClasificacionAsync(
        PnmcDbContext db, long eventoId, GuardarEventoAgendaSolicitud solicitud, CancellationToken ct)
    {
        var practicas = await db.EventosAgendaPracticasMusicales.Where(x => x.EventoAgendaId == eventoId).ToListAsync(ct);
        ClasificacionDeContenido.Reconciliar(
            db.EventosAgendaPracticasMusicales, practicas, x => x.PracticaMusicalId, solicitud.PracticasMusicalesIds,
            id => new EventoAgendaPracticaMusicalRow { EventoAgendaId = eventoId, PracticaMusicalId = id });

        var territorios = await db.EventosAgendaTerritoriosSonoros.Where(x => x.EventoAgendaId == eventoId).ToListAsync(ct);
        ClasificacionDeContenido.Reconciliar(
            db.EventosAgendaTerritoriosSonoros, territorios, x => x.TerritorioSonoroId, solicitud.TerritoriosSonorosIds,
            id => new EventoAgendaTerritorioSonoroRow { EventoAgendaId = eventoId, TerritorioSonoroId = id });

        // LAS INICIATIVAS DEL PROGRAMA se reconcilian igual que la clasificación: misma regla de
        // «ausente no es vacío», misma pieza compartida.
        var proyectos = await db.EventosAgendaProyectosTransversales.Where(x => x.EventoAgendaId == eventoId).ToListAsync(ct);
        ClasificacionDeContenido.Reconciliar(
            db.EventosAgendaProyectosTransversales, proyectos, x => x.ProyectoTransversalId, solicitud.ProyectosTransversalesIds,
            id => new EventoAgendaProyectoTransversalRow { EventoAgendaId = eventoId, ProyectoTransversalId = id });
    }

    /// <summary>
    /// El nombre de cada categoría de la página, en una consulta.
    /// </summary>
    /// <remarks>
    /// EL IDENTIFICADOR ES EL DATO Y EL NOMBRE ES COMO SE LEE. Se resuelve al servir y no se guarda
    /// junto al identificador: dos columnas con la misma verdad acaban discrepando.
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
            TableName = "EventosAgenda",
            RecordId = id.ToString(CultureInfo.InvariantCulture),
            Action = accion,
            PreviousValuesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            NewValuesJson = despues is null ? null : JsonSerializer.Serialize(despues),
            CreatedAt = DateTime.UtcNow,
        });

    /// <summary>
    /// Lee un afiche o un volante y propone con qué rellenar un evento.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>LA MISMA TUBERIA QUE EL CATALOGO EDITORIAL, CON OTRA SALIDA.</b> El tramo de comprobar,
    /// leer y renderizar vive en <see cref="ImportacionDeDocumentos"/> y se comparte; lo que cambia es
    /// qué se reconoce. En una publicación se buscan identificadores y autoría; en un afiche, lo que
    /// importa es <b>cuándo y dónde</b>.
    /// </para>
    /// <para>
    /// <b>EL LUGAR SE RESUELVE CONTRA DIVIPOLA</b>, que es la fuente territorial del proyecto. Un
    /// afiche que dice «Ibagué» llega así con su código de municipio y su departamento, que es lo que
    /// el evento necesita para salir en el mapa. Proponerlo como texto suelto obligaría a volver a
    /// buscarlo a mano.
    /// </para>
    /// <para>
    /// <b>Y NO SE CREA NINGUN EVENTO.</b> Se propone; una persona revisa, completa y aprueba. Es la
    /// restricción del proyecto sobre lo obtenido por importación asistida.
    /// </para>
    /// </remarks>
    private static async Task<IResult> LeerDocumento(
        HttpRequest peticion, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        var (documento, error) = await ImportacionDeDocumentos.LeerAsync(peticion, ct);
        if (error is not null) { return error; }

        var titulos = new List<ValorPropuestoDto>();
        var fechas = new List<FechaPropuestaDto>();
        var lugares = new List<LugarPropuestoDto>();
        var organizaciones = new List<ValorPropuestoDto>();
        var pudo = new List<string>();

        if (documento!.Metadatos.Titulo is { Length: > 0 } tituloMeta)
        {
            titulos.Add(new ValorPropuestoDto(tituloMeta, "metadatos", "Lo escribió quien generó el documento."));
            pudo.Add("metadatos del documento");
        }

        if (documento.Lectura is { } lectura)
        {
            if (lectura.Portada.Count > 0)
            {
                // EN UN AFICHE, EL NOMBRE DEL EVENTO ES LO MAS GRANDE. Es la misma regla que en una
                // portada de libro, y por el mismo motivo: está hecho para leerse de lejos.
                titulos.Add(new ValorPropuestoDto(lectura.Portada[0].Texto, "portada", "El texto más grande del afiche."));
                pudo.Add("texto del afiche");
            }

            var texto = lectura.TextoDeLasPrimerasPaginas;

            // EL AÑO QUE APAREZCA EN EL DOCUMENTO sirve de respaldo para las fechas que no lo repiten:
            // un afiche dice «12 de octubre» porque quien lo lee está dentro de ese año.
            var anioDeRespaldo = lectura.Anios.Count > 0 ? lectura.Anios[0] : (int?)null;
            foreach (var fecha in FechasEnElTexto.Leer(texto, anioDeRespaldo))
            {
                fechas.Add(new FechaPropuestaDto(
                    fecha.Inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    fecha.Fin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    fecha.Origen));
            }
            if (fechas.Count > 0) { pudo.Add("fechas anunciadas"); }

            // ── El lugar, resuelto contra DIVIPOLA ──
            var territorios = await db.DivipolaLocations.AsNoTracking()
                .Select(d => new { d.MunicipalityName, d.MunicipalityCode, d.DepartmentName, d.DepartmentCode })
                .ToListAsync(ct);

            // CUATRO LETRAS Y PALABRA COMPLETA, no las ocho del vocabulario editorial: un topónimo
            // corto —Cali, Tunja, Neiva, Paipa— sí es distintivo cuando aparece, al revés que «Libro».
            // Exigir palabra entera es lo que impide reconocer «Cali» dentro de «California».
            var reconocidos = CotejoConElAcervo.Reconocer(
                texto,
                territorios.Select(t => (t.MunicipalityName, 0)),
                largoMinimo: 4,
                exigirPalabraCompleta: true);

            foreach (var reconocido in reconocidos.Take(5))
            {
                var territorio = territorios.First(t => t.MunicipalityName == reconocido.Valor);
                lugares.Add(new LugarPropuestoDto(
                    territorio.MunicipalityName, territorio.MunicipalityCode,
                    territorio.DepartmentName, territorio.DepartmentCode));
            }
            if (lugares.Count > 0) { pudo.Add("lugares reconocidos en DIVIPOLA"); }

            // ── Y la organización, contra las que ya están registradas ──
            // Las organizaciones viven en `Entidades`: es la tabla que las registra a todas, no
            // solo a las del canal externo.
            var nombres = await db.EntityProfiles.AsNoTracking()
                .Select(o => o.Name)
                .ToListAsync(ct);
            foreach (var reconocida in CotejoConElAcervo.Reconocer(texto, nombres.Select(n => (n, 0))))
            {
                organizaciones.Add(new ValorPropuestoDto(reconocida.Valor, "acervo", "Ya está registrada en el sistema."));
            }
            if (organizaciones.Count > 0) { pudo.Add("organizaciones reconocidas"); }
        }

        var (aficheId, aficheUrl) = await ImportacionDeDocumentos.GuardarPortadaAsync(documento, principal, db, ct);
        if (aficheId is not null) { pudo.Add("afiche renderizado"); }

        return Results.Ok(new PropuestaDeEventoDto(
            Titulos: titulos,
            Fechas: fechas,
            Lugares: lugares,
            Organizaciones: organizaciones,
            AficheArchivoId: aficheId,
            AficheUrl: aficheUrl,
            RequiereOcr: documento.Lectura?.PaginaSinTexto ?? false,
            LeidoConReconocimientoOptico: documento.Lectura?.LeidoConReconocimientoOptico ?? false,
            LoQueSePudoLeer: pudo));
    }
}
