using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

public record RegistrarPagoDTO(
    decimal Monto,
    MetodoPago MetodoPago,
    string? Referencia,
    string? Observaciones
);
