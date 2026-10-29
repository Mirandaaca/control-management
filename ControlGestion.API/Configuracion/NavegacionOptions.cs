using Microsoft.Extensions.Options;

namespace ControlGestion.API.Configuracion;

/// <summary>
/// El camino por calles hasta una visita (tarea 25), con OpenRouteService (sección "Navegacion"). La clave es un secreto:
/// en desarrollo va en user-secrets y en producción en la variable Navegacion__Clave. Es opcional: sin ella la API arranca y
/// el camino responde 503, porque la ruta del día se recorre igual sin navegación.
/// </summary>
public class NavegacionOptions
{
    public const string Seccion = "Navegacion";

    public string? Clave { get; set; }

    public string Url { get; set; } = "https://api.openrouteservice.org/";

    /// <summary>Pedidos al proveedor por día de Bolivia, entre todos los usuarios. El plan gratuito permite 2.000.</summary>
    public int TopeDiario { get; set; } = 1800;

    public int CaminosPorUsuarioAlDia { get; set; } = 300;

    public int SegundosEntreCaminos { get; set; } = 10;

    /// <summary>Cuánto se guarda un camino ya pedido, por si se vuelve a pedir desde casi el mismo punto.</summary>
    public int SegundosDeCache { get; set; } = 60;

    /// <summary>Órdenes de las visitas (la optimización del proveedor) por día de Bolivia, entre todos.</summary>
    public int TopeDiarioDeOrdenes { get; set; } = 400;

    public int OrdenesPorUsuarioAlDia { get; set; } = 50;

    public int SegundosEntreOrdenes { get; set; } = 20;
}

/// <summary>Detiene la API al arrancar si la dirección del proveedor no es válida o un límite no es mayor que cero.</summary>
public class NavegacionOptionsValidator : IValidateOptions<NavegacionOptions>
{
    public ValidateOptionsResult Validate(string? name, NavegacionOptions opciones)
    {
        var errores = new List<string>();
        if (!Uri.TryCreate(opciones.Url, UriKind.Absolute, out _))
            errores.Add("'Navegacion:Url' debe ser una dirección completa, como https://api.openrouteservice.org/.");
        if (opciones.TopeDiario <= 0)
            errores.Add("'Navegacion:TopeDiario' debe ser mayor que cero.");
        if (opciones.CaminosPorUsuarioAlDia <= 0)
            errores.Add("'Navegacion:CaminosPorUsuarioAlDia' debe ser mayor que cero.");
        if (opciones.SegundosEntreCaminos <= 0)
            errores.Add("'Navegacion:SegundosEntreCaminos' debe ser mayor que cero.");
        if (opciones.SegundosDeCache <= 0)
            errores.Add("'Navegacion:SegundosDeCache' debe ser mayor que cero.");
        if (opciones.TopeDiarioDeOrdenes <= 0)
            errores.Add("'Navegacion:TopeDiarioDeOrdenes' debe ser mayor que cero.");
        if (opciones.OrdenesPorUsuarioAlDia <= 0)
            errores.Add("'Navegacion:OrdenesPorUsuarioAlDia' debe ser mayor que cero.");
        if (opciones.SegundosEntreOrdenes <= 0)
            errores.Add("'Navegacion:SegundosEntreOrdenes' debe ser mayor que cero.");
        return errores.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errores);
    }
}
