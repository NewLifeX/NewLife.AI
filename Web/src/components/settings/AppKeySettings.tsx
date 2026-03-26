import { useState, useEffect, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import { Toggle } from '@/components/atoms/Toggle'
import {
  fetchAppKeys,
  createAppKey,
  updateAppKey,
  deleteAppKey,
  type AppKeyItem,
} from '@/lib/api'

export function AppKeySettings() {
  const { t } = useTranslation()
  const [keys, setKeys] = useState<AppKeyItem[]>([])
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [newName, setNewName] = useState('')
  const [creating, setCreating] = useState(false)
  const [createdSecret, setCreatedSecret] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const [deleteConfirmId, setDeleteConfirmId] = useState<number | null>(null)

  const load = useCallback(() => {
    fetchAppKeys()
      .then(setKeys)
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  useEffect(() => { load() }, [load])

  const handleCreate = async () => {
    if (!newName.trim()) return
    setCreating(true)
    try {
      const result = await createAppKey({ name: newName.trim() })
      setCreatedSecret(result.secret)
      setNewName('')
      load()
    } catch { /* handled by api.ts */ } finally {
      setCreating(false)
    }
  }

  const handleToggle = async (item: AppKeyItem, enable: boolean) => {
    setKeys((prev) => prev.map((k) => (k.id === item.id ? { ...k, enable } : k)))
    try {
      await updateAppKey(item.id, { enable })
    } catch {
      setKeys((prev) => prev.map((k) => (k.id === item.id ? { ...k, enable: !enable } : k)))
    }
  }

  const handleDelete = async (id: number) => {
    try {
      await deleteAppKey(id)
      setKeys((prev) => prev.filter((k) => k.id !== id))
    } catch { /* handled by api.ts */ }
    setDeleteConfirmId(null)
  }

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    })
  }

  const formatDate = (d: string) => {
    if (!d || d.startsWith('0001')) return '-'
    return new Date(d).toLocaleDateString()
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20 text-gray-400">
        <Icon name="hourglass_empty" className="animate-spin mr-2" />
        {t('common.loading')}
      </div>
    )
  }

  return (
    <div className="mb-10">
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-purple-100 dark:bg-purple-900/40 text-purple-600 p-1 rounded mr-3">
          <Icon name="key" variant="filled" size="lg" />
        </span>
        {t('appKey.title')}
      </h3>

      <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
        {t('appKey.desc')}
      </p>

      {/* 已创建密钥提示 */}
      {createdSecret && (
        <div className="mb-6 p-4 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
          <div className="flex items-center gap-2 mb-2">
            <Icon name="check_circle" className="text-green-600" />
            <span className="text-sm font-medium text-green-800 dark:text-green-200">
              {t('appKey.createSuccess')}
            </span>
          </div>
          <div className="flex items-center gap-2">
            <code className="flex-1 text-xs bg-white dark:bg-gray-800 px-3 py-2 rounded border border-green-200 dark:border-green-700 font-mono break-all select-all">
              {createdSecret}
            </code>
            <button
              onClick={() => copyToClipboard(createdSecret)}
              className="shrink-0 px-3 py-2 text-xs font-medium rounded-lg bg-green-600 text-white hover:bg-green-700 transition-colors"
            >
              {copied ? t('appKey.copied') : t('common.copy')}
            </button>
          </div>
          <p className="text-xs text-green-700 dark:text-green-300 mt-2">
            {t('appKey.secretOnce')}
          </p>
          <button
            onClick={() => setCreatedSecret(null)}
            className="mt-2 text-xs text-green-600 hover:underline"
          >
            {t('common.close')}
          </button>
        </div>
      )}

      {/* 创建按钮/表单 */}
      {showCreate ? (
        <div className="mb-6 p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg border border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-3">
            <input
              type="text"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
              placeholder={t('appKey.namePlaceholder')}
              maxLength={50}
              className="flex-1 px-3 py-2 text-sm rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-primary/50"
            />
            <button
              onClick={handleCreate}
              disabled={creating || !newName.trim()}
              className="px-4 py-2 text-sm font-medium rounded-lg bg-primary text-white hover:bg-blue-600 disabled:opacity-50 transition-colors"
            >
              {creating ? t('common.loading') : t('common.confirm')}
            </button>
            <button
              onClick={() => { setShowCreate(false); setNewName('') }}
              className="px-3 py-2 text-sm rounded-lg border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            >
              {t('common.cancel')}
            </button>
          </div>
        </div>
      ) : (
        <button
          onClick={() => setShowCreate(true)}
          className="mb-6 flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg border border-dashed border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:border-primary hover:text-primary transition-colors"
        >
          <Icon name="add" size="base" />
          {t('appKey.create')}
        </button>
      )}

      {/* 密钥列表 */}
      {keys.length === 0 ? (
        <div className="text-center py-12 text-gray-400">
          <Icon name="key_off" size="xl" className="mb-3" />
          <p className="text-sm">{t('appKey.empty')}</p>
        </div>
      ) : (
        <div className="space-y-3">
          {keys.map((item) => (
            <div
              key={item.id}
              className="flex items-center justify-between p-4 bg-gray-50 dark:bg-gray-800/50 rounded-lg border border-gray-200 dark:border-gray-700"
            >
              <div className="flex-1 min-w-0 mr-4">
                <div className="flex items-center gap-2 mb-1">
                  <span className="text-sm font-medium text-gray-900 dark:text-white truncate">
                    {item.name}
                  </span>
                  {!item.enable && (
                    <span className="text-[10px] bg-gray-200 dark:bg-gray-700 text-gray-500 dark:text-gray-400 px-1.5 py-0.5 rounded">
                      {t('appKey.disabled')}
                    </span>
                  )}
                </div>
                <code className="text-xs text-gray-500 dark:text-gray-400 font-mono">
                  {item.secretMask}
                </code>
                <div className="flex items-center gap-4 mt-1.5 text-xs text-gray-400">
                  <span>{t('appKey.calls')}: {item.calls.toLocaleString()}</span>
                  <span>Tokens: {item.totalTokens.toLocaleString()}</span>
                  <span>{t('appKey.created')}: {formatDate(item.createTime)}</span>
                </div>
              </div>
              <div className="flex items-center gap-3 shrink-0">
                <Toggle
                  checked={item.enable}
                  onChange={(v) => handleToggle(item, v)}
                />
                {deleteConfirmId === item.id ? (
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => handleDelete(item.id)}
                      className="px-2 py-1 text-xs font-medium rounded bg-red-500 text-white hover:bg-red-600 transition-colors"
                    >
                      {t('common.confirm')}
                    </button>
                    <button
                      onClick={() => setDeleteConfirmId(null)}
                      className="px-2 py-1 text-xs rounded border border-gray-300 dark:border-gray-600 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                    >
                      {t('common.cancel')}
                    </button>
                  </div>
                ) : (
                  <button
                    onClick={() => setDeleteConfirmId(item.id)}
                    className="p-1 text-gray-400 hover:text-red-500 transition-colors rounded hover:bg-red-50 dark:hover:bg-red-900/20"
                    title={t('common.delete')}
                  >
                    <Icon name="delete" size="base" />
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
