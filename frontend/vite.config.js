import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Сборка (npm run build) кладётся прямо в wwwroot backend-проекта:
// backend отдаёт фронтенд как статические файлы, в итоге получается одно запускаемое приложение.
// В режиме разработки (npm run dev) запросы /api проксируются на backend.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../backend/TFlexDocsControl.Api/wwwroot',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1500,
    rollupOptions: {
      output: {
        manualChunks: {
          react: ['react', 'react-dom', 'react-router-dom'],
          antd: ['antd', '@ant-design/icons'],
        },
      },
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY || 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
})
