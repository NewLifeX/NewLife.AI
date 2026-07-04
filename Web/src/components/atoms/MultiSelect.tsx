import { useState, useRef, useEffect, useCallback, type KeyboardEvent } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import type { SelectOption } from './Select'

interface MultiSelectProps {
  options: SelectOption[]
  values: string[]
  onChange: (values: string[]) => void
  placeholder?: string
  className?: string
  disabled?: boolean
}

/**
 * 多选下拉复选框组件。
 * Portal 渲染到 body，基于 Select 的 fixed 定位模式，支持向上/向下展开。
 * 选中项以 chip 标签形式展示在触发按钮内，带 × 移除按钮。
 * 遵循 UI 规范：CSS 变量、focus ring、animate-slide-up 入场动效。
 */
export function MultiSelect({
  options,
  values,
  onChange,
  placeholder,
  className,
  disabled = false,
}: MultiSelectProps) {
  const [open, setOpen] = useState(false)
  const [focusedIndex, setFocusedIndex] = useState(-1)
  const [openUpward, setOpenUpward] = useState(false)
  const [dropdownPos, setDropdownPos] = useState({ left: 0, top: 0, width: 0 })
  const containerRef = useRef<HTMLDivElement>(null)
  const dropdownRef = useRef<HTMLDivElement>(null)

  // 已选项映射，用于快速查找
  const selectedSet = new Set(values)
  const selectedOptions = options.filter((o) => selectedSet.has(o.value))

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
        const upward = spaceBelow < 260
        setOpenUpward(upward)
        setDropdownPos({
          left: rect.left,
          top: upward ? rect.top - 4 : rect.bottom + 4,
          width: Math.max(rect.width, 220),
        })
      }
    }
    setOpen((v) => !v)
  }, [disabled, open])

  // 点击外部/滚动/缩放时关闭
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

  const toggleValue = useCallback((val: string) => {
    const next = selectedSet.has(val)
      ? values.filter((v) => v !== val)
      : [...values, val]
    onChange(next)
  }, [values, selectedSet, onChange])

  const removeValue = useCallback((val: string, e: React.MouseEvent) => {
    e.stopPropagation()
    onChange(values.filter((v) => v !== val))
  }, [values, onChange])

  const clearAll = useCallback(() => {
    onChange([])
  }, [onChange])

  const selectAll = useCallback(() => {
    onChange(options.map((o) => o.value))
  }, [options, onChange])

  const handleKeyDown = (e: KeyboardEvent) => {
    if (disabled) return

    switch (e.key) {
      case 'Enter':
      case ' ':
        e.preventDefault()
        if (!open) {
          handleOpen()
          setFocusedIndex(0)
        } else if (focusedIndex >= 0) {
          toggleValue(options[focusedIndex].value)
        }
        break
      case 'ArrowDown':
        e.preventDefault()
        if (!open) {
          setOpen(true)
          setFocusedIndex(0)
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

  const hasSelection = selectedOptions.length > 0
  const allSelected = options.length > 0 && selectedOptions.length === options.length

  return (
    <div ref={containerRef} className={cn('relative', className)}>
      <button
        type="button"
        onClick={handleOpen}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        className={cn(
          'flex items-center gap-1 w-full min-h-[36px] rounded-lg border px-3 py-1.5 text-sm text-left transition-colors flex-wrap',
          'bg-gray-50 dark:bg-gray-800',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
          open
            ? 'border-primary ring-1 ring-primary'
            : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600',
          disabled
            ? 'opacity-50 cursor-not-allowed'
            : 'cursor-pointer',
        )}
      >
        {hasSelection ? (
          selectedOptions.map((opt) => (
            <span
              key={opt.value}
              className={cn(
                'inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-xs font-medium',
                'bg-[var(--color-brand-50)] text-[color:var(--color-brand-700)]',
                'dark:bg-[var(--color-brand-900)]/40 dark:text-[color:var(--color-brand-200)]',
              )}
            >
              <span className="truncate max-w-[100px]">{opt.label}</span>
              <button
                type="button"
                onClick={(e) => removeValue(opt.value, e)}
                className="flex-shrink-0 hover:opacity-70 focus-visible:outline-none"
                aria-label={`移除 ${opt.label}`}
              >
                <Icon name="close" size="xs" />
              </button>
            </span>
          ))
        ) : (
          <span className="text-gray-400 dark:text-gray-500">{placeholder ?? ''}</span>
        )}
        <Icon
          name="unfold_more"
          size="sm"
          className={cn(
            'ml-auto flex-shrink-0 text-gray-400 transition-transform',
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
            'py-1 overflow-auto max-h-64',
            openUpward ? 'animate-slide-down' : 'animate-slide-up',
          )}
        >
          {/* 全选/取消全选按钮 */}
          {options.length > 0 && (
            <button
              type="button"
              onClick={allSelected ? clearAll : selectAll}
              className={cn(
                'flex items-center w-full px-3 py-2 text-xs text-left transition-colors',
                'text-[var(--color-text-tertiary)] hover:bg-gray-100 dark:hover:bg-gray-700/50',
                'border-b border-gray-100 dark:border-gray-700',
              )}
            >
              <Icon
                name={allSelected ? 'deselect' : 'select_all'}
                size="xs"
                className="mr-2"
              />
              {allSelected ? '取消全选' : '全选'}
            </button>
          )}

          {options.length === 0 ? (
            <div className="px-3 py-4 text-xs text-center text-gray-400 dark:text-gray-500">
              无可用选项
            </div>
          ) : (
            options.map((opt, idx) => {
              const isChecked = selectedSet.has(opt.value)
              const isFocused = idx === focusedIndex
              return (
                <label
                  key={opt.value}
                  onMouseEnter={() => setFocusedIndex(idx)}
                  className={cn(
                    'flex items-center w-full px-3 py-2 text-sm text-left transition-colors cursor-pointer select-none',
                    isFocused && 'bg-gray-100 dark:bg-gray-700/50',
                  )}
                >
                  <input
                    type="checkbox"
                    checked={isChecked}
                    onChange={() => toggleValue(opt.value)}
                    className={cn(
                      'flex-shrink-0 rounded',
                      'border-gray-300 dark:border-gray-600',
                      'text-[color:var(--color-brand-500)]',
                      'focus:ring-[color:var(--color-brand-500)]/50 focus:ring-offset-0',
                    )}
                  />
                  {opt.icon && (
                    <Icon name={opt.icon} size="sm" className="ml-2 mr-1.5 text-gray-400" />
                  )}
                  <div className="flex-1 min-w-0 ml-2">
                    <div className="truncate text-[var(--color-text-primary)]">{opt.label}</div>
                    {opt.description && (
                      <div className="text-xs text-[var(--color-text-tertiary)] truncate">{opt.description}</div>
                    )}
                  </div>
                </label>
              )
            })
          )}
        </div>,
        document.body
      )}
    </div>
  )
}
