import { type ReactNode, useCallback, useEffect, useState } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Sidebar } from '@/components/sidebar/Sidebar'
import type { Conversation } from '@/types'

const MOBILE_BREAKPOINT = 768

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
  const [isMobile, setIsMobile] = useState(false)

  useEffect(() => {
    const check = () => setIsMobile(window.innerWidth < MOBILE_BREAKPOINT)
    check()
    window.addEventListener('resize', check)
    return () => window.removeEventListener('resize', check)
  }, [])

  // 移动端初始化时自动收起侧边栏
  useEffect(() => {
    if (isMobile && !sidebarCollapsed) {
      onSidebarToggle?.()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isMobile])

  // 移动端选择会话后自动收起侧边栏
  const handleMobileSelect = useCallback((id: number) => {
    onConversationSelect(id)
    if (isMobile && !sidebarCollapsed) {
      onSidebarToggle?.()
    }
  }, [onConversationSelect, isMobile, sidebarCollapsed, onSidebarToggle])

  const showSidebar = !sidebarCollapsed

  return (
    <div className={cn('h-screen flex overflow-hidden bg-background-light dark:bg-background-dark text-gray-900 dark:text-gray-100', className)}>
      {/* 移动端遮罩 */}
      {isMobile && showSidebar && (
        <div
          className="fixed inset-0 bg-black/40 z-40 transition-opacity"
          onClick={onSidebarToggle}
        />
      )}

      <div className={cn(
        isMobile && 'fixed inset-y-0 left-0 z-50 transition-transform duration-200',
        isMobile && !showSidebar && '-translate-x-full',
        isMobile && showSidebar && 'translate-x-0',
      )}>
        <Sidebar
          conversations={conversations}
          activeConversationId={activeConversationId}
          onConversationSelect={handleMobileSelect}
          onConversationDelete={onConversationDelete}
          onConversationPin={onConversationPin}
          onConversationRename={onConversationRename}
          onNewChat={onNewChat}
          onToggle={onSidebarToggle}
          onLoadMore={onLoadMore}
          onUserClick={onSettingsOpen}
          collapsed={isMobile ? false : sidebarCollapsed}
          userName={userName}
          userAvatar={userAvatar}
        />
      </div>

      <main className="flex-1 relative flex flex-col h-full bg-background-light dark:bg-background-dark">
        <div className="flex items-center px-4 pt-3 pb-1 z-10">
          {(sidebarCollapsed || isMobile) && (
            <button
              onClick={onSidebarToggle}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 p-1 rounded-md hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 mr-2"
            >
              <Icon name="menu" variant="outlined" size="lg" />
            </button>
          )}
          {modelSelector}
        </div>
        {children}
      </main>
    </div>
  )
}
