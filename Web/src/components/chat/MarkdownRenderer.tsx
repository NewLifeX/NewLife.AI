import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type ComponentPropsWithoutRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import remarkMath from 'remark-math'
import { createHighlighterCore, type HighlighterCore } from 'shiki/core'
import { createOnigurumaEngine } from 'shiki/engine/oniguruma'
import rehypeKatex from 'rehype-katex'
import rehypeRaw from 'rehype-raw'
import 'katex/dist/katex.min.css'
import { getMermaid } from '@/components/chat/mermaidLazy'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Lightbox } from '@/components/common/Lightbox'
import { ImageEditDialog } from '@/components/chat/ImageEditDialog'
import { ProgressiveImage } from '@/components/chat/ProgressiveImage'
import { resolveRenderableMermaidCode } from '@/components/chat/mermaidHelper'
import { useChatStore } from '@/stores/chatStore'
import { editImage } from '@/lib/api'

// mermaid 已改为按需懒加载（getMermaid），此处不再执行顶层初始化

// ── Shiki 代码高亮单例 ────────────────────────────────────────────────────────
let _shikiReady: HighlighterCore | null = null
let _shikiPromise: Promise<unknown> | null = null
const _shikiListeners: Array<() => void> = []

function ensureShiki(): void {
  if (_shikiReady || _shikiPromise) return
  _shikiPromise = createHighlighterCore({
    themes: [
      import('shiki/dist/themes/github-light.mjs'),
      import('shiki/dist/themes/github-dark.mjs'),
    ],
    langs: [
      import('shiki/dist/langs/javascript.mjs'),
      import('shiki/dist/langs/typescript.mjs'),
      import('shiki/dist/langs/jsx.mjs'),
      import('shiki/dist/langs/tsx.mjs'),
      import('shiki/dist/langs/json.mjs'),
      import('shiki/dist/langs/jsonc.mjs'),
      import('shiki/dist/langs/html.mjs'),
      import('shiki/dist/langs/css.mjs'),
      import('shiki/dist/langs/scss.mjs'),
      import('shiki/dist/langs/python.mjs'),
      import('shiki/dist/langs/bash.mjs'),
      import('shiki/dist/langs/shell.mjs'),
      import('shiki/dist/langs/powershell.mjs'),
      import('shiki/dist/langs/bat.mjs'),
      import('shiki/dist/langs/java.mjs'),
      import('shiki/dist/langs/csharp.mjs'),
      import('shiki/dist/langs/cpp.mjs'),
      import('shiki/dist/langs/c.mjs'),
      import('shiki/dist/langs/go.mjs'),
      import('shiki/dist/langs/rust.mjs'),
      import('shiki/dist/langs/sql.mjs'),
      import('shiki/dist/langs/yaml.mjs'),
      import('shiki/dist/langs/toml.mjs'),
      import('shiki/dist/langs/xml.mjs'),
      import('shiki/dist/langs/dockerfile.mjs'),
      import('shiki/dist/langs/markdown.mjs'),
    ],
    engine: createOnigurumaEngine(import('shiki/wasm')),
  }).then(h => {
    _shikiReady = h
    _shikiListeners.splice(0).forEach(fn => fn())
  }).catch(() => {
    _shikiPromise = null
  })
}

export function shikiHighlight(lang: string, code: string): string | null {
  if (!_shikiReady) { ensureShiki(); return null }
  const loaded = _shikiReady.getLoadedLanguages()
  const safeLang = loaded.includes(lang) ? lang : 'text'
  try {
    return _shikiReady.codeToHtml(code, {
      lang: safeLang,
      themes: { light: 'github-light', dark: 'github-dark' },
      defaultColor: false,
    })
  } catch {
    return null
  }
}

export function onShikiReady(fn: () => void): () => void {
  if (_shikiReady) { fn(); return () => {} }
  _shikiListeners.push(fn)
  return () => { const i = _shikiListeners.indexOf(fn); if (i >= 0) _shikiListeners.splice(i, 1) }
}
// ─────────────────────────────────────────────────────────────────────────────

let mermaidCounter = 0

interface MermaidActionButtonProps {
  title: string
  icon: string
  onClick: () => void
  disabled?: boolean
  className?: string
  testId?: string
}

interface MermaidSvgPaneProps {
  code: string
  isStreaming?: boolean
  className?: string
  fallbackClassName?: string
  scale?: number
  expand?: boolean
  onSvgChange?: (svg: string | null) => void
  testId?: string
}

interface MermaidPreviewDialogProps {
  open: boolean
  code: string
  fallbackClassName: string
  onClose: () => void
  onCopySource: () => void
  onDownloadSvg: () => void
}

function extractText(node: ReactNode): string {
  if (typeof node === 'string') return node
  if (typeof node === 'number') return String(node)
  if (Array.isArray(node)) return node.map(extractText).join('')
  if (node && typeof node === 'object' && 'props' in node) {
    return extractText((node as { props?: { children?: ReactNode } }).props?.children)
  }
  return ''
}

type HastChild = { type: string; value?: string; properties?: Record<string, unknown>; children?: HastChild[] }

function hastToText(node: HastChild): string {
  if (node.type === 'text') return node.value ?? ''
  if (node.children) return node.children.map(hastToText).join('')
  return ''
}

const CODE_COLLAPSE_THRESHOLD = 20

function downloadTextFile(fileName: string, content: string, mimeType: string) {
  const blob = new Blob([content], { type: mimeType })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 0)
}

function MermaidActionButton({ title, icon, onClick, disabled = false, className, testId }: MermaidActionButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      data-testid={testId}
      className={cn(
        'flex h-8 w-8 items-center justify-center rounded-lg border border-gray-200/80 bg-white/95 text-gray-600 shadow-sm transition hover:bg-gray-50 hover:text-gray-900 dark:border-gray-700/80 dark:bg-gray-900/90 dark:text-gray-300 dark:hover:bg-gray-800 dark:hover:text-white',
        disabled && 'cursor-not-allowed opacity-40 hover:bg-white/95 hover:text-gray-600 dark:hover:bg-gray-900/90 dark:hover:text-gray-300',
        className,
      )}
      title={title}
    >
      <Icon name={icon} size="sm" />
    </button>
  )
}

function MermaidSvgPane({
  code,
  isStreaming = false,
  className,
  fallbackClassName = 'rounded-lg bg-gray-50 dark:bg-gray-900 text-gray-800 dark:text-gray-100 border border-gray-200 dark:border-gray-700/50 p-4 overflow-x-auto text-sm leading-relaxed',
  scale = 1,
  expand = false,
  onSvgChange,
  testId,
}: MermaidSvgPaneProps) {
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    onSvgChange?.(null)

    if (isStreaming || !containerRef.current) return

    const container = containerRef.current
    container.innerHTML = ''

    const id = `mermaid-${++mermaidCounter}`
    let cancelled = false

    const cleanupBodyById = () => {
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
      pre.className = fallbackClassName
      pre.textContent = code
      container.appendChild(pre)
      onSvgChange?.(null)
    }

    void (async () => {
      const renderableCode = await resolveRenderableMermaidCode(code)
      if (!renderableCode) {
        cleanupBodyById()
        showFallback()
        return
      }

      const { svg } = await (await getMermaid()).render(id, renderableCode)
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
      cleanupBodyById()
    })().catch((err) => {
      console.error('[Mermaid] render error:', err)
      cleanupBodyById()
      showFallback()
    })

    return () => {
      cancelled = true
      cleanupBodyById()
    }
  }, [code, fallbackClassName, isStreaming, onSvgChange])

  if (isStreaming) {
    return <pre className={fallbackClassName}>{code}</pre>
  }

  return (
    <div
      ref={containerRef}
      data-testid={testId}
      className={className}
      style={scale === 1 ? undefined : { transform: `scale(${scale})`, transformOrigin: 'center top' }}
    />
  )
}

function MermaidPreviewDialog({ open, code, fallbackClassName, onClose, onCopySource, onDownloadSvg }: MermaidPreviewDialogProps) {
  const { t } = useTranslation()
  const [scale, setScale] = useState(1)

  useEffect(() => {
    if (open) setScale(1)
  }, [open])

  useEffect(() => {
    if (!open) return

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [onClose, open])

  if (!open || typeof document === 'undefined') return null

  return createPortal(
    <div data-testid="mermaid-preview-dialog" className="fixed inset-0 z-[80] bg-black/70 backdrop-blur-sm" onClick={onClose}>
      <div className="absolute inset-0 flex flex-col" onClick={(event) => event.stopPropagation()}>
        <div className="flex items-center justify-between gap-4 border-b border-white/10 px-4 py-3 text-white">
          <div className="text-sm font-medium">{t('mermaid.title')}</div>
          <div className="flex items-center gap-2">
            <MermaidActionButton title={t('mermaid.zoomOut')} icon="zoom_out" onClick={() => setScale((value) => Math.max(0.6, value - 0.2))} className="border-white/15 bg-white/10 text-white hover:bg-white/15 hover:text-white" testId="mermaid-zoom-out" />
            <MermaidActionButton title={t('mermaid.resetZoom')} icon="restart_alt" onClick={() => setScale(1)} className="border-white/15 bg-white/10 text-white hover:bg-white/15 hover:text-white" testId="mermaid-reset-zoom" />
            <MermaidActionButton title={t('mermaid.zoomIn')} icon="zoom_in" onClick={() => setScale((value) => Math.min(2.4, value + 0.2))} className="border-white/15 bg-white/10 text-white hover:bg-white/15 hover:text-white" testId="mermaid-zoom-in" />
            <MermaidActionButton title={t('mermaid.downloadSvg')} icon="download" onClick={onDownloadSvg} className="border-white/15 bg-white/10 text-white hover:bg-white/15 hover:text-white" testId="mermaid-preview-download" />
            <MermaidActionButton title={t('mermaid.copySource')} icon="content_copy" onClick={onCopySource} className="border-white/15 bg-white/10 text-white hover:bg-white/15 hover:text-white" testId="mermaid-preview-copy-source" />
            <MermaidActionButton title={t('mermaid.close')} icon="close" onClick={onClose} className="border-white/15 bg-white/10 text-white hover:bg-white/15 hover:text-white" testId="mermaid-close-preview" />
          </div>
        </div>

        <div className="flex-1 overflow-auto p-6">
          <div className="mx-auto flex w-full min-h-full items-center justify-center">
            <MermaidSvgPane
              code={code}
              testId="mermaid-preview-pane"
              className="w-full rounded-2xl bg-white p-6 shadow-2xl"
              fallbackClassName={fallbackClassName}
              scale={scale}
              expand
            />
          </div>
        </div>
      </div>
    </div>,
    document.body,
  )
}

function MermaidBlock({ code, isStreaming }: { code: string; isStreaming?: boolean }) {
  const { t } = useTranslation()
  const [previewOpen, setPreviewOpen] = useState(false)
  const [svgMarkup, setSvgMarkup] = useState<string | null>(null)

  const fallbackClassName = 'rounded-lg bg-gray-50 dark:bg-gray-900 text-gray-800 dark:text-gray-100 border border-gray-200 dark:border-gray-700/50 p-4 overflow-x-auto text-sm leading-relaxed'

  useEffect(() => {
    if (isStreaming) setPreviewOpen(false)
  }, [isStreaming])

  const handleCopySource = useCallback(() => {
    void navigator.clipboard.writeText(code)
  }, [code])

  const handleDownloadSvg = useCallback(() => {
    if (!svgMarkup) return
    downloadTextFile(`mermaid-${Date.now()}.svg`, svgMarkup, 'image/svg+xml;charset=utf-8')
  }, [svgMarkup])

  const handleOpenPreview = useCallback(() => {
    if (!svgMarkup) return
    setPreviewOpen(true)
  }, [svgMarkup])

  if (isStreaming) {
    return <MermaidSvgPane code={code} isStreaming fallbackClassName={fallbackClassName} />
  }

  return (
    <>
      <div data-testid="mermaid-block" className="group/mermaid relative my-4 overflow-x-auto rounded-xl border border-gray-200/80 bg-white dark:border-gray-700/60 dark:bg-gray-900/60">
        <div className="absolute right-2 top-2 z-10 flex items-center gap-1">
          <MermaidActionButton title={t('mermaid.enlarge')} icon="open_in_full" onClick={handleOpenPreview} disabled={!svgMarkup} testId="mermaid-open-preview" />
          <MermaidActionButton title={t('mermaid.downloadSvg')} icon="download" onClick={handleDownloadSvg} disabled={!svgMarkup} testId="mermaid-download-svg" />
          <MermaidActionButton title={t('mermaid.copySource')} icon="content_copy" onClick={handleCopySource} testId="mermaid-copy-source" />
        </div>

        <MermaidSvgPane
          code={code}
          testId="mermaid-inline-pane"
          className="overflow-x-auto p-4 pt-12"
          fallbackClassName={fallbackClassName}
          onSvgChange={setSvgMarkup}
        />
      </div>

      <MermaidPreviewDialog
        open={previewOpen}
        code={code}
        fallbackClassName={fallbackClassName}
        onClose={() => setPreviewOpen(false)}
        onCopySource={handleCopySource}
        onDownloadSvg={handleDownloadSvg}
      />
    </>
  )
}

interface MarkdownRendererProps {
  content: string
  isStreaming?: boolean
  className?: string
}

/**
 * 将 LLM 常见的 LaTeX 分隔符统一转换为 remark-math 标准格式
 * \[...\]  →  $$...$$  (块级公式)
 * \(...\)  →  $...$    (行内公式)
 */
function preprocessMath(content: string): string {
  let result = content.replace(/\\\[([\s\S]*?)\\\]/g, (_match, math) => `$$${math}$$`)
  result = result.replace(/\\\(([\s\S]*?)\\\)/g, (_match, math) => `$${math}$`)
  return result
}

/**
 * 在代码块以外，移除 Markdown 内容中危险的 HTML 标签（script/style/iframe 等），
 * 防止 AI 输出被利用作 XSS 注入。仅处理代码围栏外的部分。
 */
function preprocessStripDangerousHtml(content: string): string {
  const dangerousTagPattern = /<\/?(?:script|style|iframe|object|embed|form|base|link|meta|applet)(\s[^>]*)?>/gi
  const result: string[] = []
  const codeBlockRegex = /```[\s\S]*?```/g
  let lastIndex = 0
  let match: RegExpExecArray | null
  while ((match = codeBlockRegex.exec(content)) !== null) {
    const before = content.slice(lastIndex, match.index)
    result.push(before.replace(dangerousTagPattern, ''))
    result.push(match[0])
    lastIndex = match.index + match[0].length
  }
  result.push(content.slice(lastIndex).replace(dangerousTagPattern, ''))
  return result.join('')
}

function CopyCodeButton({ code }: { code: string }) {
  const handleCopy = useCallback(() => {
    void navigator.clipboard.writeText(code)
  }, [code])

  return (
    <button
      type="button"
      onClick={handleCopy}
      className="p-1 rounded bg-gray-200/80 dark:bg-gray-700/60 hover:bg-gray-300 dark:hover:bg-gray-600 text-gray-500 dark:text-gray-300 hover:text-gray-800 dark:hover:text-white transition-colors opacity-0 group-hover/code:opacity-100"
      title="Copy"
    >
      <Icon name="content_copy" size="sm" />
    </button>
  )
}

interface CollapsibleCodeBlockProps extends ComponentPropsWithoutRef<'pre'> {
  codeStr: string
  lang: string
  isStreaming?: boolean
  children?: ReactNode
}

function CollapsibleCodeBlock({ codeStr, lang: _lang, isStreaming = false, children, ...props }: CollapsibleCodeBlockProps) {
  const { t } = useTranslation()
  const lang = _lang ?? ''
  const [lineCount, setLineCount] = useState(() => {
    const text = codeStr.endsWith('\n') ? codeStr.slice(0, -1) : codeStr
    return text ? text.split('\n').length : 0
  })
  const shouldCollapse = lineCount > CODE_COLLAPSE_THRESHOLD
  const [collapsed, setCollapsed] = useState(shouldCollapse)

  useEffect(() => {
    if (codeStr) {
      const text = codeStr.endsWith('\n') ? codeStr.slice(0, -1) : codeStr
      setLineCount(text ? text.split('\n').length : 0)
    }
  }, [codeStr])

  const prevShouldCollapse = useRef(shouldCollapse)
  useEffect(() => {
    if (!prevShouldCollapse.current && shouldCollapse) setCollapsed(true)
    prevShouldCollapse.current = shouldCollapse
  }, [shouldCollapse])

  // Shiki 高亮（流式期间跳过，等结束后再高亮）
  const [highlightedHtml, setHighlightedHtml] = useState<string | null>(null)
  useEffect(() => {
    if (isStreaming) { setHighlightedHtml(null); return }
    let cancelled = false
    return onShikiReady(() => {
      if (!cancelled) setHighlightedHtml(shikiHighlight(lang, codeStr))
    })
  }, [lang, codeStr, isStreaming])

  const preRef = useRef<HTMLPreElement>(null)
  useLayoutEffect(() => {
    if (!codeStr && preRef.current) {
      const domText = preRef.current.textContent ?? ''
      const text = domText.endsWith('\n') ? domText.slice(0, -1) : domText
      const count = text ? text.split('\n').length : 0
      if (count > 0) {
        setLineCount(count)
        if (count > CODE_COLLAPSE_THRESHOLD) setCollapsed(true)
      }
    }
  }, [codeStr])

  const codeContent = highlightedHtml ? (
    <div
      className={cn('shiki-wrapper overflow-x-auto', shouldCollapse ? 'rounded-t-lg' : 'rounded-lg')}
      // eslint-disable-next-line react/no-danger
      dangerouslySetInnerHTML={{ __html: highlightedHtml }}
    />
  ) : (
    <pre
      ref={preRef}
      {...props}
      className={cn(
        'bg-gray-50 dark:bg-gray-900 text-gray-800 dark:text-gray-100 border border-gray-200 dark:border-gray-700/50 p-4 overflow-x-auto text-sm leading-relaxed',
        shouldCollapse ? 'rounded-t-lg' : 'rounded-lg',
      )}
    >
      {children}
    </pre>
  )

  return (
    <div className="relative group/code">
      <div className={cn(shouldCollapse && collapsed ? 'max-h-[17rem] overflow-hidden relative' : 'relative')}>
        {codeContent}
        {shouldCollapse && collapsed && (
          <div className={cn(
            'absolute bottom-0 inset-x-0 h-16 pointer-events-none',
            highlightedHtml ? 'shiki-collapse-fade' : 'bg-gradient-to-t from-gray-50 dark:from-gray-900 to-transparent',
          )} />
        )}
      </div>
      <div className="absolute top-2 right-2 flex items-center gap-1 z-10">
        {codeStr && <CopyCodeButton code={codeStr} />}
      </div>
      {shouldCollapse && (
        <button
          type="button"
          onClick={() => setCollapsed(v => !v)}
          className="w-full flex items-center justify-center gap-1 py-1.5 rounded-b-lg bg-gray-100 dark:bg-gray-800 text-xs text-gray-500 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200 hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors border-t border-gray-200 dark:border-gray-700/50"
        >
          <Icon name={collapsed ? 'expand_more' : 'expand_less'} size="sm" />
          {collapsed ? t('chat.codeExpandAll', { lines: lineCount }) : t('chat.codeCollapse')}
        </button>
      )}
    </div>
  )
}

export function MarkdownRenderer({ content, isStreaming = false, className }: MarkdownRendererProps) {
  const [lightboxOpen, setLightboxOpen] = useState(false)
  const [lightboxIndex, setLightboxIndex] = useState(0)
  const [editImageUrl, setEditImageUrl] = useState<string | null>(null)

  const images = useMemo(() => {
    const urls: string[] = []
    const imgRegex = /!\[.*?\]\((.*?)\)/g
    let match: RegExpExecArray | null
    while ((match = imgRegex.exec(content)) !== null) {
      urls.push(match[1])
    }
    return urls
  }, [content])

  const processedContent = useMemo(() => preprocessStripDangerousHtml(preprocessMath(content)), [content])

  const handleImageClick = useCallback(
    (src: string) => {
      const idx = images.indexOf(src)
      setLightboxIndex(idx >= 0 ? idx : 0)
      setLightboxOpen(true)
    },
    [images],
  )

  const markdownComponents = useMemo(() => ({
    pre({ children, node: preNode, ...props }: ComponentPropsWithoutRef<'pre'> & { node?: { children?: HastChild[] } }) {
      const codeHastNode = preNode?.children?.[0]
      const codeStr = codeHastNode ? hastToText(codeHastNode) : extractText(children)
      const classNames: string[] = (codeHastNode?.properties?.className as string[] | undefined) ?? []
      const lang = classNames.find(c => c.startsWith('language-'))?.replace('language-', '') ?? ''

      if (lang === 'mermaid') {
        return <MermaidBlock code={codeStr.replace(/\n$/, '')} isStreaming={isStreaming} />
      }

      return <CollapsibleCodeBlock codeStr={codeStr} lang={lang} isStreaming={isStreaming} {...props}>{children}</CollapsibleCodeBlock>
    },
    code({ className: codeClassName, children, ...props }: ComponentPropsWithoutRef<'code'>) {
      const isInline = !codeClassName
      if (isInline) {
        return (
          <code
            className="bg-gray-100/80 dark:bg-gray-800/80 text-gray-700 dark:text-gray-200 px-1.5 py-0.5 rounded-md text-[0.875em] font-mono border border-gray-200 dark:border-gray-700"
            {...props}
          >
            {children}
          </code>
        )
      }
      if (codeClassName?.includes('language-mermaid')) {
        const codeStr = extractText(children).replace(/\n$/, '')
        return <MermaidBlock code={codeStr} isStreaming={isStreaming} />
      }
      return (
        <code className={codeClassName} {...props}>
          {children}
        </code>
      )
    },
    a({ href, children, ...props }: ComponentPropsWithoutRef<'a'>) {
      const safeHref = (() => {
        if (!href) return undefined
        const h = /^www\./i.test(href) ? `https://${href}` : href
        try {
          const url = new URL(h)
          return ['http:', 'https:', 'mailto:'].includes(url.protocol) ? h : undefined
        } catch {
          return href.startsWith('/') || href.startsWith('#') ? href : undefined
        }
      })()
      if (!safeHref) return <span>{children}</span>

      return (
        <a
          href={safeHref}
          target="_blank"
          rel="noopener noreferrer"
          className="text-primary hover:underline"
          {...props}
        >
          {children}
        </a>
      )
    },
    table({ children, ...props }: ComponentPropsWithoutRef<'table'>) {
      return (
        <div className="overflow-x-auto my-2">
          <table className="border-collapse border border-gray-200 dark:border-gray-700 w-full text-sm" {...props}>
            {children}
          </table>
        </div>
      )
    },
    th({ children, ...props }: ComponentPropsWithoutRef<'th'>) {
      return (
        <th
          className="border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 px-3 py-2 text-left font-medium"
          {...props}
        >
          {children}
        </th>
      )
    },
    td({ children, ...props }: ComponentPropsWithoutRef<'td'>) {
      return (
        <td className="border border-gray-200 dark:border-gray-700 px-3 py-2" {...props}>
          {children}
        </td>
      )
    },
    img({ src, alt }: ComponentPropsWithoutRef<'img'>) {
      return (
        <ProgressiveImage
          src={src}
          alt={alt ?? ''}
          onClick={() => src && handleImageClick(src)}
        />
      )
    },
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }), [isStreaming, handleImageClick])

  return (
    <div className={cn('prose dark:prose-invert max-w-none break-words', isStreaming && 'streaming-prose', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkMath, remarkGfm]}
        rehypePlugins={[rehypeRaw, [rehypeKatex, { throwOnError: false, strict: false }]]}
        components={markdownComponents}
      >
        {processedContent}
      </ReactMarkdown>
      <Lightbox
        key={`${lightboxOpen}-${lightboxIndex}`}
        images={images}
        initialIndex={lightboxIndex}
        open={lightboxOpen}
        onClose={() => setLightboxOpen(false)}
        onEdit={(url) => { setLightboxOpen(false); setEditImageUrl(url) }}
      />
      {editImageUrl && (
        <ImageEditDialog
          imageUrl={editImageUrl}
          models={useChatStore.getState().models.filter((m) => m.supportImage)}
          onClose={() => setEditImageUrl(null)}
          onSubmit={async (image, mask, prompt, model) => {
            try {
              const result = await editImage(image, prompt, model, mask)
              if (result.data?.[0]?.content) {
                images.push(result.data[0].content)
                setLightboxIndex(images.length - 1)
                setLightboxOpen(true)
              }
            } catch {
              // 编辑失败静默处理
            }
            setEditImageUrl(null)
          }}
        />
      )}
    </div>
  )
}
