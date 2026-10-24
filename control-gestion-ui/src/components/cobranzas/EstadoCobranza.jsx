import { Badge } from '../ui';
import { ESTADOS_DE_COBRANZA } from './estados';

/** El estado de una cobranza, siempre con su texto y su ícono: el color solo acompaña. */
export default function EstadoCobranza({ estado }) {
  const { texto, color, icono: Icono } = ESTADOS_DE_COBRANZA[estado] ?? { texto: 'Desconocido', color: 'gray' };
  return (
    <Badge color={color}>
      {Icono && <Icono size={12} aria-hidden="true" />}
      {texto}
    </Badge>
  );
}
