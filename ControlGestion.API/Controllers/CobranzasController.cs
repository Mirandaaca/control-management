using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CobranzasController : ControllerBase
{
    private readonly AppDbContext _db;

    public CobranzasController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? clienteId,
        [FromQuery] EstadoCobranza? estado,
        [FromQuery] string? promotorId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var query = _db.Cobranzas
            .Include(c => c.Cliente)
            .Include(c => c.Promotor)
            .Include(c => c.HojaPedido)
            .Include(c => c.Pagos)
            .AsQueryable();

        if (clienteId.HasValue) query = query.Where(c => c.ClienteId == clienteId);
        if (estado.HasValue) query = query.Where(c => c.Estado == estado);
        if (!string.IsNullOrEmpty(promotorId)) query = query.Where(c => c.PromotorId == promotorId);
        if (desde.HasValue) query = query.Where(c => c.FechaEmision >= desde);
        if (hasta.HasValue) query = query.Where(c => c.FechaEmision <= hasta);

        var list = await query.OrderByDescending(c => c.FechaEmision).ToListAsync();

        return Ok(list.Select(c => new
        {
            c.Id, c.Numero, c.Estado, c.MontoTotal, c.MontoPagado,
            SaldoPendiente = Math.Max(0, c.MontoTotal - c.MontoPagado),
            c.FechaEmision, c.FechaVencimiento, c.FechaPagoCompleto,
            Cliente = c.Cliente == null ? null : new { c.Cliente.Id, c.Cliente.NombreCompleto },
            Promotor = c.Promotor == null ? null : new { c.Promotor.Id, c.Promotor.NombreCompleto },
            HojaPedido = c.HojaPedido == null ? null : new { c.HojaPedido.Id, c.HojaPedido.Numero },
            CantidadPagos = c.Pagos.Count
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cobranza = await _db.Cobranzas
            .Include(c => c.Cliente)
            .Include(c => c.Promotor)
            .Include(c => c.HojaPedido)
            .Include(c => c.Pagos).ThenInclude(p => p.RegistradoPor)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cobranza == null) return NotFound();

        return Ok(new
        {
            cobranza.Id, cobranza.Numero, cobranza.Estado,
            cobranza.MontoTotal, cobranza.MontoPagado,
            SaldoPendiente = Math.Max(0, cobranza.MontoTotal - cobranza.MontoPagado),
            cobranza.FechaEmision, cobranza.FechaVencimiento, cobranza.FechaPagoCompleto,
            cobranza.Observaciones,
            Cliente = cobranza.Cliente == null ? null : new { cobranza.Cliente.Id, cobranza.Cliente.NombreCompleto },
            Promotor = cobranza.Promotor == null ? null : new { cobranza.Promotor.Id, cobranza.Promotor.NombreCompleto },
            HojaPedido = cobranza.HojaPedido == null ? null : new { cobranza.HojaPedido.Id, cobranza.HojaPedido.Numero },
            Pagos = cobranza.Pagos.Select(p => new
            {
                p.Id, p.Monto, p.MetodoPago, p.FechaPago, p.Referencia, p.Observaciones,
                RegistradoPor = p.RegistradoPor == null ? null : new { p.RegistradoPor.Id, p.RegistradoPor.NombreCompleto }
            })
        });
    }

    [HttpPost("{id}/pagos")]
    public async Task<IActionResult> RegistrarPago(int id, [FromBody] RegistrarPagoDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var cobranza = await _db.Cobranzas
            .Include(c => c.Pagos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cobranza == null) return NotFound();
        if (cobranza.Estado == EstadoCobranza.Pagada || cobranza.Estado == EstadoCobranza.Incobrable)
            return BadRequest(new { mensaje = "La cobranza ya está cerrada." });

        var saldo = Math.Max(0, cobranza.MontoTotal - cobranza.MontoPagado);
        if (saldo <= 0)
            return BadRequest(new { mensaje = "Esta cobranza ya no tiene saldo pendiente." });
        if (dto.Monto <= 0 || dto.Monto > saldo)
            return BadRequest(new { mensaje = $"Monto inválido. Saldo pendiente: {saldo:F2}" });

        var pago = new PagoCobranza
        {
            CobranzaId = id,
            RegistradoPorId = userId,
            Monto = dto.Monto,
            MetodoPago = dto.MetodoPago,
            Referencia = dto.Referencia,
            Observaciones = dto.Observaciones
        };

        cobranza.MontoPagado += dto.Monto;

        if (cobranza.MontoPagado >= cobranza.MontoTotal)
        {
            cobranza.Estado = EstadoCobranza.Pagada;
            cobranza.FechaPagoCompleto = DateTime.UtcNow;

            var cliente = await _db.Clientes.FindAsync(cobranza.ClienteId);
            if (cliente != null)
            {
                cliente.SaldoPendiente = Math.Max(0, cliente.SaldoPendiente - cobranza.MontoTotal);
            }
        }
        else
        {
            cobranza.Estado = EstadoCobranza.Parcial;
        }

        _db.PagosCobranza.Add(pago);
        await _db.SaveChangesAsync();

        return Ok(new { pago.Id, cobranza.Estado, cobranza.MontoPagado, SaldoPendiente = cobranza.MontoTotal - cobranza.MontoPagado });
    }

    [HttpPost("{id}/incobrable")]
    [Authorize(Roles = "Admin,Supervisor")]
    public async Task<IActionResult> MarcarIncobrable(int id, [FromBody] string observacion)
    {
        var cobranza = await _db.Cobranzas.FindAsync(id);
        if (cobranza == null) return NotFound();
        if (cobranza.Estado == EstadoCobranza.Pagada)
            return BadRequest(new { mensaje = "No se puede marcar como incobrable una cobranza pagada." });

        cobranza.Estado = EstadoCobranza.Incobrable;
        cobranza.Observaciones = observacion;
        await _db.SaveChangesAsync();
        return Ok();
    }
}
