# Control de Gestión: el panel de la oficina

El panel web de Del Campo SRL, para el gerente, la secretaria y el administrador del sistema. Está hecho con React 19,
Vite y Tailwind CSS.

## Cómo correrlo

1. La API tiene que estar corriendo: ver el [README de la raíz](../README.md).
2. Copia `.env.example` como `.env.local`. `VITE_API_URL` es la raíz de la API, sin `/api`: sin ella, Vite no arranca.
3. `npm install` y `npm run dev`. El panel queda en http://localhost:5173.

## Antes de subir un cambio

`npm run lint` y `npm run build` tienen que terminar sin errores.
