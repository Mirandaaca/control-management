using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public UsuariosController(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    [HttpPost]
    [Permiso(Permisos.Seguridad.CrearUsuario)]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioDTO dto)
    {
        if (await _userManager.FindByEmailAsync(dto.Email) != null)
            throw new ConflictoException("correo_duplicado", "Ya existe un usuario con ese correo.");

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            NombreCompleto = dto.NombreCompleto.Trim()
        };

        // Crear el usuario y asignarle el rol van juntos: si lo segundo falla, no queda un usuario sin rol.
        await using var transaccion = await _db.Database.BeginTransactionAsync();

        var resultado = await _userManager.CreateAsync(user, dto.Password);
        if (!resultado.Succeeded)
            throw new ReglaDeNegocioException("usuario_invalido", string.Join(" ", resultado.Errors.Select(e => e.Description)));

        var resultadoRol = await _userManager.AddToRoleAsync(user, dto.Rol);
        if (!resultadoRol.Succeeded)
            throw new InvalidOperationException($"No se pudo asignar el rol {dto.Rol} al usuario nuevo.");

        await transaccion.CommitAsync();

        var creado = new UsuarioDTO(user.Id, user.Email!, user.NombreCompleto, user.Activo, new List<string> { dto.Rol });
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, creado);
    }

    [HttpGet]
    [Permiso(Permisos.Seguridad.CrearUsuario, Permisos.Seguridad.EditarUsuario, Permisos.Seguridad.ActivarUsuario)]
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

    // Para elegir un promotor al asignar pedidos, zonas o rutas.
    [HttpGet("activos")]
    [Permiso(Permisos.Pedidos.AsignarPedidos, Permisos.Parametros.RegistrarZona, Permisos.Parametros.ModificarZona, Permisos.Territorial.PlanificarRuta)]
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
    [Permiso(Permisos.Seguridad.CrearUsuario, Permisos.Seguridad.EditarUsuario, Permisos.Seguridad.ActivarUsuario)]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) throw new NoEncontradoException("No se encontró el usuario.");
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new { user.Id, user.Email, user.NombreCompleto, user.Activo, user.FechaCreacion, Roles = roles });
    }

    [HttpPut("{id}/activar")]
    [Permiso(Permisos.Seguridad.ActivarUsuario)]
    public async Task<IActionResult> ToggleActivo(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) throw new NoEncontradoException("No se encontró el usuario.");
        user.Activo = !user.Activo;
        await _userManager.UpdateAsync(user);
        return Ok(new { user.Id, user.Activo });
    }

    [HttpDelete("{id}")]
    [Permiso(Permisos.Seguridad.ActivarUsuario)]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) throw new NoEncontradoException("No se encontró el usuario.");
        await _userManager.DeleteAsync(user);
        return NoContent();
    }
}
