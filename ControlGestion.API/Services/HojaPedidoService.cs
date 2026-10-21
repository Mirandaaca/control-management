using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

/// <summary>Hojas de pedido (2.1): la venta en campo. Cada usuario ve solo las de su cartera, salvo con cartera completa.</summary>
public class HojaPedidoService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador,
    InventarioService inventario, CobranzaService cobranzas, TimeProvider reloj)
{
    private IQueryable<HojaPedido> Visibles => db.HojasPedido.VisiblesPara(usuario);

    public async Task<Pagina<HojaPedidoListaDTO>> ListarAsync(FiltrosHojaPedidoDTO filtros)
    {
        var consulta = Visibles.Buscar(filtros.Buscar);
        if (filtros.ClienteId.HasValue) consulta = consulta.Where(h => h.ClienteId == filtros.ClienteId);
        if (filtros.Estado.HasValue) consulta = consulta.Where(h => h.EstadoHP == filtros.Estado);
        if (filtros.TipoVenta.HasValue) consulta = consulta.Where(h => h.TipoVenta == filtros.TipoVenta);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(h => h.PromotorId == filtros.PromotorId);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(h => h.FechaRegistro >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(h => h.FechaRegistro < fin);
        }

        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderByDescending(h => h.FechaRegistro).ThenByDescending(h => h.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(HojaPedidoConsultas.ComoLista)
            .ToListAsync();
        return new Pagina<HojaPedidoListaDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    public async Task<HojaPedidoDTO> ObtenerAsync(int id) =>
        await Visibles.Where(h => h.Id == id).Select(HojaPedidoConsultas.ComoDetalle).FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró la hoja de pedido.");

    public async Task<HojaPedidoDTO> RegistrarAsync(RegistrarHojaPedidoDTO dto)
    {
        // Un cliente de otra cartera responde lo mismo que uno que no existe: no se le confirma que existe.
        var cliente = await db.Clientes.VisiblesPara(usuario)
            .Where(c => c.Id == dto.ClienteId)
            .Select(c => new { c.Id, c.ZonaId, c.EstadoClienteId })
            .FirstOrDefaultAsync()
            ?? throw new ReglaDeNegocioException("cliente_no_existe", "No se encontró el cliente.");
        if (cliente.EstadoClienteId == EstadosDeCliente.Inactivo)
            throw new ReglaDeNegocioException("cliente_inactivo",
                "El cliente está inactivo: para venderle, primero devuélvelo al ciclo de visitas.");

        var idsDeProductos = dto.Detalles.Select(d => d.ProductoId).Distinct().ToList();
        var productos = await db.Productos
            .Where(p => idsDeProductos.Contains(p.Id) && p.Activo)
            .ToDictionaryAsync(p => p.Id);
        if (productos.Count != idsDeProductos.Count)
            throw new ReglaDeNegocioException("producto_no_disponible", "Uno o más productos no existen o están inactivos.");

        var hp = new HojaPedido
        {
            Numero = await numerador.SiguienteDelAnioAsync(NumeradorService.HojaPedido),
            ClienteId = cliente.Id,
            PromotorId = usuario.Id,
            // La venta cuenta en la zona del cliente: así los reportes por zona no dependen de lo que se eligió.
            ZonaId = cliente.ZonaId,
            FechaRegistro = Ahora,
            Latitud = dto.Latitud!.Value,
            Longitud = dto.Longitud!.Value,
            PrecisionGPS = dto.PrecisionGPS,
            TipoVenta = dto.TipoVenta,
            Descuento = dto.Descuento,
            EstadoHP = EstadoHP.Borrador
        };

        foreach (var item in dto.Detalles)
        {
            var producto = productos[item.ProductoId];
            hp.Detalles.Add(new DetalleHP
            {
                ProductoId = item.ProductoId,
                Cantidad = item.Cantidad,
                PrecioUnitario = producto.Precio,
                Descuento = item.Descuento,
                Subtotal = (producto.Precio - item.Descuento) * item.Cantidad
            });
        }

        hp.SubTotal = hp.Detalles.Sum(d => d.Subtotal);
        hp.Total = hp.SubTotal - hp.Descuento;
        if (hp.Total <= 0)
            throw new ReglaDeNegocioException("total_invalido",
                $"El total de la HP debe ser mayor a cero. Subtotal: {hp.SubTotal:F2}, Descuento: {hp.Descuento:F2}, Total: {hp.Total:F2}");

        db.HojasPedido.Add(hp);
        await db.SaveChangesAsync();
        return await ObtenerAsync(hp.Id);
    }

    /// <summary>
    /// Cerrar la venta (2.1): descuenta el stock, registra la visita y, si es a crédito, crea la deuda. Todo o nada, en
    /// una transacción. Solo la confirma quien la registró.
    /// </summary>
    public async Task<HojaPedidoDTO> ConfirmarAsync(int id)
    {
        var hp = await Visibles
            .Include(h => h.Detalles)
            .Include(h => h.Cliente)
            .FirstOrDefaultAsync(h => h.Id == id)
            ?? throw new NoEncontradoException("No se encontró la hoja de pedido.");
        // La regla del dueño no es un permiso: ver la HP (cartera completa) no alcanza para confirmarla.
        if (hp.PromotorId != usuario.Id)
            throw new ReglaDeNegocioException("hp_de_otro_promotor", "Solo el promotor que registró la HP puede confirmarla.");
        if (hp.EstadoHP != EstadoHP.Borrador)
            throw new ReglaDeNegocioException("hp_no_es_borrador", "Solo se pueden confirmar HPs en estado Borrador.");

        // La transacción incluye el número de la cobranza, que el numerador pide con su propia sentencia.
        await using var tx = await db.Database.BeginTransactionAsync();
        await CerrarVentaAsync(hp);
        await GuardarAsync("La HP, el stock o el cliente cambiaron mientras confirmabas. Vuelve a intentarlo.");
        await tx.CommitAsync();
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// Cierra la venta: la HP queda confirmada, se descuenta el stock con su salida en el kardex, se registra la visita
    /// y, si es a crédito, nace la deuda. La HP ya tiene que estar guardada (el kardex y la deuda usan su id) y traer
    /// sus líneas y su cliente. No guarda: lo hace quien llama, dentro de su transacción.
    /// </summary>
    private async Task CerrarVentaAsync(HojaPedido hp)
    {
        var ahora = Ahora;
        hp.EstadoHP = EstadoHP.Confirmada;
        await inventario.DescontarPorVentaAsync(hp);

        // La venta es una visita (5.2): la próxima se calcula con el parámetro general de días de re-visita (7.1).
        var cliente = hp.Cliente!;
        cliente.UltimaVisita = ahora;
        cliente.ProximaVisita = ahora.AddDays(await db.ConfiguracionesGenerales.DiasAsync(
            ClavesDeConfiguracion.DiasRevisita, ClavesDeConfiguracion.DiasRevisitaPorDefecto));
        // Un estado asignado a mano (Inactivo y otros) saca al cliente del ciclo: la venta no lo cambia.
        if (EstadosDeCliente.DelSistema.Contains(cliente.EstadoClienteId))
            cliente.EstadoClienteId = EstadosDeCliente.Activo;

        if (hp.TipoVenta == TipoVenta.Credito)
            await cobranzas.CrearPorVentaAsync(hp);
    }

    /// <summary>
    /// Anular (2.1, H2). Una HP confirmada devuelve su stock y anula su deuda, salvo que la deuda ya tenga pagos. Un
    /// borrador no movió nada y solo cambia de estado. La visita que se registró al confirmar se conserva.
    /// </summary>
    public async Task<HojaPedidoDTO> AnularAsync(int id, string motivo)
    {
        var hp = await Visibles.Include(h => h.Detalles).FirstOrDefaultAsync(h => h.Id == id)
            ?? throw new NoEncontradoException("No se encontró la hoja de pedido.");
        if (hp.EstadoHP == EstadoHP.Anulada)
            throw new ReglaDeNegocioException("hp_ya_anulada", "La HP ya está anulada.");

        if (hp.EstadoHP == EstadoHP.Confirmada)
        {
            await cobranzas.AnularPorHojaAsync(hp);
            await inventario.DevolverPorAnulacionAsync(hp);
        }

        hp.EstadoHP = EstadoHP.Anulada;
        hp.ObservacionesAnulacion = motivo.Trim();
        hp.FechaAnulacion = Ahora;
        hp.AnuladoPorId = usuario.Id;

        // Un solo SaveChanges: la HP, el stock, el kardex, la cobranza y el saldo se guardan juntos o nada.
        await GuardarAsync("La HP, el stock o su cobranza cambiaron mientras anulabas. Vuelve a intentarlo.");
        return await ObtenerAsync(id);
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    // Otra petición cambió la HP, el inventario, el cliente o la cobranza entre que se leyeron y se guardaron.
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
}
