/// Un producto activo, con su precio de venta y su stock: el `ProductoOpcionDTO` de `GET /productos/opciones`.
class Producto {
  const Producto({
    required this.id,
    required this.sku,
    required this.descripcion,
    required this.precio,
    required this.stock,
    this.categoria,
  });

  factory Producto.fromJson(Map<String, dynamic> json) => Producto(
    id: json['id'] as int,
    sku: json['sku'] as String,
    descripcion: json['descripcion'] as String,
    categoria: json['categoria'] as String?,
    precio: (json['precio'] as num).toDouble(),
    stock: (json['stockActual'] as num).toDouble(),
  );

  final int id;

  /// El código del producto.
  final String sku;
  final String descripcion;
  final String? categoria;
  final double precio;
  final double stock;

  bool get sinStock => stock <= 0;
}
