using ControlGestion.API.Data;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Services;
using ControlGestion.API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/hojas-pedido")]
[Authorize]
public class HojasPedidoController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly InventarioService _inventario;
    private readonly NumeradorService _numerador;

    public HojasPedidoController(AppDbContext db, InventarioService inventario, NumeradorService numerador)
    {
        _db = db;
        _inventario = inventario;
        _numerador = numerador;
    }

    [HttpGet]
    [Permiso(Permisos.Ventas.ConsultarHp)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? clienteId,
        [FromQuery] string? promotorId,
        [FromQuery] EstadoHP? estado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var query = _db.HojasPedido
            .Include(h => h.Cliente)
            .Include(h => h.Promotor)
            .Include(h => h.Zona)
            .AsQueryable();

        if (clienteId.HasValue) query = query.Where(h => h.ClienteId == clienteId);
        if (!string.IsNullOrEmpty(promotorId)) query = query.Where(h => h.PromotorId == promotorId);
        if (estado.HasValue) query = query.Where(h => h.EstadoHP == estado);
        if (desde.HasValue) query = query.Where(h => h.FechaRegistro >= desde);
        if (hasta.HasValue) query = query.Where(h => h.FechaRegistro <= hasta);

        return Ok(await query.OrderByDescending(h => h.FechaRegistro).ToListAsync());
    }

    [HttpGet("{id}")]
    [Permiso(Permisos.Ventas.ConsultarHp)]
    public async Task<IActionResult> GetById(int id)
    {
        var hp = await _db.HojasPedido
            .Include(h => h.Cliente)
            .Include(h => h.Promotor)
            .Include(h => h.Zona)
            .Include(h => h.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(h => h.Id == id);

        return Ok(hp ?? throw new NoEncontradoException("No se encontró la hoja de pedido."));
    }

    [HttpPost]
    [Permiso(Permisos.Ventas.RegistrarHp)]
    public async Task<IActionResult> Create([FromBody] HojaPedidoCreateDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var cliente = await _db.Clientes.FindAsync(dto.ClienteId);
        if (cliente == null) throw new ReglaDeNegocioException("cliente_no_existe", "Cliente no encontrado.");

        var productos = await _db.Productos
            .Where(p => dto.Detalles.Select(d => d.ProductoId).Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id);

        if (productos.Count != dto.Detalles.Select(d => d.ProductoId).Distinct().Count())
            throw new ReglaDeNegocioException("producto_no_disponible", "Uno o más productos no existen o están inactivos.");

        var hp = new HojaPedido
        {
            Numero = await _numerador.SiguienteDelAnioAsync(NumeradorService.HojaPedido),
            ClienteId = dto.ClienteId,
            PromotorId = userId,
            ZonaId = dto.ZonaId,
            FechaRegistro = DateTime.UtcNow,
            Latitud = dto.Latitud,
            Longitud = dto.Longitud,
            PrecisionGPS = dto.PrecisionGPS,
            TipoVenta = dto.TipoVenta,
            Descuento = dto.Descuento,
            EstadoHP = EstadoHP.Borrador
        };

        foreach (var item in dto.Detalles)
        {
            var producto = productos[item.ProductoId];
            var subtotal = (producto.Precio - item.Descuento) * item.Cantidad;
            hp.Detalles.Add(new DetalleHP
            {
                ProductoId = item.ProductoId,
                Cantidad = item.Cantidad,
                PrecioUnitario = producto.Precio,
                Descuento = item.Descuento,
                Subtotal = subtotal
            });
        }

        hp.SubTotal = hp.Detalles.Sum(d => d.Subtotal);
        hp.Total = hp.SubTotal - hp.Descuento;

        if (hp.Total <= 0)
            throw new ReglaDeNegocioException("total_invalido", $"El total de la HP debe ser mayor a cero. Subtotal: {hp.SubTotal:F2}, Descuento: {hp.Descuento:F2}, Total: {hp.Total:F2}");

        _db.HojasPedido.Add(hp);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = hp.Id }, hp);
    }

    [HttpPost("{id}/confirmar")]
    [Permiso(Permisos.Ventas.ConfirmarHp)]
    public async Task<IActionResult> Confirmar(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var hp = await _db.HojasPedido
            .Include(h => h.Detalles)
            .Include(h => h.Cliente)
            .FirstOrDefaultAsync(h => h.Id == id);

        if (hp == null) throw new NoEncontradoException("No se encontró la hoja de pedido.");
        if (hp.EstadoHP != EstadoHP.Borrador)
            throw new ReglaDeNegocioException("hp_no_es_borrador", "Solo se pueden confirmar HPs en estado Borrador.");

        if (hp.TipoVenta == TipoVenta.Credito && hp.Cliente != null)
        {
            var creditoDisponible = hp.Cliente.LimiteCredito - hp.Cliente.SaldoPendiente;
            if (!hp.Cliente.TieneCredito || hp.Total > creditoDisponible)
                throw new ReglaDeNegocioException("credito_insuficiente", "Crédito insuficiente o no habilitado para este cliente.");
        }

        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            hp.EstadoHP = EstadoHP.Confirmada;
            await _inventario.DescontarStockPorHP(hp, userId);

            // Actualizar última visita del cliente
            if (hp.Cliente != null)
            {
                hp.Cliente.UltimaVisita = DateTime.UtcNow;
                hp.Cliente.ProximaVisita = DateTime.UtcNow.AddDays(hp.Cliente.DiasRevisita);
                hp.Cliente.EstadoClienteId = 1;

                if (hp.TipoVenta == TipoVenta.Credito)
                    hp.Cliente.SaldoPendiente += hp.Total;
            }

            // Crear cobranza automática para ventas a crédito
            if (hp.TipoVenta == TipoVenta.Credito && hp.Total > 0)
            {
                var config = await _db.ConfiguracionesGenerales.FindAsync("DiasVencimientoCredito");
                var diasVencimiento = int.TryParse(config?.Valor, out var d) ? d : 30;

                _db.Cobranzas.Add(new ControlGestion.API.Models.Cobranza
                {
                    Numero = await _numerador.SiguienteDelAnioAsync(NumeradorService.Cobranza),
                    ClienteId = hp.ClienteId,
                    HojaPedidoId = hp.Id,
                    // El responsable es quien vendió, aunque la confirme un Supervisor; los reportes se agrupan por él.
                    PromotorId = hp.PromotorId,
                    MontoTotal = hp.Total,
                    MontoPagado = 0,
                    FechaEmision = DateTime.UtcNow,
                    FechaVencimiento = DateTime.UtcNow.AddDays(diasVencimiento),
                    Estado = ControlGestion.API.Models.EstadoCobranza.Pendiente
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return Ok(new { hp.Id, hp.Numero, hp.EstadoHP, hp.Total });
    }

    [HttpPost("{id}/anular")]
    [Permiso(Permisos.Ventas.AnularHp)]
    public async Task<IActionResult> Anular(int id, [FromBody] HojaPedidoAnularDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var hp = await _db.HojasPedido.FindAsync(id);
        if (hp == null) throw new NoEncontradoException("No se encontró la hoja de pedido.");
        if (hp.EstadoHP == EstadoHP.Anulada)
            throw new ReglaDeNegocioException("hp_ya_anulada", "La HP ya está anulada.");

        hp.EstadoHP = EstadoHP.Anulada;
        hp.ObservacionesAnulacion = dto.Motivo;
        hp.FechaAnulacion = DateTime.UtcNow;
        hp.AnuladoPorId = userId;

        await _db.SaveChangesAsync();
        return Ok(new { hp.Id, hp.EstadoHP });
    }
}
