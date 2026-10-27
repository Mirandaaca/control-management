import { BarraDeRecarga, Esqueleto } from './ui';

// Los anchos de las barras del esqueleto, para que las filas no se vean todas iguales.
const ANCHOS = ['w-3/4', 'w-1/2', 'w-2/3', 'w-5/6'];

/** Lo que se ve en cada celda mientras carga: una barra con la forma de lo que va en esa columna. */
function CeldaDeCarga({ encabezado, fila, columna }) {
  const { texto, derecha, oculto } = typeof encabezado === 'string' ? { texto: encabezado } : encabezado;
  let barra = <Esqueleto className={`h-4 ${ANCHOS[(fila + columna) % ANCHOS.length]}`} />;
  if (oculto) barra = <Esqueleto className="size-4 rounded" />;
  else if (derecha) barra = <Esqueleto className="h-4 w-16 ml-auto" />;
  else if (!texto) barra = <Esqueleto className="h-4 w-12 ml-auto" />;
  return <td className="px-4 py-3">{barra}</td>;
}

/**
 * Tabla dentro de su tarjeta. "barra" va arriba (buscador y filtros) y "pie" abajo (paginación); "estado" reemplaza a
 * las filas si no hay datos o si hubo un error. Un encabezado puede ser un texto o
 * { texto, derecha: true }, para las columnas de montos y cantidades, o { texto, oculto: true }, para una columna que
 * se entiende sin título (como las casillas para elegir filas) pero que el lector de pantalla tiene que nombrar. Con
 * { texto, segundo }, el encabezado va en dos renglones, como "Cliente / Promotor" del 8.6.4.
 * "compacta" deja las celdas con 12 px a cada lado: así una tabla ancha entra junto al menú (8.6.4).
 * Mientras llega la primera carga, "cargando" (el texto, como "Cargando pedidos…") muestra el encabezado y 8 filas de
 * esqueleto; con true, el esqueleto no se anuncia, porque otra tabla de la misma carga ya lo hace. Al filtrar o cambiar
 * de página, "recargando" deja las filas de antes con una barra fina arriba (tarea 21).
 */
export function Table({
  headers, children, empty = 'Sin registros.', barra, estado, pie, compacta = false, titulo, cargando, recargando = false,
}) {
  const sinFilas = !children || (Array.isArray(children) && children.length === 0);
  const tabla = (filas) => (
    // relative: el título oculto de una columna (sr-only, absoluto) se queda dentro del desplazamiento; si no, ensancha
    // la página en el celular.
    <div className="relative overflow-x-auto">
      <table className={`w-full text-sm ${compacta ? '[&_td]:px-3 [&_th]:px-3' : ''}`} aria-label={titulo}>
        <thead>
          <tr className="bg-slate-50 border-b border-slate-200">
            {headers.map((h) => {
              const { texto, derecha, oculto, segundo } = typeof h === 'string' ? { texto: h } : h;
              return (
                <th key={texto} scope="col"
                  className={`px-4 py-3 ${derecha ? 'text-right' : 'text-left'} text-xs font-semibold text-slate-600 uppercase tracking-wide whitespace-nowrap`}>
                  {oculto && <span className="sr-only">{texto}</span>}
                  {!oculto && (segundo ? <><span className="block">{texto}</span><span className="block">{segundo}</span></> : texto)}
                </th>
              );
            })}
          </tr>
        </thead>
        {filas}
      </table>
    </div>
  );

  let contenido;
  if (cargando) contenido = (
    <>
      {typeof cargando === 'string' && <span role="status" className="sr-only">{cargando}</span>}
      {tabla(
        <tbody aria-hidden="true" className="divide-y divide-slate-200 aparecer-demorado">
          {Array.from({ length: 8 }, (_, fila) => (
            <tr key={fila}>
              {headers.map((h, columna) => (
                <CeldaDeCarga key={typeof h === 'string' ? h : h.texto} encabezado={h} fila={fila} columna={columna} />
              ))}
            </tr>
          ))}
        </tbody>,
      )}
    </>
  );
  else contenido = estado ?? (
    <>
      {tabla(<tbody className="divide-y divide-slate-200">{children}</tbody>)}
      {sinFilas && <p className="text-center text-slate-600 text-sm py-10">{empty}</p>}
      {pie}
    </>
  );

  return (
    <div className="relative bg-white rounded-xl shadow-tarjeta border border-slate-200 overflow-hidden"
      aria-busy={cargando || recargando ? true : undefined}>
      {recargando && !cargando && <BarraDeRecarga />}
      {barra}
      {contenido}
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
