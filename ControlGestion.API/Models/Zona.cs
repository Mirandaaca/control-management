namespace ControlGestion.API.Models;

public class Zona
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? IdSupervisor { get; set; }
    public ApplicationUser? Supervisor { get; set; }
}
