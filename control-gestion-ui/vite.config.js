import path from 'node:path'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig(({ mode }) => {
  // Sin la URL de la API, el panel no arranca ni compila: así nunca se publica uno que apunte a localhost.
  if (!loadEnv(mode, import.meta.dirname, 'VITE_').VITE_API_URL) {
    throw new Error(
      'Falta VITE_API_URL, la raíz de la API sin /api. En desarrollo, crea control-gestion-ui/.env.local con la línea ' +
        'VITE_API_URL=http://localhost:5124 (ver .env.example).',
    )
  }

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      // "@/..." apunta a src/: lo usan los componentes de shadcn/ui.
      alias: { '@': path.resolve(import.meta.dirname, './src') },
    },
  }
})
