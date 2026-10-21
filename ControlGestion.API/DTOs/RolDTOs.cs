using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public record PermisoDTO(string Clave, string Modulo, string Nombre);

/// <summary>
/// Un rol con las claves de sus permisos. Protegido es el administrador: no se elimina ni se renombra, y conserva
/// los PermisosProtegidos (vacío en los demás roles).
/// </summary>
public record RolDTO(string Id, string Nombre, int Usuarios, bool Protegido, List<string> Permisos, List<string> PermisosProtegidos);

public record GuardarRolDTO
{
    [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
    [MaxLength(256, ErrorMessage = "El nombre del rol no puede superar los 256 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "Elige al menos un permiso.")]
    [MinLength(1, ErrorMessage = "Un rol tiene al menos un permiso.")]
    public List<string> Permisos { get; init; } = [];
}
