using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

/// <summary>
/// Puntos de referencia (5.5). Modificar y eliminar no tienen operación propia en el catálogo: piden la más cercana,
/// territorial.registrar-punto-referencia.
/// </summary>
[ApiController]
[Route("api/puntos-referencia")]
[Authorize]
public class PuntosReferenciaController(PuntoReferenciaService puntos) : ControllerBase
{
    // Sin permiso propio: los mapas de clientes (5.1) y de la app los muestran a todos.
    [HttpGet]
    public async Task<ActionResult<List<PuntoReferenciaDTO>>> GetAll([FromQuery] int? zonaId) =>
        await puntos.ListarAsync(zonaId);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PuntoReferenciaDTO>> GetById(int id) =>
        await puntos.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Territorial.RegistrarPuntoReferencia)]
    public async Task<ActionResult<PuntoReferenciaDTO>> Registrar([FromBody] GuardarPuntoReferenciaDTO dto)
    {
        var creado = await puntos.RegistrarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [Permiso(Permisos.Territorial.RegistrarPuntoReferencia)]
    public async Task<ActionResult<PuntoReferenciaDTO>> Modificar(int id, [FromBody] GuardarPuntoReferenciaDTO dto) =>
        await puntos.ModificarAsync(id, dto);

    [HttpDelete("{id:int}")]
    [Permiso(Permisos.Territorial.RegistrarPuntoReferencia)]
    public async Task<IActionResult> Eliminar(int id)
    {
        await puntos.EliminarAsync(id);
        return NoContent();
    }
}
