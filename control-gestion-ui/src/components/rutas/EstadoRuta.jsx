import { CalendarClock, CircleCheck, Navigation } from 'lucide-react';
import { COMPLETADA, EN_CURSO, PLANIFICADA } from './marcadores';

// docs/diseno.md, "Etiquetas de estado": texto e ícono, y el color que el panel usa para cada estado (8.6.3).
const ESTADOS = {
  [PLANIFICADA]: { texto: 'Planificada', Icono: CalendarClock, clase: 'bg-amber-50 text-amber-800' },
  [EN_CURSO]: { texto: 'En curso', Icono: Navigation, clase: 'bg-marca-50 text-marca-800' },
  [COMPLETADA]: { texto: 'Completada', Icono: CircleCheck, clase: 'bg-slate-100 text-slate-700' },
};

/**
 * La etiqueta del estado de una ruta. Con blanca, el fondo es blanco: en la ruta elegida de la lista (fondo marca-50),
 * "En curso" se perdería (8.6.3).
 */
export default function EstadoRuta({ estado, blanca = false }) {
  const { texto, Icono, clase } = ESTADOS[estado] ?? ESTADOS[PLANIFICADA];
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap ${clase} ${blanca ? 'bg-white!' : ''}`}>
      <Icono size={12} aria-hidden="true" />
      {texto}
    </span>
  );
}
