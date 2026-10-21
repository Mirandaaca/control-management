import { Plus, Trash2 } from 'lucide-react';
import { Alert, Btn, Input, Selector } from './ui';
import { formatearCantidad, formatearMonto } from '../utils/formato';
import { subtotalDe } from '../utils/lineas';

/**
 * Las líneas de productos de una venta o de un pedido: producto, cantidad y descuento por unidad, con su subtotal.
 * Avisa el stock de cada producto, pero no lo exige: se valida al confirmar la venta o al entregar el pedido, y puede
 * llegar mercadería antes. Los errores vienen de la API con claves como "detalles[0].cantidad".
 */
export default function LineasDeProductos({ lineas, productos, errores, sinStock, onCambiar, onQuitar, onAgregar }) {
  return (
    <fieldset className="space-y-3">
      <legend className="text-sm font-medium text-slate-700 mb-1">Productos</legend>
      {errores.detalles && <Alert type="error">{errores.detalles}</Alert>}
      {lineas.map((linea, i) => {
        const producto = productos.find((p) => String(p.id) === linea.productoId);
        let ayudaDelProducto;
        if (producto) {
          ayudaDelProducto = Number(linea.cantidad) > producto.stockActual
            ? `Hay ${formatearCantidad(producto.stockActual)} en stock: ${sinStock}`
            : `Hay ${formatearCantidad(producto.stockActual)} en stock.`;
        }
        return (
          <div key={linea.clave} className="grid gap-3 rounded-lg border border-slate-200 p-3 sm:grid-cols-[minmax(0,1fr)_7rem_8rem_auto] sm:items-start">
            <Selector label={`Producto ${i + 1}`} placeholder="Elige el producto" value={linea.productoId}
              error={errores[`detalles[${i}].productoId`]}
              opciones={productos.map((p) => ({ valor: String(p.id), texto: `${p.descripcion} · ${formatearMonto(p.precio)}` }))}
              ayuda={ayudaDelProducto}
              onChange={(productoId) => onCambiar(linea.clave, i, { productoId })} />
            <Input label="Cantidad" type="number" inputMode="decimal" min="0" step="1" value={linea.cantidad} autoComplete="off"
              error={errores[`detalles[${i}].cantidad`]}
              onChange={(e) => onCambiar(linea.clave, i, { cantidad: e.target.value })} />
            <Input label="Descuento c/u" type="number" inputMode="decimal" min="0" step="0.01" value={linea.descuento}
              placeholder="0,00" autoComplete="off" error={errores[`detalles[${i}].descuento`]}
              onChange={(e) => onCambiar(linea.clave, i, { descuento: e.target.value })} />
            <div className="flex items-center justify-between gap-2 sm:flex-col sm:items-end sm:pt-7">
              <span className="text-sm font-semibold tabular-nums whitespace-nowrap">{formatearMonto(subtotalDe(linea, productos))}</span>
              {lineas.length > 1 && (
                <Btn variant="ghost" aria-label={`Quitar el producto ${i + 1}`} onClick={() => onQuitar(linea.clave)}>
                  <Trash2 size={14} aria-hidden="true" />
                </Btn>
              )}
            </div>
          </div>
        );
      })}
      <Btn variant="outline" onClick={onAgregar}>
        <Plus size={14} aria-hidden="true" />
        Agregar producto
      </Btn>
    </fieldset>
  );
}
