namespace ControlGestion.API.Models;

/// <summary>Ids de los estados que siembra AppDbContext. No cambian: el código los usa directo.</summary>
public static class EstadosDePedido
{
    public const int Pendiente = 1;
    public const int EnProceso = 2;
    public const int EnRuta = 3;
    public const int Entregado = 4;
    public const int Anulado = 5;

    /// <summary>Un pedido entregado o anulado ya no cambia.</summary>
    public static readonly int[] Finales = [Entregado, Anulado];

    /// <summary>
    /// Los estados solo avanzan (1.2): Pendiente → En proceso → En ruta. Desde En ruta lo que sigue es la entrega, y
    /// "Anulado" tiene su propia operación. Las transiciones viven solo aquí, no en la base.
    /// </summary>
    public static int? Siguiente(int estado) => estado switch
    {
        Pendiente => EnProceso,
        EnProceso => EnRuta,
        _ => null,
    };
}
