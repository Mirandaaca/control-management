namespace ControlGestion.API.DTOs;

public class LoginDTO
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>El login devuelve también la sesión, así el panel y la app no necesitan pedir /auth/yo enseguida.</summary>
public record LoginRespuestaDTO(string Token, SesionDTO Usuario);
