import { useTranslation } from 'react-i18next'
import { Toggle } from '@/components/atoms/Toggle'
import { Select } from '@/components/atoms/Select'
import { Slider } from '@/components/atoms/Slider'
import { Icon } from '@/components/common/Icon'
import type { ModelInfo } from '@/types'

interface ChatSettingsProps {
  sendShortcut: 'Enter' | 'Ctrl+Enter'
  onSendShortcutChange: (v: 'Enter' | 'Ctrl+Enter') => void
  defaultModel: string
  onDefaultModelChange: (v: string) => void
  defaultThinkingMode: number
  onDefaultThinkingModeChange: (v: number) => void
  contextRounds: number
  onContextRoundsChange: (v: number) => void
  systemPrompt: string
  onSystemPromptChange: (v: string) => void
  mcpEnabled: boolean
  onMcpEnabledChange: (enabled: boolean) => void
  defaultSkill: string
  onDefaultSkillChange: (skill: string) => void
  streamingSpeed: number
  onStreamingSpeedChange: (speed: number) => void
  models: ModelInfo[]
}

const skillOptions = [
  { value: 'general', label: 'General (NewLife 2.0)' },
  { value: 'code', label: 'Code (CodeMaster)' },
  { value: 'creative', label: 'Creative (Muse)' },
]

const shortcutOptions = [
  { value: 'Enter', label: 'Enter' },
  { value: 'Ctrl+Enter', label: 'Ctrl+Enter' },
]

const thinkingModeOptions = [
  { value: '0', label: '' },
  { value: '1', label: '' },
  { value: '2', label: '' },
]

export function ChatSettings({
  sendShortcut,
  onSendShortcutChange,
  defaultModel,
  onDefaultModelChange,
  defaultThinkingMode,
  onDefaultThinkingModeChange,
  contextRounds,
  onContextRoundsChange,
  systemPrompt,
  onSystemPromptChange,
  mcpEnabled,
  onMcpEnabledChange,
  defaultSkill,
  onDefaultSkillChange,
  streamingSpeed,
  onStreamingSpeedChange,
  models,
}: ChatSettingsProps) {
  const { t } = useTranslation()

  const speedLabels: Record<number, string> = {
    1: t('settings.speedSlow'),
    2: t('settings.speedStandard'),
    3: t('settings.speedBalanced'),
    4: t('settings.speedFast'),
    5: t('settings.speedMax'),
  }

  const modelOptions = models.map((m) => ({ value: m.id, label: m.name }))

  const localThinkingOptions = thinkingModeOptions.map((o) => ({
    ...o,
    label: o.value === '0' ? t('thinking.auto') : o.value === '1' ? t('thinking.think') : t('thinking.fast'),
  }))

  return (
    <div className="mb-10">
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-purple-100 dark:bg-purple-900/40 text-purple-600 p-1 rounded mr-3">
          <Icon name="chat" variant="filled" size="lg" />
        </span>
        {t('settings.chatPrefs')}
      </h3>
      <div className="space-y-6">
        {/* 4.2.1 发送快捷键 */}
        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.sendShortcut')}</div>
          </div>
          <Select
            options={shortcutOptions}
            value={sendShortcut}
            onChange={(v) => onSendShortcutChange(v as 'Enter' | 'Ctrl+Enter')}
            className="w-40"
          />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        {/* 4.2.2 默认模型 */}
        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.defaultModel')}</div>
            <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{t('settings.defaultModelDesc')}</div>
          </div>
          <Select options={modelOptions} value={defaultModel} onChange={onDefaultModelChange} className="w-48" />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        {/* 4.2.2 默认思考模式 */}
        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.defaultThinkingMode')}</div>
          </div>
          <Select
            options={localThinkingOptions}
            value={String(defaultThinkingMode)}
            onChange={(v) => onDefaultThinkingModeChange(Number(v))}
            className="w-40"
          />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        {/* 4.2.2 上下文轮数 */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.contextRounds')}</div>
            <span className="text-xs text-blue-600 dark:text-blue-400 font-medium">{contextRounds}</span>
          </div>
          <Slider value={contextRounds} onChange={onContextRoundsChange} min={1} max={30} labelLeft="1" labelRight="30" />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        {/* 4.2.2 系统提示词 */}
        <div className="space-y-2">
          <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.systemPrompt')}</div>
          <div className="text-xs text-gray-500 dark:text-gray-400">{t('settings.systemPromptDesc')}</div>
          <textarea
            value={systemPrompt}
            onChange={(e) => onSystemPromptChange(e.target.value)}
            rows={3}
            placeholder={t('settings.systemPromptPlaceholder')}
            className="w-full rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-700 dark:text-gray-200 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 resize-none"
          />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        {/* MCP 开关 */}
        <div className="flex items-center justify-between">
          <div className="flex-1 mr-4">
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.enableMcp')}</div>
            <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
              {t('settings.enableMcpDesc')}
            </div>
          </div>
          <Toggle checked={mcpEnabled} onChange={onMcpEnabledChange} />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.defaultSkill')}</div>
            <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{t('settings.defaultSkillDesc')}</div>
          </div>
          <Select options={skillOptions} value={defaultSkill} onChange={onDefaultSkillChange} className="w-48" />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.streamingSpeed')}</div>
            <span className="text-xs text-blue-600 dark:text-blue-400 font-medium">
              {speedLabels[streamingSpeed] ?? t('settings.speedBalanced')}
            </span>
          </div>
          <Slider
            value={streamingSpeed}
            onChange={onStreamingSpeedChange}
            min={1}
            max={5}
            labelLeft={t('settings.speedSlow')}
            labelRight={t('settings.speedMax')}
          />
        </div>
      </div>
    </div>
  )
}
