import { useTranslation } from 'react-i18next'
import { Toggle } from '@/components/atoms/Toggle'
import { Icon } from '@/components/common/Icon'
import { cn } from '@/lib/utils'

interface McpPlugin {
  id: string
  name: string
  version: string
  description: string
  icon: string
  iconBg: string
  iconColor: string
  enabled: boolean
}

interface McpSettingsProps {
  plugins: McpPlugin[]
  onPluginToggle: (id: string, enabled: boolean) => void
  mcpEnabled: boolean
  onMcpEnabledChange: (v: boolean) => void
  showToolCalls: boolean
  onShowToolCallsChange: (v: boolean) => void
}

export function McpSettings({
  plugins,
  onPluginToggle,
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

      <div className="bg-gray-50 dark:bg-gray-800/50 rounded-xl p-4 border border-gray-100 dark:border-gray-700/50">
        <div className="flex items-center justify-between mb-4">
          <h4 className="text-sm font-bold text-gray-800 dark:text-gray-200">{t('settings.installedPlugins')}</h4>
        </div>
        {plugins.length === 0 ? (
          <div className="text-center py-8 text-sm text-gray-400 dark:text-gray-500">
            {t('settings.noPlugins')}
          </div>
        ) : (
          <div className="space-y-3">
            {plugins.map((plugin) => (
              <div
                key={plugin.id}
                className="flex items-center justify-between bg-white dark:bg-[#252528] p-3 rounded-lg shadow-sm border border-gray-100 dark:border-gray-700"
              >
                <div className="flex items-center space-x-3">
                  <div className={cn('w-8 h-8 rounded flex items-center justify-center', plugin.iconBg)}>
                    <Icon name={plugin.icon} variant="filled" size="lg" className={plugin.iconColor} />
                  </div>
                  <div>
                    <div className="text-sm font-medium text-gray-900 dark:text-gray-100">{plugin.name}</div>
                    <div className="text-[10px] text-gray-500">
                      {plugin.version ? `${plugin.version} · ` : ''}{plugin.description}
                    </div>
                  </div>
                </div>
                <Toggle
                  checked={plugin.enabled}
                  onChange={(v) => onPluginToggle(plugin.id, v)}
                  size="sm"
                />
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
