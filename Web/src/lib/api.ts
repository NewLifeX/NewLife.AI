import type { Conversation, Message, UserSettings, ModelInfo } from '@/types'

const BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  })
  if (!res.ok) {
    throw new Error(`API ${res.status}: ${res.statusText}`)
  }
  return res.json() as Promise<T>
}

// ── Conversations ──

interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

interface ConversationDto {
  id: number
  title: string
  modelCode: string
  lastMessageTime: string
  isPinned: boolean
  icon?: string
  iconColor?: string
}

function toConversation(dto: ConversationDto): Conversation {
  return {
    id: dto.id,
    title: dto.title,
    modelCode: dto.modelCode,
    isPinned: dto.isPinned,
    icon: dto.icon,
    iconColor: dto.iconColor,
    updatedAt: dto.lastMessageTime,
  }
}

export async function fetchConversations(page = 1, pageSize = 50): Promise<Conversation[]> {
  const result = await request<PagedResult<ConversationDto>>(
    `/api/conversations?page=${page}&pageSize=${pageSize}`,
  )
  return result.items.map(toConversation)
}

export async function createConversation(title?: string, modelCode?: string): Promise<Conversation> {
  const dto = await request<ConversationDto>('/api/conversations', {
    method: 'POST',
    body: JSON.stringify({ title, modelCode }),
  })
  return toConversation(dto)
}

export async function updateConversation(
  id: number,
  data: { title?: string; modelCode?: string },
): Promise<Conversation> {
  const dto = await request<ConversationDto>(`/api/conversations/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
  return toConversation(dto)
}

export async function deleteConversation(id: number): Promise<void> {
  await request<boolean>(`/api/conversations/${id}`, { method: 'DELETE' })
}

export async function pinConversation(id: number, isPinned: boolean): Promise<void> {
  await request<boolean>(`/api/conversations/${id}/pin?isPinned=${isPinned}`, {
    method: 'PATCH',
  })
}

// ── Messages ──

interface MessageDto {
  id: number
  conversationId: number
  role: string
  content: string
  thinkingMode: number
  createTime: string
  status?: number
  thinkingContent?: string
  toolCalls?: Array<{
    id: string
    name: string
    status: number
    arguments?: string
    result?: string
  }>
}

function toMessage(dto: MessageDto): Message {
  const statusMap: Record<number, Message['status']> = { 0: 'streaming', 1: 'done', 2: 'error' }
  const toolStatusMap: Record<number, 'calling' | 'done' | 'error'> = {
    0: 'calling',
    1: 'done',
    2: 'error',
  }
  return {
    id: dto.id,
    conversationId: dto.conversationId,
    role: dto.role as Message['role'],
    content: dto.content,
    createdAt: dto.createTime,
    status: statusMap[dto.status ?? 1] ?? 'done',
    thinkingContent: dto.thinkingContent,
    toolCalls: dto.toolCalls?.map((tc) => ({
      id: tc.id,
      name: tc.name,
      status: toolStatusMap[tc.status] ?? 'done',
      arguments: tc.arguments,
      result: tc.result,
    })),
  }
}

export async function fetchMessages(conversationId: number): Promise<Message[]> {
  const dtos = await request<MessageDto[]>(`/api/conversations/${conversationId}/messages`)
  return dtos.map(toMessage)
}

// ── SSE Streaming ──

export interface ChatStreamEvent {
  type: 'message_start' | 'thinking_delta' | 'content_delta' | 'tool_call_start' | 'tool_call_done' | 'message_done' | 'error'
  messageId?: number
  content?: string
  thinkingContent?: string
  toolCall?: {
    id: string
    name: string
    status: number
    arguments?: string
    result?: string
  }
  error?: string
}

export async function streamMessage(
  conversationId: number,
  content: string,
  thinkingMode: number,
  onEvent: (event: ChatStreamEvent) => void,
  signal?: AbortSignal,
  attachmentIds?: string[],
): Promise<void> {
  const res = await fetch(`${BASE_URL}/api/conversations/${conversationId}/messages`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ content, thinkingMode, attachmentIds: attachmentIds?.length ? attachmentIds : undefined }),
    signal,
  })

  if (!res.ok) {
    throw new Error(`SSE ${res.status}: ${res.statusText}`)
  }

  const reader = res.body?.getReader()
  if (!reader) throw new Error('No response body')

  const decoder = new TextDecoder()
  let buffer = ''

  while (true) {
    const { done, value } = await reader.read()
    if (done) break

    buffer += decoder.decode(value, { stream: true })
    const lines = buffer.split('\n')
    buffer = lines.pop() ?? ''

    for (const line of lines) {
      if (!line.startsWith('data: ')) continue
      const json = line.slice(6).trim()
      if (!json) continue
      try {
        const event = JSON.parse(json) as ChatStreamEvent
        onEvent(event)
      } catch { /* skip malformed lines */ }
    }
  }
}

// ── Messages Actions ──

export async function editMessage(id: number, content: string): Promise<Message> {
  const dto = await request<MessageDto>(`/api/messages/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ content }),
  })
  return toMessage(dto)
}

export async function regenerateMessage(id: number): Promise<Message> {
  const dto = await request<MessageDto>(`/api/messages/${id}/regenerate`, {
    method: 'POST',
  })
  return toMessage(dto)
}

export async function stopGeneration(id: number): Promise<void> {
  await request<void>(`/api/messages/${id}/stop`, { method: 'POST' })
}

// ── Feedback ──

export async function submitFeedback(
  messageId: number,
  type: 'like' | 'dislike',
  reason?: string,
): Promise<void> {
  await request<void>(`/api/messages/${messageId}/feedback`, {
    method: 'POST',
    body: JSON.stringify({ type: type === 'like' ? 1 : 2, reason }),
  })
}

export async function deleteFeedback(messageId: number): Promise<void> {
  await request<void>(`/api/messages/${messageId}/feedback`, { method: 'DELETE' })
}

// ── Models ──

interface ModelInfoDto {
  code: string
  name: string
  supportThinking: boolean
  supportVision: boolean
}

export async function fetchModels(): Promise<ModelInfo[]> {
  const dtos = await request<ModelInfoDto[]>('/api/models')
  return dtos.map((d) => ({
    id: d.code,
    name: d.name,
    provider: '',
    supportThinking: d.supportThinking,
    supportVision: d.supportVision,
  }))
}

// ── User Settings ──

interface UserSettingsDto {
  language: string
  theme: string
  fontSize: number
  sendShortcut: string
  defaultModel: string
  defaultThinkingMode: number
  contextRounds: number
  systemPrompt: string
  allowTraining: boolean
  mcpEnabled: boolean
  defaultSkill: string
  streamingSpeed: number
}

function toUserSettings(dto: UserSettingsDto): UserSettings {
  return {
    theme: dto.theme as UserSettings['theme'],
    language: dto.language,
    fontSize: dto.fontSize,
    sendShortcut: dto.sendShortcut as UserSettings['sendShortcut'],
    defaultModel: dto.defaultModel,
    defaultThinkingMode: dto.defaultThinkingMode,
    contextRounds: dto.contextRounds,
    systemPrompt: dto.systemPrompt,
    mcpEnabled: dto.mcpEnabled,
    defaultSkill: dto.defaultSkill,
    streamingSpeed: dto.streamingSpeed,
  }
}

export async function fetchUserSettings(): Promise<UserSettings> {
  const dto = await request<UserSettingsDto>('/api/user/settings')
  return toUserSettings(dto)
}

export async function saveUserSettings(settings: UserSettings): Promise<UserSettings> {
  const dto = await request<UserSettingsDto>('/api/user/settings', {
    method: 'PUT',
    body: JSON.stringify({
      language: settings.language,
      theme: settings.theme,
      fontSize: settings.fontSize,
      sendShortcut: settings.sendShortcut ?? 'Enter',
      defaultModel: settings.defaultModel ?? 'qwen-max',
      defaultThinkingMode: settings.defaultThinkingMode ?? 0,
      contextRounds: settings.contextRounds ?? 10,
      systemPrompt: settings.systemPrompt ?? '',
      allowTraining: false,
      mcpEnabled: settings.mcpEnabled,
      defaultSkill: settings.defaultSkill,
      streamingSpeed: settings.streamingSpeed,
    }),
  })
  return toUserSettings(dto)
}

// ── Share ──

export async function createShareLink(
  conversationId: number,
  expireHours?: number,
): Promise<{ url: string; createTime: string; expireTime?: string }> {
  return request(`/api/conversations/${conversationId}/share`, {
    method: 'POST',
    body: JSON.stringify({ expireHours }),
  })
}

export async function revokeShareLink(token: string): Promise<void> {
  await request<boolean>(`/api/share/${token}`, { method: 'DELETE' })
}

// ── Attachments ──

export async function uploadAttachment(
  file: File,
): Promise<{ id: string; fileName: string; url: string; size: number }> {
  const formData = new FormData()
  formData.append('file', file)
  const res = await fetch(`${BASE_URL}/api/attachments`, {
    method: 'POST',
    body: formData,
  })
  if (!res.ok) throw new Error(`Upload ${res.status}`)
  return res.json()
}

// ── MCP Servers ──

export interface McpServer {
  id: number
  name: string
  endpoint: string
  transportType: string
  authType: string
  enable: boolean
  sort: number
  remark?: string
}

export async function fetchMcpServers(): Promise<McpServer[]> {
  return request<McpServer[]>('/api/mcp/servers')
}

export async function toggleMcpServer(id: number, enabled: boolean): Promise<void> {
  await request<void>(`/api/mcp/servers/${id}/enable?enabled=${enabled}`, {
    method: 'PATCH',
  })
}

// ── Data Management ──

export async function exportUserData(): Promise<Blob> {
  const res = await fetch(`${BASE_URL}/api/user/data/export`)
  if (!res.ok) throw new Error(`Export ${res.status}`)
  return res.blob()
}

export async function clearUserData(): Promise<void> {
  await request<void>('/api/user/data/clear', { method: 'DELETE' })
}
