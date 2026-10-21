using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

/// <summary>Quién inició sesión y qué puede hacer. Con los permisos, el panel y la app arman su menú.</summary>
public record SesionDTO(string Id, string Email, string NombreCompleto, string? Rol, List<string> Permisos);

/// <summary>El perfil propio (8.3).</summary>
public record PerfilDTO(string Id, string Email, string NombreCompleto, string? Rol, DateTime FechaCreacion);

public record EditarPerfilDTO
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [MaxLength(256, ErrorMessage = "El correo no puede superar los 256 caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre completo no puede superar los 150 caracteres.")]
    public string NombreCompleto { get; init; } = string.Empty;
}

public record CambiarContrasenaDTO
{
    [Required(ErrorMessage = "Escribe tu contraseña actual.")]
    public string Actual { get; init; } = string.Empty;

    // Mismas reglas que Identity en Program.cs: al menos 8 caracteres y un número (8.1).
    [Required(ErrorMessage = "Escribe la contraseña nueva.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [RegularExpression(@".*\d.*", ErrorMessage = "La contraseña debe tener al menos un número.")]
    public string Nueva { get; init; } = string.Empty;
}
