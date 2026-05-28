import { useTranslation } from 'react-i18next'
import type { SystemSettings } from '@/lib/api'

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
  onChange: (patch: Partial<SystemSettings>) => void
}

export function ToolsCapabilitySettings({ settings, onChange }: Props) {
  const { t } = useTranslation()

  return (
    <div className="space-y-1">
      <Toggle
        checked={settings.enableFunctionCalling}
        onChange={(v) => onChange({ enableFunctionCalling: v })}
        label={t('systemSettings.tools.enableFunctionCalling')}
        description={t('systemSettings.tools.enableFunctionCallingDesc')}
      />
      <Toggle
        checked={settings.enableMcp}
        onChange={(v) => onChange({ enableMcp: v })}
        label={t('systemSettings.tools.enableMcp')}
        description={t('systemSettings.tools.enableMcpDesc')}
      />
      <Toggle
        checked={settings.enableSuggestedQuestionCache}
        onChange={(v) => onChange({ enableSuggestedQuestionCache: v })}
        label={t('systemSettings.tools.enableSuggestedQuestionCache')}
        description={t('systemSettings.tools.enableSuggestedQuestionCacheDesc')}
      />
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.tools.streamingSpeed')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.tools.streamingSpeedDesc')}</p>
        <input
          type="range"
          min={1}
          max={6}
          step={1}
          value={settings.streamingSpeed}
          onChange={(e) => onChange({ streamingSpeed: Number(e.target.value) })}
          className="w-full accent-primary"
        />
        <div className="flex justify-between text-xs text-gray-400 mt-1">
          <span>{t('settings.speedSlow')}</span>
          <span>{settings.streamingSpeed}</span>
          <span>{t('settings.speedMax')}</span>
        </div>
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.tools.toolSlotLimit')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.tools.toolSlotLimitDesc')}</p>
        <input
          type="number"
          min={0}
          value={settings.toolSlotLimit}
          onChange={(e) => onChange({ toolSlotLimit: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.tools.toolResultMaxChars')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.tools.toolResultMaxCharsDesc')}</p>
        <input
          type="number"
          min={0}
          value={settings.toolResultMaxChars}
          onChange={(e) => onChange({ toolResultMaxChars: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.tools.toolMaxIterations')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.tools.toolMaxIterationsDesc')}</p>
        <input
          type="number"
          min={1}
          max={50}
          value={settings.toolMaxIterations}
          onChange={(e) => onChange({ toolMaxIterations: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.tools.skillBudgetChars')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.tools.skillBudgetCharsDesc')}</p>
        <input
          type="number"
          min={1000}
          step={1000}
          value={settings.skillBudgetChars}
          onChange={(e) => onChange({ skillBudgetChars: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
    </div>
  )
}
