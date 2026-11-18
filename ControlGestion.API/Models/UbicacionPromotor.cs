namespace ControlGestion.API.Models;

/// <summary>
/// La posición del promotor mientras su ruta está en curso (7.3.20, paso 4.3) o, desde la tarea 31, mientras tiene entregas
/// en ruta: la app la manda cada 30 segundos (cada 10 con entregas), solo con la app abierta. El panel muestra la última,
/// con su hora (paso 6.2), y en "Entregas de hoy" también el rastro de la última media hora. La hora es la del servidor
/// al recibirla.
/// </summary>
public class UbicacionPromotor
{
    public int Id { get; set; }

    public string PromotorId { get; set; } = string.Empty;
    public ApplicationUser? Promotor { get; set; }

    public decimal Latitud { get; set; }
    public decimal Longitud { get; set; }

    public DateTime Fecha { get; set; }
}
