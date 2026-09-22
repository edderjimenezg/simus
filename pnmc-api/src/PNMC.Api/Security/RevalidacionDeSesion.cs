using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Security;

/// <summary>
/// Estado de una cuenta tal y como está EN LA BASE ahora mismo, no como lo decía la cookie.
/// </summary>
/// <param name="Activo">La columna <c>Usuarios.Activo</c>.</param>
/// <param name="Roles">
/// Los roles que la cuenta tiene EN <c>dbo.UsuariosRoles</c>, normalizados y en orden estable
/// (<see cref="RolesDeUsuario.Ordenar"/>). Puede venir vacía: una cuenta sin ninguna asignación
/// es un estado real desde entonces, y significa que esa persona no debe seguir dentro.
/// </param>
public sealed record EstadoDeCuenta(bool Activo, IReadOnlyList<string> Roles)
{
    /// <summary>
    /// Los roles como una sola cadena, para comparar dos conjuntos sin escribir el bucle.
    /// </summary>
    /// <remarks>
    /// Es comparable porque <see cref="RolesDeUsuario.Ordenar"/> garantiza orden y unicidad; sin
    /// esa garantía, <c>{webmaster, gestor}</c> y <c>{gestor, webmaster}</c> parecerían distintos
    /// y la revalidación echaría a la gente en cada petición.
    /// </remarks>
    public string Huella { get; } = string.Join(',', Roles);
}

/// <summary>
/// Revalida cada sesión contra la base para que <b>desactivar a alguien lo deje fuera de verdad</b>.
/// </summary>
/// <remarks>
/// <para>
/// EL PROBLEMA QUE RESUELVE. Las dos cookies del sistema duran 8 horas y son deslizantes: se
/// renuevan solas con el uso. Las políticas de autorización solo leen los <i>claims</i> que la
/// cookie trae dentro, y esos claims se escribieron el día del inicio de sesión. Resultado: hasta
///, <b>desactivar a un usuario no lo echaba</b>. Mientras siguiera
/// usando la consola, su sesión se renovaba indefinidamente y conservaba el rol que tenía al
/// entrar, aunque en la base ya no estuviera activo ni tuviera ese rol. Para un funcionario que
/// deja el cargo, eso es acceso a toda la administración de datos por tiempo ilimitado.
/// </para>
/// <para>
/// CÓMO SE RESUELVE, Y POR QUÉ ASÍ. En cada petición, el manejador de la cookie pregunta aquí por
/// el estado real de la cuenta. Preguntar a la base en <i>cada</i> petición sería correcto pero
/// caro, así que la respuesta se guarda unos segundos (<see cref="Ventana"/>). Ese plazo es el
/// techo de lo que un revocado podría seguir dentro… <b>salvo en el caso que de verdad importa</b>:
/// cuando es el propio sistema quien desactiva a alguien o le cambia el rol, la ruta que lo hace
/// llama a <see cref="Invalidar"/> y el efecto es <b>inmediato</b>, sin esperar a que la ventana
/// expire. La ventana solo cubre los cambios hechos por fuera del API —un <c>UPDATE</c> a mano
/// contra la base—, que son los raros.
/// </para>
/// <para>
/// POR QUÉ NO SE COMPARA UN «SELLO DE SEGURIDAD». Sería la otra solución clásica, pero exige una
/// columna nueva en <c>Usuarios</c> y reescribir el inicio de sesión de los dos esquemas. Esto no
/// toca el esquema ni el contrato: lee lo que ya existe (<c>Activo</c> y el rol) y compara contra
/// el claim que la cookie ya traía.
/// </para>
/// <para>
/// ES UN SINGLETON y el contexto de datos tiene alcance por petición, de ahí
/// <see cref="IServiceScopeFactory"/>: pedirle un <c>PnmcDbContext</c> al constructor lo
/// capturaría para siempre y sería un error clásico de vida de objetos.
/// </para>
/// </remarks>
public sealed class RevalidacionDeSesion
{
    /// <summary>Clave de configuración del techo, en segundos.</summary>
    public const string ClaveDeConfiguracion = "Security:RevalidacionDeSesionSegundos";

    /// <summary>
    /// Techo por omisión: 30 segundos.
    /// </summary>
    /// <remarks>
    /// Es el tiempo máximo que un usuario desactivado POR FUERA del API seguiría dentro. Se eligió
    /// corto a propósito: el coste es una consulta por usuario cada 30 s —despreciable en una
    /// consola interna de decenas de personas— y la alternativa es explicarle a alguien por qué
    /// quien fue dado de baja seguía firmando cambios. Si algún día el volumen lo justifica, se
    /// sube por configuración; lo que no debe hacerse es apagarlo.
    /// </remarks>
    public const int SegundosPorOmision = 30;

    private readonly IServiceScopeFactory _fabricaDeAlcances;
    private readonly ConcurrentDictionary<int, (EstadoDeCuenta? Estado, DateTimeOffset Caduca)> _cache = new();
    private readonly TimeProvider _reloj;

    public RevalidacionDeSesion(IServiceScopeFactory fabricaDeAlcances, IConfiguration configuracion, TimeProvider? reloj = null)
    {
        _fabricaDeAlcances = fabricaDeAlcances;
        _reloj = reloj ?? TimeProvider.System;

        var segundos = configuracion.GetValue<int?>(ClaveDeConfiguracion) ?? SegundosPorOmision;
        // Un valor absurdo (0, negativo) no debe convertirse en «sin caché» silencioso ni en
        // «caché eterna»: se acota y se sigue. Cero significaria consultar en cada peticion, que
        // es correcto pero no es lo que nadie quiso escribir si puso un 0 por descuido.
        Ventana = TimeSpan.FromSeconds(Math.Clamp(segundos, 1, 300));
    }

    /// <summary>Cuánto se reutiliza una respuesta antes de volver a preguntar a la base.</summary>
    public TimeSpan Ventana { get; }

    /// <summary>
    /// Estado real de la cuenta. Devuelve <c>null</c> si el usuario ya no existe.
    /// </summary>
    public async Task<EstadoDeCuenta?> ObtenerAsync(int idUsuario, CancellationToken cancelacion = default)
    {
        var ahora = _reloj.GetUtcNow();
        if (_cache.TryGetValue(idUsuario, out var guardado) && guardado.Caduca > ahora)
        {
            return guardado.Estado;
        }

        using var alcance = _fabricaDeAlcances.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<PnmcDbContext>();

        // DOS CONSULTAS, NO UN JOIN, Y ES DELIBERADO. Desde la transicion los roles son varios, asi
        // que el join devolveria una fila POR ROL y habria que decidir que hacer con la cuenta
        // que no tiene ninguno. Separandolas, «existe pero sin roles» se distingue de «no
        // existe», que son dos cosas distintas: la primera se echa, la segunda ademas se registra
        // como cookie de un usuario que ya no esta.
        var cuenta = await db.Users.AsNoTracking()
            .Where(usuario => usuario.Id == idUsuario)
            .Select(usuario => new { usuario.IsActive })
            .FirstOrDefaultAsync(cancelacion);

        var estado = cuenta is null
            ? null
            : new EstadoDeCuenta(cuenta.IsActive, await RolesDeUsuario.ObtenerAsync(db, idUsuario, cancelacion));
        _cache[idUsuario] = (estado, ahora.Add(Ventana));
        return estado;
    }

    /// <summary>
    /// Olvida lo que se sabía de esta cuenta: la próxima petición vuelve a preguntar a la base.
    /// </summary>
    /// <remarks>
    /// La llama toda ruta que desactive a alguien o le cambie el rol. Es lo que convierte el techo
    /// de la caché en irrelevante para el caso normal: si la baja se hace desde el panel, tiene
    /// efecto en la siguiente petición de esa persona, no dentro de treinta segundos.
    /// </remarks>
    public void Invalidar(int idUsuario) => _cache.TryRemove(idUsuario, out _);

    /// <summary>Misma normalización que el inicio de sesión, para que la comparación sea justa.</summary>
    public static string Normalizar(string? rol) => (rol ?? string.Empty).Trim().ToLowerInvariant();
}
