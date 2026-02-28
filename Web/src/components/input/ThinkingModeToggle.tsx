import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Tooltip } from '@/components/common/Tooltip'

export type ThinkingMode = 'fast' | 'balanced' | 'deep'

interface ThinkingModeToggleProps {
  mode: ThinkingMode
  onChange: (mode: ThinkingMode) => void
  className?: string
}

const modeIcons: Record<ThinkingMode, string> = {
  fast: 'bolt',
  balanced: 'psychology_alt',
  deep: 'psychology',
}

export function ThinkingModeToggle({ mode, onChange, className }: ThinkingModeToggleProps) {
  const { t } = useTranslation()

  const modeConfig: Record<ThinkingMode, { label: string; description: string }> = {
    fast: { label: t('thinking.fast'), description: t('thinking.fastDesc') },
    balanced: { label: t('thinking.balanced'), description: t('thinking.balancedDesc') },
    deep: { label: t('thinking.deep'), description: t('thinking.deepDesc') },
  }

  const config = modeConfig[mode]
  const icon = modeIcons[mode]

  const cycleMode = () => {
    const modes: ThinkingMode[] = ['fast', 'balanced', 'deep']
    const idx = modes.indexOf(mode)
    onChange(modes[(idx + 1) % modes.length])
  }

  return (
    <Tooltip
      position="top"
      content={
        <div className="max-w-[200px]">
          <div className="font-semibold mb-1 text-blue-300">{config.label} {t('thinking.modeEnabled')}</div>
          <p className="text-gray-300 leading-relaxed">{config.description}</p>
        </div>
      }
      className="whitespace-normal"
    >
      <button
        onClick={cycleMode}
        className={cn(
          'flex items-center space-x-1.5',
          'bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700',
          'rounded-full px-3 py-1.5 shadow-sm',
          'hover:border-blue-400 dark:hover:border-blue-500 transition-colors',
          className,
        )}
      >
        <Icon name={icon} variant="symbols" size="sm" className="text-blue-500 text-[18px]" />
        <span className="text-xs font-medium text-gray-700 dark:text-gray-200">{config.label}</span>
        <Icon name="keyboard_arrow_down" size="sm" className="text-gray-400 text-[16px]" />
      </button>
    </Tooltip>
  )
}
