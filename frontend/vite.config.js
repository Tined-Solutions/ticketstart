import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { execSync } from 'node:child_process'

// Detecta automaticamente la URL del backend segun el entorno:
// - Windows puro (Edgardo): localhost alcanza porque Kestrel bindea a 0.0.0.0:5193
// - WSL2 (Martin): localhost no forwardea al host Windows; hay que usar la
//   gateway default de WSL, que apunta al host Windows (ej: 172.x.x.1).
//   Nota: en WSL 2.0+ con networkingMode=mirrored, localhost ya funciona y la
//   gateway puede no ser el host Windows. En ese caso, setear
//   VITE_API_TARGET=http://localhost:5193 en frontend/.env para anular la deteccion.
// El override explicito via VITE_API_TARGET en .env siempre gana.
function detectBackendUrl() {
  if (process.platform === 'win32') return 'http://localhost:5193'
  try {
    const version = execSync('cat /proc/version', { encoding: 'utf8' })
    const isWsl = /microsoft|WSL/i.test(version)
    if (isWsl) {
      const gateway = execSync("ip route show default | awk '/default/ {print $3}'", {
        encoding: 'utf8',
      }).trim()
      if (gateway) return `http://${gateway}:5193`
      console.warn(
        '[vite.config] WSL detectado pero no se encontro la gateway default. ' +
          'Usando localhost como fallback. Si el proxy falla, setea ' +
          'VITE_API_TARGET=http://<windows-host-ip>:5193 en frontend/.env.'
      )
    }
  } catch (e) {
    console.warn(
      '[vite.config] No se pudo detectar el entorno automaticamente: ' +
        (e && e.message ? e.message : String(e)) +
        '. Usando localhost:5193 como fallback. ' +
        'En WSL2, setea VITE_API_TARGET=http://<windows-host-ip>:5193 si falla.'
    )
  }
  return 'http://localhost:5193'
}

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')

  return {
    plugins: [react(), tailwindcss()],
    server: {
      proxy: {
        '/api': {
          target: env.VITE_API_TARGET || detectBackendUrl(),
          changeOrigin: true,
          secure: false,
        },
      },
    },
    test: {
      globals: true,
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.js'],
      maxWorkers: 2,
      forbidOnly: true,
    },
  }
})
