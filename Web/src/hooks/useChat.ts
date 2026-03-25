/**
 * useChat — 对话流式 Hook
 *
 * 参考 Vercel AI SDK useChat 设计，整合现有 fetchSSE 基础设施与 ChatStreamEvent 协议。
 *
 * @example
 * ```tsx
 * const { messages, isLoading, append, stop } = useChat({ conversationId: 'xxx' })
 * // 发送消息
 * append('你好')
 * // 页面展示
 * messages.map(m => <div key={m.id}>{m.content}</div>)
 * ```
 */

import { useState, useRef, useCallback } from 'react'
import { streamMessage } from '@/lib/api'
import type { ChatStreamEvent } from '@/lib/api'
import type { Message } from '@/types'

// ── 类型 ──────────────────────────────────────────────────────────────────────

/** useChat Hook 初始化选项 */
export interface UseChatOptions {
  /** 关联的会话 Id */
  conversationId: string

  /** 初始消息列表 */
  initialMessages?: Message[]

  /** 思考模式（0=Auto 1=Think 2=Fast）*/
  thinkingMode?: number

  /** 模型 Id（覆盖会话默认值）*/
  modelId?: number

  /** 流结束回调 */
  onFinish?: (message: Message) => void

  /** 错误回调 */
  onError?: (error: Error) => void
}

/** useChat Hook 返回值 */
export interface UseChatReturn {
  /** 当前消息列表（含 user + assistant 消息）*/
  messages: Message[]

  /** 是否正在接收流 */
  isLoading: boolean

  /** 最近一次错误 */
  error: Error | null

  /** 追加用户消息并触发 AI 回复 */
  append: (content: string, options?: AppendOptions) => Promise<void>

  /** 重新生成最后一条 AI 回复（删除后重发最后一条 user 消息）*/
  reload: () => Promise<void>

  /** 中止当前流 */
  stop: () => void

  /** 直接覆盖消息列表（用于编辑场景）*/
  setMessages: (messages: Message[]) => void
}

interface AppendOptions {
  /** 附件 Id 列表 */
  attachmentIds?: string[]
}

// ── 工具函数 ──────────────────────────────────────────────────────────────────

function makeId(): string {
  return `msg-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

function makeUserMessage(conversationId: string, content: string): Message {
  return {
    id: makeId(),
    conversationId,
    role: 'user',
    content,
    createdAt: new Date().toISOString(),
    status: 'done',
  }
}

function makeAssistantPlaceholder(conversationId: string): Message {
  return {
    id: makeId(),
    conversationId,
    role: 'assistant',
    content: '',
    createdAt: new Date().toISOString(),
    status: 'streaming',
  }
}

// ── Hook ──────────────────────────────────────────────────────────────────────

export function useChat(options: UseChatOptions): UseChatReturn {
  const {
    conversationId,
    initialMessages = [],
    thinkingMode = 0,
    modelId,
    onFinish,
    onError,
  } = options

  const [messages, setMessages] = useState<Message[]>(initialMessages)
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  const abortRef = useRef<AbortController | null>(null)

  /** 内部流式发送逻辑 */
  const sendStream = useCallback(
    async (userContent: string, appendOptions?: AppendOptions) => {
      const controller = new AbortController()
      abortRef.current = controller

      const userMsg = makeUserMessage(conversationId, userContent)
      const assistantMsg = makeAssistantPlaceholder(conversationId)

      setMessages((prev) => [...prev, userMsg, assistantMsg])
      setIsLoading(true)
      setError(null)

      const updateAssistant = (updater: (msg: Message) => Message) => {
        setMessages((prev) =>
          prev.map((m) => (m.id === assistantMsg.id ? updater(m) : m)),
        )
      }

      try {
        await streamMessage(
          conversationId,
          userContent,
          thinkingMode,
          (event: ChatStreamEvent) => {
            switch (event.type) {
              case 'message_start':
                if (event.messageId)
                  updateAssistant((m) => ({ ...m, id: event.messageId! }))
                break

              case 'content_delta':
                if (event.content)
                  updateAssistant((m) => ({
                    ...m,
                    content: m.content + event.content,
                  }))
                break

              case 'thinking_delta':
                if (event.content)
                  updateAssistant((m) => ({
                    ...m,
                    thinkingContent: (m.thinkingContent ?? '') + event.content,
                  }))
                break

              case 'message_done': {
                const finalMsg: Partial<Message> = { status: 'done' }
                if (event.usage) finalMsg.usage = event.usage
                updateAssistant((m) => ({ ...m, ...finalMsg }))
                break
              }

              case 'error':
                updateAssistant((m) => ({
                  ...m,
                  status: 'error',
                  content: m.content || (event.error ?? '发生错误'),
                }))
                break
            }
          },
          controller.signal,
          appendOptions?.attachmentIds,
          modelId,
        )

        // 回调 onFinish
        if (onFinish) {
          setMessages((prev) => {
            const last = prev[prev.length - 1]
            if (last.role === 'assistant') onFinish(last)
            return prev
          })
        }
      } catch (err) {
        if (err instanceof DOMException && err.name === 'AbortError') {
          // 用户主动中止，标记为 done
          updateAssistant((m) => ({ ...m, status: 'done' }))
          return
        }
        const e = err instanceof Error ? err : new Error(String(err))
        setError(e)
        updateAssistant((m) => ({ ...m, status: 'error' }))
        onError?.(e)
      } finally {
        setIsLoading(false)
        abortRef.current = null
      }
    },
    [conversationId, thinkingMode, modelId, onFinish, onError],
  )

  const append = useCallback(
    (content: string, appendOptions?: AppendOptions) => sendStream(content, appendOptions),
    [sendStream],
  )

  const reload = useCallback(async () => {
    // 找到最后一条 user 消息重发
    const lastUser = [...messages].reverse().find((m) => m.role === 'user')
    if (!lastUser) return
    // 移除最后一条 assistant 消息
    setMessages((prev) => {
      let idx = -1
      for (let i = prev.length - 1; i >= 0; i--) {
        if (prev[i].role === 'assistant') { idx = i; break }
      }
      return idx >= 0 ? prev.filter((_, i) => i !== idx) : prev
    })
    await sendStream(lastUser.content)
  }, [messages, sendStream])

  const stop = useCallback(() => {
    abortRef.current?.abort()
  }, [])

  return {
    messages,
    isLoading,
    error,
    append,
    reload,
    stop,
    setMessages,
  }
}
