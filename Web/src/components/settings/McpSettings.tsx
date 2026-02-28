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
  autoApproveRead: boolean
  onAutoApproveReadChange: (v: boolean) => void
  confirmDangerous: boolean
  onConfirmDangerousChange: (v: boolean) => void
}

const defaultPlugins: McpPlugin[] = [
  {
    id: 'copilot',
    name: 'GitHub Copilot Bridge',
    version: 'v1.2.4',
    description: 'Read codebase permission',
    icon: 'code',
    iconBg: 'bg-indigo-100 dark:bg-indigo-900/50',
    iconColor: 'text-indigo-600 dark:text-indigo-400',
    enabled: true,
  },
  {
    id: 'local-file',
    name: 'Local File Access',
    version: 'v0.9.0',
    description: 'Read/write local documents',
    icon: 'folder',
    iconBg: 'bg-green-100 dark:bg-green-900/50',
    iconColor: 'text-green-600 dark:text-green-400',
    enabled: false,
  },
]

export function McpSettings({
  plugins = defaultPlugins,
  onPluginToggle,
  autoApproveRead,
  onAutoApproveReadChange,
  confirmDangerous,
  onConfirmDangerousChange,
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
      <div className="bg-gray-50 dark:bg-gray-800/50 rounded-xl p-4 border border-gray-100 dark:border-gray-700/50">
        <div className="flex items-center justify-between mb-4">
          <h4 className="text-sm font-bold text-gray-800 dark:text-gray-200">{t('settings.installedPlugins')}</h4>
          <button className="text-xs text-primary hover:underline">{t('settings.managePlugins')}</button>
        </div>
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
                    {plugin.version} · {plugin.description}
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

        <div className="mt-4 pt-4 border-t border-gray-200 dark:border-gray-700">
          <h4 className="text-xs font-bold text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-3">
            {t('settings.permissions')}
          </h4>
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-sm text-gray-700 dark:text-gray-300">{t('settings.autoApproveRead')}</span>
              <Toggle checked={autoApproveRead} onChange={onAutoApproveReadChange} size="sm" />
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-gray-700 dark:text-gray-300">{t('settings.confirmDangerous')}</span>
              <Toggle checked={confirmDangerous} onChange={onConfirmDangerousChange} size="sm" />
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
