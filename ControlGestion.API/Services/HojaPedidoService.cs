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
public class HojaPedidoService(
    AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, InventarioService inventario, TimeProvider reloj)
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
        var cliente = await db.Clientes.FindAsync(dto.ClienteId)
            ?? throw new ReglaDeNegocioException("cliente_no_existe", "Cliente no encontrado.");

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
            ZonaId = dto.ZonaId,
            FechaRegistro = Ahora,
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

    public async Task<HojaPedidoDTO> ConfirmarAsync(int id)
    {
        var hp = await Visibles
            .Include(h => h.Detalles)
            .Include(h => h.Cliente)
            .FirstOrDefaultAsync(h => h.Id == id)
            ?? throw new NoEncontradoException("No se encontró la hoja de pedido.");
        if (hp.EstadoHP != EstadoHP.Borrador)
            throw new ReglaDeNegocioException("hp_no_es_borrador", "Solo se pueden confirmar HPs en estado Borrador.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var ahora = Ahora;
        hp.EstadoHP = EstadoHP.Confirmada;
        await inventario.DescontarStockPorHP(hp, usuario.Id);

        // La venta es una visita (5.2): la próxima se calcula con el parámetro general de días de re-visita (7.1).
        var cliente = hp.Cliente!;
        cliente.UltimaVisita = ahora;
        cliente.ProximaVisita = ahora.AddDays(await LeerDiasAsync(ClavesDeConfiguracion.DiasRevisita, ClavesDeConfiguracion.DiasRevisitaPorDefecto));
        // Un estado asignado a mano (Inactivo y otros) saca al cliente del ciclo: la venta no lo cambia.
        if (EstadosDeCliente.DelSistema.Contains(cliente.EstadoClienteId))
            cliente.EstadoClienteId = EstadosDeCliente.Activo;

        if (hp.TipoVenta == TipoVenta.Credito)
        {
            cliente.SaldoPendiente += hp.Total;
            var diasVencimiento = await LeerDiasAsync(ClavesDeConfiguracion.DiasVencimientoCredito,
                ClavesDeConfiguracion.DiasVencimientoCreditoPorDefecto);
            db.Cobranzas.Add(new Cobranza
            {
                Numero = await numerador.SiguienteDelAnioAsync(NumeradorService.Cobranza),
                ClienteId = hp.ClienteId,
                HojaPedidoId = hp.Id,
                // El responsable es quien vendió; los reportes se agrupan por él.
                PromotorId = hp.PromotorId,
                MontoTotal = hp.Total,
                MontoPagado = 0,
                FechaEmision = ahora,
                FechaVencimiento = ahora.AddDays(diasVencimiento),
                Estado = EstadoCobranza.Pendiente
            });
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await ObtenerAsync(id);
    }

    public async Task<HojaPedidoDTO> AnularAsync(int id, string motivo)
    {
        var hp = await Visibles.FirstOrDefaultAsync(h => h.Id == id)
            ?? throw new NoEncontradoException("No se encontró la hoja de pedido.");
        if (hp.EstadoHP == EstadoHP.Anulada)
            throw new ReglaDeNegocioException("hp_ya_anulada", "La HP ya está anulada.");

        hp.EstadoHP = EstadoHP.Anulada;
        hp.ObservacionesAnulacion = motivo.Trim();
        hp.FechaAnulacion = Ahora;
        hp.AnuladoPorId = usuario.Id;

        await db.SaveChangesAsync();
        return await ObtenerAsync(id);
    }

    private DateTime Ahora => reloj.GetUtcNow().UtcDateTime;

    private async Task<int> LeerDiasAsync(string clave, int porDefecto)
    {
        var config = await db.ConfiguracionesGenerales.FindAsync(clave);
        return int.TryParse(config?.Valor, out var dias) && dias > 0 ? dias : porDefecto;
    }
}
