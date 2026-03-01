import { type ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Sidebar } from '@/components/sidebar/Sidebar'
import type { Conversation } from '@/types'

interface ChatLayoutProps {
  children: ReactNode
  modelSelector?: ReactNode
  conversations: Conversation[]
  activeConversationId?: number
  onConversationSelect: (id: number) => void
  onConversationDelete?: (id: number) => void
  onConversationPin?: (id: number, isPinned: boolean) => void
  onConversationRename?: (id: number, title: string) => void
  onNewChat: () => void
  onSettingsOpen?: () => void
  sidebarCollapsed?: boolean
  onSidebarToggle?: () => void
  onLoadMore?: () => void
  userName?: string
  userAvatar?: string
  className?: string
}

export function ChatLayout({
  children,
  modelSelector,
  conversations,
  activeConversationId,
  onConversationSelect,
  onConversationDelete,
  onConversationPin,
  onConversationRename,
  onNewChat,
  onSettingsOpen,
  sidebarCollapsed = false,
  onSidebarToggle,
  onLoadMore,
  userName,
  userAvatar,
  className,
}: ChatLayoutProps) {
  return (
    <div className={cn('h-screen flex overflow-hidden bg-background-light dark:bg-background-dark text-gray-900 dark:text-gray-100', className)}>
      <Sidebar
        conversations={conversations}
        activeConversationId={activeConversationId}
        onConversationSelect={onConversationSelect}
        onConversationDelete={onConversationDelete}
        onConversationPin={onConversationPin}
        onConversationRename={onConversationRename}
        onNewChat={onNewChat}
        onToggle={onSidebarToggle}
        onLoadMore={onLoadMore}
        onUserClick={onSettingsOpen}
        collapsed={sidebarCollapsed}
        userName={userName}
        userAvatar={userAvatar}
      />

      <main className="flex-1 relative flex flex-col h-full bg-background-light dark:bg-background-dark">
        <div className="flex items-center px-4 pt-3 pb-1 z-10">
          {sidebarCollapsed && (
            <button
              onClick={onSidebarToggle}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 p-1 rounded-md hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 mr-2"
            >
              <Icon name="menu_open" variant="outlined" size="lg" />
            </button>
          )}
          {modelSelector}
        </div>
        {children}
      </main>
    </div>
  )
}
