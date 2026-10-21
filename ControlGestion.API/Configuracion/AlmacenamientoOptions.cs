namespace ControlGestion.API.Configuracion;

/// <summary>
/// Dónde se guardan las fotos de los eventos y los comprobantes de pago (sección "Almacenamiento"). Está fuera de
/// wwwroot: los archivos solo se entregan por GET /evidencias/{id}/archivo, que pide sesión y aplica la cartera.
/// </summary>
public class AlmacenamientoOptions
{
    public const string Seccion = "Almacenamiento";

    /// <summary>
    /// Una ruta relativa se resuelve contra la carpeta de la API (ContentRootPath), no contra el directorio de trabajo:
    /// así la carpeta es siempre la misma, se arranque la API desde donde se arranque (T7.03). En producción conviene
    /// una ruta absoluta, con la variable de entorno Almacenamiento__Ruta.
    /// </summary>
    public string Ruta { get; set; } = "archivos";
}
