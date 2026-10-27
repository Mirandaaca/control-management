import { toast } from 'sonner';

// Los toasts del panel (docs/diseno.md, "Mensajes"). El éxito de una acción se va solo; el error de una acción desde una
// lista queda hasta que se cierra, para que nadie se lo pierda. Los errores de un formulario no van aquí: van junto al
// campo o dentro del formulario.
//
// Hay un error a la vez, como el aviso de error que tenía cada pantalla: uno nuevo reemplaza al anterior, y quitarError()
// lo cierra cuando la acción se reintenta. Cada error lleva un id nuevo, porque Sonner no vuelve a mostrar un id que
// todavía está saliendo de la pantalla.
let ultimoError;

export const avisar = {
  exito: (texto) => toast.success(texto, { duration: 5000 }),
  error: (texto) => {
    avisar.quitarError();
    ultimoError = toast.error(texto, { duration: Infinity });
  },
  quitarError: () => {
    // Sin id, dismiss() cerraría todos los toasts, también los éxitos.
    if (ultimoError !== undefined) toast.dismiss(ultimoError);
    ultimoError = undefined;
  },
};
