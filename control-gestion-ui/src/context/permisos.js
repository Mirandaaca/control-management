// Las claves de ControlGestion.API/Seguridad/Permisos.cs, con la misma estructura. Si cambia allá, cambia aquí.
export const PERMISOS = {
  pedidos: {
    registrarPedido: 'pedidos.registrar-pedido',
    consultarPedidos: 'pedidos.consultar-pedidos',
    modificarPedido: 'pedidos.modificar-pedido',
    anularPedido: 'pedidos.anular-pedido',
    cambiarEstado: 'pedidos.cambiar-estado',
    reprogramarEntrega: 'pedidos.reprogramar-entrega',
    asignarPedidos: 'pedidos.asignar-pedidos',
    verBandeja: 'pedidos.ver-bandeja',
    registrarEntrega: 'pedidos.registrar-entrega',
    generarReporte: 'pedidos.generar-reporte',
  },
  ventas: {
    registrarHp: 'ventas.registrar-hp',
    confirmarHp: 'ventas.confirmar-hp',
    consultarHp: 'ventas.consultar-hp',
    anularHp: 'ventas.anular-hp',
    registrarCliente: 'ventas.registrar-cliente',
    editarCliente: 'ventas.editar-cliente',
    asignarEstadoCliente: 'ventas.asignar-estado-cliente',
    consultarProductos: 'ventas.consultar-productos',
    capturarUbicacion: 'ventas.capturar-ubicacion',
    verUbicacionCliente: 'ventas.ver-ubicacion-cliente',
    registrarIngreso: 'ventas.registrar-ingreso',
    consultarKardex: 'ventas.consultar-kardex',
  },
  cobranzas: {
    registrarPago: 'cobranzas.registrar-pago',
    consultarPagos: 'cobranzas.consultar-pagos',
    anularPago: 'cobranzas.anular-pago',
    consultarDeudas: 'cobranzas.consultar-deudas',
    marcarIncobrable: 'cobranzas.marcar-incobrable',
    generarReporte: 'cobranzas.generar-reporte',
  },
  eventos: {
    registrarEvento: 'eventos.registrar-evento',
    consultarEventos: 'eventos.consultar-eventos',
    modificarEvento: 'eventos.modificar-evento',
    resolverEvento: 'eventos.resolver-evento',
    agregarFoto: 'eventos.agregar-foto',
    eliminarFoto: 'eventos.eliminar-foto',
    verMapa: 'eventos.ver-mapa',
    generarReporte: 'eventos.generar-reporte',
  },
  territorial: {
    verMapaClientes: 'territorial.ver-mapa-clientes',
    verClientesPorVisitar: 'territorial.ver-clientes-por-visitar',
    planificarRuta: 'territorial.planificar-ruta',
    seguirRuta: 'territorial.seguir-ruta',
    recorrerRuta: 'territorial.recorrer-ruta',
    dibujarLimites: 'territorial.dibujar-limites',
    registrarPuntoReferencia: 'territorial.registrar-punto-referencia',
  },
  reportes: {
    ventasPorPromotor: 'reportes.ventas-por-promotor',
    ventasPorZona: 'reportes.ventas-por-zona',
    clientesNuevosRecurrentes: 'reportes.clientes-nuevos-recurrentes',
    inventario: 'reportes.inventario',
    imprimirHp: 'reportes.imprimir-hp',
  },
  parametros: {
    modificarDiasRevisita: 'parametros.modificar-dias-revisita',
    registrarZona: 'parametros.registrar-zona',
    modificarZona: 'parametros.modificar-zona',
    eliminarZona: 'parametros.eliminar-zona',
    registrarProducto: 'parametros.registrar-producto',
    modificarProducto: 'parametros.modificar-producto',
    eliminarProducto: 'parametros.eliminar-producto',
    registrarEstadoCliente: 'parametros.registrar-estado-cliente',
    modificarEstadoCliente: 'parametros.modificar-estado-cliente',
    eliminarEstadoCliente: 'parametros.eliminar-estado-cliente',
  },
  seguridad: {
    crearUsuario: 'seguridad.crear-usuario',
    editarUsuario: 'seguridad.editar-usuario',
    activarUsuario: 'seguridad.activar-usuario',
    crearRol: 'seguridad.crear-rol',
    editarRol: 'seguridad.editar-rol',
    eliminarRol: 'seguridad.eliminar-rol',
    consultarBitacora: 'seguridad.consultar-bitacora',
    verCarteraCompleta: 'seguridad.ver-cartera-completa',
  },
};

const { pedidos, ventas, cobranzas, eventos, territorial, reportes, parametros, seguridad } = PERMISOS;

// Qué permisos abren cada pantalla del panel: con uno basta. El menú y las rutas usan la misma lista, así nunca se
// muestra un enlace a una pantalla que después dice "Sin acceso". Inicio y Mi perfil no llevan permiso.
export const PANTALLAS = {
  clientes: [ventas.registrarCliente, ventas.editarCliente, ventas.asignarEstadoCliente, ventas.verUbicacionCliente],
  hojasPedido: [ventas.consultarHp, ventas.anularHp],
  inventario: [ventas.consultarProductos, ventas.consultarKardex, ventas.registrarIngreso],
  // Como en la API: quien opera sobre un pedido también tiene que verlo. La bandeja del día y la entrega son de la app
  // (tarea 15), y la app usa esta misma lista.
  pedidos: [pedidos.consultarPedidos, pedidos.modificarPedido, pedidos.anularPedido, pedidos.cambiarEstado,
    pedidos.reprogramarEntrega, pedidos.asignarPedidos, pedidos.registrarEntrega],
  reportePedidos: [pedidos.generarReporte],
  eventos: [eventos.consultarEventos, eventos.registrarEvento, eventos.modificarEvento, eventos.resolverEvento,
    eventos.agregarFoto, eventos.eliminarFoto],
  mapaEventos: [eventos.verMapa],
  reporteEventos: [eventos.generarReporte],
  cobranzas: [cobranzas.consultarDeudas, cobranzas.consultarPagos],
  mapaClientes: [territorial.verMapaClientes],
  porVisitar: [territorial.verClientesPorVisitar],
  rutas: [territorial.planificarRuta, territorial.seguirRuta, territorial.recorrerRuta],
  // El gerente dibuja y registra puntos; quien ve el mapa de clientes (el promotor) lo mira sin acciones.
  mapaZonas: [territorial.dibujarLimites, territorial.registrarPuntoReferencia, territorial.verMapaClientes],
  zonas: [parametros.registrarZona, parametros.modificarZona, parametros.eliminarZona],
  productos: [parametros.registrarProducto, parametros.modificarProducto, parametros.eliminarProducto],
  configuracionGeneral: [parametros.modificarDiasRevisita],
  estadosCliente: [parametros.registrarEstadoCliente, parametros.modificarEstadoCliente, parametros.eliminarEstadoCliente],
  usuarios: [seguridad.crearUsuario, seguridad.editarUsuario, seguridad.activarUsuario],
  roles: [seguridad.crearRol, seguridad.editarRol, seguridad.eliminarRol],
  reportes: [reportes.ventasPorPromotor, reportes.ventasPorZona, cobranzas.generarReporte, reportes.inventario,
    seguridad.consultarBitacora],
};

// Quien asigna pedidos, zonas o rutas, o filtra el reporte de pedidos, el mapa de eventos o el reporte de incidencias,
// puede pedir la lista de promotores (GET /usuarios/promotores).
export const ELIGEN_PROMOTOR = [pedidos.asignarPedidos, parametros.registrarZona, parametros.modificarZona,
  territorial.planificarRuta, pedidos.generarReporte, eventos.verMapa, eventos.generarReporte];
