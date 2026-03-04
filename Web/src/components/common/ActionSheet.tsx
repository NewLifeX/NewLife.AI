import { useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

export interface ActionSheetItem {
  icon: string
  label: string
  onClick: () => void
  danger?: boolean
}

interface ActionSheetProps {
  open: boolean
  onClose: () => void
  items: ActionSheetItem[]
  position?: { x: number; y: number }
}

export function ActionSheet({ open, onClose, items, position }: ActionSheetProps) {
  const sheetRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const handleClick = (e: MouseEvent) => {
      if (sheetRef.current && !sheetRef.current.contains(e.target as Node)) {
        onClose()
      }
    }
    document.addEventListener('touchstart', handleClick as never)
    document.addEventListener('mousedown', handleClick as never)
    return () => {
      document.removeEventListener('touchstart', handleClick as never)
      document.removeEventListener('mousedown', handleClick as never)
    }
  }, [open, onClose])

  if (!open) return null

  // 计算弹出位置
  const style: React.CSSProperties = position
    ? {
        position: 'fixed',
        left: Math.min(position.x, window.innerWidth - 200),
        top: Math.min(position.y, window.innerHeight - items.length * 48 - 20),
      }
    : {}

  return createPortal(
    <div className="fixed inset-0 z-[9998] bg-black/20" onClick={onClose}>
      <div
        ref={sheetRef}
        className="bg-white dark:bg-gray-800 rounded-xl shadow-xl border border-gray-200 dark:border-gray-700 overflow-hidden min-w-[180px] animate-in fade-in slide-in-from-bottom-2 duration-150"
        style={style}
        onClick={(e) => e.stopPropagation()}
      >
        {items.map((item, i) => (
          <button
            key={i}
            onClick={() => {
              item.onClick()
              onClose()
            }}
            className={cn(
              'flex items-center gap-3 w-full px-4 py-3 text-sm transition-colors',
              item.danger
                ? 'text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20'
                : 'text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700',
              i < items.length - 1 && 'border-b border-gray-100 dark:border-gray-700',
            )}
          >
            <Icon name={item.icon} size="base" />
            <span>{item.label}</span>
          </button>
        ))}
      </div>
    </div>,
    document.body,
  )
}
