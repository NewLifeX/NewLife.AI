import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Modal } from '@/components/common/Modal'
import { Icon } from '@/components/common/Icon'
import { ScrollArea } from '@/components/common/ScrollArea'
import { GeneralSettings } from './GeneralSettings'
import { ChatSettings } from './ChatSettings'
import { PersonalizationSettings } from './PersonalizationSettings'
import { McpSettings } from './McpSettings'
import { DataSettings } from './DataSettings'
import { AppKeySettings } from './AppKeySettings'
import type { UserSettings, ModelInfo } from '@/types'
import { fetchMcpServers, toggleMcpServer, fetchUserProfile, type McpServer, type UserProfile } from '@/lib/api'

type SettingsTab = 'account' | 'general' | 'personalization' | 'chat' | 'mcp' | 'appkeys' | 'data'

interface SettingsModalProps {
  open: boolean
  onClose: () => void
  settings: UserSettings
  onSettingsChange: (partial: Partial<UserSettings>) => void
  onDataCleared?: () => void
  models?: ModelInfo[]
}

export function SettingsModal({
  open,
  onClose,
  settings,
  onSettingsChange,
  onDataCleared,
  models = [],
}: SettingsModalProps) {
  const { t } = useTranslation()
  const [activeTab, setActiveTab] = useState<SettingsTab>('account')
  const [mcpServers, setMcpServers] = useState<McpServer[]>([])
  const [userProfile, setUserProfile] = useState<UserProfile | null>(null)
  const [legalDialog, setLegalDialog] = useState<'terms' | 'privacy' | null>(null)
  const [avatarImgError, setAvatarImgError] = useState(false)

  useEffect(() => {
    if (open) {
      fetchMcpServers().then(setMcpServers).catch((e) => console.error('Failed to load MCP servers:', e))
      fetchUserProfile().then((p) => { setUserProfile(p); setAvatarImgError(false) }).catch(() => {})
    }
  }, [open])

  const tabs: { id: SettingsTab; icon: string; label: string; badge?: string }[] = [
    { id: 'account', icon: 'account_circle', label: t('settings.account') },
    { id: 'general', icon: 'tune', label: t('settings.general') },
    { id: 'personalization', icon: 'auto_awesome', label: t('personalization.title') },
    { id: 'chat', icon: 'chat', label: t('settings.chatPrefs') },
    { id: 'mcp', icon: 'extension', label: t('settings.mcpAdvanced'), badge: 'New' },
    { id: 'appkeys', icon: 'key', label: t('appKey.title') },
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
        <nav className="flex-1 px-3 overflow-y-auto custom-scrollbar">
          <div className="space-y-0.5">
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
          </div>
        </nav>
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
            contentWidth={settings.contentWidth ?? 960}
            onContentWidthChange={(v) => update({ contentWidth: v })}
          />
        )}
        {activeTab === 'personalization' && (
          <PersonalizationSettings
            nickname={settings.nickname}
            onNicknameChange={(v) => update({ nickname: v })}
            userBackground={settings.userBackground}
            onUserBackgroundChange={(v) => update({ userBackground: v })}
            responseStyle={settings.responseStyle}
            onResponseStyleChange={(v) => update({ responseStyle: v })}
            systemPrompt={settings.systemPrompt}
            onSystemPromptChange={(v) => update({ systemPrompt: v })}
          />
        )}
        {activeTab === 'chat' && (
          <ChatSettings
            sendShortcut={settings.sendShortcut}
            onSendShortcutChange={(v) => update({ sendShortcut: v })}
            defaultModel={settings.defaultModel}
            onDefaultModelChange={(v) => update({ defaultModel: v })}
            defaultThinkingMode={settings.defaultThinkingMode}
            onDefaultThinkingModeChange={(v) => update({ defaultThinkingMode: v })}
            contextRounds={settings.contextRounds}
            onContextRoundsChange={(v) => update({ contextRounds: v })}
            models={models}
          />
        )}
        {activeTab === 'mcp' && (
          <McpSettings
            plugins={mcpServers.map((s) => ({
              id: String(s.id),
              name: s.name,
              version: '',
              description: s.endpoint,
              icon: 'extension',
              iconBg: 'bg-indigo-100 dark:bg-indigo-900/50',
              iconColor: 'text-indigo-600 dark:text-indigo-400',
              enabled: s.enable,
            }))}
            onPluginToggle={(id, enabled) => {
              const numId = Number(id)
              toggleMcpServer(numId, enabled).catch((e) => console.error('Failed to toggle MCP server:', e))
              setMcpServers((prev) =>
                prev.map((s) => (s.id === numId ? { ...s, enable: enabled } : s)),
              )
            }}
            mcpEnabled={settings.mcpEnabled}
            onMcpEnabledChange={(v) => update({ mcpEnabled: v })}
            showToolCalls={settings.showToolCalls}
            onShowToolCallsChange={(v) => update({ showToolCalls: v })}
          />
        )}
        {activeTab === 'account' && (
          <div className="mb-10">
            <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
              <span className="bg-blue-100 dark:bg-blue-900/40 text-blue-600 p-1 rounded mr-3">
                <Icon name="account_circle" variant="filled" size="lg" />
              </span>
              {t('settings.account')}
            </h3>
            <div className="space-y-5">
              {/* 用户头像 + 昵称卡片 */}
              <div className="flex items-center gap-4 p-4 rounded-xl bg-gray-50 dark:bg-gray-800/50 border border-gray-100 dark:border-gray-700">
                {userProfile?.avatar && userProfile.avatar.trim() && !avatarImgError ? (
                  <img
                    src={userProfile.avatar}
                    alt={userProfile.nickname || userProfile.account}
                    className="w-16 h-16 rounded-full object-cover flex-shrink-0"
                    onError={() => setAvatarImgError(true)}
                  />
                ) : (
                  <div className="w-16 h-16 rounded-full bg-blue-100 dark:bg-blue-900/40 flex items-center justify-center flex-shrink-0">
                    <Icon name="account_circle" variant="filled" size="xl" className="text-blue-500 dark:text-blue-400" />
                  </div>
                )}
                <div className="flex flex-col gap-1 min-w-0">
                  <span className="text-base font-semibold text-gray-900 dark:text-white truncate">
                    {userProfile?.nickname || userProfile?.account || '—'}
                  </span>
                  {userProfile?.nickname && userProfile.account && (
                    <span className="text-sm text-gray-500 dark:text-gray-400 truncate">
                      @{userProfile.account}
                    </span>
                  )}
                </div>
              </div>

              {/* 详细信息列表 */}
              <div className="rounded-xl border border-gray-100 dark:border-gray-700 divide-y divide-gray-100 dark:divide-gray-700 overflow-hidden">
                {([
                  { icon: 'badge', label: t('account.role'), value: userProfile?.role },
                  { icon: 'corporate_fare', label: t('account.department'), value: userProfile?.department },
                  { icon: 'alternate_email', label: t('account.username'), value: userProfile?.account },
                  { icon: 'mail', label: t('account.email'), value: userProfile?.email },
                  { icon: 'phone', label: t('account.mobile'), value: userProfile?.mobile },
                  { icon: 'sticky_note_2', label: t('account.remark'), value: userProfile?.remark },
                ] as { icon: string; label: string; value?: string }[]).map(({ icon, label, value }) => (
                  <div key={label} className="flex items-start gap-3 px-4 py-3 bg-white dark:bg-gray-800/30">
                    <Icon name={icon} size="base" className="text-gray-400 dark:text-gray-500 mt-0.5 shrink-0" />
                    <span className="text-sm text-gray-500 dark:text-gray-400 w-20 shrink-0">{label}</span>
                    <span className="text-sm text-gray-800 dark:text-gray-200 break-all">
                      {value || <span className="text-gray-300 dark:text-gray-600">—</span>}
                    </span>
                  </div>
                ))}
              </div>

              <div className="border-b border-gray-100 dark:border-gray-800" />

              <div className="border-b border-gray-100 dark:border-gray-800" />

              {/* 服务条款 & 隐私政策 —— 点击弹窗 */}
              <div className="flex flex-col gap-3">
                <button
                  onClick={() => setLegalDialog('terms')}
                  className="text-sm text-primary hover:underline flex items-center gap-2 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 rounded"
                >
                  <Icon name="description" size="base" />
                  {t('about.terms')}
                </button>
                <button
                  onClick={() => setLegalDialog('privacy')}
                  className="text-sm text-primary hover:underline flex items-center gap-2 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 rounded"
                >
                  <Icon name="shield" size="base" />
                  {t('about.privacy')}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* 服务条款 / 隐私政策 弹窗 */}
        {legalDialog && (
          <div
            className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 backdrop-blur-sm"
            onClick={() => setLegalDialog(null)}
          >
            <div
              className="bg-white dark:bg-gray-900 rounded-2xl shadow-2xl w-full max-w-lg mx-4 max-h-[80vh] flex flex-col"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 dark:border-gray-800">
                <h4 className="text-base font-semibold text-gray-900 dark:text-white">
                  {legalDialog === 'terms' ? t('about.terms') : t('about.privacy')}
                </h4>
                <button
                  onClick={() => setLegalDialog(null)}
                  className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors focus-visible:outline-none"
                  aria-label="close"
                >
                  <Icon name="close" size="lg" />
                </button>
              </div>
              <div className="px-6 py-5 overflow-y-auto text-sm text-gray-600 dark:text-gray-300 leading-relaxed space-y-4">
                {legalDialog === 'terms' ? (
                  <>
                    <p>{t('legal.termsIntro')}</p>
                    <p><strong>{t('legal.termsUseTitle')}</strong><br />{t('legal.termsUseBody')}</p>
                    <p><strong>{t('legal.termsDataTitle')}</strong><br />{t('legal.termsDataBody')}</p>
                    <p><strong>{t('legal.termsLimitTitle')}</strong><br />{t('legal.termsLimitBody')}</p>
                    <p className="text-xs text-gray-400">{t('legal.termsUpdate')}</p>
                  </>
                ) : (
                  <>
                    <p>{t('legal.privacyIntro')}</p>
                    <p><strong>{t('legal.privacyCollectTitle')}</strong><br />{t('legal.privacyCollectBody')}</p>
                    <p><strong>{t('legal.privacyUseTitle')}</strong><br />{t('legal.privacyUseBody')}</p>
                    <p><strong>{t('legal.privacySecurityTitle')}</strong><br />{t('legal.privacySecurityBody')}</p>
                    <p className="text-xs text-gray-400">{t('legal.privacyUpdate')}</p>
                  </>
                )}
              </div>
            </div>
          </div>
        )}
        {activeTab === 'data' && (
          <DataSettings
            onDataCleared={onDataCleared}
            allowTraining={settings.allowTraining}
            onAllowTrainingChange={(v) => update({ allowTraining: v })}
          />
        )}
        {activeTab === 'appkeys' && <AppKeySettings />}
      </ScrollArea>
    </Modal>
  )
}
