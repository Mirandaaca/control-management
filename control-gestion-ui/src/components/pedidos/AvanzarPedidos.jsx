import { useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import { Alert, AreaDeTexto, Confirmar } from '../ui';
import { mostrarPrimerProblema } from '../../utils/foco';
import { formatearHora } from '../../utils/formato';
import { SIGUIENTE } from './estados';

/**
 * Pasar varios pedidos a En ruta a la vez (1.2), con una sola confirmación. Los que no avanzan (los que ya están En
 * ruta: lo que sigue es la entrega, en la app) quedan igual. La API lo hace todo o nada y con el estado que se ve aquí:
 * si uno cambió mientras tanto, no avanza ninguno y avisa con "pedido_cambio".
 */
export default function AvanzarPedidos({ pedidos, onCerrar, onHecho, onCambiaron }) {
  const [texto, setTexto] = useState('');
  const [errores, setErrores] = useState({});
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const contenedorRef = useRef(null);

  const avanzan = pedidos.filter((p) => SIGUIENTE[p.estado.id]);
  const quedan = pedidos.filter((p) => !SIGUIENTE[p.estado.id]);
  // En ruta sin promotor no se puede entregar: la entrega la registra el promotor asignado.
  const sinPromotor = avanzan.filter((p) => !p.repartidor);
  const unoSolo = avanzan.length === 1 ? avanzan[0] : null;

  const exito = () => (unoSolo ? `${unoSolo.numero} pasó a En ruta.` : `${avanzan.length} pedidos pasaron a En ruta.`);

  const enviar = async () => {
    setEnviando(true);
    setErrores({});
    setError('');
    try {
      const { data } = await api.post('/pedidos/avanzar', {
        pedidos: avanzan.map((p) => ({ id: p.id, estadoId: p.estado.id })),
        observacion: texto || null,
      });
      onHecho(exito(), data);
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      if (codigo === 'pedido_cambio') {
        onCambiaron(detail);
        return;
      }
      flushSync(() => {
        const campos = erroresPorCampo(err);
        setErrores(campos);
        if (!campos.observacion) setError(mensajeDeError(err, 'No se pudieron pasar los pedidos a En ruta.'));
      });
      mostrarPrimerProblema(contenedorRef.current);
    } finally {
      setEnviando(false);
    }
  };

  const grupo = (titulo, lista) => lista.length > 0 && (
    <div>
      <p className="text-sm font-medium text-slate-700 mb-1">{titulo}</p>
      <ul className="divide-y divide-slate-200 rounded-lg border border-slate-200">
        {lista.map((p) => (
          <li key={p.id} className="flex items-center justify-between gap-3 px-3 py-2">
            <span className="min-w-0 truncate">
              <span className="font-mono text-xs" translate="no">{p.numero}</span> · {p.cliente.nombreCompleto}
            </span>
            <span className="shrink-0 tabular-nums text-slate-600">{p.fechaEntregaEstimada ? formatearHora(p.fechaEntregaEstimada) : 'Sin hora'}</span>
          </li>
        ))}
      </ul>
    </div>
  );

  return (
    <Confirmar
      abierto
      titulo={unoSolo ? `¿Pasar ${unoSolo.numero} a En ruta?` : `¿Pasar ${avanzan.length} pedidos a En ruta?`}
      textoAccion={unoSolo ? 'Pasar a En ruta' : `Pasar ${avanzan.length} pedidos a En ruta`}
      variante="primary"
      enviando={enviando}
      error={error}
      onConfirmar={enviar}
      onCancelar={onCerrar}
    >
      <div ref={contenedorRef} className="space-y-3">
        <p>
          {unoSolo
            ? 'Sale a entregarse: ya no se modifica, y el promotor asignado registra la entrega.'
            : 'Salen a entregarse: ya no se modifican, y el promotor asignado registra la entrega.'}
        </p>
        {grupo(unoSolo ? 'Pasa a En ruta' : 'Pasan a En ruta', avanzan)}
        {sinPromotor.length > 0 && (
          <Alert type="warning">
            {sinPromotor.length === 1
              ? `${sinPromotor[0].numero} no tiene promotor asignado: asígnalo para que se pueda entregar.`
              : `${sinPromotor.map((p) => p.numero).join(', ')} no tienen promotor asignado: asígnalos para que se puedan entregar.`}
          </Alert>
        )}
        {quedan.length > 0 && (
          <p className="text-slate-600">
            Quedan igual: {quedan.map((p) => `${p.numero} (${p.estado.nombre})`).join(', ')}.
          </p>
        )}
        <AreaDeTexto
          label="Observación (opcional)"
          value={texto}
          autoComplete="off"
          maxLength={500}
          error={errores.observacion}
          placeholder="Algo que deba saber quien los entregue…"
          onChange={(e) => { setTexto(e.target.value); setErrores((x) => ({ ...x, observacion: undefined })); }}
        />
      </div>
    </Confirmar>
  );
}
