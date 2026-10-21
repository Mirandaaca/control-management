using System.Text.Json;
using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Pedidos (1.1 a 1.4). Cada usuario ve los de su cartera y los que tiene asignados, salvo con cartera completa.</summary>
public class PedidoService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, TimeProvider reloj)
{
    private IQueryable<Pedido> Visibles => db.Pedidos.VisiblesPara(usuario);

    public async Task<Pagina<PedidoListaDTO>> ListarAsync(FiltrosPedidoDTO filtros)
    {
        var consulta = Visibles.Buscar(filtros.Buscar);
        if (filtros.ClienteId.HasValue) consulta = consulta.Where(p => p.ClienteId == filtros.ClienteId);
        if (filtros.EstadoId.HasValue) consulta = consulta.Where(p => p.EstadoPedidoId == filtros.EstadoId);
        if (filtros.ZonaId.HasValue) consulta = consulta.Where(p => p.ZonaId == filtros.ZonaId);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(p => p.PromotorId == filtros.PromotorId);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(p => p.FechaEntregaEstimada >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(p => p.FechaEntregaEstimada < fin);
        }

        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderByDescending(p => p.FechaRegistro).ThenByDescending(p => p.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(PedidoConsultas.ComoLista)
            .ToListAsync();
        return new Pagina<PedidoListaDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    // Las líneas y el historial son dos colecciones: en consultas separadas no se multiplican entre sí (Query[20504]).
    public async Task<PedidoDTO> ObtenerAsync(int id) =>
        await Visibles.Where(p => p.Id == id).AsSplitQuery().Select(PedidoConsultas.ComoDetalle).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró el pedido.");

    public async Task<List<HistorialPedidoDTO>> HistorialAsync(int id)
    {
        if (!await Visibles.AnyAsync(p => p.Id == id))
            throw new NoEncontradoException("No se encontró el pedido.");
        return await db.HistorialEstadosPedido.Where(h => h.PedidoId == id)
            .OrderBy(h => h.Id).Select(PedidoConsultas.HistorialComoDTO).ToListAsync();
    }

    public Task<List<EstadoPedidoDTO>> EstadosAsync() =>
        db.EstadosPedido.OrderBy(e => e.Orden).Select(e => new EstadoPedidoDTO(e.Id, e.Nombre)).ToListAsync();

    public async Task<PedidoDTO> RegistrarAsync(PedidoCreateDTO dto)
    {
        var idsDeProductos = dto.Detalles.Select(d => d.ProductoId).Distinct().ToList();
        var productos = await db.Productos
            .Where(p => idsDeProductos.Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id);
        if (productos.Count != idsDeProductos.Count)
            throw new ReglaDeNegocioException("producto_no_disponible", "Uno o más productos no existen o están inactivos.");

        var ahora = Ahora;
        var pedido = new Pedido
        {
            Numero = await numerador.SiguienteDelAnioAsync(NumeradorService.Pedido),
            ClienteId = dto.ClienteId,
            PromotorId = usuario.Id,
            ZonaId = dto.ZonaId,
            HojaPedidoId = dto.HojaPedidoId,
            FechaRegistro = ahora,
            FechaEntregaEstimada = dto.FechaEntregaEstimada,
            EstadoPedidoId = EstadosDePedido.Pendiente,
            Descuento = dto.Descuento,
            Observaciones = dto.Observaciones
        };

        foreach (var item in dto.Detalles)
        {
            var producto = productos[item.ProductoId];
            pedido.Detalles.Add(new DetallePedido
            {
                ProductoId = item.ProductoId,
                Cantidad = item.Cantidad,
                PrecioUnitario = producto.Precio,
                Descuento = item.Descuento,
                Subtotal = (producto.Precio - item.Descuento) * item.Cantidad
            });
        }

        pedido.SubTotal = pedido.Detalles.Sum(d => d.Subtotal);
        pedido.Total = pedido.SubTotal - pedido.Descuento;
        pedido.Historial.Add(new HistorialEstadoPedido
        {
            EstadoPedidoId = EstadosDePedido.Pendiente,
            UsuarioId = usuario.Id,
            FechaCambio = ahora,
            Observacion = "Pedido creado"
        });

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        return await ObtenerAsync(pedido.Id);
    }

    // Hoy esta operación hace tres: avanzar, entregar y anular. Se separa en los pasos siguientes de la tarea 08.
    public async Task<PedidoDTO> CambiarEstadoAsync(int id, CambiarEstadoDTO dto)
    {
        var pedido = await Visibles.Include(p => p.EstadoPedido).Include(p => p.Detalles).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NoEncontradoException("No se encontró el pedido.");
        if (pedido.EstadoPedido!.EsEstadoFinal)
            throw new ReglaDeNegocioException("pedido_en_estado_final", "El pedido está en un estado final y no puede cambiar.");

        var transicionesPermitidas = JsonSerializer.Deserialize<List<int>>(pedido.EstadoPedido.PermiteTransicion) ?? [];
        if (!transicionesPermitidas.Contains(dto.NuevoEstadoId))
            throw new ReglaDeNegocioException("transicion_no_permitida", $"Transición no permitida desde '{pedido.EstadoPedido.Nombre}'.");

        var ahora = Ahora;
        pedido.EstadoPedidoId = dto.NuevoEstadoId;
        if (dto.NuevoEstadoId == EstadosDePedido.Entregado)
        {
            pedido.FechaEntregaReal = ahora;
            pedido.LatitudEntrega = dto.Latitud;
            pedido.LongitudEntrega = dto.Longitud;
            if (dto.CantidadEntregada.HasValue)
                foreach (var d in pedido.Detalles) d.CantidadEntregada = dto.CantidadEntregada.Value;
        }
        if (dto.NuevoEstadoId == EstadosDePedido.EnProceso && !string.IsNullOrEmpty(dto.Observacion))
            pedido.RepartidorId = usuario.Id;

        db.HistorialEstadosPedido.Add(new HistorialEstadoPedido
        {
            PedidoId = id,
            EstadoPedidoId = dto.NuevoEstadoId,
            UsuarioId = usuario.Id,
            FechaCambio = ahora,
            Observacion = dto.Observacion,
            LatitudCambio = dto.Latitud,
            LongitudCambio = dto.Longitud
        });

        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
}
