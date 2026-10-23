import { Check, UserX } from 'lucide-react';
import { AUSENTE, PASTILLAS, PENDIENTE, VISITADO } from './marcadores';

/**
 * La pastilla de una parada fuera del mapa (la leyenda y la lista de visitas), igual que en el mapa. Con ancho, todas
 * miden lo mismo y los nombres de al lado quedan alineados.
 */
export default function Pastilla({ orden, resultado = PENDIENTE, ancho = false }) {
  const p = PASTILLAS[resultado] ?? PASTILLAS[PENDIENTE];
  return (
    <span
      className={`inline-flex h-6.5 items-center justify-center gap-0.5 rounded-full border-2 px-2 text-sm font-bold leading-none tabular-nums ${ancho ? 'min-w-12' : 'min-w-7.5'}`}
      style={{ background: p.fondo, borderColor: resultado === PENDIENTE ? p.borde : p.fondo, color: p.texto }}
      aria-hidden="true"
    >
      {orden}
      {resultado === VISITADO && <Check size={14} strokeWidth={2.5} />}
      {resultado === AUSENTE && <UserX size={14} strokeWidth={2.5} />}
    </span>
  );
}
