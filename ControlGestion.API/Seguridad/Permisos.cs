using static ControlGestion.API.Seguridad.Roles;

namespace ControlGestion.API.Seguridad;

/// <summary>
/// Catálogo de permisos de docs/permisos-por-rol.md: uno por operación. Las claves son las que usan [Permiso] y el panel.
/// El sembrador las copia a la tabla Permisos al arrancar; desde el panel solo se asignan a los roles.
/// </summary>
public static class Permisos
{
    public static class Pedidos
    {
        public const string Modulo = "Gestión de Pedidos";
        public const string RegistrarPedido = "pedidos.registrar-pedido";
        public const string ConsultarPedidos = "pedidos.consultar-pedidos";
        public const string ModificarPedido = "pedidos.modificar-pedido";
        public const string AnularPedido = "pedidos.anular-pedido";
        public const string CambiarEstado = "pedidos.cambiar-estado";
        public const string ReprogramarEntrega = "pedidos.reprogramar-entrega";
        public const string AsignarPedidos = "pedidos.asignar-pedidos";
        public const string VerBandeja = "pedidos.ver-bandeja";
        public const string RegistrarEntrega = "pedidos.registrar-entrega";
        public const string GenerarReporte = "pedidos.generar-reporte";
    }

    public static class Ventas
    {
        public const string Modulo = "Gestión de Ventas Geolocalizadas";
        public const string RegistrarHp = "ventas.registrar-hp";
        public const string ConfirmarHp = "ventas.confirmar-hp";
        public const string ConsultarHp = "ventas.consultar-hp";
        public const string AnularHp = "ventas.anular-hp";
        public const string RegistrarCliente = "ventas.registrar-cliente";
        public const string EditarCliente = "ventas.editar-cliente";
        public const string AsignarEstadoCliente = "ventas.asignar-estado-cliente";
        public const string ConsultarProductos = "ventas.consultar-productos";
        public const string CapturarUbicacion = "ventas.capturar-ubicacion";
        public const string VerUbicacionCliente = "ventas.ver-ubicacion-cliente";
        public const string RegistrarIngreso = "ventas.registrar-ingreso";
        public const string ConsultarKardex = "ventas.consultar-kardex";
    }

    public static class Cobranzas
    {
        public const string Modulo = "Gestión de Cobranzas";
        public const string RegistrarPago = "cobranzas.registrar-pago";
        public const string ConsultarPagos = "cobranzas.consultar-pagos";
        public const string AnularPago = "cobranzas.anular-pago";
        public const string ConsultarDeudas = "cobranzas.consultar-deudas";
        public const string MarcarIncobrable = "cobranzas.marcar-incobrable";
        public const string GenerarReporte = "cobranzas.generar-reporte";
    }

    public static class Eventos
    {
        public const string Modulo = "Gestión de Eventos y Evidencias";
        public const string RegistrarEvento = "eventos.registrar-evento";
        public const string ConsultarEventos = "eventos.consultar-eventos";
        public const string ModificarEvento = "eventos.modificar-evento";
        public const string ResolverEvento = "eventos.resolver-evento";
        public const string AgregarFoto = "eventos.agregar-foto";
        public const string EliminarFoto = "eventos.eliminar-foto";
        public const string VerMapa = "eventos.ver-mapa";
        public const string GenerarReporte = "eventos.generar-reporte";
    }

    public static class Territorial
    {
        public const string Modulo = "Visualización Territorial y Re-visitas";
        public const string VerMapaClientes = "territorial.ver-mapa-clientes";
        public const string VerClientesPorVisitar = "territorial.ver-clientes-por-visitar";
        public const string PlanificarRuta = "territorial.planificar-ruta";
        public const string SeguirRuta = "territorial.seguir-ruta";
        public const string RecorrerRuta = "territorial.recorrer-ruta";
        public const string DibujarLimites = "territorial.dibujar-limites";
        public const string RegistrarPuntoReferencia = "territorial.registrar-punto-referencia";
    }

    public static class Reportes
    {
        public const string Modulo = "Reportes";
        public const string VentasPorPromotor = "reportes.ventas-por-promotor";
        public const string VentasPorZona = "reportes.ventas-por-zona";
        public const string ClientesNuevosRecurrentes = "reportes.clientes-nuevos-recurrentes";
        public const string Inventario = "reportes.inventario";
        public const string ImprimirHp = "reportes.imprimir-hp";
    }

    public static class Parametros
    {
        public const string Modulo = "Parámetros y Configuraciones";
        public const string ModificarDiasRevisita = "parametros.modificar-dias-revisita";
        public const string RegistrarZona = "parametros.registrar-zona";
        public const string ModificarZona = "parametros.modificar-zona";
        public const string EliminarZona = "parametros.eliminar-zona";
        public const string RegistrarProducto = "parametros.registrar-producto";
        public const string ModificarProducto = "parametros.modificar-producto";
        public const string EliminarProducto = "parametros.eliminar-producto";
        public const string RegistrarEstadoCliente = "parametros.registrar-estado-cliente";
        public const string ModificarEstadoCliente = "parametros.modificar-estado-cliente";
        public const string EliminarEstadoCliente = "parametros.eliminar-estado-cliente";
    }

    public static class Seguridad
    {
        public const string Modulo = "Usuario, Seguridad y Auditoría";
        public const string CrearUsuario = "seguridad.crear-usuario";
        public const string EditarUsuario = "seguridad.editar-usuario";
        public const string ActivarUsuario = "seguridad.activar-usuario";
        public const string CrearRol = "seguridad.crear-rol";
        public const string EditarRol = "seguridad.editar-rol";
        public const string EliminarRol = "seguridad.eliminar-rol";
        public const string ConsultarBitacora = "seguridad.consultar-bitacora";
        public const string VerCarteraCompleta = "seguridad.ver-cartera-completa";
    }

    /// <summary>Una fila del catálogo. RolesIniciales es la asignación al instalar el sistema (RolPermiso).</summary>
    public record Definicion(string Clave, string Modulo, string Nombre, string[] RolesIniciales);

    public static readonly IReadOnlyList<Definicion> Catalogo =
    [
        new(Pedidos.RegistrarPedido, Pedidos.Modulo, "Registrar pedidos", [Secretaria, Promotor]),
        new(Pedidos.ConsultarPedidos, Pedidos.Modulo, "Consultar pedidos", [Secretaria, Promotor]),
        new(Pedidos.ModificarPedido, Pedidos.Modulo, "Modificar pedidos", [Secretaria]),
        new(Pedidos.AnularPedido, Pedidos.Modulo, "Anular pedidos", [Secretaria]),
        new(Pedidos.CambiarEstado, Pedidos.Modulo, "Cambiar el estado de un pedido", [Secretaria]),
        new(Pedidos.ReprogramarEntrega, Pedidos.Modulo, "Reprogramar la entrega de un pedido", [Secretaria]),
        new(Pedidos.AsignarPedidos, Pedidos.Modulo, "Asignar pedidos para su entrega", [Secretaria]),
        new(Pedidos.VerBandeja, Pedidos.Modulo, "Ver la bandeja de entregas del día", [Promotor]),
        new(Pedidos.RegistrarEntrega, Pedidos.Modulo, "Registrar entregas", [Promotor]),
        new(Pedidos.GenerarReporte, Pedidos.Modulo, "Generar el reporte de pedidos", [GerenteGeneral]),

        new(Ventas.RegistrarHp, Ventas.Modulo, "Registrar hojas de pedido", [Promotor]),
        new(Ventas.ConfirmarHp, Ventas.Modulo, "Confirmar hojas de pedido", [Promotor]),
        new(Ventas.ConsultarHp, Ventas.Modulo, "Consultar hojas de pedido", [Secretaria, Promotor]),
        new(Ventas.AnularHp, Ventas.Modulo, "Anular hojas de pedido", [Secretaria]),
        new(Ventas.RegistrarCliente, Ventas.Modulo, "Registrar clientes", [Secretaria, Promotor]),
        new(Ventas.EditarCliente, Ventas.Modulo, "Editar clientes", [Secretaria, Promotor]),
        new(Ventas.AsignarEstadoCliente, Ventas.Modulo, "Asignar a mano el estado de un cliente", [Secretaria]),
        new(Ventas.ConsultarProductos, Ventas.Modulo, "Consultar productos", [Secretaria, Promotor]),
        new(Ventas.CapturarUbicacion, Ventas.Modulo, "Capturar la ubicación de un cliente", [Promotor]),
        new(Ventas.VerUbicacionCliente, Ventas.Modulo, "Ver la ubicación de los clientes", [GerenteGeneral, Promotor]),
        new(Ventas.RegistrarIngreso, Ventas.Modulo, "Registrar ingresos de mercadería", [Secretaria]),
        new(Ventas.ConsultarKardex, Ventas.Modulo, "Consultar el kardex", [Secretaria]),

        new(Cobranzas.RegistrarPago, Cobranzas.Modulo, "Registrar pagos", [Secretaria, Promotor]),
        new(Cobranzas.ConsultarPagos, Cobranzas.Modulo, "Consultar pagos", [Secretaria, Promotor]),
        new(Cobranzas.AnularPago, Cobranzas.Modulo, "Anular pagos", [Secretaria]),
        new(Cobranzas.ConsultarDeudas, Cobranzas.Modulo, "Consultar deudas", [GerenteGeneral, Secretaria, Promotor]),
        new(Cobranzas.MarcarIncobrable, Cobranzas.Modulo, "Marcar deudas como incobrables", [GerenteGeneral]),
        new(Cobranzas.GenerarReporte, Cobranzas.Modulo, "Generar el reporte de cobranzas", [GerenteGeneral]),

        new(Eventos.RegistrarEvento, Eventos.Modulo, "Registrar eventos", [Promotor]),
        new(Eventos.ConsultarEventos, Eventos.Modulo, "Consultar eventos", [Secretaria, Promotor]),
        new(Eventos.ModificarEvento, Eventos.Modulo, "Modificar eventos", [Secretaria, Promotor]),
        new(Eventos.ResolverEvento, Eventos.Modulo, "Resolver eventos", [Secretaria]),
        new(Eventos.AgregarFoto, Eventos.Modulo, "Agregar fotos", [Secretaria, Promotor]),
        new(Eventos.EliminarFoto, Eventos.Modulo, "Eliminar fotos", [Secretaria, Promotor]),
        new(Eventos.VerMapa, Eventos.Modulo, "Ver los eventos en el mapa", [GerenteGeneral]),
        new(Eventos.GenerarReporte, Eventos.Modulo, "Generar el reporte de incidencias", [GerenteGeneral]),

        new(Territorial.VerMapaClientes, Territorial.Modulo, "Ver el mapa de clientes con su estado", [GerenteGeneral, Promotor]),
        new(Territorial.VerClientesPorVisitar, Territorial.Modulo, "Ver los clientes por visitar", [GerenteGeneral, Promotor]),
        new(Territorial.PlanificarRuta, Territorial.Modulo, "Planificar rutas", [GerenteGeneral]),
        new(Territorial.SeguirRuta, Territorial.Modulo, "Seguir rutas", [GerenteGeneral]),
        new(Territorial.RecorrerRuta, Territorial.Modulo, "Recorrer su ruta", [Promotor]),
        new(Territorial.DibujarLimites, Territorial.Modulo, "Dibujar los límites de las zonas", [GerenteGeneral]),
        new(Territorial.RegistrarPuntoReferencia, Territorial.Modulo, "Registrar, modificar y eliminar puntos de referencia", [GerenteGeneral]),

        new(Reportes.VentasPorPromotor, Reportes.Modulo, "Generar el reporte de ventas por promotor", [GerenteGeneral]),
        new(Reportes.VentasPorZona, Reportes.Modulo, "Generar el reporte de ventas por zona", [GerenteGeneral]),
        new(Reportes.ClientesNuevosRecurrentes, Reportes.Modulo, "Generar el reporte de clientes nuevos y recurrentes", [GerenteGeneral]),
        new(Reportes.Inventario, Reportes.Modulo, "Generar el reporte de inventario", [GerenteGeneral, Secretaria]),
        new(Reportes.ImprimirHp, Reportes.Modulo, "Imprimir hojas de pedido", [Secretaria]),

        new(Parametros.ModificarDiasRevisita, Parametros.Modulo, "Modificar los días de re-visita", [Administrador]),
        new(Parametros.RegistrarZona, Parametros.Modulo, "Registrar zonas de venta", [GerenteGeneral]),
        new(Parametros.ModificarZona, Parametros.Modulo, "Modificar zonas de venta", [GerenteGeneral]),
        new(Parametros.EliminarZona, Parametros.Modulo, "Eliminar zonas de venta", [GerenteGeneral]),
        new(Parametros.RegistrarProducto, Parametros.Modulo, "Registrar productos", [Administrador]),
        new(Parametros.ModificarProducto, Parametros.Modulo, "Modificar productos", [Administrador]),
        new(Parametros.EliminarProducto, Parametros.Modulo, "Eliminar productos", [Administrador]),
        new(Parametros.RegistrarEstadoCliente, Parametros.Modulo, "Registrar estados del cliente", [Administrador]),
        new(Parametros.ModificarEstadoCliente, Parametros.Modulo, "Modificar estados del cliente", [Administrador]),
        new(Parametros.EliminarEstadoCliente, Parametros.Modulo, "Eliminar estados del cliente", [Administrador]),

        new(Seguridad.CrearUsuario, Seguridad.Modulo, "Crear usuarios", [Administrador]),
        new(Seguridad.EditarUsuario, Seguridad.Modulo, "Editar usuarios", [Administrador]),
        new(Seguridad.ActivarUsuario, Seguridad.Modulo, "Activar y desactivar usuarios", [Administrador]),
        new(Seguridad.CrearRol, Seguridad.Modulo, "Crear roles", [Administrador]),
        new(Seguridad.EditarRol, Seguridad.Modulo, "Editar roles", [Administrador]),
        new(Seguridad.EliminarRol, Seguridad.Modulo, "Eliminar roles", [Administrador]),
        new(Seguridad.ConsultarBitacora, Seguridad.Modulo, "Consultar la bitácora", [Administrador]),
        new(Seguridad.VerCarteraCompleta, Seguridad.Modulo, "Ver la cartera de todos los promotores", [GerenteGeneral, Secretaria]),
    ];

    /// <summary>
    /// Lo que el rol administrador conserva siempre: sin estos permisos nadie podría volver a administrar usuarios ni roles.
    /// </summary>
    public static readonly string[] ProtegidosDelAdministrador =
    [
        Seguridad.CrearUsuario, Seguridad.EditarUsuario, Seguridad.ActivarUsuario,
        Seguridad.CrearRol, Seguridad.EditarRol, Seguridad.EliminarRol,
    ];
}
