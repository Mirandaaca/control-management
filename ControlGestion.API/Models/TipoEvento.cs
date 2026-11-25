namespace ControlGestion.API.Models;

public enum NivelPrioridad { Bajo, Medio, Alto }

/// <summary>
/// El tipo de un evento (7.3.13). Desde la tarea 32 lo administra el administrador del sistema: el nombre no se repite,
/// sin importar mayúsculas ni tildes (la intercalación de la base). Uno inactivo no se elige en un evento nuevo, pero
/// los eventos que ya lo tienen lo conservan; uno con eventos no se elimina.
/// </summary>
public class TipoEvento
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Un evento de este tipo se resuelve con al menos una foto (7.3.13, ext. 9).</summary>
    public bool RequiereEvidencia { get; set; } = false;

    public NivelPrioridad NivelPrioridad { get; set; } = NivelPrioridad.Medio;
    public bool Activo { get; set; } = true;
}
