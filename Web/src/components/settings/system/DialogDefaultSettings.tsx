import { useTranslation } from 'react-i18next'
import type { SystemSettings, ModelOption } from '@/lib/api'

interface Props {
  settings: SystemSettings
  models: ModelOption[]
  onChange: (patch: Partial<SystemSettings>) => void
}

export function DialogDefaultSettings({ settings, models, onChange }: Props) {
  const { t } = useTranslation()

  return (
    <div className="space-y-1">
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.dialogDefault.defaultModel')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.dialogDefault.defaultModelDesc')}</p>
        <select
          value={settings.defaultModel}
          onChange={(e) => onChange({ defaultModel: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        >
          <option value={0}>—</option>
          {models.map((m) => (
            <option key={m.id} value={m.id}>{m.name}</option>
          ))}
        </select>
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.dialogDefault.defaultThinkingMode')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.dialogDefault.defaultThinkingModeDesc')}</p>
        <select
          value={settings.defaultThinkingMode}
          onChange={(e) => onChange({ defaultThinkingMode: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        >
          <option value={0}>{t('systemSettings.dialogDefault.thinkingAuto')}</option>
          <option value={1}>{t('systemSettings.dialogDefault.thinkingThink')}</option>
          <option value={2}>{t('systemSettings.dialogDefault.thinkingFast')}</option>
        </select>
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.dialogDefault.defaultContextRounds')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.dialogDefault.defaultContextRoundsDesc')}</p>
        <input
          type="number"
          min={0}
          value={settings.defaultContextRounds}
          onChange={(e) => onChange({ defaultContextRounds: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
    </div>
  )
}
