/**
 * Tabla dentro de su tarjeta. "barra" va arriba (buscador y filtros) y "pie" abajo (paginación); "estado" reemplaza a
 * las filas mientras carga, si no hay datos o si hubo un error. Un encabezado puede ser un texto o
 * { texto, derecha: true }, para las columnas de montos y cantidades, o { texto, oculto: true }, para una columna que
 * se entiende sin título (como las casillas para elegir filas) pero que el lector de pantalla tiene que nombrar.
 */
export function Table({ headers, children, empty = 'Sin registros.', barra, estado, pie }) {
  const sinFilas = !children || (Array.isArray(children) && children.length === 0);
  return (
    <div className="bg-white rounded-xl shadow-tarjeta border border-slate-200 overflow-hidden">
      {barra}
      {estado ?? (
        <>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200">
                  {headers.map((h) => {
                    const { texto, derecha, oculto } = typeof h === 'string' ? { texto: h } : h;
                    return (
                      <th key={texto} scope="col"
                        className={`px-4 py-3 ${derecha ? 'text-right' : 'text-left'} text-xs font-semibold text-slate-600 uppercase tracking-wide whitespace-nowrap`}>
                        {oculto ? <span className="sr-only">{texto}</span> : texto}
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {children}
              </tbody>
            </table>
          </div>
          {sinFilas && <p className="text-center text-slate-600 text-sm py-10">{empty}</p>}
          {pie}
        </>
      )}
    </div>
  );
}

// Un código (SKU, HP-2026-00001) no se traduce: el traductor del navegador podría cambiarlo.
export function Td({ children, mono = false, className = '' }) {
  return (
    <td translate={mono ? 'no' : undefined} className={`px-4 py-2.5 text-slate-900 ${mono ? 'font-mono text-xs whitespace-nowrap' : ''} ${className}`}>
      {children}
    </td>
  );
}

export function Tr({ children }) {
  return (
    <tr className="hover:bg-slate-50 transition-colors">
      {children}
    </tr>
  );
}
