using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Gestión de usuarios (8.2). Los usuarios no se borran: se desactivan.</summary>
public class UsuarioService(
    AppDbContext db,
    UserManager<ApplicationUser> usuarios,
    RoleManager<IdentityRole> roles,
    IUsuarioActual actual,
    TimeProvider reloj)
{
    public Task<List<UsuarioDTO>> ListarAsync() =>
        db.Users.OrderBy(u => u.NombreCompleto).ComoDTO(db).ToListAsync();

    public async Task<UsuarioDTO> ObtenerAsync(string id) =>
        await db.Users.Where(u => u.Id == id).ComoDTO(db).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el usuario.");

    /// <summary>Promotor es quien recorre rutas: así la lista no depende del nombre del rol.</summary>
    public Task<List<UsuarioResumenDTO>> ListarPromotoresAsync() =>
        db.Users
            .Where(u => u.Activo)
            .ConPermiso(db, Permisos.Territorial.RecorrerRuta)
            .OrderBy(u => u.NombreCompleto)
            .Select(u => new UsuarioResumenDTO(u.Id, u.NombreCompleto))
            .ToListAsync();

    public async Task<UsuarioDTO> CrearAsync(CrearUsuarioDTO dto)
    {
        var rol = await RolExistenteAsync(dto.Rol);
        if (await usuarios.FindByEmailAsync(dto.Email) != null)
            throw new ConflictoException("correo_duplicado", "Ya existe un usuario con ese correo.");

        var usuario = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            NombreCompleto = dto.NombreCompleto.Trim(),
            FechaCreacion = reloj.GetUtcNow().UtcDateTime
        };

        // Crear el usuario y asignarle el rol van juntos: si lo segundo falla, no queda un usuario sin rol.
        await using var transaccion = await db.Database.BeginTransactionAsync();
        Exigir(await usuarios.CreateAsync(usuario, dto.Password));
        Exigir(await usuarios.AddToRoleAsync(usuario, rol));
        await transaccion.CommitAsync();

        return await ObtenerAsync(usuario.Id);
    }

    public async Task<UsuarioDTO> EditarAsync(string id, EditarUsuarioDTO dto)
    {
        var usuario = await usuarios.FindByIdAsync(id) ?? throw new NoEncontradoException("No se encontró el usuario.");
        var rolNuevo = await RolExistenteAsync(dto.Rol);
        var rolesActuales = await usuarios.GetRolesAsync(usuario);
        var cambiaElRol = rolesActuales.Count != 1 || rolesActuales[0] != rolNuevo;

        // Así el administrador no se quita a sí mismo el acceso a esta pantalla.
        if (cambiaElRol && id == actual.Id)
            throw new ReglaDeNegocioException("no_puede_cambiar_su_rol", "No puedes cambiar tu propio rol.");

        var conEseCorreo = await usuarios.FindByEmailAsync(dto.Email);
        if (conEseCorreo != null && conEseCorreo.Id != id)
            throw new ConflictoException("correo_duplicado", "Ya existe un usuario con ese correo.");

        usuario.NombreCompleto = dto.NombreCompleto.Trim();
        // El correo es también el nombre de usuario con el que se inicia sesión.
        usuario.Email = dto.Email;
        usuario.UserName = dto.Email;

        await using var transaccion = await db.Database.BeginTransactionAsync();
        Exigir(await usuarios.UpdateAsync(usuario));
        if (cambiaElRol)
        {
            Exigir(await usuarios.RemoveFromRolesAsync(usuario, rolesActuales));
            Exigir(await usuarios.AddToRoleAsync(usuario, rolNuevo));
        }
        await transaccion.CommitAsync();

        return await ObtenerAsync(id);
    }

    public Task<UsuarioDTO> ActivarAsync(string id) => CambiarActivoAsync(id, true);

    public Task<UsuarioDTO> DesactivarAsync(string id)
    {
        if (id == actual.Id)
            throw new ReglaDeNegocioException("no_puede_desactivarse", "No puedes desactivar tu propio usuario.");
        return CambiarActivoAsync(id, false);
    }

    private async Task<UsuarioDTO> CambiarActivoAsync(string id, bool activo)
    {
        var usuario = await usuarios.FindByIdAsync(id) ?? throw new NoEncontradoException("No se encontró el usuario.");
        usuario.Activo = activo;
        Exigir(await usuarios.UpdateAsync(usuario));
        return await ObtenerAsync(id);
    }

    private async Task<string> RolExistenteAsync(string nombre) =>
        (await roles.FindByNameAsync(nombre.Trim()))?.Name
        ?? throw new ReglaDeNegocioException("rol_no_existe", $"El rol \"{nombre.Trim()}\" no existe.");

    private static void Exigir(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
            throw new ReglaDeNegocioException("usuario_invalido", string.Join(" ", resultado.Errors.Select(e => e.Description)));
    }
}
