import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import {
  fetchMemories,
  updateMemory,
  deleteMemory,
  fetchUserProfile,
  fetchUserTags,
  deleteUserTag,
  type MemoryItem,
  type UserProfileInfo,
  type UserTagInfo,
} from '@/lib/api'

export function LearningSettings() {
  const { t } = useTranslation()
  const [activeSection, setActiveSection] = useState<'memories' | 'profile' | 'tags'>('memories')
  const [memories, setMemories] = useState<MemoryItem[]>([])
  const [profile, setProfile] = useState<UserProfileInfo | null>(null)
  const [tags, setTags] = useState<UserTagInfo[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    loadAll()
  }, [])

  async function loadAll() {
    setLoading(true)
    setError(null)
    try {
      const [m, p, tg] = await Promise.all([fetchMemories(), fetchUserProfile(), fetchUserTags()])
      setMemories(m)
      setProfile(p)
      setTags(tg)
    } catch {
      setError(t('learning.loadError'))
    } finally {
      setLoading(false)
    }
  }

  async function handleToggleMemory(item: MemoryItem) {
    try {
      await updateMemory(item.id, { isActive: !item.isActive })
      setMemories((prev) => prev.map((m) => (m.id === item.id ? { ...m, isActive: !m.isActive } : m)))
    } catch {
      // ignore
    }
  }

  async function handleDeleteMemory(id: number) {
    try {
      await deleteMemory(id)
      setMemories((prev) => prev.filter((m) => m.id !== id))
    } catch {
      // ignore
    }
  }

  async function handleDeleteTag(id: number) {
    try {
      await deleteUserTag(id)
      setTags((prev) => prev.filter((t) => t.id !== id))
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

  const activeMemories = memories.filter((m) => m.isActive)
  const inactiveMemories = memories.filter((m) => !m.isActive)

  return (
    <div className="mb-10">
      {/* Title */}
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-purple-100 dark:bg-purple-900/40 text-purple-600 p-1 rounded mr-3">
          <Icon name="psychology" variant="filled" size="lg" />
        </span>
        {t('learning.title')}
      </h3>

      {/* Section tabs */}
      <div className="flex gap-1 mb-6 p-1 bg-gray-100 dark:bg-gray-800 rounded-lg">
        {(['memories', 'profile', 'tags'] as const).map((section) => (
          <button
            key={section}
            onClick={() => setActiveSection(section)}
            className={`flex-1 py-1.5 text-sm font-medium rounded-md transition-colors ${
              activeSection === section
                ? 'bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow-sm'
                : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
            }`}
          >
            {t(`learning.section.${section}`)}
          </button>
        ))}
      </div>

      {/* Memories section */}
      {activeSection === 'memories' && (
        <div>
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
                    {t('learning.active')} ({activeMemories.length})
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
            </div>
          )}
        </div>
      )}

      {/* Profile section */}
      {activeSection === 'profile' && (
        <div>
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">{t('learning.profileDesc')}</p>
          {!profile || (
            !profile.summary && !profile.preferences && !profile.habits && !profile.interests
          ) ? (
            <div className="text-center py-12 text-gray-400">
              <Icon name="person_search" size="xl" className="mb-2 opacity-40" />
              <p>{t('learning.noProfile')}</p>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-3 mb-4">
                <StatCard label={t('learning.memoryCount')} value={profile.memoryCount} icon="memory" color="purple" />
                <StatCard label={t('learning.analyzeCount')} value={profile.analyzeCount} icon="analytics" color="blue" />
              </div>
              {profile.summary && (
                <ProfileSection title={t('learning.profileSummary')} content={profile.summary} />
              )}
              {profile.preferences && (
                <ProfileSection title={t('learning.profilePreferences')} content={profile.preferences} />
              )}
              {profile.habits && (
                <ProfileSection title={t('learning.profileHabits')} content={profile.habits} />
              )}
              {profile.interests && (
                <ProfileSection title={t('learning.profileInterests')} content={profile.interests} />
              )}
              {profile.lastAnalyzeTime && (
                <p className="text-xs text-gray-400 mt-2">
                  {t('learning.lastAnalyze')}: {new Date(profile.lastAnalyzeTime).toLocaleString()}
                </p>
              )}
            </div>
          )}
        </div>
      )}

      {/* Tags section */}
      {activeSection === 'tags' && (
        <div>
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">{t('learning.tagsDesc')}</p>
          {tags.length === 0 ? (
            <div className="text-center py-12 text-gray-400">
              <Icon name="label" size="xl" className="mb-2 opacity-40" />
              <p>{t('learning.noTags')}</p>
            </div>
          ) : (
            <div className="flex flex-wrap gap-2">
              {tags.map((tag) => (
                <TagChip key={tag.id} tag={tag} onDelete={handleDeleteTag} />
              ))}
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
  onDelete: (id: number) => void
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
            {t('learning.confidence')}: {Math.round(item.confidence * 100)}%
          </span>
        </div>
      </div>
      <div className="flex items-center gap-1 shrink-0">
        <button
          onClick={() => onToggle(item)}
          title={item.isActive ? t('learning.deactivate') : t('learning.activate')}
          className={`p-1 rounded hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors ${
            item.isActive ? 'text-green-500' : 'text-gray-400'
          }`}
        >
          <Icon name={item.isActive ? 'toggle_on' : 'toggle_off'} size="lg" />
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

function ProfileSection({ title, content }: { title: string; content: string }) {
  return (
    <div className="p-3 rounded-lg border border-gray-100 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50">
      <div className="text-xs font-semibold text-gray-500 dark:text-gray-400 mb-1">{title}</div>
      <p className="text-sm text-gray-700 dark:text-gray-200 whitespace-pre-wrap">{content}</p>
    </div>
  )
}

function StatCard({
  label,
  value,
  icon,
  color,
}: {
  label: string
  value: number
  icon: string
  color: 'purple' | 'blue'
}) {
  const colorMap = {
    purple: 'bg-purple-50 dark:bg-purple-900/20 text-purple-600 dark:text-purple-300',
    blue: 'bg-blue-50 dark:bg-blue-900/20 text-blue-600 dark:text-blue-300',
  }
  return (
    <div className={`p-3 rounded-lg ${colorMap[color]} flex items-center gap-3`}>
      <Icon name={icon} size="lg" />
      <div>
        <div className="text-lg font-bold">{value}</div>
        <div className="text-xs opacity-70">{label}</div>
      </div>
    </div>
  )
}

function TagChip({ tag, onDelete }: { tag: UserTagInfo; onDelete: (id: number) => void }) {
  const { t } = useTranslation()
  const weightColor =
    tag.weight >= 80
      ? 'bg-green-100 dark:bg-green-900/40 text-green-700 dark:text-green-300 border-green-200 dark:border-green-800'
      : tag.weight >= 50
        ? 'bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 border-blue-200 dark:border-blue-800'
        : 'bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400 border-gray-200 dark:border-gray-700'

  return (
    <div
      className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full border text-sm ${weightColor}`}
    >
      <span>{tag.name}</span>
      <span className="text-xs opacity-60">{tag.weight}</span>
      <button
        onClick={() => onDelete(tag.id)}
        title={t('common.delete')}
        className="ml-0.5 opacity-50 hover:opacity-100 transition-opacity"
      >
        <Icon name="close" size="sm" />
      </button>
    </div>
  )
}
