/// Las claves de los permisos de `ControlGestion.API/Seguridad/Permisos.cs`, con la misma forma que `PERMISOS` del
/// panel (`control-gestion-ui/src/context/permisos.js`). Si cambian allá, cambian aquí: una prueba compara las dos.
///
/// La app pregunta por permisos, nunca por roles: `sesion.tiene([Permisos.ventas.registrarHp])`.
abstract final class Permisos {
  static const pedidos = _Pedidos();
  static const ventas = _Ventas();
  static const cobranzas = _Cobranzas();
  static const eventos = _Eventos();
  static const territorial = _Territorial();
  static const reportes = _Reportes();
  static const parametros = _Parametros();
  static const seguridad = _Seguridad();

  /// Las 66 claves.
  static List<String> get todas => [
    ...pedidos.todas,
    ...ventas.todas,
    ...cobranzas.todas,
    ...eventos.todas,
    ...territorial.todas,
    ...reportes.todas,
    ...parametros.todas,
    ...seguridad.todas,
  ];
}

final class _Pedidos {
  const _Pedidos();

  final registrarPedido = 'pedidos.registrar-pedido';
  final consultarPedidos = 'pedidos.consultar-pedidos';
  final modificarPedido = 'pedidos.modificar-pedido';
  final anularPedido = 'pedidos.anular-pedido';
  final cambiarEstado = 'pedidos.cambiar-estado';
  final reprogramarEntrega = 'pedidos.reprogramar-entrega';
  final asignarPedidos = 'pedidos.asignar-pedidos';
  final verBandeja = 'pedidos.ver-bandeja';
  final registrarEntrega = 'pedidos.registrar-entrega';
  final generarReporte = 'pedidos.generar-reporte';

  List<String> get todas => [
    registrarPedido,
    consultarPedidos,
    modificarPedido,
    anularPedido,
    cambiarEstado,
    reprogramarEntrega,
    asignarPedidos,
    verBandeja,
    registrarEntrega,
    generarReporte,
  ];
}

final class _Ventas {
  const _Ventas();

  final registrarHp = 'ventas.registrar-hp';
  final confirmarHp = 'ventas.confirmar-hp';
  final consultarHp = 'ventas.consultar-hp';
  final anularHp = 'ventas.anular-hp';
  final registrarCliente = 'ventas.registrar-cliente';
  final editarCliente = 'ventas.editar-cliente';
  final asignarEstadoCliente = 'ventas.asignar-estado-cliente';
  final consultarProductos = 'ventas.consultar-productos';
  final capturarUbicacion = 'ventas.capturar-ubicacion';
  final verUbicacionCliente = 'ventas.ver-ubicacion-cliente';
  final registrarIngreso = 'ventas.registrar-ingreso';
  final consultarKardex = 'ventas.consultar-kardex';

  List<String> get todas => [
    registrarHp,
    confirmarHp,
    consultarHp,
    anularHp,
    registrarCliente,
    editarCliente,
    asignarEstadoCliente,
    consultarProductos,
    capturarUbicacion,
    verUbicacionCliente,
    registrarIngreso,
    consultarKardex,
  ];
}

final class _Cobranzas {
  const _Cobranzas();

  final registrarPago = 'cobranzas.registrar-pago';
  final consultarPagos = 'cobranzas.consultar-pagos';
  final anularPago = 'cobranzas.anular-pago';
  final consultarDeudas = 'cobranzas.consultar-deudas';
  final marcarIncobrable = 'cobranzas.marcar-incobrable';
  final generarReporte = 'cobranzas.generar-reporte';

  List<String> get todas => [
    registrarPago,
    consultarPagos,
    anularPago,
    consultarDeudas,
    marcarIncobrable,
    generarReporte,
  ];
}

final class _Eventos {
  const _Eventos();

  final registrarEvento = 'eventos.registrar-evento';
  final consultarEventos = 'eventos.consultar-eventos';
  final modificarEvento = 'eventos.modificar-evento';
  final resolverEvento = 'eventos.resolver-evento';
  final agregarFoto = 'eventos.agregar-foto';
  final eliminarFoto = 'eventos.eliminar-foto';
  final verMapa = 'eventos.ver-mapa';
  final generarReporte = 'eventos.generar-reporte';

  List<String> get todas => [
    registrarEvento,
    consultarEventos,
    modificarEvento,
    resolverEvento,
    agregarFoto,
    eliminarFoto,
    verMapa,
    generarReporte,
  ];
}

final class _Territorial {
  const _Territorial();

  final verMapaClientes = 'territorial.ver-mapa-clientes';
  final verClientesPorVisitar = 'territorial.ver-clientes-por-visitar';
  final planificarRuta = 'territorial.planificar-ruta';
  final seguirRuta = 'territorial.seguir-ruta';
  final recorrerRuta = 'territorial.recorrer-ruta';
  final dibujarLimites = 'territorial.dibujar-limites';
  final registrarPuntoReferencia = 'territorial.registrar-punto-referencia';

  List<String> get todas => [
    verMapaClientes,
    verClientesPorVisitar,
    planificarRuta,
    seguirRuta,
    recorrerRuta,
    dibujarLimites,
    registrarPuntoReferencia,
  ];
}

final class _Reportes {
  const _Reportes();

  final ventasPorPromotor = 'reportes.ventas-por-promotor';
  final ventasPorZona = 'reportes.ventas-por-zona';
  final clientesNuevosRecurrentes = 'reportes.clientes-nuevos-recurrentes';
  final inventario = 'reportes.inventario';
  final imprimirHp = 'reportes.imprimir-hp';

  List<String> get todas => [ventasPorPromotor, ventasPorZona, clientesNuevosRecurrentes, inventario, imprimirHp];
}

final class _Parametros {
  const _Parametros();

  final modificarDiasRevisita = 'parametros.modificar-dias-revisita';
  final registrarZona = 'parametros.registrar-zona';
  final modificarZona = 'parametros.modificar-zona';
  final eliminarZona = 'parametros.eliminar-zona';
  final registrarProducto = 'parametros.registrar-producto';
  final modificarProducto = 'parametros.modificar-producto';
  final eliminarProducto = 'parametros.eliminar-producto';
  final registrarEstadoCliente = 'parametros.registrar-estado-cliente';
  final modificarEstadoCliente = 'parametros.modificar-estado-cliente';
  final eliminarEstadoCliente = 'parametros.eliminar-estado-cliente';

  List<String> get todas => [
    modificarDiasRevisita,
    registrarZona,
    modificarZona,
    eliminarZona,
    registrarProducto,
    modificarProducto,
    eliminarProducto,
    registrarEstadoCliente,
    modificarEstadoCliente,
    eliminarEstadoCliente,
  ];
}

final class _Seguridad {
  const _Seguridad();

  final crearUsuario = 'seguridad.crear-usuario';
  final editarUsuario = 'seguridad.editar-usuario';
  final activarUsuario = 'seguridad.activar-usuario';
  final crearRol = 'seguridad.crear-rol';
  final editarRol = 'seguridad.editar-rol';
  final eliminarRol = 'seguridad.eliminar-rol';
  final consultarBitacora = 'seguridad.consultar-bitacora';
  final verCarteraCompleta = 'seguridad.ver-cartera-completa';

  List<String> get todas => [
    crearUsuario,
    editarUsuario,
    activarUsuario,
    crearRol,
    editarRol,
    eliminarRol,
    consultarBitacora,
    verCarteraCompleta,
  ];
}

/// Quien puede pedir la lista de promotores (`GET /usuarios/promotores`), como `ELIGEN_PROMOTOR` del panel.
final eligenPromotor = [
  Permisos.pedidos.asignarPedidos,
  Permisos.parametros.registrarZona,
  Permisos.parametros.modificarZona,
  Permisos.territorial.planificarRuta,
  Permisos.pedidos.generarReporte,
  Permisos.cobranzas.generarReporte,
  Permisos.reportes.ventasPorPromotor,
  Permisos.eventos.verMapa,
  Permisos.eventos.generarReporte,
];
