/** El subtotal de una línea de productos: (precio − descuento por unidad) × cantidad, con el precio vigente. */
export function subtotalDe(linea, productos) {
  const precio = productos.find((p) => String(p.id) === linea.productoId)?.precio ?? 0;
  return (precio - (Number(linea.descuento) || 0)) * (Number(linea.cantidad) || 0);
}

/** El total de una venta o un pedido: la suma de sus líneas menos el descuento sobre el total. */
export function totalDe(lineas, productos, descuento) {
  return lineas.reduce((suma, l) => suma + subtotalDe(l, productos), 0) - (Number(descuento) || 0);
}
