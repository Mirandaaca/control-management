namespace ControlGestion.API.Seguridad;

/// <summary>
/// Los cuatro roles que se siembran al instalar el sistema (precondición de la 7.3.34). Son datos: el administrador puede
/// crear otros y cambiar sus permisos. El código solo reconoce por su nombre al administrador, que no se renombra.
/// </summary>
public static class Roles
{
    public const string Administrador = "Administrador del sistema";
    public const string GerenteGeneral = "Gerente general";
    public const string Secretaria = "Secretaria";
    public const string Promotor = "Promotor";

    public static readonly string[] DelSistema = [Administrador, GerenteGeneral, Secretaria, Promotor];
}
