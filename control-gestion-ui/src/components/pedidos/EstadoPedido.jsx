import { Badge } from '../ui';
import { ANULADO, EN_PROCESO, EN_RUTA, ENTREGADO, PENDIENTE } from './estados';

const COLORES = { [PENDIENTE]: 'yellow', [EN_PROCESO]: 'blue', [EN_RUTA]: 'purple', [ENTREGADO]: 'green', [ANULADO]: 'red' };

/** El estado de un pedido, siempre con su nombre: el color solo acompaña. */
export default function EstadoPedido({ estado }) {
  return <Badge color={COLORES[estado.id] ?? 'gray'}>{estado.nombre}</Badge>;
}
