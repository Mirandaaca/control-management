using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public class DetallePedidoDTO
{
    [Required] public int ProductoId { get; set; }
    [Required][Range(0.001, double.MaxValue)] public decimal Cantidad { get; set; }
    public decimal Descuento { get; set; } = 0;
}

public class PedidoCreateDTO
{
    [Required] public int ClienteId { get; set; }
    [Required] public int ZonaId { get; set; }
    public int? HojaPedidoId { get; set; }
    public DateTime? FechaEntregaEstimada { get; set; }
    public decimal Descuento { get; set; } = 0;
    public string? Observaciones { get; set; }
    [Required][MinLength(1)] public List<DetallePedidoDTO> Detalles { get; set; } = new();
}

public class CambiarEstadoDTO
{
    [Required] public int NuevoEstadoId { get; set; }
    public string? Observacion { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public decimal? CantidadEntregada { get; set; }
}
