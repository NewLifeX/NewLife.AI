import { useCallback, useEffect, useMemo, useRef, useState, type ReactElement, type ReactNode } from 'react'
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

type CodeLikeElement = ReactElement<{ className?: string; children?: ReactNode }> & { type?: unknown }

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

function isCodeLikeElement(value: unknown): value is CodeLikeElement {
  if (!value || typeof value !== 'object' || !('props' in value)) return false
  const element = value as CodeLikeElement
  const className = element.props?.className
  return element.type === 'code' || typeof className === 'string'
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
          svgEl.style.maxWidth = '100%'
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
          <div className="mx-auto flex min-h-full min-w-max items-start justify-center">
            <MermaidSvgPane
              code={code}
              testId="mermaid-preview-pane"
              className="rounded-2xl bg-white p-6 shadow-2xl"
              fallbackClassName={fallbackClassName}
              scale={scale}
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
      <div data-testid="mermaid-block" className="group/mermaid relative my-4 overflow-hidden rounded-xl border border-gray-700/70 bg-gray-950/70">
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

  return (
    <div className={cn('prose dark:prose-invert max-w-none break-words', isStreaming && 'streaming-prose', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkMath, remarkGfm]}
        rehypePlugins={[[rehypeHighlight, { plainText: ['mermaid'] }], [rehypeKatex, { throwOnError: false, strict: false }]]}
        components={{
          pre({ children, ...props }) {
            const codeEl = Array.isArray(children)
              ? children.find((c) => isCodeLikeElement(c))
              : children
            const rawChildren =
              typeof codeEl === 'object' && codeEl !== null && 'props' in codeEl
                ? (codeEl as { props?: { children?: ReactNode } }).props?.children
                : undefined
            const codeStr = extractText(rawChildren)
            const langClass =
              typeof codeEl === 'object' && codeEl !== null && 'props' in codeEl
                ? String((codeEl as { props?: { className?: string } }).props?.className ?? '')
                : ''
            const lang = langClass.split(/\s+/).find((c) => c.startsWith('language-'))?.replace('language-', '') ?? ''

            if (lang === 'mermaid') {
              return <MermaidBlock code={codeStr.replace(/\n$/, '')} isStreaming={isStreaming} />
            }

            return (
              <div className="relative group/code">
                <pre
                  {...props}
                  className="rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 p-4 overflow-x-auto text-sm leading-relaxed"
                >
                  {children}
                </pre>
                <div className="absolute top-2 right-2 flex items-center gap-1">
                  {codeStr && <CopyCodeButton code={codeStr} />}
                </div>
              </div>
            )
          },
          code({ className: codeClassName, children, ...props }) {
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
          a({ href, children, ...props }) {
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
          table({ children, ...props }) {
            return (
              <div className="overflow-x-auto my-4">
                <table className="border-collapse border border-gray-200 dark:border-gray-700 w-full text-sm" {...props}>
                  {children}
                </table>
              </div>
            )
          },
          th({ children, ...props }) {
            return (
              <th
                className="border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 px-3 py-2 text-left font-medium"
                {...props}
              >
                {children}
              </th>
            )
          },
          td({ children, ...props }) {
            return (
              <td className="border border-gray-200 dark:border-gray-700 px-3 py-2" {...props}>
                {children}
              </td>
            )
          },
          img({ src, alt }) {
            return (
              <ProgressiveImage
                src={src}
                alt={alt ?? ''}
                onClick={() => src && handleImageClick(src)}
              />
            )
          },
        }}
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
