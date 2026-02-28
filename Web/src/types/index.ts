export interface Conversation {
  id: number
  title: string
  isPinned: boolean
  icon?: string
  iconColor?: string
  updatedAt?: string
}

export interface Message {
  id: number
  conversationId: number
  role: 'user' | 'assistant'
  content: string
  createdAt: string
  status?: 'streaming' | 'done' | 'error'
  thinkingContent?: string
  toolCalls?: ToolCall[]
}

export interface ToolCall {
  id: string
  name: string
  status: 'calling' | 'done' | 'error'
  arguments?: string
  result?: string
}

export interface ModelInfo {
  id: string
  name: string
  provider: string
}

export interface Attachment {
  id: string
  name: string
  size: number
  type: 'pdf' | 'image' | 'file'
  previewUrl?: string
}

export interface UserSettings {
  theme: 'light' | 'dark' | 'system'
  language: string
  fontSize: number
  mcpEnabled: boolean
  defaultSkill: string
  streamingSpeed: number
}
