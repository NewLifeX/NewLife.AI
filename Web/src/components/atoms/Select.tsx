import { useState, useRef, useEffect, useCallback, type KeyboardEvent } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

export interface SelectOption {
  value: string
  label: string
  icon?: string
  description?: string
}

interface SelectProps {
  options: SelectOption[]
  value: string
  onChange: (value: string) => void
  placeholder?: string
  className?: string
  disabled?: boolean
}

export function Select({
  options,
  value,
  onChange,
  placeholder,
  className,
  disabled = false,
}: SelectProps) {
  const [open, setOpen] = useState(false)
  const [focusedIndex, setFocusedIndex] = useState(-1)
  const [openUpward, setOpenUpward] = useState(false)
  const [dropdownPos, setDropdownPos] = useState({ left: 0, top: 0, width: 0 })
  const containerRef = useRef<HTMLDivElement>(null)
  const dropdownRef = useRef<HTMLDivElement>(null)

  const selected = options.find((o) => o.value === value)

  const close = useCallback(() => {
    setOpen(false)
    setFocusedIndex(-1)
  }, [])

  const handleOpen = useCallback(() => {
    if (disabled) return
    if (!open) {
      if (containerRef.current) {
        const rect = containerRef.current.getBoundingClientRect()
        const spaceBelow = window.innerHeight - rect.bottom
        const upward = spaceBelow < 220
        setOpenUpward(upward)
        // 用 fixed 坐标渲染到 body，彻底摆脱 Modal overflow-hidden 裁剪
        setDropdownPos({
          left: rect.left,
          top: upward ? rect.top - 4 : rect.bottom + 4,
          width: rect.width,
        })
      }
    }
    setOpen((v) => !v)
  }, [disabled, open])

  // 点击外部或滚动/缩放时关闭下拉（Portal 模式下下拉列表不在 containerRef 子树，需同时排除）
  useEffect(() => {
    if (!open) return
    const handler = (e: MouseEvent) => {
      const target = e.target as Node
      const inTrigger = containerRef.current?.contains(target) ?? false
      const inDropdown = dropdownRef.current?.contains(target) ?? false
      if (!inTrigger && !inDropdown) {
        close()
      }
    }
    document.addEventListener('mousedown', handler)
    window.addEventListener('scroll', (e: Event) => {
      // 下拉列表内部滚动不关闭
      if (dropdownRef.current?.contains(e.target as Node)) return
      close()
    }, { capture: true })
    window.addEventListener('resize', close)
    return () => {
      document.removeEventListener('mousedown', handler)
      window.removeEventListener('scroll', close, { capture: true })
      window.removeEventListener('resize', close)
    }
  }, [open, close])

  const handleKeyDown = (e: KeyboardEvent) => {
    if (disabled) return

    switch (e.key) {
      case 'Enter':
      case ' ':
        e.preventDefault()
        if (!open) {
          handleOpen()
          setFocusedIndex(options.findIndex((o) => o.value === value))
        } else if (focusedIndex >= 0) {
          onChange(options[focusedIndex].value)
          close()
        }
        break
      case 'ArrowDown':
        e.preventDefault()
        if (!open) {
          setOpen(true)
          setFocusedIndex(options.findIndex((o) => o.value === value))
        } else {
          setFocusedIndex((i) => (i + 1) % options.length)
        }
        break
      case 'ArrowUp':
        e.preventDefault()
        if (open) {
          setFocusedIndex((i) => (i - 1 + options.length) % options.length)
        }
        break
      case 'Escape':
        e.preventDefault()
        close()
        break
    }
  }

  return (
    <div ref={containerRef} className={cn('relative', className)}>
      <button
        type="button"
        onClick={handleOpen}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        className={cn(
          'flex items-center justify-between w-full rounded-lg border',
          'bg-gray-50 dark:bg-gray-800 text-sm',
          'px-3 py-2 min-h-[36px] text-left transition-colors',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
          open
            ? 'border-primary ring-1 ring-primary'
            : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600',
          disabled
            ? 'opacity-50 cursor-not-allowed'
            : 'cursor-pointer',
        )}
      >
        <span className={cn(
          'truncate',
          selected ? 'text-gray-900 dark:text-gray-100' : 'text-gray-400 dark:text-gray-500',
        )}>
          {selected?.label ?? placeholder ?? ''}
        </span>
        <Icon
          name="unfold_more"
          size="sm"
          className={cn(
            'ml-2 flex-shrink-0 text-gray-400 transition-transform',
            open && 'text-primary',
          )}
        />
      </button>

      {open && createPortal(
        <div
          ref={dropdownRef}
          style={{
            position: 'fixed',
            left: `${dropdownPos.left}px`,
            top: openUpward ? `${dropdownPos.top}px` : `${dropdownPos.top}px`,
            width: `${dropdownPos.width}px`,
            transform: openUpward ? 'translateY(-100%)' : '',
          }}
          className={cn(
            'z-50 min-w-[120px]',
            'bg-white dark:bg-gray-800 rounded-lg',
            'border border-gray-200 dark:border-gray-700',
            'shadow-menu dark:shadow-black/40',
            'py-1 overflow-auto max-h-60',
            openUpward ? 'animate-slide-down' : 'animate-slide-up',
          )}
        >
          {options.map((opt, idx) => {
            const isSelected = opt.value === value
            const isFocused = idx === focusedIndex
            return (
              <button
                key={opt.value}
                type="button"
                onClick={() => {
                  onChange(opt.value)
                  close()
                }}
                onMouseEnter={() => setFocusedIndex(idx)}
                className={cn(
                  'flex items-center w-full px-3 py-2 text-sm text-left transition-colors',
                  isFocused && 'bg-gray-100 dark:bg-gray-700/50',
                  isSelected
                    ? 'text-primary font-medium'
                    : 'text-gray-700 dark:text-gray-300',
                )}
              >
                {opt.icon && (
                  <Icon name={opt.icon} size="sm" className="mr-2 text-gray-400" />
                )}
                <div className="flex-1 min-w-0">
                  <div className="truncate">{opt.label}</div>
                  {opt.description && (
                    <div className="text-xs text-gray-400 dark:text-gray-500 truncate">{opt.description}</div>
                  )}
                </div>
                {isSelected && (
                  <Icon name="check" size="sm" className="ml-2 text-primary flex-shrink-0" />
                )}
              </button>
            )
          })}
        </div>,
        document.body,
      )}
    </div>
  )
}
