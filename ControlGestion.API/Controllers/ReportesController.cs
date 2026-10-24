using ClosedXML.Excel;
using ControlGestion.API.Data;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportesController(AppDbContext db) => _db = db;

    // ─── Dashboard KPIs ───────────────────────────────────────────────────────

    // Sin operación en el catálogo: es el inicio del panel.
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var hoy = DateTime.UtcNow.Date;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalClientes = await _db.Clientes.CountAsync(c => c.EstadoClienteId != EstadosDeCliente.Inactivo);
        var pedidosMes = await _db.HojasPedido
            .Where(h => h.FechaRegistro >= inicioMes && h.EstadoHP == EstadoHP.Confirmada)
            .CountAsync();
        var ventasMes = await _db.HojasPedido
            .Where(h => h.FechaRegistro >= inicioMes && h.EstadoHP == EstadoHP.Confirmada)
            .SumAsync(h => (decimal?)h.Total) ?? 0;
        var cobranzasPendientes = await _db.Cobranzas
            .Where(c => c.Estado == EstadoCobranza.Pendiente || c.Estado == EstadoCobranza.Parcial || c.Estado == EstadoCobranza.Vencida)
            .SumAsync(c => (decimal?)(c.MontoTotal - c.MontoPagado)) ?? 0;
        var eventosAbiertos = await _db.Eventos.CountAsync(e => e.EstadoEvento == EstadoEvento.Pendiente);
        var stockBajo = await _db.InventariosProducto
            .CountAsync(i => i.StockActual <= i.StockMinimo);

        return Ok(new
        {
            totalClientes,
            pedidosMes,
            ventasMes,
            cobranzasPendientes,
            eventosAbiertos,
            stockBajo
        });
    }

    // ─── Ventas por período ───────────────────────────────────────────────────

    // Sin operación en el catálogo: el inicio del panel usa sus totales.
    [HttpGet("ventas")]
    public async Task<IActionResult> Ventas(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? promotorId,
        [FromQuery] int? zonaId)
    {
        desde ??= DateTime.UtcNow.AddMonths(-1);
        hasta = (hasta ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);

        var query = _db.HojasPedido
            .Include(h => h.Cliente)
            .Include(h => h.Promotor)
            .Include(h => h.Zona)
            .Include(h => h.Detalles).ThenInclude(d => d.Producto)
            .Where(h => h.EstadoHP == EstadoHP.Confirmada
                     && h.FechaRegistro >= desde
                     && h.FechaRegistro <= hasta);

        if (!string.IsNullOrEmpty(promotorId)) query = query.Where(h => h.PromotorId == promotorId);
        if (zonaId.HasValue) query = query.Where(h => h.ZonaId == zonaId);

        var hojas = await query.OrderByDescending(h => h.FechaRegistro).ToListAsync();

        var porDia = hojas
            .GroupBy(h => h.FechaRegistro.Date)
            .Select(g => new { Fecha = g.Key.ToString("yyyy-MM-dd"), Total = g.Sum(h => h.Total), Cantidad = g.Count() })
            .OrderBy(x => x.Fecha)
            .ToList();

        var porPromotor = hojas
            .GroupBy(h => h.Promotor?.NombreCompleto ?? "—")
            .Select(g => new { Promotor = g.Key, Total = g.Sum(h => h.Total), Cantidad = g.Count() })
            .OrderByDescending(x => x.Total)
            .ToList();

        var porProducto = hojas
            .SelectMany(h => h.Detalles)
            .GroupBy(d => d.Producto?.Descripcion ?? "—")
            .Select(g => new { Producto = g.Key, Cantidad = g.Sum(d => d.Cantidad), Total = g.Sum(d => d.Subtotal) })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToList();

        return Ok(new
        {
            resumen = new
            {
                totalVentas = hojas.Sum(h => h.Total),
                cantidadHPs = hojas.Count,
                ticketPromedio = hojas.Count > 0 ? hojas.Average(h => h.Total) : 0,
                ventasCredito = hojas.Where(h => h.TipoVenta == TipoVenta.Credito).Sum(h => h.Total),
                ventasContado = hojas.Where(h => h.TipoVenta == TipoVenta.Contado).Sum(h => h.Total),
            },
            porDia,
            porPromotor,
            porProducto
        });
    }

    // ─── Inventario ───────────────────────────────────────────────────────────

    [HttpGet("inventario")]
    [Permiso(Permisos.Reportes.Inventario)]
    public async Task<IActionResult> ResumenInventario()
    {
        var inventario = await _db.InventariosProducto
            .Include(i => i.Producto)
            .ToListAsync();

        var movimientos = await _db.MovimientosInventario
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .Where(m => m.Fecha >= DateTime.UtcNow.AddDays(-30))
            .OrderByDescending(m => m.Fecha)
            .Take(100)
            .ToListAsync();

        return Ok(new
        {
            stockTotal = inventario.Sum(i => i.StockActual),
            valorTotal = inventario.Sum(i => i.StockActual * (i.Producto?.Precio ?? 0)),
            stockBajo = inventario.Where(i => i.StockActual <= i.StockMinimo).Select(i => new
            {
                Producto = i.Producto?.Descripcion,
                i.StockActual,
                i.StockMinimo,
                Deficit = i.StockMinimo - i.StockActual
            }),
            movimientos30Dias = movimientos.Select(m => new
            {
                m.Id,
                Producto = m.Producto?.Descripcion,
                m.TipoMovimiento,
                m.Cantidad,
                m.StockResultante,
                m.Fecha,
                Usuario = m.Usuario?.NombreCompleto
            })
        });
    }

    // ─── Promotores rendimiento ───────────────────────────────────────────────

    [HttpGet("promotores")]
    [Permiso(Permisos.Reportes.VentasPorPromotor)]
    public async Task<IActionResult> RendimientoPromotores(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        desde ??= DateTime.UtcNow.AddMonths(-1);
        hasta = (hasta ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);

        var hojas = await _db.HojasPedido
            .Include(h => h.Promotor)
            .Where(h => h.EstadoHP == EstadoHP.Confirmada && h.FechaRegistro >= desde && h.FechaRegistro <= hasta)
            .GroupBy(h => new { h.PromotorId, h.Promotor!.NombreCompleto })
            .Select(g => new
            {
                PromotorId = g.Key.PromotorId,
                Promotor = g.Key.NombreCompleto,
                CantidadHPs = g.Count(),
                TotalVentas = g.Sum(h => h.Total),
                TicketPromedio = g.Average(h => h.Total)
            })
            .OrderByDescending(x => x.TotalVentas)
            .ToListAsync();

        var clientes = await _db.Clientes
            .Where(c => c.EstadoClienteId != EstadosDeCliente.Inactivo)
            .GroupBy(c => c.PromotorId)
            .Select(g => new { PromotorId = g.Key, Clientes = g.Count() })
            .ToListAsync();

        var eventos = await _db.Eventos
            .Where(e => e.FechaEvento >= desde && e.FechaEvento <= hasta)
            .GroupBy(e => e.PromotorId)
            .Select(g => new { PromotorId = g.Key, Eventos = g.Count() })
            .ToListAsync();

        var resultado = hojas.Select(h => new
        {
            h.Promotor, h.CantidadHPs, h.TotalVentas, h.TicketPromedio,
            ClientesAsignados = clientes.FirstOrDefault(c => c.PromotorId == h.PromotorId)?.Clientes ?? 0,
            EventosRegistrados = eventos.FirstOrDefault(e => e.PromotorId == h.PromotorId)?.Eventos ?? 0
        });

        return Ok(resultado);
    }

    // ─── Exportar Excel ───────────────────────────────────────────────────────

    [HttpGet("export/ventas")]
    [Permiso(Permisos.Reportes.VentasPorPromotor)]
    public async Task<IActionResult> ExportVentas(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        desde ??= DateTime.UtcNow.AddMonths(-1);
        hasta ??= DateTime.UtcNow;

        hasta = (hasta ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);

        var hojas = await _db.HojasPedido
            .Include(h => h.Cliente)
            .Include(h => h.Promotor)
            .Include(h => h.Zona)
            .Include(h => h.Detalles).ThenInclude(d => d.Producto)
            .Where(h => h.EstadoHP == EstadoHP.Confirmada && h.FechaRegistro >= desde && h.FechaRegistro <= hasta)
            .OrderByDescending(h => h.FechaRegistro)
            .ToListAsync();

        using var wb = new XLWorkbook();

        // Hoja 1: Resumen de HPs
        var wsHPs = wb.Worksheets.Add("Hojas de Pedido");
        var headersHP = new[] { "Número", "Fecha", "Cliente", "Zona", "Promotor", "Tipo Venta", "Subtotal", "Descuento", "Total" };
        for (int i = 0; i < headersHP.Length; i++)
        {
            wsHPs.Cell(1, i + 1).Value = headersHP[i];
            wsHPs.Cell(1, i + 1).Style.Font.Bold = true;
            wsHPs.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
            wsHPs.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }
        int rowHP = 2;
        foreach (var hp in hojas)
        {
            wsHPs.Cell(rowHP, 1).Value = hp.Numero;
            wsHPs.Cell(rowHP, 2).Value = hp.FechaRegistro.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            wsHPs.Cell(rowHP, 3).Value = hp.Cliente?.NombreCompleto ?? "—";
            wsHPs.Cell(rowHP, 4).Value = hp.Zona?.Nombre ?? "—";
            wsHPs.Cell(rowHP, 5).Value = hp.Promotor?.NombreCompleto ?? "—";
            wsHPs.Cell(rowHP, 6).Value = hp.TipoVenta.ToString();
            wsHPs.Cell(rowHP, 7).Value = hp.SubTotal;
            wsHPs.Cell(rowHP, 8).Value = hp.Descuento;
            wsHPs.Cell(rowHP, 9).Value = hp.Total;
            wsHPs.Cell(rowHP, 7).Style.NumberFormat.Format = "#,##0.00";
            wsHPs.Cell(rowHP, 8).Style.NumberFormat.Format = "#,##0.00";
            wsHPs.Cell(rowHP, 9).Style.NumberFormat.Format = "#,##0.00";
            if (rowHP % 2 == 0)
                for (int c = 1; c <= 9; c++)
                    wsHPs.Cell(rowHP, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
            rowHP++;
        }
        wsHPs.Columns().AdjustToContents();

        // Hoja 2: Detalles de productos
        var wsDetalle = wb.Worksheets.Add("Detalle de Productos");
        var headersD = new[] { "HP Número", "Fecha", "Producto", "Categoría", "Cantidad", "Precio Unit.", "Descuento", "Subtotal" };
        for (int i = 0; i < headersD.Length; i++)
        {
            wsDetalle.Cell(1, i + 1).Value = headersD[i];
            wsDetalle.Cell(1, i + 1).Style.Font.Bold = true;
            wsDetalle.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
            wsDetalle.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }
        int rowD = 2;
        foreach (var hp in hojas)
        {
            foreach (var d in hp.Detalles)
            {
                wsDetalle.Cell(rowD, 1).Value = hp.Numero;
                wsDetalle.Cell(rowD, 2).Value = hp.FechaRegistro.ToLocalTime().ToString("dd/MM/yyyy");
                wsDetalle.Cell(rowD, 3).Value = d.Producto?.Descripcion ?? "—";
                wsDetalle.Cell(rowD, 4).Value = d.Producto?.Categoria ?? "—";
                wsDetalle.Cell(rowD, 5).Value = d.Cantidad;
                wsDetalle.Cell(rowD, 6).Value = d.PrecioUnitario;
                wsDetalle.Cell(rowD, 7).Value = d.Descuento;
                wsDetalle.Cell(rowD, 8).Value = d.Subtotal;
                for (int c = 6; c <= 8; c++)
                    wsDetalle.Cell(rowD, c).Style.NumberFormat.Format = "#,##0.00";
                if (rowD % 2 == 0)
                    for (int c = 1; c <= 8; c++)
                        wsDetalle.Cell(rowD, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
                rowD++;
            }
        }
        wsDetalle.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Seek(0, SeekOrigin.Begin);

        var fileName = $"ventas_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
