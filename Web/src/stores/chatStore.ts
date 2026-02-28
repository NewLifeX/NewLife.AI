import { create } from 'zustand'
import type { Conversation, Message } from '@/types'

const mockConversations: Conversation[] = [
  { id: 1, title: '系统性能与数据可视化', isPinned: true },
  { id: 2, title: 'MCP 协议调试', isPinned: true },
  { id: 3, title: '服务器日志分析', isPinned: false },
]

interface ChatState {
  conversations: Conversation[]
  activeConversationId: number | undefined
  messages: Message[]
  isGenerating: boolean

  setActiveConversation: (id: number | undefined) => void
  newChat: () => void
  sendMessage: (content: string) => void
  stopGenerating: () => void
  appendMessage: (msg: Message) => void
  updateMessage: (id: number, partial: Partial<Message>) => void
  copyMessage: (id: number) => void
}

export const useChatStore = create<ChatState>((set, get) => ({
  conversations: mockConversations,
  activeConversationId: undefined,
  messages: [],
  isGenerating: false,

  setActiveConversation: (id) => {
    set({ activeConversationId: id, messages: [] })
  },

  newChat: () => {
    set({ activeConversationId: undefined, messages: [], isGenerating: false })
  },

  sendMessage: (content) => {
    const state = get()
    const now = Date.now()

    const userMsg: Message = {
      id: now,
      conversationId: state.activeConversationId ?? 0,
      role: 'user',
      content,
      createdAt: new Date().toISOString(),
      status: 'done',
    }

    const newConvId = state.activeConversationId ?? now

    set((s) => ({
      messages: [...s.messages, userMsg],
      activeConversationId: newConvId,
      isGenerating: true,
    }))

    // TODO: 替换为真实 SSE 调用
    setTimeout(() => {
      const current = get()
      if (!current.isGenerating) return

      const aiMsg: Message = {
        id: Date.now(),
        conversationId: newConvId,
        role: 'assistant',
        content: `收到你的消息：「${content}」\n\n这是一个演示回复。后端 SSE 对接后将显示真实的 AI 流式响应。`,
        createdAt: new Date().toISOString(),
        status: 'done',
      }
      set((s) => ({
        messages: [...s.messages, aiMsg],
        isGenerating: false,
      }))
    }, 1500)
  },

  stopGenerating: () => {
    set({ isGenerating: false })
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
