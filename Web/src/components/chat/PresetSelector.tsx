import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { fetchPresets, type Preset } from '@/lib/api'

interface PresetSelectorProps {
  onSelect: (preset: Preset) => void
}

const POPUP_WIDTH = 224 // w-56 = 14rem

export function PresetSelector({ onSelect }: PresetSelectorProps) {
  const { t } = useTranslation()
  const [presets, setPresets] = useState<Preset[]>([])
  const [open, setOpen] = useState(false)
  const buttonRef = useRef<HTMLButtonElement>(null)
  const [popupPos, setPopupPos] = useState<{ bottom: number; left: number } | null>(null)

  useEffect(() => {
    fetchPresets().then(setPresets).catch(() => {})
  }, [])

  // 使用 portal 渲染弹出层，彻底脱离祖先 overflow: hidden 裁剪
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

  const handleSelect = useCallback((preset: Preset) => {
    onSelect(preset)
    setOpen(false)
  }, [onSelect])

  if (presets.length === 0) return null

  return (
    <div className="relative">
      <button
        ref={buttonRef}
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

      {open && popupPos && createPortal(
        <>
          <div className="fixed inset-0 z-[9998]" onClick={() => setOpen(false)} />
          <div
            className="fixed w-56 py-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg shadow-lg z-[9999] max-h-64 overflow-y-auto"
            style={{ bottom: popupPos.bottom, left: popupPos.left }}
          >
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
        </>,
        document.body,
      )}
    </div>
  )
}
