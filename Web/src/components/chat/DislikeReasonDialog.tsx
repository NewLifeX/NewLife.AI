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
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100 pr-8">
          {t('feedback.title')}
        </h2>
        <p className="text-sm text-gray-500 dark:text-gray-400">
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
                  : 'bg-gray-50 dark:bg-gray-800 border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-400 hover:border-gray-300 dark:hover:border-gray-600'
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
          className="w-full px-3 py-2 text-sm border border-gray-200 dark:border-gray-700 rounded-lg bg-gray-50 dark:bg-gray-800 text-gray-700 dark:text-gray-300 placeholder-gray-400 dark:placeholder-gray-500 resize-none focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
        <div className="flex justify-end gap-2 pt-2">
          <button
            onClick={handleClose}
            className="px-4 py-2 text-sm text-gray-500 hover:text-gray-700 dark:hover:text-gray-300 transition-colors"
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
