using System.ComponentModel.DataAnnotations;

namespace ControlGestion.API.DTOs;

public class ClienteCreateDTO
{
    [Required] public string NombreCompleto { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public string? NIT { get; set; }
    [Required] public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    [Required] public string DireccionTexto { get; set; } = string.Empty;
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    [Required] public int ZonaId { get; set; }
    public string? PromotorId { get; set; }
    public int DiasRevisita { get; set; } = 15;
    public bool TieneCredito { get; set; } = false;
    public decimal LimiteCredito { get; set; } = 0;
}

public class ClienteUpdateDTO : ClienteCreateDTO
{
    public int EstadoClienteId { get; set; } = 1;
}
