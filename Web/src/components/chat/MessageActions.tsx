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
    <div className={cn('flex items-center space-x-2 ml-1', className)}>
      <IconButton icon="content_copy" size="sm" label={t('common.copy')} onClick={onCopy} />
      <IconButton icon="refresh" size="sm" label={t('common.regenerate')} onClick={onRegenerate} />
      <IconButton icon="thumb_down" size="sm" label={t('common.dislike')} onClick={onDislike} />
    </div>
  )
}
