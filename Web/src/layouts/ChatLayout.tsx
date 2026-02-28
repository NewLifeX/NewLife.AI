import { type ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Sidebar } from '@/components/sidebar/Sidebar'
import type { Conversation } from '@/types'

interface ChatLayoutProps {
  children: ReactNode
  conversations: Conversation[]
  activeConversationId?: number
  onConversationSelect: (id: number) => void
  onNewChat: () => void
  onSettingsOpen?: () => void
  sidebarCollapsed?: boolean
  onSidebarToggle?: () => void
  userName?: string
  userAvatar?: string
  className?: string
}

export function ChatLayout({
  children,
  conversations,
  activeConversationId,
  onConversationSelect,
  onNewChat,
  onSettingsOpen,
  sidebarCollapsed = false,
  onSidebarToggle,
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
        onNewChat={onNewChat}
        onUserClick={onSettingsOpen}
        collapsed={sidebarCollapsed}
        userName={userName}
        userAvatar={userAvatar}
      />

      <main className="flex-1 relative flex flex-col h-full bg-background-light dark:bg-background-dark">
        {sidebarCollapsed && (
          <div className="absolute top-4 left-4 z-10">
            <button
              onClick={onSidebarToggle}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 p-1 rounded-md hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors"
            >
              <Icon name="menu_open" size="lg" />
            </button>
          </div>
        )}
        {children}
      </main>
    </div>
  )
}
