import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type ComponentPropsWithoutRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import remarkMath from 'remark-math'
import rehypeHighlight from 'rehype-highlight'
import rehypeKatex from 'rehype-katex'
import 'katex/dist/katex.min.css'
import mermaid from 'mermaid'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Lightbox } from '@/components/common/Lightbox'
import { ImageEditDialog } from '@/components/chat/ImageEditDialog'
import { ProgressiveImage } from '@/components/chat/ProgressiveImage'
import { resolveRenderableMermaidCode } from '@/components/chat/mermaidHelper'
import { useChatStore } from '@/stores/chatStore'
import { editImage } from '@/lib/api'

mermaid.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'loose' })

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
        'flex h-8 w-8 items-center justify-center rounded-lg border border-gray-700/70 bg-gray-900/90 text-gray-300 shadow-sm transition hover:bg-gray-800 hover:text-white',
        disabled && 'cursor-not-allowed opacity-40 hover:bg-gray-900/90 hover:text-gray-300',
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
  fallbackClassName = 'rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 p-4 overflow-x-auto text-sm leading-relaxed',
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

      const { svg } = await mermaid.render(id, renderableCode)
      if (!cancelled && containerRef.current === container) {
        container.innerHTML = svg
        const svgEl = container.querySelector('svg')
        if (svgEl instanceof SVGSVGElement) {
          if (expand) {
            svgEl.style.width = '100%'
          } else {
            svgEl.style.maxWidth = '100%'
          }
          svgEl.style.height = 'auto'
        }
        onSvgChange?.(svg)
      }
      cleanupBodyById()
    })().catch(() => {
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
    <div data-testid="mermaid-preview-dialog" className="fixed inset-0 z-[80] bg-black/75 backdrop-blur-sm" onClick={onClose}>
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
          <div className="mx-auto flex w-full min-h-full items-start justify-center">
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

  const fallbackClassName = 'rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 p-4 overflow-x-auto text-sm leading-relaxed'

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
      <div data-testid="mermaid-block" className="group/mermaid relative my-4 overflow-x-auto rounded-xl border border-gray-700/70 bg-gray-950/70">
        <div className="absolute right-2 top-2 z-10 flex items-center gap-1">
          <MermaidActionButton title={t('mermaid.enlarge')} icon="open_in_full" onClick={handleOpenPreview} disabled={!svgMarkup} testId="mermaid-open-preview" />
          <MermaidActionButton title={t('mermaid.downloadSvg')} icon="download" onClick={handleDownloadSvg} disabled={!svgMarkup} testId="mermaid-download-svg" />
          <MermaidActionButton title={t('mermaid.copySource')} icon="content_copy" onClick={handleCopySource} testId="mermaid-copy-source" />
        </div>

        <MermaidSvgPane
          code={code}
          testId="mermaid-inline-pane"
          className="flex justify-center overflow-x-auto p-4 pt-12"
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

function CopyCodeButton({ code }: { code: string }) {
  const handleCopy = useCallback(() => {
    void navigator.clipboard.writeText(code)
  }, [code])

  return (
    <button
      type="button"
      onClick={handleCopy}
      className="p-1 rounded bg-gray-700/60 hover:bg-gray-600 text-gray-300 hover:text-white transition-colors opacity-0 group-hover/code:opacity-100"
      title="Copy"
    >
      <Icon name="content_copy" size="sm" />
    </button>
  )
}

const CODE_COLLAPSE_THRESHOLD = 20

interface CollapsibleCodeBlockProps extends ComponentPropsWithoutRef<'pre'> {
  codeStr: string
  lang: string
  children: ReactNode
}

function CollapsibleCodeBlock({ codeStr, lang: _lang, children, ...props }: CollapsibleCodeBlockProps) {
  const { t } = useTranslation()
  const preRef = useRef<HTMLPreElement>(null)

  // effectiveCode：优先用 prop 传入的 codeStr（来自 hast AST），兜底用 DOM 文本内容
  const [effectiveCode, setEffectiveCode] = useState(codeStr)
  const [lineCount, setLineCount] = useState(() => {
    const text = codeStr.endsWith('\n') ? codeStr.slice(0, -1) : codeStr
    return text ? text.split('\n').length : 0
  })
  const shouldCollapse = lineCount > CODE_COLLAPSE_THRESHOLD
  const [collapsed, setCollapsed] = useState(shouldCollapse)

  // codeStr 变化时同步（流式写入场景）
  useEffect(() => {
    if (codeStr) {
      setEffectiveCode(codeStr)
      const text = codeStr.endsWith('\n') ? codeStr.slice(0, -1) : codeStr
      setLineCount(text ? text.split('\n').length : 0)
    }
  }, [codeStr])

  // codeStr 为空时从 DOM 兜底测量；useLayoutEffect 在浏览器绘制前同步运行，无闪烁
  useLayoutEffect(() => {
    if (!codeStr && preRef.current) {
      const domText = preRef.current.textContent ?? ''
      const text = domText.endsWith('\n') ? domText.slice(0, -1) : domText
      const count = text ? text.split('\n').length : 0
      if (count > 0) {
        setEffectiveCode(domText)
        setLineCount(count)
        if (count > CODE_COLLAPSE_THRESHOLD) setCollapsed(true)
      }
    }
  }, [codeStr])

  // 流式写入时若行数刚越过阈值则自动折叠，否则保持用户选择
  const prevShouldCollapse = useRef(shouldCollapse)
  useEffect(() => {
    if (!prevShouldCollapse.current && shouldCollapse) {
      setCollapsed(true)
    }
    prevShouldCollapse.current = shouldCollapse
  }, [shouldCollapse])

  return (
    <div className="relative group/code">
      <div className={cn(shouldCollapse && collapsed ? 'max-h-[17rem] overflow-hidden relative' : 'relative')}>
        <pre
          ref={preRef}
          {...props}
          className={cn(
            'bg-gray-900 dark:bg-gray-950 text-gray-100 p-4 overflow-x-auto text-sm leading-relaxed',
            shouldCollapse ? 'rounded-t-lg' : 'rounded-lg',
          )}
        >
          {children}
        </pre>
        {shouldCollapse && collapsed && (
          <div className="absolute bottom-0 inset-x-0 h-16 bg-gradient-to-t from-gray-900 dark:from-gray-950 to-transparent pointer-events-none" />
        )}
      </div>
      <div className="absolute top-2 right-2 flex items-center gap-1 z-10">
        {effectiveCode && <CopyCodeButton code={effectiveCode} />}
      </div>
      {shouldCollapse && (
        <button
          type="button"
          onClick={() => setCollapsed((v) => !v)}
          className="w-full flex items-center justify-center gap-1 py-1.5 rounded-b-lg bg-gray-800 dark:bg-gray-900 text-xs text-gray-400 hover:text-gray-200 hover:bg-gray-700 dark:hover:bg-gray-800 transition-colors border-t border-gray-700/40"
        >
          <Icon name={collapsed ? 'expand_more' : 'expand_less'} size="sm" />
          {collapsed
            ? t('chat.codeExpandAll', { lines: lineCount })
            : t('chat.codeCollapse')}
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

  const processedContent = useMemo(() => preprocessMath(content), [content])

  const handleImageClick = useCallback(
    (src: string) => {
      const idx = images.indexOf(src)
      setLightboxIndex(idx >= 0 ? idx : 0)
      setLightboxOpen(true)
    },
    [images],
  )

  // 将 components 提取为 useMemo，避免每次渲染都生成新函数引用，
  // 防止 ReactMarkdown 把 pre/code 视为新组件类型而卸载/重挂载（导致折叠状态丢失）
  const markdownComponents = useMemo(
    () => ({
      pre({ children, node: preNode, ...props }: ComponentPropsWithoutRef<'pre'> & { node?: { children?: HastChild[] } }) {
        // 直接从 hast 节点提取原始文本，比从 React children 解析更可靠
        const codeHastNode = preNode?.children?.[0]
        const codeStr = codeHastNode ? hastToText(codeHastNode) : extractText(children)
        const classNames: string[] = (codeHastNode?.properties?.className as string[] | undefined) ?? []
        const lang = classNames.find((c) => c.startsWith('language-'))?.replace('language-', '') ?? ''

        if (lang === 'mermaid') {
          return <MermaidBlock code={codeStr.replace(/\n$/, '')} isStreaming={isStreaming} />
        }

        return (
          <CollapsibleCodeBlock codeStr={codeStr} lang={lang} {...props}>
            {children}
          </CollapsibleCodeBlock>
        )
      },
      code({ className: codeClassName, children, ...props }: ComponentPropsWithoutRef<'code'>) {
        const isInline = !codeClassName
        if (isInline) {
          return (
            <code
              className="bg-gray-100 dark:bg-gray-800 text-primary dark:text-blue-400 px-1.5 py-0.5 rounded text-sm font-mono"
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
        return (
          <a
            href={href}
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
          <div className="overflow-x-auto my-4">
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
    }),
    [isStreaming, handleImageClick],
  )

  return (
    <div className={cn('prose dark:prose-invert max-w-none break-words', isStreaming && 'streaming-prose', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkMath, remarkGfm]}
        rehypePlugins={[[rehypeHighlight, { plainText: ['mermaid'] }], [rehypeKatex, { throwOnError: false, strict: false }]]}
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
            } catch (e) {
              console.error('Image edit failed:', e)
            }
            setEditImageUrl(null)
          }}
        />
      )}
    </div>
  )
}
