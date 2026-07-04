import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import mermaid from 'mermaid'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { resolveRenderableMermaidCode, extractMermaidCode } from '@/components/chat/mermaidHelper'
import { savePngBlob, copyImageOrFallback } from '@/utils/imageCapture'
import { MobileImageFallback } from '@/components/atoms/MobileImageFallback'

mermaid.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'loose' })

let mermaidWidgetCounter = 0

/** ShowWidget 工具结果的解析数据 */
export interface WidgetData {
  widgetId: string
  kind: 'svg' | 'html'
  title?: string
  code: string
  loadingMessage?: string
  initialHeight?: number
  /** 卡片背景模式。transparent 时复制按钮输出带 Alpha 通道的 PNG，可直接叠加到 PPT 模板底图 */
  background?: 'solid' | 'transparent'
  /** PPT 幻灯片模式。true 时容器锁定 16:9 宽高比，适合直接粘贴到演示文稿 */
  slideMode?: boolean
}

interface WidgetBlockProps {
  data: WidgetData
  className?: string
}

/**
 * 解析 SVG 字符串，返回自然像素尺寸（优先 width/height 属性，其次 viewBox）。
 */
function getSvgDimensions(svgHtml: string): { w: number; h: number } {
  const parser = new DOMParser()
  const doc = parser.parseFromString(svgHtml, 'image/svg+xml')
  const svgEl = doc.querySelector('svg')
  if (!svgEl) return { w: 800, h: 600 }
  let w = 0, h = 0
  const wAttr = svgEl.getAttribute('width')
  const hAttr = svgEl.getAttribute('height')
  const vb   = svgEl.getAttribute('viewBox')
  if (wAttr && !/^\d*\.?\d+%$/.test(wAttr)) w = parseFloat(wAttr)
  if (hAttr && !/^\d*\.?\d+%$/.test(hAttr)) h = parseFloat(hAttr)
  if ((!w || !h) && vb) {
    const parts = vb.trim().split(/[\s,]+/)
    if (parts.length >= 4) {
      if (!w) w = parseFloat(parts[2])
      if (!h) h = parseFloat(parts[3])
    }
  }
  if (!w || w <= 0) w = 800
  if (!h || h <= 0) h = 600
  return { w, h }
}

/**
 * 从 <foreignObject> 内容提取文本行（<br>/<p>/<div> 均视为换行符）。
 * mermaid SVG 的节点标签与边标签均通过 foreignObject 嵌入 HTML，需要此函数。
 */
function extractForeignObjectLines(fo: Element): string[] {
  const lines: string[] = []
  let cur = ''

  function walk(node: Node): void {
    if (node.nodeType === Node.TEXT_NODE) {
      cur += node.textContent ?? ''
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      const tag = (node as Element).localName.toLowerCase()
      if (tag === 'br') {
        const t = cur.trim(); if (t) { lines.push(t); cur = '' }
      } else {
        for (const child of Array.from(node.childNodes)) walk(child)
        if (tag === 'p' || tag === 'div') {
          const t = cur.trim(); if (t) { lines.push(t); cur = '' }
        }
      }
    }
  }

  for (const child of Array.from(fo.childNodes)) walk(child)
  const t = cur.trim(); if (t) lines.push(t)
  return lines.filter(Boolean)
}

/**
 * 将 SVG doc 中所有 <foreignObject> 替换为原生 <text> 元素。
 * Chrome 120+ 规定：含 <foreignObject> 的 SVG 绘入 canvas 后 canvas 被污染（origin-clean = false），
 * 调用 canvas.toBlob() 会抛 SecurityError。替换为 <text> 后即可正常导出 PNG。
 */
function replaceForeignObjectsWithText(doc: Document): void {
  const fos = Array.from(doc.querySelectorAll('foreignObject'))
  if (fos.length === 0) return

  const NS = 'http://www.w3.org/2000/svg'
  for (const fo of fos) {
    const foW = parseFloat(fo.getAttribute('width')  ?? '100')
    const foH = parseFloat(fo.getAttribute('height') ?? '24')
    const lines = extractForeignObjectLines(fo)
    if (lines.length === 0) { fo.parentElement?.removeChild(fo); continue }

    const textEl = doc.createElementNS(NS, 'text')
    textEl.setAttribute('text-anchor', 'middle')
    textEl.setAttribute('dominant-baseline', 'middle')
    textEl.setAttribute('font-size', '14')
    textEl.setAttribute('font-family', '"trebuchet ms",verdana,arial,sans-serif')
    textEl.setAttribute('fill', '#333')

    if (lines.length === 1) {
      textEl.setAttribute('x', String(foW / 2))
      textEl.setAttribute('y', String(foH / 2))
      textEl.textContent = lines[0]
    } else {
      const lineH = 20
      const startY = foH / 2 - (lines.length * lineH) / 2 + lineH / 2
      for (let i = 0; i < lines.length; i++) {
        const tspan = doc.createElementNS(NS, 'tspan')
        tspan.setAttribute('x', String(foW / 2))
        tspan.setAttribute('y', String(startY + i * lineH))
        tspan.textContent = lines[i]
        textEl.appendChild(tspan)
      }
    }
    fo.parentElement?.replaceChild(textEl, fo)
  }
}

/**
 * 将 SVG 字符串渲染到 canvas 并返回 PNG Blob，用于「复制图片」功能。
 * 自动从 width/height 属性或 viewBox 推算像素尺寸；scale=2 输出 2× 高清。
 * 含 <foreignObject> 的 SVG（如 mermaid 流程图）会先替换为原生 <text>，避免 canvas 污染。
 */
export async function svgStringToPngBlob(svgHtml: string, scale = 2): Promise<Blob> {
  const parser = new DOMParser()
  const doc = parser.parseFromString(svgHtml, 'image/svg+xml')
  const svgEl = doc.querySelector('svg')
  if (!svgEl) throw new Error('SVG element not found')

  // 替换 <foreignObject> → <text>，避免 Chrome 120+ canvas 污染导致 toBlob 抛 SecurityError
  replaceForeignObjectsWithText(doc)

  let w = 0, h = 0
  const wAttr = svgEl.getAttribute('width')
  const hAttr = svgEl.getAttribute('height')
  const vb   = svgEl.getAttribute('viewBox')

  if (wAttr && !/^\d*\.?\d+%$/.test(wAttr)) w = parseFloat(wAttr)
  if (hAttr && !/^\d*\.?\d+%$/.test(hAttr)) h = parseFloat(hAttr)

  if ((!w || !h) && vb) {
    const parts = vb.trim().split(/[\s,]+/)
    if (parts.length >= 4) {
      if (!w) w = parseFloat(parts[2])
      if (!h) h = parseFloat(parts[3])
    }
  }
  if (!w || w <= 0) w = 800
  if (!h || h <= 0) h = 600

  svgEl.setAttribute('width',  String(w))
  svgEl.setAttribute('height', String(h))

  const serializer = new XMLSerializer()
  const svgString  = serializer.serializeToString(svgEl)
  const svgBlob    = new Blob([svgString], { type: 'image/svg+xml;charset=utf-8' })
  const svgUrl     = URL.createObjectURL(svgBlob)

  return new Promise<Blob>((resolve, reject) => {
    const img = new Image()
    img.onload = () => {
      const canvas = document.createElement('canvas')
      canvas.width  = w * scale
      canvas.height = h * scale
      const ctx = canvas.getContext('2d')
      if (!ctx) { URL.revokeObjectURL(svgUrl); reject(new Error('canvas 2d unavailable')); return }
      ctx.fillStyle = '#ffffff'
      ctx.fillRect(0, 0, canvas.width, canvas.height)
      ctx.scale(scale, scale)
      ctx.drawImage(img, 0, 0, w, h)
      URL.revokeObjectURL(svgUrl)
      try {
        canvas.toBlob(
          blob => (blob ? resolve(blob) : reject(new Error('canvas toBlob failed'))),
          'image/png',
        )
      } catch (e) {
        // canvas 被外部资源污染（tainted）时 toBlob 同步抛 SecurityError
        reject(e)
      }
    }
    img.onerror = () => { URL.revokeObjectURL(svgUrl); reject(new Error('SVG image load failed')) }
    img.src = svgUrl
  })
}

/**
 * 生成 HTML Widget 的 srcDoc。forFullscreen=true 时省略 resize postMessage。
 * 如果用户已经提供了 <!doctype 或 <html，原样返回。
 */
function buildHtmlSrcDoc(code: string, widgetId: string, forFullscreen = false): string {
  const trimmed = code.trimStart()
  const looksLikeDoc = /^<!doctype/i.test(trimmed) || /^<html/i.test(trimmed)
  // 沙箱补丁（三层防御）：
  //   1. 直接覆盖 window.postMessage，拦截 sandbox iframe 内所有 postMessage 调用
  //      （包括浏览器扩展注入的 content_main.js 等外部脚本），将 'null' targetOrigin 替换为 '*'
  //   2. Proxy 包装 window.parent，防止 LLM 代码持有 parent.postMessage 存储引用绕过直接覆盖
  //   3. unhandledrejection 监听器，抑制 Promise 中未处理的 postMessage 报错
  //      注意：Object.defineProperty(location, 'origin', ...) 在 Chrome 中会失败（non-configurable），
  //            因此不能通过修改 location.origin 属性来修复，只能拦截 postMessage 调用
  // 截图能力由父页面临时截图 iframe 承担（htmlIframeToPngBlob），此处无需注入
  const patchScript = `<script>(function(){try{var _o=window.postMessage;window.postMessage=function(m,t,r){if(!t||t==='null'||t===location.origin)t='*';return _o.call(this,m,t,r);};var _p=window.parent;if(_p&&_p!==window){var _x=new Proxy(_p,{get:function(t,p){if(p==='postMessage'){return function(m,t,r){if(!t||t==='null'||t===location.origin)t='*';return t.postMessage.call(t,m,t,r);};}try{return t[p];}catch(e){return undefined;}}});Object.defineProperty(window,'parent',{get:function(){return _x;},configurable:true});}}catch(e){}window.addEventListener('error',function(e){if(e.message&&e.message.indexOf('target origin')>=0)e.preventDefault();},true);window.addEventListener('unhandledrejection',function(e){if(e.reason&&e.reason.message&&e.reason.message.indexOf('target origin')>=0)e.preventDefault();});})();<\/script>`

  const resizeScript = forFullscreen
    ? ''
    : `<script>
(function(){
  // 覆盖 LLM 可能设置的固定高度（height:100vh / min-height:Npx 等），防止 iframe 底部出现大片空白。
  // 注入 !important 样式确保在 LLM 自定义 CSS 之后生效。
  // div/section 等 block 容器的 min-height 全部清零：
  //   LLM 常用 display:flex + min-height:500px + 底部 flex:1 空 spacer 撑高卡片，
  //   清零后 spacer 自动收缩为 0，scrollHeight 即为实际内容高度。
  // post() 中临时向 <head> 注入 <style> 清零所有 block 容器的 height（含深层元素）：
  //   LLM 常用 height:100vh 或嵌套 height:100% 的全屏 wrapper，若只清零直接子元素
  //   则深层 100vh 会随 iframe 高度正反馈增长直至上限 1200px。
  //   临时 <style> 插入 head，读完立即删除；MutationObserver 只监视 body，
  //   head 的 childList 变化不触发，彻底消除循环。
  //   不修改任何元素的 style 属性，图表库（ECharts/Leaflet 等）感知不到高度变化，不会重新渲染。
  var _hs = document.createElement('style');
  _hs.textContent = 'html,body{height:auto!important;min-height:0!important;}' +
    'div,section,article,main,aside,header,footer,li,ul,ol{min-height:0!important;}';
  (document.head || document.documentElement).appendChild(_hs);
  var _t = null;
  function post(){
    if(_t) return;
    _t = requestAnimationFrame(function(){
      _t = null;
      var _head = document.head || document.documentElement;
      var _tmp = document.createElement('style');
      _tmp.textContent = 'html,body,div,section,article,main,aside,header,footer,ul,ol,li{height:auto!important;}';
      _head.appendChild(_tmp);
      // 用 getBoundingClientRect().height 而非 scrollHeight：
      //   scrollHeight 的语义是 max(内容高度, 视口高度)，随 iframe 视口增大而增大，
      //   导致每次 resize 触发后循环放大直至 1200px 上限。
      //   getBoundingClientRect().height 返回 body 元素的实际渲染高度，不受视口高度影响。
      var h = document.body.getBoundingClientRect().height;
      _head.removeChild(_tmp);
      parent.postMessage({ type: 'widget-resize', widgetId: ${JSON.stringify(widgetId)}, height: h }, '*');
    });
  }
  window.addEventListener('load', post);
  window.addEventListener('resize', post);
  new MutationObserver(post).observe(document.body, { childList: true, subtree: true, attributes: true });
})();
<\/script>`
  if (looksLikeDoc) {
    // patchScript 必须注入在 <head> 开头，确保在 LLM 脚本运行前就设置好 postMessage proxy
    let result = code
    if (/<head[^>]*>/i.test(result)) result = result.replace(/<head[^>]*>/i, m => `${m}${patchScript}`)
    else if (/<\/head>/i.test(result)) result = result.replace(/<\/head>/i, `${patchScript}</head>`)
    else if (/<html[^>]*>/i.test(result)) result = result.replace(/<html[^>]*>/i, m => `${m}${patchScript}`)
    else result = patchScript + result
    if (!forFullscreen && /<\/body>/i.test(result)) result = result.replace(/<\/body>/i, `${resizeScript}</body>`)
    return result
  }
  return `<!doctype html>
<html><head><meta charset="utf-8">${patchScript}<style>*,*::before,*::after{box-sizing:border-box}html,body{margin:0;padding:0;height:auto;font-family:system-ui,-apple-system,Segoe UI,sans-serif;color:#1f2937;background:transparent;}body{padding:8px;}</style></head>
<body>${code}${resizeScript}</body></html>`
}

type H2CFunc = (el: HTMLElement, opts?: object) => Promise<HTMLCanvasElement>
let _h2cLoader: Promise<H2CFunc> | null = null

/** 懒加载 html2canvas 模块，首次点击时才拉取代码分片，后续复用缓存。 */
function lazyHtml2Canvas(): Promise<H2CFunc> {
  _h2cLoader ??= import('html2canvas').then(m => m.default as H2CFunc)
  return _h2cLoader
}

/**
 * 用 blob URL 临时 iframe + 父页面直接调用 html2canvas 的方式截取 HTML 内容，返回 PNG Blob。
 * - 无 CDN 依赖、无 eval、无 postMessage 协议，稳定可靠
 * - blob URL 与父页面同源，allow-same-origin 让父页面直接访问 iframe DOM
 * - 临时 iframe 仅在截图期间存在，完成后立即销毁
 */
function captureWidgetFromHtml(htmlSrc: string, transparent = false): Promise<Blob> {
  return new Promise((resolve, reject) => {
    const htmlBlob = new Blob([htmlSrc], { type: 'text/html; charset=utf-8' })
    const blobUrl = URL.createObjectURL(htmlBlob)

    const iframe = document.createElement('iframe')
    iframe.setAttribute('sandbox', 'allow-scripts allow-same-origin')
    iframe.style.cssText = 'position:fixed;left:-9999px;top:-9999px;width:900px;height:600px;visibility:hidden;pointer-events:none;border:none;'
    iframe.src = blobUrl

    const cleanup = () => {
      try { document.body.removeChild(iframe) } catch { /* already removed */ }
      URL.revokeObjectURL(blobUrl)
    }
    const timer = setTimeout(() => { cleanup(); reject(new Error('capture timeout')) }, 15000)

    iframe.addEventListener('load', () => {
      // 等待 ECharts 等异步渲染脚本完成（通常 < 500ms，预留 1.5s）
      setTimeout(() => {
        lazyHtml2Canvas()
          .then(h2c => {
            const body = iframe.contentDocument?.body
            if (!body) throw new Error('no body')
            // 读取实际渲染高度，避免固定 iframe 高度引起底部大量空白
            // getBoundingClientRect().height 不受视口高度影响（同 resizeScript 里的取值策略）
            const contentH = Math.ceil(body.getBoundingClientRect().height) || 600
            iframe.style.height = `${contentH}px`
            return h2c(body, {
              scale: 2, useCORS: true, logging: false,
              backgroundColor: transparent ? null : '#ffffff',
              width: 900, height: contentH,
              windowWidth: 900, windowHeight: contentH,
            })
          })
          .then(canvas => {
            clearTimeout(timer)
            cleanup()
            canvas.toBlob(b => {
              if (b) resolve(b)
              else reject(new Error('toBlob failed'))
            }, 'image/png')
          })
          .catch((err: unknown) => { clearTimeout(timer); cleanup(); reject(err) })
      }, 1500)
    })

    iframe.addEventListener('error', () => { clearTimeout(timer); cleanup(); reject(new Error('iframe load error')) })
    document.body.appendChild(iframe)
  })
}

interface MermaidWidgetPaneProps {
  code: string
  onSvgChange?: (svg: string | null) => void
  className?: string
  expand?: boolean
}

/**
 * 将 Mermaid 图表语法渲染为 SVG，供 WidgetBlock 在 show_widget 工具结果中展示。
 * 逻辑与 MarkdownRenderer 中的 MermaidSvgPane 一致，但独立于 Markdown 渲染管道。
 */
function MermaidWidgetPane({ code, onSvgChange, className, expand = false }: MermaidWidgetPaneProps) {
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    onSvgChange?.(null)
    if (!containerRef.current) return

    const container = containerRef.current
    container.innerHTML = ''
    const id = `mermaid-widget-${++mermaidWidgetCounter}`
    let cancelled = false

    const cleanupById = () => {
      for (const targetId of [id, `d${id}`]) {
        document.querySelectorAll(`[id="${targetId}"]`).forEach((el) => {
          if (!container.contains(el)) el.remove()
        })
      }
    }

    const showFallback = () => {
      if (cancelled || containerRef.current !== container) return
      container.innerHTML = ''
      const pre = document.createElement('pre')
      pre.style.cssText = 'padding:8px;font-size:12px;overflow:auto;white-space:pre-wrap;margin:0'
      pre.textContent = code
      container.appendChild(pre)
      onSvgChange?.(null)
    }

    void (async () => {
      const renderableCode = await resolveRenderableMermaidCode(code)
      if (!renderableCode) {
        cleanupById()
        showFallback()
        return
      }

      const { svg } = await mermaid.render(id, renderableCode)
      if (!cancelled && containerRef.current === container) {
        container.innerHTML = svg
        const svgEl = container.querySelector('svg')
        if (svgEl instanceof SVGSVGElement) {
          svgEl.style.display = 'block'
          svgEl.style.height = 'auto'
          if (expand) {
            svgEl.style.width = '100%'
            svgEl.style.margin = '0 auto'
          } else {
            const vb = svgEl.getAttribute('viewBox')
            const vbParts = vb?.trim().split(/[\s,]+/) ?? []
            if (vbParts.length >= 4) {
              const vw = parseFloat(vbParts[2])
              if (vw > 0) svgEl.setAttribute('width', String(Math.ceil(vw)))
            }
            svgEl.style.maxWidth = '100%'
            svgEl.style.margin = '0 auto'
          }
        }
        onSvgChange?.(svg)
      }
      cleanupById()
    })().catch(() => {
      cleanupById()
      showFallback()
    })

    return () => {
      cancelled = true
      cleanupById()
    }
  }, [code, onSvgChange])

  return <div ref={containerRef} className={className} />
}

interface WidgetFullscreenDialogProps {
  data: WidgetData
  onClose: () => void
}

/**
 * Widget 全屏预览对话框。
 * - 预览模式：SVG 内联居中展示；HTML 在 sandboxed iframe 中全屏渲染
 * - 代码模式：可编辑 textarea，切换回预览时立即生效
 * - ESC / 点击背景关闭
 */
function WidgetFullscreenDialog({ data, onClose }: WidgetFullscreenDialogProps) {
  const [tab, setTab] = useState<'preview' | 'code'>('preview')
  const [editCode, setEditCode] = useState(data.code)
  const [previewCode, setPreviewCode] = useState(data.code)
  const [zoom, setZoom] = useState(1)
  const svgScrollRef = useRef<HTMLDivElement>(null)
  const [fsMermaidSvg, setFsMermaidSvg] = useState<string | null>(null)
  const [fsImageCopied, setFsImageCopied] = useState(false)
  const [fsImageCopyErr, setFsImageCopyErr] = useState(false)
  const [fsImageSaved, setFsImageSaved] = useState(false)
  const [fsFallbackBlob, setFsFallbackBlob] = useState<Blob | null>(null)

  // 检测是否为 Mermaid 语法（从 HTML widget 中识别出来）：支持原始语法和 <pre class="mermaid"> 包装形式
  const mermaidCode = data.kind === 'html' ? extractMermaidCode(previewCode) : null
  const isMermaid = mermaidCode !== null

  // SVG 自然尺寸：transform: scale 方案需要明确声明 zoomed 后的占位尺寸来撑开 scroll region
  const svgDim = useMemo(
    () => data.kind === 'svg' ? getSvgDimensions(previewCode) : { w: 800, h: 600 },
    [data.kind, previewCode],
  )
  // 白卡 p-6 = 24px，两侧共 48px
  const cardPad = 48
  const cardNatW = svgDim.w + cardPad
  const cardNatH = svgDim.h + cardPad

  const srcDoc = useMemo(
    () => (data.kind === 'html' && !isMermaid) ? buildHtmlSrcDoc(previewCode, data.widgetId, true) : '',
    [data.kind, data.widgetId, isMermaid, previewCode],
  )

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  // Ctrl+滚轮缩放 SVG（非 passive 监听才能 preventDefault 阻止页面滚动）
  useEffect(() => {
    if (tab !== 'preview' || data.kind !== 'svg') return
    const el = svgScrollRef.current
    if (!el) return
    function onWheel(e: WheelEvent) {
      if (!e.ctrlKey && !e.metaKey) return
      e.preventDefault()
      setZoom(z => Math.max(0.25, Math.min(5, z + (e.deltaY > 0 ? -0.1 : 0.1))))
    }
    el.addEventListener('wheel', onWheel, { passive: false })
    return () => el.removeEventListener('wheel', onWheel)
  }, [tab, data.kind])

  function switchToPreview() {
    setPreviewCode(editCode)
    setTab('preview')
  }

  async function getFsBlob(): Promise<Blob> {
    const transparent = data.background === 'transparent'
    if (isMermaid) {
      if (!fsMermaidSvg) throw new Error('svg not ready')
      return svgStringToPngBlob(fsMermaidSvg)
    }
    if (data.kind === 'svg') return svgStringToPngBlob(previewCode)
    return captureWidgetFromHtml(buildHtmlSrcDoc(previewCode, data.widgetId, true), transparent)
  }

  async function copyFsImage() {
    try {
      const blob = await getFsBlob()
      const ok = await copyImageOrFallback(blob)
      if (ok) {
        setFsImageCopied(true)
        setTimeout(() => setFsImageCopied(false), 1500)
      } else {
        setFsFallbackBlob(blob)
      }
    } catch {
      setFsImageCopyErr(true)
      setTimeout(() => setFsImageCopyErr(false), 2000)
    }
  }

  async function saveFsImage() {
    try {
      const blob = await getFsBlob()
      savePngBlob(blob, `${data.title || 'widget'}-${Date.now()}.png`)
      setFsImageSaved(true)
      setTimeout(() => setFsImageSaved(false), 1500)
    } catch {
      /* ignore */
    }
  }

  if (typeof document === 'undefined') return null

  const fsBtnClass = 'flex items-center justify-center w-7 h-7 rounded-md border border-white/20 transition-colors'

  return createPortal(
    <div
      className="fixed inset-0 z-[80] bg-black/70 backdrop-blur-sm flex flex-col"
      data-testid="widget-fullscreen-dialog"
    >
      {/* 点击背景关闭 */}
      <div className="absolute inset-0" onClick={onClose} />
      {/* 内容区（需在背景层之上） */}
      <div className="relative flex flex-col h-full">
        {/* 标题栏 */}
        <div className="flex-none flex items-center justify-between gap-4 border-b border-white/10 px-4 py-3 text-white">
          <div className="text-sm font-medium truncate">
            {data.title || (data.kind === 'svg' ? 'SVG 图形' : 'HTML 组件')}
          </div>
          <div className="flex items-center gap-2">
            {/* 预览 / 代码 切换 */}
            <div className="flex rounded-lg border border-white/20 overflow-hidden text-xs">
              <button
                type="button"
                onClick={switchToPreview}
                className={cn(
                  'px-3 py-1.5 transition-colors',
                  tab === 'preview'
                    ? 'bg-white/20 text-white'
                    : 'text-white/60 hover:bg-white/10 hover:text-white/90',
                )}
              >
                预览
              </button>
              <button
                type="button"
                onClick={() => setTab('code')}
                className={cn(
                  'px-3 py-1.5 transition-colors',
                  tab === 'code'
                    ? 'bg-white/20 text-white'
                    : 'text-white/60 hover:bg-white/10 hover:text-white/90',
                )}
              >
                代码
              </button>
            </div>
            {/* SVG 缩放控件（Ctrl+滚轮 或 按钮）*/}
            {data.kind === 'svg' && tab === 'preview' && (
              <div className="flex items-center rounded-lg border border-white/20 overflow-hidden text-xs">
                <button
                  type="button"
                  onClick={() => setZoom(z => Math.max(0.25, z - 0.25))}
                  className="px-2.5 py-1.5 text-white/60 hover:bg-white/10 hover:text-white transition-colors"
                  title="缩小"
                >−</button>
                <button
                  type="button"
                  onClick={() => setZoom(1)}
                  className="px-2 py-1.5 text-white/70 tabular-nums min-w-[3.5rem] text-center hover:bg-white/10 hover:text-white transition-colors"
                  title="点击重置为 100%"
                >{Math.round(zoom * 100)}%</button>
                <button
                  type="button"
                onClick={() => setZoom(z => Math.min(16, z + 0.25))}
                  className="px-2.5 py-1.5 text-white/60 hover:bg-white/10 hover:text-white transition-colors"
                  title="放大"
                >+</button>
              </div>
            )}
            {/* 复制图片 / 另存为图片（预览模式才显示） */}
            {tab === 'preview' && (
              <>
                <button
                  type="button"
                  onClick={copyFsImage}
                  title={fsImageCopyErr ? '复制失败' : fsImageCopied ? '已复制' : '复制图片'}
                  className={cn(
                    fsBtnClass,
                    fsImageCopyErr
                      ? 'text-red-400 hover:text-red-300 bg-white/5'
                      : 'text-white/70 hover:text-white hover:bg-white/10',
                  )}
                >
                  <Icon name={fsImageCopyErr ? 'error' : fsImageCopied ? 'check' : 'content_copy'} size="sm" />
                </button>
                <button
                  type="button"
                  onClick={saveFsImage}
                  title={fsImageSaved ? '已保存' : '另存为图片'}
                  className={cn(fsBtnClass, 'text-white/70 hover:text-white hover:bg-white/10')}
                >
                  <Icon name={fsImageSaved ? 'check' : 'save_alt'} size="sm" />
                </button>
              </>
            )}
            {/* 关闭 */}
            <button
              type="button"
              onClick={onClose}
              className="flex h-8 w-8 items-center justify-center rounded-lg border border-white/15 bg-white/10 text-white hover:bg-white/15 transition-colors"
              title="关闭 (ESC)"
            >
              <Icon name="close" size="sm" />
            </button>
          </div>
        </div>
        {/* 主体：三个 tab 内容绝对定位填满剩余区域 */}
        <div className="flex-1 relative overflow-hidden">
          {/* SVG 预览：transform: scale + 外壳占位
              CSS zoom 在 Chrome 的 overflow: auto 容器中不撑开 scroll region，
              改用 transform: scale，外层 wrapper 声明 zoom 后的逻辑尺寸来撑开滚动区域。 */}
          {tab === 'preview' && data.kind === 'svg' && (
            <div ref={svgScrollRef} className="absolute inset-0 overflow-auto">
              {/* flex 容器：min-w/h-full 保证小图能居中；grow 跟随 wrapper 撑开 */}
              <div className="flex justify-center items-start min-w-full min-h-full p-8">
                {/* 外层占位：宽高 = 卡片自然尺寸 × zoom，撑开 overflow-auto 的 scroll region */}
                <div
                  style={{
                    flexShrink: 0,
                    width: cardNatW * zoom,
                    height: cardNatH * zoom,
                    position: 'relative',
                  }}
                >
                  {/* 内层：transform: scale 视觉缩放（不影响布局，由外壳负责布局尺寸）*/}
                  <div
                    style={{
                      position: 'absolute',
                      top: 0,
                      left: 0,
                      width: cardNatW,
                      height: cardNatH,
                      transform: `scale(${zoom})`,
                      transformOrigin: 'top left',
                    }}
                    className="bg-white rounded-2xl p-6 shadow-2xl [&_svg]:block [&_svg]:max-w-full [&_svg]:h-auto"
                    // eslint-disable-next-line react/no-danger
                    dangerouslySetInnerHTML={{ __html: previewCode }}
                  />
                </div>
              </div>
            </div>
          )}
          {/* Mermaid 预览：HTML widget 内含 Mermaid 语法时，使用 Mermaid 引擎渲染 SVG */}
          {tab === 'preview' && data.kind === 'html' && isMermaid && (
            <div className="absolute inset-0 overflow-auto">
              <div className="flex justify-center items-start min-w-full min-h-full p-8">
                <div className="bg-white rounded-2xl p-6 shadow-2xl w-full max-w-5xl">
                  <MermaidWidgetPane code={mermaidCode ?? ''} onSvgChange={setFsMermaidSvg} expand className="w-full" />
                </div>
              </div>
            </div>
          )}
          {/* HTML 预览：全屏 iframe */}
          {tab === 'preview' && data.kind === 'html' && !isMermaid && (
            <iframe
              sandbox="allow-scripts"
              srcDoc={srcDoc}
              title={data.title || 'HTML 全屏预览'}
              className="absolute inset-0 w-full h-full border-0 bg-white"
            />
          )}
          {/* 代码编辑 */}
          {tab === 'code' && (
            <textarea
              value={editCode}
              onChange={e => setEditCode(e.target.value)}
              className="absolute inset-0 w-full h-full font-mono text-sm bg-gray-900 text-gray-100 p-4 resize-none outline-none border-0"
              spellCheck={false}
            />
          )}
        </div>
      </div>
      <MobileImageFallback
        open={fsFallbackBlob !== null}
        blob={fsFallbackBlob}
        onClose={() => setFsFallbackBlob(null)}
        filename={`${data.title || 'widget'}-${Date.now()}.png`}
      />
    </div>,
    document.body,
  )
}

/**
 * 可视化 Widget 渲染块。SVG 直接内联；HTML 使用 sandboxed iframe + srcDoc 隔离。
 *
 * 安全策略：
 * - iframe sandbox 仅允许 allow-scripts，不允许 allow-same-origin/allow-forms/allow-top-navigation
 * - srcDoc 注入完整 HTML 文档，禁止访问父窗口
 * - 后端 WidgetToolService 已对体积上限与 <script src=...> 做白名单校验（仅允许 jsdelivr/cdnjs/unpkg/bootcdn/staticfile）
 */
export function WidgetBlock({ data, className }: WidgetBlockProps) {
  const [iframeHeight, setIframeHeight] = useState<number>(data.initialHeight ?? 480)
  const [copied, setCopied] = useState(false)
  const [imageCopied, setImageCopied] = useState(false)
  const [imageCopyErr, setImageCopyErr] = useState(false)
  const [imageSaved, setImageSaved] = useState(false)
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [fallbackBlob, setFallbackBlob] = useState<Blob | null>(null)
  // show_widget 工具传入原始 Mermaid 语法时，记录渲染后的 SVG（用于复制图片）
  const [mermaidSvg, setMermaidSvg] = useState<string | null>(null)

  const handleCloseFullscreen = useCallback(() => setIsFullscreen(false), [])

  // 检测是否为 Mermaid 语法：show_widget 工具有时会把 mermaid 代码当 HTML 传入。
  // 支持原始语法（flowchart TD…）和 <pre class="mermaid">…</pre> 包装形式。
  const mermaidCode = data.kind === 'html' ? extractMermaidCode(data.code) : null
  const isMermaid = mermaidCode !== null

  // 包一层 HTML 文档；如果用户已经提供了 <!doctype 或 <html，原样使用
  const srcDoc = useMemo(() => {
    if (data.kind !== 'html' || isMermaid) return ''
    return buildHtmlSrcDoc(data.code, data.widgetId)
  }, [data.kind, data.code, data.widgetId, isMermaid])

  useEffect(() => {
    if (data.kind !== 'html' || isMermaid) return
    function onMsg(ev: MessageEvent) {
      const msg = ev.data as { type?: string; widgetId?: string; height?: number } | undefined
      if (!msg || msg.type !== 'widget-resize' || msg.widgetId !== data.widgetId) return
      if (typeof msg.height === 'number' && msg.height > 0) {
        // 上限 1200px，避免恶意撑爆页面
        setIframeHeight(Math.min(Math.max(msg.height + 8, 80), 1200))
      }
    }
    window.addEventListener('message', onMsg)
    return () => window.removeEventListener('message', onMsg)
  }, [data.kind, data.widgetId, isMermaid])

  async function copyCode() {
    try {
      await navigator.clipboard.writeText(data.code)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      /* ignore */
    }
  }

  async function getPngBlob(): Promise<Blob> {
    const transparent = data.background === 'transparent'
    if (isMermaid) {
      if (!mermaidSvg) throw new Error('svg not ready')
      return svgStringToPngBlob(mermaidSvg)
    }
    if (data.kind === 'svg') return svgStringToPngBlob(data.code)
    return captureWidgetFromHtml(buildHtmlSrcDoc(data.code, data.widgetId, true), transparent)
  }

  async function copyImage() {
    try {
      const pngBlob = await getPngBlob()
      const ok = await copyImageOrFallback(pngBlob)
      if (ok) {
        setImageCopied(true)
        setTimeout(() => setImageCopied(false), 1500)
      } else {
        setFallbackBlob(pngBlob)
      }
    } catch {
      setImageCopyErr(true)
      setTimeout(() => setImageCopyErr(false), 2000)
    }
  }

  async function saveImage() {
    try {
      const pngBlob = await getPngBlob()
      savePngBlob(pngBlob, `${data.title || 'widget'}-${Date.now()}.png`)
      setImageSaved(true)
      setTimeout(() => setImageSaved(false), 1500)
    } catch {
      /* ignore */
    }
  }

  async function downloadSvg() {
    if (data.kind !== 'svg') return
    const blob = new Blob([data.code], { type: 'image/svg+xml' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${data.title || 'widget'}-${Date.now()}.svg`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }

  const iconBtnClass = 'flex items-center justify-center w-6 h-6 rounded transition-colors text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400 hover:bg-gray-100 dark:hover:bg-gray-700'

  return (
    <div
      className={cn(
        'my-2 rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800/60 overflow-hidden',
        data.slideMode && 'aspect-[16/9] max-w-full',
        className,
      )}
      data-testid="widget-block"
      data-widget-id={data.widgetId}
      data-widget-kind={data.kind}
    >
      <div className="flex items-center justify-between px-3 py-1.5 bg-gray-50 dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700">
        <span className="text-xs font-medium text-gray-600 dark:text-gray-300 truncate">
          {data.title || (data.kind === 'svg' ? 'SVG 图形' : 'HTML 组件')}
        </span>
        <div className="flex items-center gap-0.5">
          <button type="button" onClick={() => setIsFullscreen(true)}
            title="全屏"
            className={iconBtnClass}
          >
            <Icon name="fullscreen" size="sm" />
          </button>
          <button
            type="button"
            onClick={copyImage}
            title={imageCopyErr ? '复制失败' : imageCopied ? '已复制' : '复制图片'}
            className={cn(
              iconBtnClass,
              imageCopyErr && 'text-red-500 hover:text-red-400 dark:text-red-500',
            )}
          >
            <Icon name={imageCopyErr ? 'error' : imageCopied ? 'check' : 'content_copy'} size="sm" />
          </button>
          <button type="button" onClick={saveImage}
            title={imageSaved ? '已保存' : '另存为图片'}
            className={iconBtnClass}
          >
            <Icon name={imageSaved ? 'check' : 'save_alt'} size="sm" />
          </button>
          {data.kind === 'svg' && (
            <button type="button" onClick={downloadSvg}
              title="下载 SVG（可拖入 PowerPoint 矢量编辑）"
              className={iconBtnClass}
            >
              <Icon name="download" size="sm" />
            </button>
          )}
          <button type="button" onClick={copyCode}
            title={copied ? '已复制' : '复制代码'}
            className={iconBtnClass}
          >
            <Icon name={copied ? 'check' : 'code'} size="sm" />
          </button>
        </div>
      </div>
      <div className="p-2">
        {data.kind === 'svg' ? (
          // SVG 直接 dangerouslySetInnerHTML；后端已校验体积，前端依赖浏览器对内联 SVG 的脚本沙箱
          // [&>svg]:h-auto：强制浏览器依据 viewBox 宽高比计算高度，消除 LLM 偏大的 height 属性造成的底部空白
          <div
            className="widget-svg overflow-auto max-h-[600px] [&>svg]:block [&>svg]:max-w-full [&>svg]:h-auto"
            // eslint-disable-next-line react/no-danger
            dangerouslySetInnerHTML={{ __html: data.code }}
          />
        ) : isMermaid ? (
          // show_widget 工具传入原始 Mermaid 语法时（kind='html' 但以 flowchart/graph 等开头，
          // 或包裹在 <pre class="mermaid"> 中），
          // 使用 Mermaid 引擎直接渲染 SVG，避免放入空白 iframe。
          <MermaidWidgetPane
            code={mermaidCode ?? ''}
            onSvgChange={setMermaidSvg}
            className="overflow-auto max-h-[600px] p-4"
          />
        ) : (
          <iframe
            title={data.title || 'widget'}
            sandbox="allow-scripts"
            srcDoc={srcDoc}
            style={{ width: '100%', height: iframeHeight, border: 0 }}
            className="bg-white dark:bg-gray-900 rounded"
          />
        )}
      </div>
      {isFullscreen && <WidgetFullscreenDialog data={data} onClose={handleCloseFullscreen} />}
      <MobileImageFallback
        open={fallbackBlob !== null}
        blob={fallbackBlob}
        onClose={() => setFallbackBlob(null)}
        filename={`${data.title || 'widget'}-${Date.now()}.png`}
      />
    </div>
  )
}

/** 从工具结果 JSON 解析 WidgetData。result 非 JSON 或缺字段时返回 null */
export function parseWidgetData(result?: string): WidgetData | null {
  if (!result) return null
  try {
    const r = JSON.parse(result) as Partial<WidgetData>
    if (!r.widgetId || !r.code || (r.kind !== 'svg' && r.kind !== 'html')) return null
    return {
      widgetId: String(r.widgetId),
      kind: r.kind,
      title: typeof r.title === 'string' ? r.title : undefined,
      code: String(r.code),
      loadingMessage: typeof r.loadingMessage === 'string' ? r.loadingMessage : undefined,
      initialHeight: typeof r.initialHeight === 'number' && r.initialHeight >= 80 && r.initialHeight <= 1200 ? r.initialHeight : undefined,
      background: r.background === 'transparent' ? 'transparent' : r.background === 'solid' ? 'solid' : undefined,
      slideMode: r.slideMode === true ? true : undefined,
    }
  } catch {
    return null
  }
}
