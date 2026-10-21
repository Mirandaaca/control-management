import { useEffect, useState } from 'react';
import { ImageOff, LoaderCircle } from 'lucide-react';
import api from '../api/axios';

/**
 * Una foto que la API entrega solo con la sesión (GET /evidencias/{id}/archivo). Un <img src> no manda el token: la foto
 * se pide con axios como Blob y se muestra con una URL local, que se libera al desmontar o al cambiar de foto.
 */
export default function ImagenConSesion({ evidenciaId, alt, className = '' }) {
  const [foto, setFoto] = useState({ id: null, url: null, error: false });

  useEffect(() => {
    let vigente = true;
    let url = null;
    api.get(`/evidencias/${evidenciaId}/archivo`, { responseType: 'blob' })
      .then(({ data }) => {
        url = URL.createObjectURL(data);
        if (vigente) setFoto({ id: evidenciaId, url, error: false });
      })
      .catch(() => { if (vigente) setFoto({ id: evidenciaId, url: null, error: true }); });
    return () => {
      vigente = false;
      if (url) URL.revokeObjectURL(url);
    };
  }, [evidenciaId]);

  if (foto.id !== evidenciaId) {
    return (
      <div role="status" className={`grid place-items-center bg-slate-100 text-slate-600 ${className}`}>
        <LoaderCircle size={18} className="animate-spin" aria-hidden="true" />
        <span className="sr-only">Cargando la foto…</span>
      </div>
    );
  }
  if (foto.error) {
    return (
      <div className={`grid place-items-center gap-1 bg-slate-100 p-2 text-center text-xs text-slate-700 ${className}`}>
        <ImageOff size={18} aria-hidden="true" />
        No se pudo cargar la foto.
      </div>
    );
  }
  return <img src={foto.url} alt={alt} className={className} />;
}
