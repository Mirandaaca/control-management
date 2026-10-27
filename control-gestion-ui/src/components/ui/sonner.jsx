import { useSyncExternalStore } from 'react';
import { Toaster } from 'sonner';
import { CircleAlert, CircleCheck, Info, TriangleAlert, X } from 'lucide-react';

// Dónde aparecen los toasts de avisar.js: abajo a la derecha en una pantalla ancha y, en el celular, arriba, debajo de
// la barra del menú (h-14 en Layout.jsx): abajo taparían los botones, y sobre la barra, el menú. 600 px es el corte de
// Sonner para el celular, donde usa mobileOffset y el ancho completo. Los colores son los de los mensajes de
// docs/diseno.md.

const ANGOSTA = '(max-width: 600px)';

function suscribir(avisarCambio) {
  const consulta = window.matchMedia(ANGOSTA);
  consulta.addEventListener('change', avisarCambio);
  return () => consulta.removeEventListener('change', avisarCambio);
}

const esAngosta = () => window.matchMedia(ANGOSTA).matches;

const colores = {
  '--normal-bg': '#ffffff',
  '--normal-border': '#e2e8f0',
  '--normal-text': '#0f172a',
  '--success-bg': '#f0fdf4',
  '--success-border': '#bbf7d0',
  '--success-text': '#166534',
  '--error-bg': '#fef2f2',
  '--error-border': '#fecaca',
  '--error-text': '#b91c1c',
  '--info-bg': '#eff5fb',
  '--info-border': '#b9d1eb',
  '--info-text': '#1c3a66',
  '--warning-bg': '#fffbeb',
  '--warning-border': '#fde68a',
  '--warning-text': '#92400e',
  '--border-radius': '0.5rem',
  // La X de cerrar, en la esquina de arriba a la derecha: Sonner la pone a la izquierda.
  '--toast-close-button-start': 'unset',
  '--toast-close-button-end': '0',
  '--toast-close-button-transform': 'translate(35%, -35%)',
  fontFamily: 'var(--font-sans)',
};

export function Avisos() {
  const angosta = useSyncExternalStore(suscribir, esAngosta);
  return (
    <Toaster
      position={angosta ? 'top-center' : 'bottom-right'}
      mobileOffset={{ top: 64 }}
      visibleToasts={3}
      richColors
      closeButton
      containerAriaLabel="Avisos"
      style={colores}
      toastOptions={{ closeButtonAriaLabel: 'Cerrar el aviso', className: 'text-sm' }}
      icons={{
        success: <CircleCheck size={18} aria-hidden="true" />,
        error: <CircleAlert size={18} aria-hidden="true" />,
        info: <Info size={18} aria-hidden="true" />,
        warning: <TriangleAlert size={18} aria-hidden="true" />,
        close: <X size={14} aria-hidden="true" />,
      }}
    />
  );
}
