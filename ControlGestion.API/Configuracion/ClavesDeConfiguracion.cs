namespace ControlGestion.API.Configuracion;

/// <summary>Claves de la tabla ConfiguracionesGenerales que lee el código, con el valor que se usa si no existen.</summary>
public static class ClavesDeConfiguracion
{
    /// <summary>Días entre una visita y la siguiente (7.1). Uno solo para todos los clientes.</summary>
    public const string DiasRevisita = "DiasRevisita";
    public const int DiasRevisitaPorDefecto = 15;

    /// <summary>Plazo de un crédito, en días.</summary>
    public const string DiasVencimientoCredito = "DiasVencimientoCredito";
    public const int DiasVencimientoCreditoPorDefecto = 30;
}
