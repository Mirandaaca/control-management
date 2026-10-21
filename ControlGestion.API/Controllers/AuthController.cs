using ControlGestion.API.Configuracion;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PerfilService _perfil;
    private readonly JwtOptions _jwt;

    public AuthController(UserManager<ApplicationUser> userManager, PerfilService perfil, IOptions<JwtOptions> jwt)
    {
        _userManager = userManager;
        _perfil = perfil;
        _jwt = jwt.Value;
    }

    // Único endpoint sin sesión: los demás quedan protegidos por la FallbackPolicy de Program.cs.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginRespuestaDTO>> Login([FromBody] LoginDTO dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            throw new NoAutenticadoException("credenciales_incorrectas", "Credenciales incorrectas.");

        if (!user.Activo)
            throw new NoAutenticadoException("usuario_inactivo", "Usuario inactivo.");

        return new LoginRespuestaDTO(GenerarToken(user), await _perfil.ObtenerSesionAsync(user.Id));
    }

    // Con los permisos de la sesión, el panel arma el menú. Se lee de la base: un cambio de permisos se ve sin
    // volver a iniciar sesión.
    [HttpGet("yo")]
    public async Task<ActionResult<SesionDTO>> Yo() =>
        await _perfil.ObtenerMiSesionAsync();

    // El token no lleva el rol ni los permisos: se leen de la base en cada petición (SesionDesdeLaBase).
    private string GenerarToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.NombreCompleto)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
