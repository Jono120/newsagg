import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('node_modules/antd')) return 'antd'
          if (id.includes('node_modules/@mui') || id.includes('node_modules/@emotion')) return 'mui'
          if (id.includes('node_modules/react-bootstrap') || id.includes('node_modules/bootstrap')) return 'bootstrap'
          if (id.includes('node_modules/react') || id.includes('node_modules/react-dom')) return 'react'
        },
      },
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      }
    }
  }
})
