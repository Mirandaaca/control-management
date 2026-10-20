using ControlGestion.API.DTOs;
using ControlGestion.API.Models;

namespace ControlGestion.API.Data.Consultas;

public static class UsuarioConsultas
{
    // El rol sale en la misma consulta: antes se pedía con GetRolesAsync, una consulta más por cada usuario.
    public static IQueryable<UsuarioDTO> ComoDTO(this IQueryable<ApplicationUser> usuarios, AppDbContext db) =>
        usuarios.Select(u => new UsuarioDTO(
            u.Id,
            u.Email!,
            u.NombreCompleto,
            db.UserRoles.Where(ur => ur.UserId == u.Id)
                .Select(ur => db.Roles.Where(r => r.Id == ur.RoleId).Select(r => r.Name).FirstOrDefault())
                .FirstOrDefault(),
            u.Activo,
            u.FechaCreacion));

    public static IQueryable<SesionDTO> ComoSesion(this IQueryable<ApplicationUser> usuarios, AppDbContext db) =>
        usuarios.Select(u => new SesionDTO(
            u.Id,
            u.Email!,
            u.NombreCompleto,
            db.UserRoles.Where(ur => ur.UserId == u.Id)
                .Select(ur => db.Roles.Where(r => r.Id == ur.RoleId).Select(r => r.Name).FirstOrDefault())
                .FirstOrDefault(),
            db.UserRoles.Where(ur => ur.UserId == u.Id)
                .SelectMany(ur => db.RolesPermisos.Where(rp => rp.RolId == ur.RoleId).Select(rp => rp.Permiso!.Clave))
                .OrderBy(clave => clave)
                .ToList()));

    public static IQueryable<PerfilDTO> ComoPerfil(this IQueryable<ApplicationUser> usuarios, AppDbContext db) =>
        usuarios.Select(u => new PerfilDTO(
            u.Id,
            u.Email!,
            u.NombreCompleto,
            db.UserRoles.Where(ur => ur.UserId == u.Id)
                .Select(ur => db.Roles.Where(r => r.Id == ur.RoleId).Select(r => r.Name).FirstOrDefault())
                .FirstOrDefault(),
            u.FechaCreacion));

    /// <summary>Usuarios cuyo rol tiene el permiso, sea cual sea el nombre del rol.</summary>
    public static IQueryable<ApplicationUser> ConPermiso(this IQueryable<ApplicationUser> usuarios, AppDbContext db, string clave) =>
        usuarios.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id
            && db.RolesPermisos.Any(rp => rp.RolId == ur.RoleId && rp.Permiso!.Clave == clave)));
}
