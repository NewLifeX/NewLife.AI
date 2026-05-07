import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import { useSettingsStore } from '@/stores/settingsStore'
import {
  fetchMemories,
  updateMemory,
  deleteMemory,
  type MemoryItem,
} from '@/lib/api'

export function LearningSettings() {
  const { t } = useTranslation()
  const enableLearning = useSettingsStore((s) => s.enableLearning)
  const update = useSettingsStore((s) => s.update)
  const [memories, setMemories] = useState<MemoryItem[]>([])
  const [memoryTotal, setMemoryTotal] = useState(0)
  const [memoryPageIndex, setMemoryPageIndex] = useState(1)
  const [loadingMore, setLoadingMore] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    loadAll()
  }, [])

  async function loadAll() {
    setLoading(true)
    setError(null)
    try {
      const result = await fetchMemories(1)
      setMemories(result.items ?? [])
      setMemoryTotal(result.total)
      setMemoryPageIndex(1)
    } catch {
      setError(t('learning.loadError'))
    } finally {
      setLoading(false)
    }
  }

  async function loadMoreMemories() {
    setLoadingMore(true)
    try {
      const nextPage = memoryPageIndex + 1
      const result = await fetchMemories(nextPage)
      setMemories((prev) => [...prev, ...(result.items ?? [])])
      setMemoryTotal(result.total)
      setMemoryPageIndex(nextPage)
    } catch {
      // ignore
    } finally {
      setLoadingMore(false)
    }
  }

  async function handleToggleMemory(item: MemoryItem) {
    try {
      await updateMemory(item.id, { enable: !item.enable })
      setMemories((prev) => prev.map((m) => (m.id === item.id ? { ...m, enable: !m.enable } : m)))
      setMemoryTotal((prev) => prev + (item.enable ? -1 : 1))
    } catch {
      // ignore
    }
  }

  async function handleDeleteMemory(id: string) {
    try {
      await deleteMemory(id)
      const deleted = memories.find((m) => m.id === id)
      setMemories((prev) => prev.filter((m) => m.id !== id))
      if (deleted?.enable) setMemoryTotal((prev) => Math.max(0, prev - 1))
    } catch {
      // ignore
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20 text-gray-400">
        <Icon name="hourglass_empty" className="animate-spin mr-2" />
        {t('common.loading')}
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-20 text-gray-400 gap-3">
        <Icon name="error_outline" size="xl" />
        <p>{error}</p>
        <button
          onClick={loadAll}
          className="px-4 py-1.5 text-sm bg-primary text-white rounded-lg hover:bg-primary/90"
        >
          {t('common.retry') ?? '重试'}
        </button>
      </div>
    )
  }

  const activeMemories = memories.filter((m) => m.enable)
  const inactiveMemories = memories.filter((m) => !m.enable)

  return (
    <div className="mb-10">
      {/* Title */}
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-purple-100 dark:bg-purple-900/40 text-purple-600 p-1 rounded mr-3">
          <Icon name="psychology" variant="filled" size="lg" />
        </span>
        {t('learning.title')}
      </h3>

      {/* Enable learning toggle */}
      <div className="flex items-center justify-between p-3 mb-6 rounded-lg border border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50">
        <div>
          <p className="text-sm font-medium text-gray-800 dark:text-gray-200">{t('learning.enableLearning')}</p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{t('learning.enableLearningDesc')}</p>
        </div>
        <button
          onClick={() => update({ enableLearning: !enableLearning })}
          className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors focus:outline-none ${
            enableLearning ? 'bg-purple-500' : 'bg-gray-300 dark:bg-gray-600'
          }`}
          role="switch"
          aria-checked={enableLearning}
        >
          <span
            className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow transform transition-transform ${
              enableLearning ? 'translate-x-5' : 'translate-x-0'
            }`}
          />
        </button>
      </div>

      {/* Memories */}
      <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">{t('learning.memoriesDesc')}</p>
      {memories.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <Icon name="memory" size="xl" className="mb-2 opacity-40" />
          <p>{t('learning.noMemories')}</p>
        </div>
      ) : (
        <div className="space-y-6">
          {activeMemories.length > 0 && (
            <div>
              <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
                {t('learning.active')} ({memoryTotal})
              </div>
              <div className="space-y-2">
                {activeMemories.map((item) => (
                  <MemoryCard
                    key={item.id}
                    item={item}
                    onToggle={handleToggleMemory}
                    onDelete={handleDeleteMemory}
                  />
                ))}
              </div>
            </div>
          )}
          {inactiveMemories.length > 0 && (
            <div>
              <div className="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-2">
                {t('learning.inactive')} ({inactiveMemories.length})
              </div>
              <div className="space-y-2 opacity-60">
                {inactiveMemories.map((item) => (
                  <MemoryCard
                    key={item.id}
                    item={item}
                    onToggle={handleToggleMemory}
                    onDelete={handleDeleteMemory}
                  />
                ))}
              </div>
            </div>
          )}
          {/* 加载更多 */}
          {memories.length < memoryTotal && (
            <div className="flex justify-center pt-2">
              <button
                onClick={loadMoreMemories}
                disabled={loadingMore}
                className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 text-gray-600 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors disabled:opacity-50"
              >
                <Icon name={loadingMore ? 'hourglass_empty' : 'expand_more'} size="base" className={loadingMore ? 'animate-spin' : ''} />
                {loadingMore ? t('common.loading') : t('learning.loadMore', { loaded: memories.length, total: memoryTotal })}
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

// ── Sub-components ─────────────────────────────────────────────

function MemoryCard({
  item,
  onToggle,
  onDelete,
}: {
  item: MemoryItem
  onToggle: (item: MemoryItem) => void
  onDelete: (id: string) => void
}) {
  const { t } = useTranslation()
  return (
    <div className="flex items-start gap-3 p-3 rounded-lg border border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 mb-0.5">
          <span className="text-xs font-mono px-1.5 py-0.5 rounded bg-purple-100 dark:bg-purple-900/40 text-purple-600 dark:text-purple-300">
            {item.category}
          </span>
          <span className="text-xs text-gray-400">{item.key}</span>
        </div>
        <p className="text-sm text-gray-700 dark:text-gray-200 break-words">{item.value}</p>
        <div className="flex items-center gap-2 mt-1">
          <span className="text-xs text-gray-400">
            {t('learning.confidence')}: {item.confidence}%
          </span>
        </div>
      </div>
      <div className="flex items-center gap-1 shrink-0">
        <button
          onClick={() => onToggle(item)}
          title={item.enable ? t('learning.deactivate') : t('learning.activate')}
          className={`p-1 rounded hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors ${
            item.enable ? 'text-green-500' : 'text-gray-400'
          }`}
        >
          <Icon name={item.enable ? 'toggle_on' : 'toggle_off'} size="lg" />
        </button>
        <button
          onClick={() => onDelete(item.id)}
          title={t('common.delete')}
          className="p-1 rounded hover:bg-red-50 dark:hover:bg-red-900/20 text-gray-400 hover:text-red-500 transition-colors"
        >
          <Icon name="delete_outline" size="base" />
        </button>
      </div>
    </div>
  )
}
