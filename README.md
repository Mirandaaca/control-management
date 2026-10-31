# Control de Gestión — Del Campo SRL

Sistema de ventas directas en campo para Del Campo SRL (Santa Cruz, Bolivia): pedidos, hojas de pedido, cobranzas,
eventos con fotos, rutas de re-visita y reportes, con la ubicación de cada operación. Es el sistema de un proyecto de
grado.

## Las tres partes

| Carpeta | Qué es | Con qué |
|---|---|---|
| `ControlGestion.API/` | La API REST, con sus pruebas de integración en `ControlGestion.API.Tests/` | .NET 10, ASP.NET Core, EF Core 10 y MySQL 8.4 |
| `control-gestion-ui/` | El panel web de la oficina: el gerente, la secretaria y el administrador | React 19, Vite y Tailwind CSS |
| `control-gestion-app/gestion_control/` | La app de los promotores, para Android | Flutter |

Los roles (administrador del sistema, gerente general, secretaria y promotor) son datos: cada uno tiene los permisos
que le asigna el administrador.

## Cómo levantarlo en local

Hace falta el SDK de .NET 10, Node.js 24, Flutter 3.44 (solo para la app) y un MySQL 8.4.

### La API

Los secretos no van en el repositorio: en desarrollo se guardan con user-secrets. Desde `ControlGestion.API/`:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<host>;Port=<puerto>;Database=control;User=<usuario>;Password=<contraseña>"
dotnet user-secrets set "Jwt:Key" "<una clave al azar de al menos 32 bytes>"
dotnet user-secrets set "Seed:AdminPassword" "<la contraseña del administrador>"
dotnet run
```

- Queda en http://localhost:5124. Al arrancar, aplica las migraciones: basta con una base vacía.
- Crea el administrador `admin@control.com`, con la contraseña de `Seed:AdminPassword`. Con `Seed:DemoPassword`, crea
  además `gerente@control.com`, `secretaria@control.com` y `promotor@control.com`.
- `Navegacion:Clave` es opcional: la clave de OpenRouteService para el camino por calles de la app. Sin ella, todo
  funciona menos ese camino.

### El panel

```bash
cd control-gestion-ui
cp .env.example .env.local   # VITE_API_URL: la raíz de la API, sin /api
npm install
npm run dev                  # http://localhost:5173
```

### La app

Cómo correrla en un celular Android o como web, con la URL de la API, está en
[`control-gestion-app/gestion_control/README.md`](control-gestion-app/gestion_control/README.md).

## Las pruebas

- **API:** `dotnet test ControlGestion.sln`. Cada corrida crea una base `control_test_*` y la borra al terminar. Necesita
  un MySQL 8.4 con `lower_case_table_names=1` y un usuario que pueda crear y borrar bases, en
  `ConnectionStrings:Pruebas` (user-secrets de `ControlGestion.API.Tests`).
- **Panel:** `npm run lint` y `npm run build`.
- **App:** `flutter analyze` y `flutter test`.

GitHub Actions las corre en cada pull request.

## Los documentos anteriores

`historico/` guarda los documentos del desarrollador anterior. No describen el sistema actual.
