namespace ControlGestion.API.Models;

public class AuditoriaLog
{
    public int Id { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string? DireccionIp { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}
