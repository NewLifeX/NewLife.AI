import { useState } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface ToolCallBadgeProps {
  name: string
  status: 'calling' | 'done' | 'error'
  arguments?: string
  result?: string
  showDetails?: boolean
  className?: string
}

export function formatToolCallJson(str: string): string {
  try {
    return JSON.stringify(JSON.parse(str), null, 2)
  } catch {
    return str
  }
}

function getArgCount(args?: string): number {
  if (!args) return 0
  try {
    const parsed = JSON.parse(args)
    if (parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed))
      return Object.keys(parsed).length
    return 1
  } catch {
    return 0
  }
}

function getResultSize(str?: string): string {
  if (!str) return ''
  const bytes = new TextEncoder().encode(str).length
  if (bytes < 1024) return `${bytes}B`
  return `${(bytes / 1024).toFixed(1)}KB`
}

export function ToolCallBadge({ name, status, arguments: args, result, showDetails, className }: ToolCallBadgeProps) {
  const [expanded, setExpanded] = useState(false)
  const hasDetails = showDetails && Boolean(args || result)

  const argCount = getArgCount(args)
  const resultSize = getResultSize(result)
  const hasSummary = hasDetails && (argCount > 0 || resultSize)

  return (
    <div data-testid="tool-call-badge" data-tool-name={name} className={cn('inline-flex', className)}>
      <button
        onClick={() => hasDetails && setExpanded((v) => !v)}
        className={cn(
          'flex items-center space-x-2 px-3 py-1.5 rounded-full text-xs font-medium border transition-colors',
          hasDetails && 'cursor-pointer',
          !hasDetails && 'cursor-default',
          status === 'calling' && 'bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300 border-green-100 dark:border-green-800/50',
          status === 'done' && 'bg-gray-50 dark:bg-gray-800 text-gray-600 dark:text-gray-300 border-gray-200 dark:border-gray-700',
          status === 'error' && 'bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-300 border-red-100 dark:border-red-800/50',
        )}
      >
        <span className="inline-flex items-center justify-center w-[14px] h-[14px]">
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
        <span>{name}</span>
        {hasSummary && (
          <span className="opacity-50 font-normal text-[10px]">
            {argCount > 0 ? `${argCount}p` : ''}{argCount > 0 && resultSize ? ' · ' : ''}{resultSize}
          </span>
        )}
        {hasDetails && (
          <Icon name={expanded ? 'expand_less' : 'expand_more'} variant="outlined" size="xs" />
        )}
      </button>

      {expanded && hasDetails && (
        <div className="mt-2 rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 text-xs font-mono overflow-hidden">
          {args && (
            <div className="px-3 py-2 border-b border-gray-700/50">
              <div className="text-gray-400 mb-1 text-[10px] uppercase tracking-wider">Arguments</div>
              <pre className="whitespace-pre-wrap break-words leading-relaxed max-h-40 overflow-y-auto custom-scrollbar">{formatToolCallJson(args)}</pre>
            </div>
          )}
          {result && (
            <div className="px-3 py-2">
              <div className="text-gray-400 mb-1 text-[10px] uppercase tracking-wider">Result</div>
              <pre className="whitespace-pre-wrap break-words leading-relaxed max-h-40 overflow-y-auto custom-scrollbar">{formatToolCallJson(result)}</pre>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
