namespace ControlGestion.API.DTOs;

public record RutaCreateDTO(
    string Nombre,
    int ZonaId,
    DateTime FechaPlanificada,
    string? Observaciones,
    List<RutaDetalleDTO> Clientes
);

public record RutaDetalleDTO(int ClienteId, int Orden);

public record MarcarVisitaDTO(
    int EstadoVisita,
    decimal? Latitud,
    decimal? Longitud,
    string? Observaciones
);
