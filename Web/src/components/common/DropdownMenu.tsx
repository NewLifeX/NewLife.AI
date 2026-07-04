import { useState, useRef, useEffect, useCallback } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { Icon } from './Icon'

export interface DropdownMenuItem {
  id: string
  label: string
  icon?: string
  danger?: boolean
  disabled?: boolean
  children?: DropdownMenuItem[]
  onClick?: () => void
}

interface DropdownMenuProps {
  items: DropdownMenuItem[]
  trigger?: React.ReactNode
  className?: string
  align?: 'left' | 'right'
}

export function DropdownMenu({ items, trigger, className, align = 'right' }: DropdownMenuProps) {
  const [open, setOpen] = useState(false)
  const [menuPos, setMenuPos] = useState<{ left: number; top: number } | null>(null)
  const [activeSubmenu, setActiveSubmenu] = useState<string | null>(null)
  const [submenuAnchor, setSubmenuAnchor] = useState<DOMRect | null>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const submenuRef = useRef<HTMLDivElement>(null)
  const submenuTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const close = useCallback(() => {
    setOpen(false)
    setActiveSubmenu(null)
  }, [])

  useEffect(() => {
    if (!open) return
    const handler = (e: MouseEvent) => {
      const t = e.target as Node
      if (
        !triggerRef.current?.contains(t) &&
        !menuRef.current?.contains(t) &&
        !submenuRef.current?.contains(t)
      ) {
        close()
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open, close])

  useEffect(() => {
    if (!open && submenuTimerRef.current) {
      clearTimeout(submenuTimerRef.current)
      submenuTimerRef.current = null
    }
  }, [open])

  const handleTriggerClick = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation()
      if (open) { close(); return }
      const rect = triggerRef.current?.getBoundingClientRect()
      if (!rect) return
      const menuW = 164
      const viewW = window.innerWidth
      const viewH = window.innerHeight
      const estH = items.length * 34 + 8
      const top =
        viewH - rect.bottom - 4 >= estH
          ? rect.bottom + 2
          : Math.max(8, rect.top - estH - 2)
      let left = align === 'right' ? rect.right - menuW : rect.left
      left = Math.max(8, Math.min(left, viewW - menuW - 8))
      setMenuPos({ left, top })
      setOpen(true)
    },
    [open, close, align, items.length],
  )

  const handleItemEnter = useCallback((item: DropdownMenuItem, e: React.MouseEvent<HTMLDivElement>) => {
    if (submenuTimerRef.current) { clearTimeout(submenuTimerRef.current); submenuTimerRef.current = null }
    if (item.children) {
      setActiveSubmenu(item.id)
      setSubmenuAnchor((e.currentTarget as HTMLDivElement).getBoundingClientRect())
    } else if (activeSubmenu !== null) {
      setActiveSubmenu(null)
    }
  }, [activeSubmenu])

  const handleItemLeave = useCallback((item: DropdownMenuItem) => {
    if (item.children) {
      submenuTimerRef.current = setTimeout(() => setActiveSubmenu(null), 120)
    }
  }, [])

  const activeItem = items.find((i) => i.id === activeSubmenu)

  return (
    <div className={cn('relative inline-block', className)}>
      <button
        ref={triggerRef}
        onClick={handleTriggerClick}
        className="p-0.5 rounded text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] hover:bg-[var(--color-surface-2)] focus:outline-none"
      >
        {trigger ?? <Icon name="more_horiz" size="sm" />}
      </button>

      {open &&
        menuPos &&
        createPortal(
          <div
            ref={menuRef}
            style={{ position: 'fixed', left: menuPos.left, top: menuPos.top, zIndex: 9999, minWidth: 160 }}
            className="py-1 rounded-xl glass-panel shadow-menu animate-scale-in origin-top-right"
          >
            {items.map((item) => (
              <div
                key={item.id}
                className="relative"
                onMouseEnter={(e) => handleItemEnter(item, e)}
                onMouseLeave={() => handleItemLeave(item)}
              >
                <button
                  disabled={item.disabled}
                  onClick={(e) => {
                    if (item.children) { e.stopPropagation(); return }
                    item.onClick?.()
                    close()
                  }}
                  className={cn(
                    'flex items-center gap-2 w-full px-3 py-1.5 text-sm text-left rounded-md mx-1',
                    item.danger
                      ? 'text-red-500 hover:bg-red-50 dark:hover:bg-red-900/25'
                      : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)] hover:bg-[var(--color-surface-2)]',
                    item.disabled && 'opacity-40 cursor-not-allowed',
                  )}
                >
                  {item.icon && <Icon name={item.icon} size="sm" />}
                  <span className="flex-1">{item.label}</span>
                  {item.children && <Icon name="chevron_right" size="xs" className="text-[var(--color-text-tertiary)]" />}
                </button>
              </div>
            ))}
          </div>,
          document.body,
        )}

      {open &&
        activeItem?.children &&
        submenuAnchor &&
        createPortal(
          <div
            ref={submenuRef}
            style={{
              position: 'fixed',
              left: Math.min(submenuAnchor.right + 4, window.innerWidth - 168),
              top: submenuAnchor.top,
              zIndex: 10000,
              minWidth: 160,
            }}
            className="py-1 rounded-xl glass-panel shadow-menu animate-scale-in origin-top-left"
            onMouseEnter={() => {
              if (submenuTimerRef.current) { clearTimeout(submenuTimerRef.current); submenuTimerRef.current = null }
            }}
            onMouseLeave={() => setActiveSubmenu(null)}
          >
            {activeItem.children.map((sub) => (
              <button
                key={sub.id}
                disabled={sub.disabled}
                onClick={() => { sub.onClick?.(); close() }}
                className={cn(
                  'flex items-center gap-2 w-full px-3 py-1.5 text-sm text-left transition-colors rounded-md mx-1',
                  sub.danger
                    ? 'text-red-500 hover:bg-red-50 dark:hover:bg-red-900/25'
                    : 'text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)] hover:bg-[var(--color-surface-2)]',
                  sub.disabled && 'opacity-40 cursor-not-allowed',
                )}
              >
                {sub.icon && <Icon name={sub.icon} size="sm" />}
                <span>{sub.label}</span>
              </button>
            ))}
          </div>,
          document.body,
        )}
    </div>
  )
}
