namespace ControlGestion.API.Services.Pdf;

/// <summary>
/// Un reporte listo para el PDF (tarea 33). Cada servicio lo arma con los mismos datos de su Excel, y
/// <see cref="GeneradorDePdf"/> lo dibuja: el título, los filtros elegidos y cada sección con su tabla. Los textos llegan
/// ya escritos (montos en Bs y fechas de Bolivia), así el generador no sabe nada del negocio.
/// </summary>
/// <param name="Horizontal">La hoja Carta de costado, para las tablas de muchas columnas.</param>
public record ReportePdf(string Titulo, IReadOnlyList<FiltroPdf> Filtros, IReadOnlyList<SeccionPdf> Secciones, bool Horizontal = false);

/// <summary>Un filtro elegido, como "Zona: Zona Norte".</summary>
public record FiltroPdf(string Etiqueta, string Valor);

/// <summary>Una columna: su encabezado, su ancho relativo a las demás y si va alineada a la derecha (cantidades y montos).</summary>
public record ColumnaPdf(string Encabezado, float Ancho = 1, bool Derecha = false);

/// <summary>
/// Una tabla con su título. Sin filas, muestra <paramref name="SiVacia"/>. <paramref name="Total"/> es la última fila, en
/// negrita; <paramref name="Nota"/>, un texto chico debajo de la tabla. <paramref name="AnchoMaximo"/> (en puntos) angosta
/// una tabla de pocas columnas, como la de los totales, para que no ocupe toda la hoja.
/// </summary>
public record SeccionPdf(
    string Titulo,
    IReadOnlyList<ColumnaPdf> Columnas,
    IReadOnlyList<IReadOnlyList<string>> Filas,
    IReadOnlyList<string>? Total = null,
    string? SiVacia = null,
    string? Nota = null,
    float? AnchoMaximo = null);

/// <summary>Los anchos de las tablas chicas, en puntos (la hoja Carta derecha deja unos 570 de ancho útil).</summary>
public static class AnchosPdf
{
    public const float Totales = 380;
}
