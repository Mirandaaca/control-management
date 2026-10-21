using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public record CrearUsuarioDTO
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [MaxLength(256, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre completo no puede superar los 150 caracteres.")]
    public string NombreCompleto { get; init; } = string.Empty;

    // Mismas reglas que Identity en Program.cs: al menos 8 caracteres y un número (8.1).
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [RegularExpression(@".*\d.*", ErrorMessage = "La contraseña debe tener al menos un número.")]
    public string Password { get; init; } = string.Empty;

    // Los roles son datos: el servicio comprueba que exista.
    [Required(ErrorMessage = "El rol es obligatorio.")]
    [MaxLength(256, ErrorMessage = "El rol no puede superar los 256 caracteres.")]
    public string Rol { get; init; } = string.Empty;
}

public record EditarUsuarioDTO
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [MaxLength(256, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre completo no puede superar los 150 caracteres.")]
    public string NombreCompleto { get; init; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    [MaxLength(256, ErrorMessage = "El rol no puede superar los 256 caracteres.")]
    public string Rol { get; init; } = string.Empty;
}

/// <summary>Un usuario tiene un solo rol (un rol por actor); Rol es null solo si alguien lo quitó a mano en la base.</summary>
public record UsuarioDTO(string Id, string Email, string NombreCompleto, string? Rol, bool Activo, DateTime FechaCreacion);
