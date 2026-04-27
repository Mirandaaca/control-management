namespace ControlGestion.API.Models;

public enum NivelPrioridad { Bajo, Medio, Alto }

public class TipoEvento
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool RequiereEvidencia { get; set; } = false;
    public bool RequiereGeolocalizacion { get; set; } = true;
    public NivelPrioridad NivelPrioridad { get; set; } = NivelPrioridad.Medio;
    public bool Activo { get; set; } = true;
}
