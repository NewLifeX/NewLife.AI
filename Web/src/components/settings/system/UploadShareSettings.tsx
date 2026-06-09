import { useTranslation } from 'react-i18next'
import type { SystemSettings } from '@/lib/api'
import { Toggle } from '@/components/atoms'

interface Props {
  settings: SystemSettings
  onChange: (patch: Partial<SystemSettings>) => void
}

export function UploadShareSettings({ settings, onChange }: Props) {
  const { t } = useTranslation()

  return (
    <div className="space-y-1">
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.upload.maxAttachmentSize')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.upload.maxAttachmentSizeDesc')}</p>
        <input
          type="number"
          min={1}
          value={settings.maxAttachmentSize}
          onChange={(e) => onChange({ maxAttachmentSize: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.upload.maxAttachmentCount')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.upload.maxAttachmentCountDesc')}</p>
        <input
          type="number"
          min={1}
          value={settings.maxAttachmentCount}
          onChange={(e) => onChange({ maxAttachmentCount: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.upload.allowedExtensions')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.upload.allowedExtensionsDesc')}</p>
        <input
          type="text"
          value={settings.allowedExtensions}
          onChange={(e) => onChange({ allowedExtensions: e.target.value })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.upload.defaultImageSize')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.upload.defaultImageSizeDesc')}</p>
        <input
          type="text"
          value={settings.defaultImageSize}
          onChange={(e) => onChange({ defaultImageSize: e.target.value })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3">
        <label className="block text-sm font-medium text-gray-900 dark:text-gray-100 mb-1">
          {t('systemSettings.upload.shareExpireMinutes')}
        </label>
        <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">{t('systemSettings.upload.shareExpireMinutesDesc')}</p>
        <input
          type="number"
          min={0}
          value={settings.shareExpireMinutes}
          onChange={(e) => onChange({ shareExpireMinutes: Number(e.target.value) })}
          className="w-full px-3 py-2 text-sm bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/50"
        />
      </div>
      <div className="py-3 flex items-center justify-between">
        <div>
          <span className="text-sm font-medium text-gray-900 dark:text-gray-100">{t('systemSettings.upload.allowAnonymousShare')}</span>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{t('systemSettings.upload.allowAnonymousShareDesc')}</p>
        </div>
        <Toggle
              checked={settings.allowAnonymousShare}
              onChange={(checked) => onChange({ allowAnonymousShare: checked })}
              size="sm"
            />
      </div>
    </div>
  )
}
