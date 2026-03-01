import { useState, useRef, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

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

const modes: ThinkingMode[] = ['fast', 'balanced', 'deep']

export function ThinkingModeToggle({ mode, onChange, className }: ThinkingModeToggleProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  const modeConfig: Record<ThinkingMode, { label: string; description: string }> = {
    fast: { label: t('thinking.fast'), description: t('thinking.fastDesc') },
    balanced: { label: t('thinking.balanced'), description: t('thinking.balancedDesc') },
    deep: { label: t('thinking.deep'), description: t('thinking.deepDesc') },
  }

  const config = modeConfig[mode]
  const icon = modeIcons[mode]

  useEffect(() => {
    if (!open) return
    const handleClick = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  return (
    <div ref={containerRef} className="relative flex flex-col items-start">
      <button
        onClick={() => setOpen(!open)}
        className={cn(
          'flex items-center space-x-1.5',
          'bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700',
          'rounded-full px-3 py-1.5 shadow-sm',
          'hover:border-blue-400 dark:hover:border-blue-500 transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
          className,
        )}
      >
        <Icon name={icon} variant="symbols" size="lg" className="text-blue-500" />
        <span className="text-xs font-medium text-gray-700 dark:text-gray-200">{config.label}</span>
        <Icon name="keyboard_arrow_down" size="base" className="text-gray-400" />
      </button>

      {open && (
        <div className="mt-2 w-56 py-1 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 animate-fade-in">
          {modes.map((m) => {
            const c = modeConfig[m]
            const active = m === mode
            return (
              <button
                key={m}
                onClick={() => { onChange(m); setOpen(false) }}
                className={cn(
                  'w-full flex items-start gap-3 px-3 py-2.5 text-left transition-colors',
                  active
                    ? 'bg-blue-50 dark:bg-blue-900/30'
                    : 'hover:bg-gray-50 dark:hover:bg-gray-700/50',
                )}
              >
                <Icon
                  name={modeIcons[m]}
                  variant="symbols"
                  size="lg"
                  className={cn('mt-0.5', active ? 'text-blue-500' : 'text-gray-400 dark:text-gray-500')}
                />
                <div className="flex-1 min-w-0">
                  <div className={cn(
                    'text-xs font-medium',
                    active ? 'text-blue-600 dark:text-blue-400' : 'text-gray-700 dark:text-gray-200',
                  )}>
                    {c.label}
                    {active && <Icon name="check" size="sm" className="inline-block ml-1 text-blue-500" />}
                  </div>
                  <p className="text-[11px] text-gray-400 dark:text-gray-500 leading-snug mt-0.5">{c.description}</p>
                </div>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}
