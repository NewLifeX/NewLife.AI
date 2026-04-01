import { create } from 'zustand'
import type { Conversation, Message } from '@/types'
import {
  fetchConversations,
  createConversation,
  deleteConversation,
  pinConversation,
  updateConversation,
  fetchMessages,
  streamMessage,
  streamRegenerate,
  streamEditAndResend,
  editMessage,
  deleteMessage,
  submitFeedback,
  deleteFeedback,
  uploadAttachment,
  fetchModels,
  stopGeneration,
  type ChatStreamEvent,
} from '@/lib/api'
import { useSettingsStore } from '@/stores/settingsStore'
import type { Attachment, ModelInfo } from '@/types'

type ThinkingModeKey = 'fast' | 'auto' | 'think'

const thinkingModeMap: Record<ThinkingModeKey, number> = {
  fast: 2,  // ThinkingMode.Fast  → enable_thinking: false
  auto: 0,  // ThinkingMode.Auto  → 不传 enable_thinking
  think: 1, // ThinkingMode.Think → enable_thinking: true
}

interface ChatState {
  conversations: Conversation[]
  activeConversationId: string | undefined
  messages: Message[]
  isGenerating: boolean
  isLoadingMessages: boolean
  thinkingMode: ThinkingModeKey
  pendingAttachments: Attachment[]
  models: ModelInfo[]
  _abortController: AbortController | null
  _generatingMsgId: string | null
  _convPage: number
  _convHasMore: boolean
  _convLoading: boolean

  loadConversations: () => Promise<void>
  loadMoreConversations: () => Promise<void>
  loadModels: () => Promise<void>
  switchModel: (modelId: number) => Promise<void>
  setActiveConversation: (id: string | undefined) => void
  newChat: () => void
  sendMessage: (content: string) => void
  stopGenerating: () => void
  setThinkingMode: (mode: ThinkingModeKey) => void
  addAttachment: (file: File) => Promise<void>
  removeAttachment: (id: string) => void
  regenerateMsg: (id: string) => Promise<void>
  editMsg: (id: string, content: string) => Promise<void>
  editMsgOnly: (id: string, content: string) => Promise<void>
  deleteMsg: (id: string) => Promise<void>
  likeMsg: (id: string) => Promise<void>
  dislikeMsg: (id: string, reasons?: string[]) => Promise<void>
  deleteConversation: (id: string) => Promise<void>
  pinConversation: (id: string, isPinned: boolean) => Promise<void>
  renameConversation: (id: string, title: string) => Promise<void>
  appendMessage: (msg: Message) => void
  updateMessage: (id: string, partial: Partial<Message>) => void
  copyMessage: (id: string) => void
}

export const useChatStore = create<ChatState>((set, get) => ({
  conversations: [],
  activeConversationId: undefined,
  messages: [],
  isGenerating: false,
  isLoadingMessages: false,
  thinkingMode: 'auto' as ThinkingModeKey,
  pendingAttachments: [],
  models: [],
  _abortController: null,
  _generatingMsgId: null,
  _convPage: 1,
  _convHasMore: true,
  _convLoading: false,

  loadConversations: async () => {
    try {
      const currentPage = get()._convPage
      const totalSize = currentPage * 50
      const list = await fetchConversations(1, totalSize)
      set({ conversations: list, _convHasMore: list.length >= totalSize })
    } catch {
      /* 静默失败，保留当前列表 */
    }
  },

  loadMoreConversations: async () => {
    const { _convHasMore, _convPage, _convLoading } = get()
    if (!_convHasMore || _convLoading) return
    set({ _convLoading: true })
    try {
      const nextPage = _convPage + 1
      const list = await fetchConversations(nextPage, 50)
      set((s) => ({
        conversations: [...s.conversations, ...list],
        _convPage: nextPage,
        _convHasMore: list.length >= 50,
        _convLoading: false,
      }))
    } catch {
      set({ _convLoading: false })
    }
  },

  loadModels: async () => {
    try {
      const list = await fetchModels()
      set({ models: list })
    } catch { /* 静默 */ }
  },

  switchModel: async (modelId) => {
    const { activeConversationId } = get()
    if (activeConversationId != null) {
      try {
        await updateConversation(activeConversationId, { modelId })
        set((s) => ({
          conversations: s.conversations.map((c) =>
            c.id === activeConversationId ? { ...c, modelId } : c,
          ),
        }))
      } catch { /* 静默 */ }
    }
  },

  setActiveConversation: (id) => {
    // 空会话自动清理：切走时若当前会话无消息则删除
    const prevId = get().activeConversationId
    if (prevId != null && prevId !== id) {
      const prevConv = get().conversations.find((c) => c.id === prevId)
      if (prevConv && get().messages.length === 0) {
        deleteConversation(prevId).catch(() => {})
        set((s) => ({ conversations: s.conversations.filter((c) => c.id !== prevId) }))
      }
    }

    set({ activeConversationId: id, messages: [], isLoadingMessages: id != null })
    if (id != null) {
      fetchMessages(id)
        .then((msgs) => {
          if (get().activeConversationId === id) {
            set({ messages: msgs, isLoadingMessages: false })
          }
        })
        .catch(() => { set({ isLoadingMessages: false }) })
    }
  },

  newChat: () => {
    const ac = get()._abortController
    if (ac) ac.abort()

    // 空会话自动清理
    const prevId = get().activeConversationId
    if (prevId != null) {
      const prevConv = get().conversations.find((c) => c.id === prevId)
      if (prevConv && get().messages.length === 0) {
        deleteConversation(prevId).catch(() => {})
        set((s) => ({ conversations: s.conversations.filter((c) => c.id !== prevId) }))
      }
    }

    set({ activeConversationId: undefined, messages: [], isGenerating: false, isLoadingMessages: false, pendingAttachments: [], _abortController: null, _generatingMsgId: null })
  },

  addAttachment: async (file) => {
    // 图片文件先生成本地预览 URL（立即显示缩略图，无需等待上传完成）
    const isImage = file.type.startsWith('image/')
    const localPreview = isImage ? URL.createObjectURL(file) : undefined
    try {
      const result = await uploadAttachment(file)
      const ext = file.name.split('.').pop()?.toLowerCase() ?? ''
      const imgExts = ['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg']
      // 剪贴板粘贴的截图 file.name 往往为 'image'（无扩展名），需同时检查 MIME type
      const type = (imgExts.includes(ext) || isImage) ? 'image' as const
        : (ext === 'pdf' || file.type === 'application/pdf') ? 'pdf' as const
        : 'file' as const
      const att: Attachment = {
        id: result.id,
        name: result.fileName,
        size: result.size,
        type,
        previewUrl: type === 'image' ? (result.url || localPreview) : undefined,
      }
      set((s) => ({ pendingAttachments: [...s.pendingAttachments, att] }))
    } catch (err) {
      console.error('[addAttachment] upload failed:', err)
      if (localPreview) URL.revokeObjectURL(localPreview)
    }
  },

  removeAttachment: (id) => {
    set((s) => ({ pendingAttachments: s.pendingAttachments.filter((a) => a.id !== id) }))
  },

  sendMessage: async (content) => {
    const state = get()
    let convId = state.activeConversationId
    const attachmentIds = state.pendingAttachments.map((a) => a.id)
    const defaultModel = useSettingsStore.getState().defaultModel

    // 如果没有当前会话，先创建
    if (convId == null) {
      try {
        const conv = await createConversation(content.slice(0, 30), defaultModel || undefined)
        convId = conv.id
        set((s) => ({
          conversations: [conv, ...s.conversations],
          activeConversationId: convId,
        }))
      } catch {
        return
      }
    }

    // 获取当前会话绑定的模型，若无则使用默认模型
    const activeConv = get().conversations.find((c) => c.id === convId)
    const currentModelId = activeConv?.modelId || defaultModel || 0

    // 乐观添加用户消息
    const userMsg: Message = {
      id: Date.now().toString(),
      conversationId: convId,
      role: 'user',
      content,
      createdAt: new Date().toISOString(),
      status: 'done',
      attachments: attachmentIds.length ? JSON.stringify(attachmentIds) : undefined,
    }

    const abortController = new AbortController()
    set((s) => ({
      messages: [...s.messages, userMsg],
      isGenerating: true,
      pendingAttachments: [],
      _abortController: abortController,
      _generatingMsgId: null,
    }))

    // SSE 流式接收
    let assistantMsgId: string | undefined
    const finalConvId = convId
    let segmentFinalized = true

      // 查询当前模型是否支持思考模式，不支持时强制传 Auto(0)，避免发送 enable_thinking 参数
      const currentModelInfo = get().models.find((m) => m.id === currentModelId)
      const effectiveThinkingMode = currentModelInfo?.supportThinking
        ? thinkingModeMap[get().thinkingMode]
        : 0

    try {
      await streamMessage(finalConvId, content, effectiveThinkingMode, (event: ChatStreamEvent) => {
        switch (event.type) {
          case 'message_start':
            assistantMsgId = event.messageId
            if (assistantMsgId != null) {
              set({ _generatingMsgId: assistantMsgId })
              const aiMsg: Message = {
                id: assistantMsgId,
                conversationId: finalConvId,
                role: 'assistant',
                content: '',
                createdAt: new Date().toISOString(),
                status: 'streaming',
              }
              set((s) => ({ messages: [...s.messages, aiMsg] }))
            }
            break

          case 'content_delta':
            if (assistantMsgId != null && event.content) {
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, content: m.content + event.content }
                    : m,
                ),
              }))
            }
            break

          case 'thinking_delta':
            if (assistantMsgId != null && event.content) {
              const needNewSegment = segmentFinalized
              segmentFinalized = false
              set((s) => ({
                messages: s.messages.map((m) => {
                  if (m.id !== assistantMsgId) return m
                  const segments = [...(m.thinkingSegments ?? [])]
                  if (needNewSegment || segments.length === 0) {
                    segments.push({ content: event.content! })
                  } else {
                    const last = segments[segments.length - 1]
                    segments[segments.length - 1] = { ...last, content: last.content + event.content }
                  }
                  return { ...m, thinkingContent: (m.thinkingContent ?? '') + event.content, thinkingSegments: segments }
                }),
              }))
            }
            break

          case 'thinking_done':
            if (assistantMsgId != null) {
              segmentFinalized = true
              set((s) => ({
                messages: s.messages.map((m) => {
                  if (m.id !== assistantMsgId) return m
                  const segments = [...(m.thinkingSegments ?? [])]
                  if (segments.length > 0 && event.thinkingTime) {
                    segments[segments.length - 1] = { ...segments[segments.length - 1], thinkingTime: event.thinkingTime }
                  }
                  return { ...m, thinkingTime: event.thinkingTime, thinkingSegments: segments }
                }),
              }))
            }
            break

          case 'tool_call_start':
            if (assistantMsgId != null && event.toolCallId) {
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, toolCalls: [...(m.toolCalls ?? []), { id: event.toolCallId!, name: event.name ?? '', status: 'calling' as const, arguments: event.arguments }] }
                    : m,
                ),
              }))
            }
            break

          case 'tool_call_done':
            if (assistantMsgId != null && event.toolCallId) {
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, toolCalls: (m.toolCalls ?? []).map((t) => t.id === event.toolCallId ? { ...t, status: 'done' as const, result: event.result } : t) }
                    : m,
                ),
              }))
            }
            break

          case 'tool_call_error':
            if (assistantMsgId != null && event.toolCallId) {
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, toolCalls: (m.toolCalls ?? []).map((t) => t.id === event.toolCallId ? { ...t, status: 'error' as const, result: event.error } : t) }
                    : m,
                ),
              }))
            }
            break

          case 'message_done':
            if (assistantMsgId != null) {
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId ? { ...m, status: 'done', usage: event.usage } : m,
                ),
                isGenerating: false,
                _abortController: null,
                _generatingMsgId: null,
                // 如果后端返回了自动生成的标题，直接更新会话列表
                conversations: event.title
                  ? s.conversations.map((c) => c.id === finalConvId ? { ...c, title: event.title! } : c)
                  : s.conversations,
              }))
              // 刷新会话列表以获取后端自动生成的标题
              if (!event.title) get().loadConversations()
            }
            break

          case 'error': {
            const errorMsg = event.message || event.error || '发生错误'
            if (assistantMsgId != null) {
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, content: errorMsg, status: 'error' }
                    : m,
                ),
                isGenerating: false,
                _abortController: null,
                _generatingMsgId: null,
              }))
            } else {
              set({ isGenerating: false, _abortController: null, _generatingMsgId: null })
            }
            break
          }
        }
      }, abortController.signal, attachmentIds.length ? attachmentIds : undefined, currentModelId || undefined)
    } catch {
      // 网络错误或中断
      set((s) => ({
        isGenerating: false,
        _abortController: null,
        _generatingMsgId: null,
        messages: assistantMsgId
          ? s.messages.map((m) =>
              m.id === assistantMsgId ? { ...m, status: 'done' as const } : m,
            )
          : s.messages,
      }))
    }

    // 仅在 message_done 未触发刷新时才补刷一次（如中断、异常等场景）
    if (!assistantMsgId) get().loadConversations()
  },

  stopGenerating: () => {
    const ac = get()._abortController
    const msgId = get()._generatingMsgId
    if (ac) ac.abort()
    if (msgId) stopGeneration(msgId).catch(() => {})
    set({ isGenerating: false, _abortController: null, _generatingMsgId: null })
  },

  setThinkingMode: (mode) => {
    set({ thinkingMode: mode })
  },

  regenerateMsg: async (id) => {
    // 标记该消息为 streaming 并清空旧内容
    set((s) => ({
      isGenerating: true,
      messages: s.messages.map((m) =>
        m.id === id ? { ...m, content: '', thinkingContent: undefined, status: 'streaming' as const, usage: undefined } : m,
      ),
    }))

    const ac = new AbortController()
    set({ _abortController: ac, _generatingMsgId: id })

    try {
      await streamRegenerate(id, (event) => {
        switch (event.type) {
          case 'content_delta':
            set((s) => ({
              messages: s.messages.map((m) =>
                m.id === id ? { ...m, content: (m.content ?? '') + (event.content ?? '') } : m,
              ),
            }))
            break
          case 'thinking_delta':
            set((s) => ({
              messages: s.messages.map((m) =>
                m.id === id ? { ...m, thinkingContent: (m.thinkingContent ?? '') + (event.content ?? '') } : m,
              ),
            }))
            break
          case 'message_done':
            set((s) => ({
              isGenerating: false,
              _abortController: null,
              _generatingMsgId: null,
              messages: s.messages.map((m) =>
                m.id === id
                  ? { ...m, status: 'done' as const, usage: event.usage ? { promptTokens: event.usage.promptTokens, completionTokens: event.usage.completionTokens, totalTokens: event.usage.totalTokens } : m.usage }
                  : m,
              ),
            }))
            break
          case 'error':
            set((s) => ({
              isGenerating: false,
              _abortController: null,
              _generatingMsgId: null,
              messages: s.messages.map((m) =>
                m.id === id ? { ...m, content: event.error ?? event.message ?? '[error]', status: 'error' as const } : m,
              ),
            }))
            break
          default:
            break
        }
      }, ac.signal)
    } catch {
      // 网络中断或取消
      set({ isGenerating: false, _abortController: null, _generatingMsgId: null })
      set((s) => ({
        messages: s.messages.map((m) =>
          m.id === id && m.status === 'streaming' ? { ...m, status: 'done' as const } : m,
        ),
      }))
    }
  },

  editMsg: async (id, content) => {
    try {
      const msg = get().messages.find((m) => m.id === id)
      if (!msg) return

      if (msg.role === 'user') {
        // 编辑用户消息：删除后续所有消息 + 流式生成新 AI 回复（原子操作）
        // 先在前端截断消息列表，保留到编辑的消息为止，并更新内容
        const idx = get().messages.findIndex((m) => m.id === id)
        set((s) => ({
          messages: s.messages.slice(0, idx + 1).map((m) => m.id === id ? { ...m, content } : m),
          isGenerating: true,
        }))

        const abortController = new AbortController()
        set({ _abortController: abortController })

        let assistantMsgId: string | null = null

        await streamEditAndResend(id, content, (event) => {
          switch (event.type) {
            case 'message_start':
              assistantMsgId = event.messageId ?? null
              if (assistantMsgId != null) {
                set({ _generatingMsgId: assistantMsgId })
                set((s) => ({
                  messages: [...s.messages, {
                    id: assistantMsgId!,
                    conversationId: s.activeConversationId!,
                    role: 'assistant' as const,
                    content: '',
                    thinkingContent: undefined,
                    status: 'streaming' as const,
                    createdAt: new Date().toISOString(),
                  }],
                }))
              }
              break
            case 'thinking_delta':
              if (assistantMsgId != null) {
                set((s) => ({
                  messages: s.messages.map((m) =>
                    m.id === assistantMsgId ? { ...m, thinkingContent: (m.thinkingContent ?? '') + (event.content ?? '') } : m,
                  ),
                }))
              }
              break
            case 'content_delta':
              if (assistantMsgId != null) {
                set((s) => ({
                  messages: s.messages.map((m) =>
                    m.id === assistantMsgId ? { ...m, content: (typeof m.content === 'string' ? m.content : '') + (event.content ?? '') } : m,
                  ),
                }))
              }
              break
            case 'message_done':
              if (assistantMsgId != null) {
                set((s) => ({
                  messages: s.messages.map((m) =>
                    m.id === assistantMsgId ? { ...m, status: 'done' as const, usage: event.usage ? { promptTokens: event.usage.promptTokens, completionTokens: event.usage.completionTokens, totalTokens: event.usage.totalTokens } : m.usage } : m,
                  ),
                  isGenerating: false,
                  _abortController: null,
                  _generatingMsgId: null,
                }))
              }
              break
            case 'error':
              if (assistantMsgId != null) {
                set((s) => ({
                  messages: s.messages.map((m) =>
                    m.id === assistantMsgId ? { ...m, content: event.error ?? '生成失败', status: 'error' as const } : m,
                  ),
                  isGenerating: false,
                  _abortController: null,
                  _generatingMsgId: null,
                }))
              } else {
                set({ isGenerating: false, _abortController: null, _generatingMsgId: null })
              }
              break
          }
        }, abortController.signal).catch(() => {
          set((s) => ({
            isGenerating: false,
            _abortController: null,
            _generatingMsgId: null,
            messages: assistantMsgId != null
              ? s.messages.map((m) => m.id === assistantMsgId ? { ...m, status: 'done' as const } : m)
              : s.messages,
          }))
        })
      } else {
        // 编辑 assistant 消息：仅更新内容，不触发重新生成
        const updated = await editMessage(id, content)
        set((s) => ({
          messages: s.messages.map((m) => (m.id === id ? updated : m)),
        }))
      }
    } catch { /* 静默 */ }
  },

  editMsgOnly: async (id, content) => {
    try {
      const updated = await editMessage(id, content)
      set((s) => ({
        messages: s.messages.map((m) => (m.id === id ? updated : m)),
      }))
    } catch (e) {
      console.error('Failed to save message:', e)
    }
  },

  deleteMsg: async (id) => {
    try {
      await deleteMessage(id)
      set((s) => ({ messages: s.messages.filter((m) => m.id !== id) }))
    } catch (e) {
      console.error('Failed to delete message:', e)
    }
  },

  likeMsg: async (id) => {
    const msg = get().messages.find((m) => m.id === id)
    const isAlreadyLiked = msg?.feedbackType === 1
    try {
      if (isAlreadyLiked) {
        await deleteFeedback(id)
        set((s) => ({ messages: s.messages.map((m) => m.id === id ? { ...m, feedbackType: 0 } : m) }))
      } else {
        await submitFeedback(id, 'like')
        set((s) => ({ messages: s.messages.map((m) => m.id === id ? { ...m, feedbackType: 1 } : m) }))
      }
    } catch { /* 静默 */ }
  },

  dislikeMsg: async (id, reasons) => {
    const msg = get().messages.find((m) => m.id === id)
    const isAlreadyDisliked = msg?.feedbackType === 2
    try {
      if (isAlreadyDisliked) {
        await deleteFeedback(id)
        set((s) => ({ messages: s.messages.map((m) => m.id === id ? { ...m, feedbackType: 0 } : m) }))
      } else {
        const reason = reasons?.length ? reasons.join(',') : undefined
        await submitFeedback(id, 'dislike', reason)
        set((s) => ({ messages: s.messages.map((m) => m.id === id ? { ...m, feedbackType: 2 } : m) }))
      }
    } catch { /* 静默 */ }
  },

  deleteConversation: async (id) => {
    try {
      await deleteConversation(id)
      set((s) => ({
        conversations: s.conversations.filter((c) => c.id !== id),
        ...(s.activeConversationId === id
          ? { activeConversationId: undefined, messages: [] }
          : {}),
      }))
    } catch { /* 静默 */ }
  },

  pinConversation: async (id, isPinned) => {
    try {
      await pinConversation(id, isPinned)
      set((s) => ({
        conversations: s.conversations.map((c) =>
          c.id === id ? { ...c, isPinned } : c,
        ),
      }))
    } catch { /* 静默 */ }
  },

  renameConversation: async (id, title) => {
    try {
      await updateConversation(id, { title })
      set((s) => ({
        conversations: s.conversations.map((c) =>
          c.id === id ? { ...c, title } : c,
        ),
      }))
    } catch { /* 静默 */ }
  },

  appendMessage: (msg) => {
    set((s) => ({ messages: [...s.messages, msg] }))
  },

  updateMessage: (id, partial) => {
    set((s) => ({
      messages: s.messages.map((m) => (m.id === id ? { ...m, ...partial } : m)),
    }))
  },

  copyMessage: (id) => {
    const msg = get().messages.find((m) => m.id === id)
    if (msg && typeof msg.content === 'string') {
      navigator.clipboard.writeText(msg.content)
    }
  },
}))
