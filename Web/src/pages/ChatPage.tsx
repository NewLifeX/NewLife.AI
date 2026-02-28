import { useRef, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { ScrollArea } from '@/components/common/ScrollArea'
import { MessageBubble } from '@/components/chat/MessageBubble'
import { ChatInput } from '@/components/input/ChatInput'
import type { Message } from '@/types'

interface ChatPageProps {
  messages: Message[]
  isGenerating: boolean
  onSend: (message: string) => void
  onStop?: () => void
  onCopy?: (id: number) => void
  onRegenerate?: (id: number) => void
  onEdit?: (id: number) => void
}

export function ChatPage({
  messages,
  isGenerating,
  onSend,
  onStop,
  onCopy,
  onRegenerate,
  onEdit,
}: ChatPageProps) {
  const { t } = useTranslation()
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  return (
    <>
      <ScrollArea className="flex-1 px-4 md:px-0">
        <div className="max-w-3xl mx-auto w-full pt-8 pb-32">
          {messages.map((msg) => (
            <MessageBubble
              key={msg.id}
              role={msg.role}
              content={msg.content}
              isStreaming={msg.status === 'streaming'}
              toolCalls={msg.toolCalls}
              thinkingLabel={msg.thinkingContent ? t('chat.thinkingDeep') : undefined}
              onCopy={() => onCopy?.(msg.id)}
              onRegenerate={msg.role === 'assistant' ? () => onRegenerate?.(msg.id) : undefined}
              onEdit={msg.role === 'user' ? () => onEdit?.(msg.id) : undefined}
            />
          ))}
          <div ref={bottomRef} />
        </div>
      </ScrollArea>

      <div className="absolute bottom-0 left-0 w-full pb-6 pt-2 px-4 bg-gradient-to-t from-white via-white to-transparent dark:from-background-dark dark:via-background-dark z-20">
        <ChatInput
          onSend={onSend}
          onStop={onStop}
          isGenerating={isGenerating}
          showThinkingToggle
        />
      </div>
    </>
  )
}
