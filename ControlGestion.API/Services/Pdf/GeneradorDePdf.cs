using ControlGestion.API.Configuracion;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ControlGestion.API.Services.Pdf;

/// <summary>
/// Dibuja un <see cref="ReportePdf"/> con QuestPDF (tarea 33): en la primera hoja, la empresa, el título, los filtros y
/// cuándo se generó (hora de Bolivia); después, cada sección con su tabla, cuyo encabezado se repite en cada hoja; al pie
/// de todas, "Página N de M". La hoja es Carta, de costado si el reporte lo pide.
/// </summary>
public class GeneradorDePdf(TimeProvider reloj)
{
    /// <summary>
    /// El encabezado de la empresa. El logo y sus datos (dirección y teléfonos) siguen pendientes, como en la HP impresa
    /// del panel (components/hojas/empresa.js): cuando Julio los pase, van en los dos lugares.
    /// </summary>
    public const string Empresa = "Del Campo SRL";

    private const string Letra = "Inter";

    // Los grises de docs/diseno.md (slate de Tailwind), los mismos del panel.
    private const string ColorTexto = "#0f172a";
    private const string ColorSecundario = "#475569";
    private const string ColorBorde = "#e2e8f0";
    private const string ColorBordeFuerte = "#cbd5e1";
    private const string ColorFondo = "#f1f5f9";

    private static readonly string[] Fuentes = ["Fuentes.Inter-Regular.ttf", "Fuentes.Inter-SemiBold.ttf"];

    /// <summary>
    /// Se llama una vez al arrancar, antes del primer PDF. La licencia Community es la de una empresa con menos de
    /// US$ 1 millón al año (Del Campo, decisión de Cristopher del 26/11/2026; ver docs/despliegue.md).
    /// </summary>
    public static void Configurar()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // Solo la letra embebida: el App Service de Linux casi no trae fuentes, y así el PDF sale igual en todos lados.
        QuestPDF.Settings.UseSystemFonts = false;
        // Sin buscar fuentes en la carpeta de la API, que en desarrollo guarda las fotos (archivos/).
        QuestPDF.Settings.FontDiscoveryPath = null;
        // Un carácter que Inter no tiene (un emoji en el nombre de un cliente) sale como un recuadro, sin romper el PDF.
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

        var ensamblado = typeof(GeneradorDePdf).Assembly;
        foreach (var nombre in Fuentes)
        {
            using var fuente = ensamblado.GetManifestResourceStream(nombre)
                ?? throw new InvalidOperationException($"Falta la fuente embebida {nombre}.");
            FontManager.RegisterFontFromStream(fuente);
        }
    }

    public byte[] Generar(ReportePdf reporte)
    {
        var generado = HoraBolivia.DesdeUtc(reloj.GetUtcNow().UtcDateTime);
        return Document
            .Create(documento => documento.Page(pagina =>
            {
                pagina.Size(reporte.Horizontal ? PageSizes.Letter.Landscape() : PageSizes.Letter);
                pagina.Margin(1.5f, Unit.Centimetre);
                // Sin las alternativas de contexto ("calt") de Inter: cambian el guion y los dos puntos entre números por
                // otros glifos que el texto del PDF no sabe leer, y "PED-2026-00001" se copiaba como "PED202600001".
                pagina.DefaultTextStyle(t => t.FontFamily(Letra).FontSize(9).FontColor(ColorTexto).DisableFontFeature("calt"));

                pagina.Content().Column(columna =>
                {
                    columna.Item().Element(c => Encabezado(c, reporte, generado));
                    foreach (var seccion in reporte.Secciones)
                        columna.Item().PaddingTop(16).Element(c => Seccion(c, seccion));
                });

                pagina.Footer().PaddingTop(8).Row(fila =>
                {
                    fila.RelativeItem().Text($"{Empresa} · {reporte.Titulo}").FontSize(8).FontColor(ColorSecundario);
                    fila.AutoItem().Text(t =>
                    {
                        t.DefaultTextStyle(e => e.FontSize(8).FontColor(ColorSecundario));
                        t.Span("Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
                });
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = reporte.Titulo,
                Author = Empresa,
                Creator = "Control de Gestión",
                Language = "es-BO",
                CreationDate = new DateTimeOffset(DateTime.SpecifyKind(generado, DateTimeKind.Unspecified), TimeSpan.FromHours(HoraBolivia.DesfaseEnHoras)),
            })
            .GeneratePdf();
    }

    private static void Encabezado(IContainer contenedor, ReportePdf reporte, DateTime generado) =>
        contenedor.Column(columna =>
        {
            columna.Item().Text(Empresa).FontSize(9).FontColor(ColorSecundario);
            columna.Item().Text(reporte.Titulo).FontSize(16).SemiBold();
            columna.Item().PaddingTop(6).Text(t =>
            {
                if (reporte.Filtros.Count == 0)
                {
                    t.Span("Sin filtros").FontColor(ColorSecundario);
                    return;
                }
                for (var i = 0; i < reporte.Filtros.Count; i++)
                {
                    if (i > 0) t.Span("   ·   ").FontColor(ColorSecundario);
                    t.Span($"{reporte.Filtros[i].Etiqueta}: ").FontColor(ColorSecundario);
                    t.Span(reporte.Filtros[i].Valor).SemiBold();
                }
            });
            columna.Item().PaddingTop(2).Text($"Generado el {generado:dd/MM/yyyy} a las {generado:HH:mm}")
                .FontColor(ColorSecundario);
            columna.Item().PaddingTop(8).LineHorizontal(0.75f).LineColor(ColorBordeFuerte);
        });

    private static void Seccion(IContainer contenedor, SeccionPdf seccion) =>
        // El título no queda solo al final de una hoja: si no caben unas filas, la sección empieza en la siguiente.
        contenedor.EnsureSpace(70).Column(columna =>
        {
            columna.Item().Text(seccion.Titulo).FontSize(11).SemiBold();
            if (seccion.Filas.Count == 0)
            {
                columna.Item().PaddingTop(4).Text(seccion.SiVacia ?? "Sin datos.").FontColor(ColorSecundario);
                return;
            }
            var contenedorDeLaTabla = columna.Item().PaddingTop(6);
            if (seccion.AnchoMaximo is { } anchoMaximo) contenedorDeLaTabla = contenedorDeLaTabla.MaxWidth(anchoMaximo);
            contenedorDeLaTabla.Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    foreach (var col in seccion.Columnas) c.RelativeColumn(col.Ancho);
                });
                tabla.Header(encabezado =>
                {
                    foreach (var col in seccion.Columnas)
                        Celda(encabezado.Cell().Background(ColorFondo).BorderBottom(0.75f).BorderColor(ColorBordeFuerte), col)
                            .Text(col.Encabezado).SemiBold().FontColor(ColorSecundario);
                });
                foreach (var fila in seccion.Filas)
                    for (var i = 0; i < seccion.Columnas.Count; i++)
                        Celda(tabla.Cell().BorderBottom(0.5f).BorderColor(ColorBorde), seccion.Columnas[i])
                            .Text(i < fila.Count ? fila[i] : "");
                if (seccion.Total is { } total)
                    for (var i = 0; i < seccion.Columnas.Count; i++)
                        Celda(tabla.Cell().BorderTop(0.75f).BorderColor(ColorBordeFuerte), seccion.Columnas[i])
                            .Text(i < total.Count ? total[i] : "").SemiBold();
            });
            if (seccion.Nota is { } nota)
                columna.Item().PaddingTop(4).Text(nota).FontSize(8).FontColor(ColorSecundario);
        });

    private static IContainer Celda(IContainer celda, ColumnaPdf columna)
    {
        var conMargen = celda.PaddingVertical(3).PaddingHorizontal(4);
        return columna.Derecha ? conMargen.AlignRight() : conMargen;
    }
}
