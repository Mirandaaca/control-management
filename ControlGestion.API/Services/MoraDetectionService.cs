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
        const int intentosMaximos = 3;
        for (var intento = 1; intento <= intentosMaximos; intento++)
        {
            try
            {
                await MarcarCobranzasVencidasAsync();
                return;
            }
            catch (DbUpdateConcurrencyException) when (intento < intentosMaximos)
            {
                // Un pago cambió alguna cobranza entre la lectura y el guardado. Se vuelve a leer todo:
                // si ese pago la cerró, ya no aparece como vencida.
                _logger.LogWarning("Detección de moras: conflicto de concurrencia en el intento {Intento}; se reintenta.", intento);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la detección automática de moras.");
                return;
            }
        }
    }

    private async Task MarcarCobranzasVencidasAsync()
    {
        // Un scope nuevo en cada intento: el DbContext de un intento fallido guarda valores viejos.
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
}
