/**
 * useCompletion — 文本补全流式 Hook
 *
 * 参考 Vercel AI SDK useCompletion 设计，接受单次文本 prompt，
 * 通过 SSE 流式接收 content_delta 并累加为 completion 字符串。
 *
 * @example
 * ```tsx
 * const { completion, isLoading, complete, stop } = useCompletion({
 *   conversationId: 'xxx',
 * })
 * complete('帮我写一首五言绝句')
 * // completion 随流式响应逐渐填充
 * ```
 */

import { useState, useRef, useCallback } from 'react'
import { streamMessage } from '@/lib/api'
import type { ChatStreamEvent } from '@/lib/api'

// ── 类型 ──────────────────────────────────────────────────────────────────────

/** useCompletion Hook 初始化选项 */
export interface UseCompletionOptions {
  /** 关联的会话 Id（复用现有 API 端点）*/
  conversationId: string

  /** 思考模式（0=Auto 1=Think 2=Fast）*/
  thinkingMode?: number

  /** 模型 Id */
  modelId?: number

  /** 补全完成回调（参数为完整补全文本）*/
  onFinish?: (completion: string) => void

  /** 错误回调 */
  onError?: (error: Error) => void
}

/** useCompletion Hook 返回值 */
export interface UseCompletionReturn {
  /** 当前补全文本（流式累加）*/
  completion: string

  /** 是否正在接收流 */
  isLoading: boolean

  /** 最近一次错误 */
  error: Error | null

  /** 发送 prompt，开始流式补全 */
  complete: (prompt: string) => Promise<void>

  /** 中止当前流 */
  stop: () => void
}

// ── Hook ──────────────────────────────────────────────────────────────────────

export function useCompletion(options: UseCompletionOptions): UseCompletionReturn {
  const {
    conversationId,
    thinkingMode = 0,
    modelId,
    onFinish,
    onError,
  } = options

  const [completion, setCompletion] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  const abortRef = useRef<AbortController | null>(null)
  // 使用 ref 追踪当前累加内容（避免闭包过期问题）
  const bufferRef = useRef('')

  const complete = useCallback(
    async (prompt: string) => {
      const controller = new AbortController()
      abortRef.current = controller
      bufferRef.current = ''

      setCompletion('')
      setIsLoading(true)
      setError(null)

      try {
        await streamMessage(
          conversationId,
          prompt,
          thinkingMode,
          (event: ChatStreamEvent) => {
            switch (event.type) {
              case 'content_delta':
                if (event.content) {
                  bufferRef.current += event.content
                  setCompletion(bufferRef.current)
                }
                break

              case 'message_done':
                // 流结束，通知外部
                if (onFinish) onFinish(bufferRef.current)
                break

              case 'error':
                setError(new Error(event.error ?? '补全失败'))
                break
            }
          },
          controller.signal,
          undefined,
          modelId,
        )
      } catch (err) {
        if (err instanceof DOMException && err.name === 'AbortError') return
        const e = err instanceof Error ? err : new Error(String(err))
        setError(e)
        onError?.(e)
      } finally {
        setIsLoading(false)
        abortRef.current = null
      }
    },
    [conversationId, thinkingMode, modelId, onFinish, onError],
  )

  const stop = useCallback(() => {
    abortRef.current?.abort()
  }, [])

  return { completion, isLoading, error, complete, stop }
}
