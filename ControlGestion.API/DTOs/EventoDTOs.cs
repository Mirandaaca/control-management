using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public class EventoCreateDTO
{
    [Required] public int TipoEventoId { get; set; }
    public int? ClienteId { get; set; }
    public int? PedidoId { get; set; }
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    [Required] public string Descripcion { get; set; } = string.Empty;
}

public class ResolverEventoDTO
{
    [Required] public string Observacion { get; set; } = string.Empty;
}
