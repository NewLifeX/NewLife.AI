import { useTranslation } from 'react-i18next'
import { Toggle } from '@/components/atoms/Toggle'
import { Select } from '@/components/atoms/Select'
import { Slider } from '@/components/atoms/Slider'
import { Icon } from '@/components/common/Icon'

interface ChatSettingsProps {
  mcpEnabled: boolean
  onMcpEnabledChange: (enabled: boolean) => void
  defaultSkill: string
  onDefaultSkillChange: (skill: string) => void
  streamingSpeed: number
  onStreamingSpeedChange: (speed: number) => void
}

const skillOptions = [
  { value: 'general', label: 'General (NewLife 2.0)' },
  { value: 'code', label: 'Code (CodeMaster)' },
  { value: 'creative', label: 'Creative (Muse)' },
]

export function ChatSettings({
  mcpEnabled,
  onMcpEnabledChange,
  defaultSkill,
  onDefaultSkillChange,
  streamingSpeed,
  onStreamingSpeedChange,
}: ChatSettingsProps) {
  const { t } = useTranslation()

  const speedLabels: Record<number, string> = {
    1: t('settings.speedSlow'),
    2: t('settings.speedStandard'),
    3: t('settings.speedBalanced'),
    4: t('settings.speedFast'),
    5: t('settings.speedMax'),
  }

  return (
    <div className="mb-10">
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-purple-100 dark:bg-purple-900/40 text-purple-600 p-1 rounded mr-3">
          <Icon name="chat" variant="filled" size="lg" />
        </span>
        {t('settings.chatPrefs')}
      </h3>
      <div className="space-y-6">
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
