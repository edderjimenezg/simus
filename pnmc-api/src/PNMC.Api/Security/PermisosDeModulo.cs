using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Security;

/// <summary>
/// Qué módulos de la consola tiene activados una cuenta, y quién puede entrar a cada uno.
/// </summary>
/// <remarks>
/// <para>
/// <b>SIN ESTO, RECORTAR EL MENU ES DECORACION.</b> Ocultar «Catálogo Editorial» en la barra
/// izquierda no impide nada: la ruta sigue respondiendo a quien la escriba a mano o a quien tenga el
/// enlace guardado. El permiso solo existe de verdad cuando el servidor lo comprueba, y por eso esta
/// pieza vive en <c>Security</c> y no en el panel que la dibuja.
/// </para>
/// <para>
/// <b>EL WEBMASTER LOS TIENE TODOS, SIEMPRE.</b> No es comodidad: si el permiso de entrar a
/// «Usuarios y roles» se pudiera quitar a todo el mundo, la última cuenta que lo tuviera podría
/// dejar al Programa sin forma de conceder permisos a nadie, y eso no se arregla desde la interfaz.
/// Es la misma razón por la que existe el candado del último webmaster.
/// </para>
/// <para>
/// <b>DOS MODULOS NO SE CONSULTAN NUNCA</b> —«monitor» y «solicitudes»—: los tiene toda cuenta de
/// consola por decisión de producto. Se responden sin tocar la base, que además es el caso más
/// frecuente: son las dos pantallas por las que se entra.
/// </para>
/// </remarks>
public static class PermisosDeModulo
{
    /// <summary>El identificador de la cuenta que hay en la sesión, si lo hay.</summary>
    public static int? CuentaDe(ClaimsPrincipal principal) =>
        int.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var cuenta)
            ? cuenta
            : null;

    /// <summary>Si esa sesión puede entrar a ese módulo.</summary>
    public static async Task<bool> PuedeAsync(
        PnmcDbContext db, ClaimsPrincipal principal, string modulo, CancellationToken ct)
    {
        if (!ModulosDeLaConsola.Existe(modulo)) return false;
        if (ModulosDeLaConsola.EsSiempreActivado(modulo)) return true;
        if (principal.IsInRole(Permisos.Webmaster)) return true;

        var cuenta = CuentaDe(principal);
        if (cuenta is null) return false;

        return await db.ModulosPorCuenta.AsNoTracking()
            .AnyAsync(x => x.IdUsuario == cuenta.Value && x.CodigoModulo == modulo, ct);
    }

    /// <summary>
    /// Todos los módulos que esa cuenta tiene activados, incluidos los que vienen siempre.
    /// </summary>
    /// <remarks>
    /// <b>SE DEVUELVE EN EL ORDEN DE LA BARRA</b> y no en el que los guardó la base: la pantalla que
    /// los enseña los lista así, y una lista ordenada distinto de la barra que describe obliga a
    /// buscar cada módulo dos veces.
    /// </remarks>
    public static async Task<IReadOnlyList<string>> DeLaCuentaAsync(
        PnmcDbContext db, int cuenta, bool esWebmaster, CancellationToken ct)
    {
        if (esWebmaster) return ModulosDeLaConsola.Todos;

        var concedidos = await db.ModulosPorCuenta.AsNoTracking()
            .Where(x => x.IdUsuario == cuenta)
            .Select(x => x.CodigoModulo)
            .ToListAsync(ct);

        var tiene = new HashSet<string>(concedidos, StringComparer.OrdinalIgnoreCase);
        foreach (var siempre in ModulosDeLaConsola.SiempreActivados) tiene.Add(siempre);

        return ModulosDeLaConsola.Todos.Where(tiene.Contains).ToList();
    }
}
