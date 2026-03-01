import { useCallback, useEffect, useRef } from 'react'
import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'
import remarkMath from 'remark-math'
import rehypeHighlight from 'rehype-highlight'
import rehypeKatex from 'rehype-katex'
import 'katex/dist/katex.min.css'
import mermaid from 'mermaid'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

mermaid.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'loose' })

let mermaidCounter = 0

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
  return (
    <div className={cn('prose prose-sm dark:prose-invert max-w-none break-words', className)}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkMath]}
        rehypePlugins={[rehypeHighlight, rehypeKatex]}
        components={{
          pre({ children, ...props }) {
            const codeEl = Array.isArray(children)
              ? children.find((c: any) => c?.type === 'code' || c?.props?.className)
              : children
            const codeStr =
              typeof codeEl === 'object' && codeEl !== null && 'props' in (codeEl as any)
                ? String((codeEl as any).props.children ?? '')
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
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  )
}
