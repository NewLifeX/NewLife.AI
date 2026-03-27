import { useState, useCallback, useRef, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface MessageActionsProps {
  onCopy?: () => void
  onRegenerate?: () => void
  onLike?: () => void
  onDislike?: () => void
  onShare?: () => void
  onDelete?: () => void
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
  onShare,
  onDelete,
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
        <Icon name={copied ? 'check' : 'content_copy'} variant="outlined" size="lg" />
      </button>
      <button className={btnBase} onClick={onRegenerate} title={t('common.regenerate')}>
        <Icon name="refresh" variant="outlined" size="lg" />
      </button>
      {onLike && (
        <button className={cn(btnBase, liked && 'text-primary')} onClick={onLike} title={t('common.like')}>
          <Icon name="thumb_up" variant={liked ? 'filled' : 'outlined'} size="lg" />
        </button>
      )}
      <button className={cn(btnBase, disliked && 'text-red-500')} onClick={onDislike} title={t('common.dislike')}>
        <Icon name="thumb_down" variant={disliked ? 'filled' : 'outlined'} size="lg" />
      </button>
      {onShare && (
        <button className={btnBase} onClick={onShare} title={t('common.share')}>
          <Icon name="share" variant="outlined" size="lg" />
        </button>
      )}
      {onDelete && (
        <button className={cn(btnBase, 'hover:!text-red-500')} onClick={onDelete} title={t('common.delete')}>
          <Icon name="delete" variant="outlined" size="lg" />
        </button>
      )}
    </div>
  )
}
