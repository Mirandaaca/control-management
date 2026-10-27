import { CircleAlert, CircleCheck, Info, Map as IconoDeMapa, TriangleAlert } from 'lucide-react';
import { Skeleton } from './skeleton';

// Los componentes del panel que no dependen de Radix, cmdk ni el calendario (tarea 21). index.jsx los re-exporta, y
// las pantallas los usan desde ahí. Solo Login y App.jsx los importan de aquí: van en el archivo principal, y con
// index.jsx el inicio de sesión bajaría todo lo demás.

export function Card({ children, className = '' }) {
  return (
    <div className={`bg-white rounded-xl shadow-tarjeta border border-slate-200 p-5 ${className}`}>
      {children}
    </div>
  );
}

export function Alert({ children, type = 'error', className = '' }) {
  const styles = {
    error: { clase: 'bg-red-50 border-red-200 text-red-700', Icono: CircleAlert },
    success: { clase: 'bg-green-50 border-green-200 text-green-800', Icono: CircleCheck },
    warning: { clase: 'bg-amber-50 border-amber-200 text-amber-800', Icono: TriangleAlert },
    info: { clase: 'bg-marca-50 border-marca-200 text-marca-800', Icono: Info },
  };
  const { clase, Icono } = styles[type];
  return (
    <div role={type === 'error' ? 'alert' : 'status'} className={`flex items-start gap-2.5 border rounded-lg px-4 py-3 text-sm ${clase} ${className}`}>
      <Icono size={18} className="shrink-0 mt-px" aria-hidden="true" />
      <div className="min-w-0">{children}</div>
    </div>
  );
}

/**
 * Lo que se ve mientras llega la primera carga (tarea 21): la forma de lo que viene, en gris, en lugar de un indicador
 * que gira. Aparece a los 150 ms, así una carga rápida no parpadea, y la página no salta cuando llegan los datos. El
 * lector de pantalla oye el texto ("Cargando pedidos…"), no las formas; sin texto, es la parte de una carga que ya
 * anuncia otro (los totales de un reporte, junto a su tabla). Adentro van los Esqueleto… de abajo; una tabla no lo usa:
 * Table tiene su prop cargando. El indicador que gira queda solo dentro de los botones ("Guardando…").
 */
export function ZonaDeCarga({ texto, children, className = '' }) {
  return (
    <div aria-busy="true" className={className}>
      {texto && <span role="status" className="sr-only">{texto}</span>}
      <div aria-hidden="true" className="aparecer-demorado">{children}</div>
    </div>
  );
}

/**
 * La barra fina de arriba de una tarjeta mientras se piden otros datos (useRecarga): lo de antes queda a la vista, sin
 * parpadear. Va dentro de una tarjeta con relative, y aparece a los 150 ms.
 */
export function BarraDeRecarga() {
  return (
    <div role="progressbar" aria-label="Actualizando" className="absolute inset-x-0 top-0 z-10 h-0.5 overflow-hidden aparecer-demorado">
      <div className="barra-de-recarga h-full w-2/5 bg-marca-500" />
    </div>
  );
}

/** Una barra gris suelta, para armar el esqueleto de algo que no tiene una forma de aquí (la HP impresa). */
export function Esqueleto({ className = '' }) {
  return <Skeleton className={className} />;
}

/** Las tarjetas de los indicadores, con la grilla de las reales: la de Inicio, o la de un reporte (grilla). */
export function EsqueletoDeIndicadores({ cantidad, grilla = 'grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3 mb-6' }) {
  return (
    <ul className={grilla}>
      {Array.from({ length: cantidad }, (_, i) => (
        <li key={i} className="flex items-center gap-4 rounded-xl border border-slate-200 bg-white p-5 shadow-tarjeta">
          <Skeleton className="size-10 shrink-0 rounded-lg" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-7 w-24" />
          </div>
        </li>
      ))}
    </ul>
  );
}

/** Un gráfico en su tarjeta: el título y el alto del gráfico. */
export function EsqueletoDeGrafico({ alto = 'h-60' }) {
  return (
    <Card>
      <Skeleton className="h-4 w-40 mb-4" />
      <Skeleton className={`w-full ${alto}`} />
    </Card>
  );
}

/** Pares de etiqueta y campo (o valor), como un formulario o un detalle. */
export function EsqueletoDeDetalle({ pares = 3 }) {
  return (
    <div className="space-y-5">
      {Array.from({ length: pares }, (_, i) => (
        <div key={i} className="space-y-2">
          <Skeleton className="h-3.5 w-28" />
          <Skeleton className="h-10 w-full rounded-lg" />
        </div>
      ))}
    </div>
  );
}

/** Un mapa, con el alto de los del panel. */
export function EsqueletoDeMapa({ alto = 'h-104 sm:h-128' }) {
  return (
    <div className={`relative ${alto}`}>
      <Skeleton className="absolute inset-0 rounded-none" />
      <IconoDeMapa size={32} aria-hidden="true" className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 text-slate-400" />
    </div>
  );
}

/** Los renglones de una lista corta, como las rutas del día. */
export function EsqueletoDeLista({ filas = 4 }) {
  return (
    <ul className="space-y-1 p-2">
      {Array.from({ length: filas }, (_, i) => (
        <li key={i} className="space-y-1.5 px-3 py-2">
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-3 w-1/2" />
        </li>
      ))}
    </ul>
  );
}

/** Una pantalla que todavía se está bajando (React.lazy en App.jsx): su encabezado y una tarjeta. */
export function EsqueletoDePagina() {
  return (
    <ZonaDeCarga texto="Cargando la pantalla…">
      <div className="mb-6 space-y-2">
        <Skeleton className="h-8 w-56" />
        <Skeleton className="h-4 w-80 max-w-full" />
      </div>
      <Card className="space-y-4">
        {Array.from({ length: 6 }, (_, i) => <Skeleton key={i} className="h-5 w-full" />)}
      </Card>
    </ZonaDeCarga>
  );
}
