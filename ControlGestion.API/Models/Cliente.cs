namespace ControlGestion.API.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public string? NIT { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string DireccionTexto { get; set; } = string.Empty;
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }

    public int ZonaId { get; set; }
    public Zona? Zona { get; set; }

    public int EstadoClienteId { get; set; }
    public EstadoCliente? EstadoCliente { get; set; }

    public string? PromotorId { get; set; }
    public ApplicationUser? Promotor { get; set; }

    public int DiasRevisita { get; set; } = 15;
    public DateTime? UltimaVisita { get; set; }
    public DateTime? ProximaVisita { get; set; }

    public bool TieneCredito { get; set; } = false;
    public decimal LimiteCredito { get; set; } = 0;
    public decimal SaldoPendiente { get; set; } = 0;

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? CreadoPorId { get; set; }
    public ApplicationUser? CreadoPor { get; set; }
}
