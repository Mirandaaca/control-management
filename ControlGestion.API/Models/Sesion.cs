namespace ControlGestion.API.Models;

/// <summary>Desde dónde se inició la sesión: lo manda el cliente al iniciarla.</summary>
public enum OrigenSesion { Panel, App }

/// <summary>
/// Una sesión abierta con el login (8.1). El token lleva su id: cerrar la sesión, dejarla inactiva o cambiar la
/// contraseña lo anula, aunque su vencimiento (exp) todavía no haya llegado.
/// </summary>
public class Sesion
{
    /// <summary>Un Guid y no un número correlativo: el token se puede leer, y un número diría cuántas sesiones hubo.</summary>
    public Guid Id { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public ApplicationUser? Usuario { get; set; }

    public OrigenSesion Origen { get; set; }

    public DateTime Creada { get; set; }

    /// <summary>Se actualiza como mucho cada 5 minutos, para no escribir en la base en cada petición.</summary>
    public DateTime UltimaActividad { get; set; }

    /// <summary>Vacía mientras está abierta.</summary>
    public DateTime? CerradaEn { get; set; }
}
