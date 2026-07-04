import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import type { ToolCall } from '@/types'

interface AgentTaskCardProps {
  toolCalls: ToolCall[]
  className?: string
}

const STEP_ICON: Record<string, string> = {
  done: 'check_circle',
  calling: 'pending',
  error: 'cancel',
}
const STEP_COLOR: Record<string, string> = {
  done: 'text-green-500 dark:text-green-400',
  calling: 'text-[color:var(--color-brand-500)] animate-pulse',
  error: 'text-red-500 dark:text-red-400',
}

/** Agent 任务进度卡片。当消息有 3 个以上不同名称的工具调用时，
 * 以折叠卡片形式展示任务步骤列表，取代冗长的单条 ToolCallBadge 列表 */
export function AgentTaskCard({ toolCalls, className }: AgentTaskCardProps) {
  const { t } = useTranslation()
  const [expanded, setExpanded] = useState(false)

  // 按顺序去重（同名连续调用折叠为一步，显示次数）
  const steps = buildSteps(toolCalls)

  const doneCount = steps.filter((s) => s.status === 'done').length
  const totalCount = steps.length
  const hasError = steps.some((s) => s.status === 'error')
  const allDone = doneCount === totalCount
  const inProgress = steps.some((s) => s.status === 'calling')

  return (
    <div className={cn('my-3 rounded-xl border bg-[var(--color-surface-1)] border-[var(--color-border-subtle)]', className)}>
      <button
        type="button"
        onClick={() => setExpanded((v) => !v)}
        className="w-full flex items-center gap-2 px-3 py-2.5 text-left hover:bg-[var(--color-surface-2)] rounded-xl transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-400)]/50"
      >
        {/* 状态图标 */}
        <Icon
          name={hasError ? 'error' : allDone ? 'task_alt' : 'play_circle'}
          size="base"
          className={cn(
            'flex-shrink-0',
            hasError ? 'text-red-500 dark:text-red-400'
              : allDone ? 'text-green-500 dark:text-green-400'
              : 'text-[color:var(--color-brand-500)]',
            inProgress && !allDone && 'animate-pulse',
          )}
        />
        <span className="flex-1 text-[13px] font-medium text-[var(--color-text-primary)]">
          {t('chat.agentTaskProgress', 'Agent 任务进度')}
        </span>
        <span className="text-[12px] text-[var(--color-text-tertiary)]">
          {doneCount}/{totalCount} {t('chat.steps', '步')}
        </span>
        <Icon
          name={expanded ? 'expand_less' : 'expand_more'}
          size="sm"
          className="flex-shrink-0 text-[var(--color-text-tertiary)]"
        />
      </button>

      {expanded && (
        <div className="px-3 pb-2.5 flex flex-col gap-1.5">
          {steps.map((step, i) => (
            <div key={i} className="flex items-start gap-2 py-1">
              <Icon
                name={STEP_ICON[step.status] ?? 'radio_button_unchecked'}
                size="sm"
                variant="filled"
                className={cn('flex-shrink-0 mt-0.5', STEP_COLOR[step.status] ?? 'text-[var(--color-text-tertiary)]')}
              />
              <span className="text-[12px] text-[var(--color-text-primary)] leading-snug flex-1 min-w-0">
                {step.displayName}
                {step.count > 1 && (
                  <span className="ml-1 text-[var(--color-text-tertiary)]">×{step.count}</span>
                )}
              </span>
              {step.status === 'error' && step.error && (
                <span className="text-[11px] text-red-500 dark:text-red-400 shrink-0 max-w-[120px] truncate" title={step.error}>
                  {step.error}
                </span>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

interface StepInfo {
  name: string
  displayName: string
  status: 'calling' | 'done' | 'error'
  count: number
  error?: string
}

/** 将工具调用列表压缩为步骤信息（连续同名合并） */
function buildSteps(toolCalls: ToolCall[]): StepInfo[] {
  const result: StepInfo[] = []
  for (const tc of toolCalls) {
    const last = result[result.length - 1]
    if (last && last.name === tc.name) {
      last.count++
      // 优先级：error > calling > done
      if (tc.status === 'error') { last.status = 'error'; last.error = tc.result }
      else if (tc.status === 'calling' && last.status !== 'error') last.status = 'calling'
    } else {
      result.push({
        name: tc.name,
        displayName: formatToolName(tc.name),
        status: tc.status,
        count: 1,
        error: tc.status === 'error' ? tc.result : undefined,
      })
    }
  }
  return result
}

/** 将工具名转换为可读显示名 */
function formatToolName(name: string): string {
  return name
    .replace(/_/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .trim()
}
