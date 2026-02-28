import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Modal } from '@/components/common/Modal'
import { Icon } from '@/components/common/Icon'
import { ScrollArea } from '@/components/common/ScrollArea'
import { GeneralSettings } from './GeneralSettings'
import { ChatSettings } from './ChatSettings'
import { McpSettings } from './McpSettings'
import type { UserSettings } from '@/types'

type SettingsTab = 'general' | 'account' | 'chat' | 'mcp' | 'data'

interface SettingsModalProps {
  open: boolean
  onClose: () => void
  settings: UserSettings
  onSettingsChange: (partial: Partial<UserSettings>) => void
}

export function SettingsModal({
  open,
  onClose,
  settings,
  onSettingsChange,
}: SettingsModalProps) {
  const { t } = useTranslation()
  const [activeTab, setActiveTab] = useState<SettingsTab>('general')

  const tabs: { id: SettingsTab; icon: string; label: string; badge?: string }[] = [
    { id: 'general', icon: 'tune', label: t('settings.general') },
    { id: 'account', icon: 'account_circle', label: t('settings.account') },
    { id: 'chat', icon: 'chat', label: t('settings.chatPrefs') },
    { id: 'mcp', icon: 'extension', label: t('settings.mcpAdvanced'), badge: 'New' },
    { id: 'data', icon: 'storage', label: t('settings.dataManagement') },
  ]

  const update = (partial: Partial<UserSettings>) => {
    onSettingsChange(partial)
  }

  return (
    <Modal open={open} onClose={onClose} className="h-[680px]">
      <div className="w-64 bg-sidebar-light dark:bg-[#252528] border-r border-gray-100 dark:border-gray-800 flex flex-col pt-6 pb-4">
        <div className="px-6 mb-6">
          <h2 className="text-xl font-bold tracking-tight text-gray-900 dark:text-white">{t('settings.title')}</h2>
        </div>
        <nav className="flex-1 px-3 space-y-1 overflow-y-auto custom-scrollbar">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={cn(
                'flex items-center space-x-3 px-3 py-2.5 text-sm font-medium rounded-lg w-full text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
                activeTab === tab.id
                  ? 'bg-blue-50 dark:bg-blue-900/20 text-primary dark:text-blue-400'
                  : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800',
              )}
            >
              <Icon name={tab.icon} size="lg" />
              <span>{tab.label}</span>
              {tab.badge && (
                <span className="ml-auto bg-blue-100 dark:bg-blue-900 text-[10px] text-blue-600 dark:text-blue-300 font-bold px-1.5 py-0.5 rounded-full">
                  {tab.badge}
                </span>
              )}
            </button>
          ))}
        </nav>
        <div className="px-6 pt-4 border-t border-gray-100 dark:border-gray-800">
          <div className="flex items-center space-x-2 text-xs text-gray-400">
            <span>{t('common.version')}</span>
            <span className="w-1 h-1 rounded-full bg-gray-300" />
            <button className="hover:underline hover:text-primary transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 rounded">{t('common.checkUpdate')}</button>
          </div>
        </div>
      </div>

      <ScrollArea className="flex-1 bg-white dark:bg-[#1e1e20] p-8">
        {activeTab === 'general' && (
          <GeneralSettings
            language={settings.language}
            onLanguageChange={(v) => update({ language: v })}
            theme={settings.theme}
            onThemeChange={(v) => update({ theme: v })}
            fontSize={settings.fontSize}
            onFontSizeChange={(v) => update({ fontSize: v })}
          />
        )}
        {activeTab === 'chat' && (
          <ChatSettings
            mcpEnabled={settings.mcpEnabled}
            onMcpEnabledChange={(v) => update({ mcpEnabled: v })}
            defaultSkill={settings.defaultSkill}
            onDefaultSkillChange={(v) => update({ defaultSkill: v })}
            streamingSpeed={settings.streamingSpeed}
            onStreamingSpeedChange={(v) => update({ streamingSpeed: v })}
          />
        )}
        {activeTab === 'mcp' && (
          <McpSettings
            plugins={[]}
            onPluginToggle={() => {}}
            autoApproveRead={true}
            onAutoApproveReadChange={() => {}}
            confirmDangerous={true}
            onConfirmDangerousChange={() => {}}
          />
        )}
        {activeTab === 'account' && (
          <div className="text-gray-400 text-sm">{t('settings.accountNote')}</div>
        )}
        {activeTab === 'data' && (
          <div className="text-gray-400 text-sm">{t('settings.dataNote')}</div>
        )}
      </ScrollArea>
    </Modal>
  )
}
