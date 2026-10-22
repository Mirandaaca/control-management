using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ZonasController(ZonaService zonas) : ControllerBase
{
    // Sin permiso propio: los formularios de todos los roles eligen una zona.
    [HttpGet]
    public async Task<ActionResult<List<ZonaDTO>>> GetAll() =>
        await zonas.ListarAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ZonaDTO>> GetById(int id) =>
        await zonas.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Parametros.RegistrarZona)]
    public async Task<ActionResult<ZonaDTO>> Registrar([FromBody] RegistrarZonaDTO dto)
    {
        var creada = await zonas.RegistrarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:int}")]
    [Permiso(Permisos.Parametros.ModificarZona)]
    public async Task<ActionResult<ZonaDTO>> Modificar(int id, [FromBody] ModificarZonaDTO dto) =>
        await zonas.ModificarAsync(id, dto);

    [HttpDelete("{id:int}")]
    [Permiso(Permisos.Parametros.EliminarZona)]
    public async Task<IActionResult> Eliminar(int id)
    {
        await zonas.EliminarAsync(id);
        return NoContent();
    }
}
