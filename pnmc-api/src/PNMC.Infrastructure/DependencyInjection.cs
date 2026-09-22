using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PNMC.Infrastructure.Data;
using PNMC.Infrastructure.Integrations.Participation;

namespace PNMC.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPnmcInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        // En pruebas NO se registra ningun proveedor, y esa ausencia es la
        // entrega: el proveedor de pruebas es SQLite, y registrarlo aqui obligaba
        // a que `Microsoft.EntityFrameworkCore.Sqlite` fuera dependencia de
        // PRODUCCION. Un motor de base de datos entero —con su binario nativo y
        // su propio historial de CVE, el ultimo de corrupcion de memoria con
        // 9,8 de gravedad— viajaba en cada despliegue para no ejecutarse nunca.
        //
        // El arnes de pruebas registra el suyo (ver TestWebApplicationFactory),
        // asi que aqui basta con no pisarlo ni exigir una cadena de SQL Server
        // que en pruebas no existe. Si alguien olvidara registrarlo, la primera
        // resolucion de PnmcDbContext falla con «No database provider has been
        // configured», que dice exactamente lo que pasa.
        if (!environment.IsEnvironment("Test"))
        {
            var sqlServerConnectionString = DatabaseConnectionResolver.ResolveSqlServerConnectionString(configuration);
            if (string.IsNullOrWhiteSpace(sqlServerConnectionString))
            {
                throw new InvalidOperationException(
                    "A SQL Server connection string is required. Use ConnectionStrings:SqlServer or AZURE_SQL_* environment variables.");
            }

            services.AddDbContext<PnmcDbContext>(options =>
                options.UseSqlServer(sqlServerConnectionString, sql => sql.EnableRetryOnFailure(5)));
        }

        services.AddScoped<IParticipationSubmissionStore, DbParticipationSubmissionStore>();

        return services;
    }
}
