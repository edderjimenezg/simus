using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PNMC.Api.ImportacionAsistida;
using PNMC.Api.Security;
using PNMC.Contracts;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Previsualización, decisión y aplicación gobernada de archivos, para cualquier dominio.
/// </summary>
/// <remarks>
/// <para>
/// <b>UNA SOLA RUTA PARA TODOS.</b> Hasta esto era
/// <c>ImportacionFestivalesEndpoints</c>: quinientas líneas que sabían de Festivales. Importar otra
/// cosa habría exigido otro endpoint, otro contrato y otra pantalla, es decir tres copias de la
/// misma transacción, la misma idempotencia y la misma retención, divergiendo desde el primer día.
/// </para>
/// <para>
/// <b>EL DOMINIO SE RESUELVE DE LA RUTA.</b> <c>/admin/importaciones/festivales/…</c> sigue
/// funcionando igual que antes porque el identificador del dominio ocupa exactamente el lugar donde
/// estaba escrito. Un dominio que no existe responde 404, no una lista vacía.
/// </para>
/// <para>
/// <b>NINGUN PASO ESCRIBE EN EL DESTINO ANTES DE CONFIRMAR.</b> Ver
/// <see cref="ReglasDeImportacion"/> para las cuatro reglas de la capacidad.
/// </para>
/// </remarks>
public static class ImportacionAsistidaEndpoints
{
    public static RouteGroupBuilder MapImportacionAsistidaEndpoints(this RouteGroupBuilder group)
    {
        var importaciones = group.MapGroup("/admin/importaciones")
            .WithTags("importaciones")
            .RequireAuthorization(Permisos.PoliticaFuncionario);

        // QUE SE PUEDE IMPORTAR, preguntado al servidor. La consola llevaba la lista escrita, y una
        // lista en el navegador acaba ofreciendo un destino que el servidor no sabe recibir: es lo
        // que pasó cuando esta pantalla anunciaba ocho módulos y solo uno tenía circuito.
        importaciones.MapGet("/", ListarDominios)
            .WithName("ListarDominiosImportables")
            .Produces<DominiosImportablesDto>(StatusCodes.Status200OK);

        var dominio = importaciones.MapGroup("/{dominio}");

        dominio.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext contexto) =>
        {
            var testigos = antiforgery.GetAndStoreTokens(contexto);
            return Results.Ok(new { token = testigos.RequestToken ?? string.Empty });
        });

        dominio.MapGet("/", ListarAsync)
            .Produces<PaginaDeImportacionesDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        dominio.MapGet("/{id:long}", ConsultarAsync)
            .Produces<ImportacionDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        dominio.MapPost("/previsualizar", PrevisualizarAsync)
            .RequireRateLimiting("importaciones-festivales")
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(5 * 1024 * 1024))
            .Produces<ImportacionDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        dominio.MapPost("/{id:long}/confirmar", ConfirmarAsync)
            .RequireRateLimiting("importaciones-festivales")
            .Produces<ImportacionDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        return group;
    }

    private static IResult ListarDominios(IEnumerable<IDominioDeImportacion> dominios) =>
        Results.Ok(new DominiosImportablesDto(dominios
            .Select(item => item.Capacidad)
            .OrderBy(item => item.Etiqueta, StringComparer.CurrentCulture)
            .Select(item => new DominioImportableDto(
                item.ModuloId, item.Etiqueta, item.EstadoAlImportar, item.PorQueEseEstado, item.Campos))
            .ToList()));

    /// <summary>El dominio que pide la ruta, o <c>null</c> si nadie sabe importar eso.</summary>
    private static IDominioDeImportacion? Resolver(IEnumerable<IDominioDeImportacion> dominios, string nombre) =>
        dominios.FirstOrDefault(item => string.Equals(item.Capacidad.ModuloId, nombre, StringComparison.OrdinalIgnoreCase));

    private static async Task<IResult> ListarAsync(
        string dominio,
        string? estado,
        string? archivo,
        int? pagina,
        int? tamano,
        ClaimsPrincipal principal,
        IEnumerable<IDominioDeImportacion> dominios,
        PnmcDbContext db,
        IOptions<RetencionImportacionesOptions> opciones,
        CancellationToken ct)
    {
        if (Resolver(dominios, dominio) is null) return Results.NotFound();

        var estadosAdmitidos = new[]
        {
            ReglasDeImportacion.Previsualizado, ReglasDeImportacion.Aplicado, ReglasDeImportacion.Expirado,
        };
        var estadoPedido = estado?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(estadoPedido) && !estadosAdmitidos.Contains(estadoPedido, StringComparer.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["estado"] = ["El estado debe ser previsualizado, aplicado o expirado."],
            });
        }

        var paginaPedida = Math.Max(1, pagina ?? 1);
        var tamanoPedido = Math.Clamp(tamano ?? 10, 1, 50);
        var archivoPedido = archivo?.Trim() ?? string.Empty;
        var consulta = db.LotesImportacion.AsNoTracking().Where(item => item.ModuloId == dominio);
        if (!string.IsNullOrEmpty(estadoPedido)) consulta = consulta.Where(item => item.Estado == estadoPedido);
        if (archivoPedido.Length > 0) consulta = consulta.Where(item => item.NombreArchivo.Contains(archivoPedido));

        var total = await consulta.CountAsync(ct);
        var lotes = await consulta
            .OrderByDescending(item => item.FechaPrevisualizacion)
            .ThenByDescending(item => item.Id)
            .Skip((paginaPedida - 1) * tamanoPedido)
            .Take(tamanoPedido)
            .ToListAsync(ct);

        var usuarioId = Usuario(principal) ?? -1;
        var dias = opciones.Value.DiasPrevisualizacion;
        return Results.Ok(new PaginaDeImportacionesDto(
            lotes.Select(lote => NucleoDeImportacion.Resumen(lote, usuarioId, dias)).ToList(),
            paginaPedida,
            tamanoPedido,
            total,
            (int)Math.Ceiling(total / (double)tamanoPedido),
            dias,
            NucleoDeImportacion.PoliticaRetencion(dias)));
    }

    private static async Task<IResult> ConsultarAsync(
        string dominio,
        long id,
        ClaimsPrincipal principal,
        IEnumerable<IDominioDeImportacion> dominios,
        PnmcDbContext db,
        IOptions<RetencionImportacionesOptions> opciones,
        CancellationToken ct)
    {
        if (Resolver(dominios, dominio) is not { } elegido) return Results.NotFound();

        var lote = await db.LotesImportacion.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.ModuloId == dominio, ct);
        return lote is null
            ? Results.NotFound()
            : Results.Ok(await NucleoDeImportacion.ConstruirDtoAsync(
                lote, elegido, db, Usuario(principal) ?? -1, opciones.Value.DiasPrevisualizacion, ct));
    }

    /// <summary>
    /// Guarda el plan y no escribe ni un registro en el destino.
    /// </summary>
    /// <remarks>
    /// ES LA PRIMERA REGLA DE LA CAPACIDAD. Se guarda el lote con sus filas y hallazgos para poder
    /// decidir después sobre exactamente lo mismo que se vio; nada del dominio se toca.
    /// </remarks>
    private static async Task<IResult> PrevisualizarAsync(
        string dominio,
        PrevisualizarImportacionSolicitud solicitud,
        ClaimsPrincipal principal,
        IEnumerable<IDominioDeImportacion> dominios,
        PnmcDbContext db,
        IAntiforgery antiforgery,
        IOptions<RetencionImportacionesOptions> opciones,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (Resolver(dominios, dominio) is not { } elegido) return Results.NotFound();

        var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, contexto);
        if (csrf is not null) return csrf;
        var usuarioId = Usuario(principal);
        if (usuarioId is null) return Results.Unauthorized();

        var errores = NucleoDeImportacion.ValidarSobre(solicitud);
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        var filasFuente = NucleoDeImportacion.SoloCamposDeclarados(solicitud.Filas!, elegido.Capacidad);
        var plan = await elegido.PlanearAsync(filasFuente, solicitud.HuellaArchivo!.Trim().ToLowerInvariant(), db, ct);

        var lote = new LoteImportacionRow
        {
            ModuloId = elegido.Capacidad.ModuloId,
            NombreArchivo = Path.GetFileName(solicitud.NombreArchivo!.Trim()),
            Formato = solicitud.Formato!.Trim().ToLowerInvariant(),
            HuellaArchivo = solicitud.HuellaArchivo!.Trim().ToLowerInvariant(),
            HuellaPlan = plan.HuellaPlan,
            VersionContrato = solicitud.VersionContrato,
            Estado = ReglasDeImportacion.Previsualizado,
            UsuarioId = usuarioId.Value,
            FechaPrevisualizacion = DateTime.UtcNow,
            TotalFilas = plan.Filas.Count,
            FilasImportables = plan.Importables,
            FilasRechazadas = plan.Rechazadas,
        };

        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            db.LotesImportacion.Add(lote);
            await db.SaveChangesAsync(ct);

            foreach (var planeada in plan.Filas)
            {
                var fila = new FilaImportacionRow
                {
                    LoteImportacionId = lote.Id,
                    NumeroFila = planeada.NumeroFila,
                    ContenidoNormalizadoJson = planeada.ContenidoNormalizadoJson,
                    Resultado = planeada.Resultado,
                    PuedeImportarse = planeada.PuedeImportarse,
                    RegistroCoincidenteId = planeada.RegistroCoincidenteId,
                };
                db.FilasImportacion.Add(fila);
                await db.SaveChangesAsync(ct);
                db.HallazgosImportacion.AddRange(planeada.Hallazgos.Select(hallazgo => new HallazgoImportacionRow
                {
                    FilaImportacionId = fila.Id,
                    Severidad = hallazgo.Severidad,
                    Codigo = hallazgo.Codigo,
                    Campo = hallazgo.Campo,
                    Mensaje = hallazgo.Mensaje,
                }));
            }

            db.AuditLogs.Add(new AuditLogRow
            {
                UserId = usuarioId.Value,
                TableName = "LotesImportacion",
                RecordId = lote.Id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.Crear,
                NewValuesJson = JsonSerializer.Serialize(new
                {
                    evento = "ImportacionPrevisualizada",
                    dominio = lote.ModuloId,
                    lote.HuellaArchivo,
                    lote.TotalFilas,
                    lote.FilasImportables,
                }),
                CreatedAt = lote.FechaPrevisualizacion,
            });
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        return Results.Ok(await NucleoDeImportacion.ConstruirDtoAsync(
            lote, elegido, db, usuarioId.Value, opciones.Value.DiasPrevisualizacion, ct));
    }

    /// <summary>
    /// Escribe lo que se decidió escribir, una sola vez.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>LA RESERVA CONDICIONAL ES LO QUE IMPIDE DOS CONFIRMACIONES SIMULTANEAS.</b> Vive dentro de
    /// la transacción, así que cualquier fallo revierte también la marca: un lote no se queda
    /// «aplicando» para siempre porque alguien cerrara el portátil.
    /// </para>
    /// <para>
    /// <b>SE PREGUNTA AL DOMINIO QUE CAMBIO DESDE LA PREVISUALIZACION.</b> El caso real es que
    /// alguien cree a mano, entremedias, un registro que el archivo también trae. El núcleo no sabe
    /// reconocerlo; el dominio sí, y si lo reconoce no se escribe nada.
    /// </para>
    /// </remarks>
    private static async Task<IResult> ConfirmarAsync(
        string dominio,
        long id,
        ConfirmarImportacionSolicitud solicitud,
        ClaimsPrincipal principal,
        IEnumerable<IDominioDeImportacion> dominios,
        PnmcDbContext db,
        IAntiforgery antiforgery,
        IOptions<RetencionImportacionesOptions> opciones,
        HttpContext contexto,
        CancellationToken ct)
    {
        if (Resolver(dominios, dominio) is not { } elegido) return Results.NotFound();

        var csrf = await GuardasDelCms.ValidateAntiforgeryAsync(antiforgery, contexto);
        if (csrf is not null) return csrf;
        var usuarioId = Usuario(principal);
        if (usuarioId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(solicitud.HuellaPlan) || !NucleoDeImportacion.EsHuella(solicitud.HuellaPlan))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["huellaPlan"] = ["Previsualiza el archivo y confirma con la huella de 64 caracteres recibida."],
            });
        }
        if (string.IsNullOrWhiteSpace(solicitud.ClaveIdempotencia) || solicitud.ClaveIdempotencia.Trim().Length > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["claveIdempotencia"] = ["La confirmación requiere una clave de idempotencia de hasta 100 caracteres."],
            });
        }

        var lote = await db.LotesImportacion.FirstOrDefaultAsync(item => item.Id == id && item.ModuloId == dominio, ct);
        if (lote is null) return Results.NotFound();
        if (lote.UsuarioId != usuarioId.Value) return Results.Forbid();

        var clave = solicitud.ClaveIdempotencia.Trim();
        var dias = opciones.Value.DiasPrevisualizacion;

        if (lote.Estado == ReglasDeImportacion.Aplicado)
        {
            // REPETIR LA MISMA CONFIRMACION NO ES UN ERROR: una red inestable reenvía. Con otra
            // clave sí lo es, porque sería una segunda decisión sobre un lote ya decidido.
            return string.Equals(lote.ClaveIdempotencia, clave, StringComparison.Ordinal)
                ? Results.Ok(await NucleoDeImportacion.ConstruirDtoAsync(lote, elegido, db, usuarioId.Value, dias, ct))
                : Results.Conflict(new { message = "Este lote ya se aplicó con otra clave de confirmación." });
        }
        if (lote.Estado != ReglasDeImportacion.Previsualizado)
            return Results.Conflict(new { message = "El lote no está en un estado que admita confirmación." });
        if (!string.Equals(lote.HuellaPlan, solicitud.HuellaPlan.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            return Results.Conflict(new
            {
                message = "La huella del plan no coincide con la previsualización guardada. Vuelve a previsualizar.",
            });
        }

        var organizacionInstitucionalId = await ProcedenciaDeRegistro.IdInstitucionalAsync(db, ct);
        if (organizacionInstitucionalId is null)
        {
            return Results.Problem(
                title: "Configuración incompleta",
                detail: "No existe una organización institucional activa que responda por los registros importados.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (await db.LotesImportacion.AsNoTracking().AnyAsync(item => item.ClaveIdempotencia == clave && item.Id != lote.Id, ct))
            return Results.Conflict(new { message = "La clave de confirmación ya fue usada por otro lote." });

        var idsExcluidos = (solicitud.IdsFilasExcluidas ?? []).ToHashSet();
        string? conflicto = null;
        var repeticionIdempotente = false;

        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            conflicto = null;
            repeticionIdempotente = false;
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var reservado = await db.LotesImportacion
                .Where(item => item.Id == lote.Id
                    && item.Estado == ReglasDeImportacion.Previsualizado
                    && item.UsuarioId == usuarioId.Value
                    && item.HuellaPlan == lote.HuellaPlan)
                .ExecuteUpdateAsync(actualizacion => actualizacion
                    .SetProperty(item => item.Estado, ReglasDeImportacion.Aplicando), ct);
            if (reservado == 0)
            {
                await db.Entry(lote).ReloadAsync(ct);
                repeticionIdempotente = lote.Estado == ReglasDeImportacion.Aplicado
                    && string.Equals(lote.ClaveIdempotencia, clave, StringComparison.Ordinal);
                if (!repeticionIdempotente)
                    conflicto = "El lote cambió mientras se confirmaba. Vuelve a previsualizar antes de intentar otra operación.";
                return;
            }
            lote.Estado = ReglasDeImportacion.Aplicando;

            if (await db.LotesImportacion.AsNoTracking().AnyAsync(item => item.ClaveIdempotencia == clave && item.Id != lote.Id, ct))
            {
                conflicto = "La clave de confirmación ya fue usada por otro lote.";
                return;
            }

            var filas = await db.FilasImportacion
                .Where(item => item.LoteImportacionId == lote.Id)
                .OrderBy(item => item.NumeroFila)
                .ToListAsync(ct);

            conflicto = await elegido.QueCambioDesdeLaPrevisualizacionAsync(
                filas.Where(fila => fila.PuedeImportarse && !idsExcluidos.Contains(fila.Id)).ToList(), db, ct);
            if (conflicto is not null) return;

            var ahora = DateTime.UtcNow;
            var aplicacion = new ContextoDeAplicacion(organizacionInstitucionalId.Value, usuarioId.Value, ahora);
            var elegidas = 0;

            foreach (var fila in filas)
            {
                var decision = fila.PuedeImportarse
                    ? idsExcluidos.Contains(fila.Id) ? "excluir" : "importar"
                    : "rechazar_validacion";
                db.DecisionesImportacion.Add(new DecisionImportacionRow
                {
                    LoteImportacionId = lote.Id,
                    FilaImportacionId = fila.Id,
                    UsuarioId = usuarioId.Value,
                    Decision = decision,
                    Fecha = ahora,
                });

                if (decision != "importar") continue;

                var registroId = await elegido.AplicarAsync(fila, aplicacion, db, ct);
                fila.RegistroCreadoId = registroId;
                elegidas++;

                // DE DONDE VIENE ESTE REGISTRO: de un archivo, no de alguien escribiéndolo.
                // `importacion` no es lo mismo que `administrativo` aunque lo ejecute un
                // funcionario: distinguirlo es lo que permite responder después «esto lo escribió
                // alguien» frente a «esto llegó en una carga». Es la tercera regla de la capacidad.
                await ProcedenciaDeRegistro.AnotarAsync(
                    db,
                    elegido.Capacidad.TipoDeRegistro,
                    registroId.ToString(CultureInfo.InvariantCulture),
                    ProcedenciaDeRegistro.Importacion,
                    organizacionInstitucionalId.Value,
                    usuarioId.Value,
                    ct);

                // LA BITACORA APUNTA AL REGISTRO CREADO, no al lote: el historial de un Festival se
                // lee en su propia ficha, y anotarlo solo contra el lote lo dejaría sin rastro de
                // cómo apareció justo para quien lo esté mirando.
                db.AuditLogs.Add(new AuditLogRow
                {
                    UserId = usuarioId.Value,
                    TableName = elegido.Capacidad.TablaDeAuditoria,
                    RecordId = registroId.ToString(CultureInfo.InvariantCulture),
                    Action = AccionesAuditoria.Crear,
                    NewValuesJson = JsonSerializer.Serialize(new
                    {
                        evento = "RegistroCreadoPorImportacion",
                        dominio = elegido.Capacidad.ModuloId,
                        loteId = lote.Id,
                        filaId = fila.Id,
                    }),
                    CreatedAt = ahora,
                });
            }

            // EL REGISTRO CREADO CONSERVA SU CONTACTO CANONICO. La traza no necesita otra copia:
            // mantiene lo que se importó y el vínculo, pero retira correo y teléfono.
            foreach (var fila in filas)
            {
                fila.ContenidoNormalizadoJson = DepuradorImportaciones
                    .MinimizarContenidoNormalizado(fila.ContenidoNormalizadoJson, fila.Id);
            }

            lote.Estado = ReglasDeImportacion.Aplicado;
            lote.ClaveIdempotencia = clave;
            lote.FechaAplicacion = ahora;
            lote.FechaDepuracion = ahora;
            lote.FilasAplicadas = elegidas;
            lote.FilasExcluidas = idsExcluidos.Count;
            db.AuditLogs.Add(new AuditLogRow
            {
                UserId = usuarioId.Value,
                TableName = "LotesImportacion",
                RecordId = lote.Id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.Actualizar,
                NewValuesJson = JsonSerializer.Serialize(new
                {
                    evento = "ImportacionAplicada",
                    dominio = lote.ModuloId,
                    lote.FilasAplicadas,
                    lote.FilasExcluidas,
                }),
                CreatedAt = ahora,
            });
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        });

        if (repeticionIdempotente)
            return Results.Ok(await NucleoDeImportacion.ConstruirDtoAsync(lote, elegido, db, usuarioId.Value, dias, ct));
        if (conflicto is not null)
            return Results.Conflict(new { message = conflicto });

        return Results.Ok(await NucleoDeImportacion.ConstruirDtoAsync(lote, elegido, db, usuarioId.Value, dias, ct));
    }

    private static int? Usuario(ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
