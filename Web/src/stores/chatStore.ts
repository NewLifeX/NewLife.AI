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
  regenerateMessage,
  editMessage,
  submitFeedback,
  uploadAttachment,
  fetchModels,
  type ChatStreamEvent,
} from '@/lib/api'
import type { Attachment, ModelInfo } from '@/types'

type ThinkingModeKey = 'fast' | 'balanced' | 'deep'

const thinkingModeMap: Record<ThinkingModeKey, number> = {
  fast: 2,      // ThinkingMode.Fast
  balanced: 0,  // ThinkingMode.Auto
  deep: 1,      // ThinkingMode.Think
}

interface ChatState {
  conversations: Conversation[]
  activeConversationId: number | undefined
  messages: Message[]
  isGenerating: boolean
  thinkingMode: ThinkingModeKey
  pendingAttachments: Attachment[]
  models: ModelInfo[]
  _abortController: AbortController | null
  _convPage: number
  _convHasMore: boolean
  _convLoading: boolean

  loadConversations: () => Promise<void>
  loadMoreConversations: () => Promise<void>
  loadModels: () => Promise<void>
  switchModel: (modelId: string) => Promise<void>
  setActiveConversation: (id: number | undefined) => void
  newChat: () => void
  sendMessage: (content: string) => void
  stopGenerating: () => void
  setThinkingMode: (mode: ThinkingModeKey) => void
  addAttachment: (file: File) => Promise<void>
  removeAttachment: (id: string) => void
  regenerateMsg: (id: number) => Promise<void>
  editMsg: (id: number, content: string) => Promise<void>
  likeMsg: (id: number) => Promise<void>
  dislikeMsg: (id: number) => Promise<void>
  deleteConversation: (id: number) => Promise<void>
  pinConversation: (id: number, isPinned: boolean) => Promise<void>
  renameConversation: (id: number, title: string) => Promise<void>
  appendMessage: (msg: Message) => void
  updateMessage: (id: number, partial: Partial<Message>) => void
  copyMessage: (id: number) => void
}

export const useChatStore = create<ChatState>((set, get) => ({
  conversations: [],
  activeConversationId: undefined,
  messages: [],
  isGenerating: false,
  thinkingMode: 'balanced' as ThinkingModeKey,
  pendingAttachments: [],
  models: [],
  _abortController: null,
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
        await updateConversation(activeConversationId, { modelCode: modelId })
        set((s) => ({
          conversations: s.conversations.map((c) =>
            c.id === activeConversationId ? { ...c, modelCode: modelId } : c,
          ),
        }))
      } catch { /* 静默 */ }
    }
  },

  setActiveConversation: (id) => {
    set({ activeConversationId: id, messages: [] })
    if (id != null) {
      fetchMessages(id)
        .then((msgs) => {
          if (get().activeConversationId === id) {
            set({ messages: msgs })
          }
        })
        .catch(() => {})
    }
  },

  newChat: () => {
    const ac = get()._abortController
    if (ac) ac.abort()
    set({ activeConversationId: undefined, messages: [], isGenerating: false, pendingAttachments: [], _abortController: null })
  },

  addAttachment: async (file) => {
    try {
      const result = await uploadAttachment(file)
      const ext = file.name.split('.').pop()?.toLowerCase() ?? ''
      const imgExts = ['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg']
      const type = imgExts.includes(ext) ? 'image' as const : ext === 'pdf' ? 'pdf' as const : 'file' as const
      const att: Attachment = {
        id: result.id,
        name: result.fileName,
        size: result.size,
        type,
      }
      set((s) => ({ pendingAttachments: [...s.pendingAttachments, att] }))
    } catch { /* 静默 */ }
  },

  removeAttachment: (id) => {
    set((s) => ({ pendingAttachments: s.pendingAttachments.filter((a) => a.id !== id) }))
  },

  sendMessage: async (content) => {
    const state = get()
    let convId = state.activeConversationId
    const attachmentIds = state.pendingAttachments.map((a) => a.id)

    // 如果没有当前会话，先创建
    if (convId == null) {
      try {
        const conv = await createConversation(content.slice(0, 30))
        convId = conv.id
        set((s) => ({
          conversations: [conv, ...s.conversations],
          activeConversationId: convId,
        }))
      } catch {
        return
      }
    }

    // 乐观添加用户消息
    const userMsg: Message = {
      id: Date.now(),
      conversationId: convId,
      role: 'user',
      content,
      createdAt: new Date().toISOString(),
      status: 'done',
    }

    const abortController = new AbortController()
    set((s) => ({
      messages: [...s.messages, userMsg],
      isGenerating: true,
      pendingAttachments: [],
      _abortController: abortController,
    }))

    // SSE 流式接收
    let assistantMsgId: number | undefined
    const finalConvId = convId

    try {
      await streamMessage(finalConvId, content, thinkingModeMap[get().thinkingMode], (event: ChatStreamEvent) => {
        switch (event.type) {
          case 'message_start':
            assistantMsgId = event.messageId
            if (assistantMsgId != null) {
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
            if (assistantMsgId != null && event.thinkingContent) {
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, thinkingContent: (m.thinkingContent ?? '') + event.thinkingContent }
                    : m,
                ),
              }))
            }
            break

          case 'tool_call_start':
            if (assistantMsgId != null && event.toolCall) {
              const tc = event.toolCall
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, toolCalls: [...(m.toolCalls ?? []), { id: tc.id, name: tc.name, status: 'calling' as const, arguments: tc.arguments }] }
                    : m,
                ),
              }))
            }
            break

          case 'tool_call_done':
            if (assistantMsgId != null && event.toolCall) {
              const tc = event.toolCall
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, toolCalls: (m.toolCalls ?? []).map((t) => t.id === tc.id ? { ...t, status: 'done' as const, result: tc.result } : t) }
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
              }))
              // 刷新会话列表以获取后端自动生成的标题
              get().loadConversations()
            }
            break

          case 'error':
            if (assistantMsgId != null) {
              set((s) => ({
                messages: s.messages.map((m) =>
                  m.id === assistantMsgId
                    ? { ...m, content: event.error ?? '发生错误', status: 'error' }
                    : m,
                ),
                isGenerating: false,
                _abortController: null,
              }))
            } else {
              set({ isGenerating: false, _abortController: null })
            }
            break
        }
      }, abortController.signal, attachmentIds.length ? attachmentIds : undefined)
    } catch {
      // 网络错误或中断
      set((s) => ({
        isGenerating: false,
        _abortController: null,
        messages: assistantMsgId
          ? s.messages.map((m) =>
              m.id === assistantMsgId ? { ...m, status: 'done' as const } : m,
            )
          : s.messages,
      }))
    }

    // 刷新会话列表
    get().loadConversations()
  },

  stopGenerating: () => {
    const ac = get()._abortController
    if (ac) ac.abort()
    set({ isGenerating: false, _abortController: null })
  },

  setThinkingMode: (mode) => {
    set({ thinkingMode: mode })
  },

  regenerateMsg: async (id) => {
    try {
      const updated = await regenerateMessage(id)
      set((s) => ({
        messages: s.messages.map((m) => (m.id === id ? updated : m)),
      }))
    } catch { /* 静默 */ }
  },

  editMsg: async (id, content) => {
    try {
      const updated = await editMessage(id, content)
      set((s) => ({
        messages: s.messages.map((m) => (m.id === id ? updated : m)),
      }))
    } catch { /* 静默 */ }
  },

  likeMsg: async (id) => {
    try {
      await submitFeedback(id, 'like')
    } catch { /* 静默 */ }
  },

  dislikeMsg: async (id) => {
    try {
      await submitFeedback(id, 'dislike')
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
