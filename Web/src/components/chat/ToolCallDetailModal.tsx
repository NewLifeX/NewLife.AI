import { useEffect, useState } from 'react'
import { Modal } from '@/components/common/Modal'
import { Icon } from '@/components/common/Icon'
import { formatToolCallJson } from './ToolCallBadge'
import { shikiHighlight, onShikiReady } from './MarkdownRenderer'

interface ToolCallDetailModalProps {
  open: boolean
  onClose: () => void
  name: string
  status: 'calling' | 'done' | 'error'
  arguments?: string
  result?: string
}

const STATUS_LABEL: Record<string, string> = {
  calling: '调用中',
  done: '已完成',
  error: '错误',
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)
  const handleCopy = async () => {
    await navigator.clipboard.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }
  return (
    <button
      onClick={handleCopy}
      title={copied ? '已复制' : '复制'}
      className="flex items-center justify-center w-6 h-6 rounded text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-3)] transition-colors"
    >
      <Icon name={copied ? 'check' : 'content_copy'} size="xs" />
    </button>
  )
}

function JsonCodeBlock({ text }: { text: string }) {
  const [html, setHtml] = useState<string | null>(() => shikiHighlight('json', formatToolCallJson(text)))
  useEffect(() => {
    return onShikiReady(() => setHtml(shikiHighlight('json', formatToolCallJson(text))))
  }, [text])
  if (html) {
    return <div className="shiki-wrapper rounded-lg overflow-hidden text-xs [&_pre]:whitespace-pre-wrap [&_pre]:break-all" dangerouslySetInnerHTML={{ __html: html }} />
  }
  return (
    <pre className="text-xs font-mono whitespace-pre-wrap break-words leading-relaxed rounded-lg p-4 bg-[var(--color-surface-1)] text-[var(--color-text-primary)]">
      {formatToolCallJson(text)}
    </pre>
  )
}

export function ToolCallDetailModal({ open, onClose, name, status, arguments: args, result }: ToolCallDetailModalProps) {
  return (
    <Modal open={open} onClose={onClose} maxWidth="max-w-4xl">
      <div className="flex flex-col w-full" style={{ minHeight: '50vh', maxHeight: '80vh' }}>
        {/* 标题栏 */}
        <div className="flex items-center gap-3 px-6 py-4 border-b border-[var(--color-border-subtle)] flex-shrink-0 pr-12">
          <span className="inline-flex items-center justify-center w-5 h-5 flex-shrink-0">
            {status === 'calling' && (
              <span className="relative flex h-2 w-2">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-green-400 opacity-75" />
                <span className="relative inline-flex rounded-full h-2 w-2 bg-green-500" />
              </span>
            )}
            {status === 'done' && (
              <Icon name="check_circle" variant="filled" size="sm" className="text-green-500" />
            )}
            {status === 'error' && (
              <Icon name="error" variant="filled" size="sm" className="text-red-500" />
            )}
          </span>
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)] font-mono truncate">{name}</h2>
          <span className="ml-auto text-xs text-[var(--color-text-tertiary)] uppercase tracking-wider flex-shrink-0">
            {STATUS_LABEL[status]}
          </span>
        </div>

        {/* 内容区（左右分栏，可独立滚动） */}
        <div className="flex flex-row flex-1 min-h-0 overflow-hidden">
          {args && (
            <div className={`flex flex-col overflow-hidden ${result ? 'flex-1 border-r border-[var(--color-border-subtle)]' : 'flex-1'}`}>
              <div className="px-6 pt-4 pb-2 flex-shrink-0 flex items-center justify-between">
                <div className="text-[10px] uppercase tracking-wider font-medium text-[var(--color-text-tertiary)]">
                  入参
                </div>
                <CopyButton text={formatToolCallJson(args)} />
              </div>
              <div className="flex-1 overflow-y-auto custom-scrollbar px-6 pb-4">
                <JsonCodeBlock text={args} />
              </div>
            </div>
          )}

          {result && (
            <div className="flex flex-col flex-1 overflow-hidden">
              <div className="px-6 pt-4 pb-2 flex-shrink-0 flex items-center justify-between">
                <div className="text-[10px] uppercase tracking-wider font-medium text-[var(--color-text-tertiary)]">
                  出参
                </div>
                <CopyButton text={formatToolCallJson(result)} />
              </div>
              <div className="flex-1 overflow-y-auto custom-scrollbar px-6 pb-4">
                <JsonCodeBlock text={result} />
              </div>
            </div>
          )}
        </div>
      </div>
    </Modal>
  )
}
