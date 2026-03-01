import { useState } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface ToolCallBadgeProps {
  name: string
  status: 'calling' | 'done' | 'error'
  arguments?: string
  result?: string
  className?: string
}

export function ToolCallBadge({ name, status, arguments: args, result, className }: ToolCallBadgeProps) {
  const [expanded, setExpanded] = useState(false)
  const hasDetails = Boolean(args || result)

  return (
    <div className={cn('w-full', className)}>
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
        {hasDetails && (
          <Icon name={expanded ? 'expand_less' : 'expand_more'} variant="outlined" size="xs" />
        )}
      </button>

      {expanded && hasDetails && (
        <div className="mt-2 rounded-lg bg-gray-900 dark:bg-gray-950 text-gray-100 text-xs font-mono overflow-hidden">
          {args && (
            <div className="px-3 py-2 border-b border-gray-700/50">
              <div className="text-gray-400 mb-1 text-[10px] uppercase tracking-wider">Arguments</div>
              <pre className="whitespace-pre-wrap break-words leading-relaxed">{formatJson(args)}</pre>
            </div>
          )}
          {result && (
            <div className="px-3 py-2">
              <div className="text-gray-400 mb-1 text-[10px] uppercase tracking-wider">Result</div>
              <pre className="whitespace-pre-wrap break-words leading-relaxed max-h-48 overflow-y-auto custom-scrollbar">{formatJson(result)}</pre>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function formatJson(str: string): string {
  try {
    return JSON.stringify(JSON.parse(str), null, 2)
  } catch {
    return str
  }
}
