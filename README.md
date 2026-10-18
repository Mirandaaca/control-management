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
  -e MYSQL_ROOT_PASSWORD=<contraseña-root> \
  -e MYSQL_DATABASE=control \
  -p 3306:3306 \
  mysql:8.0
```

### 2. Backend

Antes del primer arranque, configura los secretos (ver "Secretos" más abajo). Luego:

```bash
cd ControlGestion.API
dotnet run
# API disponible en http://localhost:5124
```

Las migraciones se aplican automáticamente al iniciar. Si no existe, se crea el usuario administrador:

- **Email:** `admin@control.com`
- **Contraseña:** la que guardaste en `Seed:AdminPassword` (ver "Secretos")

En Development, si existe `Seed:DemoPassword`, también se crean `supervisor@control.com` (Supervisor) y `promotor@control.com` (Promotor) con esa contraseña. La siembra nunca cambia la contraseña de un usuario que ya existe.

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
POST   /api/auth/login        (único endpoint sin sesión)

GET    /api/usuarios          (solo Admin)
POST   /api/usuarios          (solo Admin: crea un usuario con rol Admin, Supervisor o Promotor)

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

## Secretos

`appsettings.json` no guarda secretos: solo `Jwt:Issuer` y `Jwt:Audience`. Los secretos se configuran aparte:

| Clave | Para qué | ¿Obligatoria? |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Cadena de conexión a MySQL | Sí. Sin ella la API no arranca. |
| `Jwt:Key` | Clave para firmar los tokens. Mínimo 32 bytes. | Sí. Sin ella, o si es corta, la API no arranca. |
| `Seed:AdminPassword` | Contraseña del admin al crearlo por primera vez | Si falta, el admin no se crea y se avisa en el log. |
| `Seed:DemoPassword` | Contraseña del Supervisor y el Promotor de prueba | Opcional. Solo se usa en Development. |

**En desarrollo** se guardan con `dotnet user-secrets`, fuera del repositorio. Desde `ControlGestion.API/`:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<host>;Port=<puerto>;Database=control;User=<usuario>;Password=<contraseña>"
dotnet user-secrets set "Jwt:Key" "<clave aleatoria de al menos 32 bytes>"
dotnet user-secrets set "Seed:AdminPassword" "<contraseña del admin>"
dotnet user-secrets set "Seed:DemoPassword" "<contraseña de los usuarios de prueba>"
```

Para generar una clave JWT aleatoria en PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

**En producción** se usan variables de entorno, con `__` en lugar de `:`: `ConnectionStrings__DefaultConnection`, `Jwt__Key` y `Seed__AdminPassword`. Una variable de entorno tiene prioridad sobre user-secrets y sobre `appsettings.json`.

Las contraseñas deben tener al menos 6 caracteres y un dígito.

---

## Servicios en segundo plano

**MoraDetectionService** — corre cada noche a las 02:00 UTC. Detecta cobranzas vencidas y actualiza su estado automáticamente.

---

## Documentación técnica

Ver [DOCUMENTACION.md](DOCUMENTACION.md) para diagramas de clases, ER, secuencia y flujos en formato Mermaid.

Ver [MANUAL_USUARIO.md](MANUAL_USUARIO.md) para el manual de uso del sistema.
# management_control
