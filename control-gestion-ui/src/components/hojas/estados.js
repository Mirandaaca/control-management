import { Ban, CircleCheck, FilePen } from 'lucide-react';

// EstadoHP de la API, con el texto, el color y el ícono de la tabla de docs/diseno.md ("Etiquetas de estado").
export const BORRADOR = 0;
export const CONFIRMADA = 1;
export const ANULADA = 2;

export const ESTADOS_DE_HP = {
  [BORRADOR]: { texto: 'Borrador', color: 'gray', icono: FilePen },
  [CONFIRMADA]: { texto: 'Confirmada', color: 'green', icono: CircleCheck },
  [ANULADA]: { texto: 'Anulada', color: 'red', icono: Ban },
};
