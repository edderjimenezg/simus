using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PNMC.Api.Security;
using PNMC.Domain.Entities;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Endpoints;

/// <summary>
/// Qué módulos de la consola tiene activados cada cuenta.
/// </summary>
/// <remarks>
/// <para>
/// <b>TRES RUTAS Y CADA UNA RESPONDE A OTRA PREGUNTA.</b> «Qué módulos existen» lo necesita la
/// pantalla que los concede; «qué tiene esta cuenta» también; y «qué tengo yo» lo necesita CUALQUIER
/// cuenta al abrir la consola, para dibujar su barra izquierda. Por eso la tercera no exige el
/// módulo «usuarios»: si lo exigiera, solo quien administra usuarios podría ver su propio menú.
/// </para>
/// <para>
/// <b>SE GUARDA LA LISTA ENTERA Y NO UN «AÑADE» Y UN «QUITA».</b> La pantalla es una lista de
/// casillas: lo que la persona decide es el conjunto, no una diferencia. Con dos operaciones habría
/// que reconstruir aquí qué cambió, y dos pestañas abiertas podrían dejar un estado que nadie
/// eligió. Con el conjunto, la última en guardar gana y lo que queda es exactamente lo que se vio.
/// </para>
/// </remarks>
public static class PermisosDeModuloEndpoints
{
    public static RouteGroupBuilder MapPermisosDeModuloEndpoints(this RouteGroupBuilder group)
    {
        // MIS MODULOS: sin exigir «usuarios», porque toda cuenta necesita saber qué puede abrir.
        group.MapGet("/admin/mis-modulos", async (
            ClaimsPrincipal principal, PnmcDbContext db, CancellationToken ct) =>
        {
            var cuenta = PermisosDeModulo.CuentaDe(principal);
            if (cuenta is null) return Results.Unauthorized();

            var esWebmaster = principal.IsInRole(Permisos.Webmaster);
            var mios = await PermisosDeModulo.DeLaCuentaAsync(db, cuenta.Value, esWebmaster, ct);

            return Results.Ok(new
            {
                modulos = mios,
                siempreActivados = ModulosDeLaConsola.SiempreActivados,
                // SE DICE SI LOS TIENE TODOS POR SER WEBMASTER y no por concesión: la pantalla que
                // administra permisos necesita explicar por qué sus casillas están todas puestas.
                todosPorSerWebmaster = esWebmaster,
            });
        })
        .WithName("MisModulosDeConsola")
        .RequireAuthorization(Permisos.PoliticaFuncionario);

        var administracion = group.MapGroup("/admin/usuarios")
            .WithTags("admin-modulos-por-cuenta")
            .RequireAuthorization(Permisos.PoliticaFuncionario)
            .ExigeModulo("usuarios");

        // EL CATALOGO: qué módulos existen y cuáles no se pueden quitar.
        administracion.MapGet("/modulos-disponibles", () => Results.Ok(new
        {
            modulos = ModulosDeLaConsola.Todos,
            siempreActivados = ModulosDeLaConsola.SiempreActivados,
        }))
        .WithName("ModulosDisponiblesDeConsola");

        // EL VOCABULARIO DE TIPOS DE DOCUMENTO, LEIDO DE LA TABLA. Es el mismo catálogo del país
        // que usa el alta externa, y aquí se lee de `dbo.TiposDocumento` en vez de llevar una copia:
        // el número de identificación de una cuenta administrativa se captura con una lista, no con
        // texto libre, igual que cualquier otro vocabulario controlado del proyecto.
        administracion.MapGet("/catalogos/tipos-documento", async (PnmcDbContext db, CancellationToken ct) =>
            Results.Ok(await db.TiposDocumento.AsNoTracking()
                .Where(t => t.Activo)
                .OrderBy(t => t.OrdenVisualizacion)
                .Select(t => new { codigo = t.Codigo, etiqueta = t.Nombre })
                .ToListAsync(ct)))
        .WithName("TiposDeDocumentoParaLaConsola");

        administracion.MapGet("/{id:int}/modulos", async (
            int id, PnmcDbContext db, CancellationToken ct) =>
        {
            var cuenta = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (cuenta is null) return Results.NotFound();

            var esWebmaster = await EsWebmasterAsync(db, id, ct);
            var suyos = await PermisosDeModulo.DeLaCuentaAsync(db, id, esWebmaster, ct);

            return Results.Ok(new
            {
                idUsuario = id,
                modulos = suyos,
                siempreActivados = ModulosDeLaConsola.SiempreActivados,
                todosPorSerWebmaster = esWebmaster,
            });
        })
        .WithName("ModulosDeUnaCuenta");

        administracion.MapPut("/{id:int}/modulos", async (
            int id, ModulosDeUnaCuentaSolicitud solicitud, ClaimsPrincipal principal,
            PnmcDbContext db, CancellationToken ct) =>
        {
            var cuenta = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (cuenta is null) return Results.NotFound();

            // UN MODULO QUE NO EXISTE NO SE GUARDA EN SILENCIO: se dice cuál, porque casi siempre es
            // un identificador mal escrito y adivinarlo desde una lista vacía cuesta una tarde.
            var pedidos = (solicitud.Modulos ?? []).Select(m => (m ?? string.Empty).Trim()).ToList();
            var desconocidos = pedidos.Where(m => !ModulosDeLaConsola.Existe(m)).ToList();
            if (desconocidos.Count > 0)
            {
                return Results.BadRequest(new
                {
                    modulos = new[]
                    {
                        $"Estos no son módulos de la consola: {string.Join(", ", desconocidos)}.",
                    },
                });
            }

            // LOS DE SIEMPRE NO SE GUARDAN, aunque vengan en la lista: guardarlos sería poder
            // borrarlos. Se descartan aquí y el catálogo del servidor los sigue concediendo.
            var aGuardar = pedidos
                .Where(m => !ModulosDeLaConsola.EsSiempreActivado(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var actuales = await db.ModulosPorCuenta.Where(x => x.IdUsuario == id).ToListAsync(ct);
            var antes = actuales.Select(x => x.CodigoModulo).OrderBy(x => x, StringComparer.Ordinal).ToList();

            db.ModulosPorCuenta.RemoveRange(actuales.Where(x => !aGuardar.Contains(x.CodigoModulo, StringComparer.OrdinalIgnoreCase)));

            var ahora = DateTime.UtcNow;
            var quienOtorga = PermisosDeModulo.CuentaDe(principal);
            foreach (var modulo in aGuardar.Where(m => !actuales.Exists(x => string.Equals(x.CodigoModulo, m, StringComparison.OrdinalIgnoreCase))))
            {
                db.ModulosPorCuenta.Add(new ModuloPorCuentaRow
                {
                    IdUsuario = id,
                    CodigoModulo = modulo,
                    FechaOtorgado = ahora,
                    IdUsuarioQueOtorgo = quienOtorga,
                });
            }

            // QUEDA EN LA BITACORA. «Por qué esta persona puede publicar» es una pregunta que se hace
            // seis meses después, y sin rastro no tiene respuesta.
            db.AuditLogs.Add(new AuditLogRow
            {
                UserId = quienOtorga ?? 0,
                TableName = "ModulosPorCuenta",
                RecordId = id.ToString(CultureInfo.InvariantCulture),
                Action = AccionesAuditoria.Actualizar,
                PreviousValuesJson = JsonSerializer.Serialize(new { modulos = antes }),
                NewValuesJson = JsonSerializer.Serialize(new { modulos = aGuardar.OrderBy(x => x, StringComparer.Ordinal).ToList() }),
                CreatedAt = ahora,
            });

            await db.SaveChangesAsync(ct);

            var esWebmaster = await EsWebmasterAsync(db, id, ct);
            return Results.Ok(new
            {
                idUsuario = id,
                modulos = await PermisosDeModulo.DeLaCuentaAsync(db, id, esWebmaster, ct),
                siempreActivados = ModulosDeLaConsola.SiempreActivados,
                todosPorSerWebmaster = esWebmaster,
            });
        })
        .WithName("GuardarModulosDeUnaCuenta");

        return group;
    }

    private static async Task<bool> EsWebmasterAsync(PnmcDbContext db, int idUsuario, CancellationToken ct) =>
        await db.UsuariosRoles.AsNoTracking()
            .Where(ur => ur.UserId == idUsuario)
            .Join(db.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .AnyAsync(nombre => nombre == Permisos.Webmaster, ct);
}

/// <summary>El conjunto de módulos que queda activado para esa cuenta.</summary>
public sealed class ModulosDeUnaCuentaSolicitud
{
    public string?[]? Modulos { get; set; }
}
