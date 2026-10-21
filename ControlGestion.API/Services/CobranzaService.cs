using ControlGestion.API.Configuracion;
using ControlGestion.API.Data;
using ControlGestion.API.Data.Consultas;
using ControlGestion.API.DTOs;
using ControlGestion.API.Excepciones;
using ControlGestion.API.Models;
using ControlGestion.API.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Services;

public class CobranzaService(AppDbContext db, IUsuarioActual usuario, NumeradorService numerador, EvidenciaService evidencias,
    TimeProvider reloj)
{
    public async Task<Pagina<CobranzaResumenDTO>> ListarAsync(FiltrosCobranzaDTO filtros)
    {
        var consulta = Filtrar(filtros);
        var total = await consulta.CountAsync();
        var items = await consulta
            .OrderByDescending(c => c.FechaEmision).ThenByDescending(c => c.Id)
            .Skip((filtros.Pagina - 1) * filtros.Tamano)
            .Take(filtros.Tamano)
            .Select(CobranzaConsultas.ComoResumen)
            .ToListAsync();
        return new Pagina<CobranzaResumenDTO>(items, total, filtros.Pagina, filtros.Tamano);
    }

    /// <summary>Los indicadores de la pantalla (3.2), con los mismos filtros que la lista.</summary>
    public async Task<ResumenCobranzasDTO> ResumenAsync(FiltrosCobranzaDTO filtros)
    {
        var abiertas = Filtrar(filtros).Where(c => EstadosAbiertos.Contains(c.Estado));
        return new ResumenCobranzasDTO(
            await abiertas.SumAsync(c => c.MontoTotal - c.MontoPagado),
            await abiertas.CountAsync(c => c.Estado != EstadoCobranza.Vencida),
            await abiertas.CountAsync(c => c.Estado == EstadoCobranza.Vencida));
    }

    private IQueryable<Cobranza> Filtrar(FiltrosCobranzaDTO filtros)
    {
        var consulta = db.Cobranzas.VisiblesPara(usuario).Buscar(filtros.Buscar);

        if (filtros.ClienteId.HasValue) consulta = consulta.Where(c => c.ClienteId == filtros.ClienteId);
        if (filtros.Estado.HasValue) consulta = consulta.Where(c => c.Estado == filtros.Estado);
        if (!string.IsNullOrEmpty(filtros.PromotorId)) consulta = consulta.Where(c => c.PromotorId == filtros.PromotorId);
        if (filtros.Desde is { } desde)
        {
            var inicio = HoraBolivia.InicioDelDiaUtc(desde);
            consulta = consulta.Where(c => c.FechaEmision >= inicio);
        }
        if (filtros.Hasta is { } hasta)
        {
            var fin = HoraBolivia.FinDelDiaUtc(hasta);
            consulta = consulta.Where(c => c.FechaEmision < fin);
        }
        return consulta;
    }

    public async Task<CobranzaDetalleDTO> ObtenerAsync(int id) =>
        await db.Cobranzas.VisiblesPara(usuario)
            .Where(c => c.Id == id)
            .Select(CobranzaConsultas.ComoDetalle)
            .FirstOrDefaultAsync()
        ?? throw new NoEncontradoException("No se encontró la cobranza.");

    public async Task<PagoRegistradoDTO> RegistrarPagoAsync(int id, RegistrarPagoDTO dto)
    {
        // A un Promotor, la cobranza de un cliente ajeno le responde "no existe": ni siquiera se le confirma que está.
        if (!await db.Cobranzas.VisiblesPara(usuario).AnyAsync(c => c.Id == id))
            throw new NoEncontradoException("No se encontró la cobranza.");

        // Idempotencia: si la app reintenta un pago que ya se guardó, se devuelve ese pago y no se crea otro.
        // Va antes de validar el estado, para que reintentar el pago que cerró la deuda no responda 400.
        if (dto.ClaveIdempotencia is { } clave && await PagoYaRegistradoAsync(id, clave) is { } repetido)
            return repetido;

        var cobranza = await db.Cobranzas.Include(c => c.Cliente).FirstAsync(c => c.Id == id);
        if (cobranza.Estado is EstadoCobranza.Pagada or EstadoCobranza.Incobrable or EstadoCobranza.Anulada)
            throw new ReglaDeNegocioException("cobranza_cerrada", "La cobranza ya está cerrada.");

        var saldo = Math.Max(0, cobranza.MontoTotal - cobranza.MontoPagado);
        if (saldo <= 0)
            throw new ReglaDeNegocioException("cobranza_sin_saldo", "Esta cobranza ya no tiene saldo pendiente.");
        if (dto.Monto <= 0 || dto.Monto > saldo)
            throw new ReglaDeNegocioException("monto_invalido", $"Monto inválido. Saldo pendiente: {saldo:F2}");

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var pago = new PagoCobranza
        {
            CobranzaId = id,
            RegistradoPorId = usuario.Id,
            Monto = dto.Monto,
            MetodoPago = dto.MetodoPago,
            FechaPago = ahora,
            Referencia = string.IsNullOrWhiteSpace(dto.Referencia) ? null : dto.Referencia.Trim(),
            Observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim(),
            ClaveIdempotencia = dto.ClaveIdempotencia
        };

        cobranza.MontoPagado += dto.Monto;
        if (cobranza.MontoPagado >= cobranza.MontoTotal)
        {
            cobranza.Estado = EstadoCobranza.Pagada;
            cobranza.FechaPagoCompleto = ahora;
        }
        else
        {
            cobranza.Estado = EstadoCobranza.Parcial;
        }

        db.PagosCobranza.Add(pago);
        await RecalcularSaldoDelClienteAsync(cobranza);

        // SaveChanges guarda el pago, la cobranza y el cliente en una sola transacción: si algo falla, no se guarda nada.
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            db.ChangeTracker.Clear();

            // Dos peticiones con la misma clave a la vez: el índice único frena la segunda.
            if (dto.ClaveIdempotencia is { } claveRepetida && await PagoYaRegistradoAsync(id, claveRepetida) is { } ganador)
                return ganador;

            // Otro pago modificó la cobranza, o el saldo del cliente, entre que los leímos y los guardamos.
            if (ex is DbUpdateConcurrencyException)
                throw new ConflictoException("conflicto_concurrencia",
                    "La cobranza o el saldo del cliente cambió mientras registrabas el pago. Vuelve a intentarlo.");
            throw;
        }

        return new PagoRegistradoDTO(pago.Id, cobranza.Estado, cobranza.MontoPagado, cobranza.MontoTotal - cobranza.MontoPagado);
    }

    public async Task<CobranzaDetalleDTO> MarcarIncobrableAsync(int id, string observacion)
    {
        var cobranza = await db.Cobranzas.VisiblesPara(usuario).Include(c => c.Cliente).FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NoEncontradoException("No se encontró la cobranza.");
        if (cobranza.Estado == EstadoCobranza.Pagada)
            throw new ReglaDeNegocioException("cobranza_pagada", "No se puede marcar como incobrable una cobranza pagada.");
        if (cobranza.Estado == EstadoCobranza.Anulada)
            throw new ReglaDeNegocioException("cobranza_anulada", "La cobranza está anulada: su venta se anuló.");

        cobranza.Estado = EstadoCobranza.Incobrable;
        cobranza.Observaciones = observacion;
        await RecalcularSaldoDelClienteAsync(cobranza);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictoException("conflicto_concurrencia",
                "La cobranza cambió mientras la marcabas como incobrable. Vuelve a intentarlo.");
        }

        return await ObtenerAsync(id);
    }

    /// <summary>
    /// Anular un pago (3.1): queda marcado con su motivo y deja de contar. La cobranza vuelve a calcular lo pagado y
    /// su estado, y el cliente su saldo, todo en un solo guardado.
    /// </summary>
    public async Task<CobranzaDetalleDTO> AnularPagoAsync(int id, int pagoId, string motivo)
    {
        var cobranza = await db.Cobranzas.VisiblesPara(usuario).Include(c => c.Cliente).Include(c => c.Pagos)
            .FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NoEncontradoException("No se encontró la cobranza.");
        var pago = cobranza.Pagos.FirstOrDefault(p => p.Id == pagoId)
            ?? throw new NoEncontradoException("No se encontró el pago.");
        if (pago.Anulado)
            throw new ReglaDeNegocioException("pago_ya_anulado", "El pago ya está anulado.");

        pago.Anulado = true;
        pago.MotivoAnulacion = motivo.Trim();
        cobranza.MontoPagado = cobranza.Pagos.Where(p => !p.Anulado).Sum(p => p.Monto);
        // Incobrable y Anulada son decisiones tomadas a mano: anular un pago no las cambia.
        if (cobranza.Estado is not (EstadoCobranza.Incobrable or EstadoCobranza.Anulada))
        {
            cobranza.FechaPagoCompleto = null;
            cobranza.Estado = cobranza.FechaVencimiento < InicioDeHoyUtc ? EstadoCobranza.Vencida
                : cobranza.MontoPagado > 0 ? EstadoCobranza.Parcial
                : EstadoCobranza.Pendiente;
        }
        await RecalcularSaldoDelClienteAsync(cobranza);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictoException("conflicto_concurrencia",
                "La cobranza o el saldo del cliente cambió mientras anulabas el pago. Vuelve a intentarlo.");
        }
        return await ObtenerAsync(id);
    }

    /// <summary>
    /// El comprobante de un pago (3.1): una foto opcional, que se sube aparte del pago para no tocar su idempotencia. Uno
    /// nuevo reemplaza al anterior, cuyo archivo se borra después de guardar. Un pago anulado no recibe comprobante.
    /// </summary>
    public async Task<CobranzaDetalleDTO> AdjuntarComprobanteAsync(int id, int pagoId, ArchivoRecibido archivo)
    {
        if (!await db.Cobranzas.VisiblesPara(usuario).AnyAsync(c => c.Id == id))
            throw new NoEncontradoException("No se encontró la cobranza.");
        var pago = await db.PagosCobranza.Include(p => p.Comprobante).FirstOrDefaultAsync(p => p.Id == pagoId && p.CobranzaId == id)
            ?? throw new NoEncontradoException("No se encontró el pago.");
        if (pago.Anulado)
            throw new ReglaDeNegocioException("pago_anulado", "El pago está anulado: no recibe comprobante.");

        var nuevo = await evidencias.GuardarFotoAsync(archivo);
        var anterior = pago.Comprobante;
        try
        {
            pago.Comprobante = nuevo;
            if (anterior != null) db.Evidencias.Remove(anterior);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            evidencias.BorrarArchivo(nuevo.RutaAlmacenamiento);
            // Dos comprobantes a la vez para el mismo pago: el segundo ya no encuentra el anterior que quería reemplazar.
            if (ex is DbUpdateConcurrencyException)
                throw new ConflictoException("conflicto_concurrencia",
                    "El comprobante cambió mientras subías el nuevo. Vuelve a intentarlo.");
            throw;
        }

        if (anterior != null) evidencias.BorrarArchivo(anterior.RutaAlmacenamiento);
        return await ObtenerAsync(id);
    }

    /// <summary>Una cobranza vence cuando termina el día de su vencimiento en Bolivia, igual que en el proceso de mora.</summary>
    private DateTime InicioDeHoyUtc => HoraBolivia.InicioDelDiaUtc(HoraBolivia.Hoy(reloj));

    /// <summary>
    /// La deuda de una venta a crédito (2.1): vence a los días de DiasVencimientoCredito y queda a nombre del promotor
    /// que vendió, aunque otro la cobre. La HP tiene que traer a su cliente. No guarda: lo hace quien confirma la HP.
    /// </summary>
    public async Task CrearPorVentaAsync(HojaPedido hp)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var dias = await db.ConfiguracionesGenerales.DiasAsync(ClavesDeConfiguracion.DiasVencimientoCredito,
            ClavesDeConfiguracion.DiasVencimientoCreditoPorDefecto);
        var cobranza = new Cobranza
        {
            Numero = await numerador.SiguienteDelAnioAsync(NumeradorService.Cobranza),
            ClienteId = hp.ClienteId,
            Cliente = hp.Cliente,
            HojaPedidoId = hp.Id,
            PromotorId = hp.PromotorId,
            MontoTotal = hp.Total,
            FechaEmision = ahora,
            FechaVencimiento = ahora.AddDays(dias),
            Estado = EstadoCobranza.Pendiente
        };
        db.Cobranzas.Add(cobranza);
        await RecalcularSaldoDelClienteAsync(cobranza);
    }

    /// <summary>
    /// Al anular una HP confirmada, su deuda pasa a Anulada y deja de contar en el saldo (2.1). Si ya tiene pagos no se
    /// anula: primero hay que anular esos pagos. Una venta al contado no tiene cobranza. No guarda.
    /// </summary>
    public async Task AnularPorHojaAsync(HojaPedido hp)
    {
        var cobranza = await db.Cobranzas.Include(c => c.Cliente).Include(c => c.Pagos)
            .FirstOrDefaultAsync(c => c.HojaPedidoId == hp.Id);
        if (cobranza == null) return;
        if (cobranza.Pagos.Any(p => !p.Anulado))
            throw new ReglaDeNegocioException("cobranza_con_pagos",
                $"No se puede anular: su cobranza {cobranza.Numero} ya tiene pagos. Primero hay que anular esos pagos.");

        cobranza.Estado = EstadoCobranza.Anulada;
        await RecalcularSaldoDelClienteAsync(cobranza);
    }

    /// <summary>
    /// El saldo del cliente es lo que debe en sus cobranzas abiertas. Se recalcula completo en vez de restar el
    /// pago, así un saldo que ya estaba mal (los pagos parciales no lo bajaban, T4.04) se corrige solo.
    /// </summary>
    private async Task RecalcularSaldoDelClienteAsync(Cobranza cambiada)
    {
        var deLasOtras = await db.Cobranzas
            .Where(c => c.ClienteId == cambiada.ClienteId && c.Id != cambiada.Id && EstadosAbiertos.Contains(c.Estado))
            .SumAsync(c => c.MontoTotal - c.MontoPagado);
        var deEsta = EstadosAbiertos.Contains(cambiada.Estado) ? cambiada.MontoTotal - cambiada.MontoPagado : 0m;

        cambiada.Cliente!.SaldoPendiente = deLasOtras + deEsta;
    }

    private static readonly EstadoCobranza[] EstadosAbiertos =
        [EstadoCobranza.Pendiente, EstadoCobranza.Parcial, EstadoCobranza.Vencida];

    // Devuelve el pago si la clave ya se usó en esta cobranza, null si es nueva, y 409 si se usó en otra.
    private async Task<PagoRegistradoDTO?> PagoYaRegistradoAsync(int cobranzaId, Guid clave)
    {
        var existente = await db.PagosCobranza
            .Where(p => p.ClaveIdempotencia == clave)
            .Select(p => new { p.Id, p.CobranzaId, p.Cobranza!.Estado, p.Cobranza.MontoTotal, p.Cobranza.MontoPagado })
            .FirstOrDefaultAsync();

        if (existente == null) return null;
        if (existente.CobranzaId != cobranzaId)
            throw new ConflictoException("clave_idempotencia_usada", "Esa clave de idempotencia ya se usó en otra cobranza.");

        return new PagoRegistradoDTO(existente.Id, existente.Estado, existente.MontoPagado, existente.MontoTotal - existente.MontoPagado);
    }
}
