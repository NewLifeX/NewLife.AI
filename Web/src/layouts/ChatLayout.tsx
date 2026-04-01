import { type ReactNode, useCallback, useEffect, useState, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Sidebar } from '@/components/sidebar/Sidebar'
import { useUIStore } from '@/stores'
import type { Conversation } from '@/types'

const MOBILE_BREAKPOINT = 768

interface ChatLayoutProps {
  children: ReactNode
  modelSelector?: ReactNode
  conversationTitle?: string
  conversations: Conversation[]
  activeConversationId?: string
  onConversationSelect: (id: string) => void
  onConversationDelete?: (id: string) => void
  onConversationPin?: (id: string, isPinned: boolean) => void
  onConversationRename?: (id: string, title: string) => void
  onNewChat: () => void
  onSettingsOpen?: () => void
  sidebarCollapsed?: boolean
  onSidebarToggle?: () => void
  onLoadMore?: () => void
  onFileDrop?: (file: File) => void
  userName?: string
  userAvatar?: string
  className?: string
}

export function ChatLayout({
  children,
  modelSelector,
  conversationTitle,
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
  onFileDrop,
  userName,
  userAvatar,
  className,
}: ChatLayoutProps) {
  const { t } = useTranslation()
  const [isDragOver, setIsDragOver] = useState(false)
  const dragCounterRef = useRef(0)
  const [isMobile, setIsMobile] = useState(
    () => typeof window !== 'undefined' && window.innerWidth < MOBILE_BREAKPOINT,
  )

  useEffect(() => {
    const check = () => setIsMobile(window.innerWidth < MOBILE_BREAKPOINT)
    check()
    window.addEventListener('resize', check)
    return () => window.removeEventListener('resize', check)
  }, [])

  // 移动端初始化时自动收起侧边栏
  useEffect(() => {
    if (isMobile) {
      useUIStore.getState().setSidebarCollapsed(true)
    }
  }, [isMobile])

  // 移动端选择会话后自动收起侧边栏
  const handleMobileSelect = useCallback((id: string) => {
    onConversationSelect(id)
    if (isMobile && !sidebarCollapsed) {
      onSidebarToggle?.()
    }
  }, [onConversationSelect, isMobile, sidebarCollapsed, onSidebarToggle])

  const handleDragEnter = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    dragCounterRef.current++
    if (e.dataTransfer.types.includes('Files')) setIsDragOver(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    dragCounterRef.current--
    if (dragCounterRef.current === 0) setIsDragOver(false)
  }, [])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
  }, [])

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    dragCounterRef.current = 0
    setIsDragOver(false)
    const files = e.dataTransfer.files
    if (files && onFileDrop) {
      Array.from(files).forEach((f) => onFileDrop(f))
    }
  }, [onFileDrop])

  const showSidebar = !sidebarCollapsed

  return (
    <div
      className={cn('h-screen flex overflow-hidden bg-background-light dark:bg-background-dark text-gray-900 dark:text-gray-100', className)}
      onDragEnter={handleDragEnter}
      onDragLeave={handleDragLeave}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
    >
      {isDragOver && (
        <div className="fixed inset-0 z-[100] p-2.5 pointer-events-none">
          <div className="w-full h-full border-2 border-dashed border-primary/40 dark:border-primary/30 rounded-2xl bg-white/80 dark:bg-background-dark/80 backdrop-blur-sm flex items-center justify-center transition-colors">
            <div className="flex flex-col items-center gap-3 text-primary/70 dark:text-primary/50">
              <Icon name="upload_file" size="xl" />
              <span className="text-sm font-medium">{t('chat.dropToUpload')}</span>
            </div>
          </div>
        </div>
      )}
      {/* 移动端遮罩 */}
      {isMobile && showSidebar && (
        <div
          className="fixed inset-0 bg-black/40 z-40 transition-opacity"
          onClick={onSidebarToggle}
        />
      )}

      <div
        className={cn(
          'h-full',
          isMobile && 'fixed inset-y-0 left-0 z-50',
        )}
        style={isMobile ? {
          transform: `translateX(${showSidebar ? 0 : -100}%)`,
          transition: 'transform 0.2s ease-in-out',
        } : undefined}
      >
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
          {conversationTitle && (
            <h1 className="ml-3 text-sm font-medium text-gray-700 dark:text-gray-300 truncate max-w-xs">
              {conversationTitle}
            </h1>
          )}
        </div>
        {children}
      </main>
    </div>
  )
}
