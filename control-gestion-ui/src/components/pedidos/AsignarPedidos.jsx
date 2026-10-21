import { useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { LoaderCircle } from 'lucide-react';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import { Alert, Btn, Input, Modal, Selector } from '../ui';
import { mostrarPrimerProblema } from '../../utils/foco';
import { diaEnBolivia, formatearDia, formatearHora } from '../../utils/formato';

/**
 * Distribución (1.3): los pedidos elegidos van a un promotor y a un día de reparto, sin cambiar su estado. Cada uno
 * conserva su hora. La API lo hace todo o nada: si uno no se puede asignar, dice cuál y no asigna ninguno.
 */
export default function AsignarPedidos({ pedidos, promotores, onCerrar, onHecho }) {
  const [promotorId, setPromotorId] = useState('');
  const [dia, setDia] = useState(diaEnBolivia());
  const [errores, setErrores] = useState({});
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const formularioRef = useRef(null);

  const asignar = async () => {
    setEnviando(true);
    setErrores({});
    setError('');
    try {
      await api.post('/pedidos/asignar', { pedidoIds: pedidos.map((p) => p.id), promotorId, dia: dia || null });
      const nombre = promotores.find((p) => p.id === promotorId)?.nombreCompleto;
      onHecho(`${pedidos.length === 1 ? `${pedidos[0].numero} quedó asignado` : `${pedidos.length} pedidos quedaron asignados`} a ${nombre} para el ${formatearDia(dia)}.`);
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (codigo === 'promotor_no_valido') campos.promotorId = detail;
        setErrores(campos);
        if (!campos.promotorId && !campos.dia) setError(mensajeDeError(err, 'No se pudieron asignar los pedidos.'));
      });
      mostrarPrimerProblema(formularioRef.current);
    } finally {
      setEnviando(false);
    }
  };

  return (
    <Modal
      abierto
      onCerrar={onCerrar}
      titulo={pedidos.length === 1 ? `Asignar ${pedidos[0].numero}` : `Asignar ${pedidos.length} pedidos`}
      descripcion="Elige quién los entrega y qué día. Cada pedido conserva su hora y su estado."
      pie={(
        <>
          <Btn variant="outline" size="md" onClick={onCerrar} disabled={enviando}>Cancelar</Btn>
          <Btn type="submit" form="formulario-asignar" size="md" disabled={enviando}>
            {enviando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            {enviando ? 'Asignando…' : 'Asignar'}
          </Btn>
        </>
      )}
    >
      <form id="formulario-asignar" ref={formularioRef} onSubmit={(e) => { e.preventDefault(); asignar(); }} className="space-y-4" noValidate>
        {error && <Alert type="error">{error}</Alert>}
        <Selector label="Promotor" placeholder="Elige el promotor" value={promotorId} error={errores.promotorId}
          opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
          onChange={(valor) => { setPromotorId(valor); setErrores((e) => ({ ...e, promotorId: undefined })); }} />
        <Input label="Día de entrega" type="date" value={dia} min={diaEnBolivia()} autoComplete="off" error={errores.dia}
          onChange={(e) => { setDia(e.target.value); setErrores((x) => ({ ...x, dia: undefined })); }} />
        <div>
          <p className="text-sm font-medium text-slate-700 mb-1">Pedidos</p>
          <ul className="divide-y divide-slate-200 rounded-lg border border-slate-200 text-sm">
            {pedidos.map((p) => (
              <li key={p.id} className="flex items-center justify-between gap-3 px-3 py-2">
                <span className="min-w-0 truncate">
                  <span className="font-mono text-xs" translate="no">{p.numero}</span> · {p.cliente.nombreCompleto}
                </span>
                <span className="shrink-0 tabular-nums text-slate-600">{p.fechaEntregaEstimada ? formatearHora(p.fechaEntregaEstimada) : 'Sin hora'}</span>
              </li>
            ))}
          </ul>
        </div>
      </form>
    </Modal>
  );
}
