import { useState, useRef, useEffect } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import type { ModelInfo } from '@/types'

interface ModelSelectorProps {
  models: ModelInfo[]
  currentModel: number
  onModelChange: (modelId: number) => void
  className?: string
}

export function ModelSelector({
  models,
  currentModel,
  onModelChange,
  className,
}: ModelSelectorProps) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  const selected = models.find((m) => m.id === currentModel)

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    if (open) document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open])

  return (
    <div ref={ref} className={cn('relative', className)}>
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors text-sm font-medium text-gray-700 dark:text-gray-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
      >
        <span>{selected?.name ?? '选择模型'}</span>
        <Icon name="expand_more" size="base" className={cn('transition-transform', open && 'rotate-180')} />
      </button>

      {open && (
        <div className="absolute top-full left-0 mt-1 w-64 bg-white dark:bg-gray-800 rounded-xl shadow-menu dark:shadow-black/40 border border-gray-100 dark:border-gray-700 overflow-hidden z-50 animate-slide-up">
          <div className="p-1.5 max-h-80 overflow-y-auto custom-scrollbar">
            {models.map((model) => {
              const isActive = model.id === currentModel
              return (
                <button
                  key={model.id}
                  onClick={() => {
                    onModelChange(model.id)
                    setOpen(false)
                  }}
                  className={cn(
                    'w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm transition-colors text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
                    isActive
                      ? 'bg-blue-50 dark:bg-blue-900/20 text-primary dark:text-blue-400'
                      : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/50',
                  )}
                >
                  <div className="flex items-center space-x-2 min-w-0">
                    <Icon name="smart_toy" size="base" className={isActive ? 'text-primary' : 'text-gray-400'} />
                    <span className="font-medium truncate">{model.name}</span>
                  </div>
                  <div className="flex items-center space-x-1 flex-shrink-0 ml-2">
                    {model.supportThinking && (
                      <Icon name="psychology" size="xs" className="text-purple-500" />
                    )}
                    {model.supportVision && (
                      <Icon name="visibility" size="xs" className="text-green-500" />
                    )}
                    {isActive && <Icon name="check" size="sm" className="text-primary" />}
                  </div>
                </button>
              )
            })}
          </div>
        </div>
      )}
    </div>
  )
}
