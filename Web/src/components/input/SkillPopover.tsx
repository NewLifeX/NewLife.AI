import { useLayoutEffect, useState, type RefObject } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface SkillOption {
  id: string
  icon: string
  iconBg: string
  iconColor: string
  label: string
  description: string
  active?: boolean
}

interface SkillPopoverProps {
  open: boolean
  onSelect: (id: string) => void
  onClose: () => void
  options?: SkillOption[]
  anchorRef?: RefObject<HTMLElement | null>
  className?: string
}

function useDefaultOptions(): SkillOption[] {
  const { t } = useTranslation()
  return [
    { id: 'mcp', icon: 'hub', iconBg: 'bg-blue-100 dark:bg-blue-900/40', iconColor: 'text-primary dark:text-blue-400', label: t('skills.mcp'), description: t('skills.mcpDesc') },
    { id: 'search', icon: 'travel_explore', iconBg: 'bg-purple-100 dark:bg-purple-900/40', iconColor: 'text-purple-600 dark:text-purple-400', label: t('skills.search'), description: t('skills.searchDesc') },
    { id: 'image', icon: 'palette', iconBg: 'bg-pink-100 dark:bg-pink-900/40', iconColor: 'text-pink-600 dark:text-pink-400', label: t('skills.imageGen'), description: t('skills.imageGenDesc') },
    { id: 'data', icon: 'analytics', iconBg: 'bg-green-100 dark:bg-green-900/40', iconColor: 'text-green-600 dark:text-green-400', label: t('skills.dataAnalysis'), description: t('skills.dataAnalysisDesc') },
  ]
}

export function SkillPopover({
  open,
  onSelect,
  onClose,
  options,
  anchorRef,
  className,
}: SkillPopoverProps) {
  const { t } = useTranslation()
  const defaultOptions = useDefaultOptions()
  const resolved = options ?? defaultOptions

  // 通过 anchorRef 计算弹出位置，使用 portal 渲染到 document.body
  // 彻底脱离父级 backdrop-filter 容器，确保 fixed 定位相对视口
  const MENU_WIDTH = 288 // w-72
  const [pos, setPos] = useState<{ bottom: number; left: number } | null>(null)

  useLayoutEffect(() => {
    if (open && anchorRef?.current) {
      const rect = anchorRef.current.getBoundingClientRect()
      setPos({
        bottom: window.innerHeight - rect.top + 8,
        left: Math.max(8, Math.min(rect.left, window.innerWidth - MENU_WIDTH - 8)),
      })
    } else {
      setPos(null)
    }
  }, [open, anchorRef])

  if (!open || pos === null) return null

  return createPortal(
    <>
      <div className="fixed inset-0 z-[9998]" onClick={onClose} />
      <div
        className={cn(
          'fixed w-72',
          'bg-[var(--color-surface-0)] rounded-2xl shadow-menu',
          'border border-[var(--color-border-subtle)] overflow-hidden z-[9999]',
          'animate-slide-up',
          className,
        )}
        style={{ bottom: pos.bottom, left: pos.left }}
      >
        <div className="px-3 py-2 bg-[var(--color-surface-1)] border-b border-[var(--color-border-subtle)] flex items-center justify-between">
          <span className="text-xs font-semibold text-[var(--color-text-secondary)] uppercase tracking-wider">
            {t('skills.title')}
          </span>
          <span className="text-[10px] bg-[var(--color-surface-2)] text-[var(--color-text-secondary)] px-1.5 py-0.5 rounded">
            {t('skills.escClose')}
          </span>
        </div>
        <div className="p-1.5 max-h-80 overflow-y-auto">
          {resolved.map((opt) => (
            <button
              key={opt.id}
              onClick={() => onSelect(opt.id)}
              className={cn(
                'w-full flex items-center p-2 rounded-xl group transition-colors text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/50',
                opt.active
                  ? 'bg-[color:var(--color-brand-50)]'
                  : 'hover:bg-[color:var(--color-brand-50)]',
              )}
            >
              <div className={cn('w-9 h-9 rounded-lg flex items-center justify-center mr-3 flex-shrink-0', opt.iconBg)}>
                <Icon name={opt.icon} className={opt.iconColor} />
              </div>
              <div className="flex-1 min-w-0">
                <div className={cn(
                  'text-sm font-medium transition-colors',
                  opt.active
                    ? 'text-primary'
                    : 'text-gray-800 dark:text-gray-200 group-hover:text-primary',
                )}>
                  {opt.label}
                </div>
                <div className="text-xs text-gray-500 dark:text-gray-400">
                  {opt.description}
                </div>
              </div>
              {opt.active && (
                <Icon name="check" size="base" className="text-primary flex-shrink-0 ml-2" />
              )}
            </button>
          ))}
        </div>
      </div>
    </>,
    document.body,
  )
}
