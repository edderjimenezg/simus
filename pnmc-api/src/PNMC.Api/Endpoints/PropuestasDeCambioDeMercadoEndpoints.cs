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
/// La propuesta de cambio de una organización sobre un mercado suyo YA PUBLICADO.
/// </summary>
/// <remarks>
/// <para>
/// <b>POR QUE NO SE EDITA Y YA.</b> Lo que ve el público no puede cambiar porque alguien abrió un
/// formulario. Entre la intención de la organización y la ficha publicada tiene que haber una
/// decisión del Programa: eso es todo lo que hace este circuito, y es el último que le faltaba a
/// Mercados para funcionar como Festivales.
/// </para>
/// <para>
/// <b>EL BORRADOR ES DE LA ORGANIZACION.</b> Puede guardarlo tantas veces como quiera y abandonarlo;
/// nadie del Programa lo ve hasta que se envía. Simétrico al borrador de revisión, que es del
/// funcionario hasta que lo manda.
/// </para>
/// <para>
/// <b>EL VALOR ANTERIOR SE COPIA AL ENVIAR</b>, no antes ni después: antes, el registro todavía
/// podía cambiar por otra vía; después, aplicar la propuesta haría que la comparación dijera que no
/// cambió nada, porque el «antes» ya sería el «después».
/// </para>
/// <para>
/// <b>QUE CAMPOS SE PUEDEN PROPONER LO DECIDE EL SERVIDOR</b>, en
/// <see cref="CamposProponiblesDeMercado"/>. La tabla es genérica y guarda pares «campo, valor» como
/// texto; si la correspondencia con las columnas la trajera el cliente, quien propone elegiría qué
/// columna escribir.
/// </para>
/// </remarks>
public static class PropuestasDeCambioDeMercadoEndpoints
{
    /// <summary>El módulo con el que viajan estas propuestas. El mismo vocabulario de la auditoría.</summary>
    internal const string Modulo = Modulos.Mercados;

    private const string Publicado = "publicado";

    public static RouteGroupBuilder MapPropuestasDeCambioDeMercadoEndpoints(this RouteGroupBuilder group)
    {
        MapearLoExterno(group);
        MapearLoInstitucional(group);
        return group;
    }

    // ── Lo que la organización propone ──────────────────────────────────────────────────────────
    private static void MapearLoExterno(RouteGroupBuilder group)
    {
        var externo = group.MapGroup("/externo/mercados")
            .WithTags("propuestas-de-cambio-de-mercado")
            .RequireAuthorization(SimusAuthentication.ExternalPolicy);

        externo.MapGet("/{mercadoId:int}/propuesta", async (
            int mercadoId, ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
        {
            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;

            var mercado = await db.Mercados.AsNoTracking().FirstAsync(m => m.Id == mercadoId, ct);
            var propuesta = await PropuestaVivaAsync(db, mercadoId, seguimiento: false, ct);
            return Results.Ok(await ADtoAsync(db, mercado, propuesta, ct));
        }).WithName("PropuestaDeCambioDeMiMercado");

        externo.MapPut("/{mercadoId:int}/propuesta", async (
            int mercadoId,
            GuardarPropuestaDeCambioSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(solicitud);
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "La propuesta no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;

            var mercado = await db.Mercados.FirstAsync(m => m.Id == mercadoId, ct);
            // SOLO SOBRE LO PUBLICADO. Un mercado en borrador o en revisión se edita directamente;
            // proponer un cambio sobre él sería un circuito de más para llegar al mismo sitio.
            if (mercado.EstadoRegistro != Publicado)
            {
                return Results.Conflict(new { message = "Solo se proponen cambios sobre un mercado publicado. Este todavía puedes editarlo directamente.", estado = mercado.EstadoRegistro });
            }

            var (campos, errores) = Normalizar(solicitud.Campos);
            if (errores.Count > 0) return Results.ValidationProblem(errores);

            var ahora = DateTime.UtcNow;
            var propuesta = await PropuestaVivaAsync(db, mercadoId, seguimiento: true, ct);
            if (propuesta is null)
            {
                propuesta = await AbrirPropuestaAsync(db, mercado, Actor(principal), ahora, ct);
            }
            else if (propuesta.Estado == EstadosDePropuesta.EnRevision)
            {
                // MIENTRAS EL PROGRAMA LA MIRA, NO SE TOCA. Cambiarla por debajo haría que la
                // decisión se tomara sobre algo distinto de lo que se leyó.
                return Results.Conflict(new { message = "La propuesta está en revisión: no puede modificarse hasta que el Programa decida.", estado = propuesta.Estado });
            }

            propuesta.Motivo = LimpiarTexto(solicitud.Motivo, 2400);
            propuesta.FechaActualizacion = ahora;
            await ReemplazarCamposAsync(db, propuesta, campos, ahora, ct);
            await db.SaveChangesAsync(ct);

            return Results.Ok(await ADtoAsync(db, mercado, propuesta, ct));
        }).WithName("GuardarPropuestaDeCambioDeMiMercado");

        externo.MapPost("/{mercadoId:int}/propuesta/enviar", async (
            int mercadoId,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "El envío no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;

            var propuesta = await PropuestaVivaAsync(db, mercadoId, seguimiento: true, ct);
            if (propuesta is null) return Results.NotFound();
            if (propuesta.Estado == EstadosDePropuesta.EnRevision)
            {
                return Results.Conflict(new { message = "Esta propuesta ya está esperando una decisión del Programa.", estado = propuesta.Estado });
            }

            var mercado = await db.Mercados.AsNoTracking().FirstAsync(m => m.Id == mercadoId, ct);
            var campos = await db.PropuestasDeCambioCampos
                .Where(c => c.IdPropuesta == propuesta.Id).ToListAsync(ct);
            if (campos.Count == 0)
            {
                return Results.Conflict(new { message = "La propuesta no cambia ningún campo. Indica al menos uno antes de enviarla." });
            }

            var ahora = DateTime.UtcNow;

            // AQUI SE CONGELA EL «ANTES». Es el momento en que el Programa se compromete a mirar
            // esto, y contra esto es contra lo que se compara.
            foreach (var campo in campos)
            {
                campo.ValorAnterior = await CamposProponiblesDeMercado.ValorActualAsync(db, mercado, campo.CampoId, ct);
                campo.FechaActualizacion = ahora;
            }

            propuesta.Estado = EstadosDePropuesta.EnRevision;
            propuesta.FechaEnvio = ahora;
            propuesta.FechaActualizacion = ahora;
            MercadosEndpoints.Auditar(db, Actor(principal), mercadoId, "MercadoCambioPropuesto",
                antes: null, despues: new { propuesta = propuesta.Id, campos = campos.Count });
            await db.SaveChangesAsync(ct);

            return Results.Ok(await ADtoAsync(db, mercado, propuesta, ct));
        }).WithName("EnviarPropuestaDeCambioDeMiMercado");

        externo.MapDelete("/{mercadoId:int}/propuesta", async (
            int mercadoId,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            if (!await SesionExterna.VieneDeNuestraPaginaAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "La operación no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var fallo = await NoEsSuyoAsync(mercadoId, principal, db, ct);
            if (fallo is not null) return fallo;

            var propuesta = await PropuestaVivaAsync(db, mercadoId, seguimiento: true, ct);
            if (propuesta is null) return Results.NoContent();
            // ABANDONAR UN BORRADOR SI; RETIRAR ALGO QUE EL PROGRAMA YA TIENE DELANTE, NO. Para eso
            // está la decisión, que deja constancia de qué se pidió y qué se contestó.
            if (propuesta.Estado == EstadosDePropuesta.EnRevision)
            {
                return Results.Conflict(new { message = "La propuesta está en revisión y ya no puede abandonarse.", estado = propuesta.Estado });
            }

            // LOS CAMPOS PRIMERO, PORQUE NO HAY CASCADA. La base no borra hijos por su cuenta en
            // ninguna tabla de PNMC, así que el orden es parte de la operación y se ve aquí.
            var campos = await db.PropuestasDeCambioCampos.Where(c => c.IdPropuesta == propuesta.Id).ToListAsync(ct);
            db.PropuestasDeCambioCampos.RemoveRange(campos);
            db.PropuestasDeCambio.Remove(propuesta);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithName("AbandonarPropuestaDeCambioDeMiMercado");
    }

    // ── Lo que el Programa lee y decide ─────────────────────────────────────────────────────────
    private static void MapearLoInstitucional(RouteGroupBuilder group)
    {
        var institucional = group.MapGroup("/institucional/mercados")
            .WithTags("propuestas-de-cambio-de-mercado")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("mercados");

        // EL TESTIGO, EN EL MISMO GRUPO QUE LA DECISION. Es como lo hacen las rutas institucionales
        // de Festival: quien va a decidir pide su testigo aquí y lo manda en la cabecera. Sin esta
        // ruta, la consola no tenía de dónde sacarlo y la decisión moría en un 400 que se leía como
        // «actualiza la página», que es justo lo que no había que hacer.
        institucional.MapGet("/propuestas/csrf", (IAntiforgery antiforgery, HttpContext contexto) =>
        {
            var testigos = antiforgery.GetAndStoreTokens(contexto);
            return Results.Ok(new { requestToken = testigos.RequestToken ?? string.Empty });
        }).WithName("TestigoDePropuestasDeMercado");

        institucional.MapGet("/propuestas/en-revision", async (PnmcDbContext db, CancellationToken ct) =>
        {
            var propuestas = await db.PropuestasDeCambio.AsNoTracking()
                .Where(p => p.ModuloId == Modulo && p.Estado == EstadosDePropuesta.EnRevision)
                .OrderBy(p => p.FechaEnvio)
                .ToListAsync(ct);

            var cuantos = await db.PropuestasDeCambioCampos.AsNoTracking()
                .Where(c => propuestas.Select(p => p.Id).Contains(c.IdPropuesta))
                .GroupBy(c => c.IdPropuesta)
                .Select(g => new { Id = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Total, ct);

            var nombres = await NombresDeLosMercadosAsync(db, propuestas, ct);

            return Results.Ok(propuestas.Select(p => new PropuestaEnRevisionDto(
                p.Id, p.ModuloId, p.RegistroId,
                nombres.TryGetValue(p.RegistroId, out var nombre) ? nombre : $"Mercado {p.RegistroId}",
                p.OrganizacionNombre, p.ProponenteNombre, p.Motivo,
                cuantos.TryGetValue(p.Id, out var total) ? total : 0,
                p.FechaEnvio)).ToArray());
        }).WithName("PropuestasDeMercadoEnRevision");

        institucional.MapGet("/{mercadoId:int}/propuesta", async (
            int mercadoId, PnmcDbContext db, CancellationToken ct) =>
        {
            var mercado = await db.Mercados.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mercadoId && m.Activo, ct);
            if (mercado is null) return Results.NotFound();

            var propuesta = await PropuestaVivaAsync(db, mercadoId, seguimiento: false, ct);
            // SOLO LO ENVIADO. El borrador es de la organización mientras lo escribe: enseñarlo
            // aquí sería leer por encima del hombro algo que todavía nadie ha querido mandar. Se
            // devuelve la carcasa vacía y no un 404, por lo mismo que en la revisión por campos.
            if (propuesta is not null && propuesta.Estado == EstadosDePropuesta.Borrador)
            {
                propuesta = null;
            }

            return Results.Ok(await ADtoAsync(db, mercado, propuesta, ct));
        }).WithName("PropuestaDeCambioDeMercado");

        institucional.MapPost("/propuestas/{propuestaId:long}/decision", async (
            long propuestaId,
            DecidirPropuestaDeCambioSolicitud solicitud,
            ClaimsPrincipal principal,
            PnmcDbContext db,
            IAntiforgery antiforgery,
            HttpContext contexto,
            CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(solicitud);
            if (!await TestigoDePeticion.ValidoAsync(antiforgery, contexto))
            {
                return Results.BadRequest(new { message = "La decisión no pudo validarse. Actualiza la página e inténtalo nuevamente." });
            }

            var propuesta = await db.PropuestasDeCambio
                .FirstOrDefaultAsync(p => p.Id == propuestaId && p.ModuloId == Modulo, ct);
            if (propuesta is null) return Results.NotFound();
            if (propuesta.Estado != EstadosDePropuesta.EnRevision)
            {
                return Results.Conflict(new { message = "Solo una propuesta en revisión puede recibir una decisión.", estado = propuesta.Estado });
            }

            var decision = (solicitud.Decision ?? string.Empty).Trim();
            var motivo = LimpiarTexto(solicitud.Motivo, 2400);
            if (decision is not ("aplicar" or "pedir_ajustes" or "rechazar"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["decision"] = ["La decisión tiene que ser «aplicar», «pedir_ajustes» o «rechazar»."],
                });
            }

            // RECHAZAR O DEVOLVER SIN DECIR POR QUE NO ES UNA DECISION. Aplicar no necesita motivo:
            // el cambio aprobado habla por sí mismo, y la comparación queda guardada.
            if (decision is "rechazar" or "pedir_ajustes" && motivo is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["motivo"] = [decision == "rechazar"
                        ? "Escribe por qué se rechaza: es lo que leerá la organización."
                        : "Escribe qué hay que ajustar: es lo que leerá la organización."],
                });
            }

            if (!int.TryParse(propuesta.RegistroId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mercadoId))
            {
                return Results.NotFound();
            }

            var mercado = await db.Mercados.FirstOrDefaultAsync(m => m.Id == mercadoId && m.Activo, ct);
            if (mercado is null)
            {
                return Results.Conflict(new { message = "El mercado de la propuesta ya no está disponible." });
            }

            var actor = Actor(principal);
            var ahora = DateTime.UtcNow;

            // BAJO LA ESTRATEGIA DE EJECUCION, como la decisión institucional de Festival: con
            // `EnableRetryOnFailure` una transacción abierta fuera de la estrategia lanza en el
            // primer SaveChangesAsync, y la decisión respondería 500 siempre contra SQL Server.
            var estrategia = db.Database.CreateExecutionStrategy();
            return await estrategia.ExecuteAsync<IResult>(async () =>
            {
                await using var transaccion = await db.Database.BeginTransactionAsync(ct);

                if (decision == "aplicar")
                {
                    var aplicado = await AplicarAsync(db, propuesta, mercado, ahora, ct);
                    if (aplicado is not null) return aplicado;
                }

                propuesta.Estado = decision switch
                {
                    "aplicar" => EstadosDePropuesta.Aplicada,
                    "rechazar" => EstadosDePropuesta.Rechazada,
                    _ => EstadosDePropuesta.AjustesSolicitados,
                };
                propuesta.MotivoDeLaDecision = motivo;
                propuesta.IdUsuarioDecide = actor == 0 ? null : actor;
                propuesta.DecideNombre = actor == 0 ? null : await NombreDeUsuarioAsync(db, actor, ct);
                propuesta.FechaActualizacion = ahora;
                propuesta.FechaDecision = EstadosDePropuesta.EstaCerrada(propuesta.Estado) ? ahora : null;

                MercadosEndpoints.Auditar(db, actor, mercadoId,
                    decision switch
                    {
                        "aplicar" => "MercadoPropuestaAplicada",
                        "rechazar" => "MercadoPropuestaRechazada",
                        _ => "MercadoPropuestaConAjustes",
                    },
                    antes: null, despues: new { propuesta = propuesta.Id, motivo });

                await AvisarALaOrganizacionAsync(db, mercado, propuesta.Estado, motivo, ahora, ct);
                await db.SaveChangesAsync(ct);
                await transaccion.CommitAsync(ct);

                return Results.Ok(await ADtoAsync(db, mercado, propuesta, ct));
            });
        }).WithName("DecidirPropuestaDeCambioDeMercado");
    }

    /// <summary>
    /// Escribe en el mercado lo que la propuesta pide, campo por campo.
    /// </summary>
    /// <returns>Un resultado de fallo si algo impide aplicarla; <c>null</c> si se aplicó.</returns>
    /// <remarks>
    /// <b>NO SE APLICA LO QUE NO ESTA EN EL CATALOGO.</b> Un campo que no reconozca
    /// <see cref="CamposProponiblesDeMercado"/> no se escribe: es la misma lista cerrada que impidió
    /// guardarlo, y comprobarla otra vez aquí es lo que hace que una fila metida por otra vía no
    /// llegue nunca a la ficha publicada.
    /// </remarks>
    private static async Task<IResult?> AplicarAsync(
        PnmcDbContext db, PropuestaDeCambioRow propuesta, MercadoRow mercado, DateTime ahora, CancellationToken ct)
    {
        var campos = await db.PropuestasDeCambioCampos.AsNoTracking()
            .Where(c => c.IdPropuesta == propuesta.Id).ToListAsync(ct);

        foreach (var campo in campos)
        {
            if (!CamposProponiblesDeMercado.Todos.TryGetValue(campo.CampoId, out var definicion)) continue;

            if (definicion.Escribir is null)
            {
                await CamposProponiblesDeMercado.AplicarListaAsync(db, mercado, campo.CampoId, campo.ValorPropuesto, ahora, ct);
                continue;
            }

            definicion.Escribir(mercado, campo.ValorPropuesto);
        }

        mercado.FechaActualizacion = ahora;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // LA COHERENCIA ENTRE NIVEL Y TERRITORIO LA GUARDA LA BASE, y es lo que salta aquí: una
            // propuesta que deja «municipal» sin municipio no puede aplicarse. Se contesta como lo
            // que es —un conflicto explicado— y no como un 500.
            return Results.Conflict(new { message = "Los cambios propuestos dejarían el mercado en un estado imposible: revisa el nivel de cobertura y su territorio antes de aplicarlos." });
        }

        return null;
    }

    private static async Task<PropuestaDeCambioRow?> PropuestaVivaAsync(
        PnmcDbContext db, int mercadoId, bool seguimiento, CancellationToken ct)
    {
        var registro = mercadoId.ToString(CultureInfo.InvariantCulture);
        var consulta = seguimiento ? db.PropuestasDeCambio : db.PropuestasDeCambio.AsNoTracking();
        return await consulta
            .Where(p => p.ModuloId == Modulo && p.RegistroId == registro
                        && p.Estado != EstadosDePropuesta.Aplicada
                        && p.Estado != EstadosDePropuesta.Rechazada)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<PropuestaDeCambioRow> AbrirPropuestaAsync(
        PnmcDbContext db, MercadoRow mercado, int proponenteId, DateTime ahora, CancellationToken ct)
    {
        var organizacion = await db.EntityProfiles.AsNoTracking()
            .Where(e => e.Id == mercado.OrganizacionPrincipalId)
            .Select(e => e.Name)
            .FirstOrDefaultAsync(ct);

        var fila = new PropuestaDeCambioRow
        {
            ModuloId = Modulo,
            RegistroId = mercado.Id.ToString(CultureInfo.InvariantCulture),
            Estado = EstadosDePropuesta.Borrador,
            IdOrganizacion = mercado.OrganizacionPrincipalId,
            OrganizacionNombre = organizacion,
            IdUsuarioProponente = proponenteId == 0 ? null : proponenteId,
            ProponenteNombre = proponenteId == 0 ? null : await NombreDeUsuarioAsync(db, proponenteId, ct),
            FechaCreacion = ahora,
        };
        db.PropuestasDeCambio.Add(fila);
        return fila;
    }

    /// <summary>
    /// Deja los campos de la propuesta EXACTAMENTE como llegaron: crea, actualiza y borra.
    /// </summary>
    private static async Task ReemplazarCamposAsync(
        PnmcDbContext db, PropuestaDeCambioRow propuesta,
        List<(string CampoId, string? Valor)> campos, DateTime ahora, CancellationToken ct)
    {
        var existentes = propuesta.Id == 0
            ? []
            : await db.PropuestasDeCambioCampos.Where(c => c.IdPropuesta == propuesta.Id).ToListAsync(ct);

        var pedidos = campos.ToDictionary(c => c.CampoId, c => c.Valor, StringComparer.Ordinal);

        foreach (var fila in existentes.Where(fila => !pedidos.ContainsKey(fila.CampoId)))
        {
            db.PropuestasDeCambioCampos.Remove(fila);
        }

        foreach (var (campoId, valor) in pedidos)
        {
            var definicion = CamposProponiblesDeMercado.Todos[campoId];
            var fila = existentes.Find(f => string.Equals(f.CampoId, campoId, StringComparison.Ordinal));
            if (fila is null)
            {
                db.PropuestasDeCambioCampos.Add(new PropuestaDeCambioCampoRow
                {
                    Propuesta = propuesta,
                    SeccionId = definicion.SeccionId,
                    CampoId = campoId,
                    CampoEtiqueta = definicion.Etiqueta,
                    ValorPropuesto = valor,
                    FechaCreacion = ahora,
                });
                continue;
            }

            fila.SeccionId = definicion.SeccionId;
            fila.CampoEtiqueta = definicion.Etiqueta;
            fila.ValorPropuesto = valor;
            fila.FechaActualizacion = ahora;
        }
    }

    /// <summary>Descarta lo que no se puede proponer y explica por qué, en vez de ignorarlo en silencio.</summary>
    private static (List<(string CampoId, string? Valor)> Campos, Dictionary<string, string[]> Errores) Normalizar(
        List<CampoPropuestoSolicitud>? entrada)
    {
        var campos = new List<(string CampoId, string? Valor)>();
        var errores = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (entrada is null)
        {
            errores["campos"] = ["La propuesta tiene que traer la lista de campos, aunque esté vacía."];
            return (campos, errores);
        }

        var vistos = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entrada.Count; i++)
        {
            var campoId = (entrada[i].CampoId ?? string.Empty).Trim();
            if (!CamposProponiblesDeMercado.Todos.ContainsKey(campoId))
            {
                errores[$"campos[{i}].campoId"] = ["Ese campo de un mercado no admite propuesta de cambio."];
                continue;
            }

            if (!vistos.Add(campoId))
            {
                errores[$"campos[{i}].campoId"] = ["Ese campo viene dos veces: solo puede proponerse un valor por campo."];
                continue;
            }

            campos.Add((campoId, LimpiarTexto(entrada[i].ValorPropuesto, 4000)));
        }

        return (campos, errores);
    }

    /// <summary>
    /// La propuesta como la lee la pantalla.
    /// </summary>
    /// <remarks>
    /// <b>SIN PROPUESTA SE DEVUELVE UNA CARCASA VACIA, NO UN NULO NI UN 404.</b> Es el mismo
    /// criterio que la revisión por campos: la pantalla se pinta igual en los dos casos, y un 404
    /// la obligaría a distinguir «no hay» de «no existe». Un cuerpo vacío, además, revienta al
    /// deserializar en cuanto alguien lo lee como JSON.
    /// </remarks>
    private static async Task<PropuestaDeCambioDto> ADtoAsync(
        PnmcDbContext db, MercadoRow mercado, PropuestaDeCambioRow? propuesta, CancellationToken ct)
    {
        if (propuesta is null)
        {
            return new PropuestaDeCambioDto(
                0, Modulo, mercado.Id.ToString(CultureInfo.InvariantCulture), mercado.Nombre,
                EstadosDePropuesta.Borrador, null, null, null, null, null, null, null, []);
        }

        var campos = propuesta.Id == 0
            ? []
            : await db.PropuestasDeCambioCampos.AsNoTracking()
                .Where(c => c.IdPropuesta == propuesta.Id)
                .OrderBy(c => c.SeccionId).ThenBy(c => c.Id)
                .ToListAsync(ct);

        return new PropuestaDeCambioDto(
            propuesta.Id, propuesta.ModuloId, propuesta.RegistroId, mercado.Nombre,
            propuesta.Estado, propuesta.Motivo, propuesta.OrganizacionNombre, propuesta.ProponenteNombre,
            propuesta.DecideNombre, propuesta.MotivoDeLaDecision,
            propuesta.FechaEnvio, propuesta.FechaDecision,
            campos.Select(c => new CampoPropuestoDto(
                c.Id, c.SeccionId, c.CampoId, c.CampoEtiqueta, c.ValorAnterior, c.ValorPropuesto)).ToArray());
    }

    private static async Task<Dictionary<string, string>> NombresDeLosMercadosAsync(
        PnmcDbContext db, List<PropuestaDeCambioRow> propuestas, CancellationToken ct)
    {
        var ids = propuestas
            .Select(p => int.TryParse(p.RegistroId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var filas = await db.Mercados.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.Nombre })
            .ToListAsync(ct);

        return filas.ToDictionary(m => m.Id.ToString(CultureInfo.InvariantCulture), m => m.Nombre, StringComparer.Ordinal);
    }

    private static async Task AvisarALaOrganizacionAsync(
        PnmcDbContext db, MercadoRow mercado, string estado, string? motivo, DateTime ahora, CancellationToken ct)
    {
        var (titulo, cuerpo) = estado switch
        {
            EstadosDePropuesta.Aplicada => (
                "Tu propuesta de cambio se aplicó",
                $"El Programa aplicó los cambios que propusiste en el mercado musical «{mercado.Nombre}». Ya se ven en el sitio público."),
            EstadosDePropuesta.Rechazada => (
                "Tu propuesta de cambio no se aplicó",
                $"El Programa no aplicó los cambios que propusiste en «{mercado.Nombre}». Motivo: {motivo}"),
            _ => (
                "Tu propuesta de cambio necesita ajustes",
                $"El Programa pidió ajustes en la propuesta de cambio de «{mercado.Nombre}». {motivo}"),
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
                EventType = estado switch
                {
                    EstadosDePropuesta.Aplicada => "MercadoPropuestaAplicada",
                    EstadosDePropuesta.Rechazada => "MercadoPropuestaRechazada",
                    _ => "MercadoPropuestaConAjustes",
                },
                AccessScope = SimusAuthentication.ExternalScope,
                Channel = "internal",
                Title = titulo,
                Body = cuerpo,
                Status = "enviada",
                ModuloId = Modulo,
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

    private static string? LimpiarTexto(string? valor, int tope) =>
        string.IsNullOrWhiteSpace(valor) ? null : ValidationHelpers.SanitizeText(valor, tope);
}
