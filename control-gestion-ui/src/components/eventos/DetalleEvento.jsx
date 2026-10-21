import MapaUbicacion from '../MapaUbicacion';
import { Btn, Modal } from '../ui';
import { formatearFecha } from '../../utils/formato';
import EstadoEvento from './EstadoEvento';
import { PRIORIDADES, RESUELTO } from './estados';

function Dato({ titulo, children }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-semibold text-slate-600">{titulo}</dt>
      <dd className="mt-0.5 text-sm text-slate-900 wrap-break-word">{children || '—'}</dd>
    </div>
  );
}

/**
 * El detalle de un evento: sus datos, la descripción, la solución (si está resuelto), dónde pasó y sus fotos. Las fotos
 * llegan en "fotos" y las acciones que el usuario puede hacer, en "acciones", que van al pie junto a "Cerrar".
 */
export default function DetalleEvento({ evento, onCerrar, acciones, fotos }) {
  return (
    <Modal
      abierto={evento !== null}
      onCerrar={onCerrar}
      ancho="lg"
      titulo={evento ? `${evento.numero} · ${evento.tipo.nombre}` : ''}
      pie={(
        <>
          <Btn variant="outline" size="md" onClick={onCerrar}>Cerrar</Btn>
          {acciones}
        </>
      )}
    >
      {evento && (
        <div className="space-y-5">
          <dl className="grid gap-4 sm:grid-cols-3">
            <Dato titulo="Estado"><EstadoEvento estado={evento.estado} /></Dato>
            <Dato titulo="Prioridad del tipo">{PRIORIDADES[evento.tipo.nivelPrioridad]}</Dato>
            <Dato titulo="Fecha">{formatearFecha(evento.fechaEvento, { hora: true })}</Dato>
            <Dato titulo="Cliente">{evento.cliente?.nombreCompleto}</Dato>
            <Dato titulo="Pedido"><span translate="no">{evento.pedido?.numero}</span></Dato>
            <Dato titulo="Zona">{evento.zona?.nombre}</Dato>
            <Dato titulo="Registró">{evento.promotor.nombreCompleto}</Dato>
          </dl>

          <section aria-labelledby="titulo-descripcion">
            <h3 id="titulo-descripcion" className="text-sm font-semibold text-slate-900 mb-1">Qué pasó</h3>
            <p className="text-sm text-slate-800 whitespace-pre-line wrap-break-word">{evento.descripcion}</p>
          </section>

          {evento.estado === RESUELTO && (
            <section aria-labelledby="titulo-solucion" className="rounded-lg border border-marca-200 bg-marca-50 p-4">
              <h3 id="titulo-solucion" className="text-sm font-semibold text-marca-800 mb-1">Solución</h3>
              <p className="text-sm text-slate-900 whitespace-pre-line wrap-break-word">{evento.solucion}</p>
              <p className="mt-2 text-xs text-slate-700 tabular-nums">
                Resuelto el {formatearFecha(evento.fechaResolucion, { hora: true })} por {evento.resueltoPor?.nombreCompleto ?? '—'}
              </p>
            </section>
          )}

          {fotos}

          <section aria-labelledby="titulo-ubicacion" className="space-y-2">
            <h3 id="titulo-ubicacion" className="text-sm font-semibold text-slate-900">Dónde pasó</h3>
            <MapaUbicacion valor={{ latitud: evento.latitud, longitud: evento.longitud }} alto={220}
              etiqueta="Mapa con el lugar donde se registró el evento" />
          </section>
        </div>
      )}
    </Modal>
  );
}
