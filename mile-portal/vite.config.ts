import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In dev, the SPA runs on :5173 and forwards API calls to the NGINX gateway (docker compose, port 80),
// so the browser sees a single origin exactly like in the containerised setup.
const gateway = process.env.VITE_GATEWAY_URL ?? 'http://localhost:80';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': { target: gateway, changeOrigin: true },
    },
  },
});
