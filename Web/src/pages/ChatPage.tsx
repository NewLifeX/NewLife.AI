import { useRef, useEffect, useCallback, useState, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useSettingsStore } from '@/stores'
import { Icon } from '@/components/common/Icon'
import { MessageBubble } from '@/components/chat/MessageBubble'
import { ChatInput } from '@/components/input/ChatInput'
import type { Message, Attachment, ToolCall } from '@/types'
import { MarkdownRenderer } from '@/components/chat/MarkdownRenderer'
import { ThinkingBlock } from '@/components/chat/ThinkingBlock'
import { ToolCallBadge } from '@/components/chat/ToolCallBadge'
import { ShareDialog } from '@/components/chat/ShareDialog'
import { DislikeReasonDialog } from '@/components/chat/DislikeReasonDialog'

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

/** 构建消息的推理块（单段或交错多段），供消息内联与页面级右侧推理栏复用 */
function buildThinkingBlock(
  msg: Message,
  defaultCollapsed: boolean,
  onLayoutToggle?: () => void,
  sideLayout = false,
): React.ReactNode {
  const hasSegments = msg.thinkingSegments && msg.thinkingSegments.length > 1
  if (hasSegments && msg.thinkingSegments) {
    // 交错模式：思考段与工具调用按时间线交织，整体作为 thinkingBlock
    const isLastSegmentStreaming = msg.status === 'streaming' && !msg.content
    return (
      <>
        {msg.thinkingSegments.map((seg, i) => (
          <div key={`seg-${i}`}>
            <ThinkingBlock
              content={seg.content}
              isStreaming={isLastSegmentStreaming && i === msg.thinkingSegments!.length - 1}
              thinkingTime={seg.thinkingTime}
              defaultCollapsed={defaultCollapsed}
              onLayoutToggle={i === 0 ? onLayoutToggle : undefined}
              sideLayout={sideLayout}
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
  }
  if (msg.thinkingContent) {
    return (
      <ThinkingBlock
        content={msg.thinkingContent}
        isStreaming={msg.status === 'streaming' && !msg.content}
        thinkingTime={msg.thinkingTime}
        defaultCollapsed={defaultCollapsed}
        onLayoutToggle={onLayoutToggle}
        sideLayout={sideLayout}
      />
    )
  }
  return undefined
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
  const thinkingScrollRef = useRef<HTMLDivElement>(null)
  const userScrolledRef = useRef(false)
  const contentWidth = useSettingsStore((s) => s.contentWidth) ?? 960
  const updateSettings = useSettingsStore((s) => s.update)
  // 推理过程布局（ThinkingLayout 枚举）：0=默认(上方折叠) 1=上方折叠 2=上方展开 3=右侧分栏
  const thinkingLayoutNum = useSettingsStore((s) => s.thinkingLayout) ?? 0
  const thinkingLayout = thinkingLayoutNum === 3 ? 'side' : 'above'
  // 消息内联折叠条的默认折叠状态：0/1/3=折叠，2=上方展开
  const thinkingCollapsed = thinkingLayoutNum !== 2
  // 右侧推理栏宽度（拖动记忆，localStorage，不进后端设置避免高频写库）
  const [panelWidth, setPanelWidth] = useState(() => {
    const w = Number(localStorage.getItem('thinkingPanelWidth'))
    return w >= 320 && w <= 600 ? w : 400
  })
  const handleToggleThinkingLayout = useCallback(() => {
    // 右侧分栏(3) ↔ 上方折叠(1)
    updateSettings({ thinkingLayout: thinkingLayoutNum === 3 ? 1 : 3 })
  }, [thinkingLayoutNum, updateSettings])
  // 拖动分隔线调整推理栏宽度（320~600px）
  const handleResizeStart = useCallback(() => {
    const onMove = (ev: MouseEvent) => {
      const w = window.innerWidth - ev.clientX
      const clamped = Math.min(600, Math.max(320, w))
      setPanelWidth(clamped)
      localStorage.setItem('thinkingPanelWidth', String(clamped))
    }
    const onUp = () => {
      document.removeEventListener('mousemove', onMove)
      document.removeEventListener('mouseup', onUp)
      document.body.style.cursor = ''
    }
    document.body.style.cursor = 'col-resize'
    document.addEventListener('mousemove', onMove)
    document.addEventListener('mouseup', onUp)
  }, [])
  // 右侧分栏：取最后一条带推理的助手消息，渲染到页面级侧栏（跟随最新，流式实时更新）
  const latestThinkingMsg = useMemo(() => {
    if (thinkingLayout !== 'side') return undefined
    for (let i = messages.length - 1; i >= 0; i--) {
      const m = messages[i]
      if (m.role === 'assistant' && (m.thinkingContent || (m.thinkingSegments && m.thinkingSegments.length > 0))) return m
    }
    return undefined
  }, [messages, thinkingLayout])
  const latestThinkingBlock = latestThinkingMsg
    ? buildThinkingBlock(latestThinkingMsg, false, handleToggleThinkingLayout, true)
    : null
  // 推理内容长度指纹：流式增长时驱动右侧推理栏自动滚动到底部
  const thinkingFingerprint = latestThinkingMsg
    ? (latestThinkingMsg.thinkingContent?.length ?? 0) + (latestThinkingMsg.thinkingSegments?.reduce((n, s) => n + s.content.length, 0) ?? 0)
    : 0
  useEffect(() => {
    if (thinkingLayout !== 'side' || !isGenerating) return
    const el = thinkingScrollRef.current
    if (el) el.scrollTop = el.scrollHeight
  }, [thinkingFingerprint, thinkingLayout, isGenerating])
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
    const el = scrollRef.current
    if (!el) return
    el.scrollTo({ top: el.scrollHeight, behavior })
    userScrolledRef.current = false
  }, [])

  const handleScroll = useCallback(() => {
    const el = scrollRef.current
    if (!el) return
    userScrolledRef.current = !isNearBottom(el)
  }, [])

  const navigateMessages = useCallback((direction: 'up' | 'down') => {
    const el = scrollRef.current
    if (!el) return
    const role = direction === 'up' ? 'user' : 'assistant'
    const items = Array.from(el.querySelectorAll<HTMLElement>(`[data-role="${role}"]`))
    if (items.length === 0) return
    if (direction === 'up') {
      const threshold = el.scrollTop - 10
      let target: HTMLElement | undefined
      for (let i = items.length - 1; i >= 0; i--) {
        if (items[i].offsetTop < threshold) {
          target = items[i]
          break
        }
      }
      if (target) el.scrollTo({ top: target.offsetTop, behavior: 'smooth' })
    } else {
      const viewportBottom = el.scrollTop + el.clientHeight
      let target: HTMLElement | undefined
      for (const item of items) {
        const itemBottom = item.offsetTop + item.offsetHeight
        if (itemBottom > viewportBottom + 1) {
          target = item
          break
        }
      }
      if (target) {
        const targetScrollTop = target.offsetTop + target.offsetHeight - el.clientHeight
        el.scrollTo({ top: Math.max(0, targetScrollTop), behavior: 'smooth' })
      } else {
        // 最后一条助手消息已完全可见或接近末尾，直接滚到容器底部
        el.scrollTo({ top: el.scrollHeight, behavior: 'smooth' })
      }
    }
  }, [])

  useEffect(() => {
    if (!userScrolledRef.current) {
      // 流式输出期间用 instant，避免每个 SSE token 触发 smooth 动画积压导致视觉抖动
      scrollToBottom(isGenerating ? 'instant' : 'smooth')
    }
  }, [messages, scrollToBottom, isGenerating])

  return (
    <div className="relative flex flex-1 min-h-0 min-w-0">
      <div
        className="relative flex flex-col flex-1 min-h-0 min-w-0"
      >
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex-1 overflow-y-auto overflow-x-hidden custom-scrollbar"
      >
        <div className={`${contentWidth >= 1200 ? 'max-w-5xl' : contentWidth < 960 ? 'max-w-2xl' : 'max-w-3xl'} mx-auto w-full pt-8 pb-32 px-4`}>
          {isLoadingMessages && messages.length === 0 && (
            <div className="flex items-center justify-center py-12">
              <div className="w-5 h-5 border-2 border-primary/30 border-t-primary rounded-full animate-spin" />
              <span className="ml-3 text-sm text-gray-400">{t('common.loading')}</span>
            </div>
          )}
          {messages.map((msg) => {
            // 构建推理块（单段或交错多段），消息内联展示
            const hasSegments = msg.thinkingSegments && msg.thinkingSegments.length > 1
            let thinkingBlock: React.ReactNode = undefined
            let toolCallsForBubble: ToolCall[] | undefined = msg.toolCalls

            if (hasSegments && msg.thinkingSegments) {
              // 交错模式：思考段与工具调用按时间线交织，整体作为 thinkingBlock
              thinkingBlock = buildThinkingBlock(msg, thinkingCollapsed, handleToggleThinkingLayout, thinkingLayout === 'side')
              toolCallsForBubble = undefined
            } else if (msg.thinkingContent) {
              thinkingBlock = buildThinkingBlock(msg, thinkingCollapsed, handleToggleThinkingLayout, thinkingLayout === 'side')
            }

            return (
            <div key={msg.id} data-role={msg.role} data-message-id={msg.id}>
            <MessageBubble
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
            </div>
            )
          })}
          <div ref={bottomRef} />
        </div>
      </div>

      <div className="absolute bottom-32 right-6 z-40 flex flex-col gap-1.5">
        <button
          type="button"
          onClick={() => navigateMessages('up')}
          className="w-10 h-10 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-full shadow-md flex items-center justify-center text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 transition-all"
          title={t('chat.prevUserMessage')}
        >
          <Icon name="keyboard_arrow_up" variant="outlined" size="xl" />
        </button>
        <button
          type="button"
          onClick={() => navigateMessages('down')}
          className="w-10 h-10 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-full shadow-md flex items-center justify-center text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-700 transition-all"
          title={t('chat.nextAssistantMessage')}
        >
          <Icon name="keyboard_arrow_down" variant="outlined" size="xl" />
        </button>
      </div>

      <div className="absolute bottom-0 left-0 w-full pb-input pt-2 px-4 max-md:px-2 bg-gradient-to-t from-white via-white to-transparent dark:from-background-dark dark:via-background-dark z-30">
        <input
          ref={fileInputRef}
          type="file"
          multiple
          className="hidden"
          accept=".jpg,.jpeg,.png,.gif,.webp,.pdf,.docx,.doc,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv"
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
      {thinkingLayout === 'side' && latestThinkingBlock && (
        <aside
          className="relative hidden lg:flex flex-col min-h-0 shrink-0 border-l border-[var(--color-border-subtle)] bg-[var(--color-surface-1)]"
          data-testid="thinking-panel"
          style={{ width: panelWidth }}
        >
          {/* 拖动分隔线：调整推理栏宽度 320~600px */}
          <div
            onMouseDown={handleResizeStart}
            className="absolute -left-1.5 top-0 bottom-0 w-3 cursor-col-resize hover:bg-primary/20 active:bg-primary/40 transition-colors"
            title={t('chat.thinkingResizeTip')}
          />
          <div className="flex items-center gap-2 px-4 py-3 border-b border-[var(--color-border-subtle)]">
            <Icon name="psychology" variant="outlined" size="sm" className="text-blue-600 dark:text-blue-400" />
            <span className="text-sm font-medium text-[var(--color-text-primary)]">{t('chat.thinkingProcess')}</span>
            <span className="ml-auto" />
            <button
              type="button"
              onClick={handleToggleThinkingLayout}
              title={t('chat.thinkingLayoutRestoreTip')}
              className="flex items-center justify-center w-7 h-7 text-xs text-[var(--color-text-tertiary)] hover:bg-[var(--color-surface-2)] rounded-lg transition-colors"
              data-testid="thinking-layout-toggle"
            >
              <Icon name="view_agenda" variant="outlined" size="sm" />
            </button>
          </div>
          <div ref={thinkingScrollRef} className="flex-1 min-h-0 overflow-y-auto px-4 py-3 custom-scrollbar">
            {latestThinkingBlock}
          </div>
        </aside>
      )}
    </div>
  )
}
