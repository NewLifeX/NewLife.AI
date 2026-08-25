import { useTranslation } from 'react-i18next'
import type { SystemSettings, ModelOption } from '@/lib/api'

interface Toggle {
  checked: boolean
  onChange: (v: boolean) => void
  label: string
  description?: string
}

function Toggle({ checked, onChange, label, description }: Toggle) {
  return (
    <div className="flex items-start justify-between gap-4 py-3">
      <div className="flex-1 min-w-0">
        <div className="text-sm font-medium text-gray-900 dark:text-gray-100">{label}</div>
        {description && <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{description}</div>}
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={`relative inline-flex flex-shrink-0 h-6 w-11 border-2 border-transparent rounded-full cursor-pointer transition-colors duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 ${checked ? 'bg-primary' : 'bg-gray-200 dark:bg-gray-700'}`}
      >
        <span
          aria-hidden="true"
          className="pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow ring-0"
          style={{ translate: `${checked ? 20 : 0}px 0`, transition: 'translate 0.2s ease-in-out' }}
        />
      </button>
    </div>
  )
}

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
          {models.filter((m) => m.isChatModel !== false).map((m) => (
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

      {/* 用户隔离 */}
      <Toggle
        checked={settings.enableUserIsolation}
        onChange={(v) => onChange({ enableUserIsolation: v })}
        label={t('systemSettings.dialogDefault.enableUserIsolation')}
        description={t('systemSettings.dialogDefault.enableUserIsolationDesc')}
      />

      {/* 全局系统指令 */}
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.dialogDefault.systemInstruction')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.dialogDefault.systemInstructionDesc')}</p>
        <textarea
          value={settings.systemInstruction || ''}
          onChange={(e) => onChange({ systemInstruction: e.target.value })}
          rows={5}
          placeholder={t('systemSettings.dialogDefault.systemInstructionPlaceholder')}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50 resize-y"
        />
      </div>
    </div>
  )
}
