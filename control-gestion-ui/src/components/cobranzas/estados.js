import { Ban, CalendarX, ChartPie, CircleCheck, CircleX, Hourglass } from 'lucide-react';

// EstadoCobranza de la API, con el texto, el color y el ícono de la tabla de docs/diseno.md ("Etiquetas de estado").
export const ESTADOS_DE_COBRANZA = {
  0: { texto: 'Pendiente', color: 'yellow', icono: Hourglass },
  1: { texto: 'Parcial', color: 'blue', icono: ChartPie },
  2: { texto: 'Pagada', color: 'green', icono: CircleCheck },
  3: { texto: 'Vencida', color: 'red', icono: CalendarX },
  4: { texto: 'Incobrable', color: 'purple', icono: CircleX },
  5: { texto: 'Anulada', color: 'gray', icono: Ban },
};

// La API solo los manda en una cobranza vencida: días de calendario de Bolivia desde el vencimiento.
export const textoDeMora = (dias) => (dias === 1 ? '1 día de mora' : `${dias} días de mora`);
