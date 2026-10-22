import { Clock, MapPin } from 'lucide-react';
import { colorDePaleta } from './paletaDeEstados';

// Los ids de ControlGestion.API/Models/EstadosDeCliente.cs. Su color tiene significado fijo (docs/diseno.md, 5.2):
// rojo es "visitado hace poco" y verde es "disponible para re-visita", siempre con su texto y su ícono.
const ACTIVO = 1;
const DISPONIBLE = 2;

/** La etiqueta del estado de un cliente: { id, nombre, color } como lo entrega la API. */
export default function EstadoCliente({ estado }) {
  if (!estado) return null;
  const base = 'inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold whitespace-nowrap';

  if (estado.id === ACTIVO || estado.id === DISPONIBLE) {
    const activo = estado.id === ACTIVO;
    const Icono = activo ? Clock : MapPin;
    return (
      <span className={`${base} ${activo
        ? 'bg-cliente-activo-fondo text-cliente-activo-texto'
        : 'bg-cliente-disponible-fondo text-cliente-disponible-texto'}`}
      >
        <Icono size={12} aria-hidden="true" />
        {estado.nombre}
      </span>
    );
  }

  // Los estados asignados a mano se pintan con el color que eligió el administrador (7.4), de la paleta, con su texto.
  // Un color que no es de la paleta (no debería pasar: la API lo rechaza) queda en gris.
  const pintura = colorDePaleta(estado.color);
  if (!pintura) return <span className={`${base} bg-slate-100 text-slate-700`}>{estado.nombre}</span>;
  return (
    <span className={base} style={{ backgroundColor: pintura.color, color: pintura.texto }}>
      {estado.nombre}
    </span>
  );
}
