using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ControlGestion.API.Services;

/// <summary>
/// Las sesiones guardadas en el servidor (8.1). El token lleva el id de su sesión, y SesionDesdeLaBase la revisa en cada
/// petición: así cerrar sesión o cambiar la contraseña anula un token aunque alguien lo haya copiado.
/// </summary>
public class SesionService(
    AppDbContext db,
    UserManager<ApplicationUser> usuarios,
    IUsuarioActual actual,
    IOptions<JwtOptions> jwt,
    IOptions<SesionOptions> opciones,
    TimeProvider reloj)
{
    public async Task<LoginRespuestaDTO> IniciarAsync(LoginDTO dto)
    {
        var usuario = await usuarios.FindByEmailAsync(dto.Email);
        if (usuario == null || !await usuarios.CheckPasswordAsync(usuario, dto.Password))
            throw new NoAutenticadoException("credenciales_incorrectas", "Credenciales incorrectas.");

        if (!usuario.Activo)
            throw new NoAutenticadoException("usuario_inactivo", "Usuario inactivo.");

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var sesion = new Sesion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Origen = dto.Origen!.Value,
            Creada = ahora,
            UltimaActividad = ahora
        };
        db.Sesiones.Add(sesion);
        await db.SaveChangesAsync();

        var datos = await db.Users.Where(u => u.Id == usuario.Id).ComoSesion(db).FirstAsync();
        return new LoginRespuestaDTO(GenerarToken(usuario, sesion.Id, ahora), datos);
    }

    /// <summary>Cierra la sesión del token. Repetirlo no hace daño: solo cambia una sesión que sigue abierta.</summary>
    public Task CerrarAsync()
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var sesionId = actual.SesionId;
        return db.Sesiones
            .Where(s => s.Id == sesionId && s.CerradaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CerradaEn, ahora));
    }

    /// <summary>Cierra las demás sesiones abiertas del usuario, pero no la de este token: así lo pide cambiar la contraseña (8.1).</summary>
    public Task CerrarLasDemasAsync()
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var (usuarioId, sesionId) = (actual.Id, actual.SesionId);
        return db.Sesiones
            .Where(s => s.UsuarioId == usuarioId && s.Id != sesionId && s.CerradaEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CerradaEn, ahora));
    }

    // El token no lleva el rol ni los permisos: se leen de la base en cada petición (SesionDesdeLaBase), junto con la sesión.
    private string GenerarToken(ApplicationUser usuario, Guid sesionId, DateTime ahora)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id),
            new(ClaimsDeSeguridad.Sesion, sesionId.ToString()),
            new(ClaimTypes.Email, usuario.Email!),
            new(ClaimTypes.Name, usuario.NombreCompleto)
        };

        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Key));
        var token = new JwtSecurityToken(
            issuer: jwt.Value.Issuer,
            audience: jwt.Value.Audience,
            claims: claims,
            expires: ahora.AddDays(opciones.Value.DiasDelToken),
            signingCredentials: new SigningCredentials(clave, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
