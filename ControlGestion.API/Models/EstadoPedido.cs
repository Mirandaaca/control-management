namespace ControlGestion.API.Models;

public class EstadoPedido
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool EsEstadoFinal { get; set; } = false;
    public string PermiteTransicion { get; set; } = "[]";
    public string Color { get; set; } = "#6b7280";
}
