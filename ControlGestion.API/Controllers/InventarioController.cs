using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventarioController(InventarioService inventario) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Ventas.ConsultarProductos, Permisos.Ventas.ConsultarKardex, Permisos.Ventas.RegistrarIngreso)]
    public async Task<ActionResult<Pagina<InventarioDTO>>> GetAll([FromQuery] FiltrosInventarioDTO filtros) =>
        await inventario.ListarAsync(filtros);

    [HttpGet("{productoId:int}/movimientos")]
    [Permiso(Permisos.Ventas.ConsultarKardex)]
    public async Task<ActionResult<Pagina<MovimientoDTO>>> GetMovimientos(int productoId, [FromQuery] FiltrosKardexDTO filtros) =>
        await inventario.KardexAsync(productoId, filtros);

    [HttpPost("{productoId:int}/ajuste")]
    [Permiso(Permisos.Ventas.RegistrarIngreso)]
    public async Task<ActionResult<InventarioDTO>> Ajustar(int productoId, [FromBody] AjusteStockDTO dto) =>
        await inventario.AjustarStockAsync(productoId, dto);
}
