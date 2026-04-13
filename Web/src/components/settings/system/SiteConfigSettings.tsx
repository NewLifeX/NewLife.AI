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
          className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow transform ring-0 transition duration-200 ${checked ? 'translate-x-5' : 'translate-x-0'}`}
        />
      </button>
    </div>
  )
}

interface Props {
  settings: SystemSettings
  onChange: (patch: Partial<SystemSettings>) => void
}

export function SiteConfigSettings({ settings, onChange }: Props) {
  const { t } = useTranslation()

  return (
    <div className="space-y-1">
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.siteConfig.name')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.siteConfig.nameDesc')}</p>
        <input
          type="text"
          value={settings.name}
          onChange={(e) => onChange({ name: e.target.value })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.siteConfig.siteTitle')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.siteConfig.siteTitleDesc')}</p>
        <input
          type="text"
          value={settings.siteTitle}
          onChange={(e) => onChange({ siteTitle: e.target.value })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.siteConfig.logoUrl')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.siteConfig.logoUrlDesc')}</p>
        <input
          type="text"
          value={settings.logoUrl}
          onChange={(e) => onChange({ logoUrl: e.target.value })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <Toggle
        checked={settings.autoGenerateTitle}
        onChange={(v) => onChange({ autoGenerateTitle: v })}
        label={t('systemSettings.siteConfig.autoGenerateTitle')}
        description={t('systemSettings.siteConfig.autoGenerateTitleDesc')}
      />
    </div>
  )
}
