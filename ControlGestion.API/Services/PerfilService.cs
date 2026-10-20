using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Lo que cada usuario hace sobre sí mismo, sea cual sea su rol: su sesión, su perfil (8.3) y su contraseña (8.1).</summary>
public class PerfilService(AppDbContext db, UserManager<ApplicationUser> usuarios, IUsuarioActual actual)
{
    public async Task<SesionDTO> ObtenerSesionAsync(string usuarioId) =>
        await db.Users.Where(u => u.Id == usuarioId).ComoSesion(db).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el usuario.");

    public Task<SesionDTO> ObtenerMiSesionAsync() => ObtenerSesionAsync(actual.Id);

    public async Task<PerfilDTO> ObtenerAsync() =>
        await db.Users.Where(u => u.Id == actual.Id).ComoPerfil(db).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el usuario.");

    public async Task<PerfilDTO> EditarAsync(EditarPerfilDTO dto)
    {
        var usuario = await MiUsuarioAsync();
        var conEseCorreo = await usuarios.FindByEmailAsync(dto.Email);
        if (conEseCorreo != null && conEseCorreo.Id != usuario.Id)
            throw new ConflictoException("correo_duplicado", "Ya existe un usuario con ese correo.");

        usuario.NombreCompleto = dto.NombreCompleto.Trim();
        // El correo es también el nombre de usuario con el que se inicia sesión.
        usuario.Email = dto.Email;
        usuario.UserName = dto.Email;
        var resultado = await usuarios.UpdateAsync(usuario);
        if (!resultado.Succeeded)
            throw new ReglaDeNegocioException("perfil_invalido", string.Join(" ", resultado.Errors.Select(e => e.Description)));

        return await ObtenerAsync();
    }

    public async Task CambiarContrasenaAsync(CambiarContrasenaDTO dto)
    {
        var usuario = await MiUsuarioAsync();
        var resultado = await usuarios.ChangePasswordAsync(usuario, dto.Actual, dto.Nueva);
        if (resultado.Succeeded) return;

        // Identity marca con este código que la contraseña actual no coincide; el resto son reglas de la nueva.
        if (resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
            throw new ReglaDeNegocioException("contrasena_actual_incorrecta", "La contraseña actual no es correcta.");
        throw new ReglaDeNegocioException("contrasena_invalida", string.Join(" ", resultado.Errors.Select(e => e.Description)));
    }

    private async Task<ApplicationUser> MiUsuarioAsync() =>
        await usuarios.FindByIdAsync(actual.Id) ?? throw new NoEncontradoException("No se encontró el usuario.");
}
