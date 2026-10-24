using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// El tablero de Inicio: los indicadores y las ventas del mes en curso en Bolivia, con la cartera del usuario. Un
/// promotor ve los de sus clientes, no los de toda la empresa. El stock es de la empresa.
/// </summary>
public class TableroService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
{
    public async Task<TableroDTO> ResumenAsync()
    {
        var hoy = HoraBolivia.Hoy(reloj);
        var inicioDelMes = HoraBolivia.InicioDelDiaUtc(new DateOnly(hoy.Year, hoy.Month, 1));
        var delMes = db.HojasPedido.VisiblesPara(usuario)
            .Where(h => h.EstadoHP == EstadoHP.Confirmada && h.FechaRegistro >= inicioDelMes);

        var ventas = await delMes.Select(h => new { h.FechaRegistro, h.Total }).ToListAsync();
        var porPromotor = await delMes
            .GroupBy(h => new { h.PromotorId, h.Promotor!.NombreCompleto })
            .Select(g => new VentasDePromotorDTO(g.Key.PromotorId, g.Key.NombreCompleto, g.Count(), g.Sum(h => h.Total)))
            .ToListAsync();
        var porProducto = (await delMes
                .SelectMany(h => h.Detalles)
                .GroupBy(d => d.Producto!.Descripcion)
                .Select(g => new { Producto = g.Key, Cantidad = g.Sum(d => d.Cantidad), Total = g.Sum(d => d.Subtotal) })
                .OrderByDescending(p => p.Total)
                .Take(10)
                .ToListAsync())
            .Select(p => new VentasDeProductoDTO(p.Producto, p.Cantidad, p.Total))
            .ToList();
        // El día de cada venta es el de Bolivia: una HP de las 21:00 no cae en el día siguiente.
        var porDia = ventas
            .GroupBy(v => HoraBolivia.DiaDe(v.FechaRegistro))
            .OrderBy(g => g.Key)
            .Select(g => new VentasDelDiaDTO(g.Key.ToString("yyyy-MM-dd"), g.Count(), g.Sum(v => v.Total)))
            .ToList();

        return new TableroDTO(
            await db.Clientes.VisiblesPara(usuario).CountAsync(c => c.EstadoClienteId != EstadosDeCliente.Inactivo),
            ventas.Count,
            ventas.Sum(v => v.Total),
            await db.Cobranzas.VisiblesPara(usuario).Where(c => CobranzaConsultas.Abiertas.Contains(c.Estado))
                .SumAsync(c => c.MontoTotal - c.MontoPagado),
            await db.Eventos.VisiblesPara(usuario).CountAsync(e => e.EstadoEvento == EstadoEvento.Pendiente),
            await db.Productos.Where(p => p.Activo).BajoElMinimo().CountAsync(),
            porPromotor.OrderByDescending(p => p.Total).ToList(),
            porProducto,
            porDia);
    }
}
