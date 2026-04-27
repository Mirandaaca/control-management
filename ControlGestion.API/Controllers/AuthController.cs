using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IConfiguration _config;

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { mensaje = "Credenciales incorrectas." });

        if (!user.Activo)
            return Unauthorized(new { mensaje = "Usuario inactivo." });

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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            NombreCompleto = dto.NombreCompleto
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        var rolesValidos = new[] { "Admin", "Supervisor", "Promotor" };
        var rol = rolesValidos.Contains(dto.Rol) ? dto.Rol : "Promotor";
        await _userManager.AddToRoleAsync(user, rol);

        return Ok(new { mensaje = "Usuario creado correctamente." });
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

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
