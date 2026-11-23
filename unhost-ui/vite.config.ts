import { defineConfig } from 'vite'
import plugin from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [plugin(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  build: {
    outDir: '../Unhost.API/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 54902,
    proxy: {
      '/api': {
        target: process.env.UNHOST_API_URL || 'http://localhost:5189',
        secure: false,
      },
    },
  },
})
