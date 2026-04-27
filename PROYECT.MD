# Documentación Técnica: Sistema de Control Gestión (Admin Panel)

## 1. Descripción General
Plataforma de administración centralizada para gestionar la operación en campo de promotores. Cubre el ciclo completo: gestión de clientes, ventas geolocalizadas, pedidos, cobranzas, eventos con evidencias fotográficas, visualización territorial y reportes ejecutivos.

---

## 2. Stack Tecnológico

| Capa | Tecnología | Versión | Descripción |
| :--- | :--- | :--- | :--- |
| **Frontend** | React | 19+ | UI reactiva con Vite como bundler |
| **Estilos** | Tailwind CSS | v4 | Utility-first CSS con plugin nativo de Vite |
| **HTTP Client** | Axios | latest | Interceptores JWT automáticos |
| **Gráficos** | Recharts | latest | Gráficos de barras y torta para dashboard |
| **Mapas** | Leaflet.js | latest | Mapa interactivo open-source (Fase 5) |
| **Backend** | .NET | 9 | Web API con arquitectura en capas |
| **ORM** | EF Core | 9 | Code-First con Pomelo MySQL provider |
| **Base de Datos** | MySQL | 8.0 | Docker container (`control-db`) |
| **Seguridad** | JWT + Identity | — | Tokens stateless, RBAC con 3 roles |

---

## 3. Arquitectura del Software

```
┌─────────────────────────────────────────────────────┐
│                  React 19 (Vite)                     │
│  Context API (Auth) │ Axios (JWT interceptor)        │
│  Pages: Login, Dashboard, Clientes, HP, Inventario   │
│         Pedidos, Cobranzas, Eventos, Mapa, Reportes  │
└──────────────────────┬──────────────────────────────┘
                       │ HTTP/JSON
┌──────────────────────▼──────────────────────────────┐
│              .NET 9 Web API                          │
│  Middleware: JWT Auth │ Auditoría (POST/PUT/DELETE)  │
│  Controllers: Auth, Usuarios, Clientes, HojasPedido  │
│               Pedidos, Cobranzas, Eventos, Inventario│
│  Services: InventarioService │ (CobranzaService F4)  │
└──────────────────────┬──────────────────────────────┘
                       │ EF Core + Pomelo
┌──────────────────────▼──────────────────────────────┐
│              MySQL 8.0 (Docker)                      │
│  DB: control  │  User: root  │  Port: 3306           │
└─────────────────────────────────────────────────────┘
```

---

## 4. Roles y Permisos (RBAC)

| Acción | Admin | Supervisor | Promotor |
|--------|-------|-----------|---------|
| Gestionar usuarios | ✅ | ❌ | ❌ |
| Ver auditoría | ✅ | ❌ | ❌ |
| CRUD Zonas/Productos | ✅ | ❌ | ❌ |
| Crear/Editar Clientes | ✅ | ✅ | ❌ |
| Crear HP | ✅ | ✅ | ✅ |
| Confirmar HP | ✅ | ✅ | ✅ |
| Anular HP | ✅ | ✅ | ❌ |
| Gestionar Pedidos | ✅ | ✅ | ❌ |
| Registrar Cobros | ✅ | ✅ | ✅ |
| Ajustar Inventario | ✅ | ✅ | ❌ |

---

## 5. Módulos del Sistema

### Módulo 1 — Gestión de Pedidos *(Fase 3)*
Control de pedidos con máquina de estados configurable.
- Estados: `Pendiente → En Proceso → En Ruta → Entregado | Anulado`
- Historial de cambios de estado con geolocalización
- Vinculación con HP origen y cobranza destino

### Módulo 2 — Ventas Geolocalizadas (HP's) *(Fase 2 — Implementado)*
Registro de ventas en campo mediante Hojas de Pedido digitales.
- Captura lat/lng del dispositivo en el momento de la venta
- Validación de crédito disponible antes de confirmar
- Descuento automático de inventario al confirmar (transacción atómica)
- Actualización de UltimaVisita y ProximaVisita del cliente

### Módulo 3 — Gestión de Cobranzas *(Fase 4)*
Seguimiento de ventas a crédito y pagos.
- Creación automática al confirmar HP a crédito
- Registro de pagos parciales o totales
- Job nocturno que detecta vencimientos y actualiza estados

### Módulo 4 — Eventos y Evidencias *(Fase 3)*
Registro de incidencias en campo.
- Tipos configurables (Cliente Ausente, Entrega Fallida, etc.)
- Evidencias fotográficas con geolocalización EXIF
- Tabla unificada `Anexos` para todas las fotos del sistema

### Módulo 5 — Visualización Territorial y Re-visitas *(Fase 5)*
Mapa interactivo con Leaflet.js.
- Clientes georreferenciados con colores por estado
- Control de re-visita (ProximaVisita <= Hoy)
- Planificación de rutas diarias

### Módulo 6 — Reportes *(Fase 6)*
KPIs ejecutivos con filtros avanzados.
- Ventas por zona, promotor, período
- Estado de cobranzas y mora
- Exportación a Excel con ClosedXML

### Módulo 7 — Parámetros y Configuraciones *(Fase 1 — Implementado)*
Configuración maestra del sistema.
- Tabla `ConfiguracionesGenerales` clave-valor
- Claves típicas: `dias_revisita`, `iva_porcentaje`, `limite_credito_default`
- Zonas con supervisor asignado
- Catálogo de productos (SKU, precio, categoría)

### Módulo 8 — Usuario, Seguridad y Auditoría *(Fase 1 — Implementado)*
Control de acceso y trazabilidad.
- CRUD de usuarios con roles RBAC
- Middleware de auditoría en POST/PUT/DELETE
- JWT con expiración de 8 horas

---

## 6. Modelo de Base de Datos

### Tablas Fase 1 (existentes)
| Tabla | PK | Descripción |
|-------|----|-------------|
| `AspNetUsers` | Id (string) | Identity: usuarios del sistema |
| `AspNetRoles` | Id | Roles: Admin, Supervisor, Promotor |
| `Zonas` | Id (int) | Perímetros de trabajo con supervisor |
| `Productos` | Id (int) | Catálogo: SKU, precio, categoría |
| `AuditoriaLogs` | Id (int) | Bitácora de mutaciones |
| `ConfiguracionesGenerales` | Clave (string) | Parámetros clave-valor |

### Tablas Fase 2 (implementadas)
| Tabla | PK | FKs principales |
|-------|----|----------------|
| `EstadosCliente` | Id | — (catálogo con seed) |
| `Clientes` | Id | ZonaId, EstadoClienteId, PromotorId |
| `InventariosProducto` | Id | ProductoId (único) |
| `MovimientosInventario` | Id | ProductoId, UsuarioId |
| `HojasPedido` | Id | ClienteId, PromotorId, ZonaId |
| `DetallesHP` | Id | HojaPedidoId, ProductoId |

### Tablas Fase 3 (pendientes)
| Tabla | PK | FKs principales |
|-------|----|----------------|
| `EstadosPedido` | Id | — (configurable con transiciones) |
| `Pedidos` | Id | ClienteId, PromotorId, HojaPedidoId |
| `DetallesPedido` | Id | PedidoId, ProductoId |
| `HistorialEstadoPedido` | Id | PedidoId, EstadoPedidoId, UsuarioId |
| `TiposEvento` | Id | — (catálogo) |
| `Eventos` | Id | ClienteId, PedidoId, PromotorId |
| `Anexos` | Id | TablaOrigen + RegistroOrigenId (polimórfico) |

### Tablas Fase 4 (pendientes)
| Tabla | PK | FKs principales |
|-------|----|----------------|
| `Cobranzas` | Id | ClienteId, PedidoId, HojaPedidoId |
| `PagosCobranza` | Id | CobranzaId, PromotorId |

### Tablas Fase 5 (pendientes)
| Tabla | PK | FKs principales |
|-------|----|----------------|
| `Rutas` | Id | PromotorId, ZonaId |
| `RutasDetalle` | Id | RutaId, ClienteId |

---

## 7. Flujo de Trabajo Principal

### Flujo de Venta en Campo (HP)
1. Promotor selecciona cliente → sistema captura GPS automáticamente
2. Agrega productos → valida stock disponible
3. Elige tipo: **Contado** o **Crédito** (valida límite)
4. Confirma → transacción atómica:
   - `EstadoHP = Confirmada`
   - Descuenta `InventarioProducto` por cada detalle
   - Inserta `MovimientoInventario` (Salida)
   - Actualiza `UltimaVisita` y `ProximaVisita` del cliente
   - Si Crédito → crea `Cobranza` automáticamente *(Fase 4)*

### Flujo de Autenticación
1. React envía `POST /api/auth/login`
2. .NET valida credenciales con Identity
3. Genera JWT (8h) con claims: userId, email, roles
4. React guarda token en `localStorage`
5. Axios intercepta todas las peticiones agregando `Authorization: Bearer <token>`
6. Si 401 → limpia storage y redirige a `/login`

---

## 8. Consideraciones de Seguridad
- **CORS:** Solo acepta `http://localhost:5173` y `http://localhost:5174`
- **Passwords:** Hasheados con Identity (PBKDF2)
- **JWT:** Firmado con HMAC-SHA256, clave en `appsettings.json`
- **Geolocalización:** `decimal(10,8)` / `decimal(11,8)` — sin pérdida de precisión
- **Concurrencia Inventario:** `VersionConcurrencia` (rowversion) en `InventarioProducto`
- **Auditoría:** Middleware captura toda mutación con IP, usuario y módulo

---

## 9. Comandos de Desarrollo

```bash
# Base de datos Docker
docker run -d --name control-db \
  -e MYSQL_ROOT_PASSWORD=sistemas \
  -e MYSQL_DATABASE=control \
  -p 3306:3306 mysql:8.0

# Backend
cd ControlGestion.API
dotnet run                          # Puerto 5124

# Nueva migración
export PATH="$PATH:/Users/<user>/.dotnet/tools"
dotnet ef migrations add <Nombre> --output-dir Data/Migrations
dotnet ef database update

# Frontend
cd control-gestion-ui
npm run dev                         # Puerto 5173
```

---

## 10. Credenciales de acceso inicial
- **Email:** `admin@control.com`
- **Password:** `Admin123!`
- **Rol:** Admin
