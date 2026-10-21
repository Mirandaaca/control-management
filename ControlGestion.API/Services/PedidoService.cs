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

    public async Task<PedidoDTO> RegistrarAsync(RegistrarPedidoDTO dto)
    {
        // Un cliente de otra cartera responde lo mismo que uno que no existe, como en la HP: no se le confirma que existe.
        var cliente = await db.Clientes.VisiblesPara(usuario)
            .Where(c => c.Id == dto.ClienteId)
            .Select(c => new { c.Id, c.ZonaId, c.PromotorId, c.EstadoClienteId })
            .FirstOrDefaultAsync()
            ?? throw new ReglaDeNegocioException("cliente_no_existe", "No se encontró el cliente.");
        if (cliente.EstadoClienteId == EstadosDeCliente.Inactivo)
            throw new ReglaDeNegocioException("cliente_inactivo",
                "El cliente está inactivo: para registrarle un pedido, primero devuélvelo al ciclo de visitas.");

        var entrega = HoraBolivia.AUtc(dto.FechaEntregaEstimada!.Value);
        ExigirFutura(entrega);

        var pedido = new Pedido
        {
            ClienteId = cliente.Id,
            // El pedido es de la cartera del cliente (7.5), aunque lo registre la secretaria, y cuenta en su zona.
            PromotorId = cliente.PromotorId,
            RegistradoPorId = usuario.Id,
            ZonaId = cliente.ZonaId,
            FechaRegistro = Ahora,
            EstadoPedidoId = EstadosDePedido.Pendiente
        };
        await CopiarAsync(dto, pedido, entrega);
        // El número se pide después de validar: un pedido rechazado no gasta un número.
        pedido.Numero = await numerador.SiguienteDelAnioAsync(NumeradorService.Pedido);
        pedido.Historial.Add(Entrada(EstadosDePedido.Pendiente, "Pedido registrado"));

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();
        return await ObtenerAsync(pedido.Id);
    }

    /// <summary>
    /// Copia lo que se puede cambiar de un pedido y rearma sus líneas con el precio vigente de cada producto, que es el
    /// que muestra el formulario.
    /// </summary>
    private async Task CopiarAsync(ModificarPedidoDTO dto, Pedido pedido, DateTime entrega)
    {
        var idsDeProductos = dto.Detalles.Select(d => d.ProductoId).Distinct().ToList();
        var productos = await db.Productos
            .Where(p => idsDeProductos.Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id);
        if (productos.Count != idsDeProductos.Count)
            throw new ReglaDeNegocioException("producto_no_disponible", "Uno o más productos no existen o están inactivos.");

        pedido.Detalles.Clear();
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
        pedido.Descuento = dto.Descuento;
        pedido.Total = pedido.SubTotal - pedido.Descuento;
        if (pedido.Total <= 0)
            throw new ReglaDeNegocioException("total_invalido",
                $"El total del pedido debe ser mayor a cero. Subtotal: {pedido.SubTotal:F2}, Descuento: {pedido.Descuento:F2}, Total: {pedido.Total:F2}");

        pedido.FechaEntregaEstimada = entrega;
        pedido.DireccionEntrega = dto.DireccionEntrega.Trim();
        pedido.Observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim();
    }

    private void ExigirFutura(DateTime entregaUtc)
    {
        if (entregaUtc <= Ahora)
            throw new ReglaDeNegocioException("fecha_en_el_pasado", "La fecha y la hora de entrega ya pasaron: elige unas futuras.");
    }

    /// <summary>Una entrada del historial: quién, cuándo y con qué observación pasó el pedido a ese estado.</summary>
    private HistorialEstadoPedido Entrada(int estadoId, string? observacion) => new()
    {
        EstadoPedidoId = estadoId,
        UsuarioId = usuario.Id,
        FechaCambio = Ahora,
        Observacion = observacion
    };

    /// <summary>Pasa el pedido al estado siguiente: de Pendiente a En proceso, y de ahí a En ruta.</summary>
    public async Task<PedidoDTO> AvanzarAsync(int id, string? observacion)
    {
        var pedido = await BuscarAbiertoAsync(id);
        var siguiente = EstadosDePedido.Siguiente(pedido.EstadoPedidoId)
            ?? throw new ReglaDeNegocioException("transicion_no_permitida",
                "El pedido ya está en ruta: lo que sigue es registrar la entrega.");

        pedido.EstadoPedidoId = siguiente;
        pedido.Historial.Add(Entrada(siguiente, string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim()));
        await GuardarAsync("El pedido cambió mientras lo avanzabas. Vuelve a intentarlo.");
        return await ObtenerAsync(id);
    }

    /// <summary>Anular (1.1): el motivo queda en el historial. Un pedido entregado o ya anulado no se anula.</summary>
    public async Task<PedidoDTO> AnularAsync(int id, string motivo)
    {
        var pedido = await BuscarAbiertoAsync(id);
        pedido.EstadoPedidoId = EstadosDePedido.Anulado;
        pedido.Historial.Add(Entrada(EstadosDePedido.Anulado, motivo.Trim()));
        await GuardarAsync("El pedido cambió mientras lo anulabas. Vuelve a intentarlo.");
        return await ObtenerAsync(id);
    }

    /// <summary>Un pedido visible que todavía puede cambiar: ni entregado ni anulado.</summary>
    private async Task<Pedido> BuscarAbiertoAsync(int id)
    {
        var pedido = await Visibles.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NoEncontradoException("No se encontró el pedido.");
        if (pedido.EstadoPedidoId == EstadosDePedido.Entregado)
            throw new ReglaDeNegocioException("pedido_en_estado_final", $"El pedido {pedido.Numero} ya se entregó: no cambia más.");
        if (pedido.EstadoPedidoId == EstadosDePedido.Anulado)
            throw new ReglaDeNegocioException("pedido_en_estado_final", $"El pedido {pedido.Numero} está anulado: no cambia más.");
        return pedido;
    }

    // Otra petición cambió el pedido (o el stock, el cliente o la cobranza al entregar) entre que se leyó y se guardó.
    private async Task GuardarAsync(string mensajeDeConflicto)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictoException("conflicto_concurrencia", mensajeDeConflicto);
        }
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
