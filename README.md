# Control Gestión — Sistema de Gestión de Promotores de Campo

Sistema full-stack para la gestión integral de promotores de campo: clientes, hojas de pedido, inventario, cobranzas, rutas, pedidos y reportes.

---

## Stack Tecnológico

| Capa | Tecnología |
|---|---|
| Backend | .NET 9 / ASP.NET Core Web API |
| Frontend | React 19 + Vite + Tailwind CSS v4 |
| Base de datos | MySQL 8 (Docker) |
| ORM | Entity Framework Core 9 + Pomelo |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Mapas | React-Leaflet + OpenStreetMap |
| Gráficos | Recharts |
| Exportación | ClosedXML (Excel .xlsx) |

---

## Requisitos previos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

## Puesta en marcha

### 1. Base de datos (Docker)

```bash
docker run -d \
  --name control-db \
  -e MYSQL_ROOT_PASSWORD=sistemas \
  -e MYSQL_DATABASE=control \
  -p 3306:3306 \
  mysql:8.0
```

### 2. Backend

```bash
cd ControlGestion.API
dotnet run
# API disponible en http://localhost:5124
```

Las migraciones se aplican automáticamente al iniciar. Se crea el usuario administrador por defecto:

- **Email:** `admin@control.com`
- **Contraseña:** `Admin123!`

### 3. Frontend

```bash
cd control-gestion-ui
npm install
npm run dev
# UI disponible en http://localhost:5173
```

---

## Estructura del proyecto

```
/
├── ControlGestion.API/          # Backend .NET 9
│   ├── Controllers/             # 12 controllers REST
│   ├── Models/                  # Entidades EF Core
│   ├── DTOs/                    # Data Transfer Objects
│   ├── Data/                    # DbContext + Migrations
│   ├── Services/                # InventarioService, MoraDetectionService
│   ├── Middleware/              # AuditoriaMiddleware
│   └── appsettings.json         # Configuración (ver nota de seguridad)
│
├── control-gestion-ui/          # Frontend React 19
│   ├── src/
│   │   ├── pages/               # Dashboard, Clientes, HP, Inventario...
│   │   ├── components/          # Layout, Table, UI primitivos
│   │   ├── api/                 # axios con interceptor JWT
│   │   └── context/             # AuthContext
│   └── package.json
│
├── README.md                    # Este archivo
├── DOCUMENTACION.md             # Diagramas técnicos (Mermaid)
└── MANUAL_USUARIO.md            # Manual de uso para operadores
```

---

## Módulos del sistema

| Módulo | Descripción |
|---|---|
| **Dashboard** | KPIs generales + 4 gráficos de ventas |
| **Clientes** | CRUD de clientes con geolocalización y límite de crédito |
| **Hojas de Pedido** | Registro de ventas en campo con GPS, contado/crédito |
| **Inventario** | Stock con control de concurrencia y movimientos |
| **Pedidos** | Gestión de despacho con máquina de estados |
| **Eventos** | Incidencias y seguimiento en campo |
| **Cobranzas** | Registro de pagos, detección automática de mora |
| **Rutas** | Planificación de visitas con mapa interactivo |
| **Reportes** | Análisis de ventas, cobranzas, inventario, promotores + exportación Excel |
| **Configuraciones** | Parámetros del sistema (días de vencimiento, etc.) |
| **Usuarios** | Administración de cuentas y roles |

---

## Roles y permisos

| Rol | Acceso |
|---|---|
| **Admin** | Acceso total, incluyendo auditoría y configuraciones |
| **Supervisor** | Gestión operativa, sin configuraciones del sistema |
| **Promotor** | Solo puede ver sus propios datos de campo |

---

## API Endpoints principales

```
POST   /api/auth/login
POST   /api/auth/register

GET    /api/clientes
POST   /api/clientes
PUT    /api/clientes/{id}

GET    /api/hojas-pedido
POST   /api/hojas-pedido
POST   /api/hojas-pedido/{id}/confirmar
POST   /api/hojas-pedido/{id}/anular

GET    /api/inventario
POST   /api/inventario/{productoId}/ajuste

GET    /api/cobranzas
POST   /api/cobranzas/{id}/pagos
POST   /api/cobranzas/{id}/incobrable

GET    /api/rutas
POST   /api/rutas
POST   /api/rutas/{id}/iniciar
POST   /api/rutas/{id}/completar

GET    /api/reportes/dashboard
GET    /api/reportes/ventas
GET    /api/reportes/cobranzas
GET    /api/reportes/export/ventas
GET    /api/reportes/export/cobranzas
```

---

## Seguridad — configuración en producción

El archivo `appsettings.json` contiene la cadena de conexión y la clave JWT. **Nunca subir a repositorio público.** Para producción usar variables de entorno o un gestor de secretos:

```bash
# Variables de entorno recomendadas
ConnectionStrings__DefaultConnection="Server=...;Password=..."
Jwt__Key="clave-secreta-de-al-menos-32-caracteres"
```

---

## Servicios en segundo plano

**MoraDetectionService** — corre cada noche a las 02:00 UTC. Detecta cobranzas vencidas y actualiza su estado automáticamente.

---

## Documentación técnica

Ver [DOCUMENTACION.md](DOCUMENTACION.md) para diagramas de clases, ER, secuencia y flujos en formato Mermaid.

Ver [MANUAL_USUARIO.md](MANUAL_USUARIO.md) para el manual de uso del sistema.
# management_control
