import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { captureDomAsPng, copyImageOrFallback, savePngBlob } from '@/utils/imageCapture'
import { MobileImageFallback } from '@/components/atoms/MobileImageFallback'
import { Icon } from '@/components/common/Icon'

// ── 类型 ─────────────────────────────────────────────────────────────────────

export interface KanbanCard {
  id: string
  title: string
  description?: string
  priority?: 'high' | 'medium' | 'low'
  tags?: string[]
  /** 截止日期（ISO 8601），阶段二启用 */
  dueDate?: string
  /** 负责人，阶段二启用 */
  assignee?: string
  /** 子任务清单，阶段二启用 */
  checklist?: { title: string; done: boolean }[]
  /** 完成进度 0-100，阶段二启用 */
  progress?: number
  /** 关联链接，阶段二启用 */
  link?: string
}

export interface KanbanColumn {
  id: string
  title: string
  color?: string
  /** WIP 上限，超限时标题变红，阶段二启用 */
  wipLimit?: number
  cards: KanbanCard[]
}

export type KanbanLayout = 'board' | 'swimlane'

export interface KanbanSpec {
  kanbanId: string
  title: string
  columns: KanbanColumn[]
  /** 布局模式，阶段二启用 */
  layout?: KanbanLayout
  /** 泳道分组，阶段二启用 */
  swimlanes?: { id: string; title: string; columnIds: string[] }[]
}

const PRIORITY_STYLES: Record<string, { label: string; className: string }> = {
  high:   { label: '高',   className: 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400' },
  medium: { label: '中',   className: 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400' },
  low:    { label: '低',   className: 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400' },
}

const SORT_OPTIONS = [
  { value: 'default', label: '默认' },
  { value: 'priority', label: '优先级' },
  { value: 'title', label: '标题' },
] as const

type SortMode = (typeof SORT_OPTIONS)[number]['value']

// ── 数据解析 ─────────────────────────────────────────────────────────────────

export function parseKanbanData(result: string): KanbanSpec | null {
  try {
    const raw = JSON.parse(result)
    if (!raw?.kanbanId || !Array.isArray(raw.columns)) return null
    return {
      kanbanId: String(raw.kanbanId),
      title: String(raw.title ?? ''),
      layout: (raw.layout === 'board' || raw.layout === 'swimlane') ? raw.layout : undefined,
      swimlanes: Array.isArray(raw.swimlanes)
        ? (raw.swimlanes as Record<string, unknown>[]).map((s) => ({
            id: String(s['id'] ?? ''),
            title: String(s['title'] ?? ''),
            columnIds: Array.isArray(s['columnIds']) ? (s['columnIds'] as unknown[]).map(String) : [],
          }))
        : undefined,
      columns: (raw.columns as Record<string, unknown>[]).map((c) => ({
        id: String(c['id'] ?? ''),
        title: String(c['title'] ?? ''),
        color: c['color'] ? String(c['color']) : undefined,
        wipLimit: typeof c['wipLimit'] === 'number' ? c['wipLimit'] : undefined,
        cards: Array.isArray(c['cards'])
          ? (c['cards'] as Record<string, unknown>[]).map((ca) => ({
              id: String(ca['id'] ?? ''),
              title: String(ca['title'] ?? ''),
              description: ca['description'] ? String(ca['description']) : undefined,
              priority: (['high', 'medium', 'low'] as const).includes(String(ca['priority']) as 'high' | 'medium' | 'low')
                ? (String(ca['priority']) as 'high' | 'medium' | 'low')
                : undefined,
              tags: Array.isArray(ca['tags']) ? (ca['tags'] as unknown[]).map(String) : undefined,
              dueDate: ca['dueDate'] ? String(ca['dueDate']) : undefined,
              assignee: ca['assignee'] ? String(ca['assignee']) : undefined,
              checklist: Array.isArray(ca['checklist'])
                ? (ca['checklist'] as Record<string, unknown>[]).map((cl) => ({
                    title: String(cl['title'] ?? ''),
                    done: Boolean(cl['done']),
                  }))
                : undefined,
              progress: typeof ca['progress'] === 'number' ? ca['progress'] : undefined,
              link: ca['link'] ? String(ca['link']) : undefined,
            }))
          : [],
      })),
    }
  } catch {
    return null
  }
}

// ── 辅助函数 ─────────────────────────────────────────────────────────────────

/** 收集所有唯一标签 */
function collectTags(columns: KanbanColumn[]): string[] {
  const set = new Set<string>()
  for (const col of columns) {
    for (const card of col.cards) {
      if (card.tags) {
        for (const t of card.tags) set.add(t)
      }
    }
  }
  return [...set].sort()
}

/** 按排序模式排列卡片 */
function sortCards(cards: KanbanCard[], mode: SortMode): KanbanCard[] {
  if (mode === 'default') return cards
  const copy = [...cards]
  if (mode === 'priority') {
    const order = { high: 0, medium: 1, low: 2, undefined: 3 }
    return copy.sort((a, b) => (order[a.priority ?? 'undefined'] ?? 3) - (order[b.priority ?? 'undefined'] ?? 3))
  }
  if (mode === 'title') {
    return copy.sort((a, b) => a.title.localeCompare(b.title, 'zh-Hans-CN', { sensitivity: 'base' }))
  }
  return copy
}

/** 判断卡片是否过期 */
function isOverdue(dueDate?: string): boolean {
  if (!dueDate) return false
  try {
    return new Date(dueDate) < new Date()
  } catch {
    return false
  }
}

// ── 子组件：卡片详情弹窗 ────────────────────────────────────────────────────

interface CardDetailModalProps {
  card: KanbanCard
  onClose: () => void
}

function KanbanCardDetailModal({ card, onClose }: CardDetailModalProps) {
  const priority = card.priority ? PRIORITY_STYLES[card.priority] : null
  const overdue = isOverdue(card.dueDate)
  const checklistDone = card.checklist ? card.checklist.filter((c) => c.done).length : 0
  const checklistTotal = card.checklist?.length ?? 0

  // Esc 关闭
  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [onClose])

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={onClose}>
      <div
        className="bg-white dark:bg-gray-900 rounded-xl shadow-xl border border-[var(--color-border-subtle)] w-full max-w-md mx-4 max-h-[85vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        {/* 标题栏 */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-[var(--color-border-subtle)]">
          <div className="flex items-center gap-2 min-w-0">
            <h3 className="text-base font-semibold text-gray-900 dark:text-gray-100 truncate">{card.title}</h3>
            {priority && (
              <span className={cn('shrink-0 text-[10px] font-semibold px-1.5 py-0.5 rounded', priority.className)}>
                {priority.label}
              </span>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            className="flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-400"
          >
            <Icon name="close" size="sm" />
          </button>
        </div>

        <div className="px-5 py-4 space-y-4">
          {/* 描述 */}
          {card.description && (
            <p className="text-sm text-gray-600 dark:text-gray-400 leading-relaxed">{card.description}</p>
          )}

          {/* 截止日期 */}
          {card.dueDate && (
            <div className={cn('flex items-center gap-2 text-xs', overdue ? 'text-red-600 dark:text-red-400' : 'text-gray-500 dark:text-gray-400')}>
              <Icon name="calendar_today" size="sm" />
              <span>{card.dueDate}</span>
              {overdue && <span className="font-semibold">已过期</span>}
            </div>
          )}

          {/* 负责人 */}
          {card.assignee && (
            <div className="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
              <Icon name="person" size="sm" />
              <span>{card.assignee}</span>
            </div>
          )}

          {/* 进度条 */}
          {card.progress != null && (
            <div className="space-y-1">
              <div className="flex items-center justify-between text-xs text-gray-500 dark:text-gray-400">
                <span>进度</span>
                <span>{card.progress}%</span>
              </div>
              <div className="w-full h-1.5 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                <div
                  className="h-full bg-blue-500 rounded-full transition-all duration-300"
                  style={{ width: `${Math.min(100, Math.max(0, card.progress))}%` }}
                />
              </div>
            </div>
          )}

          {/* 子任务清单 */}
          {card.checklist && card.checklist.length > 0 && (
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs text-gray-500 dark:text-gray-400">
                <span>子任务</span>
                <span>{checklistDone}/{checklistTotal}</span>
              </div>
              <div className="w-full h-1.5 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
                <div
                  className="h-full bg-green-500 rounded-full transition-all duration-300"
                  style={{ width: checklistTotal > 0 ? `${(checklistDone / checklistTotal) * 100}%` : '0%' }}
                />
              </div>
              {card.checklist.map((cl, i) => (
                <div key={i} className="flex items-center gap-2 text-xs">
                  <Icon
                    name={cl.done ? 'check_circle' : 'radio_button_unchecked'}
                    size="sm"
                    className={cl.done ? 'text-green-500' : 'text-gray-400'}
                  />
                  <span className={cn(cl.done && 'line-through text-gray-400 dark:text-gray-600')}>{cl.title}</span>
                </div>
              ))}
            </div>
          )}

          {/* 标签 */}
          {card.tags && card.tags.length > 0 && (
            <div className="flex flex-wrap gap-1">
              {card.tags.map((tag, i) => (
                <span
                  key={i}
                  className="text-[10px] px-1.5 py-0.5 rounded-full bg-[var(--color-surface-2)] text-[var(--color-text-secondary)] border border-[var(--color-border-subtle)]"
                >
                  {tag}
                </span>
              ))}
            </div>
          )}

          {/* 关联链接 */}
          {card.link && (
            <a
              href={card.link}
              target="_blank"
              rel="noopener noreferrer"
              className="flex items-center gap-2 text-xs text-blue-600 dark:text-blue-400 hover:underline"
            >
              <Icon name="open_in_new" size="sm" />
              <span className="truncate">{card.link}</span>
            </a>
          )}
        </div>
      </div>
    </div>,
    document.body,
  )
}

// ── 子组件：卡片 ──────────────────────────────────────────────────────────────

interface KanbanCardItemProps {
  card: KanbanCard
  columnId: string
  isHidden?: boolean
  onDragStart: (cardId: string, fromColumnId: string) => void
  onClick: (card: KanbanCard) => void
}

function KanbanCardItem({ card, columnId, isHidden, onDragStart, onClick }: KanbanCardItemProps) {
  const priority = card.priority ? PRIORITY_STYLES[card.priority] : null
  const overdue = isOverdue(card.dueDate)

  const handleDragStart = useCallback(
    (e: React.DragEvent) => {
      e.dataTransfer.setData('text/plain', JSON.stringify({ cardId: card.id, fromColumnId: columnId }))
      e.dataTransfer.effectAllowed = 'move'
      onDragStart(card.id, columnId)
    },
    [card.id, columnId, onDragStart],
  )

  return (
    <div
      draggable
      onDragStart={handleDragStart}
      onClick={() => onClick(card)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => { if (e.key === 'Enter') onClick(card) }}
      className={cn(
        'rounded-lg border border-[var(--color-border-subtle)] bg-[var(--color-surface-0)] p-3 shadow-soft',
        'cursor-pointer hover:shadow-md hover:border-blue-300 dark:hover:border-blue-700',
        'focus:outline-none focus:ring-2 focus:ring-blue-400 focus:ring-offset-1',
        'transition-all duration-150',
        isHidden && 'hidden',
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <p className="text-sm font-medium text-[var(--color-text-primary)] leading-snug flex-1 min-w-0">
          {card.title}
        </p>
        <div className="flex items-center gap-1 shrink-0">
          {card.link && (
            <a
              href={card.link}
              target="_blank"
              rel="noopener noreferrer"
              onClick={(e) => e.stopPropagation()}
              className="text-gray-400 hover:text-blue-500"
              title="打开链接"
            >
              <Icon name="open_in_new" size="sm" />
            </a>
          )}
          {priority && (
            <span className={cn('text-[10px] font-semibold px-1.5 py-0.5 rounded', priority.className)}>
              {priority.label}
            </span>
          )}
        </div>
      </div>

      {/* 截止日期行 */}
      {card.dueDate && (
        <div className={cn('mt-1.5 flex items-center gap-1 text-[10px]', overdue ? 'text-red-600 dark:text-red-400 font-semibold' : 'text-gray-400 dark:text-gray-500')}>
          <Icon name="calendar_today" size="sm" />
          <span>{card.dueDate}</span>
        </div>
      )}

      {/* 负责人 */}
      {card.assignee && (
        <div className="mt-1 flex items-center gap-1 text-[10px] text-gray-400 dark:text-gray-500">
          <Icon name="person" size="sm" />
          <span>{card.assignee}</span>
        </div>
      )}

      {card.description && (
        <p className="mt-1.5 text-xs text-[var(--color-text-secondary)] leading-relaxed line-clamp-2">{card.description}</p>
      )}

      {/* 进度条 */}
      {card.progress != null && (
        <div className="mt-2 w-full h-1 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
          <div
            className="h-full bg-blue-500 rounded-full transition-all"
            style={{ width: `${Math.min(100, Math.max(0, card.progress))}%` }}
          />
        </div>
      )}

      {/* 子任务勾选指示 */}
      {card.checklist && card.checklist.length > 0 && (
        <div className="mt-1.5 text-[10px] text-gray-400 dark:text-gray-500">
          {card.checklist.filter((c) => c.done).length}/{card.checklist.length} 子任务
        </div>
      )}

      {card.tags && card.tags.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1">
          {card.tags.map((tag, i) => (
            <span
              key={i}
              className="text-[10px] px-1.5 py-0.5 rounded-full bg-[var(--color-surface-2)] text-[var(--color-text-secondary)] border border-[var(--color-border-subtle)]"
            >
              {tag}
            </span>
          ))}
        </div>
      )}
    </div>
  )
}

// ── 子组件：筛选栏（无搜索框） ───────────────────────────────────────────────

interface KanbanFilterBarProps {
  activePriorities: Set<string>
  onTogglePriority: (p: string) => void
  activeTags: Set<string>
  onToggleTag: (t: string) => void
  allTags: string[]
  totalVisible: number
  totalCards: number
}

function KanbanFilterBar({
  activePriorities, onTogglePriority,
  activeTags, onToggleTag,
  allTags, totalVisible, totalCards,
}: KanbanFilterBarProps) {
  const hasActiveFilters = activePriorities.size > 0 || activeTags.size > 0

  return (
    <div className="px-4 pt-3 pb-2 border-b border-[var(--color-border-subtle)]" data-no-capture>
      <div className="flex items-center gap-1.5 flex-wrap">
        {/* 优先级过滤 */}
        {(['high', 'medium', 'low'] as const).map((p) => {
          const style = PRIORITY_STYLES[p]
          const active = activePriorities.has(p)
          return (
            <button
              key={p}
              type="button"
              onClick={() => onTogglePriority(p)}
              className={cn(
                'text-[10px] font-semibold px-2 py-0.5 rounded-full border transition-all',
                active
                  ? cn(style.className, 'border-current opacity-100')
                  : 'border-gray-200 dark:border-gray-700 text-gray-400 dark:text-gray-600 opacity-50 hover:opacity-80',
              )}
            >
              {style.label}
            </button>
          )
        })}

        {/* 分隔线 */}
        {allTags.length > 0 && <span className="w-px h-4 bg-gray-200 dark:bg-gray-700 mx-0.5" />}

        {/* 标签过滤 */}
        {allTags.map((tag) => {
          const active = activeTags.has(tag)
          return (
            <button
              key={tag}
              type="button"
              onClick={() => onToggleTag(tag)}
              className={cn(
                'text-[10px] px-2 py-0.5 rounded-full border transition-all',
                active
                  ? 'bg-blue-100 dark:bg-blue-900/30 border-blue-300 dark:border-blue-700 text-blue-700 dark:text-blue-400'
                  : 'border-gray-200 dark:border-gray-700 text-gray-400 dark:text-gray-600 opacity-50 hover:opacity-80',
              )}
            >
              {tag}
            </button>
          )
        })}

        {/* 统计 */}
        {hasActiveFilters && (
          <span className="text-[10px] text-gray-400 dark:text-gray-500 ml-auto">
            {totalVisible}/{totalCards}
          </span>
        )}
      </div>
    </div>
  )
}

// ── 主组件 ────────────────────────────────────────────────────────────────────

interface KanbanBlockProps {
  spec: KanbanSpec
  className?: string
}

export function KanbanBlock({ spec, className }: KanbanBlockProps) {
  // ── 本地可变状态（列数据，用于拖拽等交互） ──
  const [columns, setColumns] = useState<KanbanColumn[]>(() =>
    spec.columns.map((c) => ({ ...c, cards: [...c.cards] })),
  )
  // 当 spec 变化（AI 重新生成看板）时同步
  useEffect(() => {
    setColumns(spec.columns.map((c) => ({ ...c, cards: [...c.cards] })))
  }, [spec.kanbanId, spec.columns])

  // ── 交互状态 ──
  const rootRef = useRef<HTMLDivElement>(null)
  const [imageCopied, setImageCopied] = useState(false)
  const [imageCopyErr, setImageCopyErr] = useState(false)
  const [imageSaved, setImageSaved] = useState(false)
  const [fallbackBlob, setFallbackBlob] = useState<Blob | null>(null)
  const [isFullscreen, setIsFullscreen] = useState(false)

  // 折叠状态
  const [collapsedColumns, setCollapsedColumns] = useState<Set<string>>(new Set())

  // 排序模式（每列独立）
  const [sortModes, setSortModes] = useState<Record<string, SortMode>>({})

  // 筛选状态
  const [activePriorities, setActivePriorities] = useState<Set<string>>(new Set())
  const [activeTags, setActiveTags] = useState<Set<string>>(new Set())

  // 拖拽状态
  const [dragOverColumnId, setDragOverColumnId] = useState<string | null>(null)

  // 详情弹窗
  const [detailCard, setDetailCard] = useState<KanbanCard | null>(null)

  const allTags = useMemo(() => collectTags(columns), [columns])

  // ── 筛选逻辑（无搜索） ──
  const isCardVisible = useCallback(
    (card: KanbanCard): boolean => {
      if (activePriorities.size > 0 && (!card.priority || !activePriorities.has(card.priority))) return false
      if (activeTags.size > 0 && (!card.tags || !card.tags.some((t) => activeTags.has(t)))) return false
      return true
    },
    [activePriorities, activeTags],
  )

  const visibleCount = useMemo(() => {
    let count = 0
    for (const col of columns) {
      for (const card of col.cards) {
        if (isCardVisible(card)) count++
      }
    }
    return count
  }, [columns, isCardVisible])

  const totalCards = useMemo(() => {
    let count = 0
    for (const col of columns) count += col.cards.length
    return count
  }, [columns])

  // ── 筛选切换 ──
  const togglePriority = useCallback((p: string) => {
    setActivePriorities((prev) => {
      const next = new Set(prev)
      if (next.has(p)) next.delete(p); else next.add(p)
      return next
    })
  }, [])

  const toggleTag = useCallback((t: string) => {
    setActiveTags((prev) => {
      const next = new Set(prev)
      if (next.has(t)) next.delete(t); else next.add(t)
      return next
    })
  }, [])

  // ── 拖拽处理 ──
  const handleDragStart = useCallback((_cardId: string, _fromColumnId: string) => {
    /* 拖拽开始：无需额外状态，仅用于触发 drag 事件 */
  }, [])

  const handleDragOver = useCallback((e: React.DragEvent, columnId: string) => {
    e.preventDefault()
    e.dataTransfer.dropEffect = 'move'
    setDragOverColumnId(columnId)
  }, [])

  const handleDragLeave = useCallback((_e: React.DragEvent, columnId: string) => {
    setDragOverColumnId((prev) => (prev === columnId ? null : prev))
  }, [])

  const handleDrop = useCallback(
    (e: React.DragEvent, toColumnId: string) => {
      e.preventDefault()
      setDragOverColumnId(null)
      try {
        const data = JSON.parse(e.dataTransfer.getData('text/plain'))
        const cardId: string = data.cardId
        const fromColumnId: string = data.fromColumnId

        if (fromColumnId === toColumnId) return

        setColumns((prev) => {
          const next = prev.map((c) => ({ ...c, cards: [...c.cards] }))
          const fromCol = next.find((c) => c.id === fromColumnId)
          const toCol = next.find((c) => c.id === toColumnId)
          if (!fromCol || !toCol) return prev

          const cardIdx = fromCol.cards.findIndex((c) => c.id === cardId)
          if (cardIdx === -1) return prev

          const [moved] = fromCol.cards.splice(cardIdx, 1)
          toCol.cards.push(moved)
          return next
        })
      } catch {
        /* 忽略无效拖拽数据 */
      }
    },
    [],
  )

  const handleDragEnd = useCallback(() => {
    setDragOverColumnId(null)
  }, [])

  // ── 折叠切换 ──
  const toggleColumnCollapse = useCallback((colId: string) => {
    setCollapsedColumns((prev) => {
      const next = new Set(prev)
      if (next.has(colId)) next.delete(colId); else next.add(colId)
      return next
    })
  }, [])

  // ── 排序切换 ──
  const cycleSort = useCallback((colId: string) => {
    setSortModes((prev) => {
      const current = prev[colId] ?? 'default'
      const idx = SORT_OPTIONS.findIndex((o) => o.value === current)
      const next = SORT_OPTIONS[(idx + 1) % SORT_OPTIONS.length].value
      return { ...prev, [colId]: next }
    })
  }, [])

  // ── 图片导出 ──
  const copyImage = useCallback(async () => {
    if (!rootRef.current) return
    try {
      const blob = await captureDomAsPng(rootRef.current)
      const ok = await copyImageOrFallback(blob)
      if (ok) {
        setImageCopied(true)
        setTimeout(() => setImageCopied(false), 1500)
      } else {
        setFallbackBlob(blob)
      }
    } catch {
      setImageCopyErr(true)
      setTimeout(() => setImageCopyErr(false), 2000)
    }
  }, [])

  const saveImage = useCallback(async () => {
    if (!rootRef.current) return
    try {
      const blob = await captureDomAsPng(rootRef.current)
      savePngBlob(blob, `${spec.title || 'kanban'}-${Date.now()}.png`)
      setImageSaved(true)
      setTimeout(() => setImageSaved(false), 1500)
    } catch {
      /* ignore */
    }
  }, [spec.title])

  const iconBtnClass = 'flex items-center justify-center w-6 h-6 rounded transition-colors hover:bg-[var(--color-surface-2)]'

  // ── 全屏切换 ──
  const toggleFullscreen = useCallback(() => {
    setIsFullscreen((prev) => !prev)
  }, [])

  // Esc 退出全屏
  useEffect(() => {
    if (!isFullscreen) return
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') setIsFullscreen(false) }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [isFullscreen])

  if (spec.columns.length === 0) {
    return (
      <div className={cn('rounded-xl border border-[var(--color-border-subtle)] p-4 text-sm text-[var(--color-text-secondary)]', className)}>
        看板暂无数据
      </div>
    )
  }

  const boardContent = (
    <>
      {/* 标题栏 */}
      {(spec.title || true) && (
        <div className="flex items-center justify-between px-5 pt-4 pb-3 bg-[var(--color-surface-0)] border-b border-[var(--color-border-subtle)]">
          <h3 className="text-sm font-semibold text-gray-800 dark:text-gray-100">{spec.title || '看板'}</h3>
          <div className="flex items-center gap-0.5" data-no-capture>
            <button
              type="button"
              onClick={toggleFullscreen}
              title={isFullscreen ? '退出全屏' : '全屏'}
              className={cn(iconBtnClass, 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}
            >
              <Icon name={isFullscreen ? 'fullscreen_exit' : 'fullscreen'} size="sm" />
            </button>
            <button
              type="button"
              onClick={copyImage}
              title={imageCopyErr ? '复制失败' : imageCopied ? '已复制' : '复制图片'}
              className={cn(
                iconBtnClass,
                imageCopyErr ? 'text-red-500' : 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400',
              )}
            >
              <Icon name={imageCopyErr ? 'error' : imageCopied ? 'check' : 'content_copy'} size="sm" />
            </button>
            <button
              type="button"
              onClick={saveImage}
              title={imageSaved ? '已保存' : '另存为图片'}
              className={cn(iconBtnClass, 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}
            >
              <Icon name={imageSaved ? 'check' : 'save_alt'} size="sm" />
            </button>
          </div>
        </div>
      )}

      {/* 筛选栏（无搜索框） */}
      <KanbanFilterBar
        activePriorities={activePriorities}
        onTogglePriority={togglePriority}
        activeTags={activeTags}
        onToggleTag={toggleTag}
        allTags={allTags}
        totalVisible={visibleCount}
        totalCards={totalCards}
      />

      {/* 看板列：横向滚动 */}
      <div className="overflow-x-auto">
        <div className="flex gap-3 p-4 min-w-0" style={{ minWidth: `${columns.length * 220}px` }}>
          {columns.map((col) => {
            const sortedCards = sortCards(col.cards, sortModes[col.id] ?? 'default')
            const isCollapsed = collapsedColumns.has(col.id)
            const isOverWip = col.wipLimit != null && col.cards.length > col.wipLimit
            const currentSort = sortModes[col.id] ?? 'default'

            return (
              <div
                key={col.id}
                className={cn(
                  'flex-shrink-0 w-52 flex flex-col gap-2 transition-all duration-200',
                  dragOverColumnId === col.id && 'ring-2 ring-blue-400 ring-offset-2 rounded-lg',
                )}
                onDragOver={(e) => handleDragOver(e, col.id)}
                onDragLeave={(e) => handleDragLeave(e, col.id)}
                onDrop={(e) => handleDrop(e, col.id)}
                onDragEnd={handleDragEnd}
              >
                {/* 列标题 */}
                <div
                  className="flex items-center gap-2 px-1 cursor-pointer select-none"
                  onClick={() => toggleColumnCollapse(col.id)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => { if (e.key === 'Enter') toggleColumnCollapse(col.id) }}
                >
                  <Icon
                    name={isCollapsed ? 'chevron_right' : 'expand_more'}
                    size="sm"
                    className="text-gray-400 shrink-0"
                  />
                  <div
                    className="w-2.5 h-2.5 rounded-full shrink-0"
                    style={{ backgroundColor: col.color ?? '#94a3b8' }}
                  />
                  <span className={cn('text-xs font-semibold truncate', isOverWip ? 'text-red-600 dark:text-red-400' : 'text-gray-700 dark:text-gray-300')}>
                    {col.title}
                  </span>
                  <span className={cn('ml-auto text-[10px] shrink-0', isOverWip ? 'text-red-500 font-semibold' : 'text-gray-400 dark:text-gray-500')}>
                    {col.cards.length}{col.wipLimit != null ? `/${col.wipLimit}` : ''}
                  </span>
                  {/* 排序按钮 */}
                  <button
                    type="button"
                    onClick={(e) => { e.stopPropagation(); cycleSort(col.id) }}
                    title={`排序：${SORT_OPTIONS.find((o) => o.value === currentSort)?.label}`}
                    className="ml-0.5 text-gray-400 hover:text-blue-500 shrink-0"
                    data-no-capture
                  >
                    <Icon name="sort" size="sm" />
                  </button>
                </div>

                {/* 卡片列表 */}
                {!isCollapsed && (
                  <div className="flex flex-col gap-2">
                    {sortedCards.map((card) => (
                      <KanbanCardItem
                        key={card.id}
                        card={card}
                        columnId={col.id}
                        isHidden={!isCardVisible(card)}
                        onDragStart={handleDragStart}
                        onClick={setDetailCard}
                      />
                    ))}
                    {col.cards.length === 0 && (
                      <div className="text-center text-xs text-gray-400 dark:text-gray-600 py-4 rounded-lg border border-dashed border-gray-200 dark:border-gray-700">
                        空
                      </div>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
      </div>
    </>
  )

  return (
    <>
      {/* 正常模式 */}
      {!isFullscreen && (
        <div
          ref={rootRef}
          data-testid="kanban-block"
          className={cn(
            'rounded-xl border border-[var(--color-border-subtle)]',
            'bg-[var(--color-surface-1)] overflow-hidden',
            className,
          )}
        >
          {boardContent}
        </div>
      )}

      {/* 全屏模式 */}
      {isFullscreen && createPortal(
        <div className="fixed inset-0 z-40 bg-black/50 flex items-center justify-center p-4">
          <div
            ref={rootRef}
            data-testid="kanban-block"
            className={cn(
              'rounded-xl border border-[var(--color-border-subtle)]',
              'bg-[var(--color-surface-1)] overflow-hidden',
              'w-full max-w-[95vw] h-[90vh] flex flex-col',
              className,
            )}
          >
            <div className="flex-1 overflow-y-auto">
              {boardContent}
            </div>
          </div>
        </div>,
        document.body,
      )}

      {/* 详情弹窗 */}
      {detailCard && (
        <KanbanCardDetailModal card={detailCard} onClose={() => setDetailCard(null)} />
      )}

      <MobileImageFallback
        open={fallbackBlob !== null}
        blob={fallbackBlob}
        onClose={() => setFallbackBlob(null)}
        filename={`${spec.title || 'kanban'}-${Date.now()}.png`}
      />
    </>
  )
}
