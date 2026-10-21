using System.Globalization;
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
public class PedidoService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, HojaPedidoService hojas,
    TimeProvider reloj)
{
    private IQueryable<Pedido> Visibles => db.Pedidos.VisiblesPara(usuario);

    public async Task<Pagina<PedidoListaDTO>> ListarAsync(FiltrosPedidoDTO filtros)
    {
        var consulta = Visibles.Buscar(filtros.Buscar);
        if (filtros.ClienteId.HasValue) consulta = consulta.Where(p => p.ClienteId == filtros.ClienteId);
        if (filtros.EstadoId.HasValue) consulta = consulta.Where(p => p.EstadoPedidoId == filtros.EstadoId);
        if (filtros.ZonaId.HasValue) consulta = consulta.Where(p => p.ZonaId == filtros.ZonaId);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(p => p.PromotorId == filtros.PromotorId);
        if (!string.IsNullOrEmpty(filtros.RepartidorId)) consulta = consulta.Where(p => p.RepartidorId == filtros.RepartidorId);
        if (filtros.SinAsignar) consulta = consulta.Where(p => p.RepartidorId == null);
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

    /// <summary>
    /// La bandeja del día (1.3): los pedidos que tengo asignados con entrega ese día de Bolivia (hoy, si no se elige
    /// otro), sin los entregados ni los anulados, por hora de entrega. No se pagina: son los de un promotor en un día.
    /// </summary>
    public Task<List<PedidoListaDTO>> BandejaAsync(DateOnly? dia)
    {
        var elDia = dia ?? HoraBolivia.Hoy(reloj);
        var inicio = HoraBolivia.InicioDelDiaUtc(elDia);
        var fin = HoraBolivia.FinDelDiaUtc(elDia);
        var usuarioId = usuario.Id;
        return Visibles
            .Where(p => p.RepartidorId == usuarioId && p.FechaEntregaEstimada >= inicio && p.FechaEntregaEstimada < fin
                && !EstadosDePedido.Finales.Contains(p.EstadoPedidoId))
            .OrderBy(p => p.FechaEntregaEstimada).ThenBy(p => p.Id)
            .Select(PedidoConsultas.ComoLista)
            .ToListAsync();
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

    /// <summary>Una fecha y hora para un texto del historial: dd/mm/aaaa hh:mm, en la hora de Bolivia.</summary>
    private static string EnBolivia(DateTime utc) =>
        HoraBolivia.DesdeUtc(utc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

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

    /// <summary>
    /// Modificar (1.1): todo menos el cliente, y solo mientras el pedido no salió a entregarse (Pendiente o En
    /// proceso). Para otro cliente se anula el pedido y se registra uno nuevo.
    /// </summary>
    public async Task<PedidoDTO> ModificarAsync(int id, ModificarPedidoDTO dto)
    {
        var pedido = await Visibles.Include(p => p.Detalles).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NoEncontradoException("No se encontró el pedido.");
        if (pedido.EstadoPedidoId is not (EstadosDePedido.Pendiente or EstadosDePedido.EnProceso))
        {
            var estado = pedido.EstadoPedidoId switch
            {
                EstadosDePedido.EnRuta => "está en ruta",
                EstadosDePedido.Entregado => "ya se entregó",
                _ => "está anulado",
            };
            throw new ReglaDeNegocioException("pedido_no_modificable",
                $"El pedido {pedido.Numero} {estado}: solo se modifica mientras está pendiente o en proceso.");
        }

        var entrega = HoraBolivia.AUtc(dto.FechaEntregaEstimada!.Value);
        // Un pedido atrasado se puede corregir sin tocar su fecha: la fecha se exige futura solo si cambió.
        if (entrega != pedido.FechaEntregaEstimada) ExigirFutura(entrega);
        await CopiarAsync(dto, pedido, entrega);
        pedido.Historial.Add(Entrada(pedido.EstadoPedidoId, "Pedido modificado"));
        await GuardarAsync("El pedido cambió mientras lo modificabas. Vuelve a intentarlo.");
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// Reprogramar (1.2): nueva fecha y hora de entrega, con motivo. El pedido vuelve a Pendiente y conserva al
    /// promotor que tenga asignado.
    /// </summary>
    public async Task<PedidoDTO> ReprogramarAsync(int id, ReprogramarPedidoDTO dto)
    {
        var pedido = await BuscarAbiertoAsync(id);
        var entrega = HoraBolivia.AUtc(dto.FechaEntregaEstimada!.Value);
        ExigirFutura(entrega);

        pedido.FechaEntregaEstimada = entrega;
        pedido.EstadoPedidoId = EstadosDePedido.Pendiente;
        pedido.Historial.Add(Entrada(EstadosDePedido.Pendiente,
            $"Entrega reprogramada para el {EnBolivia(entrega)}. Motivo: {dto.Motivo.Trim()}"));
        await GuardarAsync("El pedido cambió mientras lo reprogramabas. Vuelve a intentarlo.");
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// Distribución (1.3): asigna los pedidos a un promotor y a un día de reparto, sin cambiar su estado. Cada pedido
    /// pasa a ese día con su misma hora. Es todo o nada: si uno no se puede asignar, no se asigna ninguno.
    /// </summary>
    public async Task<List<PedidoListaDTO>> AsignarAsync(AsignarPedidosDTO dto)
    {
        // Promotor es quien recorre rutas, como en /usuarios/promotores: así no depende del nombre del rol.
        var promotor = await db.Users
            .Where(u => u.Id == dto.PromotorId && u.Activo)
            .ConPermiso(db, Permisos.Territorial.RecorrerRuta)
            .Select(u => new { u.Id, u.NombreCompleto })
            .FirstOrDefaultAsync()
            ?? throw new ReglaDeNegocioException("promotor_no_valido", "El promotor elegido no existe, está inactivo o no es promotor.");

        var ids = dto.PedidoIds.Distinct().ToList();
        var pedidos = await Visibles.Where(p => ids.Contains(p.Id)).OrderBy(p => p.Id).ToListAsync();
        if (pedidos.Count != ids.Count)
            throw new NoEncontradoException("No se encontró uno de los pedidos.");

        var dia = dto.Dia!.Value;
        foreach (var pedido in pedidos)
        {
            if (pedido.EstadoPedidoId == EstadosDePedido.Entregado)
                throw new ReglaDeNegocioException("pedido_en_estado_final", $"El pedido {pedido.Numero} ya se entregó: no se asigna.");
            if (pedido.EstadoPedidoId == EstadosDePedido.Anulado)
                throw new ReglaDeNegocioException("pedido_en_estado_final", $"El pedido {pedido.Numero} está anulado: no se asigna.");
            if (pedido.FechaEntregaEstimada is not { } actual)
                throw new ReglaDeNegocioException("pedido_sin_fecha",
                    $"El pedido {pedido.Numero} no tiene fecha de entrega: reprográmalo primero.");

            var hora = TimeOnly.FromDateTime(HoraBolivia.DesdeUtc(actual));
            var entrega = HoraBolivia.AUtc(dia.ToDateTime(hora));
            if (entrega <= Ahora)
                throw new ReglaDeNegocioException("fecha_en_el_pasado",
                    $"La entrega de {pedido.Numero} quedaría el {EnBolivia(entrega)}, que ya pasó: reprográmalo a otra hora.");

            pedido.RepartidorId = promotor.Id;
            pedido.FechaEntregaEstimada = entrega;
            pedido.Historial.Add(Entrada(pedido.EstadoPedidoId,
                $"Asignado a {promotor.NombreCompleto} para el {dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}."));
        }

        await GuardarAsync("Uno de los pedidos cambió mientras los asignabas. Vuelve a intentarlo.");
        return await Visibles.Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.FechaEntregaEstimada).ThenBy(p => p.Id)
            .Select(PedidoConsultas.ComoLista)
            .ToListAsync();
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

    /// <summary>
    /// Registrar la entrega (1.2): solo el promotor asignado, y solo desde En ruta. La entrega es la venta: crea y
    /// confirma una HP con los productos del pedido (stock, kardex, visita y, si es a crédito, la deuda), todo en una
    /// transacción. Se entrega el pedido completo: si falta stock de un producto, no se registra nada.
    /// </summary>
    public async Task<PedidoDTO> EntregarAsync(int id, EntregarPedidoDTO dto)
    {
        var pedido = await Visibles.Include(p => p.Detalles).Include(p => p.Cliente).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new NoEncontradoException("No se encontró el pedido.");
        if (pedido.EstadoPedidoId == EstadosDePedido.Entregado)
            throw new ReglaDeNegocioException("pedido_en_estado_final", $"El pedido {pedido.Numero} ya se entregó.");
        if (pedido.EstadoPedidoId == EstadosDePedido.Anulado)
            throw new ReglaDeNegocioException("pedido_en_estado_final", $"El pedido {pedido.Numero} está anulado: no se entrega.");
        if (pedido.EstadoPedidoId != EstadosDePedido.EnRuta)
            throw new ReglaDeNegocioException("pedido_no_esta_en_ruta",
                $"El pedido {pedido.Numero} todavía no salió a entregarse: primero tiene que pasar a En ruta.");
        if (pedido.RepartidorId == null)
            throw new ReglaDeNegocioException("pedido_sin_asignar", $"El pedido {pedido.Numero} no tiene un promotor asignado para entregarlo.");
        // La regla del dueño no es un permiso: ver el pedido (por la cartera) no alcanza para entregarlo.
        if (pedido.RepartidorId != usuario.Id)
            throw new ReglaDeNegocioException("pedido_de_otro_promotor", "Solo el promotor asignado registra la entrega.");
        if (pedido.Cliente!.EstadoClienteId == EstadosDeCliente.Inactivo)
            throw new ReglaDeNegocioException("cliente_inactivo",
                "El cliente está inactivo: no recibe la venta. Anula el pedido o devuelve al cliente al ciclo de visitas.");

        // La transacción incluye la HP y su número, la deuda y su número, el stock, el kardex, el cliente y el pedido.
        await using var tx = await db.Database.BeginTransactionAsync();
        var hp = await hojas.RegistrarVentaDeEntregaAsync(pedido, dto.TipoVenta!.Value, dto.Latitud!.Value, dto.Longitud!.Value,
            dto.PrecisionGPS);

        var recibidoPor = dto.RecibidoPor.Trim();
        pedido.EstadoPedidoId = EstadosDePedido.Entregado;
        pedido.FechaEntregaReal = Ahora;
        pedido.LatitudEntrega = dto.Latitud;
        pedido.LongitudEntrega = dto.Longitud;
        pedido.RecibidoPor = recibidoPor;
        pedido.HojaPedidoId = hp.Id;
        foreach (var linea in pedido.Detalles) linea.CantidadEntregada = linea.Cantidad;

        var observacion = $"Recibió {recibidoPor}. Venta {hp.Numero}, {(hp.TipoVenta == TipoVenta.Credito ? "a crédito" : "al contado")}.";
        if (!string.IsNullOrWhiteSpace(dto.Observacion)) observacion += $" {dto.Observacion.Trim()}";
        var entrada = Entrada(EstadosDePedido.Entregado, observacion);
        entrada.LatitudCambio = dto.Latitud;
        entrada.LongitudCambio = dto.Longitud;
        pedido.Historial.Add(entrada);

        await GuardarAsync("El pedido, el stock o el cliente cambiaron mientras registrabas la entrega. Vuelve a intentarlo.");
        await tx.CommitAsync();
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

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;
}
