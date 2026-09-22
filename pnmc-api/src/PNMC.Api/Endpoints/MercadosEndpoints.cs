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
/// Mercados musicales: un proceso del Ecosistema, al mismo nivel que un Festival.
/// </summary>
/// <remarks>
/// <para>
/// <b>ORDENAR Y FILTRAR ACTUAN SOBRE EL TOTAL.</b> No sobre la página que el navegador tiene en la
/// mano. Es la regla que este proyecto ya tuvo que corregir en Agenda: con la ordenación hecha en el
/// cliente, la página 1 nunca enseñaba un publicado por muchas veces que se pulsara la cabecera.
/// </para>
/// <para>
/// <b>LA RELACION CON EL FESTIVAL SE COMPRUEBA AQUI, Y NO SOLO EN LA PANTALLA.</b> Un selector
/// acotado a los festivales de la organización es una comodidad; la regla solo existe de verdad
/// cuando el servidor la aplica, porque la ruta sigue respondiendo a quien mande otro identificador
/// a mano.
/// </para>
/// </remarks>
public static class MercadosEndpoints
{
    private const int TamanoPorOmision = 20;
    private const int TamanoMaximo = 100;

    private static readonly string[] NombreObligatorio = ["El nombre del mercado es obligatorio."];
    private static readonly string[] OrganizacionObligatoria = ["Indica la organización responsable."];
    private static readonly string[] OrganizacionDesconocida = ["Esa organización no existe."];
    private static readonly string[] CoberturaInvalida = ["El nivel de cobertura debe ser nacional, departamental o municipal."];
    private static readonly string[] TerritorioIncoherente = ["El territorio no corresponde con el nivel de cobertura."];
    private static readonly string[] TerritorioDesconocido = ["Ese departamento o municipio no está en DIVIPOLA."];
    private static readonly string[] FestivalObligatorio = ["Si el mercado se realiza en el marco de un festival, elige cuál."];
    private static readonly string[] FestivalSobrante = ["Has elegido un festival pero el mercado no se realiza en el marco de ninguno."];
    private static readonly string[] FestivalDesconocido = ["Ese festival no existe."];
    private static readonly string[] FestivalDeOtraOrganizacion = ["Ese festival pertenece a otra organización."];
    private static readonly string[] AlcanceDesconocido = ["Ese alcance no está en el catálogo."];
    private static readonly string[] ModalidadDesconocida = ["Esa modalidad no está en el catálogo."];

    private static readonly string[] NivelesDeCobertura = ["nacional", "departamental", "municipal"];
    private static readonly string[] MotivoObligatorio = ["Di qué hay que corregir: sin motivo, el mercado vuelve igual."];
    private static readonly string[] DecisionDesconocida = ["La decisión debe ser publicar o pedir ajustes."];
    private static readonly string[] MotivoDeRetiroObligatorio =
        ["Escribe por qué se retira el mercado del ecosistema. Dentro de un año nadie recordará el motivo."];

    public static RouteGroupBuilder MapMercadosEndpoints(this RouteGroupBuilder api)
    {
        /*
          LA CONSULTA PUBLICA.

          MERCADOS ENTRA AL MAPA Y AL ECOSISTEMA. Hasta hoy su capa del geovisor existía con sus
          colores y sus treinta y cinco campos, y se alimentaba de una lista vacía: era una de las
          cuatro que se anunciaban «en preparación» porque sus tablas se habían retirado, con la
          decisión escrita al lado de que «cada proceso volverá con su modelo y su revisión
          propios». Mercados ya volvió.

          SOLO LO PUBLICADO SALE. Un borrador o algo en revisión es trabajo interno de una
          organización y del Programa; enseñarlo aquí publicaría por la puerta de atrás lo que el
          circuito de revisión existe para decidir.
        */
        var publico = api.MapGroup("/publico/mercados").WithTags("mercados-publico").AllowAnonymous();
        publico.MapGet(string.Empty, ListarPublicos).WithName("ListarMercadosPublicos");
        publico.MapGet("/{id:int}", ObtenerPublico).WithName("ObtenerMercadoPublico");

        /*
          LOS MERCADOS QUE OCURREN DENTRO DE UN FESTIVAL, leídos desde el festival.

          ES LA RELACION VISTA DEL OTRO LADO, y es donde de verdad se ve para qué sirve: quien abre
          la ficha pública de un festival descubre que en su marco se realiza un mercado, sin haber
          tenido que saber que ese mercado existe. Sin esta ruta la relación solo se puede recorrer
          en un sentido, que es la mitad de una relación.

          CUELGA DE `/publico/festivales/{id}/mercados` Y NO DE `/publico/mercados?festival=`
          porque la pregunta es «qué pasa dentro de este festival», y la dirección debería poder
          leerse como la pregunta.
        */
        // COMO SE VERA EN EL DIRECTORIO, antes de publicarlo. Devuelve el mercado esté en el estado
        // que esté: quien previsualiza tiene sesión de consola, y el directorio público inserta
        // esta tarjeta en su sitio para enseñar cómo quedará. Ver `PrevisualizacionEnListado`.
        api.MapGroup(PrevisualizacionEnListado.Prefijo + "/mercados")
            .WithTags("previsualizacion")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .MapGet("/{id:int}", PrevisualizarEnListado)
            .WithName("PrevisualizarMercadoEnListado")
            .Produces<MercadoDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        api.MapGroup("/publico/festivales/{festivalId:int}/mercados")
            .WithTags("mercados-publico")
            .AllowAnonymous()
            .MapGet(string.Empty, MercadosDeUnFestival)
            .WithName("ListarMercadosDeUnFestivalPublico");

        var consola = api.MapGroup("/institucional/mercados")
            .WithTags("mercados")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("mercados");

        consola.MapGet(string.Empty, Listar).WithName("ListarMercados");
        consola.MapGet("/catalogos", Catalogos).WithName("CatalogosDeMercado");
        consola.MapGet("/festivales-elegibles", FestivalesElegibles).WithName("FestivalesElegiblesParaMercado");
        consola.MapGet("/{id:int}", Obtener).WithName("ObtenerMercado");
        consola.MapPost(string.Empty, Crear).WithName("CrearMercado");
        consola.MapPut("/{id:int}", Guardar).WithName("GuardarMercado");
        consola.MapPost("/{id:int}/decision", Decidir).WithName("DecidirSobreMercado");
        // RETIRAR DEL ECOSISTEMA UN MERCADO YA PUBLICADO, con su motivo. Es la pareja de
        // `institucional/festivales/{id}/archivar`, y Mercados no la tenía.
        consola.MapPost("/{id:int}/archivar", Archivar).WithName("ArchivarMercado");

        return api;
    }

    // ─────────────────────────── Consulta ───────────────────────────

    /// <summary>Los mercados publicados, para el geovisor y el directorio del Ecosistema.</summary>
    /// <remarks>
    /// <b>SE PIDE UNA SOLA PAGINA GRANDE</b> porque el geovisor cuenta por departamento sobre el
    /// total: una página parcial daría un mapa con cifras que no son las del país. Es el mismo
    /// criterio y el mismo tope que Festivales.
    /// </remarks>
    private static async Task<IResult> ListarPublicos(
        PnmcDbContext db, int? limit, int? offset, CancellationToken ct)
    {
        var consulta = db.Mercados.AsNoTracking()
            .Where(m => m.Activo && m.EstadoRegistro == "publicado")
            .OrderBy(m => m.Nombre).ThenBy(m => m.Id);

        var total = await consulta.CountAsync(ct);
        var porPagina = Math.Clamp(limit ?? 500, 1, 500);
        var desde = Math.Max(offset ?? 0, 0);
        var filas = await consulta.Skip(desde).Take(porPagina).ToListAsync(ct);

        var lista = (await ATarjetasAsync(db, filas, ct)).Select(APublico).ToList();
        return Results.Ok(new PagedResponse<MercadoPublicoDto>(lista, porPagina, desde, total));
    }

    /// <summary>Los mercados publicados que se realizan en el marco de ese festival.</summary>
    /// <remarks>
    /// <b>EL FESTIVAL TAMBIEN TIENE QUE ESTAR PUBLICADO.</b> Si no, esta ruta sería una forma de
    /// preguntar por festivales que todavía no existen para el público: responder «ninguno» a un
    /// identificador y una lista a otro ya dice cuál de los dos existe.
    /// </remarks>
    private static async Task<IResult> MercadosDeUnFestival(int festivalId, PnmcDbContext db, CancellationToken ct)
    {
        var publicado = await db.FestivalRecords.AsNoTracking()
            .AnyAsync(f => f.Id == festivalId && f.StatusCode == EstadosFestival.Publicado, ct);
        if (!publicado) return Results.NotFound();

        var filas = await db.Mercados.AsNoTracking()
            .Where(m => m.Activo && m.EstadoRegistro == "publicado"
                && m.SeRealizaEnElMarcoDeUnFestival && m.FestivalId == festivalId)
            .OrderBy(m => m.Nombre)
            .ToListAsync(ct);

        return Results.Ok((await ATarjetasAsync(db, filas, ct)).Select(APublico).ToList());
    }

    private static async Task<IResult> PrevisualizarEnListado(int id, PnmcDbContext db, CancellationToken ct)
    {
        var fila = await db.Mercados.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.Activo, ct);
        if (fila is null) return Results.NotFound();

        var lista = await ATarjetasAsync(db, [fila], ct);
        return Results.Ok(lista[0]);
    }

    private static async Task<IResult> ObtenerPublico(int id, PnmcDbContext db, CancellationToken ct)
    {
        var fila = await db.Mercados.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.Activo && m.EstadoRegistro == "publicado", ct);
        if (fila is null) return Results.NotFound();

        var lista = await ATarjetasAsync(db, [fila], ct);
        return Results.Ok(APublico(lista[0]));
    }


    private static async Task<IResult> Listar(
        PnmcDbContext db, string? q, string? estado, int? organizacion, string? orden, bool? descendente,
        bool? incluirBorradores, int? pagina, int? tamano, CancellationToken ct)
    {
        var consulta = db.Mercados.AsNoTracking().Where(m => m.Activo);

        // EL OJO DE BORRADORES LO RESUELVE EL SERVIDOR, no un filtro sobre la página cargada: es la
        // regla de ordenar y filtrar sobre el total. Solo se manda cuando se pide ocultarlos.
        if (incluirBorradores == false)
        {
            consulta = consulta.Where(m => m.EstadoRegistro != "borrador");
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var texto = q.Trim();
            consulta = consulta.Where(m => m.Nombre.Contains(texto) || (m.Descripcion != null && m.Descripcion.Contains(texto)));
        }

        if (!string.IsNullOrWhiteSpace(estado) && !string.Equals(estado, "todos", StringComparison.OrdinalIgnoreCase))
        {
            var pedido = estado.Trim().ToLowerInvariant();
            consulta = consulta.Where(m => m.EstadoRegistro == pedido);
        }

        if (organizacion is > 0)
        {
            consulta = consulta.Where(m => m.OrganizacionPrincipalId == organizacion.Value);
        }

        var desc = descendente ?? false;
        consulta = (orden ?? "nombre").Trim().ToLowerInvariant() switch
        {
            "estado" => desc
                ? consulta.OrderByDescending(m => m.EstadoRegistro).ThenBy(m => m.Nombre)
                : consulta.OrderBy(m => m.EstadoRegistro).ThenBy(m => m.Nombre),
            "creacion" => desc
                ? consulta.OrderByDescending(m => m.FechaCreacion).ThenBy(m => m.Id)
                : consulta.OrderBy(m => m.FechaCreacion).ThenBy(m => m.Id),
            _ => desc
                ? consulta.OrderByDescending(m => m.Nombre).ThenBy(m => m.Id)
                : consulta.OrderBy(m => m.Nombre).ThenBy(m => m.Id),
        };

        var total = await consulta.CountAsync(ct);
        var porPagina = Math.Clamp(tamano ?? TamanoPorOmision, 1, TamanoMaximo);
        var actual = Math.Max(pagina ?? 1, 1);
        var filas = await consulta.Skip((actual - 1) * porPagina).Take(porPagina).ToListAsync(ct);

        var lista = await ATarjetasAsync(db, filas, ct);
        return Results.Ok(new PagedResponse<MercadoDto>(lista, porPagina, (actual - 1) * porPagina, total));
    }

    private static async Task<IResult> Obtener(int id, PnmcDbContext db, CancellationToken ct)
    {
        var fila = await db.Mercados.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.Activo, ct);
        if (fila is null) return Results.NotFound();

        var lista = await ATarjetasAsync(db, [fila], ct);
        return Results.Ok(lista[0]);
    }

    private static async Task<IResult> Catalogos(PnmcDbContext db, CancellationToken ct) =>
        Results.Ok(await CatalogosAsync(db, ct));

    /// <summary>Los vocabularios del formulario, iguales para la consola y para la organización.</summary>
    internal static async Task<CatalogosDeMercadoDto> CatalogosAsync(PnmcDbContext db, CancellationToken ct)
    {
        var alcances = await db.AlcancesMercado.AsNoTracking()
            .OrderBy(a => a.OrdenVisualizacion).ThenBy(a => a.Nombre)
            .Select(a => new OpcionDeMercadoDto(a.Id, a.Nombre, a.Slug))
            .ToListAsync(ct);
        var modalidades = await db.ModalidadesMercado.AsNoTracking()
            .OrderBy(m => m.OrdenVisualizacion).ThenBy(m => m.Nombre)
            .Select(m => new OpcionDeMercadoDto(m.Id, m.Nombre, m.Slug))
            .ToListAsync(ct);
        // LAS PRACTICAS Y LOS TERRITORIOS SON LOS DEL ECOSISTEMA, el mismo catálogo que llena un
        // Festival: un mercado no tiene vocabulario propio para lo que suena en él.
        var practicas = await db.PracticasMusicales.AsNoTracking()
            .OrderBy(x => x.Orden).ThenBy(x => x.Nombre)
            .Select(x => new CatalogoDeMercadoDto(x.Id, x.Nombre))
            .ToListAsync(ct);
        var territorios = await db.TerritoriosSonoros.AsNoTracking()
            .OrderBy(x => x.Orden).ThenBy(x => x.Nombre)
            .Select(x => new CatalogoDeMercadoDto(x.Id, x.Nombre))
            .ToListAsync(ct);
        return new CatalogosDeMercadoDto(alcances, modalidades, practicas, territorios);
    }

    /// <summary>
    /// Los festivales que ese mercado puede declarar como marco.
    /// </summary>
    /// <remarks>
    /// <b>ACOTA LA PERTENENCIA, NO EL ESTADO.</b> La dirección está definido con esas
    /// palabras: «podrá seleccionarse aunque se encuentre en estado borrador, en revisión o
    /// publicado. La condición fundamental es que exista como registro y pertenezca a la misma
    /// organización». Un festival que todavía se está redactando es tan real como uno publicado
    /// para efectos de decir dónde ocurre un mercado.
    /// </remarks>
    private static async Task<IResult> FestivalesElegibles(
        int organizacion, PnmcDbContext db, CancellationToken ct)
    {
        if (organizacion <= 0) return Results.Ok(Array.Empty<FestivalElegibleDto>());
        return Results.Ok(await FestivalesDeLaOrganizacionAsync(db, organizacion, ct));
    }

    /// <summary>
    /// Los festivales de una organización, sea cual sea su estado.
    /// </summary>
    /// <remarks>
    /// LA MISMA CONSULTA PARA LOS DOS CANALES. Si la consola y el panel de la organización listaran
    /// candidatos distintos, un mercado registrado por la organización no podría asociarse al mismo
    /// festival que el Programa sí ve, y nadie sabría por qué.
    /// </remarks>
    internal static async Task<List<FestivalElegibleDto>> FestivalesDeLaOrganizacionAsync(
        PnmcDbContext db, int organizacion, CancellationToken ct) =>
        await db.FestivalRecords.AsNoTracking()
            .Where(f => f.OrganizacionPrincipalId == organizacion)
            .OrderBy(f => f.Name)
            .Select(f => new FestivalElegibleDto(f.Id, f.Name, f.StatusCode))
            .ToListAsync(ct);

    // ─────────────────────────── Escritura ───────────────────────────

    private static async Task<IResult> Crear(
        MercadoUpsertRequest peticion, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(peticion);
        var errores = await ValidarAsync(peticion, db, ct);
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        var ahora = DateTime.UtcNow;

        // EL MERCADO Y SU RASTRO SON UNA SOLA UNIDAD, por el mismo motivo que en el canal externo:
        // la bitácora necesita el identificador, y sin transacción un fallo al auditar deja el
        // mercado creado y sin rastro. Dentro de la estrategia de ejecución por el reintento de
        // SQL Server.
        var estrategia = db.Database.CreateExecutionStrategy();
        var fila = await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);

            var nueva = new MercadoRow
            {
                EstadoRegistro = "borrador",
                Activo = true,
                IdUsuarioCreador = Actor(principal),
                FechaCreacion = ahora,
            };
            Volcar(peticion, nueva);

            db.Mercados.Add(nueva);
            await db.SaveChangesAsync(ct);
            await SincronizarCatalogosAsync(db, nueva.Id, peticion, ct);

            Auditar(db, Actor(principal), nueva.Id, "crear", null, Resumen(nueva));
            // DE DONDE VINO EL REGISTRO, dentro de la misma transacción que lo crea: si el alta se
            // deshace, la procedencia se deshace con ella. Creado desde la consola, la procedencia
            // es el Programa; la organización responsable es otra cosa y se guarda en el mercado.
            await ProcedenciaDeRegistro.AnotarAltaInstitucionalAsync(
                db, Modulos.Mercados, nueva.Id.ToString(CultureInfo.InvariantCulture), Actor(principal), ct);
            await db.SaveChangesAsync(ct);

            await transaccion.CommitAsync(ct);
            return nueva;
        });

        var lista = await ATarjetasAsync(db, [fila], ct);
        return Results.Created($"/api/v1/institucional/mercados/{fila.Id}", lista[0]);
    }

    private static async Task<IResult> Guardar(
        int id, MercadoUpsertRequest peticion, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(peticion);
        var fila = await db.Mercados.FirstOrDefaultAsync(m => m.Id == id && m.Activo, ct);
        if (fila is null) return Results.NotFound();

        var errores = await ValidarAsync(peticion, db, ct);
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        var antes = Resumen(fila);
        Volcar(peticion, fila);
        await SincronizarCatalogosAsync(db, fila.Id, peticion, ct);
        fila.FechaActualizacion = DateTime.UtcNow;

        Auditar(db, Actor(principal), fila.Id, "guardar", antes, Resumen(fila));
        await db.SaveChangesAsync(ct);

        var lista = await ATarjetasAsync(db, [fila], ct);
        return Results.Ok(lista[0]);
    }

    /// <summary>
    /// La decisión del Programa sobre un mercado que llegó a revisión.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SOLO DESDE «EN REVISION», Y ESO ES EL CIRCUITO.</b> Decidir sobre un borrador sería
    /// resolver algo que su organización todavía no ha entregado; decidir dos veces sobre lo mismo
    /// dejaría dos decisiones contradictorias en la bitácora sin saber cuál mandó.
    /// </para>
    /// <para>
    /// <b>PEDIR AJUSTES SIN DECIR CUALES NO ES UNA DECISION.</b> Devuelve el registro a la
    /// organización sin nada que corregir, y lo único que consigue es que vuelva igual. Por eso el
    /// motivo es obligatorio en esa rama y viaja al aviso que recibe la organización.
    /// </para>
    /// </remarks>
    /// <summary>Quién está decidiendo, para firmar el cierre de la revisión. Nulo si no consta.</summary>
    private static int? ActorDeLaConsola(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;

    private static async Task<IResult> Decidir(
        int id, DecisionSobreMercado decision, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var mercado = await db.Mercados.FirstOrDefaultAsync(m => m.Id == id && m.Activo, ct);
        if (mercado is null) return Results.NotFound();

        if (!string.Equals(mercado.EstadoRegistro, "en_revision", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                message = "Solo se decide sobre un mercado que esté en revisión.",
                estado = mercado.EstadoRegistro,
            });
        }

        var pedida = (decision.Decision ?? string.Empty).Trim().ToLowerInvariant();
        var motivo = Opcional(decision.Motivo);

        if (pedida == "ajustes" && motivo is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["motivo"] = MotivoObligatorio,
            });
        }

        var ahora = DateTime.UtcNow;
        var estadoAnterior = mercado.EstadoRegistro;
        switch (pedida)
        {
            case "publicar":
                mercado.EstadoRegistro = "publicado";
                mercado.FechaPublicacion = ahora;
                // PUBLICAR CIERRA LA REVISION VIVA: lo que se pidió corregir o se corrigió, o dejó
                // de importar, y en cualquier caso la organización no tiene nada que reenviar.
                await RevisionDeCamposDeMercadoEndpoints.CerrarRevisionVivaAsync(
                    db, id, ActorDeLaConsola(principal), ahora, ct);
                break;
            case "ajustes":
                mercado.EstadoRegistro = "ajustes_solicitados";
                break;
            default:
                return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["decision"] = DecisionDesconocida,
                });
        }

        mercado.FechaActualizacion = ahora;
        Auditar(db, Actor(principal), mercado.Id, "decidir",
            new { estado = estadoAnterior }, new { estado = mercado.EstadoRegistro, motivo });
        await AvisarALaOrganizacionAsync(db, mercado, motivo, ahora, ct);
        await db.SaveChangesAsync(ct);

        var lista = await ATarjetasAsync(db, [mercado], ct);
        return Results.Ok(lista[0]);
    }

    /// <summary>
    /// Deja el aviso en el buzón de quien administra la organización del mercado.
    /// </summary>
    /// <remarks>
    /// VA AL AMBITO EXTERNO: quien lo lee entra por el panel de su organización, no por la consola.
    /// Mandarlo al institucional lo dejaría en una bandeja que esa persona no abre nunca.
    /// </remarks>
    /// <summary>
    /// Retira del ecosistema un mercado ya publicado, con el motivo por el que se retira.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ES UN ARCHIVADO, NO UN BORRADO.</b> El registro conserva su historial y su auditoría:
    /// deja de estar público, y sigue existiendo para poder responder qué pasó con él. La misma
    /// decisión que se toma sobre un Festival desde su fila —«Eliminar del ecosistema»—, que es
    /// como se llama en pantalla porque es lo que significa para quien la usa.
    /// </para>
    /// <para>
    /// <b>SOLO SOBRE UN MERCADO PUBLICADO.</b> Retirar del portal algo que no está en el portal no
    /// es una decisión: lo que no se ha publicado se queda en borrador o se decide en la bandeja.
    /// </para>
    /// <para>
    /// <b>Y EL MOTIVO NO ES OPCIONAL.</b> Quitar del ecosistema un proceso que su organización
    /// registró y que el Programa publicó es de las decisiones que dentro de un año nadie va a
    /// recordar por qué se tomaron. Viaja a la bitácora y al aviso que recibe la organización.
    /// </para>
    /// </remarks>
    private static async Task<IResult> Archivar(
        int id, ArchivarMercadoSolicitud solicitud, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var mercado = await db.Mercados.FirstOrDefaultAsync(m => m.Id == id && m.Activo, ct);
        if (mercado is null) return Results.NotFound();

        if (!string.Equals(mercado.EstadoRegistro, "publicado", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                message = "Solo un mercado publicado puede retirarse del ecosistema desde aquí.",
                estado = mercado.EstadoRegistro,
            });
        }

        var motivo = Opcional(solicitud.Motivo);
        if (motivo is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["motivo"] = MotivoDeRetiroObligatorio,
            });
        }

        var ahora = DateTime.UtcNow;
        var estadoAnterior = mercado.EstadoRegistro;
        mercado.EstadoRegistro = "archivado";
        mercado.FechaActualizacion = ahora;

        // LA MISMA FORMA QUE EL RESTO DEL CIRCUITO —`evento` y `detalle`—, que es de donde lee el
        // historial que ve la organización en su ficha.
        Auditar(db, Actor(principal), mercado.Id, "decidir",
            new { estado = estadoAnterior }, new { estado = mercado.EstadoRegistro, motivo });
        await AvisarALaOrganizacionAsync(db, mercado, motivo, ahora, ct);
        await db.SaveChangesAsync(ct);

        var lista = await ATarjetasAsync(db, [mercado], ct);
        return Results.Ok(lista[0]);
    }

    private static async Task AvisarALaOrganizacionAsync(
        PnmcDbContext db, MercadoRow mercado, string? motivo, DateTime ahora, CancellationToken ct)
    {
        // EL AVISO LO DECIDE EL ESTADO AL QUE SE LLEGO, no un booleano. Era binario —publicado o
        // ajustes— y al llegar el retiro del ecosistema una organización habría recibido, sobre un
        // mercado archivado, un aviso que decía que le pedían ajustes.
        var (evento, titulo, cuerpo) = mercado.EstadoRegistro switch
        {
            "publicado" => (
                "MercadoPublicado",
                "Mercado publicado",
                $"El mercado musical «{mercado.Nombre}» quedó publicado en el ecosistema."),
            "archivado" => (
                "MercadoArchivado",
                "Mercado retirado del ecosistema",
                $"El mercado musical «{mercado.Nombre}» fue retirado del ecosistema. {motivo}"),
            _ => (
                "MercadoConAjustesSolicitados",
                "Ajustes solicitados en tu mercado",
                $"El Programa pidió ajustes en el mercado musical «{mercado.Nombre}». {motivo}"),
        };

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
                EventType = evento,
                AccessScope = SimusAuthentication.ExternalScope,
                Channel = "internal",
                Title = titulo,
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

    // ─────────────────────────── Reglas ───────────────────────────

    internal static async Task<Dictionary<string, string[]>> ValidarAsync(
        MercadoUpsertRequest peticion, PnmcDbContext db, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(peticion.Nombre)) errores["nombre"] = NombreObligatorio;

        if (peticion.OrganizacionId <= 0)
        {
            errores["organizacionId"] = OrganizacionObligatoria;
        }
        else if (!await db.EntityProfiles.AsNoTracking().AnyAsync(e => e.Id == peticion.OrganizacionId, ct))
        {
            errores["organizacionId"] = OrganizacionDesconocida;
        }

        var nivel = (peticion.NivelCobertura ?? string.Empty).Trim().ToLowerInvariant();
        var departamento = Opcional(peticion.CodigoDepartamento);
        var municipio = Opcional(peticion.CodigoMunicipio);

        if (!Array.Exists(NivelesDeCobertura, n => n == nivel))
        {
            errores["nivelCobertura"] = CoberturaInvalida;
        }
        else
        {
            // LA MISMA COHERENCIA QUE VIGILA EL CHECK DE LA BASE, comprobada antes de llegar a ella:
            // sin esto, un nivel mal combinado sale como un 500 sin explicación en vez de como un
            // 400 sobre el campo que hay que corregir.
            var coherente = nivel switch
            {
                "nacional" => departamento is null && municipio is null,
                "departamental" => departamento is not null && municipio is null,
                _ => departamento is not null && municipio is not null,
            };
            if (!coherente) errores["nivelCobertura"] = TerritorioIncoherente;
            else if (departamento is not null && !await ExisteEnDivipolaAsync(db, departamento, municipio, ct))
            {
                errores["codigoDepartamento"] = TerritorioDesconocido;
            }
        }

        if (peticion.AlcanceId is > 0
            && !await db.AlcancesMercado.AsNoTracking().AnyAsync(a => a.Id == peticion.AlcanceId, ct))
        {
            errores["alcanceId"] = AlcanceDesconocido;
        }

        if (peticion.ModalidadId is > 0
            && !await db.ModalidadesMercado.AsNoTracking().AnyAsync(m => m.Id == peticion.ModalidadId, ct))
        {
            errores["modalidadId"] = ModalidadDesconocida;
        }

        // LA PERIODICIDAD ES UN VOCABULARIO CERRADO, Y AQUI NO SE COMPROBABA. Los Festivales sí lo
        // validan desde su alta; los mercados aceptaban cualquier texto, así que el cajón de la
        // consola —que hasta hoy lo pedía con un campo libre— podía guardar «Anual», «anual» o
        // «cada año» como tres periodicidades distintas. El contrato declara ocho y son esas.
        var periodicidad = PeriodicidadesFestival.Normalizar(peticion.Periodicidad);
        if (periodicidad is not null && !PeriodicidadesFestival.Todas.Contains(periodicidad))
        {
            errores["periodicidad"] = ["Elige una de las periodicidades disponibles."];
        }
        // Y LAS DOS QUE NO DICEN CADA CUANTO OBLIGAN A EXPLICARSE, la misma regla que la pantalla.
        else if (PeriodicidadesFestival.RequiereDetalle(periodicidad)
            && string.IsNullOrWhiteSpace(peticion.PeriodicidadDetalle))
        {
            errores["periodicidadDetalle"] = ["Explica cada cuánto ocurre el mercado."];
        }

        await ValidarElFestivalAsync(peticion, db, errores, ct);
        // LOS CATALOGOS SE VALIDAN COMO EL ALCANCE: un identificador que no existe no se guarda.
        var practicasPedidas = (peticion.PracticasMusicalesIds ?? []).Distinct().ToList();
        if (practicasPedidas.Count > 0)
        {
            var conocidas = await db.PracticasMusicales.AsNoTracking().Where(x => practicasPedidas.Contains(x.Id)).CountAsync(ct);
            if (conocidas != practicasPedidas.Count) errores["practicasMusicalesIds"] = ["Alguna práctica musical no existe en el catálogo."];
        }
        var territoriosPedidos = (peticion.TerritoriosSonorosIds ?? []).Distinct().ToList();
        if (territoriosPedidos.Count > 0)
        {
            var conocidos = await db.TerritoriosSonoros.AsNoTracking().Where(x => territoriosPedidos.Contains(x.Id)).CountAsync(ct);
            if (conocidos != territoriosPedidos.Count) errores["territoriosSonorosIds"] = ["Algún territorio sonoro no existe en el catálogo."];
        }

        return errores;
    }

    /// <summary>
    /// Las tres reglas de la relación con el festival, comprobadas en el servidor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>DECIR QUE SI Y NO ELEGIR</b> deja el dato a medias, y <b>elegir sin decir que sí</b> deja
    /// una relación que la ficha no enseña y que nadie sabe que existe. Las dos combinaciones se
    /// rechazan, y el CHECK de la base las vuelve a rechazar por si alguna ruta futura se olvida.
    /// </para>
    /// <para>
    /// <b>Y LA PERTENENCIA.</b> Esta es la que no puede vivir en un CHECK: compara la organización
    /// del mercado con la del festival, dos filas de dos tablas distintas. Sin ella, el selector
    /// acotado de la pantalla sería decoración.
    /// </para>
    /// </remarks>
    private static async Task ValidarElFestivalAsync(
        MercadoUpsertRequest peticion, PnmcDbContext db, Dictionary<string, string[]> errores, CancellationToken ct)
    {
        if (!peticion.SeRealizaEnElMarcoDeUnFestival)
        {
            if (peticion.FestivalId is > 0) errores["festivalId"] = FestivalSobrante;
            return;
        }

        if (peticion.FestivalId is not > 0)
        {
            errores["festivalId"] = FestivalObligatorio;
            return;
        }

        var festival = await db.FestivalRecords.AsNoTracking()
            .Where(f => f.Id == peticion.FestivalId)
            .Select(f => new { f.Id, f.OrganizacionPrincipalId })
            .FirstOrDefaultAsync(ct);

        if (festival is null) errores["festivalId"] = FestivalDesconocido;
        else if (festival.OrganizacionPrincipalId != peticion.OrganizacionId) errores["festivalId"] = FestivalDeOtraOrganizacion;
    }

    private static Task<bool> ExisteEnDivipolaAsync(
        PnmcDbContext db, string departamento, string? municipio, CancellationToken ct) =>
        municipio is null
            ? db.DivipolaLocations.AsNoTracking().AnyAsync(d => d.DepartmentCode == departamento, ct)
            : db.DivipolaLocations.AsNoTracking()
                .AnyAsync(d => d.DepartmentCode == departamento && d.MunicipalityCode == municipio, ct);

    // ─────────────────────────── Traducción ───────────────────────────

    /// <summary>
    /// Deja las prácticas y los territorios del mercado exactamente como los pide la petición.
    /// </summary>
    /// <remarks>
    /// APARTE DE <see cref="Volcar"/> porque las relaciones necesitan el identificador del mercado,
    /// que un alta no tiene hasta guardar. Quita lo que sobra y añade lo que falta; lo que ya estaba
    /// se queda con su fecha, que es su rastro.
    /// </remarks>
    internal static async Task SincronizarCatalogosAsync(
        PnmcDbContext db, int mercadoId, MercadoUpsertRequest peticion, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var practicasPedidas = (peticion.PracticasMusicalesIds ?? []).Distinct().ToHashSet();
        var practicasActuales = await db.MercadosPracticasMusicales.Where(x => x.MercadoId == mercadoId).ToListAsync(ct);
        db.MercadosPracticasMusicales.RemoveRange(practicasActuales.Where(x => !practicasPedidas.Contains(x.PracticaMusicalId)));
        foreach (var id in practicasPedidas.Where(id => !practicasActuales.Exists(x => x.PracticaMusicalId == id)))
        {
            db.MercadosPracticasMusicales.Add(new MercadoPracticaMusicalRow { MercadoId = mercadoId, PracticaMusicalId = id, FechaCreacion = ahora });
        }

        var territoriosPedidos = (peticion.TerritoriosSonorosIds ?? []).Distinct().ToHashSet();
        var territoriosActuales = await db.MercadosTerritoriosSonoros.Where(x => x.MercadoId == mercadoId).ToListAsync(ct);
        db.MercadosTerritoriosSonoros.RemoveRange(territoriosActuales.Where(x => !territoriosPedidos.Contains(x.TerritorioSonoroId)));
        foreach (var id in territoriosPedidos.Where(id => !territoriosActuales.Exists(x => x.TerritorioSonoroId == id)))
        {
            db.MercadosTerritoriosSonoros.Add(new MercadoTerritorioSonoroRow { MercadoId = mercadoId, TerritorioSonoroId = id, FechaCreacion = ahora });
        }
    }

    /// <summary>La ficha pública: lo mismo que la tarjeta, sin correo, teléfono ni observaciones de contacto.</summary>
    internal static MercadoPublicoDto APublico(MercadoDto m) => new(
        m.Id, m.Nombre, m.Descripcion, m.Alcance, m.Modalidad, m.Periodicidad, m.PeriodicidadDetalle,
        m.SitioWebMercado, m.InstagramMercado, m.FacebookMercado, m.OtroEnlaceMercado,
        m.NivelCobertura, m.CodigoDepartamento, m.NombreDepartamento, m.CodigoMunicipio, m.NombreMunicipio, m.LugarEspecifico,
        m.SeRealizaEnElMarcoDeUnFestival, m.FestivalId, m.FestivalNombre, m.OrganizacionNombre,
        m.NumeroDeEdiciones, m.FechaPublicacion, m.PracticasMusicales, m.TerritoriosSonoros);

    internal static void Volcar(MercadoUpsertRequest peticion, MercadoRow fila)
    {
        fila.Nombre = peticion.Nombre.Trim();
        fila.Descripcion = Opcional(peticion.Descripcion);
        fila.AlcanceMercadoId = peticion.AlcanceId is > 0 ? peticion.AlcanceId : null;
        fila.ModalidadMercadoId = peticion.ModalidadId is > 0 ? peticion.ModalidadId : null;
        fila.Periodicidad = PeriodicidadesFestival.Normalizar(peticion.Periodicidad);
        fila.PeriodicidadDetalle = Opcional(peticion.PeriodicidadDetalle);
        fila.CorreoMercado = Opcional(peticion.CorreoMercado);
        fila.TelefonoMercado = Opcional(peticion.TelefonoMercado);
        fila.SitioWebMercado = Opcional(peticion.SitioWebMercado);
        fila.InstagramMercado = Opcional(peticion.InstagramMercado);
        fila.FacebookMercado = Opcional(peticion.FacebookMercado);
        fila.OtroEnlaceMercado = Opcional(peticion.OtroEnlaceMercado);
        fila.ObservacionesContacto = Opcional(peticion.ObservacionesContacto);
        fila.NivelCobertura = peticion.NivelCobertura.Trim().ToLowerInvariant();
        fila.CodigoDepartamento = Opcional(peticion.CodigoDepartamento);
        fila.CodigoMunicipio = Opcional(peticion.CodigoMunicipio);
        fila.LugarEspecifico = Opcional(peticion.LugarEspecifico);
        fila.SeRealizaEnElMarcoDeUnFestival = peticion.SeRealizaEnElMarcoDeUnFestival;
        fila.FestivalId = peticion.SeRealizaEnElMarcoDeUnFestival ? peticion.FestivalId : null;
        fila.OrganizacionPrincipalId = peticion.OrganizacionId;
    }

    /// <summary>
    /// Convierte filas en tarjetas, resolviendo de una vez lo que cada una necesita de fuera.
    /// </summary>
    /// <remarks>
    /// <b>UNA CONSULTA POR CONCEPTO Y NO UNA POR FILA.</b> El nombre del festival, el de la
    /// organización, el del territorio y el número de ediciones son cuatro datos externos; pedirlos
    /// fila a fila son cuatro consultas por mercado, que es el problema que aparece justo cuando la
    /// lista crece y ya no se puede reproducir con tres registros de prueba.
    /// </remarks>
    internal static async Task<List<MercadoDto>> ATarjetasAsync(
        PnmcDbContext db, List<MercadoRow> filas, CancellationToken ct)
    {
        if (filas.Count == 0) return [];

        var idsDeMercado = filas.Select(f => f.Id).ToList();
        var idsDeFestival = filas.Where(f => f.FestivalId is not null).Select(f => f.FestivalId!.Value).Distinct().ToList();
        var idsDeOrganizacion = filas.Select(f => f.OrganizacionPrincipalId).Distinct().ToList();
        var idsDeAlcance = filas.Where(f => f.AlcanceMercadoId is not null).Select(f => f.AlcanceMercadoId!.Value).Distinct().ToList();
        var idsDeModalidad = filas.Where(f => f.ModalidadMercadoId is not null).Select(f => f.ModalidadMercadoId!.Value).Distinct().ToList();

        var festivales = await db.FestivalRecords.AsNoTracking()
            .Where(f => idsDeFestival.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);
        var organizaciones = await db.EntityProfiles.AsNoTracking()
            .Where(e => idsDeOrganizacion.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);
        var alcances = await db.AlcancesMercado.AsNoTracking()
            .Where(a => idsDeAlcance.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Nombre, ct);
        var modalidades = await db.ModalidadesMercado.AsNoTracking()
            .Where(m => idsDeModalidad.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Nombre, ct);
        var ediciones = await db.EdicionesMercado.AsNoTracking()
            .Where(e => idsDeMercado.Contains(e.MercadoId))
            .GroupBy(e => e.MercadoId)
            .Select(g => new { MercadoId = g.Key, Cuantas = g.Count() })
            .ToDictionaryAsync(x => x.MercadoId, x => x.Cuantas, ct);

        var practicasPorMercado = (await db.MercadosPracticasMusicales.AsNoTracking()
                .Where(x => idsDeMercado.Contains(x.MercadoId))
                .Join(db.PracticasMusicales.AsNoTracking(), x => x.PracticaMusicalId, p => p.Id,
                    (x, pm) => new { x.MercadoId, pm.Id, pm.Nombre, pm.Orden })
                .ToListAsync(ct))
            .GroupBy(x => x.MercadoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CatalogoDeMercadoDto>)g.OrderBy(x => x.Orden).ThenBy(x => x.Nombre).Select(x => new CatalogoDeMercadoDto(x.Id, x.Nombre)).ToList());
        var territoriosPorMercado = (await db.MercadosTerritoriosSonoros.AsNoTracking()
                .Where(x => idsDeMercado.Contains(x.MercadoId))
                .Join(db.TerritoriosSonoros.AsNoTracking(), x => x.TerritorioSonoroId, t => t.Id,
                    (x, ts) => new { x.MercadoId, ts.Id, ts.Nombre, ts.Orden })
                .ToListAsync(ct))
            .GroupBy(x => x.MercadoId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CatalogoDeMercadoDto>)g.OrderBy(x => x.Orden).ThenBy(x => x.Nombre).Select(x => new CatalogoDeMercadoDto(x.Id, x.Nombre)).ToList());

        var codigos = filas.Where(f => f.CodigoDepartamento is not null)
            .Select(f => f.CodigoDepartamento!).Distinct().ToList();
        var territorios = await db.DivipolaLocations.AsNoTracking()
            .Where(d => codigos.Contains(d.DepartmentCode))
            .Select(d => new { d.DepartmentCode, d.DepartmentName, d.MunicipalityCode, d.MunicipalityName })
            .ToListAsync(ct);

        var tarjetas = new List<MercadoDto>(filas.Count);
        foreach (var f in filas)
        {
            var departamento = f.CodigoDepartamento is null
                ? null
                : territorios.Find(t => t.DepartmentCode == f.CodigoDepartamento)?.DepartmentName;
            var municipio = f.CodigoMunicipio is null
                ? null
                : territorios.Find(t => t.DepartmentCode == f.CodigoDepartamento && t.MunicipalityCode == f.CodigoMunicipio)?.MunicipalityName;

            tarjetas.Add(new MercadoDto(
                f.Id,
                f.Nombre,
                f.Descripcion,
                f.AlcanceMercadoId,
                f.AlcanceMercadoId is not null && alcances.TryGetValue(f.AlcanceMercadoId.Value, out var alcance) ? alcance : null,
                f.ModalidadMercadoId,
                f.ModalidadMercadoId is not null && modalidades.TryGetValue(f.ModalidadMercadoId.Value, out var modalidad) ? modalidad : null,
                f.Periodicidad,
                f.PeriodicidadDetalle,
                f.CorreoMercado,
                f.TelefonoMercado,
                f.SitioWebMercado,
                f.InstagramMercado,
                f.FacebookMercado,
                f.OtroEnlaceMercado,
                f.ObservacionesContacto,
                f.NivelCobertura,
                f.CodigoDepartamento,
                departamento,
                f.CodigoMunicipio,
                municipio,
                f.LugarEspecifico,
                f.SeRealizaEnElMarcoDeUnFestival,
                f.FestivalId,
                f.FestivalId is not null && festivales.TryGetValue(f.FestivalId.Value, out var festival) ? festival : null,
                f.OrganizacionPrincipalId,
                organizaciones.TryGetValue(f.OrganizacionPrincipalId, out var organizacion) ? organizacion : null,
                f.EstadoRegistro,
                ediciones.TryGetValue(f.Id, out var cuantas) ? cuantas : 0,
                f.FechaCreacion,
                f.FechaActualizacion,
                f.FechaPublicacion,
                practicasPorMercado.TryGetValue(f.Id, out var practicasDelMercado) ? practicasDelMercado : [],
                territoriosPorMercado.TryGetValue(f.Id, out var territoriosDelMercado) ? territoriosDelMercado : []));
        }

        return tarjetas;
    }

    private static string? Opcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : 0;

    internal static object Resumen(MercadoRow fila) => new
    {
        fila.Nombre,
        fila.EstadoRegistro,
        fila.NivelCobertura,
        fila.CodigoDepartamento,
        fila.CodigoMunicipio,
        fila.OrganizacionPrincipalId,
        fila.SeRealizaEnElMarcoDeUnFestival,
        fila.FestivalId,
    };

    /// <summary>
    /// Escribe un movimiento del mercado en la bitácora, con la forma que lee su historial.
    /// </summary>
    /// <remarks>
    /// <b>ES `internal` PARA QUE LA REVISION POR CAMPOS ESCRIBA IGUAL.</b> Esa devolución también
    /// mueve el estado del mercado, y si lo auditara con otra forma —otro `evento`, otro `detalle`—
    /// el historial que ve la organización diría «actualización registrada» donde hubo una
    /// devolución con cambios pedidos. Una sola función, una sola forma.
    /// </remarks>
    internal static void Auditar(PnmcDbContext db, int actor, int id, string accion, object? antes, object? despues) =>
        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = actor,
            TableName = "Mercados",
            RecordId = id.ToString(CultureInfo.InvariantCulture),
            // EL VERBO SE TRADUCE: `CK_BitacoraAuditoria_Accion` solo admite trece, y ni «guardar»
            // ni «decidir» están entre ellos. Ver AccionesAuditoria.
            Action = AccionesAuditoria.DeMercado(accion),
            PreviousValuesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            NewValuesJson = JsonSerializer.Serialize(new { evento = accion, detalle = despues }),
            CreatedAt = DateTime.UtcNow,
        });
}
