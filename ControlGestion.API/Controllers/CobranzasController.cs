using ControlGestion.API.DTOs;
using ControlGestion.API.Services;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CobranzasController(CobranzaService cobranzas) : ControllerBase
{
    [HttpGet]
    [Permiso(Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.ConsultarPagos)]
    public async Task<ActionResult<Pagina<CobranzaResumenDTO>>> GetAll([FromQuery] FiltrosCobranzaDTO filtros) =>
        await cobranzas.ListarAsync(filtros);

    [HttpGet("resumen")]
    [Permiso(Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.ConsultarPagos)]
    public async Task<ActionResult<ResumenCobranzasDTO>> GetResumen([FromQuery] FiltrosCobranzaDTO filtros) =>
        await cobranzas.ResumenAsync(filtros);

    [HttpGet("{id}")]
    [Permiso(Permisos.Cobranzas.ConsultarDeudas, Permisos.Cobranzas.ConsultarPagos)]
    public async Task<ActionResult<CobranzaDetalleDTO>> GetById(int id) =>
        await cobranzas.ObtenerAsync(id);

    [HttpPost("{id}/pagos")]
    [Permiso(Permisos.Cobranzas.RegistrarPago)]
    public async Task<ActionResult<PagoRegistradoDTO>> RegistrarPago(int id, [FromBody] RegistrarPagoDTO dto) =>
        await cobranzas.RegistrarPagoAsync(id, dto);

    [HttpPost("{id}/pagos/{pagoId}/anular")]
    [Permiso(Permisos.Cobranzas.AnularPago)]
    public async Task<ActionResult<CobranzaDetalleDTO>> AnularPago(int id, int pagoId, [FromBody] AnularPagoDTO dto) =>
        await cobranzas.AnularPagoAsync(id, pagoId, dto.Motivo);

    [HttpPost("{id}/incobrable")]
    [Permiso(Permisos.Cobranzas.MarcarIncobrable)]
    public async Task<ActionResult<CobranzaDetalleDTO>> MarcarIncobrable(int id, [FromBody] MarcarIncobrableDTO dto) =>
        await cobranzas.MarcarIncobrableAsync(id, dto.Observacion);
}
