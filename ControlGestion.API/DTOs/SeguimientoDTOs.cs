namespace ControlGestion.API.DTOs;

/// <summary>
/// Lo que la app sigue del promotor (tarea 31): su ruta de hoy, si está en curso, y cuántos de sus pedidos están En ruta
/// con entrega hoy. Con alguna entrega en ruta, la app manda su ubicación cada 10 s; con la ruta sola, cada 30 s.
/// </summary>
public record SeguimientoDTO(int? RutaEnCurso, int EntregasEnRuta);

/// <summary>
/// "Entregas de hoy" del panel (tarea 31): los promotores con pedidos asignados para hoy. Ahora es la hora del servidor:
/// con ella el panel dice "hace 20 s" sin depender del reloj de la PC.
/// </summary>
public record EntregasDelDiaDTO(DateOnly Dia, DateTime Ahora, IReadOnlyList<RepartidorDelDiaDTO> Promotores);

/// <summary>
/// Un promotor con sus entregas de hoy: cuántas tiene, cuántas entregó y cuántas están en ruta, su última ubicación de
/// hoy y por dónde pasó en los últimos 30 minutos (como mucho un punto cada 30 s, y el último).
/// </summary>
public record RepartidorDelDiaDTO(
    UsuarioResumenDTO Promotor,
    int Total,
    int Entregadas,
    int EnRuta,
    UbicacionDTO? UltimaUbicacion,
    IReadOnlyList<UbicacionDTO> Rastro,
    IReadOnlyList<EntregaDelDiaDTO> Entregas);

/// <summary>Una entrega de hoy. La ubicación es la del cliente, null si no la tiene: el pedido no guarda coordenadas.</summary>
public record EntregaDelDiaDTO(
    int Id,
    string Numero,
    EstadoPedidoDTO Estado,
    DateTime? FechaEntregaEstimada,
    DateTime? FechaEntregaReal,
    string DireccionEntrega,
    ClienteResumenDTO Cliente,
    PuntoDTO? UbicacionDelCliente);
