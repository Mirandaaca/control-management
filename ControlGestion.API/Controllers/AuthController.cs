using ControlGestion.API.DTOs;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(SesionService sesiones, PerfilService perfil) : ControllerBase
{
    // Único endpoint sin sesión: los demás quedan protegidos por la FallbackPolicy de Program.cs.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginRespuestaDTO>> Login([FromBody] LoginDTO dto) =>
        await sesiones.IniciarAsync(dto);

    // Con los permisos de la sesión, el panel arma el menú. Se lee de la base: un cambio de permisos se ve sin
    // volver a iniciar sesión.
    [HttpGet("yo")]
    public async Task<ActionResult<SesionDTO>> Yo() =>
        await perfil.ObtenerMiSesionAsync();

    // Cierra solo la sesión de este token: desde ahí no sirve, aunque alguien lo haya copiado. Es del propio usuario,
    // así que no pide un permiso del catálogo.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await sesiones.CerrarAsync();
        return NoContent();
    }
}
