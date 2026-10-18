namespace ControlGestion.API.DTOs;

// Cómo se ve un registro relacionado dentro de otro: solo lo necesario para mostrarlo y enlazarlo.
public record UsuarioResumenDTO(string Id, string NombreCompleto);

public record ClienteResumenDTO(int Id, string NombreCompleto);

public record HojaPedidoResumenDTO(int Id, string Numero);
