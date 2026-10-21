using ControlGestion.API.Configuracion;

namespace ControlGestion.API.Services;

/// <summary>
/// Corre el proceso de mora al arrancar la API y después cada día a las 00:05 de Bolivia, cuando ya terminó el día de
/// vencimiento de ayer. Al arrancar, porque si la API estuvo apagada a esa hora, las moras de ese día quedarían sin marcar.
/// </summary>
public class MoraDetectionService(IServiceScopeFactory scopeFactory, TimeProvider reloj, ILogger<MoraDetectionService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DetectarMorasAsync();
            try
            {
                await Task.Delay(HastaLaProximaCorrida(), reloj, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private TimeSpan HastaLaProximaCorrida()
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var hoy = HoraBolivia.Hoy(reloj);
        var proxima = HoraBolivia.InicioDelDiaUtc(hoy).AddMinutes(5);
        if (proxima <= ahora) proxima = HoraBolivia.InicioDelDiaUtc(hoy.AddDays(1)).AddMinutes(5);
        return proxima - ahora;
    }

    private async Task DetectarMorasAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var marcadas = await scope.ServiceProvider.GetRequiredService<MoraService>().MarcarVencidasAsync();
            logger.LogInformation("Detección de moras: {Cantidad} cobranzas marcadas como vencidas.", marcadas);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en la detección automática de moras.");
        }
    }
}
