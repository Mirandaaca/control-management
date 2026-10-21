using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Marca vencidas las cobranzas abiertas cuyo plazo ya terminó (3.2). No toca al cliente: su estado (5.2) sale solo de
/// las visitas.
/// </summary>
public class MoraService(AppDbContext db, TimeProvider reloj)
{
    public Task<int> MarcarVencidasAsync()
    {
        // Una cobranza vence cuando termina, en Bolivia, el día de su vencimiento.
        var inicioDeHoy = HoraBolivia.InicioDelDiaUtc(HoraBolivia.Hoy(reloj));
        // Un solo UPDATE en la base: si un pago cierra la deuda al mismo tiempo, esa cobranza ya no cumple el filtro.
        return db.Cobranzas
            .Where(c => (c.Estado == EstadoCobranza.Pendiente || c.Estado == EstadoCobranza.Parcial) && c.FechaVencimiento < inicioDeHoy)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCobranza.Vencida));
    }
}
