import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'
import fs from 'fs'

function renameHtml(from: string, to: string): Plugin {
  return {
    name: 'rename-html',
    closeBundle() {
      const outDir = path.resolve(__dirname, '../NewLife.ChatAI/wwwroot')
      const src = path.join(outDir, from)
      const dst = path.join(outDir, to)
      if (fs.existsSync(src)) fs.renameSync(src, dst)
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss(), renameHtml('index.html', 'chat.html')],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    outDir: path.resolve(__dirname, '../NewLife.ChatAI/wwwroot'),
    emptyOutDir: true,
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
      '/api': 'http://localhost:5080',
      '/v1': 'http://localhost:5080',
      '/admin': 'http://localhost:5080',
      '/Sso': 'http://localhost:5080',
    },
  },
})
