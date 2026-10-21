/**
 * Lleva la vista al primer error o aviso de un formulario, y el foco al primer campo con error. Dentro de un modal con
 * scroll, un mensaje que aparece arriba queda fuera de la vista de quien está abajo, junto al botón Guardar.
 * Los mensajes ya tienen que estar en el DOM: pon sus setState dentro de flushSync antes de llamarla.
 */
export function mostrarPrimerProblema(contenedor) {
  const problema = contenedor?.querySelector('[aria-invalid="true"], [role="alert"], [role="status"]');
  if (!problema) return;
  const sinAnimacion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  problema.scrollIntoView({ block: 'center', behavior: sinAnimacion ? 'auto' : 'smooth' });
  if (problema.matches('input, select, textarea, button')) problema.focus({ preventScroll: true });
}
