namespace ControlGestion.API.Models;

public enum EstadoEvento { Registrado, Revisado, Resuelto, Escalado }

public class Evento
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;

    public int TipoEventoId { get; set; }
    public TipoEvento? TipoEvento { get; set; }

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int? PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }

    public DateTime FechaEvento { get; set; } = DateTime.UtcNow;
    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    public EstadoEvento EstadoEvento { get; set; } = EstadoEvento.Registrado;
    public string? ResueltoPorId { get; set; }
    public ApplicationUser? ResueltoPor { get; set; }
    public DateTime? FechaResolucion { get; set; }

    public ICollection<Anexo> Anexos { get; set; } = new List<Anexo>();
}
