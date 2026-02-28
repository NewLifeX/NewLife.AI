import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { ScrollArea } from '@/components/common/ScrollArea'
import type { Conversation } from '@/types'

interface ConversationListProps {
  conversations: Conversation[]
  activeId?: number
  onSelect: (id: number) => void
  className?: string
}

function ConversationIcon({ conv }: { conv: Conversation }) {
  if (conv.icon && conv.iconColor) {
    return (
      <span
        className={cn('w-4 h-4 rounded-full flex items-center justify-center text-[8px] text-white')}
        style={{ backgroundColor: conv.iconColor }}
      >
        {conv.icon}
      </span>
    )
  }
  if (conv.isPinned) {
    return <Icon name="smart_toy" size="base" className="text-gray-500" />
  }
  return <Icon name="chat_bubble_outline" size="base" className="text-gray-400" />
}

export function ConversationList({
  conversations,
  activeId,
  onSelect,
  className,
}: ConversationListProps) {
  const { t } = useTranslation()
  return (
    <ScrollArea className={cn('flex-1 px-3 pb-2', className)}>
      <div className="text-xs text-gray-400 px-3 py-2 font-medium">{t('sidebar.history')}</div>
      <ul className="space-y-0.5">
        {conversations.map((conv) => {
          const isActive = conv.id === activeId
          return (
            <li key={conv.id}>
              <button
                onClick={() => onSelect(conv.id)}
                className={cn(
                  'group flex items-center space-x-2 px-3 py-2 rounded-lg text-sm w-full text-left relative transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
                  isActive
                    ? 'bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-gray-100 font-medium'
                    : 'text-gray-700 dark:text-gray-300 hover:bg-gray-200/50 dark:hover:bg-gray-700/50',
                )}
              >
                <ConversationIcon conv={conv} />
                <span className="truncate">{conv.title}</span>
                {conv.isPinned && (
                  <span className="absolute right-3 rotate-45 transform">
                    <Icon name="push_pin" variant="filled" size="xs" className="text-gray-400" />
                  </span>
                )}
              </button>
            </li>
          )
        })}
      </ul>
    </ScrollArea>
  )
}
