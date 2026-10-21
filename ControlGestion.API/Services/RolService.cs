using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>
/// Roles y sus permisos (8.4). Tres protecciones evitan que el sistema quede sin nadie que lo administre: el rol
/// administrador no se elimina y conserva los permisos de usuarios y roles, un rol tiene al menos un permiso y no se
/// elimina un rol que algún usuario tiene.
/// </summary>
public class RolService(AppDbContext db, RoleManager<IdentityRole> roles)
{
    public async Task<List<RolDTO>> ListarAsync()
    {
        var filas = await db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                Nombre = r.Name!,
                Usuarios = db.UserRoles.Count(ur => ur.RoleId == r.Id),
                Permisos = db.RolesPermisos.Where(rp => rp.RolId == r.Id).Select(rp => rp.Permiso!.Clave).OrderBy(c => c).ToList()
            })
            .ToListAsync();

        return filas.Select(r => ComoDTO(r.Id, r.Nombre, r.Usuarios, r.Permisos)).ToList();
    }

    public async Task<RolDTO> ObtenerAsync(string id) =>
        (await ListarAsync()).FirstOrDefault(r => r.Id == id) ?? throw new NoEncontradoException("No se encontró el rol.");

    /// <summary>El catálogo, en el orden de docs/permisos-por-rol.md: el panel lo agrupa por módulo.</summary>
    public Task<List<PermisoDTO>> CatalogoAsync() =>
        db.Permisos.OrderBy(p => p.Id).Select(p => new PermisoDTO(p.Clave, p.Modulo, p.Nombre)).ToListAsync();

    public async Task<RolDTO> CrearAsync(GuardarRolDTO dto)
    {
        var nombre = dto.Nombre.Trim();
        if (await roles.RoleExistsAsync(nombre))
            throw new ConflictoException("rol_duplicado", "Ya existe un rol con ese nombre.");
        var permisoIds = await IdsDePermisosAsync(dto.Permisos);

        var rol = new IdentityRole(nombre);
        await using var transaccion = await db.Database.BeginTransactionAsync();
        Exigir(await roles.CreateAsync(rol));
        db.RolesPermisos.AddRange(permisoIds.Select(id => new RolPermiso { RolId = rol.Id, PermisoId = id }));
        await db.SaveChangesAsync();
        await transaccion.CommitAsync();

        return await ObtenerAsync(rol.Id);
    }

    public async Task<RolDTO> EditarAsync(string id, GuardarRolDTO dto)
    {
        var rol = await roles.FindByIdAsync(id) ?? throw new NoEncontradoException("No se encontró el rol.");
        var nombre = dto.Nombre.Trim();
        var esAdministrador = rol.Name == Roles.Administrador;

        if (esAdministrador && nombre != Roles.Administrador)
            throw new ReglaDeNegocioException("rol_protegido", "El rol Administrador del sistema no se renombra.");
        if (esAdministrador && Permisos.ProtegidosDelAdministrador.Except(dto.Permisos).Any())
            throw new ReglaDeNegocioException("permisos_protegidos",
                "El rol Administrador del sistema conserva siempre los permisos para administrar usuarios y roles.");
        if (nombre != rol.Name && await roles.RoleExistsAsync(nombre))
            throw new ConflictoException("rol_duplicado", "Ya existe un rol con ese nombre.");
        var permisoIds = await IdsDePermisosAsync(dto.Permisos);

        await using var transaccion = await db.Database.BeginTransactionAsync();
        if (nombre != rol.Name)
        {
            rol.Name = nombre;
            Exigir(await roles.UpdateAsync(rol));
        }
        await db.RolesPermisos.Where(rp => rp.RolId == id).ExecuteDeleteAsync();
        db.RolesPermisos.AddRange(permisoIds.Select(permisoId => new RolPermiso { RolId = id, PermisoId = permisoId }));
        await db.SaveChangesAsync();
        await transaccion.CommitAsync();

        return await ObtenerAsync(id);
    }

    public async Task EliminarAsync(string id)
    {
        var rol = await roles.FindByIdAsync(id) ?? throw new NoEncontradoException("No se encontró el rol.");
        if (rol.Name == Roles.Administrador)
            throw new ReglaDeNegocioException("rol_protegido", "El rol Administrador del sistema no se elimina.");
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id))
            throw new ConflictoException("rol_con_usuarios",
                "No se puede eliminar un rol que tiene usuarios. Primero asígnales otro rol.");

        // Sus permisos se borran con él (RolesPermisos borra en cascada).
        Exigir(await roles.DeleteAsync(rol));
    }

    private async Task<List<int>> IdsDePermisosAsync(IEnumerable<string> claves)
    {
        var pedidas = claves.Select(c => c.Trim()).Distinct().ToList();
        var encontrados = await db.Permisos.Where(p => pedidas.Contains(p.Clave)).Select(p => new { p.Id, p.Clave }).ToListAsync();
        var faltantes = pedidas.Except(encontrados.Select(p => p.Clave)).ToList();
        if (faltantes.Count > 0)
            throw new ReglaDeNegocioException("permiso_no_existe", $"No existe el permiso {string.Join(", ", faltantes)}.");
        return encontrados.Select(p => p.Id).ToList();
    }

    private static RolDTO ComoDTO(string id, string nombre, int usuarios, List<string> permisos)
    {
        var protegido = nombre == Roles.Administrador;
        return new RolDTO(id, nombre, usuarios, protegido, permisos,
            protegido ? [.. Permisos.ProtegidosDelAdministrador] : []);
    }

    private static void Exigir(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
            throw new ReglaDeNegocioException("rol_invalido", string.Join(" ", resultado.Errors.Select(e => e.Description)));
    }
}
