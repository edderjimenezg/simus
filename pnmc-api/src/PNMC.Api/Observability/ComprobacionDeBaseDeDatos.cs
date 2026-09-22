using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Observability;

/// <summary>
/// La unica comprobacion de salud con capacidad de ponerse en rojo: ¿responde la base?
/// </summary>
/// <remarks>
/// <para>
/// POR QUE EXISTE. Hasta <c>AddHealthChecks()</c> no registraba
/// ninguna comprobacion, de modo que <c>/health/live</c> y <c>/health/ready</c> eran
/// identicos, vacios y respondian <c>Healthy</c> con la base caida o con la cadena de
/// conexion en blanco. Verificado en ejecucion. Para un balanceador, el health check de
/// App Service o un intercambio de ranuras en Azure, una instancia sin base pasaba la
/// puerta como sana y recibia trafico que iba a morir en 500.
/// </para>
/// <para>
/// QUE COMPRUEBA Y QUE NO. Abre un ambito propio —el health check es singleton y el
/// contexto es scoped— y pregunta a EF si puede conectar. No ejecuta consultas de negocio
/// ni mira el esquema: una sonda de readiness debe ser barata y no puede exigir sesion.
/// La pregunta «¿estan las tablas?» la responde el arranque, no la sonda.
/// </para>
/// <para>
/// SOLO VA EN <c>/health/ready</c>, por la etiqueta <c>ready</c>. <c>/health/live</c>
/// sigue sin dependencias a proposito: liveness responde «el proceso esta vivo», y si
/// dependiera de la base un hipo de SQL Server haria que el orquestador reiniciara el
/// contenedor en bucle en vez de simplemente dejar de enrutarle trafico.
/// </para>
/// </remarks>
public sealed class ComprobacionDeBaseDeDatos(IServiceScopeFactory ambitos) : IHealthCheck
{
    public const string Nombre = "base-de-datos";
    public const string EtiquetaReady = "ready";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var ambito = ambitos.CreateScope();
            var db = ambito.ServiceProvider.GetRequiredService<PnmcDbContext>();
            var responde = await db.Database.CanConnectAsync(cancellationToken);
            return responde
                ? HealthCheckResult.Healthy("La base de datos responde.")
                : HealthCheckResult.Unhealthy("La base de datos no responde.");
        }
        catch (Exception excepcion)
        {
            // El detalle queda en el registro estructurado via la excepcion; la sonda
            // solo dice que no esta lista. No se revela la cadena ni el servidor.
            return HealthCheckResult.Unhealthy("No fue posible conectar con la base de datos.", excepcion);
        }
    }
}
