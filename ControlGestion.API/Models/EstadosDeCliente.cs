namespace ControlGestion.API.Models;

/// <summary>
/// Ids de los estados que el código asigna o revisa. Activo y Disponible para re-visita son del sistema
/// (EstadoCliente.EsDelSistema): los asigna según las visitas (5.2). Inactivo es manual, pero el código depende de él
/// (bloquea la HP y el pedido, y saca al cliente de las opciones, del mapa y del tablero): por eso no se elimina ni se
/// renombra.
/// </summary>
public static class EstadosDeCliente
{
    public const int Activo = 1;
    public const int DisponibleParaRevisita = 2;
    public const int Inactivo = 4;
}
