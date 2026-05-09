import { useCallback, useEffect, useRef, useState, useMemo, type ReactNode, type ReactElement } from 'react'
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
import { isPreviewable } from '@/components/chat/ArtifactPanel'
import { resolveRenderableMermaidCode } from '@/components/chat/mermaidHelper'
import { useArtifactStore } from '@/stores'
import { useChatStore } from '@/stores/chatStore'
import { editImage } from '@/lib/api'

mermaid.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'loose' })

let mermaidCounter = 0

type CodeLikeElement = ReactElement<{ className?: string; children?: ReactNode }> & { type?: unknown }

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

function MermaidBlock({ code, isStreaming }: { code: string; isStreaming?: boolean }) {
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    // 流式输出期间代码块不完整，跳过渲染避免 mermaid 将错误 UI 注入 document.body
    if (isStreaming || !containerRef.current) return
    const container = containerRef.current
    container.innerHTML = ''

    const id = `mermaid-${++mermaidCounter}`
    let cancelled = false

    // 清理 mermaid 在 body 遗留的临时容器（#id 和 #d{id}）。
    // 注意：容器里的根 SVG 也会使用相同 id，不能删除它，否则 Mermaid 作用域样式全部失效，
    //       图形会退化成“黑乎乎”的默认样式。这里只清理容器外部的同名临时节点。
    const cleanupBodyById = () => {
      for (const targetId of [id, `d${id}`]) {
        document.querySelectorAll(`[id="${targetId}"]`).forEach((el) => {
          if (!container.contains(el)) el.remove()
        })
      }
    }

    const showFallback = () => {
      if (cancelled || containerRef.current !== container) return
      container.textContent = code
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
        const svgEl = container.querySelector(':scope > svg')
        if (svgEl instanceof SVGSVGElement) {
          svgEl.style.maxWidth = '100%'
          svgEl.style.height = 'auto'
        }
      }
      cleanupBodyById()
    })().catch(() => {
      // 错误时 mermaid 可能已将错误 UI（炸弹图标）写入 body，按 id 精确清除
      cleanupBodyById()
      showFallback()
    })

    // 组件卸载或 deps 变化时清理：
    // - 如果已成功渲染，SVG id 已删除，cleanupBodyById 是无操作（找不到任何元素）
    // - 如果 Promise 还未决议，将 cancelled 设为 true 并清理临时 body 元素
    return () => {
      cancelled = true
      cleanupBodyById()
    }
  }, [code, isStreaming])

  // 流式输出期间展示原始代码占位，避免调用 mermaid.render 导致错误 UI 注入页面
  if (isStreaming) {
    return (
      <pre className="my-4 rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 p-4 overflow-x-auto text-sm leading-relaxed">
        {code}
      </pre>
    )
  }

  return <div ref={containerRef} className="my-4 flex justify-center overflow-x-auto" />
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
    navigator.clipboard.writeText(code)
  }, [code])

  return (
    <button
      onClick={handleCopy}
      className="p-1 rounded bg-gray-700/60 hover:bg-gray-600 text-gray-300 hover:text-white transition-colors opacity-0 group-hover/code:opacity-100"
      title="Copy"
    >
      <Icon name="content_copy" size="sm" />
    </button>
  )
}

function PreviewCodeButton({ code, language }: { code: string; language: string }) {
  const open = useArtifactStore((s) => s.open)

  const handlePreview = useCallback(() => {
    open({ language, code, title: language.toUpperCase() })
  }, [open, language, code])

  return (
    <button
      onClick={handlePreview}
      className="p-1 rounded bg-gray-700/60 hover:bg-gray-600 text-gray-300 hover:text-white transition-colors opacity-0 group-hover/code:opacity-100"
      title="Preview"
    >
      <Icon name="visibility" size="sm" />
    </button>
  )
}

export function MarkdownRenderer({ content, isStreaming = false, className }: MarkdownRendererProps) {
  const [lightboxOpen, setLightboxOpen] = useState(false)
  const [lightboxIndex, setLightboxIndex] = useState(0)
  const [editImageUrl, setEditImageUrl] = useState<string | null>(null)

  // 从 Markdown 内容中提取所有图片 URL
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
            // 提取语言标识：className 可能是 'language-mermaid hljs' 等多个 class，取第一个 language-xxx
            const langClass =
              typeof codeEl === 'object' && codeEl !== null && 'props' in codeEl
                ? String((codeEl as { props?: { className?: string } }).props?.className ?? '')
                : ''
            const lang = langClass.split(/\s+/).find((c) => c.startsWith('language-'))?.replace('language-', '') ?? ''

            // Mermaid：直接渲染图形，不走代码块 UI（避免 div 嵌套在 pre 中导致非法 HTML）
            if (lang === 'mermaid') {
              return <MermaidBlock code={codeStr.replace(/\n$/, '')} isStreaming={isStreaming} />
            }

            const canPreview = isPreviewable(lang) && !!codeStr

            return (
              <div className="relative group/code">
                <pre
                  {...props}
                  className="rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 p-4 overflow-x-auto text-sm leading-relaxed"
                >
                  {children}
                </pre>
                <div className="absolute top-2 right-2 flex items-center gap-1">
                  {canPreview && <PreviewCodeButton code={codeStr} language={lang} />}
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
                // 将编辑结果添加到 lightbox 图片列表并打开
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
