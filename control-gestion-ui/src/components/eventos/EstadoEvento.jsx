import { CircleCheck, Clock } from 'lucide-react';
import { Badge } from '../ui';
import { ESTADOS, RESUELTO } from './estados';

/**
 * El estado de un evento, con su nombre y su ícono: el color solo acompaña. Ámbar y azul, como en el mapa de eventos:
 * el rojo y el verde son de los estados del cliente (docs/diseno.md).
 */
export default function EstadoEvento({ estado }) {
  const resuelto = estado === RESUELTO;
  const Icono = resuelto ? CircleCheck : Clock;
  return (
    <Badge color={resuelto ? 'blue' : 'yellow'}>
      <Icono size={12} aria-hidden="true" />
      {ESTADOS[estado] ?? 'Desconocido'}
    </Badge>
  );
}
