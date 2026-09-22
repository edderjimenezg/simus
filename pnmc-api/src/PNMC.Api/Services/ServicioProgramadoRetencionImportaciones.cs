using Microsoft.Extensions.Options;
using PNMC.Infrastructure.Data;

namespace PNMC.Api.Services;

/// <summary>
/// Aplica en segundo plano la regla de retención. Un fallo se registra y se vuelve a intentar en
/// el siguiente ciclo; nunca impide que la API atienda las demás funciones.
/// </summary>
public sealed partial class ServicioProgramadoRetencionImportaciones(
    IServiceScopeFactory alcances,
    IOptions<RetencionImportacionesOptions> opciones,
    TimeProvider reloj,
    ILogger<ServicioProgramadoRetencionImportaciones> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        opciones.Value.Validar();
        await EjecutarCicloAsync(stoppingToken);
        using var temporizador = new PeriodicTimer(
            TimeSpan.FromHours(opciones.Value.IntervaloHoras),
            reloj);
        while (await temporizador.WaitForNextTickAsync(stoppingToken))
            await EjecutarCicloAsync(stoppingToken);
    }

    private async Task EjecutarCicloAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var alcance = alcances.CreateAsyncScope();
            var depurador = alcance.ServiceProvider.GetRequiredService<DepuradorImportaciones>();
            var resultado = await depurador.EjecutarAsync(
                reloj.GetUtcNow().UtcDateTime,
                opciones.Value.DiasPrevisualizacion,
                cancellationToken);
            if (resultado.LotesExpirados > 0 || resultado.LotesAplicadosMinimizados > 0)
            {
                RegistrarResultado(
                    logger,
                    resultado.LotesExpirados,
                    resultado.LotesAplicadosMinimizados);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cierre normal del proceso.
        }
        catch (Exception excepcion)
        {
            RegistrarFallo(logger, excepcion);
        }
    }

    [LoggerMessage(
        EventId = 9049,
        Level = LogLevel.Information,
        Message = "Retención de importaciones aplicada: {LotesExpirados} previsualizaciones expiradas y {LotesMinimizados} lotes aplicados minimizados.")]
    private static partial void RegistrarResultado(ILogger logger, int lotesExpirados, int lotesMinimizados);

    [LoggerMessage(
        EventId = 90491,
        Level = LogLevel.Error,
        Message = "No fue posible ejecutar la retención de importaciones; se reintentará en el siguiente ciclo.")]
    private static partial void RegistrarFallo(ILogger logger, Exception excepcion);
}
