using ControlGestion.API.Data;
using ControlGestion.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Seguridad;

/// <summary>
/// Se ejecuta al arrancar, después de las migraciones. Deja la base de acuerdo con el código: los cuatro roles del
/// sistema y el catálogo de Permisos.cs.
/// </summary>
public static class SembradorDeSeguridad
{
    public static async Task SembrarAsync(AppDbContext db, RoleManager<IdentityRole> roles, ILogger logger)
    {
        foreach (var nombre in Roles.DelSistema)
        {
            if (!await roles.RoleExistsAsync(nombre))
                await roles.CreateAsync(new IdentityRole(nombre));
        }
        var idDeRol = await db.Roles.ToDictionaryAsync(r => r.Name!, r => r.Id);

        // Solo un permiso nuevo recibe su asignación inicial: así no se pisa lo que el administrador cambió después.
        var existentes = await db.Permisos.ToDictionaryAsync(p => p.Clave);
        var nuevos = 0;
        foreach (var definicion in Permisos.Catalogo)
        {
            if (existentes.TryGetValue(definicion.Clave, out var permiso))
            {
                permiso.Modulo = definicion.Modulo;
                permiso.Nombre = definicion.Nombre;
                continue;
            }

            permiso = new Permiso { Clave = definicion.Clave, Modulo = definicion.Modulo, Nombre = definicion.Nombre };
            db.Permisos.Add(permiso);
            foreach (var rol in definicion.RolesIniciales)
                db.RolesPermisos.Add(new RolPermiso { RolId = idDeRol[rol], Permiso = permiso });
            nuevos++;
        }

        // Un permiso que salió del código se borra; sus asignaciones se van con él (borrado en cascada).
        var delCatalogo = Permisos.Catalogo.Select(d => d.Clave).ToHashSet();
        var sobrantes = existentes.Values.Where(p => !delCatalogo.Contains(p.Clave)).ToList();
        db.Permisos.RemoveRange(sobrantes);
        await db.SaveChangesAsync();

        // El administrador nunca se queda sin poder administrar usuarios y roles, aunque alguien edite la base a mano.
        var administradorId = idDeRol[Roles.Administrador];
        var faltantes = await db.Permisos
            .Where(p => Permisos.ProtegidosDelAdministrador.Contains(p.Clave))
            .Where(p => !db.RolesPermisos.Any(rp => rp.RolId == administradorId && rp.PermisoId == p.Id))
            .Select(p => p.Id)
            .ToListAsync();
        db.RolesPermisos.AddRange(faltantes.Select(id => new RolPermiso { RolId = administradorId, PermisoId = id }));
        await db.SaveChangesAsync();

        if (nuevos > 0 || sobrantes.Count > 0 || faltantes.Count > 0)
            logger.LogInformation("Siembra de permisos: {Nuevos} nuevos, {Borrados} borrados y {Repuestos} repuestos al administrador.",
                nuevos, sobrantes.Count, faltantes.Count);
    }
}
