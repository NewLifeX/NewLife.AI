import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          'vendor-react': ['react', 'react-dom', 'react-router-dom'],
          'vendor-markdown': ['react-markdown', 'remark-gfm', 'rehype-highlight', 'highlight.js'],
          'vendor-state': ['zustand', 'i18next', 'react-i18next'],
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5000',
      '/v1': 'http://localhost:5000',
    },
  },
})
