import { useState } from 'react';
import { Trash2 } from 'lucide-react';
import api from '../../api/axios';
import { mensajeDeError } from '../../api/errores';
import { subirFoto } from '../../api/fotos';
import BotonDeFoto from '../BotonDeFoto';
import ImagenConSesion from '../ImagenConSesion';
import { Alert, Btn, Confirmar, Modal } from '../ui';
import { formatearFecha } from '../../utils/formato';
import { MAXIMO_DE_FOTOS } from './estados';

function quienYCuando(foto) {
  const subida = `Subida por ${foto.subidoPor.nombreCompleto} el ${formatearFecha(foto.fechaSubida, { hora: true })}`;
  return foto.fechaCaptura ? `${subida}. Tomada el ${formatearFecha(foto.fechaCaptura, { hora: true })}.` : `${subida}.`;
}

/**
 * Las fotos de un evento (4.2): en miniatura, con su fecha, y grandes al elegirlas. Agregar y eliminar se ofrecen solo
 * si el usuario puede (pendiente, su permiso y su evento). Cada cambio devuelve el evento actualizado en onCambio.
 */
export default function GaleriaDeFotos({ evento, puedeAgregar, puedeEliminar, onCambio }) {
  const [abierta, setAbierta] = useState(null);
  const [subiendo, setSubiendo] = useState(false);
  const [error, setError] = useState('');
  const [porEliminar, setPorEliminar] = useState(null);
  const [eliminando, setEliminando] = useState(false);
  const [errorAlEliminar, setErrorAlEliminar] = useState('');
  const fotos = evento.fotos;

  const agregar = async (archivo) => {
    setSubiendo(true);
    setError('');
    try {
      onCambio(await subirFoto(evento.id, archivo), 'Se agregó la foto.');
    } catch (err) {
      setError(mensajeDeError(err, 'No se pudo subir la foto.'));
    } finally {
      setSubiendo(false);
    }
  };

  const eliminar = async () => {
    setEliminando(true);
    setErrorAlEliminar('');
    try {
      const { data } = await api.delete(`/eventos/${evento.id}/fotos/${porEliminar.id}`);
      setPorEliminar(null);
      setAbierta(null);
      onCambio(data, 'Se eliminó la foto.');
    } catch (err) {
      setErrorAlEliminar(mensajeDeError(err, 'No se pudo eliminar la foto.'));
    } finally {
      setEliminando(false);
    }
  };

  return (
    <section aria-labelledby="titulo-fotos" className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 id="titulo-fotos" className="text-sm font-semibold text-slate-900">
          Fotos <span className="font-normal text-slate-600 tabular-nums">({fotos.length} de {MAXIMO_DE_FOTOS})</span>
        </h3>
        {puedeAgregar && fotos.length < MAXIMO_DE_FOTOS && (
          <BotonDeFoto onElegir={agregar} enviando={subiendo}>{subiendo ? 'Subiendo…' : 'Agregar foto'}</BotonDeFoto>
        )}
      </div>
      {error && <Alert type="error">{error}</Alert>}

      {fotos.length === 0 ? (
        <p className="text-sm text-slate-600">
          Todavía no tiene fotos.{puedeAgregar && ' Sube una imagen JPEG, PNG o WebP de hasta 10 MB.'}
        </p>
      ) : (
        <ul className="grid grid-cols-3 gap-3 sm:grid-cols-5">
          {fotos.map((foto, i) => (
            <li key={foto.id} className="min-w-0">
              <button
                type="button"
                onClick={() => setAbierta(foto)}
                aria-label={`Ver la foto ${i + 1}. ${quienYCuando(foto)}`}
                className="block w-full overflow-hidden rounded-lg border border-slate-200 cursor-pointer hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2"
              >
                <ImagenConSesion evidenciaId={foto.id} alt="" className="aspect-square w-full object-cover" />
              </button>
              <p className="mt-1 truncate text-xs text-slate-600 tabular-nums">{formatearFecha(foto.fechaSubida)}</p>
            </li>
          ))}
        </ul>
      )}

      <Modal
        abierto={abierta !== null}
        onCerrar={() => setAbierta(null)}
        ancho="lg"
        titulo={`Foto de ${evento.numero}`}
        descripcion={abierta ? quienYCuando(abierta) : undefined}
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setAbierta(null)}>Cerrar</Btn>
            {puedeEliminar && abierta && (
              <Btn variant="danger" size="md" onClick={() => { setErrorAlEliminar(''); setPorEliminar(abierta); }}>
                <Trash2 size={16} aria-hidden="true" />Eliminar foto
              </Btn>
            )}
          </>
        )}
      >
        {abierta && (
          <ImagenConSesion evidenciaId={abierta.id} alt={`Foto de ${evento.numero}, ${abierta.nombreArchivo}`}
            className="mx-auto max-h-[65vh] w-auto max-w-full rounded-lg object-contain" />
        )}
      </Modal>

      <Confirmar
        abierto={porEliminar !== null}
        titulo="¿Eliminar la foto?"
        textoAccion="Eliminar foto"
        enviando={eliminando}
        error={errorAlEliminar}
        onConfirmar={eliminar}
        onCancelar={() => setPorEliminar(null)}
      >
        <p>Se borra del evento y del servidor: no se puede deshacer.</p>
      </Confirmar>
    </section>
  );
}
