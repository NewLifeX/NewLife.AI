import { type ReactNode, useState, useRef, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { cn, formatRelativeTime, formatExactTime } from '@/lib/utils'
import { Avatar } from '@/components/common/Avatar'
import { Icon } from '@/components/common/Icon'
import { MessageActions } from './MessageActions'
import { TypingCursor } from './TypingCursor'
import { ToolCallBadge } from './ToolCallBadge'
import { ActionSheet, type ActionSheetItem } from '@/components/common/ActionSheet'
import { useLongPress } from '@/hooks/useLongPress'
import type { ToolCall, TokenUsage } from '@/types'

interface MessageBubbleProps {
  role: 'user' | 'assistant'
  content: ReactNode
  userAvatar?: string
  isStreaming?: boolean
  thinkingBlock?: ReactNode
  toolCalls?: ToolCall[]
  onCopy?: () => void
  onRegenerate?: () => void
  onLike?: () => void
  onDislike?: () => void
  onShare?: () => void
  liked?: boolean
  disliked?: boolean
  onEdit?: () => void
  onEditSubmit?: (content: string) => void
  onEditCancel?: () => void
  isEditing?: boolean
  rawContent?: string
  createdAt?: string
  isError?: boolean
  usage?: TokenUsage
  className?: string
}

export function MessageBubble({
  role,
  content,
  isStreaming = false,
  thinkingBlock,
  toolCalls,
  onCopy,
  onRegenerate,
  onLike,
  onDislike,
  onShare,
  liked = false,
  disliked = false,
  onEdit,
  onEditSubmit,
  onEditCancel,
  isEditing = false,
  rawContent,
  createdAt,
  isError = false,
  usage,
  className,
}: MessageBubbleProps) {
  const { t, i18n } = useTranslation()
  const locale = i18n.language
  const [editValue, setEditValue] = useState(rawContent ?? '')
  const editRef = useRef<HTMLTextAreaElement>(null)

  // 移动端长按操作
  const [actionSheetOpen, setActionSheetOpen] = useState(false)
  const [actionSheetPos, setActionSheetPos] = useState<{ x: number; y: number }>({ x: 0, y: 0 })

  const handleLongPress = useCallback(
    (e: TouchEvent | MouseEvent) => {
      const pos = 'touches' in e ? { x: e.touches[0].clientX, y: e.touches[0].clientY } : { x: e.clientX, y: e.clientY }
      setActionSheetPos(pos)
      setActionSheetOpen(true)
    },
    [],
  )

  const longPressHandlers = useLongPress({ onLongPress: handleLongPress })

  const mobileActions: ActionSheetItem[] = []
  if (onCopy) mobileActions.push({ icon: 'content_copy', label: t('common.copy'), onClick: onCopy })
  if (role === 'user' && onEdit) mobileActions.push({ icon: 'edit', label: t('common.edit'), onClick: onEdit })
  if (role === 'assistant' && onRegenerate) mobileActions.push({ icon: 'refresh', label: t('common.regenerate'), onClick: onRegenerate })
  if (onShare) mobileActions.push({ icon: 'share', label: t('common.share'), onClick: onShare })

  if (role === 'user') {
    return (
      <div className={cn('flex flex-col items-end mb-6 group', className)} {...longPressHandlers}>
        <div className="max-w-[75%] relative">
          {isEditing ? (
            <div className="bg-gray-100 dark:bg-gray-800 rounded-2xl rounded-tr-sm px-4 py-3 shadow-sm">
              <textarea
                ref={editRef}
                value={editValue}
                onChange={(e) => setEditValue(e.target.value)}
                className="w-full bg-transparent text-gray-900 dark:text-gray-100 text-[15px] leading-7 resize-none outline-none min-h-[60px]"
                rows={Math.max(2, editValue.split('\n').length)}
                autoFocus
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault()
                    if (editValue.trim()) onEditSubmit?.(editValue.trim())
                  }
                  if (e.key === 'Escape') onEditCancel?.()
                }}
              />
              <div className="flex justify-end space-x-2 mt-2">
                <button
                  onClick={onEditCancel}
                  className="px-3 py-1 text-xs text-gray-500 hover:text-gray-700 dark:hover:text-gray-300 rounded-md hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors"
                >
                  {t('common.cancel')}
                </button>
                <button
                  onClick={() => editValue.trim() && onEditSubmit?.(editValue.trim())}
                  className="px-3 py-1 text-xs text-white bg-primary hover:bg-primary/90 rounded-md transition-colors disabled:opacity-50"
                  disabled={!editValue.trim()}
                >
                  {t('common.send')}
                </button>
              </div>
            </div>
          ) : (
            <>
              <div className="bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-gray-100 rounded-2xl rounded-tr-sm px-5 py-3.5 leading-7 shadow-sm" style={{ fontSize: 'var(--chat-font-size, 16px)' }}>
                {content}
              </div>
              {onEdit && (
                <div className="absolute -left-10 top-2 hidden group-hover:flex space-x-1">
                  <button
                    onClick={onEdit}
                    className="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
                  >
                    <Icon name="edit" variant="filled" size="base" />
                  </button>
                </div>
              )}
            </>
          )}
          {createdAt && !isEditing && (
            <div className="mt-1 text-right">
              <span className="text-[11px] text-gray-400 dark:text-gray-500 cursor-default" title={formatExactTime(createdAt)}>
                {formatRelativeTime(createdAt, locale)}
              </span>
            </div>
          )}
        </div>
        <ActionSheet open={actionSheetOpen} onClose={() => setActionSheetOpen(false)} items={mobileActions} position={actionSheetPos} />
      </div>
    )
  }

  return (
    <div className={cn('mb-8 group w-full', className)} {...longPressHandlers}>
      <div className="flex items-center gap-2 mb-3">
        <Avatar type="ai" size="sm" />
      </div>
      <div className="w-full">
        <div
          className={cn(
            'leading-7',
            isError
              ? 'bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/50 rounded-xl px-4 py-3 text-red-700 dark:text-red-400'
              : 'text-gray-900 dark:text-gray-100',
          )}
          style={{ fontSize: 'var(--chat-font-size, 16px)' }}
        >
          {thinkingBlock}

          {toolCalls && toolCalls.length > 0 && (
            <div className="flex items-center flex-wrap gap-2 mb-4">
              {toolCalls.map((tc) => (
                <ToolCallBadge key={tc.id} name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} />
              ))}
            </div>
          )}

          <div className="max-w-none">
            {content}
          </div>

          {isStreaming && (
            <div className="mt-1">
              <TypingCursor />
            </div>
          )}
        </div>

        <div className="flex items-center mt-2">
          <MessageActions
            onCopy={onCopy}
            onLike={onLike}
            onRegenerate={onRegenerate}
            onDislike={onDislike}
            onShare={onShare}
            liked={liked}
            disliked={disliked}
            className="mt-0"
          />
          <div className="ml-auto flex items-center space-x-2 mr-1">
            {usage && usage.totalTokens != null && (
              <span className="text-[11px] text-gray-400 dark:text-gray-500 cursor-default" title={`${t('chat.promptTokens')}: ${usage.promptTokens ?? 0} | ${t('chat.completionTokens')}: ${usage.completionTokens ?? 0}`}>
                {usage.promptTokens != null && usage.completionTokens != null
                  ? `${usage.promptTokens} + ${usage.completionTokens} = ${usage.totalTokens} tokens`
                  : `${usage.totalTokens} tokens`}
              </span>
            )}
            {createdAt && (
              <span className="text-[11px] text-gray-400 dark:text-gray-500 cursor-default" title={formatExactTime(createdAt)}>
                {formatRelativeTime(createdAt, locale)}
              </span>
            )}
          </div>
        </div>
      </div>
      <ActionSheet open={actionSheetOpen} onClose={() => setActionSheetOpen(false)} items={mobileActions} position={actionSheetPos} />
    </div>
  )
}
