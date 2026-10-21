namespace ControlGestion.API.Models;

/// <summary>
/// Una foto guardada en la carpeta de archivos (Almacenamiento:Ruta): de un evento (4.2) o el comprobante de un pago
/// (3.1). Solo imágenes JPEG, PNG o WebP. Antes se llamaba Anexo (7.5).
/// </summary>
public class Evidencia
{
    public int Id { get; set; }

    /// <summary>El evento de la foto. Un comprobante no tiene evento: lo apunta su pago (PagoCobranza.EvidenciaId).</summary>
    public int? EventoId { get; set; }
    public Evento? Evento { get; set; }

    public PagoCobranza? Pago { get; set; }

    /// <summary>El nombre que tenía el archivo en el equipo de quien lo subió. Solo se muestra: en el disco lleva otro.</summary>
    public string NombreArchivo { get; set; } = string.Empty;

    /// <summary>Dónde está dentro de la carpeta de archivos (aaaa/mm/guid.ext), con un nombre que pone el servidor.</summary>
    public string RutaAlmacenamiento { get; set; } = string.Empty;

    /// <summary>El tipo que sale de la firma del archivo, nunca el que declaró quien lo subió.</summary>
    public string TipoMIME { get; set; } = string.Empty;

    public long TamanoBytes { get; set; }

    // La ubicación y la hora de la toma las manda la app con el GPS del momento; en el panel quedan vacías.
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public DateTime? FechaCaptura { get; set; }

    public DateTime FechaSubida { get; set; }

    public string SubidoPorId { get; set; } = string.Empty;
    public ApplicationUser? SubidoPor { get; set; }

    public string? Descripcion { get; set; }
}
