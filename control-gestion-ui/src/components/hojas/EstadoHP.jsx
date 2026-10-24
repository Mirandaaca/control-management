import { Badge } from '../ui';
import { ESTADOS_DE_HP } from './estados';

/** El estado de una hoja de pedido, siempre con su texto y su ícono: el color solo acompaña. */
export default function EstadoHP({ estado }) {
  const { texto, color, icono: Icono } = ESTADOS_DE_HP[estado] ?? { texto: 'Desconocido', color: 'gray' };
  return (
    <Badge color={color}>
      {Icono && <Icono size={12} aria-hidden="true" />}
      {texto}
    </Badge>
  );
}
