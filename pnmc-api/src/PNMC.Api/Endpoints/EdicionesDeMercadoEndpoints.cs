using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Las ediciones de un mercado musical: cada realización, con su año y su ciclo.
/// </summary>
/// <remarks>
/// <para>
/// <b>NO PASAN POR REVISION INSTITUCIONAL, y esa es la única diferencia real de ciclo de vida
/// respecto de la edición de un Festival.</b> Lo decidió la dirección de producto el 15 de
/// septiembre de 2026: el control de calidad está en la puerta de entrada del mercado, no en cada
/// realización. Quien publica una edición ya demostró, al publicar el mercado, que su proceso está
/// validado por el Programa.
/// </para>
/// <para>
/// <b>SOLO UN MERCADO PUBLICADO TIENE EDICIONES.</b> Anunciar la realización de un proceso que el
/// Programa todavía no ha aprobado sería publicar por la puerta de atrás lo que el circuito de
/// revisión existe para decidir. En borrador el mercado se describe; sus realizaciones vienen
/// después.
/// </para>
/// <para>
/// <b>UN AÑO, UNA EDICION.</b> Lo garantiza <c>UQ_EdicionesMercado_MercadoAnio</c> en la base y se
/// comprueba aquí antes para poder decirlo con un 409 y no con un 500 sin explicación.
/// </para>
/// </remarks>
public static class EdicionesDeMercadoEndpoints
{
    private static readonly string[] EstadosDelAcontecimiento = ["en_preparacion", "programada", "realizada", "cancelada"];
    private static readonly string[] EstadosDeVisibilidad = ["borrador", "publicado", "archivado"];

    private static readonly string[] AnioFueraDeRango = ["El año de la edición no es razonable."];
    private static readonly string[] EstadoDesconocido = ["El estado debe ser en preparación, programada, realizada o cancelada."];
    private static readonly string[] VisibilidadDesconocida = ["La visibilidad debe ser borrador, publicado o archivado."];
    private static readonly string[] FinAntesDelInicio = ["La edición no puede terminar antes de empezar."];
    private static readonly string[] MunicipioSinDepartamento = ["Elige también el departamento: un municipio sin su departamento no ubica la edición."];
    private static readonly string[] TerritorioDesconocido = ["Ese departamento o municipio no está en DIVIPOLA."];
    private static readonly string[] MercadoSinPublicar = ["Solo un mercado publicado puede tener ediciones."];

    public static RouteGroupBuilder MapEdicionesDeMercadoEndpoints(this RouteGroupBuilder api)
    {
        // ── La consola ──────────────────────────────────────────────────────────────────────────
        var consola = api.MapGroup("/institucional/mercados/{mercadoId:int}/ediciones")
            .WithTags("ediciones-de-mercado")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("mercados");

        consola.MapGet(string.Empty, async (int mercadoId, PnmcDbContext db, CancellationToken ct) =>
            await ListarAsync(mercadoId, db, ct)).WithName("ListarEdicionesDeMercado");

        consola.MapPost(string.Empty, async (
            int mercadoId, EdicionDeMercadoUpsertRequest peticion, ClaimsPrincipal principal,
            PnmcDbContext db, CancellationToken ct) =>
            await CrearAsync(mercadoId, peticion, Actor(principal), db, ct, desdeLaOrganizacion: false)).WithName("CrearEdicionDeMercado");

        consola.MapPut("/{edicionId:int}", async (
            int mercadoId, int edicionId, EdicionDeMercadoUpsertRequest peticion, ClaimsPrincipal principal,
            PnmcDbContext db, CancellationToken ct) =>
            await GuardarAsync(mercadoId, edicionId, peticion, Actor(principal), db, ct)).WithName("GuardarEdicionDeMercado");

        // EL CICLO DE VISIBILIDAD DE UNA EDICION, que es lo que decide si el portal la enseña.
        // Existía en las ediciones de un Festival desde hace cortes y aquí no: una edición de
        // mercado se creaba y se guardaba, y se quedaba en borrador para siempre.
        consola.MapPost("/{edicionId:int}/publicar", async (
            int mercadoId, int edicionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
            await CambiarVisibilidadAsync(mercadoId, edicionId, "publicado", Actor(principal), db, ct)).WithName("PublicarEdicionDeMercado");

        consola.MapPost("/{edicionId:int}/despublicar", async (
            int mercadoId, int edicionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
            await CambiarVisibilidadAsync(mercadoId, edicionId, "borrador", Actor(principal), db, ct)).WithName("DespublicarEdicionDeMercado");

        consola.MapPost("/{edicionId:int}/archivar", async (
            int mercadoId, int edicionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
            await CambiarVisibilidadAsync(mercadoId, edicionId, "archivado", Actor(principal), db, ct)).WithName("ArchivarEdicionDeMercado");

        consola.MapDelete("/{edicionId:int}", async (
            int mercadoId, int edicionId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
            await EliminarAsync(mercadoId, edicionId, Actor(principal), db, ct)).WithName("EliminarEdicionDeMercado");

        // ── El panel de la organización ─────────────────────────────────────────────────────────
        //
        // LA MISMA CAPACIDAD DEL OTRO LADO DE LA PUERTA, con las mismas reglas: lo que cambia es
        // quién pregunta y que aquí hay que comprobar que el mercado es suyo.
        var externo = api.MapGroup("/externo/mercados/{mercadoId:int}/ediciones")
            .WithTags("ediciones-de-mercado-externas");
        externo.RequireAuthorization(SimusAuthentication.ExternalPolicy);

        externo.MapGet(string.Empty, async (
            int mercadoId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
        {
            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            return fallo ?? await ListarAsync(mercadoId, db, ct);
        }).WithName("ListarEdicionesDeMiMercado");

        externo.MapPost(string.Empty, async (
            int mercadoId, EdicionDeMercadoUpsertRequest peticion, ClaimsPrincipal principal,
            PnmcDbContext db, IAntiforgery antiforgery, HttpContext contexto, CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "La edición no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }
            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            return fallo ?? await CrearAsync(mercadoId, peticion, SesionExterna.PersonaDe(principal) ?? 0, db, ct, desdeLaOrganizacion: true);
        }).WithName("CrearEdicionDeMiMercado");

        externo.MapPut("/{edicionId:int}", async (
            int mercadoId, int edicionId, EdicionDeMercadoUpsertRequest peticion, ClaimsPrincipal principal,
            PnmcDbContext db, IAntiforgery antiforgery, HttpContext contexto, CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "El cambio no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }
            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            return fallo ?? await GuardarAsync(mercadoId, edicionId, peticion, SesionExterna.PersonaDe(principal) ?? 0, db, ct);
        }).WithName("GuardarEdicionDeMiMercado");

        // LAS MISMAS CUATRO DEL OTRO LADO DE LA PUERTA, con las mismas reglas. Quien anuncia una
        // realización es quien la organiza; el Programa puede hacerlo también, pero no en su lugar.
        foreach (var (ruta, estado, nombre) in new[]
        {
            ("/{edicionId:int}/publicar", "publicado", "PublicarEdicionDeMiMercado"),
            ("/{edicionId:int}/despublicar", "borrador", "DespublicarEdicionDeMiMercado"),
            ("/{edicionId:int}/archivar", "archivado", "ArchivarEdicionDeMiMercado"),
        })
        {
            externo.MapPost(ruta, async (
                int mercadoId, int edicionId, ClaimsPrincipal principal,
                PnmcDbContext db, IAntiforgery antiforgery, HttpContext contexto, CancellationToken ct) =>
            {
                if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
                {
                    return Results.BadRequest(new { message = "El cambio no pudo validarse. Actualiza la página e inténtalo nuevamente." });
                }
                var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
                return fallo ?? await CambiarVisibilidadAsync(mercadoId, edicionId, estado, SesionExterna.PersonaDe(principal) ?? 0, db, ct);
            }).WithName(nombre);
        }

        externo.MapDelete("/{edicionId:int}", async (
            int mercadoId, int edicionId, ClaimsPrincipal principal,
            PnmcDbContext db, IAntiforgery antiforgery, HttpContext contexto, CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "La edición no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }
            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            return fallo ?? await EliminarAsync(mercadoId, edicionId, SesionExterna.PersonaDe(principal) ?? 0, db, ct);
        }).WithName("EliminarEdicionDeMiMercado");

        // ── La consulta pública ─────────────────────────────────────────────────────────────────
        //
        // SOLO LAS PUBLICADAS. Una edición en borrador es trabajo de la organización sobre algo que
        // todavía no ha decidido anunciar.
        api.MapGroup("/publico/mercados/{mercadoId:int}/ediciones")
            .WithTags("mercados-publico")
            .AllowAnonymous()
            .MapGet(string.Empty, async (int mercadoId, PnmcDbContext db, CancellationToken ct) =>
            {
                var publicado = await db.Mercados.AsNoTracking()
                    .AnyAsync(m => m.Id == mercadoId && m.Activo && m.EstadoRegistro == "publicado", ct);
                if (!publicado) return Results.NotFound();

                return await ListarAsync(mercadoId, db, ct, soloPublicadas: true);
            })
            .WithName("ListarEdicionesPublicasDeMercado");

        return api;
    }

    // ─────────────────────────── Lo que hacen las tres puertas ───────────────────────────

    private static async Task<IResult> ListarAsync(
        int mercadoId, PnmcDbContext db, CancellationToken ct, bool soloPublicadas = false)
    {
        var consulta = db.EdicionesMercado.AsNoTracking().Where(e => e.MercadoId == mercadoId);
        if (soloPublicadas) consulta = consulta.Where(e => e.EstadoVisibilidad == "publicado");

        // LO MAS RECIENTE PRIMERO: quien abre la lista de ediciones quiere la última.
        var filas = await consulta.OrderByDescending(e => e.Anio).ThenByDescending(e => e.Id).ToListAsync(ct);
        return Results.Ok(await ATarjetasAsync(db, filas, ct));
    }

    /// <param name="desdeLaOrganizacion">
    /// Por qué puerta entró la edición, que es lo único que distingue a las dos altas.
    /// <b>DECIDE LA PROCEDENCIA Y NADA MAS:</b> la misma edición, creada por la organización o
    /// incorporada por el Programa, no es el mismo registro a efectos de saber de dónde vino. Cada
    /// edición puede haber entrado por un canal distinto del mercado que la contiene.
    /// </param>
    private static async Task<IResult> CrearAsync(
        int mercadoId, EdicionDeMercadoUpsertRequest peticion, int actor, PnmcDbContext db, CancellationToken ct,
        bool desdeLaOrganizacion)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var mercado = await db.Mercados.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mercadoId && m.Activo, ct);
        if (mercado is null) return Results.NotFound();
        if (!string.Equals(mercado.EstadoRegistro, "publicado", StringComparison.Ordinal))
        {
            return Results.Conflict(new { message = MercadoSinPublicar[0], estado = mercado.EstadoRegistro });
        }

        var errores = await ValidarAsync(peticion, db, ct);
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        // UN AÑO, UNA EDICION. Se comprueba antes para poder decirlo con un 409 y no con el choque
        // del índice único, que sale como un 500 sin explicación.
        if (await db.EdicionesMercado.AsNoTracking().AnyAsync(e => e.MercadoId == mercadoId && e.Anio == peticion.Anio, ct))
        {
            return Results.Conflict(new
            {
                message = $"Este mercado ya tiene una edición de {peticion.Anio}. Abre esa y corrígela en vez de crear otra.",
                anio = peticion.Anio,
            });
        }

        var ahora = DateTime.UtcNow;
        var estrategia = db.Database.CreateExecutionStrategy();
        var edicion = await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);

            var fila = new EdicionMercadoRow
            {
                MercadoId = mercadoId,
                IdUsuarioCreador = actor,
                FechaCreacion = ahora,
            };
            Volcar(peticion, fila, ahora);

            db.EdicionesMercado.Add(fila);
            await db.SaveChangesAsync(ct);

            Auditar(db, actor, fila.Id, "crear", null, Resumen(fila), ahora);
            var registro = fila.Id.ToString(CultureInfo.InvariantCulture);
            if (desdeLaOrganizacion)
            {
                await ProcedenciaDeRegistro.AnotarAsync(
                    db, Modulos.EdicionesDeMercado, registro,
                    ProcedenciaDeRegistro.Externo, mercado.OrganizacionPrincipalId, actor, ct);
            }
            else
            {
                await ProcedenciaDeRegistro.AnotarAltaInstitucionalAsync(
                    db, Modulos.EdicionesDeMercado, registro, actor, ct);
            }
            await db.SaveChangesAsync(ct);

            await transaccion.CommitAsync(ct);
            return fila;
        });

        var tarjetas = await ATarjetasAsync(db, [edicion], ct);
        return Results.Created($"/api/v1/institucional/mercados/{mercadoId}/ediciones/{edicion.Id}", tarjetas[0]);
    }

    /// <summary>
    /// Cambia si el portal enseña esta edición: publicarla, retirarla o archivarla.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>LOS DOS EJES NO SE MEZCLAN.</b> <c>EstadoVisibilidad</c> dice si el portal la enseña;
    /// <c>Estado</c> describe el acontecimiento —en preparación, programada, realizada, cancelada—.
    /// Esta ruta solo toca el primero: una edición se puede publicar antes de ocurrir y sigue
    /// publicada después de ocurrida.
    /// </para>
    /// <para>
    /// <b>NO SE PUBLICA UNA EDICION DE UN MERCADO QUE NO ESTA PUBLICADO.</b> El portal no tiene
    /// dónde enseñarla: la ficha del mercado no existe todavía para el público, así que la edición
    /// quedaría publicada y invisible, que es la peor de las dos cosas.
    /// </para>
    /// <para>
    /// <b>Y ARCHIVAR NO SE DESHACE POR AQUI.</b> Se archiva lo que ya no va a volver; volver a
    /// publicarlo sería reabrir una decisión sin dejar constancia de que se reabrió.
    /// </para>
    /// </remarks>
    private static async Task<IResult> CambiarVisibilidadAsync(
        int mercadoId, int edicionId, string visibilidad, int actor, PnmcDbContext db, CancellationToken ct)
    {
        var edicion = await db.EdicionesMercado.FirstOrDefaultAsync(e => e.Id == edicionId && e.MercadoId == mercadoId, ct);
        if (edicion is null) return Results.NotFound();

        var anterior = edicion.EstadoVisibilidad;
        if (string.Equals(anterior, "archivado", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { message = "Una edición archivada ya no cambia de visibilidad.", estadoVisibilidad = anterior });
        }
        if (string.Equals(anterior, visibilidad, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { message = "La edición ya está en ese estado.", estadoVisibilidad = anterior });
        }

        if (visibilidad == "publicado")
        {
            var mercadoPublicado = await db.Mercados.AsNoTracking()
                .AnyAsync(m => m.Id == mercadoId && m.Activo && m.EstadoRegistro == "publicado", ct);
            if (!mercadoPublicado)
            {
                return Results.Conflict(new
                {
                    message = "Publica primero el mercado: mientras no esté en el portal, su edición quedaría publicada y sin dónde verse.",
                });
            }
        }

        var ahora = DateTime.UtcNow;
        edicion.EstadoVisibilidad = visibilidad;
        edicion.FechaActualizacion = ahora;

        var evento = visibilidad switch
        {
            "publicado" => "publicar",
            "archivado" => "archivar",
            _ => "despublicar",
        };
        Auditar(db, actor, edicion.Id, evento, new { estado = anterior }, new { estado = visibilidad }, ahora);
        await db.SaveChangesAsync(ct);

        var tarjetas = await ATarjetasAsync(db, [edicion], ct);
        return Results.Ok(tarjetas[0]);
    }

    /// <summary>
    /// Borra una edición que nunca llegó al portal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>LO QUE SE PUBLICO NO SE BORRA, SE ARCHIVA.</b> Y la pregunta no es «¿está publicada
    /// ahora?» sino «¿llegó a publicarse alguna vez?»: una edición despublicada vuelve a
    /// <c>borrador</c> y ya la vio cualquiera. La respuesta está en la bitácora de auditoría, que
    /// registra cada publicación desde el primer día.
    /// </para>
    /// <para>
    /// <b>Y TAMPOCO SE BORRA LO QUE YA OCURRIO.</b> Una edición realizada o cancelada es un hecho
    /// del mercado: se archiva y queda su rastro. Solo desaparece lo que estaba en preparación o
    /// programado y nunca se anunció, que es el borrador equivocado que alguien quiere deshacer.
    /// </para>
    /// </remarks>
    private static async Task<IResult> EliminarAsync(
        int mercadoId, int edicionId, int actor, PnmcDbContext db, CancellationToken ct)
    {
        var edicion = await db.EdicionesMercado.FirstOrDefaultAsync(e => e.Id == edicionId && e.MercadoId == mercadoId, ct);
        if (edicion is null) return Results.NotFound();

        if (!string.Equals(edicion.EstadoVisibilidad, "borrador", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new
            {
                message = "Solo se borra una edición que está en borrador. Las publicadas y las archivadas se conservan.",
                estadoVisibilidad = edicion.EstadoVisibilidad,
            });
        }

        if (edicion.Estado is "realizada" or "cancelada")
        {
            return Results.Conflict(new
            {
                message = "Una edición realizada o cancelada es un hecho del mercado: archívala en vez de borrarla.",
                estado = edicion.Estado,
            });
        }

        var registro = edicionId.ToString(CultureInfo.InvariantCulture);
        var llegoAPublicarse = await db.AuditLogs.AsNoTracking().AnyAsync(
            a => a.TableName == "EdicionesMercado" && a.RecordId == registro && a.Action == AccionesAuditoria.Publicar, ct);
        if (llegoAPublicarse)
        {
            return Results.Conflict(new
            {
                message = "Esta edición estuvo publicada en el portal. Archívala: borrarla dejaría su rastro en la bitácora apuntando a un registro que ya no existe.",
            });
        }

        var ahora = DateTime.UtcNow;
        // SE AUDITA ANTES DE BORRAR, con lo que la edición era: después no habrá de dónde sacarlo.
        Auditar(db, actor, edicion.Id, "eliminar", Resumen(edicion), null, ahora);
        db.EdicionesMercado.Remove(edicion);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> GuardarAsync(
        int mercadoId, int edicionId, EdicionDeMercadoUpsertRequest peticion, int actor, PnmcDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(peticion);

        var edicion = await db.EdicionesMercado.FirstOrDefaultAsync(e => e.Id == edicionId && e.MercadoId == mercadoId, ct);
        if (edicion is null) return Results.NotFound();

        var errores = await ValidarAsync(peticion, db, ct);
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        if (peticion.Anio != edicion.Anio
            && await db.EdicionesMercado.AsNoTracking().AnyAsync(e => e.MercadoId == mercadoId && e.Anio == peticion.Anio, ct))
        {
            return Results.Conflict(new
            {
                message = $"Este mercado ya tiene una edición de {peticion.Anio}.",
                anio = peticion.Anio,
            });
        }

        var ahora = DateTime.UtcNow;
        var antes = Resumen(edicion);
        Volcar(peticion, edicion, ahora);
        edicion.FechaActualizacion = ahora;

        Auditar(db, actor, edicion.Id, "guardar", antes, Resumen(edicion), ahora);
        await db.SaveChangesAsync(ct);

        var tarjetas = await ATarjetasAsync(db, [edicion], ct);
        return Results.Ok(tarjetas[0]);
    }

    // ─────────────────────────── Reglas ───────────────────────────

    private static async Task<Dictionary<string, string[]>> ValidarAsync(
        EdicionDeMercadoUpsertRequest peticion, PnmcDbContext db, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (peticion.Anio is < 1900 or > 2200) errores["anio"] = AnioFueraDeRango;

        var estado = (peticion.Estado ?? string.Empty).Trim().ToLowerInvariant();
        if (!Array.Exists(EstadosDelAcontecimiento, e => e == estado)) errores["estado"] = EstadoDesconocido;

        var visibilidad = (peticion.EstadoVisibilidad ?? string.Empty).Trim().ToLowerInvariant();
        if (!Array.Exists(EstadosDeVisibilidad, v => v == visibilidad)) errores["estadoVisibilidad"] = VisibilidadDesconocida;

        if (peticion.FechaInicio is { } inicio && peticion.FechaFin is { } fin && fin < inicio)
        {
            errores["fechaFin"] = FinAntesDelInicio;
        }

        var departamento = Opcional(peticion.CodigoDepartamento);
        var municipio = Opcional(peticion.CodigoMunicipio);
        if (departamento is null && municipio is not null)
        {
            errores["codigoDepartamento"] = MunicipioSinDepartamento;
        }
        else if (departamento is not null)
        {
            var existe = municipio is null
                ? await db.DivipolaLocations.AsNoTracking().AnyAsync(d => d.DepartmentCode == departamento, ct)
                : await db.DivipolaLocations.AsNoTracking()
                    .AnyAsync(d => d.DepartmentCode == departamento && d.MunicipalityCode == municipio, ct);
            if (!existe) errores["codigoDepartamento"] = TerritorioDesconocido;
        }

        return errores;
    }

    /// <summary>Quien pide administra la organización que responde por ese mercado.</summary>
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

    // ─────────────────────────── Traducción ───────────────────────────

    private static void Volcar(EdicionDeMercadoUpsertRequest peticion, EdicionMercadoRow fila, DateTime ahora)
    {
        fila.Anio = peticion.Anio;
        fila.NumeroEdicion = peticion.NumeroEdicion is > 0 ? peticion.NumeroEdicion : null;
        fila.Nombre = Opcional(peticion.Nombre);
        fila.Descripcion = Opcional(peticion.Descripcion);
        fila.FechaInicio = peticion.FechaInicio?.ToDateTime(TimeOnly.MinValue);
        fila.FechaFin = peticion.FechaFin?.ToDateTime(TimeOnly.MinValue);
        fila.CodigoDepartamento = Opcional(peticion.CodigoDepartamento);
        fila.CodigoMunicipio = Opcional(peticion.CodigoMunicipio);
        fila.LugarEspecifico = Opcional(peticion.LugarEspecifico);
        fila.Estado = (peticion.Estado ?? "en_preparacion").Trim().ToLowerInvariant();

        var visibilidad = (peticion.EstadoVisibilidad ?? "borrador").Trim().ToLowerInvariant();
        // LA FECHA DE PUBLICACION SE ESCRIBE CUANDO SE PUBLICA, y solo la primera vez: reescribirla
        // en cada guardado convertiría «desde cuándo se ve» en «cuándo se tocó por última vez».
        if (visibilidad == "publicado" && fila.EstadoVisibilidad != "publicado") fila.FechaPublicacion = ahora;
        fila.EstadoVisibilidad = visibilidad;
    }

    private static async Task<List<EdicionDeMercadoDto>> ATarjetasAsync(
        PnmcDbContext db, List<EdicionMercadoRow> filas, CancellationToken ct)
    {
        if (filas.Count == 0) return [];

        var codigos = filas.Where(e => e.CodigoDepartamento is not null)
            .Select(e => e.CodigoDepartamento!).Distinct().ToList();
        var territorios = await db.DivipolaLocations.AsNoTracking()
            .Where(d => codigos.Contains(d.DepartmentCode))
            .Select(d => new { d.DepartmentCode, d.DepartmentName, d.MunicipalityCode, d.MunicipalityName })
            .ToListAsync(ct);

        return filas.Select(e => new EdicionDeMercadoDto(
            e.Id,
            e.MercadoId,
            e.Anio,
            e.NumeroEdicion,
            e.Nombre,
            e.Descripcion,
            e.FechaInicio is { } inicio ? DateOnly.FromDateTime(inicio) : null,
            e.FechaFin is { } fin ? DateOnly.FromDateTime(fin) : null,
            e.CodigoDepartamento,
            e.CodigoDepartamento is null ? null : territorios.Find(t => t.DepartmentCode == e.CodigoDepartamento)?.DepartmentName,
            e.CodigoMunicipio,
            e.CodigoMunicipio is null ? null : territorios.Find(t => t.DepartmentCode == e.CodigoDepartamento && t.MunicipalityCode == e.CodigoMunicipio)?.MunicipalityName,
            e.LugarEspecifico,
            e.Estado,
            e.EstadoVisibilidad,
            e.FechaCreacion,
            e.FechaActualizacion,
            e.FechaPublicacion)).ToList();
    }

    private static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static int Actor(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), CultureInfo.InvariantCulture, out var id) ? id : 0;

    private static object Resumen(EdicionMercadoRow fila) => new
    {
        fila.Anio,
        fila.Nombre,
        fila.Estado,
        fila.EstadoVisibilidad,
        fila.FechaInicio,
        fila.FechaFin,
    };

    private static void Auditar(
        PnmcDbContext db, int actor, int id, string evento, object? antes, object? despues, DateTime ahora) =>
        db.AuditLogs.Add(new AuditLogRow
        {
            UserId = actor,
            TableName = "EdicionesMercado",
            RecordId = id.ToString(CultureInfo.InvariantCulture),
            // EL VERBO SE TRADUCE: `CK_BitacoraAuditoria_Accion` solo admite trece. Ver AccionesAuditoria.
            Action = AccionesAuditoria.DeMercado(evento),
            PreviousValuesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            NewValuesJson = JsonSerializer.Serialize(new { evento, detalle = despues }),
            CreatedAt = ahora,
        });
}
