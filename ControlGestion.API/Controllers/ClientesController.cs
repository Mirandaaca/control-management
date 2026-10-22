using System.ComponentModel.DataAnnotations;
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

    // Con más de 2.000 clientes, el selector con búsqueda del panel pide los primeros 50 que coinciden con lo que se escribe.
    [HttpGet("opciones")]
    public async Task<ActionResult<List<ClienteOpcionDTO>>> GetOpciones([FromQuery] string? buscar,
        [FromQuery, Range(1, 100, ErrorMessage = "El límite va de 1 a 100.")] int? limite) =>
        await clientes.OpcionesAsync(buscar, limite);

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

    // Los clientes no se borran: desactivar uno es asignarle el estado Inactivo.
    [HttpPut("{id:int}/estado")]
    [Permiso(Permisos.Ventas.AsignarEstadoCliente)]
    public async Task<ActionResult<ClienteDTO>> AsignarEstado(int id, [FromBody] AsignarEstadoClienteDTO dto) =>
        await clientes.AsignarEstadoAsync(id, dto);
}
