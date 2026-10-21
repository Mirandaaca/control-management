import { LoaderCircle, LocateFixed } from 'lucide-react';
import MapaUbicacion from './MapaUbicacion';
import { Alert, Btn } from './ui';

/**
 * Dónde se hizo una venta o una entrega. En el panel se toma del navegador ("Usar mi ubicación", que también es la
 * opción para quien usa solo el teclado) o se marca con un clic en el mapa; en la app la da el GPS sola.
 */
export default function CampoUbicacion({
  titulo, valor, precision, buscando, error, etiquetaMapa, sinPunto, onMarcar, onUsarMiUbicacion,
}) {
  let estado = sinPunto;
  if (buscando) estado = 'Tomando tu ubicación…';
  else if (valor) {
    estado = `Marcada en ${Number(valor.latitud).toFixed(6)}, ${Number(valor.longitud).toFixed(6)}${
      precision != null ? `, con ${precision} m de precisión` : ''}.`;
  }

  return (
    <div>
      <p className="text-sm font-medium text-slate-700 mb-1">{titulo}</p>
      <MapaUbicacion valor={valor} etiqueta={etiquetaMapa} onCambiar={onMarcar} />
      <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-slate-600 tabular-nums" aria-live="polite">{estado}</p>
        <Btn variant="outline" onClick={onUsarMiUbicacion} disabled={buscando}>
          {buscando ? <LoaderCircle size={14} className="animate-spin" aria-hidden="true" /> : <LocateFixed size={14} aria-hidden="true" />}
          Usar mi ubicación
        </Btn>
      </div>
      {error && <Alert type="error" className="mt-2">{error}</Alert>}
    </div>
  );
}
