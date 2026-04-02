export interface Conversation {
  id: string
  title: string
  modelId?: number
  isPinned: boolean
  messageCount?: number
  icon?: string
  iconColor?: string
  updatedAt?: string
}

export interface TokenUsage {
  promptTokens?: number
  completionTokens?: number
  totalTokens?: number
}

export interface ThinkingSegment {
  content: string
  thinkingTime?: number
}

export interface Message {
  id: string
  conversationId: string
  role: 'user' | 'assistant'
  content: string
  createdAt: string
  status?: 'streaming' | 'done' | 'error'
  thinkingContent?: string
  thinkingTime?: number
  thinkingSegments?: ThinkingSegment[]
  toolCalls?: ToolCall[]
  usage?: TokenUsage
  feedbackType?: number
  attachments?: string
}

export interface ToolCall {
  id: string
  name: string
  status: 'calling' | 'done' | 'error'
  arguments?: string
  result?: string
}

export interface ModelInfo {
  id: number
  code: string
  name: string
  supportThinking?: boolean
  supportVision?: boolean
  supportImageGeneration?: boolean
  supportFunctionCalling?: boolean
}

export interface Attachment {
  id: number
  name: string
  size: number
  type: 'pdf' | 'image' | 'file'
  previewUrl?: string
}

export interface UserSettings {
  theme: 'light' | 'dark' | 'system'
  language: string
  fontSize: number
  sendShortcut: 'Enter' | 'Ctrl+Enter'
  defaultModel: number
  defaultThinkingMode: number
  contextRounds: number
  systemPrompt: string
  mcpEnabled: boolean
  streamingSpeed: number
  allowTraining: boolean
  defaultSkill?: string
  contentWidth?: number
}
