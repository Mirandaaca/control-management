// EstadoEvento de la API: Pendiente mientras se atiende y Resuelto con su solución. Un evento resuelto ya no cambia.
export const PENDIENTE = 0;
export const RESUELTO = 1;
export const ESTADOS = { [PENDIENTE]: 'Pendiente', [RESUELTO]: 'Resuelto' };

// NivelPrioridad del tipo de evento.
export const PRIORIDADES = { 0: 'Baja', 1: 'Media', 2: 'Alta' };

/**
 * Las opciones del filtro "Tipo" (GET /tipos-evento/opciones, tarea 32): los activos y los inactivos que tienen eventos,
 * así se siguen encontrando los eventos viejos de un tipo que ya no se usa.
 */
export const opcionesDeTipo = (tipos) => tipos.map((t) => ({ valor: String(t.id), texto: t.activo ? t.nombre : `${t.nombre} (inactivo)` }));

// Un evento acepta hasta 5 fotos, de hasta 10 MB cada una (lo valida la API).
export const MAXIMO_DE_FOTOS = 5;

/**
 * Lo que el usuario puede hacer con un evento: el permiso, y además que esté pendiente. Modificar y administrar las
 * fotos, solo en los eventos que registró, salvo con cartera completa (la secretaria). La API lo exige igual.
 */
export function accionesDelEvento(evento, { tiene, permisos, veCarteraCompleta, yo }) {
  const pendiente = evento?.estado === PENDIENTE;
  const suyo = veCarteraCompleta || evento?.promotor.id === yo?.id;
  return {
    modificar: pendiente && suyo && tiene(permisos.modificarEvento),
    agregarFoto: pendiente && suyo && tiene(permisos.agregarFoto),
    eliminarFoto: pendiente && suyo && tiene(permisos.eliminarFoto),
    resolver: pendiente && tiene(permisos.resolverEvento),
  };
}
