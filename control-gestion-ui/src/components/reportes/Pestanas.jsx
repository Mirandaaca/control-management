import { useRef } from 'react';

/**
 * Las pestañas de Reportes (8.6.4), con la activa en marca. "accion" va a la derecha, como "Exportar Excel" en las que
 * exportan. Con las flechas se pasa de una pestaña a otra.
 */
export default function Pestanas({ pestanas, activa, onCambiar, accion }) {
  const botones = useRef({});

  const moverCon = (evento, indice) => {
    const paso = { ArrowRight: 1, ArrowLeft: -1 }[evento.key];
    if (!paso) return;
    evento.preventDefault();
    const siguiente = pestanas[(indice + paso + pestanas.length) % pestanas.length];
    onCambiar(siguiente.id);
    botones.current[siguiente.id]?.focus();
  };

  return (
    <div className="mb-5 flex flex-col-reverse gap-3 border-b border-slate-200 sm:flex-row sm:items-end">
      <div role="tablist" aria-label="Reportes" className="-mb-px flex gap-1 overflow-x-auto">
        {pestanas.map((p, i) => (
          <button
            key={p.id}
            ref={(el) => { botones.current[p.id] = el; }}
            type="button"
            role="tab"
            id={`pestana-${p.id}`}
            aria-selected={p.id === activa}
            aria-controls={`panel-${p.id}`}
            tabIndex={p.id === activa ? 0 : -1}
            onClick={() => onCambiar(p.id)}
            onKeyDown={(e) => moverCon(e, i)}
            className={`shrink-0 cursor-pointer border-b-2 px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 ${
              p.id === activa ? 'border-marca-600 text-marca-700' : 'border-transparent text-slate-600 hover:text-slate-900'
            }`}
          >
            {p.nombre}
          </button>
        ))}
      </div>
      {accion && <div className="sm:ml-auto sm:mb-1.5">{accion}</div>}
    </div>
  );
}
