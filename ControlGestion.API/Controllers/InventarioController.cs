using ControlGestion.API.Data;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventarioController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventarioService _inventario;

    public InventarioController(AppDbContext db, InventarioService inventario)
    {
        _db = db;
        _inventario = inventario;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _db.InventariosProducto
            .Include(i => i.Producto)
            .OrderBy(i => i.Producto!.Descripcion)
            .Select(i => new
            {
                i.Id,
                i.ProductoId,
                Producto = new { i.Producto!.SKU, i.Producto.Descripcion, i.Producto.Categoria },
                i.StockActual,
                i.StockMinimo,
                i.StockMaximo,
                i.UltimaActualizacion,
                BajoMinimo = i.StockActual <= i.StockMinimo
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("{productoId}/movimientos")]
    public async Task<IActionResult> GetMovimientos(int productoId, [FromQuery] int limit = 50)
    {
        var movimientos = await _db.MovimientosInventario
            .Include(m => m.Usuario)
            .Where(m => m.ProductoId == productoId)
            .OrderByDescending(m => m.Fecha)
            .Take(limit)
            .ToListAsync();

        return Ok(movimientos);
    }

    [HttpPost("{productoId}/ajuste")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> Ajustar(int productoId, [FromBody] AjusteStockDTO dto)
    {
        if (!await _db.Productos.AnyAsync(p => p.Id == productoId))
            return NotFound(new { mensaje = "Producto no encontrado." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _inventario.AjustarStock(productoId, dto.Cantidad, userId, dto.Observacion);
        return Ok(new { mensaje = "Stock ajustado correctamente." });
    }
}

public record AjusteStockDTO(decimal Cantidad, string Observacion);
