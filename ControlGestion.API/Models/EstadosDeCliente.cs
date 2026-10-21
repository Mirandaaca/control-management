namespace ControlGestion.API.Models;

/// <summary>
/// Ids de los estados que siembra AppDbContext. Activo y Disponible para re-visita los asigna el sistema según las
/// visitas (5.2); los demás se asignan a mano y sacan al cliente del ciclo de visitas. Cuando exista
/// EstadoCliente.EsDelSistema (7.4), DelSistema sale de ese campo.
/// </summary>
public static class EstadosDeCliente
{
    public const int Activo = 1;
    public const int DisponibleParaRevisita = 2;
    public const int Inactivo = 4;

    public static readonly int[] DelSistema = [Activo, DisponibleParaRevisita];
}
