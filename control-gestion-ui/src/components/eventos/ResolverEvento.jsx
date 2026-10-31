import { useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import { subirFoto } from '../../api/fotos';
import BotonDeFoto from '../BotonDeFoto';
import { Alert, AreaDeTexto, Confirmar } from '../ui';
import { useSinGuardar } from '../../hooks/useSinGuardar';
import { mostrarPrimerProblema } from '../../utils/foco';

/**
 * Resolver un evento (4.1): la solución es obligatoria y queda con la fecha y quien lo resolvió. Si el tipo pide
 * evidencia y el evento no tiene fotos, lo explica y ofrece agregar la foto ahí mismo, en vez de mostrar solo el error.
 * Se monta al abrirse: empieza limpio. La foto agregada llega al padre por onCambio.
 */
export default function ResolverEvento({ evento, puedeAgregarFoto, onCambio, onCerrar, onHecho }) {
  const [solucion, setSolucion] = useState('');
  const sinGuardar = useSinGuardar(solucion);
  const [errorCampo, setErrorCampo] = useState('');
  const [error, setError] = useState('');
  const [laApiPideFoto, setLaApiPideFoto] = useState(false);
  const [subiendo, setSubiendo] = useState(false);
  const [errorFoto, setErrorFoto] = useState('');
  const [enviando, setEnviando] = useState(false);
  const contenedorRef = useRef(null);

  const sinFotos = evento.fotos.length === 0;
  const pideFoto = sinFotos && (evento.tipo.requiereEvidencia || laApiPideFoto);

  const agregarFoto = async (archivo) => {
    setSubiendo(true);
    setErrorFoto('');
    try {
      onCambio(await subirFoto(evento.id, archivo));
    } catch (err) {
      setErrorFoto(mensajeDeError(err, 'No se pudo subir la foto.'));
    } finally {
      setSubiendo(false);
    }
  };

  const resolver = async () => {
    setEnviando(true);
    setErrorCampo('');
    setError('');
    try {
      const { data } = await api.post(`/eventos/${evento.id}/resolver`, { solucion });
      onHecho(data);
    } catch (err) {
      const codigo = err.response?.data?.codigo;
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (codigo === 'evento_sin_evidencia') setLaApiPideFoto(true);
        else if (campos.solucion) setErrorCampo(campos.solucion);
        else setError(mensajeDeError(err, 'No se pudo resolver el evento.'));
      });
      mostrarPrimerProblema(contenedorRef.current);
    } finally {
      setEnviando(false);
    }
  };

  return (
    <Confirmar
      abierto
      titulo={`¿Resolver ${evento.numero}?`}
      textoAccion="Resolver evento"
      variante="primary"
      enviando={enviando || subiendo}
      error={error}
      onConfirmar={resolver}
      onCancelar={onCerrar}
      sinGuardar={sinGuardar}
    >
      <div ref={contenedorRef} className="space-y-3">
        <p>Queda resuelto con tu solución, la fecha y tu nombre. Después ya no se modifica ni recibe fotos.</p>
        {pideFoto && (
          <Alert type="warning">
            <p>Un evento de tipo «{evento.tipo.nombre}» se resuelve con al menos una foto, y este todavía no tiene.</p>
            {puedeAgregarFoto ? (
              <div className="mt-2">
                <BotonDeFoto onElegir={agregarFoto} enviando={subiendo}>{subiendo ? 'Subiendo…' : 'Agregar foto'}</BotonDeFoto>
              </div>
            ) : (
              <p className="mt-1">Pide a quien lo registró que agregue la foto.</p>
            )}
          </Alert>
        )}
        {errorFoto && <Alert type="error">{errorFoto}</Alert>}
        {!sinFotos && evento.tipo.requiereEvidencia && (
          <p className="text-slate-600">{evento.fotos.length === 1 ? 'Tiene 1 foto.' : `Tiene ${evento.fotos.length} fotos.`}</p>
        )}
        <AreaDeTexto label="Solución" rows={4} value={solucion} autoComplete="off" maxLength={1000} error={errorCampo}
          placeholder="Qué se hizo para resolverlo…"
          onChange={(e) => { setSolucion(e.target.value); setErrorCampo(''); }} />
      </div>
    </Confirmar>
  );
}
