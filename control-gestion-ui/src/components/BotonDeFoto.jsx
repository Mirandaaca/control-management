import { useRef } from 'react';
import { Camera, LoaderCircle } from 'lucide-react';
import { ACEPTA_FOTOS } from '../api/fotos';
import { Btn } from './ui';

/**
 * Un botón que abre el selector de archivos (en el celular, la cámara) y entrega la foto elegida. El campo de archivo
 * queda oculto: lo que se ve y se enfoca es el botón.
 */
export default function BotonDeFoto({ children, onElegir, enviando = false, disabled = false, variant = 'outline', size = 'sm', ...props }) {
  const campo = useRef(null);
  return (
    <>
      <input
        ref={campo}
        type="file"
        accept={ACEPTA_FOTOS}
        capture="environment"
        className="sr-only"
        tabIndex={-1}
        aria-hidden="true"
        onChange={(e) => {
          const archivo = e.target.files?.[0];
          // Se vacía para que elegir otra vez el mismo archivo vuelva a avisar.
          e.target.value = '';
          if (archivo) onElegir(archivo);
        }}
      />
      <Btn variant={variant} size={size} disabled={disabled || enviando} onClick={() => campo.current?.click()} {...props}>
        {enviando ? <LoaderCircle size={14} className="animate-spin" aria-hidden="true" /> : <Camera size={14} aria-hidden="true" />}
        {children}
      </Btn>
    </>
  );
}
