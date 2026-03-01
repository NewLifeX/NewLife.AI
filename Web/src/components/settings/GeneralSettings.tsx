import { useTranslation } from 'react-i18next'
import { Select } from '@/components/atoms/Select'
import { Slider } from '@/components/atoms/Slider'
import { Icon } from '@/components/common/Icon'
import { cn } from '@/lib/utils'

type Theme = 'light' | 'dark' | 'system'

interface GeneralSettingsProps {
  language: string
  onLanguageChange: (lang: string) => void
  theme: Theme
  onThemeChange: (theme: Theme) => void
  fontSize: number
  onFontSizeChange: (size: number) => void
}

const languageOptions = [
  { value: 'zh', label: '简体中文' },
  { value: 'en', label: 'English' },
]

function ThemeCard({ active, onClick, children, label }: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
  label: string
}) {
  return (
    <button onClick={onClick} className="cursor-pointer group relative text-left">
      <div
        className={cn(
          'h-24 rounded-xl border-2 overflow-hidden relative hover:shadow-md transition-all',
          active ? 'border-primary' : 'border-transparent',
          'bg-gray-100 dark:bg-gray-800',
        )}
      >
        {children}
        {active && (
          <div className="absolute right-2 bottom-2 text-primary">
            <Icon name="check_circle" variant="filled" size="xl" />
          </div>
        )}
      </div>
      <span className={cn(
        'block text-center text-xs mt-2 font-medium',
        active ? 'text-primary' : 'text-gray-500',
      )}>
        {label}
      </span>
    </button>
  )
}

export function GeneralSettings({
  language,
  onLanguageChange,
  theme,
  onThemeChange,
  fontSize,
  onFontSizeChange,
}: GeneralSettingsProps) {
  const { t } = useTranslation()
  return (
    <div className="mb-10">
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-blue-100 dark:bg-blue-900/40 text-primary p-1 rounded mr-3">
          <Icon name="tune" variant="filled" size="lg" />
        </span>
        {t('settings.general')}
      </h3>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.language')}</div>
            <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{t('settings.languageDesc')}</div>
          </div>
          <Select options={languageOptions} value={language} onChange={onLanguageChange} className="w-40" />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        <div>
          <div className="text-sm font-medium text-gray-700 dark:text-gray-200 mb-3">{t('settings.theme')}</div>
          <div className="grid grid-cols-3 gap-4">
            <ThemeCard active={theme === 'light'} onClick={() => onThemeChange('light')} label={t('settings.themeLight')}>
              <div className="absolute inset-x-2 top-2 bottom-0 bg-white rounded-t-lg shadow-sm">
                <div className="p-2 space-y-1">
                  <div className="w-8 h-2 bg-gray-100 rounded" />
                  <div className="w-12 h-2 bg-gray-100 rounded" />
                </div>
              </div>
            </ThemeCard>
            <ThemeCard active={theme === 'dark'} onClick={() => onThemeChange('dark')} label={t('settings.themeDark')}>
              <div className="absolute inset-x-2 top-2 bottom-0 bg-[#2b2b2e] rounded-t-lg shadow-sm">
                <div className="p-2 space-y-1">
                  <div className="w-8 h-2 bg-gray-600 rounded" />
                  <div className="w-12 h-2 bg-gray-600 rounded" />
                </div>
              </div>
            </ThemeCard>
            <ThemeCard active={theme === 'system'} onClick={() => onThemeChange('system')} label={t('settings.themeSystem')}>
              <div className="absolute inset-0 bg-gradient-to-br from-white via-gray-100 to-gray-800 opacity-50" />
              <div className="absolute inset-0 flex items-center justify-center">
                <Icon name="brightness_auto" variant="filled" className="text-gray-400 text-3xl" />
              </div>
            </ThemeCard>
          </div>
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">{t('settings.fontSize')}</div>
            <span className="text-xs text-gray-500 bg-gray-100 dark:bg-gray-700 px-2 py-0.5 rounded">
              {t('settings.fontSizeStandard')} ({fontSize}px)
            </span>
          </div>
          <div className="flex items-center space-x-4">
            <span className="text-xs text-gray-400">A</span>
            <Slider value={fontSize} onChange={onFontSizeChange} min={12} max={24} />
            <span className="text-lg text-gray-400">A</span>
          </div>
        </div>
      </div>
    </div>
  )
}
