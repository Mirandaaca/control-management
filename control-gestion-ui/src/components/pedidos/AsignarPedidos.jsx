import { useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { LoaderCircle } from 'lucide-react';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import { Alert, Btn, CampoFecha, Casilla, Modal, Selector } from '../ui';
import { useAuth } from '../../context/auth';
import { PERMISOS } from '../../context/permisos';
import { mostrarPrimerProblema } from '../../utils/foco';
import { diaEnBolivia, formatearDia, formatearHora } from '../../utils/formato';

/**
 * Distribución (1.3): los pedidos elegidos van a un promotor y a un día de reparto. Cada uno conserva su hora. Con "Ya
 * salen a ruta" (tarea 28, solo con el día de hoy y con el permiso de cambiar el estado), los pendientes pasan además a
 * En ruta, y el promotor ya puede registrar la entrega desde la app. La API lo hace todo o nada: si uno no se puede
 * asignar, dice cuál y no asigna ninguno.
 */
export default function AsignarPedidos({ pedidos, promotores, onCerrar, onHecho }) {
  const { tiene } = useAuth();
  const puedeDespachar = tiene(PERMISOS.pedidos.cambiarEstado);
  const [promotorId, setPromotorId] = useState('');
  const [dia, setDia] = useState(diaEnBolivia());
  // Lo que eligió la secretaria para hoy. Con otro día la casilla se ve desmarcada, y al volver a hoy recupera esto.
  const [marcada, setMarcada] = useState(true);
  const [errores, setErrores] = useState({});
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const formularioRef = useRef(null);

  const esHoy = dia === diaEnBolivia();
  const salenARuta = puedeDespachar && esHoy && marcada;

  const asignar = async () => {
    setEnviando(true);
    setErrores({});
    setError('');
    try {
      const { data } = await api.post('/pedidos/asignar', {
        pedidoIds: pedidos.map((p) => p.id), promotorId, dia: dia || null, salenARuta,
      });
      const nombre = promotores.find((p) => p.id === promotorId)?.nombreCompleto;
      const uno = pedidos.length === 1;
      const asignados = uno ? `${pedidos[0].numero} quedó asignado` : `${pedidos.length} pedidos quedaron asignados`;
      const salieron = salenARuta ? (uno ? ' y salió a ruta' : ' y salieron a ruta') : '';
      onHecho(`${asignados} a ${nombre} para el ${formatearDia(dia)}${salieron}.`, data);
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (codigo === 'promotor_no_valido') campos.promotorId = detail;
        if (codigo === 'despacho_fuera_del_dia') campos.dia = detail;
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
      descripcion={`Elige quién los entrega y qué día. Cada pedido conserva su hora${salenARuta ? ' y pasa a En ruta' : ''}.`}
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
        <CampoFecha label="Día de entrega" value={dia} min={diaEnBolivia()} error={errores.dia}
          onChange={(e) => { setDia(e.target.value); setErrores((x) => ({ ...x, dia: undefined })); }} />
        {puedeDespachar && (
          <Casilla checked={esHoy && marcada} disabled={!esHoy} onCheckedChange={(valor) => setMarcada(valor === true)}
            ayuda={esHoy
              ? 'Pasan a En ruta: el promotor ya puede registrar la entrega desde la app.'
              : 'Solo salen a ruta los pedidos de hoy.'}>
            Ya salen a ruta
          </Casilla>
        )}
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
