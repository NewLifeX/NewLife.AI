import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import type { Attachment } from '@/types'

interface AttachmentChipProps {
  attachment: Attachment
  onRemove?: () => void
  className?: string
}

const typeIcons: Record<string, { icon: string; bg: string; color: string }> = {
  pdf: { icon: 'picture_as_pdf', bg: 'bg-red-100 dark:bg-red-900/30', color: 'text-red-500' },
  image: { icon: 'image', bg: 'bg-blue-100 dark:bg-blue-900/30', color: 'text-blue-500' },
  file: { icon: 'description', bg: 'bg-gray-100 dark:bg-gray-700', color: 'text-gray-500' },
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function AttachmentChip({ attachment, onRemove, className }: AttachmentChipProps) {
  const typeStyle = typeIcons[attachment.type] ?? typeIcons.file

  return (
    <div
      className={cn(
        'attachment-chip flex items-center space-x-2',
        'bg-gray-100 dark:bg-gray-700/50 border border-gray-200 dark:border-gray-600',
        'rounded-lg pl-2 pr-1 py-1 group/chip cursor-pointer',
        'hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors select-none',
        className,
      )}
    >
      {attachment.type === 'image' && attachment.previewUrl ? (
        <div className="w-8 h-8 rounded overflow-hidden flex-shrink-0 border border-blue-200 dark:border-blue-800/30">
          <img src={attachment.previewUrl} alt="Preview" className="w-full h-full object-cover" />
        </div>
      ) : (
        <div className={cn('w-6 h-8 rounded flex items-center justify-center flex-shrink-0', typeStyle.bg)}>
          <Icon name={typeStyle.icon} variant="filled" size="sm" className={typeStyle.color} />
        </div>
      )}
      <div className="flex flex-col min-w-[80px] max-w-[120px]">
        <span className="text-[11px] font-medium text-gray-700 dark:text-gray-200 truncate">
          {attachment.name}
        </span>
        <span className="text-[9px] text-gray-400">{formatSize(attachment.size)}</span>
      </div>
      {onRemove && (
        <button
          onClick={(e) => {
            e.stopPropagation()
            onRemove()
          }}
          className="opacity-0 group-hover/chip:opacity-100 p-1 hover:bg-gray-300 dark:hover:bg-gray-600 rounded-full text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-all"
        >
          <Icon name="close" variant="filled" size="sm" className="text-[14px]" />
        </button>
      )}
    </div>
  )
}
