using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Tests;

/// <summary>
/// Registra el contexto sobre SQLite. Es responsabilidad del arnés, no del servicio.
/// </summary>
/// <remarks>
/// <para>
/// Antes esto no existía porque no hacía falta: <c>AddPnmcInfrastructure</c>
/// miraba si el entorno se llamaba «Test» y registraba SQLite él mismo. Cómodo
/// para las fábricas —bastaba con <c>UseEnvironment("Test")</c> y aparecía una
/// base— y caro para el producto: obligaba a que el paquete de SQLite, con su
/// binario nativo, fuera dependencia de producción y viajara en cada despliegue
/// para no ejecutarse jamás.
/// </para>
/// <para>
/// Al quitarlo, el proveedor lo pone quien lo necesita. Esta extensión es ese
/// sitio único: si mañana la suite cambia de motor, se cambia aquí y no en cinco
/// fábricas que se fueron copiando entre sí.
/// </para>
/// <para>
/// Barre TODOS los registros del contexto y no solo <c>DbContextOptions&lt;T&gt;</c>.
/// Fuera de «Test» el proveedor registrado es SQL Server, y si sobreviviera
/// cualquier pieza de esa configuración junto a la de SQLite, EF abortaría por
/// tener dos proveedores para el mismo contexto.
/// </para>
/// </remarks>
internal static class ProveedorDePruebas
{
    public static IServiceCollection UsarSqliteDePruebas(
        this IServiceCollection services,
        string prefijo = "pnmc-pruebas")
    {
        var registrosDelContexto = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(PnmcDbContext)
                || descriptor.ServiceType == typeof(DbContextOptions)
                || (descriptor.ServiceType.IsGenericType
                    && descriptor.ServiceType.GetGenericArguments().Contains(typeof(PnmcDbContext))))
            .ToList();

        foreach (var descriptor in registrosDelContexto)
        {
            services.Remove(descriptor);
        }

        // Un fichero por fábrica y no una base en memoria compartida: varias
        // clases de prueba corren en paralelo y una base compartida las haría
        // pisarse los datos de forma intermitente, que es la peor forma de fallo
        // en una suite.
        var ruta = Path.Combine(Path.GetTempPath(), $"{prefijo}-{Guid.NewGuid():N}.db");
        services.AddDbContext<PnmcDbContext>(options => options.UseSqlite($"Data Source={ruta}"));

        return services;
    }
}
