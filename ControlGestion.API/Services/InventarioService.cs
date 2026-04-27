using ControlGestion.API.Data;
using ControlGestion.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

public class InventarioService
{
    private readonly AppDbContext _db;

    public InventarioService(AppDbContext db)
    {
        _db = db;
    }

    public async Task DescontarStockPorHP(HojaPedido hp, string usuarioId)
    {
        foreach (var detalle in hp.Detalles)
        {
            var inventario = await _db.InventariosProducto
                .FirstOrDefaultAsync(i => i.ProductoId == detalle.ProductoId);

            if (inventario == null)
            {
                inventario = new InventarioProducto
                {
                    ProductoId = detalle.ProductoId,
                    StockActual = 0
                };
                _db.InventariosProducto.Add(inventario);
                await _db.SaveChangesAsync();
            }

            var stockAnterior = inventario.StockActual;
            inventario.StockActual -= detalle.Cantidad;
            inventario.UltimaActualizacion = DateTime.UtcNow;

            _db.MovimientosInventario.Add(new MovimientoInventario
            {
                ProductoId = detalle.ProductoId,
                TipoMovimiento = TipoMovimiento.Salida,
                Cantidad = detalle.Cantidad,
                StockAnterior = stockAnterior,
                StockResultante = inventario.StockActual,
                ReferenciaTabla = "HojaPedido",
                ReferenciaId = hp.Id,
                UsuarioId = usuarioId,
                Fecha = DateTime.UtcNow
            });
        }
    }

    public async Task AjustarStock(int productoId, decimal cantidad, string usuarioId, string observacion)
    {
        var inventario = await _db.InventariosProducto
            .FirstOrDefaultAsync(i => i.ProductoId == productoId)
            ?? throw new InvalidOperationException("Inventario no encontrado para este producto.");

        var stockAnterior = inventario.StockActual;
        inventario.StockActual += cantidad;
        inventario.UltimaActualizacion = DateTime.UtcNow;

        _db.MovimientosInventario.Add(new MovimientoInventario
        {
            ProductoId = productoId,
            TipoMovimiento = TipoMovimiento.Ajuste,
            Cantidad = Math.Abs(cantidad),
            StockAnterior = stockAnterior,
            StockResultante = inventario.StockActual,
            ReferenciaTabla = "Ajuste",
            ReferenciaId = 0,
            UsuarioId = usuarioId,
            Fecha = DateTime.UtcNow,
            Observacion = observacion
        });

        await _db.SaveChangesAsync();
    }
}
