namespace ControlGestion.API.DTOs;

public class VentaPorZonaDTO
{
    public string NombreZona { get; set; } = string.Empty;
    public decimal TotalVendido { get; set; }
    public int CantidadPedidos { get; set; }
}
