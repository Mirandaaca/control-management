namespace ControlGestion.API.Models;

public class Zona
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;

    /// <summary>El promotor de la zona, obligatorio: sus clientes son su cartera (7.2).</summary>
    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }
}
