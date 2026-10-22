using System.Globalization;
using ControlGestion.API.Excepciones;

namespace ControlGestion.API.Services;

/// <summary>Las reglas de las líneas de una venta o de un pedido que dependen del precio del producto.</summary>
public static class RevisionDeLineas
{
    private static readonly CultureInfo Bolivia = CultureInfo.GetCultureInfo("es-BO");

    /// <summary>
    /// El descuento por unidad no supera el precio (1.1, 2.1): si no, la línea queda en negativo aunque el total sea
    /// positivo. Igual al precio sí se acepta, como un producto de regalo. Marca todas las líneas con error a la vez,
    /// en el orden en que llegaron.
    /// </summary>
    public static void RevisarDescuentos(IEnumerable<(decimal Descuento, decimal Precio)> lineas)
    {
        var errores = lineas
            .Select((linea, indice) => (linea.Descuento, linea.Precio, Indice: indice))
            .Where(l => l.Descuento > l.Precio)
            .ToDictionary(l => $"detalles[{l.Indice}].descuento",
                l => new[] { $"El descuento no puede superar el precio (Bs {l.Precio.ToString("N2", Bolivia)})." });
        if (errores.Count > 0)
            throw new ValidacionException(errores);
    }
}
