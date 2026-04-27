using ControlGestion.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsuariosController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UsuariosController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userManager.Users.ToListAsync();
        var result = new List<object>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            result.Add(new { u.Id, u.Email, u.NombreCompleto, u.Activo, u.FechaCreacion, Roles = roles });
        }
        return Ok(result);
    }

    [HttpGet("activos")]
    [Authorize]
    public async Task<IActionResult> GetActivos()
    {
        var users = await _userManager.Users.Where(u => u.Activo).OrderBy(u => u.NombreCompleto).ToListAsync();
        var result = new List<object>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            result.Add(new { u.Id, u.NombreCompleto, Roles = roles });
        }
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new { user.Id, user.Email, user.NombreCompleto, user.Activo, user.FechaCreacion, Roles = roles });
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> ToggleActivo(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        user.Activo = !user.Activo;
        await _userManager.UpdateAsync(user);
        return Ok(new { user.Id, user.Activo });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        await _userManager.DeleteAsync(user);
        return NoContent();
    }
}
