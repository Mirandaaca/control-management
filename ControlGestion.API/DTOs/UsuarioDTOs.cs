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

    // Mismas reglas que Identity en Program.cs: al menos 6 caracteres y un dígito.
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    [RegularExpression(@".*\d.*", ErrorMessage = "La contraseña debe tener al menos un dígito.")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    [RegularExpression("^(Admin|Supervisor|Promotor)$", ErrorMessage = "El rol debe ser Admin, Supervisor o Promotor.")]
    public string Rol { get; init; } = string.Empty;
}

public record UsuarioDTO(string Id, string Email, string NombreCompleto, bool Activo, IList<string> Roles);
