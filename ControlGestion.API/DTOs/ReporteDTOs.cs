using System.ComponentModel.DataAnnotations;
using ControlGestion.API.Models;

namespace ControlGestion.API.DTOs;

/// <summary>Un período por días de Bolivia. Sin fechas, todo (ext. 2b de la 7.3.22 y la 7.3.23).</summary>
public record FiltrosPeriodoDTO : IValidatableObject
{
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Desde > Hasta)
            yield return new ValidationResult("La fecha \"desde\" no puede ser posterior a \"hasta\".", [nameof(Desde)]);
    }
}

public record FiltrosVentasPorPromotorDTO : FiltrosPeriodoDTO
{
    public string? PromotorId { get; init; }
}

/// <summary>Las ventas confirmadas de un grupo: cuántas HP, el total y cuánto fue al contado y cuánto a crédito.</summary>
public record TotalVentasDTO(int Cantidad, decimal Total, decimal Contado, decimal Credito);

public record VentasDeZonaDTO(int ZonaId, string Zona, int Cantidad, decimal Total, decimal Contado, decimal Credito);

/// <summary>Ventas por zona (6.2, 7.3.23): todas las zonas, también las que no vendieron, y la fila de totales.</summary>
public record ReporteVentasPorZonaDTO(List<VentasDeZonaDTO> Zonas, TotalVentasDTO Total);

public record VentasDePromotorDTO(string PromotorId, string Promotor, int Cantidad, decimal Total);

/// <summary>Ventas por promotor (6.1, 7.3.22): cada promotor con sus HP confirmadas y el total del período.</summary>
public record ReporteVentasPorPromotorDTO(List<VentasDePromotorDTO> Promotores, int Cantidad, decimal Total);

/// <summary>El período de clientes nuevos y recurrentes (7.3.24): las dos fechas son obligatorias (ext. 2a).</summary>
public record FiltrosReporteClientesDTO : IValidatableObject
{
    [Required(ErrorMessage = "Elige el primer día del período.")]
    public DateOnly? Desde { get; init; }

    [Required(ErrorMessage = "Elige el último día del período.")]
    public DateOnly? Hasta { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int PaginaNuevos { get; init; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int PaginaRecurrentes { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de la página va de 1 a 100.")]
    public int Tamano { get; init; } = 25;

    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Desde > Hasta)
            yield return new ValidationResult("La fecha \"desde\" no puede ser posterior a \"hasta\".", [nameof(Desde)]);
    }
}

public record ClienteNuevoDTO(int Id, string Codigo, string NombreCompleto, ZonaResumenDTO Zona, UsuarioResumenDTO? Promotor,
    DateTime FechaRegistro);

/// <summary>Compras son sus HP confirmadas del período.</summary>
public record ClienteRecurrenteDTO(int Id, string Codigo, string NombreCompleto, ZonaResumenDTO Zona, UsuarioResumenDTO? Promotor,
    int Compras);

/// <summary>
/// Clientes nuevos y recurrentes (6.3): nuevos son los registrados en el período; recurrentes, los registrados antes con
/// al menos una HP confirmada en él. Cada lista va paginada.
/// </summary>
public record ReporteClientesDTO(int CantidadNuevos, int CantidadRecurrentes, Pagina<ClienteNuevoDTO> Nuevos,
    Pagina<ClienteRecurrenteDTO> Recurrentes);

/// <summary>Los filtros del reporte de inventario (7.3.25): la categoría y los días de los movimientos.</summary>
public record FiltrosReporteInventarioDTO : IValidatableObject
{
    [MaxLength(100, ErrorMessage = "La categoría no puede superar los 100 caracteres.")]
    public string? Categoria { get; init; }

    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página empieza en 1.")]
    public int Pagina { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "El tamaño de la página va de 1 a 100.")]
    public int Tamano { get; init; } = 25;

    public IEnumerable<ValidationResult> Validate(ValidationContext contexto)
    {
        if (Desde > Hasta)
            yield return new ValidationResult("La fecha \"desde\" no puede ser posterior a \"hasta\".", [nameof(Desde)]);
    }
}

/// <summary>Un producto activo con su stock. Falta es lo que le falta para llegar al mínimo (0 si no está bajo él).</summary>
public record StockDeProductoDTO(int Id, string Codigo, string Producto, string? Categoria, decimal StockActual, decimal StockMinimo,
    decimal Falta);

public record MovimientoDelReporteDTO(int Id, DateTime Fecha, string Producto, TipoMovimiento Tipo, decimal Cantidad,
    decimal StockResultante, UsuarioResumenDTO Usuario);

/// <summary>
/// El reporte de inventario (6.4): las categorías para el filtro, el stock de cada producto activo, aparte los que están
/// en su mínimo o debajo, y los movimientos del período, del más reciente.
/// </summary>
public record ReporteInventarioDTO(List<string> Categorias, List<StockDeProductoDTO> Productos, List<StockDeProductoDTO> BajoElMinimo,
    Pagina<MovimientoDelReporteDTO> Movimientos);

public record VentasDeProductoDTO(string Producto, decimal Cantidad, decimal Total);

/// <summary>Las ventas confirmadas de un día de Bolivia (aaaa-mm-dd).</summary>
public record VentasDelDiaDTO(string Fecha, int Cantidad, decimal Total);

/// <summary>
/// El tablero de Inicio, del mes en curso en Bolivia y con la cartera del usuario. No es de ninguna especificación: su
/// aspecto se rehace en la tarea 18.
/// </summary>
public record TableroDTO(
    int ClientesActivos,
    int HojasDelMes,
    decimal VentasDelMes,
    decimal PorCobrar,
    int EventosPendientes,
    int StockBajo,
    List<VentasDePromotorDTO> PorPromotor,
    List<VentasDeProductoDTO> PorProducto,
    List<VentasDelDiaDTO> PorDia);
