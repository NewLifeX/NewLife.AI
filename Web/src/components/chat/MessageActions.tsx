import { useState, useCallback, useRef, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

interface MessageActionsProps {
  onCopy?: () => void
  onRegenerate?: () => void
  onLike?: () => void
  onDislike?: () => void
  liked?: boolean
  disliked?: boolean
  className?: string
}

const btnBase =
  'p-1.5 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-md transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50'

export function MessageActions({
  onCopy,
  onRegenerate,
  onLike,
  onDislike,
  liked = false,
  disliked = false,
  className,
}: MessageActionsProps) {
  const { t } = useTranslation()
  const [copied, setCopied] = useState(false)
  const timerRef = useRef<ReturnType<typeof setTimeout>>(undefined)

  useEffect(() => () => { clearTimeout(timerRef.current) }, [])

  const handleCopy = useCallback(() => {
    onCopy?.()
    setCopied(true)
    clearTimeout(timerRef.current)
    timerRef.current = setTimeout(() => setCopied(false), 2000)
  }, [onCopy])

  return (
    <div className={cn('flex items-center mt-2 space-x-2 ml-1', className)}>
      <button className={cn(btnBase, copied && 'text-green-500')} onClick={handleCopy} title={t('common.copy')}>
        <span className="material-icons-outlined text-[18px]">{copied ? 'check' : 'content_copy'}</span>
      </button>
      <button className={btnBase} onClick={onRegenerate} title={t('common.regenerate')}>
        <span className="material-icons-outlined text-[18px]">refresh</span>
      </button>
      {onLike && (
        <button className={cn(btnBase, liked && 'text-primary')} onClick={onLike} title={t('common.like')}>
          <span className="material-icons-outlined text-[18px]">thumb_up</span>
        </button>
      )}
      <button className={cn(btnBase, disliked && 'text-red-500')} onClick={onDislike} title={t('common.dislike')}>
        <span className="material-icons-outlined text-[18px]">thumb_down</span>
      </button>
    </div>
  )
}
