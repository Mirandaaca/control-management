namespace ControlGestion.API.Models;

/// <summary>Una operación de un módulo (8.4). Los siembra el código desde Seguridad/Permisos.cs.</summary>
public class Permiso
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
