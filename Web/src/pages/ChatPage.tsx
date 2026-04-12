import { useRef, useEffect, useCallback, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSettingsStore, useArtifactStore } from '@/stores'
import { Icon } from '@/components/common/Icon'
import { MessageBubble } from '@/components/chat/MessageBubble'
import { ChatInput } from '@/components/input/ChatInput'
import type { Message, Attachment, ToolCall } from '@/types'
import { MarkdownRenderer } from '@/components/chat/MarkdownRenderer'
import { ThinkingBlock } from '@/components/chat/ThinkingBlock'
import { ToolCallBadge } from '@/components/chat/ToolCallBadge'
import { ShareDialog } from '@/components/chat/ShareDialog'
import { DislikeReasonDialog } from '@/components/chat/DislikeReasonDialog'
import { ArtifactPanel } from '@/components/chat/ArtifactPanel'

type ThinkingMode = 'fast' | 'auto' | 'think'

interface ChatPageProps {
  messages: Message[]
  isGenerating: boolean
  isLoadingMessages?: boolean
  onSend: (message: string) => void
  onStop?: () => void
  onCopy?: (id: string) => void
  onRegenerate?: (id: string) => void
  onEditSubmit?: (id: string, content: string) => void
  onEditSaveOnly?: (id: string, content: string) => void
  onDelete?: (id: string) => void
  onLike?: (id: string) => void
  onDislike?: (id: string, reasons?: string[]) => void
  conversationId?: string | null
  thinkingMode?: ThinkingMode
  onThinkingModeChange?: (mode: ThinkingMode) => void
  supportsThinking?: boolean
  attachments?: Attachment[]
  onAttachmentAdd?: (file: File) => void
  onAttachmentRemove?: (id: number) => void
  sendShortcut?: 'Enter' | 'Ctrl+Enter'
  prefillValue?: string
  onPrefillConsumed?: () => void
}

function isNearBottom(el: HTMLElement, threshold = 80): boolean {
  return el.scrollHeight - el.scrollTop - el.clientHeight < threshold
}

export function ChatPage({
  messages,
  isGenerating,
  isLoadingMessages = false,
  onSend,
  onStop,
  onCopy,
  onRegenerate,
  onEditSubmit,
  onEditSaveOnly,
  onDelete,
  onLike,
  onDislike,
  conversationId,
  thinkingMode = 'auto',
  onThinkingModeChange,
  supportsThinking = false,
  attachments = [],
  onAttachmentAdd,
  onAttachmentRemove,
  sendShortcut = 'Enter',
  prefillValue,
  onPrefillConsumed,
}: ChatPageProps) {
  const { t } = useTranslation()
  const scrollRef = useRef<HTMLDivElement>(null)
  const bottomRef = useRef<HTMLDivElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const userScrolledRef = useRef(false)
  const [showBackToBottom, setShowBackToBottom] = useState(false)
  const contentWidth = useSettingsStore((s) => s.contentWidth) ?? 960
  const artifactOpen = useArtifactStore((s) => s.current !== null)
  const [editingMessageId, setEditingMessageId] = useState<string | null>(null)
  const [showShareDialog, setShowShareDialog] = useState(false)
  const [dislikeTargetId, setDislikeTargetId] = useState<string | null>(null)

  const handleAttachClick = useCallback(() => {
    fileInputRef.current?.click()
  }, [])

  const handlePasteFile = useCallback((file: File) => {
    onAttachmentAdd?.(file)
  }, [onAttachmentAdd])

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
  }, [])

  const handleBackToBottom = useCallback(() => {
    scrollToBottom('smooth')
    setShowBackToBottom(false)
  }, [scrollToBottom])

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
    <div className="relative flex flex-1 min-h-0">
    <div
      className="relative flex flex-col flex-1 min-h-0"
    >
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex-1 overflow-y-auto custom-scrollbar px-4 md:px-0"
      >
        <div className={`${contentWidth >= 1200 ? 'max-w-5xl' : contentWidth < 960 ? 'max-w-2xl' : 'max-w-3xl'} mx-auto w-full pt-8 pb-32`}>
          {isLoadingMessages && messages.length === 0 && (
            <div className="flex items-center justify-center py-12">
              <div className="w-5 h-5 border-2 border-primary/30 border-t-primary rounded-full animate-spin" />
              <span className="ml-3 text-sm text-gray-400">{t('common.loading')}</span>
            </div>
          )}
          {messages.map((msg) => {
            // 构建交错思考+工具调用块
            const hasSegments = msg.thinkingSegments && msg.thinkingSegments.length > 1
            let thinkingBlock: React.ReactNode = undefined
            let toolCallsForBubble: ToolCall[] | undefined = msg.toolCalls

            if (hasSegments && msg.thinkingSegments) {
              // 交错模式：思考段 → 工具调用 → 思考段 → ...
              const isLastSegmentStreaming = msg.status === 'streaming' && !msg.content
              thinkingBlock = (
                <>
                  {msg.thinkingSegments.map((seg, i) => (
                    <div key={`seg-${i}`}>
                      <ThinkingBlock
                        content={seg.content}
                        isStreaming={isLastSegmentStreaming && i === msg.thinkingSegments!.length - 1}
                        thinkingTime={seg.thinkingTime}
                      />
                      {i === 0 && msg.toolCalls && msg.toolCalls.length > 0 && (
                        <div className="flex items-center flex-wrap gap-2 mb-4">
                          {msg.toolCalls.map((tc) => (
                            <ToolCallBadge key={tc.id} name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} />
                          ))}
                        </div>
                      )}
                    </div>
                  ))}
                </>
              )
              toolCallsForBubble = undefined
            } else if (msg.thinkingContent) {
              thinkingBlock = (
                <ThinkingBlock
                  content={msg.thinkingContent}
                  isStreaming={msg.status === 'streaming' && !msg.content}
                  thinkingTime={msg.thinkingTime}
                />
              )
            }

            return (
            <MessageBubble
              key={msg.id}
              role={msg.role}
              content={
                msg.role === 'assistant' && typeof msg.content === 'string'
                  ? <MarkdownRenderer content={msg.content} isStreaming={msg.status === 'streaming'} />
                  : msg.content
              }
              isStreaming={msg.status === 'streaming'}
              toolCalls={toolCallsForBubble}
              thinkingBlock={thinkingBlock}
              attachments={msg.attachments}
              onCopy={() => onCopy?.(msg.id)}
              onRegenerate={msg.role === 'assistant' ? () => onRegenerate?.(msg.id) : undefined}
              onLike={msg.role === 'assistant' ? () => onLike?.(msg.id) : undefined}
              onDislike={msg.role === 'assistant' ? () => {
                const isAlreadyDisliked = msg.feedbackType === 2
                if (isAlreadyDisliked) {
                  onDislike?.(msg.id)
                } else {
                  setDislikeTargetId(msg.id)
                }
              } : undefined}
              liked={msg.feedbackType === 1}
              disliked={msg.feedbackType === 2}
              onEdit={msg.role === 'user' ? () => setEditingMessageId(msg.id) : undefined}
              isEditing={editingMessageId === msg.id}
              rawContent={typeof msg.content === 'string' ? msg.content : undefined}
              onEditSubmit={(newContent) => {
                onEditSubmit?.(msg.id, newContent)
                setEditingMessageId(null)
              }}
              onEditSaveOnly={msg.role === 'user' ? (newContent) => {
                onEditSaveOnly?.(msg.id, newContent)
                setEditingMessageId(null)
              } : undefined}
              onEditCancel={() => setEditingMessageId(null)}
              onDelete={!isGenerating ? () => onDelete?.(msg.id) : undefined}
              onShare={msg.role === 'assistant' ? () => setShowShareDialog(true) : undefined}
              createdAt={msg.createdAt}
              isError={msg.status === 'error'}
              usage={msg.usage}
              model={msg.model}
            />
            )
          })}
          <div ref={bottomRef} />
        </div>
      </div>

      {showBackToBottom && (
        <button
          onClick={handleBackToBottom}
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
          showThinkingToggle={supportsThinking}
          thinkingMode={thinkingMode}
          onThinkingModeChange={onThinkingModeChange}
          attachments={attachments}
          onAttachmentRemove={onAttachmentRemove}
          onAttachmentAdd={handleAttachClick}
          onFilePaste={handlePasteFile}
          sendShortcut={sendShortcut}
          prefillValue={prefillValue}
          onPrefillConsumed={onPrefillConsumed}
        />
      </div>

      <DislikeReasonDialog
        open={dislikeTargetId !== null}
        onClose={() => setDislikeTargetId(null)}
        onSubmit={(reasons) => {
          if (dislikeTargetId !== null) {
            onDislike?.(dislikeTargetId, reasons)
            setDislikeTargetId(null)
          }
        }}
      />

      {conversationId && (
        <ShareDialog
          open={showShareDialog}
          onClose={() => setShowShareDialog(false)}
          conversationId={conversationId}
        />
      )}
    </div>
    {artifactOpen && <ArtifactPanel />}
    </div>
  )
}
