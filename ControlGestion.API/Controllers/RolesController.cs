using ControlGestion.API.DTOs;
using ControlGestion.API.Seguridad;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController(RolService roles) : ControllerBase
{
    // También la usa la pantalla de usuarios, para elegir el rol de cada uno.
    [HttpGet]
    [Permiso(Permisos.Seguridad.CrearRol, Permisos.Seguridad.EditarRol, Permisos.Seguridad.EliminarRol,
        Permisos.Seguridad.CrearUsuario, Permisos.Seguridad.EditarUsuario)]
    public async Task<ActionResult<List<RolDTO>>> GetAll() =>
        await roles.ListarAsync();

    [HttpGet("permisos")]
    [Permiso(Permisos.Seguridad.CrearRol, Permisos.Seguridad.EditarRol)]
    public async Task<ActionResult<List<PermisoDTO>>> GetCatalogo() =>
        await roles.CatalogoAsync();

    [HttpGet("{id}")]
    [Permiso(Permisos.Seguridad.CrearRol, Permisos.Seguridad.EditarRol, Permisos.Seguridad.EliminarRol)]
    public async Task<ActionResult<RolDTO>> GetById(string id) =>
        await roles.ObtenerAsync(id);

    [HttpPost]
    [Permiso(Permisos.Seguridad.CrearRol)]
    public async Task<ActionResult<RolDTO>> Crear([FromBody] GuardarRolDTO dto)
    {
        var creado = await roles.CrearAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id}")]
    [Permiso(Permisos.Seguridad.EditarRol)]
    public async Task<ActionResult<RolDTO>> Editar(string id, [FromBody] GuardarRolDTO dto) =>
        await roles.EditarAsync(id, dto);

    [HttpDelete("{id}")]
    [Permiso(Permisos.Seguridad.EliminarRol)]
    public async Task<IActionResult> Eliminar(string id)
    {
        await roles.EliminarAsync(id);
        return NoContent();
    }
}
