import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface ThinkingBlockProps {
  content: string
  isStreaming?: boolean
  thinkingTime?: number
  className?: string
  defaultCollapsed?: boolean
  onCollapsedChange?: (collapsed: boolean) => void
}

/** 从思考内容中提取关键步骤名（最后一个加粗标题或最后段落首句） */
function extractKeyStep(content: string): string {
  if (!content) return ''
  // 提取所有 **text** 格式的加粗标题
  const boldMatches = [...content.matchAll(/\*\*([^*\n]{1,60})\*\*/g)]
  if (boldMatches.length > 0) {
    return boldMatches[boldMatches.length - 1][1].trim()
  }
  // 降级：取最后一个非空段落的首句（不超过 40 字）
  const paragraphs = content.split('\n').map((l) => l.trim()).filter(Boolean)
  if (paragraphs.length > 0) {
    const last = paragraphs[paragraphs.length - 1]
    return last.length > 40 ? last.slice(0, 40) + '…' : last
  }
  return ''
}

/** 流式推理时的实时计时器 */
function LiveTimer() {
  const [elapsed, setElapsed] = useState(0)
  const startRef = useRef(Date.now())

  useEffect(() => {
    const id = setInterval(() => setElapsed(Date.now() - startRef.current), 100)
    return () => clearInterval(id)
  }, [])

  return <span className="ml-1 tabular-nums opacity-70">({(elapsed / 1000).toFixed(1)}s)</span>
}

export function ThinkingBlock({
  content,
  isStreaming = false,
  thinkingTime,
  className,
  defaultCollapsed = false,
  onCollapsedChange,
}: ThinkingBlockProps) {
  const { t } = useTranslation()
  const [collapsed, setCollapsed] = useState(defaultCollapsed)

  const handleToggle = () => {
    const next = !collapsed
    setCollapsed(next)
    onCollapsedChange?.(next)
  }

  // 流式收缩时显示最新步骤名
  const streamingLabel = isStreaming && collapsed
    ? (extractKeyStep(content) || t('chat.thinkingInProgress'))
    : t('chat.thinkingInProgress')

  return (
    <div className={cn('mb-4', className)} data-testid="thinking-block">
      <button
        onClick={handleToggle}
        title={t('chat.thinkingToggleTip')}
        className="flex items-center space-x-2 text-xs font-medium text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/20 px-3 py-1.5 rounded-lg select-none hover:bg-blue-100 dark:hover:bg-blue-900/30 transition-colors w-fit max-w-full"
      >
        {isStreaming ? (
          <>
            <Icon name="cyclone" variant="symbols" size="sm" className="animate-spin flex-shrink-0" />
            <span className="animate-pulse max-w-[16rem] truncate">{streamingLabel}</span>
            <LiveTimer />
          </>
        ) : (
          <>
            <Icon name="psychology" variant="outlined" size="sm" className="flex-shrink-0" />
            <span>
              {t('chat.thinkingProcess')}
              {thinkingTime != null && thinkingTime > 0 && (
                <span className="ml-1 opacity-70">({(thinkingTime / 1000).toFixed(1)}s)</span>
              )}
            </span>
            <Icon
              name={collapsed ? 'expand_more' : 'expand_less'}
              variant="outlined"
              size="sm"
              className="text-blue-400 flex-shrink-0"
            />
          </>
        )}
      </button>

      {!collapsed && (
        <div className="mt-2 pl-3 border-l-2 border-blue-200 dark:border-blue-800">
          <div className="text-sm text-gray-500 dark:text-gray-400 italic leading-relaxed whitespace-pre-wrap">
            {content}
            {isStreaming && (
              <span className="inline-block w-1.5 h-4 bg-blue-400 ml-0.5 animate-pulse rounded-sm align-text-bottom" />
            )}
          </div>
        </div>
      )}
    </div>
  )
}
