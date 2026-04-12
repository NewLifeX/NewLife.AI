import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface ThinkingBlockProps {
  content: string
  isStreaming?: boolean
  thinkingTime?: number
  className?: string
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
}: ThinkingBlockProps) {
  const { t } = useTranslation()
  const [collapsed, setCollapsed] = useState(false)

  return (
    <div className={cn('mb-4', className)}>
      <button
        onClick={() => setCollapsed((v) => !v)}
        className="flex items-center space-x-2 text-xs font-medium text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/20 px-3 py-1.5 rounded-lg select-none hover:bg-blue-100 dark:hover:bg-blue-900/30 transition-colors w-fit"
      >
        {isStreaming ? (
          <>
            <Icon name="cyclone" variant="symbols" size="sm" className="animate-spin" />
            <span className="animate-pulse">{t('chat.thinkingInProgress')}</span>
            <LiveTimer />
          </>
        ) : (
          <>
            <Icon name="psychology" variant="outlined" size="sm" />
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
              className="text-blue-400"
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
