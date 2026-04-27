# Documentación Técnica — Control Gestión

**Estado:** Fases 1–6 implementadas y en producción

---

## 1. Diagrama de Clases completo

```mermaid
classDiagram
    direction TB

    class ApplicationUser {
        +string Id
        +string NombreCompleto
        +string Email
        +bool Activo
        +DateTime FechaCreacion
    }

    class Zona {
        +int Id
        +string Nombre
        +string Codigo
        +string IdSupervisor FK
    }

    class EstadoCliente {
        +int Id
        +string Nombre
        +string Color
        +bool Activo
    }

    class Cliente {
        +int Id
        +string Codigo
        +string NombreCompleto
        +string RazonSocial
        +string NIT
        +int ZonaId FK
        +int EstadoClienteId FK
        +string PromotorId FK
        +int DiasRevisita
        +bool TieneCredito
        +decimal LimiteCredito
        +decimal SaldoPendiente
    }

    class Producto {
        +int Id
        +string SKU
        +string Descripcion
        +decimal Precio
        +string Categoria
        +bool Activo
    }

    class InventarioProducto {
        +int Id
        +int ProductoId FK
        +decimal StockActual
        +decimal StockMinimo
        +decimal StockMaximo
        +DateTime VersionConcurrencia
    }

    class MovimientoInventario {
        +int Id
        +int ProductoId FK
        +TipoMovimiento TipoMovimiento
        +decimal Cantidad
        +decimal StockResultante
        +string UsuarioId FK
        +DateTime Fecha
    }

    class HojaPedido {
        +int Id
        +string Numero
        +int ClienteId FK
        +string PromotorId FK
        +int ZonaId FK
        +TipoVenta TipoVenta
        +decimal SubTotal
        +decimal Descuento
        +decimal Total
        +EstadoHP EstadoHP
    }

    class DetalleHP {
        +int Id
        +int HojaPedidoId FK
        +int ProductoId FK
        +decimal Cantidad
        +decimal PrecioUnitario
        +decimal Descuento
        +decimal Subtotal
    }

    class EstadoPedido {
        +int Id
        +string Nombre
        +int Orden
        +bool EsEstadoFinal
        +string PermiteTransicion
        +string Color
    }

    class Pedido {
        +int Id
        +string Numero
        +int ClienteId FK
        +string PromotorId FK
        +int HojaPedidoId FK
        +int EstadoPedidoId FK
        +DateTime FechaEntregaEstimada
        +decimal Total
    }

    class TipoEvento {
        +int Id
        +string Nombre
        +bool RequiereEvidencia
    }

    class Evento {
        +int Id
        +string Numero
        +int TipoEventoId FK
        +int ClienteId FK
        +string PromotorId FK
        +EstadoEvento EstadoEvento
    }

    class Cobranza {
        +int Id
        +string Numero
        +int ClienteId FK
        +int HojaPedidoId FK
        +string PromotorId FK
        +decimal MontoTotal
        +decimal MontoPagado
        +DateTime FechaVencimiento
        +EstadoCobranza Estado
    }

    class PagoCobranza {
        +int Id
        +int CobranzaId FK
        +string RegistradoPorId FK
        +decimal Monto
        +MetodoPago MetodoPago
        +string Referencia
        +DateTime FechaPago
    }

    class Ruta {
        +int Id
        +string PromotorId FK
        +int ZonaId FK
        +DateOnly FechaRuta
        +EstadoRuta Estado
    }

    class RutaDetalle {
        +int Id
        +int RutaId FK
        +int ClienteId FK
        +int Orden
        +EstadoVisita EstadoVisita
    }

    class AuditoriaLog {
        +int Id
        +string UsuarioId
        +string Accion
        +string Modulo
        +string DireccionIp
        +DateTime Fecha
    }

    ApplicationUser --> Zona : supervisa
    Zona --> Cliente : agrupa
    Cliente --> HojaPedido : genera
    HojaPedido --> DetalleHP : contiene
    Producto --> DetalleHP : aparece en
    Producto --> InventarioProducto : tiene stock
    HojaPedido --> Cobranza : origina
    Cobranza --> PagoCobranza : recibe pagos
    EstadoPedido --> Pedido : clasifica
    TipoEvento --> Evento : tipifica
    Ruta --> RutaDetalle : planifica
```

---

## 2. Diagrama Entidad-Relación

```mermaid
erDiagram
    AspNetUsers {
        string Id PK
        string NombreCompleto
        string Email
        bool Activo
    }
    Clientes {
        int Id PK
        string Codigo UK
        string NombreCompleto
        int ZonaId FK
        string PromotorId FK
        decimal LimiteCredito
        decimal SaldoPendiente
    }
    Productos {
        int Id PK
        string SKU UK
        string Descripcion
        decimal Precio
    }
    InventariosProducto {
        int Id PK
        int ProductoId FK
        decimal StockActual
        decimal StockMinimo
        datetime VersionConcurrencia
    }
    HojasPedido {
        int Id PK
        string Numero UK
        int ClienteId FK
        string PromotorId FK
        string TipoVenta
        decimal Total
        string EstadoHP
    }
    DetallesHP {
        int Id PK
        int HojaPedidoId FK
        int ProductoId FK
        decimal Cantidad
        decimal Subtotal
    }
    Cobranzas {
        int Id PK
        string Numero UK
        int ClienteId FK
        int HojaPedidoId FK
        decimal MontoTotal
        decimal MontoPagado
        datetime FechaVencimiento
        int Estado
    }
    PagosCobranza {
        int Id PK
        int CobranzaId FK
        string RegistradoPorId FK
        decimal Monto
        int MetodoPago
    }
    Pedidos {
        int Id PK
        string Numero UK
        int ClienteId FK
        int HojaPedidoId FK
        int EstadoPedidoId FK
    }
    Eventos {
        int Id PK
        string Numero UK
        int TipoEventoId FK
        int ClienteId FK
        string PromotorId FK
        int EstadoEvento
    }
    Rutas {
        int Id PK
        string PromotorId FK
        int ZonaId FK
        date FechaRuta
        int Estado
    }
    RutasDetalle {
        int Id PK
        int RutaId FK
        int ClienteId FK
        int Orden
        int EstadoVisita
    }

    AspNetUsers ||--o{ Clientes : atiende
    Clientes ||--o{ HojasPedido : genera
    HojasPedido ||--|{ DetallesHP : contiene
    Productos ||--o{ DetallesHP : incluido
    Productos ||--|| InventariosProducto : stock
    HojasPedido ||--o| Cobranzas : origina
    Cobranzas ||--o{ PagosCobranza : recibe
    HojasPedido ||--o| Pedidos : despacha
    Clientes ||--o{ Eventos : tiene
    Rutas ||--|{ RutasDetalle : planifica
    Clientes ||--o{ RutasDetalle : visita
```

---

## 3. Diagrama de Secuencia — Login

```mermaid
sequenceDiagram
    actor Usuario
    participant React
    participant AuthController
    participant Identity
    participant MySQL

    Usuario->>React: email + contraseña
    React->>AuthController: POST /api/auth/login
    AuthController->>Identity: FindByEmailAsync + CheckPasswordAsync
    Identity->>MySQL: SELECT AspNetUsers
    MySQL-->>Identity: user
    Identity-->>AuthController: OK
    AuthController->>AuthController: Genera JWT (HMAC-SHA256, 8h)
    AuthController-->>React: { token, roles, nombreCompleto }
    React->>React: localStorage + AuthContext
    React-->>Usuario: Redirige a /dashboard
```

---

## 4. Diagrama de Secuencia — Confirmar HP y crear Cobranza

```mermaid
sequenceDiagram
    actor Promotor
    participant React
    participant HPController
    participant InventarioService
    participant MySQL

    Promotor->>React: Click "Confirmar HP"
    React->>HPController: POST /api/hojas-pedido/{id}/confirmar
    HPController->>MySQL: BEGIN TRANSACTION
    HPController->>MySQL: Valida crédito del cliente
    loop Por cada producto
        HPController->>InventarioService: DescontarStock()
        InventarioService->>MySQL: UPDATE InventariosProducto
        InventarioService->>MySQL: INSERT MovimientosInventario
    end
    HPController->>MySQL: UPDATE HojasPedido SET Estado=Confirmada
    HPController->>MySQL: UPDATE Clientes SET UltimaVisita, ProximaVisita
    alt TipoVenta = Crédito AND Total > 0
        HPController->>MySQL: INSERT Cobranzas (Estado=Pendiente)
    end
    HPController->>MySQL: COMMIT
    HPController-->>React: { estado: Confirmada, total }
```

---

## 5. Diagrama de Secuencia — Registrar Pago de Cobranza

```mermaid
sequenceDiagram
    actor Promotor
    participant React
    participant CobranzasController
    participant MySQL

    Promotor->>React: Monto + método de pago
    React->>CobranzasController: POST /api/cobranzas/{id}/pagos
    CobranzasController->>MySQL: SELECT Cobranza + Pagos
    CobranzasController->>CobranzasController: Valida saldo = Max(0, Total - Pagado)
    CobranzasController->>MySQL: INSERT PagosCobranza
    CobranzasController->>MySQL: UPDATE Cobranza.MontoPagado += monto
    alt MontoPagado >= MontoTotal
        CobranzasController->>MySQL: Estado = Pagada, FechaPagoCompleto = NOW()
        CobranzasController->>MySQL: UPDATE Clientes.SaldoPendiente -= MontoTotal
    else
        CobranzasController->>MySQL: Estado = Parcial
    end
    CobranzasController-->>React: { estado, montoPagado, saldoPendiente }
```

---

## 6. Máquina de estados — HP

```mermaid
stateDiagram-v2
    [*] --> Borrador : POST /hojas-pedido
    Borrador --> Confirmada : POST /confirmar\n[valida crédito + descuenta stock]
    Borrador --> Anulada : POST /anular [Admin/Supervisor]
    Confirmada --> Anulada : POST /anular [Admin/Supervisor]
    Anulada --> [*]
    Confirmada --> [*]
```

---

## 7. Máquina de estados — Cobranza

```mermaid
stateDiagram-v2
    [*] --> Pendiente : Auto-creada al confirmar HP crédito
    Pendiente --> Parcial : POST /pagos (pago parcial)
    Parcial --> Parcial : POST /pagos (nuevo pago parcial)
    Parcial --> Pagada : POST /pagos (salda deuda)
    Pendiente --> Pagada : POST /pagos (pago total)
    Pendiente --> Vencida : Job nocturno (FechaVencimiento < NOW)
    Parcial --> Vencida : Job nocturno
    Vencida --> Pagada : POST /pagos
    Pendiente --> Incobrable : POST /incobrable [Admin/Supervisor]
    Parcial --> Incobrable : POST /incobrable [Admin/Supervisor]
    Vencida --> Incobrable : POST /incobrable [Admin/Supervisor]
    Pagada --> [*]
    Incobrable --> [*]
```

---

## 8. Flujo completo — Venta a crédito

```mermaid
flowchart TD
    A([Promotor en campo]) --> B[Crea HP + productos]
    B --> C{TipoVenta?}
    C -->|Contado| D[Confirma HP]
    C -->|Crédito| E{¿Crédito disponible?}
    E -->|No| F[❌ Error 400]
    E -->|Sí| D
    D --> G[Transacción atómica]
    G --> H[Descuenta inventario]
    G --> I[Actualiza visita cliente]
    G --> J{¿Era crédito?}
    J -->|Sí| K[Crea Cobranza automática]
    J -->|No| L([✅ Fin])
    K --> M{¿Se paga a tiempo?}
    M -->|Sí| N[Registra PagoCobranza]
    M -->|No| O[Job nocturno → Vencida]
    N --> P{¿Saldo = 0?}
    P -->|Sí| Q([✅ Cobranza cerrada])
    P -->|No| M
```

---

## 9. Arquitectura de componentes React

```mermaid
graph TD
    subgraph "Punto de entrada"
        Main["main.jsx"] --> App["App.jsx\n(BrowserRouter + Routes)"]
    end

    subgraph "Auth"
        App --> AuthProvider["AuthProvider\n(AuthContext.jsx)"]
        AuthProvider --> Axios["axios.js\n(interceptor JWT auto)"]
    end

    subgraph "Rutas protegidas"
        App --> PrivateRoute["PrivateRoute\n(RBAC)"]
        PrivateRoute --> Layout["Layout.jsx\n(sidebar + header)"]
    end

    subgraph "Páginas — 11 módulos"
        Layout --> Dashboard
        Layout --> Clientes
        Layout --> HojasPedido
        Layout --> Inventario
        Layout --> Pedidos
        Layout --> Eventos
        Layout --> Cobranzas
        Layout --> Rutas
        Layout --> Reportes
        Layout --> Configuraciones
        Layout --> Usuarios
    end

    subgraph "Componentes compartidos"
        Table["Table/Tr/Td"]
        UI["ui.jsx — Badge, Btn,\nInput, Card, Alert"]
    end
```

---

## 10. Decisiones técnicas relevantes

| Decisión | Motivo |
|---|---|
| `ReferenceHandler.IgnoreCycles` global | Relaciones bidireccionales entre entidades causaban ciclos infinitos en la serialización JSON |
| `timestamp(6)` + `ValueGeneratedOnAddOrUpdate` para `VersionConcurrencia` | `uint + IsRowVersion()` es patrón exclusivo de SQL Server; MySQL requiere timestamp para control de concurrencia optimista con Pomelo |
| `Math.Max(0, MontoTotal - MontoPagado)` en cálculo de saldo | Evita valores negativos en cobranzas con datos de prueba inconsistentes |
| Validación `Total > 0` en HP Create | Previene creación de cobranzas con monto negativo cuando el descuento supera el subtotal |
| Middleware de auditoría corre DESPUÉS de `_next` | Garantiza que `context.User` esté poblado con el JWT ya validado y que solo se registren operaciones que llegaron al pipeline |
| Filtro `hasta` = fin de día en reportes | Las fechas enviadas desde el frontend son `yyyy-MM-dd` (medianoche UTC); sin ajuste se excluyen todos los registros del día seleccionado |
