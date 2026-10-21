using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

public class LoginDTO
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    // Nullable para que [Required] detecte que falta: un enum sin valor tomaría el primero (Panel) sin avisar.
    [Required(ErrorMessage = "Indica si inicias sesión desde el panel o desde la app.")]
    [EnumDataType(typeof(OrigenSesion), ErrorMessage = "El origen debe ser el panel o la app.")]
    public OrigenSesion? Origen { get; set; }
}

/// <summary>El login devuelve también la sesión, así el panel y la app no necesitan pedir /auth/yo enseguida.</summary>
public record LoginRespuestaDTO(string Token, SesionDTO Usuario);
