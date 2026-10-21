using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController(UsuarioService usuarios) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Seguridad.CrearUsuario, Permisos.Seguridad.EditarUsuario, Permisos.Seguridad.ActivarUsuario)]
    public async Task<ActionResult<List<UsuarioDTO>>> GetAll() =>
        await usuarios.ListarAsync();

    // Para elegir un promotor al asignar pedidos, zonas o rutas: solo el id y el nombre.
    [HttpGet("promotores")]
    [Permiso(Permisos.Pedidos.AsignarPedidos, Permisos.Parametros.RegistrarZona, Permisos.Parametros.ModificarZona,
        Permisos.Territorial.PlanificarRuta)]
    public async Task<ActionResult<List<UsuarioResumenDTO>>> GetPromotores() =>
        await usuarios.ListarPromotoresAsync();

    [HttpGet("{id}")]
    [Permiso(Permisos.Seguridad.CrearUsuario, Permisos.Seguridad.EditarUsuario, Permisos.Seguridad.ActivarUsuario)]
    public async Task<ActionResult<UsuarioDTO>> GetById(string id) =>
        await usuarios.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Seguridad.CrearUsuario)]
    public async Task<ActionResult<UsuarioDTO>> Crear([FromBody] CrearUsuarioDTO dto)
    {
        var creado = await usuarios.CrearAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id}")]
    [Permiso(Permisos.Seguridad.EditarUsuario)]
    public async Task<ActionResult<UsuarioDTO>> Editar(string id, [FromBody] EditarUsuarioDTO dto) =>
        await usuarios.EditarAsync(id, dto);

    // Los usuarios no se borran (8.2): se desactivan, y conservan sus ventas, cobros y su rastro en la bitácora.
    [HttpPut("{id}/activar")]
    [Permiso(Permisos.Seguridad.ActivarUsuario)]
    public async Task<ActionResult<UsuarioDTO>> Activar(string id) =>
        await usuarios.ActivarAsync(id);

    [HttpPut("{id}/desactivar")]
    [Permiso(Permisos.Seguridad.ActivarUsuario)]
    public async Task<ActionResult<UsuarioDTO>> Desactivar(string id) =>
        await usuarios.DesactivarAsync(id);
}
