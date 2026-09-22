using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Tests;

/// <summary>
/// Da roles a una cuenta de prueba <b>por donde se los da el sistema</b>: la tabla
/// <c>dbo.UsuariosRoles</c>.
/// </summary>
/// <remarks>
/// <para>
/// POR QUÉ HIZO FALTA. Hasta la transicion de la migración de SIMUS, once ficheros de prueba creaban
/// su usuario con <c>RoleId = idWebmaster</c> y eso bastaba, porque el rol vivía en una columna.
/// Desde la transicion el rol vive en la tabla de asignación y <b>esa línea dejó de dar ningún rol</b>:
/// la cuenta se creaba, el login respondía 403 con el motivo <c>sin_rol_valido</c> y la prueba
/// fallaba en un sitio que no tenía nada que ver con lo que medía.
/// </para>
/// <para>
/// ESCRIBE LO MISMO QUE PRODUCCIÓN Y NADA MÁS: la fila de <c>UsuariosRoles</c>. Entre la transicion y
/// la C escribía además <c>Usuarios.IdRol</c>, porque el sistema también lo hacía; esa columna ya
/// no existe. Una prueba que escribiera algo que el sistema no escribe dejaría de medir el sistema.
/// </para>
/// <para>
/// NO ACUMULA: deja a la cuenta EXACTAMENTE con los roles indicados. Es la misma regla que la ruta
/// de asignación, y por la misma razón (defecto U5 del plan de construcción): una prueba que
/// acumulara roles mediría un estado que el sistema no sabe producir.
/// </para>
/// </remarks>
internal static class RolesEnPruebas
{
    /// <summary>Deja a la cuenta con exactamente estos roles, buscados por nombre.</summary>
    public static async Task AsignarAsync(PnmcDbContext db, int idUsuario, params string[] nombresDeRol)
    {
        var normalizados = nombresDeRol.Select(nombre => nombre.Trim().ToLowerInvariant()).ToList();

        var ids = await db.Roles.AsNoTracking()
            .Where(rol => normalizados.Contains(rol.Name.ToLower()))
            .Select(rol => rol.Id)
            .ToListAsync();

        if (ids.Count != normalizados.Count)
        {
            throw new InvalidOperationException(
                $"No existen en dbo.Roles todos los roles pedidos ({string.Join(", ", normalizados)}). "
                + "Una prueba que asigna un rol inexistente no mide nada: la cuenta se queda sin rol "
                + "y el login la rechaza por un motivo que no es el que la prueba investiga.");
        }

        await AsignarPorIdAsync(db, idUsuario, ids);
    }

    /// <summary>Deja a la cuenta con exactamente estos roles, ya resueltos a identificador.</summary>
    public static async Task AsignarPorIdAsync(PnmcDbContext db, int idUsuario, IReadOnlyCollection<int> idsDeRol)
    {
        var actuales = await db.UsuariosRoles
            .Where(asignacion => asignacion.UserId == idUsuario)
            .ToListAsync();

        foreach (var sobrante in actuales.Where(asignacion => !idsDeRol.Contains(asignacion.RoleId)))
        {
            db.UsuariosRoles.Remove(sobrante);
        }

        foreach (var id in idsDeRol.Where(id => !actuales.Any(asignacion => asignacion.RoleId == id)))
        {
            db.UsuariosRoles.Add(new UsuarioRolRow
            {
                UserId = idUsuario,
                RoleId = id,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Los roles que la base dice que tiene esta cuenta, normalizados y ordenados.</summary>
    public static async Task<IReadOnlyList<string>> LeerAsync(PnmcDbContext db, int idUsuario)
    {
        var nombres = await db.UsuariosRoles.AsNoTracking()
            .Where(asignacion => asignacion.UserId == idUsuario)
            .Join(db.Roles.AsNoTracking(), asignacion => asignacion.RoleId, rol => rol.Id, (_, rol) => rol.Name)
            .ToListAsync();

        return nombres.Select(nombre => nombre.Trim().ToLowerInvariant()).OrderBy(nombre => nombre, StringComparer.Ordinal).ToList();
    }
}
