import { useState, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { Modal } from '@/components/common/Modal'

interface DislikeReasonDialogProps {
  open: boolean
  onClose: () => void
  onSubmit: (reasons: string[]) => void
}

const REASON_KEYS = [
  'inaccurate',
  'incomplete',
  'formatting',
  'harmful',
  'other',
] as const

export function DislikeReasonDialog({ open, onClose, onSubmit }: DislikeReasonDialogProps) {
  const { t } = useTranslation()
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [customText, setCustomText] = useState('')

  const toggle = useCallback((key: string) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }, [])

  const handleSubmit = useCallback(() => {
    const reasons = Array.from(selected)
    if (customText.trim()) reasons.push(customText.trim())
    onSubmit(reasons)
    setSelected(new Set())
    setCustomText('')
    onClose()
  }, [selected, customText, onSubmit, onClose])

  const handleClose = useCallback(() => {
    setSelected(new Set())
    setCustomText('')
    onClose()
  }, [onClose])

  return (
    <Modal open={open} onClose={handleClose} maxWidth="max-w-sm">
      <div className="p-6 space-y-4 w-full">
        <h2 className="text-lg font-semibold text-[var(--color-text-primary)] pr-8">
          {t('feedback.title')}
        </h2>
        <p className="text-sm text-[var(--color-text-secondary)]">
          {t('feedback.description')}
        </p>
        <div className="flex flex-wrap gap-2">
          {REASON_KEYS.map((key) => (
            <button
              key={key}
              onClick={() => toggle(key)}
              className={`px-3 py-1.5 rounded-full text-sm border transition-colors ${
                selected.has(key)
                  ? 'bg-primary/10 border-primary text-primary dark:bg-primary/20'
                  : 'bg-[var(--color-surface-1)] border-[var(--color-border-subtle)] text-[var(--color-text-secondary)] hover:border-[var(--color-border-strong)]'
              }`}
            >
              {t(`feedback.reason.${key}`)}
            </button>
          ))}
        </div>
        <textarea
          value={customText}
          onChange={(e) => setCustomText(e.target.value)}
          placeholder={t('feedback.placeholder')}
          rows={3}
          className="w-full px-3 py-2 text-sm border border-[var(--color-border-default)] rounded-lg bg-[var(--color-surface-1)] text-[var(--color-text-primary)] placeholder-[var(--color-text-tertiary)] resize-none focus:outline-none focus:ring-2 focus:ring-[color:var(--color-brand-500)]/50"
        />
        <div className="flex justify-end gap-2 pt-2">
          <button
            onClick={handleClose}
            className="px-4 py-2 text-sm text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)] transition-colors"
          >
            {t('common.cancel')}
          </button>
          <button
            onClick={handleSubmit}
            disabled={selected.size === 0 && !customText.trim()}
            className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-blue-600 disabled:opacity-50 transition-colors"
          >
            {t('common.confirm')}
          </button>
        </div>
      </div>
    </Modal>
  )
}
