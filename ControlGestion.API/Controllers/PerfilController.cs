using ControlGestion.API.DTOs;
using ControlGestion.API.Services;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

// El perfil propio y la contraseña no son permisos: los tiene cualquier usuario con sesión (8.1 y 8.3).
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerfilController(PerfilService perfil) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PerfilDTO>> Get() =>
        await perfil.ObtenerAsync();

    [HttpPut]
    [Bitacora("Editar su perfil")]
    public async Task<ActionResult<PerfilDTO>> Editar([FromBody] EditarPerfilDTO dto) =>
        await perfil.EditarAsync(dto);

    [HttpPut("contrasena")]
    [Bitacora("Cambiar su contraseña")]
    public async Task<IActionResult> CambiarContrasena([FromBody] CambiarContrasenaDTO dto)
    {
        await perfil.CambiarContrasenaAsync(dto);
        return NoContent();
    }
}
