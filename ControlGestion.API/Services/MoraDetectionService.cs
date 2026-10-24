using ControlGestion.API.Configuracion;

namespace ControlGestion.API.Services;

/// <summary>
/// Corre los procesos diarios al arrancar la API y después cada día a las 00:05 de Bolivia, cuando ya terminó el día de
/// ayer: las moras (3.2) y los clientes que pasan a disponible para re-visita (5.2). Al arrancar, porque si la API estuvo
/// apagada a esa hora (en Azure se duerme), esos cambios quedarían sin hacer. Los dos se pueden repetir sin efectos. En la
/// misma corrida borra las sesiones que terminaron hace más de 30 días y las ubicaciones de los promotores de hace más
/// de 30 días.
/// </summary>
public class MoraDetectionService(IServiceScopeFactory scopeFactory, TimeProvider reloj, ILogger<MoraDetectionService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DetectarMorasAsync();
            await PasarADisponiblesAsync();
            await BorrarSesionesTerminadasAsync();
            await BorrarUbicacionesViejasAsync();
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

    private async Task PasarADisponiblesAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var pasaron = await scope.ServiceProvider.GetRequiredService<RevisitaService>().PasarADisponiblesAsync();
            logger.LogInformation("Re-visitas: {Cantidad} clientes pasaron a disponible para re-visita.", pasaron);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al pasar los clientes a disponible para re-visita.");
        }
    }

    private async Task BorrarSesionesTerminadasAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var borradas = await scope.ServiceProvider.GetRequiredService<SesionService>().BorrarTerminadasAsync();
            logger.LogInformation("Limpieza de sesiones: {Cantidad} sesiones terminadas hace más de 30 días borradas.", borradas);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al borrar las sesiones terminadas.");
        }
    }

    private async Task BorrarUbicacionesViejasAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var borradas = await scope.ServiceProvider.GetRequiredService<RutaService>().BorrarUbicacionesViejasAsync();
            logger.LogInformation("Ubicaciones: {Cantidad} ubicaciones de promotores de hace más de 30 días borradas.", borradas);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al borrar las ubicaciones viejas de los promotores.");
        }
    }
}
