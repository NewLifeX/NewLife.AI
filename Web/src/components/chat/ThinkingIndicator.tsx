import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface ThinkingIndicatorProps {
  label?: string
  className?: string
}

export function ThinkingIndicator({
  label = 'Deep Thinking...',
  className,
}: ThinkingIndicatorProps) {
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
      <span className="animate-pulse">{label}</span>
    </div>
  )
}
