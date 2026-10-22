namespace ControlGestion.API.Models;

public class EstadoCliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Color { get; set; } = PaletaDeEstados.Pizarra;
    public string? Descripcion { get; set; }

    /// <summary>
    /// Activo y Disponible para re-visita: los asigna el sistema según las visitas (5.2). No se asignan a mano, no se
    /// eliminan ni se renombran, y su color es fijo. Los demás sacan al cliente del ciclo de visitas.
    /// </summary>
    public bool EsDelSistema { get; set; }
}
