import { type ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { Avatar } from '@/components/common/Avatar'
import { MessageActions } from './MessageActions'
import { TypingCursor } from './TypingCursor'
import { ThinkingIndicator } from './ThinkingIndicator'
import { ToolCallBadge } from './ToolCallBadge'
import type { ToolCall } from '@/types'

interface MessageBubbleProps {
  role: 'user' | 'assistant'
  content: ReactNode
  userAvatar?: string
  isStreaming?: boolean
  thinkingLabel?: string
  toolCalls?: ToolCall[]
  onCopy?: () => void
  onRegenerate?: () => void
  onDislike?: () => void
  onEdit?: () => void
  className?: string
}

export function MessageBubble({
  role,
  content,
  userAvatar,
  isStreaming = false,
  thinkingLabel,
  toolCalls,
  onCopy,
  onRegenerate,
  onDislike,
  onEdit,
  className,
}: MessageBubbleProps) {
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
                className="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded transition-colors"
              >
                <span className="material-icons text-base">edit</span>
              </button>
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
        <div className="bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700/50 rounded-2xl rounded-tl-sm px-6 py-5 text-[15px] leading-7 shadow-soft text-gray-900 dark:text-gray-100">
          {toolCalls && toolCalls.length > 0 && (
            <div className="flex items-center flex-wrap gap-2 mb-4">
              {toolCalls.map((tc) => (
                <ToolCallBadge key={tc.id} name={tc.name} status={tc.status} />
              ))}
            </div>
          )}

          <div className="prose prose-sm max-w-none dark:prose-invert">
            {content}
          </div>

          {isStreaming && (
            <div className="mt-1">
              <TypingCursor />
            </div>
          )}

          {thinkingLabel && (
            <ThinkingIndicator label={thinkingLabel} className="mt-4" />
          )}
        </div>

        <MessageActions
          className="mt-2"
          onCopy={onCopy}
          onRegenerate={onRegenerate}
          onDislike={onDislike}
        />
      </div>
    </div>
  )
}
