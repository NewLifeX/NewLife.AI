import { useRef, useEffect, useCallback, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import { MessageBubble } from '@/components/chat/MessageBubble'
import { ChatInput } from '@/components/input/ChatInput'
import type { Message, Attachment } from '@/types'
import { MarkdownRenderer } from '@/components/chat/MarkdownRenderer'
import { ThinkingBlock } from '@/components/chat/ThinkingBlock'

type ThinkingMode = 'fast' | 'balanced' | 'deep'

interface ChatPageProps {
  messages: Message[]
  isGenerating: boolean
  onSend: (message: string) => void
  onStop?: () => void
  onCopy?: (id: number) => void
  onRegenerate?: (id: number) => void
  onEditSubmit?: (id: number, content: string) => void
  onLike?: (id: number) => void
  onDislike?: (id: number) => void
  thinkingMode?: ThinkingMode
  onThinkingModeChange?: (mode: ThinkingMode) => void
  attachments?: Attachment[]
  onAttachmentAdd?: (file: File) => void
  onAttachmentRemove?: (id: string) => void
}

function isNearBottom(el: HTMLElement, threshold = 80): boolean {
  return el.scrollHeight - el.scrollTop - el.clientHeight < threshold
}

export function ChatPage({
  messages,
  isGenerating,
  onSend,
  onStop,
  onCopy,
  onRegenerate,
  onEditSubmit,
  onLike,
  onDislike,
  thinkingMode = 'balanced',
  onThinkingModeChange,
  attachments = [],
  onAttachmentAdd,
  onAttachmentRemove,
}: ChatPageProps) {
  const { t } = useTranslation()
  const scrollRef = useRef<HTMLDivElement>(null)
  const bottomRef = useRef<HTMLDivElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const userScrolledRef = useRef(false)
  const [showBackToBottom, setShowBackToBottom] = useState(false)
  const [editingMessageId, setEditingMessageId] = useState<number | null>(null)

  const handleAttachClick = useCallback(() => {
    fileInputRef.current?.click()
  }, [])

  const handleFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (files) {
      Array.from(files).forEach((f) => onAttachmentAdd?.(f))
    }
    e.target.value = ''
  }, [onAttachmentAdd])

  const scrollToBottom = useCallback((behavior: ScrollBehavior = 'smooth') => {
    bottomRef.current?.scrollIntoView({ behavior })
    userScrolledRef.current = false
    setShowBackToBottom(false)
  }, [])

  const handleScroll = useCallback(() => {
    const el = scrollRef.current
    if (!el) return
    const near = isNearBottom(el)
    userScrolledRef.current = !near
    setShowBackToBottom((prev) => prev !== !near ? !near : prev)
  }, [])

  useEffect(() => {
    if (!userScrolledRef.current) {
      scrollToBottom('smooth')
    }
  }, [messages, scrollToBottom])

  return (
    <>
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex-1 overflow-y-auto custom-scrollbar px-4 md:px-0"
      >
        <div className="max-w-3xl mx-auto w-full pt-8 pb-32">
          {messages.map((msg) => (
            <MessageBubble
              key={msg.id}
              role={msg.role}
              content={
                msg.role === 'assistant' && typeof msg.content === 'string'
                  ? <MarkdownRenderer content={msg.content} />
                  : msg.content
              }
              isStreaming={msg.status === 'streaming'}
              toolCalls={msg.toolCalls}
              thinkingBlock={
                msg.thinkingContent ? (
                  <ThinkingBlock
                    content={msg.thinkingContent}
                    isStreaming={msg.status === 'streaming'}
                  />
                ) : undefined
              }
              onCopy={() => onCopy?.(msg.id)}
              onRegenerate={msg.role === 'assistant' ? () => onRegenerate?.(msg.id) : undefined}
              onLike={msg.role === 'assistant' ? () => onLike?.(msg.id) : undefined}
              onDislike={msg.role === 'assistant' ? () => onDislike?.(msg.id) : undefined}
              liked={msg.feedbackType === 1}
              disliked={msg.feedbackType === 2}
              onEdit={msg.role === 'user' ? () => setEditingMessageId(msg.id) : undefined}
              isEditing={editingMessageId === msg.id}
              rawContent={typeof msg.content === 'string' ? msg.content : undefined}
              onEditSubmit={(newContent) => {
                onEditSubmit?.(msg.id, newContent)
                setEditingMessageId(null)
              }}
              onEditCancel={() => setEditingMessageId(null)}
              createdAt={msg.createdAt}
              isError={msg.status === 'error'}
              usage={msg.usage}
            />
          ))}
          <div ref={bottomRef} />
        </div>
      </div>

      {showBackToBottom && (
        <button
          onClick={() => scrollToBottom('smooth')}
          className="absolute bottom-32 right-6 z-30 w-10 h-10 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-full shadow-md flex items-center justify-center text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 transition-all"
          title={t('chat.backToBottom')}
        >
          <Icon name="keyboard_arrow_down" variant="outlined" size="xl" />
        </button>
      )}

      <div className="absolute bottom-0 left-0 w-full pb-6 pt-2 px-4 bg-gradient-to-t from-white via-white to-transparent dark:from-background-dark dark:via-background-dark z-20">
        <input
          ref={fileInputRef}
          type="file"
          multiple
          className="hidden"
          onChange={handleFileChange}
        />
        <ChatInput
          onSend={onSend}
          onStop={onStop}
          isGenerating={isGenerating}
          showThinkingToggle
          thinkingMode={thinkingMode}
          onThinkingModeChange={onThinkingModeChange}
          attachments={attachments}
          onAttachmentRemove={onAttachmentRemove}
          onAttachmentAdd={handleAttachClick}
        />
      </div>
    </>
  )
}
