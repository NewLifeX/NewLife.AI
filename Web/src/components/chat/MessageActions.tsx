import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { IconButton } from '@/components/atoms/IconButton'

interface MessageActionsProps {
  onCopy?: () => void
  onRegenerate?: () => void
  onDislike?: () => void
  className?: string
}

export function MessageActions({
  onCopy,
  onRegenerate,
  onDislike,
  className,
}: MessageActionsProps) {
  const { t } = useTranslation()
  return (
    <div className={cn('flex items-center space-x-0.5', className)}>
      <IconButton icon="content_copy" size="xs" label={t('common.copy')} onClick={onCopy} className="hover:!bg-transparent dark:hover:!bg-transparent" />
      <IconButton icon="refresh" size="xs" label={t('common.regenerate')} onClick={onRegenerate} className="hover:!bg-transparent dark:hover:!bg-transparent" />
      <IconButton icon="thumb_down" size="xs" label={t('common.dislike')} onClick={onDislike} className="hover:!bg-transparent dark:hover:!bg-transparent" />
    </div>
  )
}
