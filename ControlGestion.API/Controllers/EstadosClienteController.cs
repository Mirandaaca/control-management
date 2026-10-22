using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

/// <summary>La administración de los estados del cliente (7.4). Los formularios leen los estados de GET /clientes/estados.</summary>
[ApiController]
[Route("api/estados-cliente")]
[Authorize]
public class EstadosClienteController(EstadoClienteService estados) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Parametros.RegistrarEstadoCliente, Permisos.Parametros.ModificarEstadoCliente, Permisos.Parametros.EliminarEstadoCliente)]
    public async Task<ActionResult<List<EstadoClienteAdminDTO>>> GetAll() =>
        await estados.ListarAsync();

    [HttpGet("{id:int}")]
    [Permiso(Permisos.Parametros.RegistrarEstadoCliente, Permisos.Parametros.ModificarEstadoCliente, Permisos.Parametros.EliminarEstadoCliente)]
    public async Task<ActionResult<EstadoClienteAdminDTO>> GetById(int id) =>
        await estados.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Parametros.RegistrarEstadoCliente)]
    public async Task<ActionResult<EstadoClienteAdminDTO>> Registrar([FromBody] GuardarEstadoClienteDTO dto)
    {
        var creado = await estados.RegistrarAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [Permiso(Permisos.Parametros.ModificarEstadoCliente)]
    public async Task<ActionResult<EstadoClienteAdminDTO>> Modificar(int id, [FromBody] GuardarEstadoClienteDTO dto) =>
        await estados.ModificarAsync(id, dto);

    [HttpDelete("{id:int}")]
    [Permiso(Permisos.Parametros.EliminarEstadoCliente)]
    public async Task<IActionResult> Eliminar(int id)
    {
        await estados.EliminarAsync(id);
        return NoContent();
    }
}
