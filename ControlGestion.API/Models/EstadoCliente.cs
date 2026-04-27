namespace ControlGestion.API.Models;

public class EstadoCliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Color { get; set; } = "#6B7280";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}
