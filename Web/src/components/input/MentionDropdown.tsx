import { useState, useEffect, useRef, useCallback, type KeyboardEvent as ReactKeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import type { SkillInfo } from '@/lib/api'

interface MentionDropdownProps {
  open: boolean
  skills: SkillInfo[]
  keyword: string
  onSelect: (skill: SkillInfo) => void
  onClose: () => void
  className?: string
}

export function MentionDropdown({
  open,
  skills,
  keyword,
  onSelect,
  onClose,
  className,
}: MentionDropdownProps) {
  const { t } = useTranslation()
  const [activeIndex, setActiveIndex] = useState(0)
  const listRef = useRef<HTMLDivElement>(null)

  // 前端二次过滤
  const filtered = keyword
    ? skills.filter(
        (s) =>
          s.code.toLowerCase().includes(keyword.toLowerCase()) ||
          s.name.toLowerCase().includes(keyword.toLowerCase()),
      )
    : skills

  // keyword 或 skills 变化时重置高亮
  useEffect(() => {
    setActiveIndex(0)
  }, [keyword, skills.length])

  // 高亮项滚动到可见区域
  useEffect(() => {
    if (!listRef.current) return
    const item = listRef.current.children[activeIndex] as HTMLElement | undefined
    item?.scrollIntoView({ block: 'nearest' })
  }, [activeIndex])

  /** 供父组件通过 ref 或直接调用处理键盘事件 */
  const handleKeyDown = useCallback(
    (e: ReactKeyboardEvent | globalThis.KeyboardEvent) => {
      if (!open || filtered.length === 0) return false
      if (e.key === 'ArrowDown') {
        e.preventDefault()
        setActiveIndex((prev) => (prev + 1) % filtered.length)
        return true
      }
      if (e.key === 'ArrowUp') {
        e.preventDefault()
        setActiveIndex((prev) => (prev - 1 + filtered.length) % filtered.length)
        return true
      }
      if (e.key === 'Enter' || e.key === 'Tab') {
        e.preventDefault()
        onSelect(filtered[activeIndex])
        return true
      }
      if (e.key === 'Escape') {
        e.preventDefault()
        onClose()
        return true
      }
      return false
    },
    [open, filtered, activeIndex, onSelect, onClose],
  )

  // 将 handleKeyDown 挂到组件实例上供父组件调用
  MentionDropdown.handleKeyDown = handleKeyDown

  if (!open) return null

  return (
    <>
      <div className="fixed inset-0 z-40" onClick={onClose} />
      <div
        data-testid="mention-dropdown"
        className={cn(
          'absolute bottom-full mb-3 left-0 w-80',
          'bg-[var(--color-surface-0)] rounded-2xl shadow-menu',
          'border border-[var(--color-border-subtle)] overflow-hidden z-50',
          'animate-slide-up',
          className,
        )}
      >
        <div className="px-3 py-2 bg-[var(--color-surface-1)] border-b border-[var(--color-border-subtle)] flex items-center justify-between">
          <span className="text-xs font-semibold text-[var(--color-text-secondary)] uppercase tracking-wider">
            {t('skills.title')}
          </span>
          <span className="text-[10px] bg-[var(--color-surface-2)] text-[var(--color-text-secondary)] px-1.5 py-0.5 rounded">
            {t('skills.escClose')}
          </span>
        </div>
        <div ref={listRef} className="p-1.5 max-h-80 overflow-y-auto">
          {filtered.length === 0 ? (
            <div className="px-3 py-4 text-center text-sm text-[var(--color-text-tertiary)]">
              {t('skills.noMatch', '无匹配技能')}
            </div>
          ) : (
            filtered.map((skill, idx) => {
              const isTool = skill.type === 'tool'
              return (
              <button
                key={isTool ? `tool-${skill.code}` : skill.id}
                onClick={() => onSelect(skill)}
                onMouseEnter={() => setActiveIndex(idx)}
                className={cn(
                  'w-full flex items-center p-2 rounded-xl group transition-colors text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/50',
                  idx === activeIndex
                    ? 'bg-[color:var(--color-brand-50)]'
                    : 'hover:bg-[color:var(--color-brand-50)]',
                )}
              >
                <div className={cn(
                  'w-9 h-9 rounded-lg flex items-center justify-center mr-3 flex-shrink-0',
                  isTool
                    ? 'bg-amber-100 dark:bg-amber-900/40'
                    : 'bg-blue-100 dark:bg-blue-900/40',
                )}>
                  <Icon
                    name={skill.icon || (isTool ? 'build' : 'smart_toy')}
                    className={isTool ? 'text-amber-600 dark:text-amber-400' : 'text-primary dark:text-blue-400'}
                  />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-1.5">
                    <span
                      className={cn(
                        'text-sm font-medium transition-colors',
                        idx === activeIndex
                          ? 'text-primary'
                          : 'text-gray-800 dark:text-gray-200 group-hover:text-primary',
                      )}
                    >
                      {skill.name}
                    </span>
                    {isTool && (
                      <span className="text-[10px] px-1 py-0.5 rounded bg-amber-100 dark:bg-amber-900/30 text-amber-600 dark:text-amber-400 leading-none">
                        {t('skills.tool', '工具')}
                      </span>
                    )}
                  </div>
                  {skill.description && (
                    <div className="text-xs text-gray-500 dark:text-gray-400 truncate">
                      {skill.description}
                    </div>
                  )}
                </div>
              </button>
              )
            })
          )}
        </div>
      </div>
    </>
  )
}

/** 键盘事件处理函数引用，供父组件在 onKeyDown 中调用 */
MentionDropdown.handleKeyDown = ((_e: ReactKeyboardEvent | globalThis.KeyboardEvent): boolean => false) as (
  e: ReactKeyboardEvent | globalThis.KeyboardEvent,
) => boolean
