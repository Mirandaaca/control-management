using ControlGestion.API.Configuracion;
using ControlGestion.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ControlGestion.API.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Zona> Zonas { get; set; }
    public DbSet<Producto> Productos { get; set; }
    public DbSet<AuditoriaLog> AuditoriaLogs { get; set; }
    public DbSet<ConfiguracionGeneral> ConfiguracionesGenerales { get; set; }

    // Fase 2
    public DbSet<EstadoCliente> EstadosCliente { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<InventarioProducto> InventariosProducto { get; set; }
    public DbSet<MovimientoInventario> MovimientosInventario { get; set; }
    public DbSet<HojaPedido> HojasPedido { get; set; }
    public DbSet<DetalleHP> DetallesHP { get; set; }

    // Fase 3
    public DbSet<EstadoPedido> EstadosPedido { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<DetallePedido> DetallesPedido { get; set; }
    public DbSet<HistorialEstadoPedido> HistorialEstadosPedido { get; set; }
    public DbSet<TipoEvento> TiposEvento { get; set; }
    public DbSet<Evento> Eventos { get; set; }
    public DbSet<Evidencia> Evidencias { get; set; }

    // Fase 4
    public DbSet<Cobranza> Cobranzas { get; set; }
    public DbSet<PagoCobranza> PagosCobranza { get; set; }

    // Fase 5
    public DbSet<Ruta> Rutas { get; set; }
    public DbSet<RutaDetalle> RutasDetalle { get; set; }

    public DbSet<Secuencia> Secuencias { get; set; }

    public DbSet<Permiso> Permisos { get; set; }
    public DbSet<RolPermiso> RolesPermisos { get; set; }

    public DbSet<Sesion> Sesiones { get; set; }

    public DbSet<PuntoReferencia> PuntosReferencia { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Secuencia>().HasKey(s => s.Clave);

        // Sesiones (8.1). Cascade: si alguien borra un usuario directo en la base, sus sesiones no deben impedirlo.
        builder.Entity<Sesion>()
            .HasOne(s => s.Usuario).WithMany().HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Cascade);

        // Permisos (8.4)
        builder.Entity<Permiso>().HasIndex(p => p.Clave).IsUnique();
        builder.Entity<Permiso>().Property(p => p.Clave).HasMaxLength(80);
        builder.Entity<Permiso>().Property(p => p.Modulo).HasMaxLength(80);
        builder.Entity<Permiso>().Property(p => p.Nombre).HasMaxLength(150);
        builder.Entity<RolPermiso>().HasKey(rp => new { rp.RolId, rp.PermisoId });
        builder.Entity<RolPermiso>()
            .HasOne(rp => rp.Rol).WithMany().HasForeignKey(rp => rp.RolId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RolPermiso>()
            .HasOne(rp => rp.Permiso).WithMany().HasForeignKey(rp => rp.PermisoId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ConfiguracionGeneral>().HasKey(c => c.Clave);
        // Solo se insertan al migrar: si después alguien cambia el valor, la siembra no lo pisa.
        builder.Entity<ConfiguracionGeneral>().HasData(
            new ConfiguracionGeneral
            {
                Clave = ClavesDeConfiguracion.DiasRevisita,
                Valor = ClavesDeConfiguracion.DiasRevisitaPorDefecto.ToString(),
                Descripcion = "Días entre una visita al cliente y la siguiente (7.1)."
            },
            new ConfiguracionGeneral
            {
                Clave = ClavesDeConfiguracion.DiasVencimientoCredito,
                Valor = ClavesDeConfiguracion.DiasVencimientoCreditoPorDefecto.ToString(),
                Descripcion = "Días de plazo de una venta a crédito."
            });

        // Zona (7.2): el código y el nombre son únicos.
        builder.Entity<Zona>().Property(z => z.Codigo).HasMaxLength(20);
        builder.Entity<Zona>().HasIndex(z => z.Codigo).IsUnique();
        builder.Entity<Zona>().Property(z => z.Nombre).HasMaxLength(100);
        builder.Entity<Zona>().HasIndex(z => z.Nombre).IsUnique();
        builder.Entity<Zona>().Property(z => z.Limite).HasColumnType("json");

        // Puntos de referencia (5.5): son información de su zona y se eliminan con ella.
        builder.Entity<PuntoReferencia>()
            .HasOne(p => p.Zona).WithMany().HasForeignKey(p => p.ZonaId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PuntoReferencia>().Property(p => p.Nombre).HasMaxLength(100);
        builder.Entity<PuntoReferencia>().Property(p => p.Descripcion).HasMaxLength(300);
        builder.Entity<PuntoReferencia>().Property(p => p.Latitud).HasPrecision(10, 8);
        builder.Entity<PuntoReferencia>().Property(p => p.Longitud).HasPrecision(11, 8);
        // Una zona siempre tiene promotor. Los usuarios no se borran, se desactivan: Restrict no estorba.
        builder.Entity<Zona>()
            .HasOne(z => z.Promotor)
            .WithMany()
            .HasForeignKey(z => z.PromotorId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Cliente
        builder.Entity<Cliente>()
            .HasOne(c => c.Zona).WithMany().HasForeignKey(c => c.ZonaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Cliente>()
            .HasOne(c => c.EstadoCliente).WithMany().HasForeignKey(c => c.EstadoClienteId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Cliente>()
            .HasOne(c => c.Promotor).WithMany().HasForeignKey(c => c.PromotorId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Cliente>()
            .HasOne(c => c.CreadoPor).WithMany().HasForeignKey(c => c.CreadoPorId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Cliente>()
            .HasIndex(c => c.Codigo).IsUnique();
        builder.Entity<Cliente>()
            .HasIndex(c => c.ClaveIdempotencia).IsUnique();
        builder.Entity<Cliente>()
            .Property(c => c.Latitud).HasPrecision(10, 8);
        builder.Entity<Cliente>()
            .Property(c => c.Longitud).HasPrecision(11, 8);
        builder.Entity<Cliente>()
            .Property(c => c.SaldoPendiente).HasPrecision(18, 2);
        builder.Entity<Cliente>()
            .Property(c => c.VersionConcurrencia)
            .HasColumnType("timestamp(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Producto (7.3)
        builder.Entity<Producto>().Property(p => p.SKU).HasMaxLength(50);
        builder.Entity<Producto>().HasIndex(p => p.SKU).IsUnique();
        builder.Entity<Producto>().Property(p => p.Descripcion).HasMaxLength(150);
        builder.Entity<Producto>().Property(p => p.Categoria).HasMaxLength(80);
        builder.Entity<Producto>().Property(p => p.Precio).HasPrecision(18, 2);
        builder.Entity<Producto>().Property(p => p.PrecioCompra).HasPrecision(18, 2);

        // InventarioProducto: una fila por producto.
        builder.Entity<InventarioProducto>()
            .HasOne(i => i.Producto).WithOne(p => p.Inventario).HasForeignKey<InventarioProducto>(i => i.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<InventarioProducto>()
            .HasIndex(i => i.ProductoId).IsUnique();
        builder.Entity<InventarioProducto>()
            .Property(i => i.VersionConcurrencia)
            .HasColumnType("timestamp(6)")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
        builder.Entity<InventarioProducto>()
            .Property(i => i.StockActual).HasPrecision(10, 3);
        builder.Entity<InventarioProducto>()
            .Property(i => i.StockMinimo).HasPrecision(10, 3);
        builder.Entity<InventarioProducto>()
            .Property(i => i.StockMaximo).HasPrecision(10, 3);

        // MovimientoInventario
        builder.Entity<MovimientoInventario>()
            .HasOne(m => m.Producto).WithMany().HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MovimientoInventario>()
            .HasOne(m => m.Usuario).WithMany().HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MovimientoInventario>()
            .Property(m => m.Cantidad).HasPrecision(10, 3);
        builder.Entity<MovimientoInventario>()
            .Property(m => m.StockAnterior).HasPrecision(10, 3);
        builder.Entity<MovimientoInventario>()
            .Property(m => m.StockResultante).HasPrecision(10, 3);

        // HojaPedido
        builder.Entity<HojaPedido>()
            .HasOne(h => h.Cliente).WithMany().HasForeignKey(h => h.ClienteId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HojaPedido>()
            .HasOne(h => h.Promotor).WithMany().HasForeignKey(h => h.PromotorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HojaPedido>()
            .HasOne(h => h.Zona).WithMany().HasForeignKey(h => h.ZonaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HojaPedido>()
            .HasOne(h => h.AnuladoPor).WithMany().HasForeignKey(h => h.AnuladoPorId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<HojaPedido>()
            .HasIndex(h => h.ClaveIdempotencia).IsUnique();
        builder.Entity<HojaPedido>()
            .HasIndex(h => h.Numero).IsUnique();
        builder.Entity<HojaPedido>()
            .Property(h => h.Latitud).HasPrecision(10, 8);
        builder.Entity<HojaPedido>()
            .Property(h => h.Longitud).HasPrecision(11, 8);
        builder.Entity<HojaPedido>()
            .Property(h => h.SubTotal).HasPrecision(18, 2);
        builder.Entity<HojaPedido>()
            .Property(h => h.Descuento).HasPrecision(18, 2);
        builder.Entity<HojaPedido>()
            .Property(h => h.Total).HasPrecision(18, 2);
        builder.Entity<HojaPedido>()
            .Property(h => h.VersionConcurrencia)
            .HasColumnType("timestamp(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // DetalleHP
        builder.Entity<DetalleHP>()
            .HasOne(d => d.HojaPedido).WithMany(h => h.Detalles).HasForeignKey(d => d.HojaPedidoId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<DetalleHP>()
            .HasOne(d => d.Producto).WithMany().HasForeignKey(d => d.ProductoId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DetalleHP>()
            .Property(d => d.Cantidad).HasPrecision(10, 3);
        builder.Entity<DetalleHP>()
            .Property(d => d.PrecioUnitario).HasPrecision(18, 2);
        builder.Entity<DetalleHP>()
            .Property(d => d.Descuento).HasPrecision(18, 2);
        builder.Entity<DetalleHP>()
            .Property(d => d.Subtotal).HasPrecision(18, 2);

        // Pedido
        builder.Entity<Pedido>()
            .HasOne(p => p.Cliente).WithMany().HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Pedido>()
            .HasOne(p => p.Promotor).WithMany().HasForeignKey(p => p.PromotorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Pedido>()
            .HasOne(p => p.RegistradoPor).WithMany().HasForeignKey(p => p.RegistradoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Pedido>()
            .HasOne(p => p.Repartidor).WithMany().HasForeignKey(p => p.RepartidorId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Pedido>()
            .HasOne(p => p.Zona).WithMany().HasForeignKey(p => p.ZonaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Pedido>()
            .HasOne(p => p.HojaPedido).WithMany().HasForeignKey(p => p.HojaPedidoId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Pedido>()
            .HasOne(p => p.EstadoPedido).WithMany().HasForeignKey(p => p.EstadoPedidoId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Pedido>()
            .HasIndex(p => p.Numero).IsUnique();
        // Una HP es la venta de un solo pedido. MySQL admite varios NULL en un índice único: los pedidos sin entregar.
        builder.Entity<Pedido>()
            .HasIndex(p => p.HojaPedidoId).IsUnique();
        builder.Entity<Pedido>().Property(p => p.RecibidoPor).HasMaxLength(150);
        builder.Entity<Pedido>().Property(p => p.DireccionEntrega).HasMaxLength(300);
        builder.Entity<Pedido>().Property(p => p.SubTotal).HasPrecision(18, 2);
        builder.Entity<Pedido>().Property(p => p.Descuento).HasPrecision(18, 2);
        builder.Entity<Pedido>().Property(p => p.Total).HasPrecision(18, 2);
        builder.Entity<Pedido>().Property(p => p.LatitudEntrega).HasPrecision(10, 8);
        builder.Entity<Pedido>().Property(p => p.LongitudEntrega).HasPrecision(11, 8);
        builder.Entity<Pedido>()
            .Property(p => p.VersionConcurrencia)
            .HasColumnType("timestamp(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // DetallePedido
        builder.Entity<DetallePedido>()
            .HasOne(d => d.Pedido).WithMany(p => p.Detalles).HasForeignKey(d => d.PedidoId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<DetallePedido>()
            .HasOne(d => d.Producto).WithMany().HasForeignKey(d => d.ProductoId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DetallePedido>().Property(d => d.Cantidad).HasPrecision(10, 3);
        builder.Entity<DetallePedido>().Property(d => d.PrecioUnitario).HasPrecision(18, 2);
        builder.Entity<DetallePedido>().Property(d => d.Descuento).HasPrecision(18, 2);
        builder.Entity<DetallePedido>().Property(d => d.Subtotal).HasPrecision(18, 2);
        builder.Entity<DetallePedido>().Property(d => d.CantidadEntregada).HasPrecision(10, 3);

        // HistorialEstadoPedido
        builder.Entity<HistorialEstadoPedido>()
            .HasOne(h => h.Pedido).WithMany(p => p.Historial).HasForeignKey(h => h.PedidoId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<HistorialEstadoPedido>()
            .HasOne(h => h.EstadoPedido).WithMany().HasForeignKey(h => h.EstadoPedidoId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HistorialEstadoPedido>()
            .HasOne(h => h.Usuario).WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<HistorialEstadoPedido>().Property(h => h.LatitudCambio).HasPrecision(10, 8);
        builder.Entity<HistorialEstadoPedido>().Property(h => h.LongitudCambio).HasPrecision(11, 8);

        // Evento
        builder.Entity<Evento>()
            .HasOne(e => e.TipoEvento).WithMany().HasForeignKey(e => e.TipoEventoId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Evento>()
            .HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.ClienteId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Evento>()
            .HasOne(e => e.Pedido).WithMany().HasForeignKey(e => e.PedidoId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Evento>()
            .HasOne(e => e.Promotor).WithMany().HasForeignKey(e => e.PromotorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Evento>()
            .HasOne(e => e.ResueltoPor).WithMany().HasForeignKey(e => e.ResueltoPorId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Evento>()
            .HasIndex(e => e.Numero).IsUnique();
        builder.Entity<Evento>().Property(e => e.Latitud).HasPrecision(10, 8);
        builder.Entity<Evento>().Property(e => e.Longitud).HasPrecision(11, 8);
        builder.Entity<Evento>().Property(e => e.Descripcion).HasMaxLength(1000);
        builder.Entity<Evento>().Property(e => e.Solucion).HasMaxLength(1000);
        builder.Entity<Evento>()
            .Property(e => e.VersionConcurrencia)
            .HasColumnType("timestamp(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Evidencia
        builder.Entity<Evidencia>()
            .HasOne(e => e.SubidoPor).WithMany().HasForeignKey(e => e.SubidoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Evidencia>()
            .HasOne(e => e.Evento).WithMany(e => e.Evidencias).HasForeignKey(e => e.EventoId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Evidencia>().Property(e => e.NombreArchivo).HasMaxLength(255);
        builder.Entity<Evidencia>().Property(e => e.RutaAlmacenamiento).HasMaxLength(300);
        builder.Entity<Evidencia>().Property(e => e.TipoMIME).HasMaxLength(50);
        builder.Entity<Evidencia>().Property(e => e.Descripcion).HasMaxLength(500);
        builder.Entity<Evidencia>().Property(e => e.Latitud).HasPrecision(10, 8);
        builder.Entity<Evidencia>().Property(e => e.Longitud).HasPrecision(11, 8);

        // Cobranza
        builder.Entity<Cobranza>()
            .HasOne(c => c.Cliente).WithMany().HasForeignKey(c => c.ClienteId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Cobranza>()
            .HasOne(c => c.HojaPedido).WithMany(h => h.Cobranzas).HasForeignKey(c => c.HojaPedidoId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Cobranza>()
            .HasOne(c => c.Promotor).WithMany().HasForeignKey(c => c.PromotorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Cobranza>()
            .HasIndex(c => c.Numero).IsUnique();
        builder.Entity<Cobranza>().Property(c => c.MontoTotal).HasPrecision(18, 2);
        builder.Entity<Cobranza>().Property(c => c.MontoPagado).HasPrecision(18, 2);
        builder.Entity<Cobranza>().Ignore(c => c.SaldoPendiente);
        // Mismo token que InventarioProducto. HasDefaultValueSql es necesario porque la columna se agrega a una
        // tabla con filas: sin él, EF pondría '0001-01-01', una fecha que MySQL no acepta en un timestamp.
        builder.Entity<Cobranza>()
            .Property(c => c.VersionConcurrencia)
            .HasColumnType("timestamp(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // PagoCobranza
        builder.Entity<PagoCobranza>()
            .HasOne(p => p.Cobranza).WithMany(c => c.Pagos).HasForeignKey(p => p.CobranzaId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PagoCobranza>()
            .HasOne(p => p.RegistradoPor).WithMany().HasForeignKey(p => p.RegistradoPorId).OnDelete(DeleteBehavior.Restrict);
        // Un pago tiene un solo comprobante, y una evidencia es el comprobante de un solo pago: el índice es único.
        builder.Entity<PagoCobranza>()
            .HasOne(p => p.Comprobante).WithOne(e => e.Pago).HasForeignKey<PagoCobranza>(p => p.EvidenciaId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<PagoCobranza>().Property(p => p.Monto).HasPrecision(18, 2);
        builder.Entity<PagoCobranza>()
            .HasIndex(p => p.ClaveIdempotencia).IsUnique();

        // Ruta
        builder.Entity<Ruta>()
            .HasOne(r => r.Promotor).WithMany().HasForeignKey(r => r.PromotorId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Ruta>()
            .HasOne(r => r.Zona).WithMany().HasForeignKey(r => r.ZonaId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Ruta>()
            .HasIndex(r => r.Numero).IsUnique();

        // RutaDetalle
        builder.Entity<RutaDetalle>()
            .HasOne(d => d.Ruta).WithMany(r => r.Detalles).HasForeignKey(d => d.RutaId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<RutaDetalle>()
            .HasOne(d => d.Cliente).WithMany().HasForeignKey(d => d.ClienteId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RutaDetalle>().Property(d => d.Latitud).HasPrecision(10, 8);
        builder.Entity<RutaDetalle>().Property(d => d.Longitud).HasPrecision(11, 8);

        // Seed estados de pedido
        builder.Entity<EstadoPedido>().HasData(
            new EstadoPedido { Id = 1, Nombre = "Pendiente",     Orden = 1, Color = "#f59e0b" },
            new EstadoPedido { Id = 2, Nombre = "En Proceso",    Orden = 2, Color = "#3b82f6" },
            new EstadoPedido { Id = 3, Nombre = "En Ruta",       Orden = 3, Color = "#8b5cf6" },
            new EstadoPedido { Id = 4, Nombre = "Entregado",     Orden = 4, Color = "#10b981", EsEstadoFinal = true },
            new EstadoPedido { Id = 5, Nombre = "Anulado",       Orden = 5, Color = "#ef4444", EsEstadoFinal = true }
        );

        // Seed tipos de evento
        builder.Entity<TipoEvento>().HasData(
            new TipoEvento { Id = 1, Nombre = "Cliente Ausente",        RequiereEvidencia = true,  NivelPrioridad = NivelPrioridad.Medio },
            new TipoEvento { Id = 2, Nombre = "Entrega No Realizada",   RequiereEvidencia = true,  NivelPrioridad = NivelPrioridad.Alto  },
            new TipoEvento { Id = 3, Nombre = "Problema de Acceso",     RequiereEvidencia = false, NivelPrioridad = NivelPrioridad.Medio },
            new TipoEvento { Id = 4, Nombre = "Incidencia de Cobro",    RequiereEvidencia = false, NivelPrioridad = NivelPrioridad.Alto  },
            new TipoEvento { Id = 5, Nombre = "Otro",                   RequiereEvidencia = false, NivelPrioridad = NivelPrioridad.Bajo  }
        );

        // Estados del cliente (7.4). El nombre es único; el color, uno de PaletaDeEstados.
        builder.Entity<EstadoCliente>().Property(e => e.Nombre).HasMaxLength(50);
        builder.Entity<EstadoCliente>().HasIndex(e => e.Nombre).IsUnique();
        builder.Entity<EstadoCliente>().Property(e => e.Color).HasMaxLength(7);
        builder.Entity<EstadoCliente>().Property(e => e.Descripcion).HasMaxLength(200);
        builder.Entity<EstadoCliente>().HasData(
            new EstadoCliente
            {
                Id = EstadosDeCliente.Activo, Nombre = "Activo", Color = PaletaDeEstados.Activo, EsDelSistema = true,
                Descripcion = "Cliente activo y disponible"
            },
            new EstadoCliente
            {
                Id = EstadosDeCliente.DisponibleParaRevisita, Nombre = "Disponible para Re-visita", Color = PaletaDeEstados.Disponible,
                EsDelSistema = true, Descripcion = "Ha pasado el período de re-visita"
            },
            new EstadoCliente
            {
                Id = EstadosDeCliente.Inactivo, Nombre = "Inactivo", Color = PaletaDeEstados.Pizarra, Descripcion = "Cliente inactivo o dado de baja"
            }
        );
    }
}
