import { useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import { AreaDeTexto, CampoFechaHora, Confirmar } from '../ui';
import { useSinGuardar } from '../../hooks/useSinGuardar';
import { mostrarPrimerProblema } from '../../utils/foco';
import { ahoraEnBolivia, formatearFecha } from '../../utils/formato';
import { SIGUIENTE } from './estados';

/**
 * Avanzar, reprogramar o anular un pedido (1.1 y 1.2), cada uno con su confirmación. Se monta al abrirse: empieza
 * limpio. "cambio" es { tipo: 'avanzar' | 'reprogramar' | 'anular', pedido }.
 */
export default function CambioDeEstado({ cambio, onCerrar, onHecho }) {
  const { tipo, pedido } = cambio;
  const siguiente = SIGUIENTE[pedido.estado.id];
  const [texto, setTexto] = useState('');
  const [fecha, setFecha] = useState('');
  const sinGuardar = useSinGuardar({ texto, fecha });
  const [errores, setErrores] = useState({});
  const [error, setError] = useState('');
  const [enviando, setEnviando] = useState(false);
  const contenedorRef = useRef(null);

  const enviar = async () => {
    setEnviando(true);
    setErrores({});
    setError('');
    try {
      if (tipo === 'avanzar') {
        await api.post(`/pedidos/${pedido.id}/avanzar`, { observacion: texto || null });
        onHecho(`${pedido.numero} pasó a ${siguiente.nombre}.`);
      } else if (tipo === 'reprogramar') {
        const { data } = await api.post(`/pedidos/${pedido.id}/reprogramar`, { fechaEntregaEstimada: fecha || null, motivo: texto });
        onHecho(`${pedido.numero} se reprogramó para el ${formatearFecha(data.fechaEntregaEstimada, { hora: true })} y volvió a Pendiente.`);
      } else {
        await api.post(`/pedidos/${pedido.id}/anular`, { motivo: texto });
        onHecho(`${pedido.numero} quedó anulado.`);
      }
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (codigo === 'fecha_en_el_pasado') campos.fechaEntregaEstimada = detail;
        setErrores(campos);
        if (!campos.motivo && !campos.observacion && !campos.fechaEntregaEstimada)
          setError(mensajeDeError(err, 'No se pudo cambiar el pedido.'));
      });
      mostrarPrimerProblema(contenedorRef.current);
    } finally {
      setEnviando(false);
    }
  };

  const textos = {
    avanzar: {
      titulo: `¿Pasar ${pedido.numero} a ${siguiente?.nombre}?`,
      accion: `Pasar a ${siguiente?.nombre}`,
      explicacion: 'Sale a entregarse: desde En ruta ya no se modifica, y el promotor asignado registra la entrega.',
      ejemplo: 'Algo que deba saber quien lo entregue…',
    },
    reprogramar: {
      titulo: `¿Reprogramar ${pedido.numero}?`,
      accion: 'Reprogramar entrega',
      explicacion: 'Vuelve a Pendiente con la nueva fecha y conserva al promotor asignado. El motivo queda en el historial.',
      ejemplo: 'Por qué cambia la entrega…',
    },
    anular: {
      titulo: `¿Anular ${pedido.numero}?`,
      accion: 'Anular pedido',
      explicacion: 'Ya no se entrega ni se puede modificar. Todavía no movió el stock ni creó una deuda: solo queda anulado.',
      ejemplo: 'Por qué se anula el pedido…',
    },
  }[tipo];

  return (
    <Confirmar
      abierto
      titulo={textos.titulo}
      textoAccion={textos.accion}
      variante={tipo === 'anular' ? 'danger' : 'primary'}
      enviando={enviando}
      error={error}
      onConfirmar={enviar}
      onCancelar={onCerrar}
      sinGuardar={sinGuardar}
    >
      <div ref={contenedorRef} className="space-y-3">
        <p>{textos.explicacion}</p>
        {tipo === 'reprogramar' && (
          <CampoFechaHora label="Nueva fecha y hora de entrega" value={fecha} min={ahoraEnBolivia()}
            ayuda="En la hora de Bolivia." error={errores.fechaEntregaEstimada}
            onChange={(e) => { setFecha(e.target.value); setErrores((x) => ({ ...x, fechaEntregaEstimada: undefined })); }} />
        )}
        <AreaDeTexto
          label={tipo === 'avanzar' ? 'Observación (opcional)' : 'Motivo'}
          value={texto}
          autoComplete="off"
          maxLength={500}
          error={tipo === 'avanzar' ? errores.observacion : errores.motivo}
          placeholder={textos.ejemplo}
          onChange={(e) => { setTexto(e.target.value); setErrores((x) => ({ ...x, motivo: undefined, observacion: undefined })); }}
        />
      </div>
    </Confirmar>
  );
}
