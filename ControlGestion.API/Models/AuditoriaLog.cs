namespace ControlGestion.API.Models;

/// <summary>
/// Un registro de la bitácora (8.5). No se modifica ni se elimina. Los de antes de la tarea 16 que se guardaron como
/// "Anonimo" (los inicios de sesión) quedaron sin usuario.
/// </summary>
public class AuditoriaLog
{
    public int Id { get; set; }
    public string? UsuarioId { get; set; }
    public ApplicationUser? Usuario { get; set; }

    /// <summary>El nombre de la operación del catálogo, con el método y la ruta: "Registrar pagos (POST /api/cobranzas/12/pagos)".</summary>
    public string Accion { get; set; } = string.Empty;

    /// <summary>Uno de los 8 módulos del catálogo de permisos.</summary>
    public string Modulo { get; set; } = string.Empty;

    public string? DireccionIp { get; set; }
    public DateTime Fecha { get; set; }
}
