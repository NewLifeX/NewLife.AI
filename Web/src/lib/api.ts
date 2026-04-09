import type { Conversation, Message, UserSettings, ModelInfo } from '@/types'
import { showToast } from '@/stores/toastStore'

const BASE_URL = import.meta.env.VITE_API_BASE_URL || ''

const SSE_MAX_RETRIES = 3

/** 是否正在跳转登录，防止多次重定向 */
let isRedirectingToLogin = false

/** 未登录时跳转到登录页，分享页和登录页除外；登录完成后跳回当前地址 */
function redirectToLogin() {
  if (isRedirectingToLogin) return
  if (window.location.pathname.startsWith('/share/')) return
  if (window.location.pathname.toLowerCase().startsWith('/admin/')) return
  isRedirectingToLogin = true
  const returnUrl = encodeURIComponent(window.location.href)
  window.location.href = `/Admin/User/Login?r=${returnUrl}`
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
  // 204 No Content 或 202 Accepted 可能没有响应体，直接跳过 JSON 解析
  const contentType = res.headers.get('Content-Type') ?? ''
  if (res.status === 204 || (!contentType.includes('json') && !contentType.includes('text/plain'))) {
    return undefined as T
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
  modelId: number
  lastMessageTime: string
  isPinned: boolean
  icon?: string
  iconColor?: string
}

function toConversation(dto: ConversationDto): Conversation {
  return {
    id: dto.id,
    title: dto.title,
    modelId: dto.modelId,
    isPinned: dto.isPinned,
    icon: dto.icon,
    iconColor: dto.iconColor,
    updatedAt: dto.lastMessageTime,
  }
}

export async function fetchConversations(page = 1, pageSize = 50, keyword?: string): Promise<Conversation[]> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (keyword?.trim()) params.set('keyword', keyword.trim())
  const result = await request<PagedResult<ConversationDto>>(
    `/api/conversations?${params}`,
  )
  return result.items.map(toConversation)
}

/** 消息搜索结果 */
export interface MessageSearchResult {
  id: string
  conversationId: string
  conversationTitle: string
  role: string
  content: string
  createTime: string
}

/** 全文搜索消息内容 */
export async function searchMessages(keyword: string, page = 1, pageSize = 20): Promise<PagedResult<MessageSearchResult>> {
  const params = new URLSearchParams({ keyword, page: String(page), pageSize: String(pageSize) })
  return request<PagedResult<MessageSearchResult>>(`/api/messages/search?${params}`)
}

export async function createConversation(title?: string, modelId?: number): Promise<Conversation> {
  const dto = await request<ConversationDto>('/api/conversations', {
    method: 'POST',
    body: JSON.stringify({ title, modelId }),
  })
  return toConversation(dto)
}

export async function updateConversation(
  id: string,
  data: { title?: string; modelId?: number },
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
  attachments?: string
  toolCalls?: Array<{
    id: string
    name: string
    status: number
    arguments?: string
    result?: string
  }>
  inputTokens?: number
  outputTokens?: number
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
      inputTokens: dto.inputTokens,
      outputTokens: dto.outputTokens,
      totalTokens: dto.totalTokens,
    } : undefined,
    feedbackType: dto.feedbackType,
    attachments: dto.attachments,
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
    inputTokens?: number
    outputTokens?: number
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
  modelId?: number,
): Promise<void> {
  await fetchSSE(
    `${BASE_URL}/api/conversations/${conversationId}/messages`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        content,
        thinkingMode,
        modelId: modelId || undefined,
        attachmentIds: attachmentIds?.length ? attachmentIds : undefined,
      }),
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

export async function deleteMessage(id: string): Promise<void> {
  await request<void>(`/api/messages/${id}`, { method: 'DELETE' })
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
  id: number
  code: string
  name: string
  provider?: string
  supportThinking: boolean
  supportVision: boolean
  supportImageGeneration: boolean
  supportFunctionCalling: boolean
}

export async function fetchModels(): Promise<ModelInfo[]> {
  const dtos = await request<ModelInfoDto[]>('/api/models')
  return dtos.map((d) => ({
    id: d.id,
    code: d.code,
    name: d.name,
    provider: d.provider || undefined,
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
  defaultModel: number
  defaultThinkingMode: number
  contextRounds: number
  nickname: string
  userBackground: string
  responseStyle: number
  systemPrompt: string
  allowTraining: boolean
  mcpEnabled: boolean
  showToolCalls: boolean
  streamingSpeed: number
  contentWidth: number
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
    nickname: dto.nickname ?? '',
    userBackground: dto.userBackground ?? '',
    responseStyle: dto.responseStyle ?? 0,
    systemPrompt: dto.systemPrompt,
    mcpEnabled: dto.mcpEnabled,
    showToolCalls: dto.showToolCalls ?? false,
    streamingSpeed: dto.streamingSpeed,
    allowTraining: dto.allowTraining,
    contentWidth: dto.contentWidth || 960,
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
      defaultModel: settings.defaultModel ?? 0,
      defaultThinkingMode: settings.defaultThinkingMode ?? 0,
      contextRounds: settings.contextRounds ?? 10,
      nickname: settings.nickname ?? '',
      userBackground: settings.userBackground ?? '',
      responseStyle: settings.responseStyle ?? 0,
      systemPrompt: settings.systemPrompt ?? '',
      allowTraining: settings.allowTraining ?? false,
      mcpEnabled: settings.mcpEnabled,
      showToolCalls: settings.showToolCalls ?? false,
      streamingSpeed: settings.streamingSpeed,
      contentWidth: settings.contentWidth ?? 960,
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
    usage?: { inputTokens?: number; outputTokens?: number; totalTokens?: number }
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
): Promise<{ id: number; fileName: string; url: string; size: number }> {
  const formData = new FormData()
  formData.append('file', file)
  const res = await fetch(`${BASE_URL}/api/attachments`, {
    method: 'POST',
    body: formData,
  })
  if (!res.ok) throw new Error(`Upload ${res.status}`)
  return res.json()
}

export interface AttachmentInfo {
  id: number
  fileName: string
  size: number
  url: string
  isImage: boolean
}

export async function fetchAttachmentInfos(ids: number[]): Promise<AttachmentInfo[]> {
  if (ids.length === 0) return []
  return request<AttachmentInfo[]>(`/api/attachments/info?ids=${ids.join(',')}`)
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
  inputTokens: number
  outputTokens: number
  totalTokens: number
  lastActiveTime?: string
}

export interface DailyUsage {
  date: string
  calls: number
  inputTokens: number
  outputTokens: number
  totalTokens: number
}

export interface ModelUsage {
  modelId: number
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

// ── System Config ──

export interface SuggestedQuestion {
  question: string
  icon?: string
  color?: string
}

export interface SystemConfig {
  appName: string
  siteTitle: string
  suggestedQuestions: SuggestedQuestion[]
}

export async function fetchSystemConfig(): Promise<SystemConfig> {
  return request<SystemConfig>('/api/system/config')
}

// ── Skills ──

export interface Skill {
  id: number
  code: string
  name: string
  icon?: string
  category?: string
  description?: string
  isSystem: boolean
}

export async function fetchAllSkills(category?: string): Promise<Skill[]> {
  const params = category ? `?category=${encodeURIComponent(category)}` : ''
  return request<Skill[]>(`/api/skills${params}`)
}

export async function fetchUserSkills(): Promise<Skill[]> {
  return request<Skill[]>('/api/user/skills')
}

export async function fetchSkillCategories(): Promise<Record<string, string>> {
  return request<Record<string, string>>('/api/skills/categories')
}

export async function fetchMentionSkills(keyword?: string, limit = 20): Promise<Skill[]> {
  const params = new URLSearchParams()
  if (keyword) params.set('keyword', keyword)
  if (limit !== 20) params.set('limit', String(limit))
  const qs = params.toString()
  return request<Skill[]>(`/api/skills/mention${qs ? '?' + qs : ''}`)
}

export async function setConversationSkill(conversationId: string, skillId: number): Promise<void> {
  await request<void>(`/api/conversations/${conversationId}/skill`, {
    method: 'PUT',
    body: JSON.stringify({ skillId }),
  })
}

// ── Memory ──

export interface MemoryItem {
  id: string
  category: string
  key: string
  value: string
  confidence: number
  enable: boolean
  createTime: string
  updateTime: string
}

export interface MemoryList {
  total: number
  page: number
  pageSize: number
  items: MemoryItem[]
}

export async function fetchMemories(category?: string, page = 1, pageSize = 20): Promise<MemoryList> {
  const params = new URLSearchParams()
  if (category) params.set('category', category)
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))
  return request<MemoryList>(`/api/memory?${params}`)
}

export async function addMemory(data: { category?: string; key: string; value: string; confidence?: number }): Promise<{ success: boolean; id: string }> {
  return request(`/api/memory`, {
    method: 'POST',
    body: JSON.stringify(data),
  })
}

export async function updateMemory(id: string, data: { value?: string; confidence?: number; category?: string; enable?: boolean }): Promise<void> {
  await request<void>(`/api/memory/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
}

export async function deleteMemory(id: string): Promise<void> {
  await request<void>(`/api/memory/${id}`, { method: 'DELETE' })
}

// ── Image Editing ──

export interface ImageEditResult {
  created: number
  data: { revised_prompt?: string; content?: string }[]
}

export async function editImage(
  image: Blob,
  prompt: string,
  model: string,
  mask?: Blob,
  size?: string,
): Promise<ImageEditResult> {
  const formData = new FormData()
  formData.append('image', image, 'image.png')
  formData.append('prompt', prompt)
  formData.append('model', model)
  if (mask) formData.append('mask', mask, 'mask.png')
  if (size) formData.append('size', size)
  const res = await fetch(`${BASE_URL}/api/images/edits`, {
    method: 'POST',
    body: formData,
  })
  if (!res.ok) throw new Error(`Image edit ${res.status}`)
  return res.json()
}

// ── AppKey Management ──

export interface AppKeyItem {
  id: number
  name: string
  secretMask: string
  enable: boolean
  models: string | null
  expireTime: string | null
  calls: number
  totalTokens: number
  lastCallTime: string
  createTime: string
}

export interface AppKeyCreateResult {
  id: number
  name: string
  secret: string
  createTime: string
}

export async function fetchAppKeys(): Promise<AppKeyItem[]> {
  return request<AppKeyItem[]>('/api/appkeys')
}

export async function createAppKey(data: {
  name: string
  expireTime?: string
  models?: string
}): Promise<AppKeyCreateResult> {
  return request<AppKeyCreateResult>('/api/appkeys', {
    method: 'POST',
    body: JSON.stringify(data),
  })
}

export async function updateAppKey(
  id: number,
  data: { name?: string; enable?: boolean; expireTime?: string; models?: string },
): Promise<AppKeyItem> {
  return request<AppKeyItem>(`/api/appkeys/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
}

export async function deleteAppKey(id: number): Promise<void> {
  await request<void>(`/api/appkeys/${id}`, { method: 'DELETE' })
}

