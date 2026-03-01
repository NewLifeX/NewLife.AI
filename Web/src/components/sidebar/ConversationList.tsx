import { useState, useMemo, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { ScrollArea } from '@/components/common/ScrollArea'
import type { Conversation } from '@/types'

type TimeGroup = 'today' | 'yesterday' | 'past7days' | 'past30days' | 'earlier'

function getTimeGroup(dateStr?: string): TimeGroup {
  if (!dateStr) return 'earlier'
  const d = new Date(dateStr)
  const now = new Date()
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const startOfYesterday = new Date(startOfToday.getTime() - 86400000)
  const start7d = new Date(startOfToday.getTime() - 6 * 86400000)
  const start30d = new Date(startOfToday.getTime() - 29 * 86400000)
  if (d >= startOfToday) return 'today'
  if (d >= startOfYesterday) return 'yesterday'
  if (d >= start7d) return 'past7days'
  if (d >= start30d) return 'past30days'
  return 'earlier'
}

const groupOrder: TimeGroup[] = ['today', 'yesterday', 'past7days', 'past30days', 'earlier']

interface ConversationListProps {
  conversations: Conversation[]
  activeId?: number
  onSelect: (id: number) => void
  onDelete?: (id: number) => void
  onPin?: (id: number, isPinned: boolean) => void
  onRename?: (id: number, title: string) => void
  className?: string
}

function ConversationIcon({ conv }: { conv: Conversation }) {
  if (conv.icon && conv.iconColor) {
    return (
      <span
        className={cn('w-4 h-4 rounded-full flex items-center justify-center text-[8px] text-white')}
        style={{ backgroundColor: conv.iconColor }}
      >
        {conv.icon}
      </span>
    )
  }
  if (conv.isPinned) {
    return <Icon name="smart_toy" size="lg" className="text-gray-500" />
  }
  return <Icon name="chat_bubble_outline" size="lg" className="text-gray-400" />
}

export function ConversationList({
  conversations,
  activeId,
  onSelect,
  onDelete,
  onPin,
  onRename,
  className,
}: ConversationListProps) {
  const { t } = useTranslation()
  const [editingId, setEditingId] = useState<number | null>(null)
  const [editTitle, setEditTitle] = useState('')
  const [confirmDeleteId, setConfirmDeleteId] = useState<number | null>(null)

  useEffect(() => { setConfirmDeleteId(null) }, [activeId])

  const handleRenameStart = (conv: Conversation) => {
    setEditingId(conv.id)
    setEditTitle(conv.title)
  }

  const handleRenameSubmit = (id: number) => {
    const trimmed = editTitle.trim()
    if (trimmed && onRename) {
      onRename(id, trimmed)
    }
    setEditingId(null)
  }

  const groupLabelKey: Record<TimeGroup, string> = {
    today: 'sidebar.today',
    yesterday: 'sidebar.yesterday',
    past7days: 'sidebar.past7days',
    past30days: 'sidebar.past30days',
    earlier: 'sidebar.earlier',
  }

  const grouped = useMemo(() => {
    const map = new Map<TimeGroup, Conversation[]>()
    for (const g of groupOrder) map.set(g, [])
    for (const conv of conversations) {
      const g = getTimeGroup(conv.updatedAt)
      map.get(g)!.push(conv)
    }
    return groupOrder.filter((g) => map.get(g)!.length > 0).map((g) => ({ group: g, items: map.get(g)! }))
  }, [conversations])

  return (
    <ScrollArea className={cn('flex-1 px-3 pb-2', className)}>
      {grouped.map(({ group, items }) => (
        <div key={group}>
          <div className="text-xs text-gray-400 px-3 py-2 font-medium">{t(groupLabelKey[group])}</div>
          <ul className="space-y-0.5">
            {items.map((conv) => {
              const isActive = conv.id === activeId
              const isEditing = editingId === conv.id
              return (
                <li key={conv.id}>
                  <div
                    className={cn(
                      'group flex items-center space-x-2 px-3 py-2 rounded-lg text-sm w-full relative transition-colors',
                      isActive
                        ? 'bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-gray-100 font-medium'
                        : 'text-gray-700 dark:text-gray-300 hover:bg-gray-200/50 dark:hover:bg-gray-700/50',
                    )}
                  >
                    <button
                      onClick={() => onSelect(conv.id)}
                      className="flex items-center space-x-2 flex-1 min-w-0 text-left focus-visible:outline-none"
                    >
                      <ConversationIcon conv={conv} />
                      {isEditing ? (
                        <input
                          value={editTitle}
                          onChange={(e) => setEditTitle(e.target.value)}
                          onBlur={() => handleRenameSubmit(conv.id)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter') handleRenameSubmit(conv.id)
                            if (e.key === 'Escape') setEditingId(null)
                          }}
                          autoFocus
                          onClick={(e) => e.stopPropagation()}
                          className="flex-1 min-w-0 bg-white dark:bg-gray-700 border border-primary/40 rounded px-1 py-0.5 text-sm outline-none"
                        />
                      ) : (
                        <span className="truncate">{conv.title}</span>
                      )}
                    </button>

                    {!isEditing && (
                      <div className="hidden group-hover:flex items-center space-x-0.5 flex-shrink-0">
                        {onPin && (
                          <button
                            onClick={(e) => { e.stopPropagation(); onPin(conv.id, !conv.isPinned) }}
                            className="p-0.5 rounded hover:bg-gray-300 dark:hover:bg-gray-600 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors"
                            title={conv.isPinned ? t('sidebar.unpin') : t('sidebar.pin')}
                          >
                            <Icon name="push_pin" variant={conv.isPinned ? 'filled' : 'outlined'} size="sm" />
                          </button>
                        )}
                        {onRename && (
                          <button
                            onClick={(e) => { e.stopPropagation(); handleRenameStart(conv) }}
                            className="p-0.5 rounded hover:bg-gray-300 dark:hover:bg-gray-600 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors"
                            title={t('sidebar.rename')}
                          >
                            <Icon name="edit" size="sm" />
                          </button>
                        )}
                        {onDelete && confirmDeleteId === conv.id ? (
                          <div className="flex items-center space-x-0.5">
                            <button
                              onClick={(e) => { e.stopPropagation(); onDelete(conv.id); setConfirmDeleteId(null) }}
                              className="p-0.5 rounded bg-red-500 text-white hover:bg-red-600 transition-colors"
                              title={t('common.confirm')}
                            >
                              <Icon name="check" size="sm" />
                            </button>
                            <button
                              onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(null) }}
                              className="p-0.5 rounded hover:bg-gray-300 dark:hover:bg-gray-600 text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors"
                              title={t('common.cancel')}
                            >
                              <Icon name="close" size="sm" />
                            </button>
                          </div>
                        ) : onDelete && (
                          <button
                            onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(conv.id) }}
                            className="p-0.5 rounded hover:bg-red-100 dark:hover:bg-red-900/30 text-gray-400 hover:text-red-500 transition-colors"
                            title={t('sidebar.delete')}
                          >
                            <Icon name="delete" size="sm" />
                          </button>
                        )}
                      </div>
                    )}

                    {!isEditing && conv.isPinned && (
                      <span className="absolute right-3 rotate-45 transform group-hover:hidden">
                        <Icon name="push_pin" variant="filled" size="xs" className="text-gray-400" />
                      </span>
                    )}
                  </div>
                </li>
              )
            })}
          </ul>
        </div>
      ))}
    </ScrollArea>
  )
}
