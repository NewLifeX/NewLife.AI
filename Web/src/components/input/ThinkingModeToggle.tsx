import { useState, useRef, useLayoutEffect } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

export type ThinkingMode = 'fast' | 'auto' | 'think'

interface ThinkingModeToggleProps {
  mode: ThinkingMode
  onChange: (mode: ThinkingMode) => void
  className?: string
}

const modeIcons: Record<ThinkingMode, string> = {
  fast: 'bolt',
  auto: 'psychology_alt',
  think: 'psychology',
}

const modes: ThinkingMode[] = ['fast', 'auto', 'think']

const POPUP_WIDTH = 224 // w-56 = 14rem

export function ThinkingModeToggle({ mode, onChange, className }: ThinkingModeToggleProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const [popupPos, setPopupPos] = useState<{ bottom: number; left: number } | null>(null)

  const modeConfig: Record<ThinkingMode, { label: string; description: string }> = {
    fast:  { label: t('thinking.fast'),  description: t('thinking.fastDesc')  },
    auto:  { label: t('thinking.auto'),  description: t('thinking.autoDesc')  },
    think: { label: t('thinking.think'), description: t('thinking.thinkDesc') },
  }

  const config = modeConfig[mode]
  const icon = modeIcons[mode]

  // 使用 portal 渲染弹出层，彻底脱离 backdrop-filter 等产生的 stacking context
  useLayoutEffect(() => {
    if (open && buttonRef.current) {
      const rect = buttonRef.current.getBoundingClientRect()
      setPopupPos({
        bottom: window.innerHeight - rect.top + 8,
        left: Math.max(8, Math.min(rect.left, window.innerWidth - POPUP_WIDTH - 8)),
      })
    } else {
      setPopupPos(null)
    }
  }, [open])

  return (
    <div className="relative">
      <button
        ref={buttonRef}
        onClick={() => setOpen(!open)}
        className={cn(
          'flex items-center space-x-1.5',
          'bg-[var(--color-surface-0)] border border-[var(--color-border-default)]',
          'rounded-full px-3 py-1.5 shadow-soft',
          'hover:border-[color:var(--color-brand-300)] transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
          className,
        )}
      >
        <Icon name={icon} variant="symbols" size="lg" className="text-blue-500" />
        <span className="text-xs font-medium text-[var(--color-text-primary)]">{config.label}</span>
        <Icon name={open ? 'keyboard_arrow_up' : 'keyboard_arrow_down'} size="base" className="text-[var(--color-text-tertiary)]" />
      </button>

      {open && popupPos && createPortal(
        <>
          <div className="fixed inset-0 z-[9998]" onClick={() => setOpen(false)} />
          <div
            className="fixed w-56 py-1 rounded-xl shadow-menu border border-[var(--color-border-subtle)] bg-[var(--color-surface-0)] animate-fade-in z-[9999]"
            style={{ bottom: popupPos.bottom, left: popupPos.left }}
          >
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
                      ? 'bg-[color:var(--color-brand-50)]'
                      : 'hover:bg-[var(--color-surface-2)]',
                  )}
                >
                  <Icon
                    name={modeIcons[m]}
                    variant="symbols"
                    size="lg"
                    className={cn('mt-0.5', active ? 'text-[color:var(--color-brand-500)]' : 'text-[var(--color-text-tertiary)]')}
                  />
                  <div className="flex-1 min-w-0">
                    <div className={cn(
                      'text-xs font-medium',
                      active ? 'text-[color:var(--color-brand-700)] dark:text-[color:var(--color-brand-400)]' : 'text-[var(--color-text-primary)]',
                    )}>
                      {c.label}
                      {active && <Icon name="check" size="sm" className="inline-block ml-1 text-blue-500" />}
                    </div>
                    <p className="text-[11px] text-[var(--color-text-tertiary)] leading-snug mt-0.5">{c.description}</p>
                  </div>
                </button>
              )
            })}
          </div>
        </>,
        document.body,
      )}
    </div>
  )
}
