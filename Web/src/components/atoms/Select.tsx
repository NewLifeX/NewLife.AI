import { useState, useRef, useEffect, useCallback, type KeyboardEvent } from 'react'
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
  const containerRef = useRef<HTMLDivElement>(null)

  const selected = options.find((o) => o.value === value)

  const close = useCallback(() => {
    setOpen(false)
    setFocusedIndex(-1)
  }, [])

  useEffect(() => {
    if (!open) return
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        close()
      }
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open, close])

  const handleKeyDown = (e: KeyboardEvent) => {
    if (disabled) return

    switch (e.key) {
      case 'Enter':
      case ' ':
        e.preventDefault()
        if (!open) {
          setOpen(true)
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
        onClick={() => !disabled && setOpen(!open)}
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

      {open && (
        <div
          className={cn(
            'absolute z-50 mt-1 w-full min-w-[120px]',
            'bg-white dark:bg-gray-800 rounded-lg',
            'border border-gray-200 dark:border-gray-700',
            'shadow-menu dark:shadow-black/40',
            'py-1 overflow-auto max-h-60',
            'animate-slide-up',
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
        </div>
      )}
    </div>
  )
}
