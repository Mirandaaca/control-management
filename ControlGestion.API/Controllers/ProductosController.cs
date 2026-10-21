using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductosController(ProductoService productos) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Parametros.RegistrarProducto, Permisos.Parametros.ModificarProducto, Permisos.Parametros.EliminarProducto)]
    public async Task<ActionResult<Pagina<ProductoDTO>>> GetAll([FromQuery] FiltrosProductoDTO filtros) =>
        await productos.ListarAsync(filtros);

    // Quien vende elige productos sin ver el precio de compra.
    [HttpGet("opciones")]
    [Permiso(Permisos.Ventas.ConsultarProductos, Permisos.Ventas.RegistrarHp, Permisos.Pedidos.RegistrarPedido)]
    public async Task<ActionResult<List<ProductoOpcionDTO>>> GetOpciones([FromQuery] string? buscar) =>
        await productos.OpcionesAsync(buscar);

    [HttpGet("{id:int}")]
    [Permiso(Permisos.Parametros.RegistrarProducto, Permisos.Parametros.ModificarProducto, Permisos.Parametros.EliminarProducto)]
    public async Task<ActionResult<ProductoDTO>> GetById(int id) =>
        await productos.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Parametros.RegistrarProducto)]
    public async Task<ActionResult<ProductoDTO>> Crear([FromBody] GuardarProductoDTO dto)
    {
        var creado = await productos.CrearAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [Permiso(Permisos.Parametros.ModificarProducto)]
    public async Task<ActionResult<ProductoDTO>> Editar(int id, [FromBody] GuardarProductoDTO dto) =>
        await productos.EditarAsync(id, dto);

    // Eliminar deja el producto inactivo: las HP y el kardex siguen apuntando a él.
    [HttpDelete("{id:int}")]
    [Permiso(Permisos.Parametros.EliminarProducto)]
    public async Task<IActionResult> Desactivar(int id)
    {
        await productos.DesactivarAsync(id);
        return NoContent();
    }
}
