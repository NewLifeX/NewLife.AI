import { type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { cn, formatRelativeTime, formatExactTime } from '@/lib/utils'
import { Avatar } from '@/components/common/Avatar'
import { Icon } from '@/components/common/Icon'
import { MessageActions } from './MessageActions'
import { TypingCursor } from './TypingCursor'
import { ToolCallBadge } from './ToolCallBadge'
import type { ToolCall } from '@/types'

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
  onEdit?: () => void
  createdAt?: string
  isError?: boolean
  className?: string
}

export function MessageBubble({
  role,
  content,
  userAvatar,
  isStreaming = false,
  thinkingBlock,
  toolCalls,
  onCopy,
  onRegenerate,
  onLike,
  onDislike,
  onEdit,
  createdAt,
  isError = false,
  className,
}: MessageBubbleProps) {
  const { i18n } = useTranslation()
  const locale = i18n.language

  if (role === 'user') {
    return (
      <div className={cn('flex flex-row-reverse items-start mb-8 group', className)}>
        <div className="flex-shrink-0 ml-3">
          <Avatar type="user" src={userAvatar} size="md" />
        </div>
        <div className="max-w-[85%] relative">
          <div className="bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-gray-100 rounded-2xl rounded-tr-sm px-5 py-3.5 text-[15px] leading-7 shadow-sm">
            {content}
          </div>
          {onEdit && (
            <div className="absolute -left-12 top-2 hidden group-hover:flex space-x-1">
              <button
                onClick={onEdit}
                className="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
              >
                <Icon name="edit" variant="filled" size="base" />
              </button>
            </div>
          )}
          {createdAt && (
            <div className="mt-1 text-right">
              <span className="text-[11px] text-gray-400 dark:text-gray-500 cursor-default" title={formatExactTime(createdAt)}>
                {formatRelativeTime(createdAt, locale)}
              </span>
            </div>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className={cn('flex items-start mb-6 group w-full', className)}>
      <div className="flex-shrink-0 mr-3">
        <Avatar type="ai" size="md" />
      </div>
      <div className="flex-1 min-w-0">
        <div className={cn(
          'rounded-2xl rounded-tl-sm px-6 py-5 text-[15px] leading-7 shadow-soft',
          isError
            ? 'bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/50 text-red-700 dark:text-red-400'
            : 'bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700/50 text-gray-900 dark:text-gray-100',
        )}>
          {toolCalls && toolCalls.length > 0 && (
            <div className="flex items-center flex-wrap gap-2 mb-4">
              {toolCalls.map((tc) => (
                <ToolCallBadge key={tc.id} name={tc.name} status={tc.status} />
              ))}
            </div>
          )}

          {thinkingBlock}

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
            className="mt-0"
          />
          {createdAt && (
            <span className="ml-auto text-[11px] text-gray-400 dark:text-gray-500 cursor-default mr-1" title={formatExactTime(createdAt)}>
              {formatRelativeTime(createdAt, locale)}
            </span>
          )}
        </div>
      </div>
    </div>
  )
}
