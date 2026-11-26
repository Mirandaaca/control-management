using ControlGestion.API.DTOs;
using ControlGestion.API.Services;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CobranzasController(CobranzaService cobranzas, ReporteCobranzasService reporte) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.ConsultarPagos)]
    public async Task<ActionResult<Pagina<CobranzaResumenDTO>>> GetAll([FromQuery] FiltrosCobranzaDTO filtros) =>
        await cobranzas.ListarAsync(filtros);

    [HttpGet("resumen")]
    [Permiso(Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.ConsultarPagos)]
    public async Task<ActionResult<ResumenCobranzasDTO>> GetResumen([FromQuery] FiltrosCobranzaDTO filtros) =>
        await cobranzas.ResumenAsync(filtros);

    [HttpGet("reporte")]
    [Permiso(Permisos.Cobranzas.GenerarReporte)]
    public async Task<ActionResult<ReporteCobranzasDTO>> GetReporte([FromQuery] FiltrosCobranzaDTO filtros) =>
        await reporte.GenerarAsync(filtros);

    // Lo que debe cada cliente, con sus cobranzas (tarea 30): los mismos filtros y los mismos totales.
    [HttpGet("reporte/por-cliente")]
    [Permiso(Permisos.Cobranzas.GenerarReporte)]
    public async Task<ActionResult<ReporteCobranzasPorClienteDTO>> GetReportePorCliente([FromQuery] FiltrosCobranzaDTO filtros) =>
        await reporte.PorClienteAsync(filtros);

    [HttpGet("reporte/excel")]
    [Permiso(Permisos.Cobranzas.GenerarReporte)]
    public async Task<IActionResult> GetReporteExcel([FromQuery] FiltrosCobranzaDTO filtros)
    {
        var (contenido, nombre) = await reporte.ExcelAsync(filtros);
        return File(contenido, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    [HttpGet("reporte/pdf")]
    [Permiso(Permisos.Cobranzas.GenerarReporte)]
    public async Task<IActionResult> GetReportePdf([FromQuery] FiltrosCobranzaDTO filtros)
    {
        var (contenido, nombre) = await reporte.PdfAsync(filtros);
        return File(contenido, "application/pdf", nombre);
    }

    [HttpGet("{id}")]
    [Permiso(Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.ConsultarPagos)]
    public async Task<ActionResult<CobranzaDetalleDTO>> GetById(int id) =>
        await cobranzas.ObtenerAsync(id);

    [HttpPost("{id}/pagos")]
    [Permiso(Permisos.Cobranzas.RegistrarPago)]
    public async Task<ActionResult<PagoRegistradoDTO>> RegistrarPago(int id, [FromBody] RegistrarPagoDTO dto) =>
        await cobranzas.RegistrarPagoAsync(id, dto);

    // Registrar el pago sigue siendo JSON, con su clave de idempotencia: el comprobante se sube aparte, al registrarlo o
    // después. Lo adjunta quien puede registrar pagos en esa cobranza (cartera).
    [HttpPut("{id}/pagos/{pagoId}/comprobante")]
    [Permiso(Permisos.Cobranzas.RegistrarPago)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CobranzaDetalleDTO>> AdjuntarComprobante(int id, int pagoId, [FromForm] AdjuntarComprobanteDTO dto)
    {
        await using var contenido = dto.Archivo!.OpenReadStream();
        return await cobranzas.AdjuntarComprobanteAsync(id, pagoId,
            new ArchivoRecibido(dto.Archivo.FileName, dto.Archivo.Length, contenido));
    }

    [HttpPost("{id}/pagos/{pagoId}/anular")]
    [Permiso(Permisos.Cobranzas.AnularPago)]
    public async Task<ActionResult<CobranzaDetalleDTO>> AnularPago(int id, int pagoId, [FromBody] AnularPagoDTO dto) =>
        await cobranzas.AnularPagoAsync(id, pagoId, dto.Motivo);

    [HttpPost("{id}/incobrable")]
    [Permiso(Permisos.Cobranzas.MarcarIncobrable)]
    public async Task<ActionResult<CobranzaDetalleDTO>> MarcarIncobrable(int id, [FromBody] MarcarIncobrableDTO dto) =>
        await cobranzas.MarcarIncobrableAsync(id, dto.Observacion);
}
