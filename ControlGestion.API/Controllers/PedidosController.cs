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
using System.Text.Json;

namespace ControlGestion.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PedidosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NumeradorService _numerador;

    public PedidosController(AppDbContext db, NumeradorService numerador)
    {
        _db = db;
        _numerador = numerador;
    }

    [HttpGet]
    [Permiso(Permisos.Pedidos.ConsultarPedidos, Permisos.Pedidos.VerBandeja)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? clienteId,
        [FromQuery] int? estadoId,
        [FromQuery] string? promotorId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
    {
        var query = _db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Promotor)
            .Include(p => p.EstadoPedido)
            .Include(p => p.Zona)
            .AsQueryable();

        if (clienteId.HasValue) query = query.Where(p => p.ClienteId == clienteId);
        if (estadoId.HasValue) query = query.Where(p => p.EstadoPedidoId == estadoId);
        if (!string.IsNullOrEmpty(promotorId)) query = query.Where(p => p.PromotorId == promotorId);
        if (desde.HasValue) query = query.Where(p => p.FechaRegistro >= desde);
        if (hasta.HasValue) query = query.Where(p => p.FechaRegistro <= hasta);

        return Ok(await query.OrderByDescending(p => p.FechaRegistro).ToListAsync());
    }

    [HttpGet("{id}")]
    [Permiso(Permisos.Pedidos.ConsultarPedidos, Permisos.Pedidos.VerBandeja)]
    public async Task<IActionResult> GetById(int id)
    {
        var pedido = await _db.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Promotor)
            .Include(p => p.Repartidor)
            .Include(p => p.EstadoPedido)
            .Include(p => p.Zona)
            .Include(p => p.Detalles).ThenInclude(d => d.Producto)
            .Include(p => p.Historial).ThenInclude(h => h.EstadoPedido)
            .Include(p => p.Historial).ThenInclude(h => h.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id);

        return Ok(pedido ?? throw new NoEncontradoException("No se encontró el pedido."));
    }

    [HttpGet("estados")]
    public async Task<IActionResult> GetEstados() =>
        Ok(await _db.EstadosPedido.OrderBy(e => e.Orden).ToListAsync());

    [HttpPost]
    [Permiso(Permisos.Pedidos.RegistrarPedido)]
    public async Task<IActionResult> Create([FromBody] PedidoCreateDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var productos = await _db.Productos
            .Where(p => dto.Detalles.Select(d => d.ProductoId).Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id);

        if (productos.Count != dto.Detalles.Select(d => d.ProductoId).Distinct().Count())
            throw new ReglaDeNegocioException("producto_no_disponible", "Uno o más productos no existen o están inactivos.");

        var pedido = new Pedido
        {
            Numero = await _numerador.SiguienteDelAnioAsync(NumeradorService.Pedido),
            ClienteId = dto.ClienteId,
            PromotorId = userId,
            ZonaId = dto.ZonaId,
            HojaPedidoId = dto.HojaPedidoId,
            FechaEntregaEstimada = dto.FechaEntregaEstimada,
            EstadoPedidoId = 1,
            Descuento = dto.Descuento,
            Observaciones = dto.Observaciones
        };

        foreach (var item in dto.Detalles)
        {
            var prod = productos[item.ProductoId];
            var subtotal = (prod.Precio - item.Descuento) * item.Cantidad;
            pedido.Detalles.Add(new DetallePedido
            {
                ProductoId = item.ProductoId,
                Cantidad = item.Cantidad,
                PrecioUnitario = prod.Precio,
                Descuento = item.Descuento,
                Subtotal = subtotal
            });
        }

        pedido.SubTotal = pedido.Detalles.Sum(d => d.Subtotal);
        pedido.Total = pedido.SubTotal - pedido.Descuento;

        pedido.Historial.Add(new HistorialEstadoPedido
        {
            EstadoPedidoId = 1,
            UsuarioId = userId,
            Observacion = "Pedido creado"
        });

        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = pedido.Id }, pedido);
    }

    // Hoy este endpoint hace las tres operaciones; se separa en la tarea de Pedidos.
    [HttpPost("{id}/estado")]
    [Permiso(Permisos.Pedidos.CambiarEstado, Permisos.Pedidos.RegistrarEntrega, Permisos.Pedidos.AnularPedido)]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoDTO dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var pedido = await _db.Pedidos
            .Include(p => p.EstadoPedido)
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pedido == null) throw new NoEncontradoException("No se encontró el pedido.");
        if (pedido.EstadoPedido!.EsEstadoFinal)
            throw new ReglaDeNegocioException("pedido_en_estado_final", "El pedido está en un estado final y no puede cambiar.");

        var transicionesPermitidas = JsonSerializer.Deserialize<List<int>>(pedido.EstadoPedido.PermiteTransicion) ?? new();
        if (!transicionesPermitidas.Contains(dto.NuevoEstadoId))
            throw new ReglaDeNegocioException("transicion_no_permitida", $"Transición no permitida desde '{pedido.EstadoPedido.Nombre}'.");

        var nuevoEstado = await _db.EstadosPedido.FindAsync(dto.NuevoEstadoId);
        if (nuevoEstado == null) throw new ReglaDeNegocioException("estado_no_existe", "Estado destino no existe.");

        pedido.EstadoPedidoId = dto.NuevoEstadoId;

        if (dto.NuevoEstadoId == 4) // Entregado
        {
            pedido.FechaEntregaReal = DateTime.UtcNow;
            pedido.LatitudEntrega = dto.Latitud;
            pedido.LongitudEntrega = dto.Longitud;

            if (dto.CantidadEntregada.HasValue)
                foreach (var d in pedido.Detalles) d.CantidadEntregada = dto.CantidadEntregada.Value;
        }

        if (dto.NuevoEstadoId == 2 && !string.IsNullOrEmpty(dto.Observacion))
            pedido.RepartidorId = userId;

        _db.HistorialEstadosPedido.Add(new HistorialEstadoPedido
        {
            PedidoId = id,
            EstadoPedidoId = dto.NuevoEstadoId,
            UsuarioId = userId,
            Observacion = dto.Observacion,
            LatitudCambio = dto.Latitud,
            LongitudCambio = dto.Longitud
        });

        await _db.SaveChangesAsync();
        return Ok(new { pedido.Id, pedido.Numero, Estado = nuevoEstado.Nombre });
    }

    [HttpGet("{id}/historial")]
    [Permiso(Permisos.Pedidos.ConsultarPedidos, Permisos.Pedidos.VerBandeja)]
    public async Task<IActionResult> GetHistorial(int id)
    {
        var historial = await _db.HistorialEstadosPedido
            .Include(h => h.EstadoPedido)
            .Include(h => h.Usuario)
            .Where(h => h.PedidoId == id)
            .OrderByDescending(h => h.FechaCambio)
            .ToListAsync();

        return Ok(historial);
    }
}
