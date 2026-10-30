import MapaUbicacion from '../MapaUbicacion';
import { Table, Td, Tr } from '../Table';
import { Alert, Btn, Modal } from '../ui';
import { formatearCantidad, formatearFecha, formatearMonto } from '../../utils/formato';
import EstadoCobranza from '../cobranzas/EstadoCobranza';
import EstadoHP from '../hojas/EstadoHP';
import EstadoPedido from './EstadoPedido';
import { ANULADO, CREDITO, ENTREGADO } from './estados';

// El div va directo dentro del <dl>: otro div en medio deja el dt y el dd fuera de la lista (axe: dlitem).
function Dato({ titulo, children, className = '' }) {
  return (
    <div className={`min-w-0 ${className}`}>
      <dt className="text-xs font-semibold text-slate-600">{titulo}</dt>
      <dd className="mt-0.5 text-sm text-slate-900 wrap-break-word">{children || '—'}</dd>
    </div>
  );
}

/**
 * El detalle de un pedido: sus datos, las líneas, la entrega (con la HP que nació de ella y dónde se entregó) y el
 * historial. Las acciones que el usuario puede hacer llegan en "acciones" y van al pie, junto a "Cerrar".
 */
export default function DetallePedido({ pedido, onCerrar, acciones }) {
  const anulacion = pedido?.estado.id === ANULADO
    ? [...pedido.historial].reverse().find((h) => h.estado.id === ANULADO)
    : null;
  const hp = pedido?.hojaPedido;

  return (
    <Modal
      abierto={pedido !== null}
      onCerrar={onCerrar}
      ancho="lg"
      titulo={pedido ? `${pedido.numero} · ${pedido.cliente.nombreCompleto}` : ''}
      pie={(
        <>
          <Btn variant="outline" size="md" onClick={onCerrar}>Cerrar</Btn>
          {acciones}
        </>
      )}
    >
      {pedido && (
        <div className="space-y-5">
          {anulacion && (
            <Alert type="warning">
              Anulado el {formatearFecha(anulacion.fecha, { hora: true })} por {anulacion.usuario.nombreCompleto}: {anulacion.observacion}
            </Alert>
          )}
          <dl className="grid gap-4 sm:grid-cols-3">
            <Dato titulo="Estado"><EstadoPedido estado={pedido.estado} /></Dato>
            <Dato titulo="Entrega">{formatearFecha(pedido.fechaEntregaEstimada, { hora: true })}</Dato>
            <Dato titulo="Asignado a">{pedido.repartidor?.nombreCompleto ?? 'Sin asignar'}</Dato>
            <Dato titulo="Dirección de entrega">{pedido.direccionEntrega}</Dato>
            <Dato titulo="Promotor del cliente">{pedido.promotor?.nombreCompleto ?? 'Sin promotor'}</Dato>
            <Dato titulo="Zona">{pedido.zona.nombre}</Dato>
            <Dato titulo="Registrado">
              {formatearFecha(pedido.fechaRegistro, { hora: true })} por {pedido.registradoPor.nombreCompleto}
            </Dato>
            <Dato titulo="Observaciones" className="sm:col-span-2">{pedido.observaciones}</Dato>
          </dl>

          <Table headers={['Producto', { texto: 'Cantidad', derecha: true }, { texto: 'Precio', derecha: true },
            { texto: 'Descuento', derecha: true }, { texto: 'Subtotal', derecha: true }]}>
            {pedido.detalles.map((d) => (
              <Tr key={d.id}>
                <Td>
                  <p className="font-medium">{d.descripcion}</p>
                  <p className="text-xs text-slate-600 font-mono" translate="no">{d.sku}</p>
                </Td>
                <Td className="text-right tabular-nums">{formatearCantidad(d.cantidad)}</Td>
                <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(d.precioUnitario)}</Td>
                <Td className="text-right tabular-nums whitespace-nowrap">{d.descuento ? formatearMonto(d.descuento) : '—'}</Td>
                <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(d.subtotal)}</Td>
              </Tr>
            ))}
          </Table>
          <dl className="ml-auto w-full max-w-xs space-y-1 text-sm tabular-nums">
            <div className="flex justify-between"><dt className="text-slate-600">Subtotal</dt><dd>{formatearMonto(pedido.subTotal)}</dd></div>
            <div className="flex justify-between"><dt className="text-slate-600">Descuento</dt><dd>{formatearMonto(pedido.descuento)}</dd></div>
            <div className="flex justify-between border-t border-slate-200 pt-1 font-semibold text-slate-900">
              <dt>Total</dt><dd>{formatearMonto(pedido.total)}</dd>
            </div>
          </dl>

          {pedido.estado.id === ENTREGADO && (
            <section aria-labelledby="titulo-entrega" className="space-y-3">
              <h3 id="titulo-entrega" className="text-sm font-semibold text-slate-900">Entrega</h3>
              <dl className="grid gap-4 sm:grid-cols-3">
                <Dato titulo="Entregado">{formatearFecha(pedido.fechaEntregaReal, { hora: true })}</Dato>
                <Dato titulo="Recibió">{pedido.recibidoPor}</Dato>
                <Dato titulo="Venta">
                  {hp && (
                    <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
                      {hp.numero}, {hp.tipoVenta === CREDITO ? 'a crédito' : 'al contado'}
                      <EstadoHP estado={hp.estadoHP} />
                    </span>
                  )}
                </Dato>
                {hp?.cobranza && (
                  <Dato titulo="Deuda">
                    <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
                      {hp.cobranza.numero}
                      <EstadoCobranza estado={hp.cobranza.estado} />
                    </span>
                  </Dato>
                )}
              </dl>
              {pedido.latitudEntrega != null && (
                <MapaUbicacion valor={{ latitud: pedido.latitudEntrega, longitud: pedido.longitudEntrega }} alto={220}
                  etiqueta="Mapa con el lugar donde se entregó el pedido" />
              )}
            </section>
          )}

          <section aria-labelledby="titulo-historial">
            <h3 id="titulo-historial" className="text-sm font-semibold text-slate-900 mb-2">Historial</h3>
            <ol className="space-y-3 border-l border-slate-200 pl-4">
              {pedido.historial.map((h) => (
                <li key={h.id} className="text-sm">
                  <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                    <EstadoPedido estado={h.estado} />
                    <span className="text-slate-600 tabular-nums">{formatearFecha(h.fecha, { hora: true })}</span>
                    <span className="text-slate-600">· {h.usuario.nombreCompleto}</span>
                  </div>
                  {h.observacion && <p className="mt-1 text-slate-800 wrap-break-word">{h.observacion}</p>}
                </li>
              ))}
            </ol>
          </section>
        </div>
      )}
    </Modal>
  );
}
