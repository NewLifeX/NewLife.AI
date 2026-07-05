import { type ReactNode, useState, useRef, useMemo, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { cn, formatRelativeTime, formatExactTime } from '@/lib/utils'
import { Avatar } from '@/components/common/Avatar'
import { Icon } from '@/components/common/Icon'
import { MessageActions } from './MessageActions'
import { TypingCursor } from './TypingCursor'
import { ToolCallBadge } from './ToolCallBadge'
import { WidgetBlock, parseWidgetData } from './WidgetBlock'
import { ChartBlock, parseChartData } from './ChartBlock'
import { TimelineBlock, parseTimelineData } from './TimelineBlock'
import { MindmapBlock, parseMindmapData } from './MindmapBlock'
import { KanbanBlock, parseKanbanData } from './KanbanBlock'
import { SlideBlock, parseSlideData } from './SlideBlock'
import { SpreadsheetBlock, parseSpreadsheetData } from './SpreadsheetBlock'
import { DocBlock, parseDocData } from './DocBlock'

import { fetchAttachmentInfos, type AttachmentInfo } from '@/lib/api'
import type { ToolCall, TokenUsage } from '@/types'
import { useSettingsStore } from '@/stores/settingsStore'
import { useChatStore } from '@/stores/chatStore'

interface MessageBubbleProps {
  role: 'user' | 'assistant'
  content: ReactNode
  userAvatar?: string
  isStreaming?: boolean
  thinkingBlock?: ReactNode
  toolCalls?: ToolCall[]
  attachments?: string
  onCopy?: () => void
  onRegenerate?: () => void
  onLike?: () => void
  onDislike?: () => void
  onShare?: () => void
  liked?: boolean
  disliked?: boolean
  onEdit?: () => void
  onEditSubmit?: (content: string) => void
  onEditSaveOnly?: (content: string) => void
  onEditCancel?: () => void
  onDelete?: () => void
  isEditing?: boolean
  rawContent?: string
  createdAt?: string
  isError?: boolean
  usage?: TokenUsage
  model?: string
  className?: string
}

/** 工具调用中文显示名 */
const TOOL_DISPLAY_NAMES: Record<string, string> = {
  show_timeline: '时间轴',
  show_chart: '图表',
  show_widget: '可视化',
  show_china_map: '地图',
  show_mindmap: '思维导图',
  show_kanban: '看板',
  build_ppt: '幻灯片',
  build_excel: '电子表格',
  build_doc: '文档',
  ask_user: '提问',
}

/** 检测技术性内部错误（如 JSON 解析失败），不应直接暴露给用户 */
function isInternalError(msg: string): boolean {
  return /JSON 格式错误|LineNumber|BytePositionInLine|Expected either|is invalid after a value/i.test(msg)
}

/** 从 ToolCall 中提取结构化错误/去重信息（后端通过 ToolException / ToolError / 去重逻辑注入） */
function getToolOutputInfo(tc: ToolCall): { type: 'error' | 'duplicate'; forUser: string } | null {
  if (tc.status === 'error') {
    // ToolException → tc.result 为纯文本，优先显示
    if (tc.result && !tc.result.startsWith('{')) {
      // 过滤技术性内部错误，不暴露给用户
      if (isInternalError(tc.result)) return { type: 'error', forUser: '' }
      return { type: 'error', forUser: tc.result }
    }
    // 通用异常 → tc.result 为 ToolError JSON，提取 for_user 或 hint
    if (tc.result) {
      try {
        const parsed = JSON.parse(tc.result) as Record<string, unknown>
        const msg = (parsed.for_user ?? parsed.hint ?? '') as string
        if (msg) return { type: 'error', forUser: msg }
      } catch { /* ignore */ }
    }
    return { type: 'error', forUser: '' }
  }
  // 去重调用（status='done' 但 result 为 {"kind":"duplicate","for_user":"..."}）
  if (tc.result) {
    try {
      const parsed = JSON.parse(tc.result) as Record<string, unknown>
      if (parsed.kind === 'duplicate') return { type: 'duplicate', forUser: (parsed.for_user as string) ?? '' }
    } catch { /* ignore */ }
  }
  return null
}

/** 根据工具名称将结果分发到对应的可视化 Block 组件 */
function renderToolResult(tc: ToolCall, showToolCalls: boolean) {
  if (tc.name === 'build_ppt') {
    if (tc.status === 'calling') return (<div key={tc.id} className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500 dark:text-gray-400 animate-pulse"><Icon name="hourglass_top" size="sm" /><span>正在生成幻灯片…</span></div>)
    const sd = parseSlideData(tc.result)
    if (sd) return (<div key={tc.id} className="mt-4">{showToolCalls && <ToolCallBadge name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />}<SlideBlock data={sd} /></div>)
  }
  if (tc.name === 'build_excel') {
    if (tc.status === 'calling') return (<div key={tc.id} className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500 dark:text-gray-400 animate-pulse"><Icon name="hourglass_top" size="sm" /><span>正在生成电子表格…</span></div>)
    const xd = parseSpreadsheetData(tc.result)
    if (xd) return (<div key={tc.id} className="mt-4">{showToolCalls && <ToolCallBadge name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />}<SpreadsheetBlock data={xd} /></div>)
  }
  if (tc.name === 'build_doc') {
    if (tc.status === 'calling') return (<div key={tc.id} className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500 dark:text-gray-400 animate-pulse"><Icon name="hourglass_top" size="sm" /><span>正在生成文档…</span></div>)
    const dd = parseDocData(tc.result)
    if (dd) return (<div key={tc.id} className="mt-4">{showToolCalls && <ToolCallBadge name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />}<DocBlock data={dd} /></div>)
  }
  if (tc.name === 'show_widget' || tc.name === 'show_china_map') {
    if (tc.status === 'calling') return (<div key={tc.id} className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500 dark:text-gray-400 animate-pulse"><Icon name="hourglass_top" size="sm" /><span>正在生成可视化…</span></div>)
    const wd = parseWidgetData(tc.result)
    if (wd) return (<div key={tc.id} className="mt-4">{showToolCalls && <ToolCallBadge name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />}<WidgetBlock data={wd} /></div>)
  }
  if (tc.name === 'show_chart') {
    if (tc.status === 'calling') return (<div key={tc.id} className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500 dark:text-gray-400 animate-pulse"><Icon name="hourglass_top" size="sm" /><span>正在渲染图表…</span></div>)
    const cd = parseChartData(tc.result)
    if (cd) return (<div key={tc.id} className="mt-4">{showToolCalls && <ToolCallBadge name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />}<ChartBlock spec={cd} /></div>)
  }
  if (tc.name === 'show_timeline') {
    if (tc.status === 'calling') return (<div key={tc.id} className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500 dark:text-gray-400 animate-pulse"><Icon name="hourglass_top" size="sm" /><span>正在生成时间轴…</span></div>)
    const td = parseTimelineData(tc.result ?? '')
    if (td) return (<div key={tc.id} className="mt-4">{showToolCalls && <ToolCallBadge name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />}<TimelineBlock key={td.timelineId} spec={td} /></div>)
  }
  if (tc.name === 'show_mindmap') {
    if (tc.status === 'calling') return (<div key={tc.id} className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500 dark:text-gray-400 animate-pulse"><Icon name="hourglass_top" size="sm" /><span>正在生成思维导图…</span></div>)
    const md = parseMindmapData(tc.result ?? '')
    if (md) return (<div key={tc.id} className="mt-4">{showToolCalls && <ToolCallBadge name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />}<MindmapBlock spec={md} /></div>)
  }
  if (tc.name === 'show_kanban') {
    if (tc.status === 'calling') return (<div key={tc.id} className="flex items-center gap-2 px-3 py-2 text-sm text-gray-500 dark:text-gray-400 animate-pulse"><Icon name="hourglass_top" size="sm" /><span>正在生成看板…</span></div>)
    const kd = parseKanbanData(tc.result ?? '')
    if (kd) return (<div key={tc.id} className="mt-4">{showToolCalls && <ToolCallBadge name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />}<KanbanBlock spec={kd} /></div>)
  }
  // 检查工具调用有无结构化错误/去重信息（来自后端 ToolException / 去重逻辑）
  const outputInfo = getToolOutputInfo(tc)
  if (outputInfo) {
    if (outputInfo.type === 'error') {
      return (
        <div key={tc.id} className="rounded-lg border border-red-200 dark:border-red-700 bg-red-50 dark:bg-red-900/20 px-3 py-2 text-sm text-red-600 dark:text-red-400">
          {outputInfo.forUser || `${TOOL_DISPLAY_NAMES[tc.name] ?? tc.name} 生成失败`}
        </div>
      )
    }
    if (outputInfo.type === 'duplicate') {
      return (
        <div key={tc.id} className="rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/30 px-3 py-2 text-sm text-gray-400 dark:text-gray-500">
          {outputInfo.forUser || `${TOOL_DISPLAY_NAMES[tc.name] ?? tc.name} 已跳过（重复调用）`}
        </div>
      )
    }
  }
  return null
}

export function MessageBubble({
  role,
  content,
  isStreaming = false,
  thinkingBlock,
  toolCalls,
  attachments,
  onCopy,
  onRegenerate,
  onLike,
  onDislike,
  onShare,
  liked = false,
  disliked = false,
  onEdit,
  onEditSubmit,
  onEditSaveOnly,
  onEditCancel,
  onDelete,
  isEditing = false,
  rawContent,
  createdAt,
  isError = false,
  usage,
  model,
  className,
}: MessageBubbleProps) {
  const { t, i18n } = useTranslation()
  const locale = i18n.language
  const showToolCalls = useSettingsStore((s) => s.showToolCalls)
  const models = useChatStore((s) => s.models)
  const modelName = model ? (models.find((m) => m.code === model)?.name ?? model) : undefined
  const [editValue, setEditValue] = useState(rawContent ?? '')
  const editRef = useRef<HTMLTextAreaElement>(null)

  const attachmentIds = useMemo(() => {
    if (!attachments) return []
    try { return (JSON.parse(attachments) as unknown[]).map(Number).filter(Boolean) } catch { return [] }
  }, [attachments])

  const [attachInfos, setAttachInfos] = useState<AttachmentInfo[]>([])
  const [attachError, setAttachError] = useState(false)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const lightboxRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (attachmentIds.length === 0) return
    setAttachError(false)
    fetchAttachmentInfos(attachmentIds).then(setAttachInfos).catch(() => setAttachError(true))
  }, [attachmentIds])

  useEffect(() => {
    if (!previewUrl) return
    document.body.style.overflow = 'hidden'
    lightboxRef.current?.focus()
    return () => { document.body.style.overflow = '' }
  }, [previewUrl])

  if (role === 'user') {
    return (
      <div className={cn('flex flex-col items-end mb-6 group', className)}>
        <div className="max-w-[75%] relative">
          {isEditing ? (
            <div className="bg-[var(--color-surface-2)] rounded-2xl rounded-tr-sm px-4 py-3 shadow-soft">
              <textarea
                ref={editRef}
                value={editValue}
                onChange={(e) => setEditValue(e.target.value)}
                className="w-full bg-transparent text-[var(--color-text-primary)] text-[15px] leading-7 resize-none outline-none min-h-[60px]"
                rows={Math.max(2, editValue.split('\n').length)}
                autoFocus
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault()
                    if (editValue.trim()) onEditSubmit?.(editValue.trim())
                  }
                  if (e.key === 'Escape') onEditCancel?.()
                }}
              />
              <div className="flex justify-end space-x-2 mt-2">
                <button
                  onClick={onEditCancel}
                  className="px-3 py-1 text-xs text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)] rounded-md hover:bg-[var(--color-surface-2)] transition-colors"
                >
                  {t('common.cancel')}
                </button>
                {onEditSaveOnly && (
                  <button
                    onClick={() => editValue.trim() && onEditSaveOnly(editValue.trim())}
                    className="px-3 py-1 text-xs text-[var(--color-text-primary)] border border-[var(--color-border-default)] hover:bg-[var(--color-surface-2)] rounded-md transition-colors disabled:opacity-50"
                    disabled={!editValue.trim()}
                  >
                    {t('common.save')}
                  </button>
                )}
                <button
                  onClick={() => editValue.trim() && onEditSubmit?.(editValue.trim())}
                  className="px-3 py-1 text-xs text-white bg-primary hover:bg-primary/90 rounded-md transition-colors disabled:opacity-50"
                  disabled={!editValue.trim()}
                >
                  {t('common.send')}
                </button>
              </div>
            </div>
          ) : (
            <>
              <div className="bg-[var(--color-surface-2)] text-[var(--color-text-primary)] rounded-2xl rounded-tr-sm px-5 py-3.5 leading-7 shadow-soft" style={{ fontSize: 'var(--chat-font-size, 16px)' }}>
                {content}
              </div>
              {attachInfos.length > 0 && (
                <div className="flex flex-wrap gap-1.5 mt-1.5 justify-end">
                  {attachInfos.map((info) =>
                    info.isImage ? (
                      <button
                        key={info.id}
                        onClick={() => setPreviewUrl(info.url)}
                        className="w-20 h-20 rounded-lg overflow-hidden border border-[var(--color-border-default)] hover:opacity-80 transition-opacity"
                      >
                        <img src={info.url} alt={info.fileName} className="w-full h-full object-cover" />
                      </button>
                    ) : (
                      <a
                        key={info.id}
                        href={info.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center space-x-1 px-2 py-1 bg-gray-100 dark:bg-gray-700/50 border border-gray-200 dark:border-gray-600 rounded-md text-xs text-gray-600 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors max-w-[200px]"
                      >
                        <Icon name="attach_file" size="xs" className="flex-shrink-0" />
                        <span className="truncate">{info.fileName}</span>
                        <Icon name="download" size="xs" className="opacity-50 flex-shrink-0" />
                      </a>
                    ),
                  )}
                </div>
              )}
              {attachError && attachmentIds.length > 0 && (
                <div className="flex flex-wrap gap-1.5 mt-1.5 justify-end">
                  {attachmentIds.map((id) => (
                    <a key={id} href={`/api/attachments/${id}`} target="_blank" rel="noopener noreferrer"
                      className="inline-flex items-center space-x-1 px-2 py-1 bg-gray-100 dark:bg-gray-700/50 border border-gray-200 dark:border-gray-600 rounded-md text-xs text-gray-600 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors"
                    >
                      <Icon name="attach_file" size="xs" />
                      <span>{t('chat.attachment')}</span>
                    </a>
                  ))}
                </div>
              )}
              {(onCopy || onEdit || onDelete) && (
                <div className="absolute right-full -translate-x-1 top-2 flex md:hidden md:group-hover:flex space-x-1">
                  {onCopy && (
                    <button
                      onClick={onCopy}
                      title={t('common.copy')}
                      className="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
                    >
                      <Icon name="content_copy" size="base" />
                    </button>
                  )}
                  {onEdit && (
                    <button
                      onClick={onEdit}
                      title={t('common.edit')}
                      className="p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
                    >
                      <Icon name="edit" variant="filled" size="base" />
                    </button>
                  )}
                  {onDelete && (
                    <button
                      onClick={onDelete}
                      title={t('common.delete')}
                      className="p-1 text-gray-400 hover:text-red-500 dark:hover:text-red-400 rounded transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
                    >
                      <Icon name="delete" variant="outlined" size="base" />
                    </button>
                  )}
                </div>
              )}
            </>
          )}
          {createdAt && !isEditing && (
            <div className="mt-1 text-right">
              <span className="text-[11px] text-gray-400 dark:text-gray-500 cursor-default" title={formatExactTime(createdAt)}>
                {formatRelativeTime(createdAt, locale)}
              </span>
            </div>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className={cn('mb-8 group w-full', className)}>
      <div className="flex items-center gap-2 mb-3">
        <Avatar type="ai" size="sm" />
      </div>
      <div className="w-full">
        <div
          className={cn(
            'leading-7',
            isError
              ? 'bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/50 rounded-xl px-4 py-3 text-red-700 dark:text-red-400'
              : 'text-gray-900 dark:text-gray-100',
          )}
          style={{ fontSize: 'var(--chat-font-size, 16px)' }}
        >
          {thinkingBlock}

          {toolCalls && toolCalls.length > 0 && (
            <div className="flex flex-col gap-3 mb-4">
              {toolCalls.map((tc) => {
                const resultBlock = renderToolResult(tc, showToolCalls)
                if (resultBlock) return resultBlock
                if (!showToolCalls) return null
                return (
                  <ToolCallBadge key={tc.id} name={tc.name} status={tc.status} arguments={tc.arguments} result={tc.result} showDetails={showToolCalls} />
                )
              })}
            </div>
          )}

          <div className="max-w-none">
            {content}
          </div>

          {isStreaming && (
            <div className="mt-1">
              <TypingCursor />
            </div>
          )}
        </div>

        <div className="flex flex-wrap items-center mt-2">
          <MessageActions
            onCopy={onCopy}
            onLike={onLike}
            onRegenerate={onRegenerate}
            onDislike={onDislike}
            onShare={onShare}
            onDelete={onDelete}
            liked={liked}
            disliked={disliked}
            className="mt-0"
          />
          <div className="ml-auto flex items-center space-x-2 mr-1">
            {modelName && (
              <span className="text-[11px] text-gray-400 dark:text-gray-500 cursor-default whitespace-nowrap">
                {modelName}
              </span>
            )}
            {usage && usage.totalTokens != null && (
              <span className="text-[11px] text-gray-400 dark:text-gray-500 cursor-default whitespace-nowrap" title={`${t('chat.inputTokens')}: ${usage.inputTokens ?? 0} | ${t('chat.outputTokens')}: ${usage.outputTokens ?? 0}`}>
                {usage.inputTokens != null && usage.outputTokens != null
                  ? `${usage.inputTokens} + ${usage.outputTokens} = ${usage.totalTokens} tokens`
                  : `${usage.totalTokens} tokens`}
              </span>
            )}
            {createdAt && (
              <span className="text-[11px] text-gray-400 dark:text-gray-500 cursor-default whitespace-nowrap" title={formatExactTime(createdAt)}>
                {formatRelativeTime(createdAt, locale)}
              </span>
            )}
          </div>
        </div>
      </div>
      {previewUrl && (
        <div
          ref={lightboxRef}
          role="dialog"
          aria-modal="true"
          tabIndex={-1}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70"
          onClick={() => setPreviewUrl(null)}
          onKeyDown={(e) => { if (e.key === 'Escape') setPreviewUrl(null) }}
        >
          <button
            className="absolute top-4 right-4 text-white hover:text-gray-300 p-2 rounded-full"
            onClick={() => setPreviewUrl(null)}
            aria-label={t('common.close')}
          >
            <Icon name="close" size="xl" />
          </button>
          <img
            src={previewUrl}
            alt="Preview"
            className="max-w-[90vw] max-h-[90vh] rounded-lg shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          />
        </div>
      )}
    </div>
  )
}
