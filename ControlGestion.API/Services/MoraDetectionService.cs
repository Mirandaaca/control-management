using ControlGestion.API.Data;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

public class MoraDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MoraDetectionService> _logger;

    public MoraDetectionService(IServiceScopeFactory scopeFactory, ILogger<MoraDetectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(2); // 02:00 UTC next day
            var delay = nextRun - now;

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!stoppingToken.IsCancellationRequested)
                await DetectarMorasAsync();
        }
    }

    private async Task DetectarMorasAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var hoy = DateTime.UtcNow.Date;

            var vencidas = await db.Cobranzas
                .Where(c => c.Estado == EstadoCobranza.Pendiente || c.Estado == EstadoCobranza.Parcial)
                .Where(c => c.FechaVencimiento.Date < hoy)
                .Include(c => c.Cliente)
                .ToListAsync();

            foreach (var cobranza in vencidas)
            {
                cobranza.Estado = EstadoCobranza.Vencida;

                if (cobranza.Cliente != null)
                {
                    var estadoMora = await db.EstadosCliente.FirstOrDefaultAsync(e => e.Nombre == "Con Deuda Vencida");
                    if (estadoMora != null)
                        cobranza.Cliente.EstadoClienteId = estadoMora.Id;
                }
            }

            await db.SaveChangesAsync();
            _logger.LogInformation("Mora detection: {Count} cobranzas marcadas como vencidas.", vencidas.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en la detección automática de moras.");
        }
    }
}
