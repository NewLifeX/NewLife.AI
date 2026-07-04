import { useState } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { ToolCallBadge } from './ToolCallBadge'
import type { ToolCall } from '@/types'

interface ToolCallGroupProps {
  /** 同名连续工具调用（已由调用方筛选，count >= 阈值） */
  calls: ToolCall[]
  /** 是否显示参数详情（沿用全局 showToolCalls 设置） */
  showDetails?: boolean
  className?: string
}

/**
 * 同类工具调用分组折叠卡片。当 AI 连续调用同一工具 3+ 次时（如多次 read_file/grep_search），
 * 默认折叠为一行摘要，展开后逐条显示，避免界面被工具徽章淹没。
 *
 * 同类操作分组模式：相邻同名工具调用达到阈值时折叠为组，减少界面噪声（详见 Doc/借鉴分析/ 6.3 节）。
 */
export function ToolCallGroup({ calls, showDetails, className }: ToolCallGroupProps) {
  const [expanded, setExpanded] = useState(false)
  if (calls.length === 0) return null
  const name = calls[0].name
  const running = calls.some(c => c.status === 'calling')
  const errors = calls.filter(c => c.status === 'error').length

  return (
    <div
      data-testid="tool-call-group"
      data-tool-name={name}
      data-tool-count={calls.length}
      className={cn(
        'my-1 rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50/60 dark:bg-gray-800/40 overflow-hidden',
        className,
      )}
    >
      <button
        type="button"
        onClick={() => setExpanded(v => !v)}
        className="w-full flex items-center gap-2 px-3 py-1.5 text-xs text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700/50 transition-colors"
      >
        <Icon name={expanded ? 'expand_more' : 'chevron_right'} size="sm" />
        <span className="font-mono font-medium">{name}</span>
        <span className="text-gray-400">×{calls.length}</span>
        {running && <span className="text-[color:var(--color-brand-500)] animate-pulse">运行中</span>}
        {errors > 0 && <span className="text-red-500">{errors} 失败</span>}
      </button>
      {expanded && (
        <div className="flex flex-col gap-1 px-3 py-2 border-t border-gray-200 dark:border-gray-700">
          {calls.map(tc => (
            <ToolCallBadge
              key={tc.id}
              name={tc.name}
              status={tc.status}
              arguments={tc.arguments}
              result={tc.result}
              showDetails={showDetails}
            />
          ))}
        </div>
      )}
    </div>
  )
}
