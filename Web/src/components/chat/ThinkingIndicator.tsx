import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface ThinkingIndicatorProps {
  label?: string
  className?: string
}

export function ThinkingIndicator({
  label,
  className,
}: ThinkingIndicatorProps) {
  const { t } = useTranslation()
  const resolvedLabel = label ?? t('chat.thinkingDeep')
  return (
    <div
      className={cn(
        'flex items-center space-x-2 text-xs font-medium',
        'text-blue-600 dark:text-blue-400',
        'bg-blue-50 dark:bg-blue-900/20',
        'w-fit px-3 py-1.5 rounded-lg select-none',
        className,
      )}
    >
      <Icon name="cyclone" variant="symbols" size="sm" className="animate-spin" />
      <span className="animate-pulse">{resolvedLabel}</span>
    </div>
  )
}
