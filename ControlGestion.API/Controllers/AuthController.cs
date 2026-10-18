using ControlGestion.API.Configuracion;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
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
    private readonly JwtOptions _jwt;

    public AuthController(UserManager<ApplicationUser> userManager, IOptions<JwtOptions> jwt)
    {
        _userManager = userManager;
        _jwt = jwt.Value;
    }

    // Único endpoint sin sesión: los demás quedan protegidos por la FallbackPolicy de Program.cs.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            throw new NoAutenticadoException("credenciales_incorrectas", "Credenciales incorrectas.");

        if (!user.Activo)
            throw new NoAutenticadoException("usuario_inactivo", "Usuario inactivo.");

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerarToken(user, roles);

        return Ok(new LoginResponseDTO
        {
            Token = token,
            Email = user.Email!,
            NombreCompleto = user.NombreCompleto,
            Roles = roles
        });
    }

    private string GenerarToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.NombreCompleto)
        };

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

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
