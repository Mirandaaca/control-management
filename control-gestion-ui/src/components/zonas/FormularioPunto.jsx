import { useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { LoaderCircle } from 'lucide-react';
import api from '../../api/axios';
import { erroresPorCampo, mensajeDeError } from '../../api/errores';
import MapaUbicacion from '../MapaUbicacion';
import { Alert, AreaDeTexto, BotonCancelar, Btn, Input, Modal, Selector } from '../ui';
import { useSinGuardar } from '../../hooks/useSinGuardar';
import { mostrarPrimerProblema } from '../../utils/foco';
import { zonaQueContiene } from '../../utils/geometria';

/**
 * Registrar o editar un punto de referencia (5.5). La zona se sugiere sola: la que contiene el punto, si alguna lo
 * contiene. Si quien registra la cambia a mano, se respeta aunque después mueva el punto. Se monta cada vez que se
 * abre, así empieza limpio.
 */
export default function FormularioPunto({ punto, zonas, zonaPorDefecto, onCerrar, onGuardado }) {
  const esNuevo = !punto.id;
  const sugerir = (latitud, longitud) => zonaQueContiene(latitud, longitud, zonas)?.id ?? zonaPorDefecto;
  const [datos, setDatos] = useState(() => ({
    nombre: punto.nombre ?? '',
    descripcion: punto.descripcion ?? '',
    latitud: punto.latitud,
    longitud: punto.longitud,
    zonaId: String(punto.zona?.id ?? sugerir(punto.latitud, punto.longitud) ?? ''),
  }));
  const [zonaElegidaAMano, setZonaElegidaAMano] = useState(!esNuevo);
  const sinGuardar = useSinGuardar(datos);
  const [errores, setErrores] = useState({});
  const [error, setError] = useState('');
  const [guardando, setGuardando] = useState(false);
  const formularioRef = useRef(null);

  const contieneLaZona = zonaQueContiene(datos.latitud, datos.longitud, zonas)?.id === Number(datos.zonaId);

  const moverPunto = ({ latitud, longitud }) => setDatos((d) => ({
    ...d,
    latitud,
    longitud,
    zonaId: zonaElegidaAMano ? d.zonaId : String(sugerir(latitud, longitud) ?? d.zonaId),
  }));

  const guardar = async (ev) => {
    ev.preventDefault();
    setGuardando(true);
    setErrores({});
    setError('');
    const cuerpo = { ...datos, zonaId: Number(datos.zonaId) || 0 };
    try {
      const { data } = esNuevo ? await api.post('/puntos-referencia', cuerpo) : await api.put(`/puntos-referencia/${punto.id}`, cuerpo);
      onGuardado(data, esNuevo);
    } catch (err) {
      flushSync(() => {
        const campos = erroresPorCampo(err);
        setErrores(campos);
        if (Object.keys(campos).length === 0) setError(mensajeDeError(err, 'No se pudo guardar el punto de referencia.'));
      });
      mostrarPrimerProblema(formularioRef.current);
    } finally {
      setGuardando(false);
    }
  };

  return (
    <Modal
      abierto
      onCerrar={onCerrar}
      sinGuardar={sinGuardar}
      titulo={esNuevo ? 'Nuevo punto de referencia' : `Editar ${punto.nombre}`}
      descripcion="Un lugar que orienta en la zona, como un mercado o una plaza."
      pie={(
        <>
          <BotonCancelar disabled={guardando} />
          <Btn type="submit" form="formulario-punto" size="md" disabled={guardando}>
            {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            {guardando ? 'Guardando…' : 'Guardar'}
          </Btn>
        </>
      )}
    >
      <form id="formulario-punto" ref={formularioRef} onSubmit={guardar} className="space-y-4" noValidate>
        {error && <Alert type="error">{error}</Alert>}
        <Input label="Nombre" value={datos.nombre} maxLength={100} autoComplete="off" placeholder="Mercado Los Pozos…"
          error={errores.nombre} onChange={(e) => setDatos({ ...datos, nombre: e.target.value })} />
        <AreaDeTexto label="Descripción (opcional)" rows={2} maxLength={300} value={datos.descripcion} autoComplete="off"
          error={errores.descripcion} onChange={(e) => setDatos({ ...datos, descripcion: e.target.value })} />
        <Selector label="Zona" placeholder="Elige la zona" value={datos.zonaId} error={errores.zonaId}
          opciones={zonas.map((z) => ({ valor: String(z.id), texto: z.nombre }))}
          ayuda={contieneLaZona ? 'El punto cae dentro del límite de esta zona.' : undefined}
          onChange={(zonaId) => { setZonaElegidaAMano(true); setDatos({ ...datos, zonaId }); }} />
        <div>
          <p className="block text-sm font-medium text-slate-700 mb-1">Ubicación</p>
          <MapaUbicacion valor={datos} onCambiar={moverPunto} alto={200} etiqueta="Mapa con el punto de referencia: un clic lo mueve" />
          <p aria-live="polite" className={`mt-1 text-sm ${errores.latitud ? 'text-red-700' : 'text-slate-600'}`}>
            {errores.latitud ?? `Marcado en ${datos.latitud}, ${datos.longitud}. Un clic en el mapa lo mueve.`}
          </p>
        </div>
      </form>
    </Modal>
  );
}
