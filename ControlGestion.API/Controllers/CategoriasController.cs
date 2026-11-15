using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

/// <summary>
/// Las categorías de los productos (7.3.29, tarea 29). Se administran con los permisos de productos: administrar las
/// categorías es parte de administrar los productos. Por eso cada escritura le dice a la bitácora qué operación es.
/// </summary>
[ApiController]
[Route("api/categorias")]
[Authorize]
public class CategoriasController(CategoriaService categorias) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Parametros.RegistrarProducto, Permisos.Parametros.ModificarProducto, Permisos.Parametros.EliminarProducto)]
    public async Task<ActionResult<List<CategoriaAdminDTO>>> GetAll() =>
        await categorias.ListarAsync();

    // El formulario del producto y los filtros de Productos e Inventario (quien abre Inventario, con sus permisos).
    [HttpGet("opciones")]
    [Permiso(Permisos.Parametros.RegistrarProducto, Permisos.Parametros.ModificarProducto, Permisos.Parametros.EliminarProducto,
        Permisos.Ventas.ConsultarProductos, Permisos.Ventas.ConsultarKardex, Permisos.Ventas.RegistrarIngreso)]
    public async Task<ActionResult<List<CategoriaResumenDTO>>> GetOpciones() =>
        await categorias.OpcionesAsync();

    [HttpGet("{id:int}")]
    [Permiso(Permisos.Parametros.RegistrarProducto, Permisos.Parametros.ModificarProducto, Permisos.Parametros.EliminarProducto)]
    public async Task<ActionResult<CategoriaAdminDTO>> GetById(int id) =>
        await categorias.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Parametros.RegistrarProducto)]
    [Bitacora("Registrar categorías")]
    public async Task<ActionResult<CategoriaAdminDTO>> Registrar([FromBody] GuardarCategoriaDTO dto)
    {
        var creada = await categorias.RegistrarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:int}")]
    [Permiso(Permisos.Parametros.ModificarProducto)]
    [Bitacora("Modificar categorías")]
    public async Task<ActionResult<CategoriaAdminDTO>> Modificar(int id, [FromBody] GuardarCategoriaDTO dto) =>
        await categorias.ModificarAsync(id, dto);

    [HttpDelete("{id:int}")]
    [Permiso(Permisos.Parametros.EliminarProducto)]
    [Bitacora("Eliminar categorías")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await categorias.EliminarAsync(id);
        return NoContent();
    }
}
