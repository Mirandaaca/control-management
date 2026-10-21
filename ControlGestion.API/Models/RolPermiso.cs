using Microsoft.AspNetCore.Identity;

namespace ControlGestion.API.Models;

public class RolPermiso
{
    public string RolId { get; set; } = string.Empty;
    public IdentityRole? Rol { get; set; }

    public int PermisoId { get; set; }
    public Permiso? Permiso { get; set; }
}
