using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Las ventas por zona (6.2, 7.3.23) y por promotor (6.1, 7.3.22): solo las HP confirmadas, por el día de su registro en
/// Bolivia, con la cartera del usuario. Ninguno de los dos se exporta: las especificaciones no lo piden.
/// </summary>
public class ReporteVentasService(AppDbContext db, IUsuarioActual usuario)
{
    /// <summary>Las HP confirmadas del período (una anulada ya no es una venta). Sin fechas, todas.</summary>
    private IQueryable<HojaPedido> Confirmadas(FiltrosPeriodoDTO filtros)
    {
        var consulta = db.HojasPedido.VisiblesPara(usuario).Where(h => h.EstadoHP == EstadoHP.Confirmada);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(h => h.FechaRegistro >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(h => h.FechaRegistro < fin);
        }
        return consulta;
    }

    public async Task<ReporteVentasPorZonaDTO> PorZonaAsync(FiltrosPeriodoDTO filtros)
    {
        var porZona = await Confirmadas(filtros)
            .GroupBy(h => h.ZonaId)
            .Select(g => new
            {
                ZonaId = g.Key,
                Cantidad = g.Count(),
                Total = g.Sum(h => h.Total),
                Contado = g.Sum(h => h.TipoVenta == TipoVenta.Contado ? h.Total : 0m),
                Credito = g.Sum(h => h.TipoVenta == TipoVenta.Credito ? h.Total : 0m)
            })
            .ToDictionaryAsync(g => g.ZonaId);

        // Las zonas sin ventas salen en cero (ext. 2.3a). Sin cartera completa, las zonas del usuario y las de sus ventas
        // (una HP conserva su zona aunque el cliente se mude).
        var (usuarioId, completa) = (usuario.Id, usuario.VeCarteraCompleta);
        var ventasEn = porZona.Keys.ToList();
        var zonas = await db.Zonas
            .Where(z => completa || z.PromotorId == usuarioId || ventasEn.Contains(z.Id))
            .Select(z => new { z.Id, z.Nombre })
            .ToListAsync();

        var filas = zonas
            .Select(z => porZona.TryGetValue(z.Id, out var v)
                ? new VentasDeZonaDTO(z.Id, z.Nombre, v.Cantidad, v.Total, v.Contado, v.Credito)
                : new VentasDeZonaDTO(z.Id, z.Nombre, 0, 0m, 0m, 0m))
            .OrderByDescending(z => z.Total).ThenBy(z => z.Zona)
            .ToList();
        var total = new TotalVentasDTO(filas.Sum(z => z.Cantidad), filas.Sum(z => z.Total), filas.Sum(z => z.Contado),
            filas.Sum(z => z.Credito));
        return new ReporteVentasPorZonaDTO(filas, total);
    }

    public async Task<ReporteVentasPorPromotorDTO> PorPromotorAsync(FiltrosVentasPorPromotorDTO filtros)
    {
        var consulta = Confirmadas(filtros);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(h => h.PromotorId == filtros.PromotorId);

        var promotores = await consulta
            .GroupBy(h => new { h.PromotorId, h.Promotor!.NombreCompleto })
            .Select(g => new VentasDePromotorDTO(g.Key.PromotorId, g.Key.NombreCompleto, g.Count(), g.Sum(h => h.Total)))
            .ToListAsync();
        var ordenados = promotores.OrderByDescending(p => p.Total).ThenBy(p => p.Promotor).ToList();
        return new ReporteVentasPorPromotorDTO(ordenados, ordenados.Sum(p => p.Cantidad), ordenados.Sum(p => p.Total));
    }
}
