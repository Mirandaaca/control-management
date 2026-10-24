// Los ids de EstadosDePedido de la API (los siembra la base y no cambian).
export const PENDIENTE = 1;
export const EN_PROCESO = 2;
export const EN_RUTA = 3;
export const ENTREGADO = 4;
export const ANULADO = 5;

/** Un pedido entregado o anulado ya no cambia. */
export const esFinal = (estadoId) => estadoId === ENTREGADO || estadoId === ANULADO;

/** Se modifica solo mientras no salió a entregarse. */
export const esModificable = (estadoId) => estadoId === PENDIENTE || estadoId === EN_PROCESO;

/** El estado al que avanza (Pendiente → En proceso → En ruta). Desde En ruta lo que sigue es la entrega. */
export const SIGUIENTE = { [PENDIENTE]: { id: EN_PROCESO, nombre: 'En proceso' }, [EN_PROCESO]: { id: EN_RUTA, nombre: 'En ruta' } };

// TipoVenta de la API, para la HP que nació de la entrega.
export const CONTADO = 0;
export const CREDITO = 1;
