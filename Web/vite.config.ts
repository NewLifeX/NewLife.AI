import { defineConfig, type Plugin } from 'vitest/config'
import react from '@vitejs/plugin-react-swc'
import tailwindcss from '@tailwindcss/vite'
import legacy from '@vitejs/plugin-legacy'
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

// 删除 KaTeX 旧格式字体（仅 ttf），保留 woff 兼容 iOS 11
function removeKatexLegacyFonts(): Plugin {
  return {
    name: 'remove-katex-legacy-fonts',
    closeBundle() {
      const assetsDir = path.join(path.resolve(__dirname, '../NewLife.ChatAI/wwwroot'), 'assets')
      if (!fs.existsSync(assetsDir)) return
      const removed = fs.readdirSync(assetsDir)
        .filter(f => f.startsWith('KaTeX_') && f.endsWith('.ttf'))
      removed.forEach(f => fs.unlinkSync(path.join(assetsDir, f)))
      if (removed.length > 0) console.log(`[remove-katex-legacy-fonts] 已删除 ${removed.length} 个旧格式字体文件`)
    },
  }
}

/**
 * Tailwind v4 将 theme/base/utilities 包在 @layer 中。
 * 不支持 cascade layers 的旧内核（Chrome < 99 等）会丢弃整个 @layer 块，
 * 表现为「CSS 未生效、布局全乱」。构建后展开 @layer，保留规则内容。
 */
function unwrapAtLayerBlocks(css: string): string {
  let out = ''
  let i = 0
  while (i < css.length) {
    if (css.startsWith('@layer', i)) {
      const afterKeyword = i + 6
      let j = afterKeyword
      while (j < css.length && /\s/.test(css[j])) j++
      // @layer theme, base;  （仅声明，无块）
      if (css[j] === ';') {
        i = j + 1
        continue
      }
      // @layer name { ... } 或 @layer a, b { ... }
      while (j < css.length && css[j] !== '{' && css[j] !== ';') j++
      if (css[j] === ';') {
        i = j + 1
        continue
      }
      if (css[j] !== '{') {
        out += css[i]
        i++
        continue
      }
      j++ // skip '{'
      let depth = 1
      const start = j
      while (j < css.length && depth > 0) {
        const ch = css[j]
        if (ch === '{') depth++
        else if (ch === '}') depth--
        j++
      }
      out += css.slice(start, j - 1)
      i = j
      continue
    }
    out += css[i]
    i++
  }
  return out
}

function unwrapCssLayers(): Plugin {
  return {
    name: 'unwrap-css-layers',
    enforce: 'post',
    transform(code, id) {
      // 开发态 / 构建中间产物：在 Tailwind 之后展开 @layer
      if (!id.includes('.css') || !code.includes('@layer')) return null
      return { code: unwrapAtLayerBlocks(code), map: null }
    },
    closeBundle() {
      const outDir = path.resolve(__dirname, '../NewLife.ChatAI/wwwroot')
      const assetsDir = path.join(outDir, 'assets')
      if (!fs.existsSync(assetsDir)) return
      let files = 0
      let layersRemoved = 0
      for (const name of fs.readdirSync(assetsDir)) {
        if (!name.endsWith('.css')) continue
        const file = path.join(assetsDir, name)
        const before = fs.readFileSync(file, 'utf8')
        const countBefore = (before.match(/@layer\b/g) || []).length
        if (countBefore === 0) continue
        const after = unwrapAtLayerBlocks(before)
        const countAfter = (after.match(/@layer\b/g) || []).length
        fs.writeFileSync(file, after)
        files++
        layersRemoved += countBefore - countAfter
      }
      if (files > 0) {
        console.log(`[unwrap-css-layers] 已处理 ${files} 个 CSS，移除约 ${layersRemoved} 处 @layer`)
      }
    },
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss(), ...(process.env.LEGACY ? [legacy({ targets: ['iOS >= 11', 'Android >= 5'] })] : []), renameHtml('index.html', 'chat.html'), unwrapCssLayers(), removeKatexLegacyFonts()],
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
    /** 与 browserslist 对齐：降级 oklch 等现代 CSS，避免旧浏览器丢色；@layer 另由 unwrapCssLayers 展开 */
    cssTarget: ['chrome87', 'safari13'],
    minify: 'esbuild',
    reportCompressedSize: false,
    /** 小于此大小的资源内联为 base64，减少 HTTP 请求 */
    assetsInlineLimit: 4096,
    modulePreload: {
      /** 现代浏览器已原生支持 modulepreload，无需 polyfill */
      polyfill: false,
    },
    rollupOptions: {
      output: {
        manualChunks(id: string) {
          if (!id.includes('node_modules')) return
          if (id.includes('/react/') || id.includes('/react-dom/') || id.includes('/react-router/')) return 'vendor-react'
          if (id.includes('/react-markdown/') || id.includes('/remark-gfm/')) return 'vendor-markdown'
          if (id.includes('/zustand/') || id.includes('/i18next/') || id.includes('/react-i18next/')) return 'vendor-state'
          if (id.includes('/mermaid/')) return 'vendor-mermaid'
          if (id.includes('/echarts/')) return 'vendor-chart'
          // shiki 语言/主题已通过动态 import() 按需加载，不做手动分包
          if (id.includes('/katex/') || id.includes('/rehype-katex/') || id.includes('/remark-math/')) return 'vendor-math'
          if (id.includes('/html2canvas/') || id.includes('/html-to-image/')) return 'vendor-utils'
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
