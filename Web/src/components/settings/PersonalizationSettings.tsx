import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'

interface PersonalizationSettingsProps {
  nickname: string
  onNicknameChange: (v: string) => void
  userBackground: string
  onUserBackgroundChange: (v: string) => void
  responseStyle: number
  onResponseStyleChange: (v: number) => void
  systemPrompt: string
  onSystemPromptChange: (v: string) => void
}

const STYLE_ICONS = ['balance', 'target', 'palette', 'auto_awesome'] as const

export function PersonalizationSettings({
  nickname,
  onNicknameChange,
  userBackground,
  onUserBackgroundChange,
  responseStyle,
  onResponseStyleChange,
  systemPrompt,
  onSystemPromptChange,
}: PersonalizationSettingsProps) {
  const { t } = useTranslation()

  const styles = [
    { value: 0, label: t('personalization.styleBalanced'), desc: t('personalization.styleBalancedDesc') },
    { value: 1, label: t('personalization.stylePrecise'), desc: t('personalization.stylePreciseDesc') },
    { value: 2, label: t('personalization.styleVivid'), desc: t('personalization.styleVividDesc') },
    { value: 3, label: t('personalization.styleCreative'), desc: t('personalization.styleCreativeDesc') },
  ]

  return (
    <div className="mb-10">
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-pink-100 dark:bg-pink-900/40 text-pink-600 p-1 rounded mr-3">
          <Icon name="auto_awesome" variant="filled" size="lg" />
        </span>
        {t('personalization.title')}
      </h3>
      <div className="space-y-6">
        {/* AI 称呼 */}
        <div className="space-y-2">
          <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('personalization.nickname')}</div>
          <div className="text-xs text-gray-500 dark:text-gray-400">{t('personalization.nicknameDesc')}</div>
          <input
            type="text"
            value={nickname}
            onChange={(e) => onNicknameChange(e.target.value)}
            maxLength={50}
            placeholder={t('personalization.nicknamePlaceholder')}
            className="w-full rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-700 dark:text-gray-200 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50"
          />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        {/* 用户背景信息 */}
        <div className="space-y-2">
          <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('personalization.userBackground')}</div>
          <div className="text-xs text-gray-500 dark:text-gray-400">{t('personalization.userBackgroundDesc')}</div>
          <div className="relative">
            <textarea
              value={userBackground}
              onChange={(e) => onUserBackgroundChange(e.target.value)}
              rows={4}
              maxLength={500}
              placeholder={t('personalization.userBackgroundPlaceholder')}
              className="w-full rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-700 dark:text-gray-200 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 resize-none"
            />
            <span className="absolute bottom-2 right-3 text-xs text-gray-400">{userBackground.length}/500</span>
          </div>
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        {/* 回复风格 */}
        <div className="space-y-3">
          <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('personalization.responseStyle')}</div>
          <div className="text-xs text-gray-500 dark:text-gray-400">{t('personalization.responseStyleDesc')}</div>
          <div className="grid grid-cols-2 gap-3">
            {styles.map((s) => (
              <button
                key={s.value}
                onClick={() => onResponseStyleChange(s.value)}
                className={`flex items-start gap-3 p-3 rounded-xl border-2 text-left transition-all ${
                  responseStyle === s.value
                    ? 'border-primary bg-blue-50 dark:bg-blue-900/20 ring-1 ring-primary/30'
                    : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600'
                }`}
              >
                <span className={`mt-0.5 shrink-0 ${responseStyle === s.value ? 'text-primary' : 'text-gray-400'}`}>
                  <Icon name={STYLE_ICONS[s.value]} size="lg" />
                </span>
                <div className="min-w-0">
                  <div className={`text-sm font-medium ${responseStyle === s.value ? 'text-primary dark:text-blue-400' : 'text-gray-700 dark:text-gray-200'}`}>
                    {s.label}
                  </div>
                  <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5 line-clamp-2">{s.desc}</div>
                </div>
              </button>
            ))}
          </div>
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        {/* 自定义指令 */}
        <div className="space-y-2">
          <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('personalization.customInstructions')}</div>
          <div className="text-xs text-gray-500 dark:text-gray-400">{t('personalization.customInstructionsDesc')}</div>
          <div className="relative">
            <textarea
              value={systemPrompt}
              onChange={(e) => onSystemPromptChange(e.target.value)}
              rows={3}
              maxLength={2000}
              placeholder={t('personalization.customInstructionsPlaceholder')}
              className="w-full rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-700 dark:text-gray-200 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/50 resize-none"
            />
            <span className="absolute bottom-2 right-3 text-xs text-gray-400">{systemPrompt.length}/2000</span>
          </div>
        </div>
      </div>
    </div>
  )
}
