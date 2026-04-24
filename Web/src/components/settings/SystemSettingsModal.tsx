import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Modal } from '@/components/common/Modal'
import { Icon } from '@/components/common/Icon'
import { ScrollArea } from '@/components/common/ScrollArea'
import { showToast } from '@/stores/toastStore'
import { fetchSystemSettings, saveSystemSettings, type SystemSettings, type ModelOption } from '@/lib/api'
import { SiteConfigSettings } from './system/SiteConfigSettings'
import { DialogDefaultSettings } from './system/DialogDefaultSettings'
import { UploadShareSettings } from './system/UploadShareSettings'
import { GatewaySettings } from './system/GatewaySettings'
import { ToolsCapabilitySettings } from './system/ToolsCapabilitySettings'
import { SystemFeaturesSettings } from './system/SystemFeaturesSettings'
import { SystemLearningSettings } from './system/SystemLearningSettings'
import { ProvidersSettings } from './system/ProvidersSettings'

type SystemTab = 'siteConfig' | 'dialogDefault' | 'upload' | 'gateway' | 'providers' | 'tools' | 'features' | 'learning'

interface SystemSettingsModalProps {
  open: boolean
  onClose: () => void
}

const defaultSettings: SystemSettings = {
  name: '',
  siteTitle: '',
  logoUrl: '',
  autoGenerateTitle: true,
  defaultModel: 0,
  defaultThinkingMode: 0,
  defaultContextRounds: 10,
  maxAttachmentSize: 10,
  maxAttachmentCount: 5,
  allowedExtensions: '',
  defaultImageSize: '1024x1024',
  shareExpireDays: 7,
  enableGateway: false,
  enableGatewayPipeline: false,
  gatewayRateLimit: 0,
  upstreamRetryCount: 2,
  enableGatewayRecording: false,
  enableFunctionCalling: true,
  enableMcp: true,
  enableSuggestedQuestionCache: true,
  streamingSpeed: 3,
  toolAdvertiseThreshold: 0,
  toolResultMaxChars: 4000,
  enableUsageStats: true,
  backgroundGeneration: true,
  maxMessagesPerMinute: 0,
  enableAutoLearning: false,
  learningModel: '',
  minLearningContentLength: 200,
}

export function SystemSettingsModal({ open, onClose }: SystemSettingsModalProps) {
  const { t } = useTranslation()
  const [activeTab, setActiveTab] = useState<SystemTab>('siteConfig')
  const [settings, setSettings] = useState<SystemSettings>(defaultSettings)
  const [models, setModels] = useState<ModelOption[]>([])
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (!open) return
    setLoading(true)
    fetchSystemSettings()
      .then(({ models: m, ...rest }) => {
        setSettings(rest)
        setModels(m)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [open])

  const handleChange = (patch: Partial<SystemSettings>) => {
    setSettings((prev) => ({ ...prev, ...patch }))
  }

  const handleSave = async () => {
    setSaving(true)
    try {
      await saveSystemSettings(settings)
      showToast('success', t('systemSettings.saveSuccess'))
    } catch {
      /* ignore */
    } finally {
      setSaving(false)
    }
  }

  const tabs: { id: SystemTab; icon: string; label: string }[] = [
    { id: 'siteConfig', icon: 'web', label: t('systemSettings.tabs.siteConfig') },
    { id: 'dialogDefault', icon: 'chat', label: t('systemSettings.tabs.dialogDefault') },
    { id: 'upload', icon: 'upload_file', label: t('systemSettings.tabs.upload') },
    { id: 'gateway', icon: 'hub', label: t('systemSettings.tabs.gateway') },
    { id: 'providers', icon: 'dns', label: t('systemSettings.tabs.providers') },
    { id: 'tools', icon: 'build', label: t('systemSettings.tabs.tools') },
    { id: 'features', icon: 'settings_applications', label: t('systemSettings.tabs.features') },
    { id: 'learning', icon: 'psychology', label: t('systemSettings.tabs.learning') },
  ]

  return (
    <Modal open={open} onClose={onClose} className="h-[612px]">
      <div className="flex flex-col w-full">
        {/* 标题栏 */}
        <div className="flex-shrink-0 px-6 py-4 border-b border-gray-100 dark:border-gray-800">
          <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t('systemSettings.title')}</h2>
        </div>
        <div className="flex flex-1 min-h-0 h-[560px]">
        {/* 左侧导航 */}
        <nav className="w-48 flex-shrink-0 border-r border-gray-100 dark:border-gray-800 py-2">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={cn(
                'w-full flex items-center gap-2.5 px-3 py-2 text-sm rounded-lg mx-1 transition-colors',
                activeTab === tab.id
                  ? 'bg-primary/10 text-primary font-medium'
                  : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800',
              )}
            >
              <Icon name={tab.icon} size="base" />
              {tab.label}
            </button>
          ))}
        </nav>

        {/* 右侧内容 */}
        <div className="flex-1 flex flex-col min-w-0">
          <ScrollArea className="flex-1 px-6 py-2">
            {loading ? (
              <div className="flex items-center justify-center h-40 text-sm text-gray-400">{t('common.loading')}</div>
            ) : (
              <>
                {activeTab === 'siteConfig' && (
                  <SiteConfigSettings settings={settings} onChange={handleChange} />
                )}
                {activeTab === 'dialogDefault' && (
                  <DialogDefaultSettings settings={settings} models={models} onChange={handleChange} />
                )}
                {activeTab === 'upload' && (
                  <UploadShareSettings settings={settings} onChange={handleChange} />
                )}
                {activeTab === 'gateway' && (
                  <GatewaySettings settings={settings} onChange={handleChange} />
                )}
                {activeTab === 'providers' && (
                  <ProvidersSettings />
                )}
                {activeTab === 'tools' && (
                  <ToolsCapabilitySettings settings={settings} onChange={handleChange} />
                )}
                {activeTab === 'features' && (
                  <SystemFeaturesSettings settings={settings} onChange={handleChange} />
                )}
                {activeTab === 'learning' && (
                  <SystemLearningSettings settings={settings} onChange={handleChange} />
                )}
              </>
            )}
          </ScrollArea>

          {/* 底部保存栏（提供商管理页不需要全局保存） */}
          {activeTab !== 'providers' && (
          <div className="flex-shrink-0 border-t border-gray-100 dark:border-gray-800 px-6 py-3 flex justify-end">
            <button
              onClick={handleSave}
              disabled={saving || loading}
              className="px-4 py-2 text-sm font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {t('common.save')}
            </button>
          </div>
          )}
        </div>
        </div>
      </div>
    </Modal>
  )
}
