import { useState } from 'react';

// Un Set se compara por lo que tiene, sin importar el orden en que se marcó (los permisos de un rol), y un archivo, por
// cuál es: JSON.stringify los convertiría a los dos en {}.
const comoTexto = (valores) => JSON.stringify(valores, (_, v) => {
  if (v instanceof Set) return [...v].sort();
  if (v instanceof File) return { archivo: v.name, tamano: v.size, modificado: v.lastModified };
  return v;
});

/**
 * Si una ventana tiene algo sin guardar: lo que tiene ahora ya no es lo que tenía al abrirse. Es la prop sinGuardar de
 * Modal y Confirmar, que preguntan "¿Descartar lo que escribiste?" antes de cerrarse.
 * - "valores" es lo que escribe la persona (el estado del formulario, o solo el motivo), y null o undefined mientras la
 *   ventana está cerrada. Al abrirse, lo que hay pasa a ser el punto de partida.
 * - Un formulario que se monta al abrirse (FormularioPedido) empieza ya con su punto de partida.
 */
export function useSinGuardar(valores) {
  const [inicial, setInicial] = useState(valores);
  // Guardar lo del render anterior (react.dev, "Storing information from previous renders"): al abrir y al cerrar.
  if ((valores == null) !== (inicial == null)) setInicial(valores);
  return valores != null && inicial != null && comoTexto(valores) !== comoTexto(inicial);
}
