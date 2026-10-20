using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController(ClienteService clientes) : ControllerBase
{
    // Sin operación en el catálogo: todos los roles consultan clientes, cada uno los de su cartera.
    [HttpGet]
    public async Task<ActionResult<Pagina<ClienteListaDTO>>> GetAll([FromQuery] FiltrosClienteDTO filtros) =>
        await clientes.ListarAsync(filtros);

    [HttpGet("opciones")]
    public async Task<ActionResult<List<ClienteOpcionDTO>>> GetOpciones([FromQuery] string? buscar) =>
        await clientes.OpcionesAsync(buscar);

    [HttpGet("mapa")]
    [Permiso(Permisos.Ventas.VerUbicacionCliente, Permisos.Territorial.VerMapaClientes)]
    public async Task<ActionResult<List<ClienteMapaDTO>>> GetParaMapa([FromQuery] int? zonaId) =>
        await clientes.MapaAsync(zonaId);

    [HttpGet("estados")]
    public async Task<ActionResult<List<EstadoClienteDTO>>> GetEstados() =>
        await clientes.EstadosAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClienteDTO>> GetById(int id) =>
        await clientes.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Ventas.RegistrarCliente)]
    public async Task<ActionResult<ClienteDTO>> Crear([FromBody] GuardarClienteDTO dto)
    {
        var creado = await clientes.CrearAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [Permiso(Permisos.Ventas.EditarCliente)]
    public async Task<ActionResult<ClienteDTO>> Editar(int id, [FromBody] GuardarClienteDTO dto) =>
        await clientes.EditarAsync(id, dto);

    // Sin operación propia en el catálogo: inactivar a un cliente es asignarle un estado a mano.
    [HttpDelete("{id:int}")]
    [Permiso(Permisos.Ventas.AsignarEstadoCliente)]
    public async Task<IActionResult> Desactivar(int id)
    {
        await clientes.DesactivarAsync(id);
        return NoContent();
    }
}
