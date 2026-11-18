import { Check, Clock, Package } from 'lucide-react';
import { EN_RUTA, ENTREGADO, PASTILLAS_DE_ENTREGA, PENDIENTE } from './marcadores';

const ICONOS = { [EN_RUTA]: Package, [ENTREGADO]: Check, [PENDIENTE]: Clock };

/**
 * La pastilla de una entrega fuera del mapa (la leyenda y la lista), igual que en el mapa: el ícono del estado y la hora.
 * Con ancho, todas miden lo mismo y los nombres de al lado quedan alineados.
 */
export default function PastillaDeEntrega({ hora, estado, ancho = false }) {
  const p = PASTILLAS_DE_ENTREGA[estado] ?? PASTILLAS_DE_ENTREGA[EN_RUTA];
  const Icono = ICONOS[estado] ?? Package;
  return (
    <span
      className={`inline-flex h-6.5 shrink-0 items-center justify-center gap-1 rounded-full border-2 px-2 text-sm font-bold leading-none tabular-nums ${ancho ? 'min-w-21' : ''}`}
      // En el mapa, el borde blanco separa la pastilla de los mosaicos; fuera de él, la de color va sin borde aparte.
      style={{ background: p.fondo, borderColor: p.borde === '#ffffff' ? p.fondo : p.borde, color: p.texto }}
      aria-hidden="true"
    >
      <Icono size={14} strokeWidth={2.5} />
      {hora}
    </span>
  );
}
