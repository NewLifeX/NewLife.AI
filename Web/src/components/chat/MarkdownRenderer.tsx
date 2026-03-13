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

function MermaidBlock({ code }: { code: string }) {
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!containerRef.current) return
    const id = `mermaid-${++mermaidCounter}`
    containerRef.current.innerHTML = ''
    mermaid.render(id, code).then(({ svg }) => {
      if (containerRef.current) containerRef.current.innerHTML = svg
    }).catch(() => {
      if (containerRef.current) containerRef.current.textContent = code
    })
  }, [code])

  return <div ref={containerRef} className="my-4 flex justify-center overflow-x-auto" />
}

interface MarkdownRendererProps {
  content: string
  className?: string
}

function CopyCodeButton({ code }: { code: string }) {
  const handleCopy = useCallback(() => {
    navigator.clipboard.writeText(code)
  }, [code])

  return (
    <button
      onClick={handleCopy}
      className="absolute top-2 right-2 p-1 rounded bg-gray-700/60 hover:bg-gray-600 text-gray-300 hover:text-white transition-colors opacity-0 group-hover/code:opacity-100"
      title="Copy"
    >
      <Icon name="content_copy" size="sm" />
    </button>
  )
}

export function MarkdownRenderer({ content, className }: MarkdownRendererProps) {
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

  const handleImageClick = useCallback(
    (src: string) => {
      const idx = images.indexOf(src)
      setLightboxIndex(idx >= 0 ? idx : 0)
      setLightboxOpen(true)
    },
    [images],
  )

  return (
    <div className={cn('prose prose-sm dark:prose-invert max-w-none break-words', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkMath]}
        rehypePlugins={[rehypeHighlight, rehypeKatex]}
        components={{
          pre({ children, ...props }) {
            const codeEl = Array.isArray(children)
              ? children.find((c) => isCodeLikeElement(c))
              : children
            const codeStr =
              typeof codeEl === 'object' && codeEl !== null && 'props' in codeEl
                ? String((codeEl as { props?: { children?: ReactNode } }).props?.children ?? '')
                : ''
            return (
              <div className="relative group/code">
                <pre
                  {...props}
                  className="rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 p-4 overflow-x-auto text-sm leading-relaxed"
                >
                  {children}
                </pre>
                {codeStr && <CopyCodeButton code={codeStr} />}
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
            // Mermaid code blocks
            if (codeClassName === 'language-mermaid') {
              const codeStr = String(children).replace(/\n$/, '')
              return <MermaidBlock code={codeStr} />
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
        {content}
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
          onClose={() => setEditImageUrl(null)}
          onSubmit={async (image, mask, prompt) => {
            try {
              const result = await editImage(image, prompt, 'dall-e-2', mask)
              if (result.data?.[0]?.content) {
                // 将编辑结果添加到 lightbox 图片列表并打开
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
