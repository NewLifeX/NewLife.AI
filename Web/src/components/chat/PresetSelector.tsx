import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { fetchPresets, type Preset } from '@/lib/api'

interface PresetSelectorProps {
  onSelect: (preset: Preset) => void
}

export function PresetSelector({ onSelect }: PresetSelectorProps) {
  const { t } = useTranslation()
  const [presets, setPresets] = useState<Preset[]>([])
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    fetchPresets().then(setPresets).catch(() => {})
  }, [])

  // 点击外部关闭
  useEffect(() => {
    if (!open) return
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open])

  const handleSelect = useCallback((preset: Preset) => {
    onSelect(preset)
    setOpen(false)
  }, [onSelect])

  if (presets.length === 0) return null

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen((v) => !v)}
        className={cn(
          'flex items-center gap-1 px-2.5 py-1.5 text-xs font-medium rounded-lg transition-colors',
          'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200',
          'hover:bg-gray-100 dark:hover:bg-gray-800',
          open && 'bg-gray-100 dark:bg-gray-800',
        )}
        title={t('preset.title')}
      >
        <Icon name="tune" size="sm" />
        <span className="hidden sm:inline">{t('preset.title')}</span>
      </button>

      {open && (
        <div className="absolute top-full left-0 mt-1 w-56 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg z-50 py-1 max-h-64 overflow-y-auto">
          {presets.map((p) => (
            <button
              key={p.id}
              onClick={() => handleSelect(p)}
              className="w-full text-left px-3 py-2 text-sm hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors flex items-center justify-between"
            >
              <div className="flex-1 min-w-0">
                <div className="font-medium text-gray-700 dark:text-gray-200 truncate">{p.name}</div>
                {p.modelName && (
                  <div className="text-xs text-gray-400 dark:text-gray-500 truncate">{p.modelName}</div>
                )}
              </div>
              {p.isDefault && (
                <span className="ml-2 text-xs text-primary">
                  <Icon name="star" size="xs" />
                </span>
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
