import { defineConfig, type Plugin } from 'vitest/config'
import react from '@vitejs/plugin-react-swc'
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

// 删除 KaTeX 旧格式字体（woff/ttf），现代浏览器只需 woff2
function removeKatexLegacyFonts(): Plugin {
  return {
    name: 'remove-katex-legacy-fonts',
    closeBundle() {
      const assetsDir = path.join(path.resolve(__dirname, '../NewLife.ChatAI/wwwroot'), 'assets')
      if (!fs.existsSync(assetsDir)) return
      const removed = fs.readdirSync(assetsDir)
        .filter(f => f.startsWith('KaTeX_') && (f.endsWith('.woff') || f.endsWith('.ttf')))
      removed.forEach(f => fs.unlinkSync(path.join(assetsDir, f)))
      if (removed.length > 0) console.log(`[remove-katex-legacy-fonts] 已删除 ${removed.length} 个旧格式字体文件`)
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss(), renameHtml('index.html', 'chat.html'), removeKatexLegacyFonts()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
    dedupe: ['react', 'react-dom'],
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    exclude: ['e2e/**', 'node_modules/**'],
  },
  build: {
    outDir: path.resolve(__dirname, '../NewLife.ChatAI/wwwroot'),
    emptyOutDir: true,
    sourcemap: false,
    target: 'esnext',
    reportCompressedSize: false,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) return
          if (id.includes('/react/') || id.includes('/react-dom/') || id.includes('/react-router/')) return 'vendor-react'
          if (id.includes('/@uiw/')) return 'vendor-react'
          if (id.includes('/react-markdown/') || id.includes('/remark-gfm/')) return 'vendor-markdown'
          if (id.includes('/zustand/') || id.includes('/i18next/') || id.includes('/react-i18next/')) return 'vendor-state'
          if (id.includes('/mermaid/')) return 'vendor-mermaid'
          if (id.includes('/echarts/')) return 'vendor-chart'
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5080',
      '/v1': 'http://localhost:5080',
      '/admin': 'http://localhost:5080',
      '/Admin': 'http://localhost:5080',
      '/Sso': 'http://localhost:5080',
      '/Content': 'http://localhost:5080',
    },
  },
})
