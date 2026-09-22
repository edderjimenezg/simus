using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Security;

/// <summary>
/// Punto unico donde se pregunta y se escribe <b>que roles tiene una persona</b>.
/// </summary>
/// <remarks>
/// <para>
/// EL MODELO DE ROLES, en una frase: el rol dejo de ser una columna escalar
/// (<c>Usuarios.IdRol</c>, que solo admite uno) y paso a ser una tabla de asignacion
/// (<c>dbo.UsuariosRoles</c>, que admite varios). Este fichero es el unico sitio del API que
/// sabe eso; el resto pregunta aqui.
/// </para>
/// <para>
/// POR QUE UN SOLO SITIO, Y NO UN <c>Join</c> EN CADA RUTA. Antes de este cambio habia siete
/// <c>Join(db.Roles, u =&gt; u.RoleId, ...)</c> repartidos por cuatro ficheros. Cada uno era una
/// copia de la misma regla, y una regla copiada es como se llega a que una copia se quede atras
/// sin que nadie lo note — que es exactamente el argumento con el que existe
/// <see cref="Permisos"/>. Con N:M el riesgo sube, porque ahora la consulta ademas puede
/// devolver mas de una fila y quien no lo espere se quedara con la primera.
/// </para>
/// <para>
/// SIN SEGUNDA FUENTE. Durante la transicion,
/// <see cref="AsignarAsync"/> escribia ademas <c>Usuarios.IdRol</c> —una escritura doble que
/// nadie leia— para poder revertir el despliegue sin tocar la base. Esa columna ya no existe: la
/// retiro la seccion 11 de <c>schema/V20260824_01__usuarios_roles_y_permisos.sql</c>, encendida
/// en el mismo cambio que borro la escritura doble. El patron era expandir / migrar / contraer y
/// esta contraido.
/// </para>
/// <para>
/// SIN VUELTA ATRAS SILENCIOSA. Si una cuenta no tiene ninguna fila en <c>UsuariosRoles</c>,
/// <see cref="ObtenerAsync"/> devuelve la lista <b>vacia</b> y no consulta <c>IdRol</c> como
/// respaldo. Es deliberado: un respaldo silencioso haria que retirar la columna
/// pasara de «todo el mundo pierde el rol de golpe, y se ve» a «unos cuantos lo pierden cuando
/// les toque, y no se ve». El plan de construccion lo pide asi en su comprobacion 7. Quien no
/// tenga rol no inicia sesion, y el registro lo dice con el motivo <c>sin_rol_valido</c>.
/// </para>
/// </remarks>
public static class RolesDeUsuario
{
    /// <summary>
    /// Orden de precedencia para elegir el rol principal. El primero que aparezca, manda.
    /// </summary>
    /// <remarks>
    /// Hace falta porque el mundo de fuera —la cookie de una sola etiqueta, el DTO de la consola,
    /// la columna <c>IdRol</c> mientras siga existiendo— todavia pide UN rol. Elegirlo por orden
    /// declarado y no «el primero que devuelva la consulta» es la diferencia entre una respuesta
    /// estable y una que cambia con el plan de ejecucion; ver el defecto de
    /// <c>AdminDataEndpoints</c>, donde eso mismo decidia que transiciones de estado se permitian.
    /// </remarks>
    private static readonly string[] Precedencia = [Permisos.Webmaster, Permisos.GestorInterno, Permisos.Externo];

    /// <summary>
    /// Los roles de esta persona, normalizados, sin repetidos y en orden estable.
    /// </summary>
    /// <remarks>
    /// Devuelve vacio si la cuenta no tiene ninguna asignacion. Ver la nota de la clase sobre por
    /// que no hay respaldo a <c>Usuarios.IdRol</c>.
    /// </remarks>
    public static async Task<IReadOnlyList<string>> ObtenerAsync(
        PnmcDbContext db,
        int idUsuario,
        CancellationToken cancelacion = default)
    {
        var nombres = await db.UsuariosRoles.AsNoTracking()
            .Where(asignacion => asignacion.UserId == idUsuario)
            .Join(db.Roles.AsNoTracking(), asignacion => asignacion.RoleId, rol => rol.Id, (_, rol) => rol.Name)
            .ToListAsync(cancelacion);

        return Ordenar(nombres);
    }

    /// <summary>
    /// Lo mismo que <see cref="ObtenerAsync"/> pero para varias personas de una vez.
    /// </summary>
    /// <remarks>
    /// EXISTE PARA QUE EL LISTADO DE USUARIOS NO HAGA UNA CONSULTA POR FILA. Con la columna
    /// escalar, la lista de la consola se resolvia con un <c>Join</c> y una sola ida a la base;
    /// con N:M, la traduccion ingenua —pedir los roles dentro del bucle— convierte esa pantalla
    /// en N+1 consultas. Es el mismo problema que <c>ContadorDeConsultas</c> vigila en otras
    /// rutas.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> ObtenerDeVariosAsync(
        PnmcDbContext db,
        IReadOnlyCollection<int> idsDeUsuario,
        CancellationToken cancelacion = default)
    {
        if (idsDeUsuario.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<string>>();
        }

        var filas = await db.UsuariosRoles.AsNoTracking()
            .Where(asignacion => idsDeUsuario.Contains(asignacion.UserId))
            .Join(
                db.Roles.AsNoTracking(),
                asignacion => asignacion.RoleId,
                rol => rol.Id,
                (asignacion, rol) => new { asignacion.UserId, rol.Name })
            .ToListAsync(cancelacion);

        return filas
            .GroupBy(fila => fila.UserId)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => Ordenar(grupo.Select(fila => fila.Name)));
    }

    /// <summary>
    /// Deja a esta persona <b>exactamente</b> con los roles indicados: anade los que falten y
    /// retira los que sobren.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NO ACUMULA. La primera version de la migracion de datos —el defecto U5 del plan— copiaba
    /// <c>IdRol</c> en cada pasada sin retirar nada, de modo que degradar a alguien le dejaba los
    /// dos roles: el viejo y el nuevo. En una tabla de permisos, acumular es siempre el error mas
    /// caro de los dos, porque no se nota.
    /// </para>
    /// <para>
    /// No llama a <c>SaveChangesAsync</c>: la ruta que la usa suele tener mas cosas que guardar en
    /// la misma transaccion, y partir el guardado en dos dejaria una ventana en la que la cuenta
    /// existe sin roles.
    /// </para>
    /// </remarks>
    /// <returns>Los nombres de rol que quedan asignados, en orden estable.</returns>
    public static async Task<IReadOnlyList<string>> AsignarAsync(
        PnmcDbContext db,
        UserRow usuario,
        IReadOnlyCollection<RoleRow> roles,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(roles);

        var deseados = roles.Select(rol => rol.Id).Distinct().ToList();

        var actuales = await db.UsuariosRoles
            .Where(asignacion => asignacion.UserId == usuario.Id)
            .ToListAsync(cancelacion);

        foreach (var sobrante in actuales.Where(asignacion => !deseados.Contains(asignacion.RoleId)))
        {
            db.UsuariosRoles.Remove(sobrante);
        }

        foreach (var idDeRol in deseados.Where(id => !actuales.Any(asignacion => asignacion.RoleId == id)))
        {
            db.UsuariosRoles.Add(new UsuarioRolRow
            {
                UserId = usuario.Id,
                RoleId = idDeRol,
                CreatedAt = DateTime.UtcNow
            });
        }

        return Ordenar(roles.Select(rol => rol.Name));
    }

    /// <summary>
    /// El rol que representa a la persona cuando solo cabe uno: la cookie, el DTO, la columna.
    /// </summary>
    /// <remarks>
    /// Por precedencia declarada, nunca por orden de llegada. Devuelve cadena vacia si no hay
    /// ninguno, que es un estado real —cuenta sin asignaciones— y no debe disfrazarse de
    /// <c>externo</c>.
    /// </remarks>
    public static string Principal(IEnumerable<string> roles)
    {
        var conjunto = Ordenar(roles);
        foreach (var candidato in Precedencia)
        {
            if (conjunto.Contains(candidato, StringComparer.Ordinal))
            {
                return candidato;
            }
        }

        return conjunto.Count > 0 ? conjunto[0] : string.Empty;
    }

    /// <summary>
    /// Normaliza, quita repetidos y ordena: primero los de <see cref="Precedencia"/> en su orden,
    /// despues cualquier otro alfabeticamente.
    /// </summary>
    /// <remarks>
    /// El orden estable no es cosmetica. Dos sitios dependen de el: el rol principal que se
    /// escribe en la cookie, y la comparacion de conjuntos que hace la revalidacion de sesion en
    /// cada peticion. Si el orden dependiera del plan de ejecucion de SQL Server, esa comparacion
    /// echaria a la gente al azar.
    /// </remarks>
    public static IReadOnlyList<string> Ordenar(IEnumerable<string?> roles)
    {
        return roles
            .Select(Normalizar)
            .Where(nombre => nombre.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(nombre => Array.IndexOf(Precedencia, nombre) is var puesto && puesto >= 0 ? puesto : int.MaxValue)
            .ThenBy(nombre => nombre, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>La misma normalizacion que usa el inicio de sesion: sin espacios y en minuscula.</summary>
    public static string Normalizar(string? rol) => (rol ?? string.Empty).Trim().ToLowerInvariant();
}
