using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PNMC.Domain.Entities;

namespace PNMC.Infrastructure.Data;

/// <summary>
/// Leer y fijar los vocabularios controlados de cualquier registro del Ecosistema.
/// </summary>
/// <remarks>
/// <para>
/// <b>EXISTE PARA QUE EL MODULO NO SE PUEDA OLVIDAR.</b> Las cinco tablas de clasificación son
/// compartidas por todos los procesos desde, así que una consulta que
/// filtre solo por el identificador del registro devolvería las prácticas musicales del Festival
/// 12 mezcladas con las de la edición 12. No es un riesgo teórico: es el precio de haber pasado de
/// catorce tablas a cinco, y se paga aquí, una vez, en lugar de en cada punto de entrada.
/// </para>
/// <para>
/// La clave del registro viaja como texto porque así viaja en la revisión, en la propuesta de
/// cambio y en la bitácora; aquí se convierte en un solo sitio.
/// </para>
/// </remarks>
public static class Clasificaciones
{
    /// <summary>La clave con la que un registro se nombra dentro de su módulo.</summary>
    public static string Clave(int registroId) => registroId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Las filas de una relación cualquiera que pertenecen a un registro.</summary>
    /// <remarks>
    /// Sirve para las ocho tablas: las cinco de vocabulario y las tres que guardan algo propio
    /// —dónde ocurre, con quién se hace y qué material tiene—.
    /// </remarks>
    public static IQueryable<TRelacion> Relacion<TRelacion>(
        this PnmcDbContext db, string modulo, int registroId)
        where TRelacion : class, IRelacionDeRegistro
    {
        var clave = Clave(registroId);
        return db.Set<TRelacion>().Where(fila => fila.ModuloId == modulo && fila.RegistroId == clave);
    }

    /// <summary>Borra la relación entera de un registro.</summary>
    public static async Task BorrarRelacionAsync<TRelacion>(
        this PnmcDbContext db, string modulo, int registroId, CancellationToken cancellationToken)
        where TRelacion : class, IRelacionDeRegistro
    {
        var actuales = await db.Relacion<TRelacion>(modulo, registroId).ToListAsync(cancellationToken);
        db.Set<TRelacion>().RemoveRange(actuales);
    }

    /// <summary>Las filas de un vocabulario que pertenecen a un registro.</summary>
    public static IQueryable<TClasificacion> De<TClasificacion>(
        this PnmcDbContext db, string modulo, int registroId)
        where TClasificacion : class, IClasificacionDeRegistro
    {
        var clave = Clave(registroId);
        return db.Set<TClasificacion>().Where(fila => fila.ModuloId == modulo && fila.RegistroId == clave);
    }

    /// <summary>Las filas de un vocabulario que pertenecen a varios registros de un mismo módulo.</summary>
    /// <remarks>
    /// Para cuando hace falta unir con el catálogo y quedarse con el nombre, no solo con el
    /// identificador. La clave del registro vuelve como texto: quien la necesite entera usa
    /// <see cref="ValoresPorRegistroAsync{TClasificacion}"/>.
    /// </remarks>
    public static IQueryable<TClasificacion> DeVarios<TClasificacion>(
        this PnmcDbContext db, string modulo, IReadOnlyCollection<int> registros)
        where TClasificacion : class, IClasificacionDeRegistro
    {
        var claves = registros.Select(Clave).ToList();
        return db.Set<TClasificacion>().Where(fila => fila.ModuloId == modulo && claves.Contains(fila.RegistroId));
    }

    /// <summary>El registro al que pertenece una fila, ya convertido.</summary>
    public static int Registro(string registroId) => int.Parse(registroId, CultureInfo.InvariantCulture);

    /// <summary>Los identificadores de catálogo atribuidos a un registro.</summary>
    public static async Task<List<int>> ValoresAsync<TClasificacion>(
        this PnmcDbContext db, string modulo, int registroId, CancellationToken cancellationToken)
        where TClasificacion : class, IClasificacionDeRegistro
    {
        var clave = Clave(registroId);
        return await db.Set<TClasificacion>().AsNoTracking()
            .Where(fila => fila.ModuloId == modulo && fila.RegistroId == clave)
            .Select(fila => fila.ValorId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lo mismo para muchos registros a la vez, en una sola consulta.
    /// </summary>
    /// <remarks>
    /// HACE FALTA PARA LAS LISTAS. Preguntar registro por registro dentro de un bucle convierte una
    /// pantalla de veinte filas en veinte viajes a la base.
    /// </remarks>
    public static async Task<Dictionary<int, List<int>>> ValoresPorRegistroAsync<TClasificacion>(
        this PnmcDbContext db, string modulo, IReadOnlyCollection<int> registros, CancellationToken cancellationToken)
        where TClasificacion : class, IClasificacionDeRegistro
    {
        if (registros.Count == 0) return [];
        var claves = registros.Select(Clave).ToList();
        var filas = await db.Set<TClasificacion>().AsNoTracking()
            .Where(fila => fila.ModuloId == modulo && claves.Contains(fila.RegistroId))
            .Select(fila => new { fila.RegistroId, fila.ValorId })
            .ToListAsync(cancellationToken);
        return filas
            .GroupBy(fila => int.Parse(fila.RegistroId, CultureInfo.InvariantCulture))
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Select(fila => fila.ValorId).ToList());
    }

    /// <summary>
    /// Deja el vocabulario de un registro exactamente en los valores indicados.
    /// </summary>
    /// <remarks>
    /// REEMPLAZA, NO AÑADE. Es lo que hacía cada punto de entrada a mano —borrar lo que había y
    /// volver a escribirlo—, y es lo correcto: un conjunto de casillas marcadas se guarda entero,
    /// porque desmarcar una tiene que borrarla.
    /// </remarks>
    public static async Task FijarAsync<TClasificacion>(
        this PnmcDbContext db, string modulo, int registroId, IEnumerable<int> valores, DateTime ahora,
        CancellationToken cancellationToken)
        where TClasificacion : class, IClasificacionDeRegistro, new()
    {
        var clave = Clave(registroId);
        var actuales = await db.Set<TClasificacion>()
            .Where(fila => fila.ModuloId == modulo && fila.RegistroId == clave)
            .ToListAsync(cancellationToken);
        db.Set<TClasificacion>().RemoveRange(actuales);
        Agregar<TClasificacion>(db, modulo, registroId, valores, ahora);
    }

    /// <summary>Añade valores sin mirar lo que hubiera, para cuando el registro acaba de nacer.</summary>
    public static void Agregar<TClasificacion>(
        this PnmcDbContext db, string modulo, int registroId, IEnumerable<int> valores, DateTime ahora)
        where TClasificacion : class, IClasificacionDeRegistro, new()
    {
        var clave = Clave(registroId);
        db.Set<TClasificacion>().AddRange(valores.Distinct().Select(valorId => new TClasificacion
        {
            ModuloId = modulo,
            RegistroId = clave,
            ValorId = valorId,
            FechaCreacion = ahora,
        }));
    }

    /// <summary>Borra el vocabulario entero de un registro, para cuando el registro desaparece.</summary>
    public static async Task BorrarAsync<TClasificacion>(
        this PnmcDbContext db, string modulo, int registroId, CancellationToken cancellationToken)
        where TClasificacion : class, IClasificacionDeRegistro
    {
        var clave = Clave(registroId);
        var actuales = await db.Set<TClasificacion>()
            .Where(fila => fila.ModuloId == modulo && fila.RegistroId == clave)
            .ToListAsync(cancellationToken);
        db.Set<TClasificacion>().RemoveRange(actuales);
    }
}
