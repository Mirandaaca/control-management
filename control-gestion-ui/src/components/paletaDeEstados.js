// Los colores de ControlGestion.API/Models/PaletaDeEstados.cs. Si cambia allá, cambia aquí.
// Rojo y verde son de Activo y Disponible para re-visita (significado fijo, docs/diseno.md); los estados asignados a mano
// eligen de PALETA, que no tiene tonos que se confundan con ellos. Cada color tiene 4,5:1 o más con su texto.
export const COLOR_ACTIVO = '#DC2626';
export const COLOR_DISPONIBLE = '#15803D';

export const PALETA = [
  { nombre: 'Pizarra', color: '#475569', texto: '#FFFFFF' },
  { nombre: 'Grafito', color: '#1E293B', texto: '#FFFFFF' },
  { nombre: 'Azul', color: '#1D4ED8', texto: '#FFFFFF' },
  { nombre: 'Celeste', color: '#0369A1', texto: '#FFFFFF' },
  { nombre: 'Índigo', color: '#4338CA', texto: '#FFFFFF' },
  { nombre: 'Violeta', color: '#6D28D9', texto: '#FFFFFF' },
];

/** El color de la paleta con ese valor, sin importar mayúsculas, o undefined si no es de ella. */
export function colorDePaleta(valor) {
  return PALETA.find((c) => c.color.toLowerCase() === String(valor ?? '').trim().toLowerCase());
}
