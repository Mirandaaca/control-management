using ControlGestion.API.DTOs;
using ControlGestion.API.Services;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

// Los reportes del módulo 6. El de pedidos, el de cobranzas y el de incidencias viven en el controlador de su módulo; la
// bitácora, en BitacoraController.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController(
    TableroService tablero,
    ReporteVentasService ventas,
    ReporteClientesService clientes,
    ReporteInventarioService inventario) : ControllerBase
{
    private const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Sin operación en el catálogo: es el inicio del panel, y cada usuario lo ve con su cartera.
    [HttpGet("tablero")]
    public async Task<ActionResult<TableroDTO>> GetTablero() =>
        await tablero.ResumenAsync();

    [HttpGet("ventas-por-zona")]
    [Permiso(Permisos.Reportes.VentasPorZona)]
    public async Task<ActionResult<ReporteVentasPorZonaDTO>> GetVentasPorZona([FromQuery] FiltrosPeriodoDTO filtros) =>
        await ventas.PorZonaAsync(filtros);

    [HttpGet("ventas-por-promotor")]
    [Permiso(Permisos.Reportes.VentasPorPromotor)]
    public async Task<ActionResult<ReporteVentasPorPromotorDTO>> GetVentasPorPromotor([FromQuery] FiltrosVentasPorPromotorDTO filtros) =>
        await ventas.PorPromotorAsync(filtros);

    [HttpGet("clientes")]
    [Permiso(Permisos.Reportes.ClientesNuevosRecurrentes)]
    public async Task<ActionResult<ReporteClientesDTO>> GetClientes([FromQuery] FiltrosReporteClientesDTO filtros) =>
        await clientes.GenerarAsync(filtros);

    [HttpGet("clientes/excel")]
    [Permiso(Permisos.Reportes.ClientesNuevosRecurrentes)]
    public async Task<IActionResult> GetClientesExcel([FromQuery] FiltrosReporteClientesDTO filtros)
    {
        var (contenido, nombre) = await clientes.ExcelAsync(filtros);
        return File(contenido, TipoExcel, nombre);
    }

    [HttpGet("inventario")]
    [Permiso(Permisos.Reportes.Inventario)]
    public async Task<ActionResult<ReporteInventarioDTO>> GetInventario([FromQuery] FiltrosReporteInventarioDTO filtros) =>
        await inventario.GenerarAsync(filtros);

    [HttpGet("inventario/excel")]
    [Permiso(Permisos.Reportes.Inventario)]
    public async Task<IActionResult> GetInventarioExcel([FromQuery] FiltrosReporteInventarioDTO filtros)
    {
        var (contenido, nombre) = await inventario.ExcelAsync(filtros);
        return File(contenido, TipoExcel, nombre);
    }
}
