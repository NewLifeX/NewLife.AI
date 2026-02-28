import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { ConversationList } from './ConversationList'
import { NavLinks, type NavItem } from './NavLinks'
import { UserProfile } from './UserProfile'
import type { Conversation } from '@/types'

interface SidebarProps {
  conversations: Conversation[]
  activeConversationId?: number
  onConversationSelect: (id: number) => void
  onNewChat: () => void
  navItems?: NavItem[]
  onNavItemClick?: (id: string) => void
  userName?: string
  userAvatar?: string
  onUserClick?: () => void
  collapsed?: boolean
  className?: string
}

export function Sidebar({
  conversations,
  activeConversationId,
  onConversationSelect,
  onNewChat,
  navItems,
  onNavItemClick,
  userName,
  userAvatar,
  onUserClick,
  collapsed = false,
  className,
}: SidebarProps) {
  const { t } = useTranslation()
  if (collapsed) return null

  return (
    <aside
      className={cn(
        'w-[260px] bg-sidebar-light dark:bg-sidebar-dark',
        'flex flex-col border-r border-gray-100 dark:border-gray-800',
        'flex-shrink-0 relative z-20',
        className,
      )}
    >
      <div className="px-4 pt-4 pb-2 flex items-center space-x-2">
        <div className="w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center text-white font-bold text-sm">
          N
        </div>
        <span className="font-bold text-lg tracking-tight">{t('common.appName')}</span>
      </div>

      <div className="px-3 py-2">
        <button
          onClick={onNewChat}
          className="w-full flex items-center justify-start space-x-2 bg-blue-50 dark:bg-blue-900/20 hover:bg-blue-100 dark:hover:bg-blue-900/30 text-primary rounded-lg px-3 py-2.5 transition-colors group focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
        >
          <Icon name="add_circle_outline" size="lg" />
          <span className="text-sm font-medium">{t('sidebar.newChat')}</span>
          <div className="flex-grow" />
          <span className="opacity-0 group-hover:opacity-100 transition-opacity flex items-center space-x-0.5">
            <kbd className="inline-flex items-center justify-center h-5 min-w-[20px] px-1 rounded bg-white/70 dark:bg-gray-700/70 border border-gray-200 dark:border-gray-600 text-[10px] text-gray-400 group-hover:text-primary font-mono shadow-[0_1px_0_rgba(0,0,0,0.06)]">
              <Icon name="keyboard_command_key" size="xs" className="text-[10px]" />
            </kbd>
            <kbd className="inline-flex items-center justify-center h-5 min-w-[20px] px-1 rounded bg-white/70 dark:bg-gray-700/70 border border-gray-200 dark:border-gray-600 text-[10px] text-gray-400 group-hover:text-primary font-mono shadow-[0_1px_0_rgba(0,0,0,0.06)]">
              K
            </kbd>
          </span>
        </button>
      </div>

      <NavLinks items={navItems ?? undefined} onItemClick={onNavItemClick} />

      <ConversationList
        conversations={conversations}
        activeId={activeConversationId}
        onSelect={onConversationSelect}
      />

      <UserProfile name={userName ?? t('common.user')} avatarUrl={userAvatar} onClick={onUserClick} />
    </aside>
  )
}
