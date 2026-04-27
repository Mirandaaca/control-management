namespace ControlGestion.API.Models;

public class Anexo
{
    public int Id { get; set; }
    public string TablaOrigen { get; set; } = string.Empty;
    public int RegistroOrigenId { get; set; }

    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaAlmacenamiento { get; set; } = string.Empty;
    public string TipoMIME { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }

    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public DateTime? FechaCaptura { get; set; }
    public DateTime FechaSubida { get; set; } = DateTime.UtcNow;

    public string SubidoPorId { get; set; } = string.Empty;
    public ApplicationUser? SubidoPor { get; set; }

    public bool EsImagen { get; set; } = true;
    public string? Descripcion { get; set; }

    // Navegación para Evento (opcional)
    public int? EventoId { get; set; }
    public Evento? Evento { get; set; }
}
