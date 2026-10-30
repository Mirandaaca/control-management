import { Ban, CircleCheck, Hourglass, Route } from 'lucide-react';
import { Badge } from '../ui';
import { ANULADO, EN_RUTA, ENTREGADO, PENDIENTE } from './estados';

// El color y el ícono de cada estado, con la tabla de docs/diseno.md ("Etiquetas de estado").
const ESTILOS = {
  [PENDIENTE]: { color: 'yellow', icono: Hourglass },
  [EN_RUTA]: { color: 'purple', icono: Route },
  [ENTREGADO]: { color: 'green', icono: CircleCheck },
  [ANULADO]: { color: 'red', icono: Ban },
};

/** El estado de un pedido, siempre con su nombre y su ícono: el color solo acompaña. */
export default function EstadoPedido({ estado }) {
  const { color, icono: Icono } = ESTILOS[estado.id] ?? { color: 'gray' };
  return (
    <Badge color={color}>
      {Icono && <Icono size={12} aria-hidden="true" />}
      {estado.nombre}
    </Badge>
  );
}
