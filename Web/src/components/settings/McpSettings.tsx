import { useTranslation } from 'react-i18next'
import { Toggle } from '@/components/atoms/Toggle'
import { Icon } from '@/components/common/Icon'

interface McpSettingsProps {
  mcpEnabled: boolean
  onMcpEnabledChange: (v: boolean) => void
  showToolCalls: boolean
  onShowToolCallsChange: (v: boolean) => void
}

export function McpSettings({
  mcpEnabled,
  onMcpEnabledChange,
  showToolCalls,
  onShowToolCallsChange,
}: McpSettingsProps) {
  const { t } = useTranslation()
  return (
    <div className="mb-6">
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-orange-100 dark:bg-orange-900/40 text-orange-600 p-1 rounded mr-3">
          <Icon name="extension" variant="filled" size="lg" />
        </span>
        {t('settings.mcpAdvanced')}
      </h3>
      <div className="bg-gray-50 dark:bg-gray-800/50 rounded-xl p-4 border border-gray-100 dark:border-gray-700/50 mb-4">
        <h4 className="text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
          {t('settings.mcpGlobalSettings')}
        </h4>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <div className="flex-1 mr-4">
              <span className="text-sm text-gray-700 dark:text-gray-300">{t('settings.enableMcp')}</span>
              <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{t('settings.enableMcpDesc')}</div>
            </div>
            <Toggle checked={mcpEnabled} onChange={onMcpEnabledChange} size="sm" />
          </div>
          <div className="flex items-center justify-between">
            <div className="flex-1 mr-4">
              <span className="text-sm text-gray-700 dark:text-gray-300">{t('settings.showToolCalls')}</span>
              <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{t('settings.showToolCallsDesc')}</div>
            </div>
            <Toggle checked={showToolCalls} onChange={onShowToolCallsChange} size="sm" />
          </div>
        </div>
      </div>
    </div>
  )
}
