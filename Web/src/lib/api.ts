import type { Conversation, Message, UserSettings, ModelInfo } from '@/types'
import { showToast } from '@/stores/toastStore'

const BASE_URL = import.meta.env.VITE_API_BASE_URL || ''

const SSE_MAX_RETRIES = 3

/** 是否正在跳转登录，防止多次重定向 */
let isRedirectingToLogin = false

/** 未登录时跳转到后台登录页，分享页除外 */
function redirectToLogin() {
  if (isRedirectingToLogin) return
  if (window.location.pathname.startsWith('/share/')) return
  isRedirectingToLogin = true
  const returnUrl = window.location.pathname + window.location.search + window.location.hash
  window.location.href = `/admin/user/login?r=${encodeURIComponent(returnUrl)}`
}

async function fetchSSE(
  url: string,
  init: RequestInit,
  onEvent: (event: ChatStreamEvent) => void,
): Promise<void> {
  let lastError: Error | undefined
  for (let attempt = 0; attempt <= SSE_MAX_RETRIES; attempt++) {
    if (attempt > 0) {
      const delay = Math.min(1000 * Math.pow(2, attempt - 1), 4000) + Math.random() * 500
      await new Promise((r) => setTimeout(r, delay))
      if (init.signal?.aborted) throw new DOMException('Aborted', 'AbortError')
    }
    let streamStarted = false
    try {
      const res = await fetch(url, init)
      if (!res.ok) {
        if (res.status === 401) redirectToLogin()
        throw new Error(`SSE ${res.status}: ${res.statusText}`)
      }
      const reader = res.body?.getReader()
      if (!reader) throw new Error('No response body')
      streamStarted = true
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
      return
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') throw err
      // 流已开始读取后中断不再重试，避免非幂等操作产生重复数据
      if (streamStarted) throw err instanceof Error ? err : new Error(String(err))
      lastError = err instanceof Error ? err : new Error(String(err))
      if (attempt === SSE_MAX_RETRIES) {
        // 最终重试仍失败，弹出友好提示
        if (lastError.message.includes('Failed to fetch') || lastError.message === 'Network error: server unreachable') {
          showToast('error', '无法连接到服务器，请检查网络连接或稍后重试')
        } else {
          showToast('error', `请求失败：${lastError.message}`)
        }
        throw lastError
      }
    }
  }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  let res: Response
  try {
    res = await fetch(`${BASE_URL}${path}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        ...options?.headers,
      },
    })
  } catch {
    // 网络不通 / DNS 解析失败 / 后端未启动
    showToast('error', '无法连接到服务器，请检查网络连接或稍后重试')
    throw new Error('Network error: server unreachable')
  }
  if (!res.ok) {
    if (res.status === 404) {
      showToast('error', `请求的资源不存在 (${path})`)
    } else if (res.status === 401) {
      redirectToLogin()
    } else if (res.status === 403) {
      showToast('warning', '无权限访问该资源')
    } else if (res.status === 429) {
      showToast('warning', '请求过于频繁，请稍后再试')
    } else if (res.status >= 500) {
      showToast('error', `服务器内部错误 (${res.status})，请稍后重试`)
    } else {
      showToast('error', `请求失败 (${res.status}: ${res.statusText})`)
    }
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
  id: string
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
  id: string,
  data: { title?: string; modelCode?: string },
): Promise<Conversation> {
  const dto = await request<ConversationDto>(`/api/conversations/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
  return toConversation(dto)
}

export async function deleteConversation(id: string): Promise<void> {
  await request<boolean>(`/api/conversations/${id}`, { method: 'DELETE' })
}

export async function pinConversation(id: string, isPinned: boolean): Promise<void> {
  await request<boolean>(`/api/conversations/${id}/pin?isPinned=${isPinned}`, {
    method: 'PATCH',
  })
}

// ── Messages ──

interface MessageDto {
  id: string
  conversationId: string
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
  promptTokens?: number
  completionTokens?: number
  totalTokens?: number
  feedbackType?: number
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
    usage: dto.totalTokens ? {
      promptTokens: dto.promptTokens,
      completionTokens: dto.completionTokens,
      totalTokens: dto.totalTokens,
    } : undefined,
    feedbackType: dto.feedbackType,
  }
}

export async function fetchMessages(conversationId: string): Promise<Message[]> {
  const dtos = await request<MessageDto[]>(`/api/conversations/${conversationId}/messages`)
  return dtos.map(toMessage)
}

// ── SSE Streaming ──

export interface ChatStreamEvent {
  type: 'message_start' | 'thinking_delta' | 'thinking_done' | 'content_delta' | 'tool_call_start' | 'tool_call_done' | 'tool_call_error' | 'message_done' | 'error'
  messageId?: string
  model?: string
  thinkingMode?: number
  content?: string
  thinkingTime?: number
  toolCallId?: string
  name?: string
  arguments?: string
  result?: string
  success?: boolean
  error?: string
  code?: string
  message?: string
  usage?: {
    promptTokens?: number
    completionTokens?: number
    totalTokens?: number
  }
  title?: string
}

export async function streamMessage(
  conversationId: string,
  content: string,
  thinkingMode: number,
  onEvent: (event: ChatStreamEvent) => void,
  signal?: AbortSignal,
  attachmentIds?: string[],
): Promise<void> {
  await fetchSSE(
    `${BASE_URL}/api/conversations/${conversationId}/messages`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content, thinkingMode, attachmentIds: attachmentIds?.length ? attachmentIds : undefined }),
      signal,
    },
    onEvent,
  )
}

// ── Messages Actions ──

export async function editMessage(id: string, content: string): Promise<Message> {
  const dto = await request<MessageDto>(`/api/messages/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ content }),
  })
  return toMessage(dto)
}

export async function regenerateMessage(id: string): Promise<Message> {
  const dto = await request<MessageDto>(`/api/messages/${id}/regenerate`, {
    method: 'POST',
  })
  return toMessage(dto)
}

export async function streamRegenerate(
  messageId: string,
  onEvent: (event: ChatStreamEvent) => void,
  signal?: AbortSignal,
): Promise<void> {
  await fetchSSE(
    `${BASE_URL}/api/messages/${messageId}/regenerate/stream`,
    { method: 'POST', signal },
    onEvent,
  )
}

export async function streamEditAndResend(
  messageId: string,
  content: string,
  onEvent: (event: ChatStreamEvent) => void,
  signal?: AbortSignal,
): Promise<void> {
  await fetchSSE(
    `${BASE_URL}/api/messages/${messageId}/edit-and-resend`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content }),
      signal,
    },
    onEvent,
  )
}

export async function stopGeneration(id: string): Promise<void> {
  await request<void>(`/api/messages/${id}/stop`, { method: 'POST' })
}

// ── Feedback ──

export async function submitFeedback(
  messageId: string,
  type: 'like' | 'dislike',
  reason?: string,
): Promise<void> {
  await request<void>(`/api/messages/${messageId}/feedback`, {
    method: 'POST',
    body: JSON.stringify({ type: type === 'like' ? 1 : 2, reason }),
  })
}

export async function deleteFeedback(messageId: string): Promise<void> {
  await request<void>(`/api/messages/${messageId}/feedback`, { method: 'DELETE' })
}

// ── Models ──

interface ModelInfoDto {
  code: string
  name: string
  supportThinking: boolean
  supportVision: boolean
  supportImageGeneration: boolean
  supportFunctionCalling: boolean
  provider?: string
}

export async function fetchModels(): Promise<ModelInfo[]> {
  const dtos = await request<ModelInfoDto[]>('/api/models')
  return dtos.map((d) => ({
    id: d.code,
    name: d.name,
    provider: d.provider ?? '',
    supportThinking: d.supportThinking,
    supportVision: d.supportVision,
    supportImageGeneration: d.supportImageGeneration,
    supportFunctionCalling: d.supportFunctionCalling,
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
    allowTraining: dto.allowTraining,
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
      allowTraining: settings.allowTraining ?? false,
      mcpEnabled: settings.mcpEnabled,
      defaultSkill: settings.defaultSkill,
      streamingSpeed: settings.streamingSpeed,
    }),
  })
  return toUserSettings(dto)
}

// ── Share ──

export async function createShareLink(
  conversationId: string,
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

export interface SharedConversationContent {
  conversationId: string
  messages: Array<{
    id: string
    conversationId: string
    role: 'user' | 'assistant'
    content: string
    createdAt: string
    thinkingContent?: string
    toolCalls?: Array<{ id: string; name: string; status: string; arguments?: string; result?: string }>
    usage?: { promptTokens?: number; completionTokens?: number; totalTokens?: number }
  }>
  createTime: string
  expireTime?: string
}

export async function fetchSharedConversation(token: string): Promise<SharedConversationContent | null> {
  try {
    return await request<SharedConversationContent>(`/api/share/${token}`)
  } catch {
    return null
  }
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
  await request<void>(`/api/mcp/servers/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ enable: enabled }),
  })
}

// ── User Profile ──

export interface UserProfile {
  nickname: string
  account: string
  avatar?: string
}

export async function fetchUserProfile(): Promise<UserProfile> {
  return request<UserProfile>('/api/user/profile')
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

// ── Usage Statistics ──

export interface UsageSummary {
  conversations: number
  messages: number
  promptTokens: number
  completionTokens: number
  totalTokens: number
  lastActiveTime?: string
}

export interface DailyUsage {
  date: string
  calls: number
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface ModelUsage {
  modelCode: string
  calls: number
  totalTokens: number
}

export async function fetchUsageSummary(): Promise<UsageSummary> {
  return request<UsageSummary>('/api/usage/summary')
}

export async function fetchDailyUsage(start?: string, end?: string): Promise<DailyUsage[]> {
  const params = new URLSearchParams()
  if (start) params.set('start', start)
  if (end) params.set('end', end)
  const qs = params.toString()
  return request<DailyUsage[]>(`/api/usage/daily${qs ? '?' + qs : ''}`)
}

export async function fetchModelUsage(): Promise<ModelUsage[]> {
  return request<ModelUsage[]>('/api/usage/models')
}
