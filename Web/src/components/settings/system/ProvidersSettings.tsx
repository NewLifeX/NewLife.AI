import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import {
  fetchProviders,
  updateProvider,
  refreshProviderModels,
  fetchModelsManage,
  updateModelSettings,
} from '@/lib/api'
import type { ProviderItem, ModelManageItem } from '@/types'
import { showToast } from '@/stores/toastStore'

function Toggle({
  checked,
  onChange,
  disabled,
}: {
  checked: boolean
  onChange: (v: boolean) => void
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors focus:outline-none focus:ring-2 focus:ring-primary/40 disabled:opacity-50 ${checked ? 'bg-primary' : 'bg-gray-200 dark:bg-gray-600'}`}
    >
      <span
        className={`inline-block h-4 w-4 rounded-full bg-white shadow transition-transform ${checked ? 'translate-x-4' : 'translate-x-0.5'}`}
      />
    </button>
  )
}

// ── API Key Dialog ──

interface ApiKeyDialogProps {
  provider: ProviderItem
  onClose: () => void
  onSaved: (updated: ProviderItem) => void
}

function ApiKeyDialog({ provider, onClose, onSaved }: ApiKeyDialogProps) {
  const { t } = useTranslation()
  const [apiKey, setApiKey] = useState('')
  const [show, setShow] = useState(false)
  const [saving, setSaving] = useState(false)

  const handleSave = async () => {
    if (!apiKey.trim()) return
    setSaving(true)
    try {
      const updated = await updateProvider(provider.id, { apiKey: apiKey.trim() })
      showToast('success', t('providers.apiKeyDialog.saved'))
      onSaved(updated)
      onClose()
    } catch {
      // error toast shown by request()
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white dark:bg-gray-900 rounded-xl shadow-xl p-6 w-96 max-w-[90vw]">
        <h3 className="text-base font-semibold text-gray-900 dark:text-gray-100 mb-1">
          {t('providers.apiKeyDialog.title')}
        </h3>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-4">{provider.name}</p>
        {provider.apiKeyMasked && (
          <p className="text-xs text-gray-400 dark:text-gray-500 mb-2">
            {t('providers.apiKeyDialog.current')}: <span className="font-mono">{provider.apiKeyMasked}</span>
          </p>
        )}
        <div className="relative">
          <input
            type={show ? 'text' : 'password'}
            value={apiKey}
            onChange={(e) => setApiKey(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSave()}
            placeholder={t('providers.apiKeyDialog.placeholder')}
            className="w-full px-3 py-2 pr-10 text-sm border border-gray-200 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary/40"
          />
          <button
            type="button"
            onClick={() => setShow(!show)}
            className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
          >
            <Icon name={show ? 'visibility_off' : 'visibility'} size="sm" />
          </button>
        </div>
        <div className="flex justify-end gap-2 mt-4">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
          >
            {t('common.cancel')}
          </button>
          <button
            onClick={handleSave}
            disabled={saving || !apiKey.trim()}
            className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {saving ? t('common.loading') : t('common.save')}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Model Edit Dialog ──

interface ModelEditDialogProps {
  model: ModelManageItem
  onClose: () => void
  onSaved: (updated: Partial<ModelManageItem>) => void
}

function ModelEditDialog({ model, onClose, onSaved }: ModelEditDialogProps) {
  const { t } = useTranslation()
  const [form, setForm] = useState({
    enable: model.enable,
    contextLength: model.contextLength,
    supportThinking: model.supportThinking,
    supportFunctionCalling: model.supportFunctionCalling,
    supportVision: model.supportVision,
    supportAudio: model.supportAudio,
    supportImageGeneration: model.supportImageGeneration,
    supportVideoGeneration: model.supportVideoGeneration,
  })
  const [saving, setSaving] = useState(false)

  const setField = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) =>
    setForm((f) => ({ ...f, [key]: value }))

  const handleSave = async () => {
    setSaving(true)
    try {
      await updateModelSettings(model.id, form)
      showToast('success', t('providers.modelEdit.saved'))
      onSaved(form)
      onClose()
    } catch {
      // error toast shown by request()
    } finally {
      setSaving(false)
    }
  }

  const featureKeys = [
    ['supportThinking', 'providers.modelEdit.thinking'],
    ['supportFunctionCalling', 'providers.modelEdit.functionCalling'],
    ['supportVision', 'providers.modelEdit.vision'],
    ['supportAudio', 'providers.modelEdit.audio'],
    ['supportImageGeneration', 'providers.modelEdit.imageGeneration'],
    ['supportVideoGeneration', 'providers.modelEdit.videoGeneration'],
  ] as const

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-white dark:bg-gray-900 rounded-xl shadow-xl p-6 w-[420px] max-w-[90vw]">
        <h3 className="text-base font-semibold text-gray-900 dark:text-gray-100 mb-1">
          {t('providers.modelEdit.title')}
        </h3>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-4 font-mono">{model.name}</p>

        {/* Enable */}
        <div className="flex items-center justify-between py-2 border-b border-gray-100 dark:border-gray-800 mb-3">
          <span className="text-sm font-medium text-gray-800 dark:text-gray-200">
            {t('providers.modelEdit.enable')}
          </span>
          <Toggle checked={form.enable} onChange={(v) => setField('enable', v)} />
        </div>

        {/* Context Length */}
        <div className="flex items-center justify-between mb-4">
          <span className="text-sm text-gray-700 dark:text-gray-300">
            {t('providers.modelEdit.contextLength')}
          </span>
          <input
            type="number"
            min={0}
            value={form.contextLength}
            onChange={(e) => setField('contextLength', Number(e.target.value))}
            className="w-28 px-3 py-1.5 text-sm border border-gray-200 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-primary/40"
          />
        </div>

        {/* Features */}
        <div className="mb-6">
          <div className="text-sm font-medium text-gray-800 dark:text-gray-200 mb-2">
            {t('providers.modelEdit.features')}
          </div>
          <div className="grid grid-cols-2 gap-2">
            {featureKeys.map(([key, labelKey]) => (
              <label
                key={key}
                className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300 cursor-pointer select-none"
              >
                <input
                  type="checkbox"
                  checked={form[key]}
                  onChange={(e) => setField(key, e.target.checked)}
                  className="rounded border-gray-300 text-primary focus:ring-primary/40"
                />
                {t(labelKey)}
              </label>
            ))}
          </div>
        </div>

        <div className="flex justify-end gap-2">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
          >
            {t('common.cancel')}
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {saving ? t('common.loading') : t('common.save')}
          </button>
        </div>
      </div>
    </div>
  )
}

// ── Main Component ──

export function ProvidersSettings() {
  const { t } = useTranslation()
  const [providers, setProviders] = useState<ProviderItem[]>([])
  const [models, setModels] = useState<ModelManageItem[]>([])
  const [selectedProviderId, setSelectedProviderId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [refreshingId, setRefreshingId] = useState<number | null>(null)
  const [apiKeyProvider, setApiKeyProvider] = useState<ProviderItem | null>(null)
  const [editingModel, setEditingModel] = useState<ModelManageItem | null>(null)

  useEffect(() => {
    setLoading(true)
    Promise.all([fetchProviders(), fetchModelsManage()])
      .then(([provs, mods]) => {
        const sorted = [...provs].sort((a, b) => (b.sort - a.sort) || (b.id - a.id))
        setProviders(sorted)
        setModels(mods)
        if (sorted.length > 0) setSelectedProviderId(sorted[0].id)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  const filteredModels = (selectedProviderId ? models.filter((m) => m.providerId === selectedProviderId) : models)
    .slice()
    .sort((a, b) => (b.sort - a.sort) || (b.id - a.id))

  const handleToggleProvider = async (provider: ProviderItem) => {
    try {
      const updated = await updateProvider(provider.id, { enable: !provider.enable })
      setProviders((prev) => prev.map((p) => (p.id === provider.id ? updated : p)))
    } catch {
      // error toast shown by request()
    }
  }

  const handleRefresh = async (providerId: number, e: React.MouseEvent) => {
    e.stopPropagation()
    setRefreshingId(providerId)
    try {
      await refreshProviderModels(providerId)
      const mods = await fetchModelsManage()
      setModels(mods)
      showToast('success', t('providers.refreshSuccess'))
    } catch {
      // error toast shown by request()
    } finally {
      setRefreshingId(null)
    }
  }

  const handleToggleModel = async (model: ModelManageItem) => {
    try {
      await updateModelSettings(model.id, { enable: !model.enable })
      setModels((prev) => prev.map((m) => (m.id === model.id ? { ...m, enable: !m.enable } : m)))
    } catch {
      // error toast shown by request()
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center h-40 text-sm text-gray-400 dark:text-gray-500">
        {t('common.loading')}
      </div>
    )
  }

  const selectedProvider = providers.find((p) => p.id === selectedProviderId)

  return (
    <div className="space-y-5">
      <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t('providers.title')}</h3>

      {/* Provider Cards */}
      {providers.length === 0 ? (
        <p className="text-sm text-gray-400 dark:text-gray-500">{t('providers.noProviders')}</p>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {providers.map((provider) => (
            <div
              key={provider.id}
              onClick={() => setSelectedProviderId(provider.id)}
              className={`p-3 rounded-xl border cursor-pointer transition-all select-none ${
                selectedProviderId === provider.id
                  ? 'border-primary/60 bg-primary/5 dark:bg-primary/10 shadow-sm'
                  : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600 bg-white dark:bg-gray-900'
              }`}
            >
              <div className="flex items-center justify-between mb-1.5">
                <div className="flex items-center gap-1.5 min-w-0">
                  <span className="text-sm font-medium text-gray-900 dark:text-gray-100 truncate">
                    {provider.name}
                  </span>
                  <span className="shrink-0 text-[10px] px-1.5 py-0.5 rounded bg-gray-100 dark:bg-gray-700 text-gray-500 dark:text-gray-400 font-mono uppercase">
                    {provider.protocol}
                  </span>
                </div>
                <Toggle checked={provider.enable} onChange={(v) => { void handleToggleProvider({ ...provider, enable: !v }) }} />
              </div>
              {provider.endpoint && (
                <p className="text-xs text-gray-400 dark:text-gray-500 truncate mb-2">{provider.endpoint}</p>
              )}
              <div className="flex items-center gap-1.5 mt-2">
                <button
                  onClick={(e) => { e.stopPropagation(); setApiKeyProvider(provider) }}
                  className="flex-1 flex items-center justify-center gap-1 text-xs px-2 py-1.5 rounded-lg border border-gray-200 dark:border-gray-600 text-gray-600 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
                >
                  <Icon name="key" size="xs" />
                  {t('providers.configureKey')}
                </button>
                <button
                  onClick={(e) => handleRefresh(provider.id, e)}
                  disabled={refreshingId === provider.id}
                  className="flex-1 flex items-center justify-center gap-1 text-xs px-2 py-1.5 rounded-lg border border-gray-200 dark:border-gray-600 text-gray-600 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <Icon
                    name="refresh"
                    size="xs"
                    className={refreshingId === provider.id ? 'animate-spin' : ''}
                  />
                  {t('providers.refreshModels')}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Models Table */}
      {selectedProvider && (
        <div>
          <div className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
            {selectedProvider.name}
            <span className="ml-2 text-xs text-gray-400 dark:text-gray-500 font-normal">
              {filteredModels.length} {t('providers.models')}
            </span>
          </div>
          <div className="overflow-x-auto rounded-xl border border-gray-200 dark:border-gray-700">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 text-xs">
                  <th className="text-left px-3 py-2.5 font-medium text-gray-600 dark:text-gray-400">
                    {t('providers.modelTable.name')}
                  </th>
                  <th className="text-left px-3 py-2.5 font-medium text-gray-600 dark:text-gray-400">
                    {t('providers.modelTable.features')}
                  </th>
                  <th className="text-center px-3 py-2.5 font-medium text-gray-600 dark:text-gray-400">
                    {t('providers.modelTable.enable')}
                  </th>
                  <th className="px-3 py-2.5" />
                </tr>
              </thead>
              <tbody>
                {filteredModels.map((model) => (
                  <tr
                    key={model.id}
                    className="border-b border-gray-100 dark:border-gray-800 last:border-0 hover:bg-gray-50/50 dark:hover:bg-gray-800/30"
                  >
                    <td className="px-3 py-2">
                      <div className="font-medium text-gray-800 dark:text-gray-200 text-sm">{model.name}</div>
                      <div className="text-xs text-gray-400 dark:text-gray-500 font-mono">{model.code}</div>
                    </td>
                    <td className="px-3 py-2">
                      <div className="flex items-center gap-1">
                        {model.supportThinking && (
                          <span title={t('providers.modelEdit.thinking')} className="text-purple-500">
                            <Icon name="psychology" size="xs" />
                          </span>
                        )}
                        {model.supportFunctionCalling && (
                          <span title={t('providers.modelEdit.functionCalling')} className="text-blue-500">
                            <Icon name="build" size="xs" />
                          </span>
                        )}
                        {model.supportVision && (
                          <span title={t('providers.modelEdit.vision')} className="text-green-500">
                            <Icon name="image" size="xs" />
                          </span>
                        )}
                        {model.supportAudio && (
                          <span title={t('providers.modelEdit.audio')} className="text-orange-500">
                            <Icon name="volume_up" size="xs" />
                          </span>
                        )}
                        {model.supportImageGeneration && (
                          <span title={t('providers.modelEdit.imageGeneration')} className="text-pink-500">
                            <Icon name="brush" size="xs" />
                          </span>
                        )}
                        {model.supportVideoGeneration && (
                          <span title={t('providers.modelEdit.videoGeneration')} className="text-red-500">
                            <Icon name="videocam" size="xs" />
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-3 py-2 text-center">
                      <Toggle checked={model.enable} onChange={() => handleToggleModel(model)} />
                    </td>
                    <td className="px-3 py-2 text-right">
                      <button
                        onClick={() => setEditingModel(model)}
                        className="text-gray-400 hover:text-primary dark:hover:text-primary transition-colors"
                        title={t('common.edit')}
                      >
                        <Icon name="edit" size="sm" />
                      </button>
                    </td>
                  </tr>
                ))}
                {filteredModels.length === 0 && (
                  <tr>
                    <td colSpan={4} className="px-3 py-8 text-center text-sm text-gray-400 dark:text-gray-500">
                      {t('providers.noModels')}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* API Key Dialog */}
      {apiKeyProvider && (
        <ApiKeyDialog
          provider={apiKeyProvider}
          onClose={() => setApiKeyProvider(null)}
          onSaved={(updated) => setProviders((prev) => prev.map((p) => (p.id === updated.id ? updated : p)))}
        />
      )}

      {/* Model Edit Dialog */}
      {editingModel && (
        <ModelEditDialog
          model={editingModel}
          onClose={() => setEditingModel(null)}
          onSaved={(updated) =>
            setModels((prev) => prev.map((m) => (m.id === editingModel.id ? { ...m, ...updated } : m)))
          }
        />
      )}
    </div>
  )
}
