import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import { ChatInput } from '@/components/input/ChatInput'
import { useSettingsStore } from '@/stores'

interface WelcomePageProps {
  onSend: (message: string) => void
}

export function WelcomePage({ onSend }: WelcomePageProps) {
  const { t } = useTranslation()
  const sendShortcut = useSettingsStore((s) => s.sendShortcut)

  const suggestions = [
    { icon: 'bolt', label: t('welcome.quick'), color: 'text-yellow-500' },
    { icon: 'image', label: t('welcome.imageGen'), color: 'text-pink-500' },
    { icon: 'code', label: t('welcome.coding'), color: 'text-blue-500' },
    { icon: 'edit_note', label: t('welcome.writing'), color: 'text-green-500' },
    { icon: 'travel_explore', label: t('welcome.research'), color: 'text-purple-500' },
    { icon: 'smart_display', label: t('welcome.videoGen'), color: 'text-red-500' },
  ]
  return (
    <>
      <div className="flex-1 overflow-y-auto custom-scrollbar px-4 md:px-0">
        <div className="max-w-3xl mx-auto w-full flex flex-col items-center justify-center h-full">
          <div className="text-center mb-12">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gradient-to-br from-blue-500 to-purple-600 mb-6">
              <span className="text-white text-2xl font-bold">N</span>
            </div>
            <h1 className="text-3xl font-bold text-gray-900 dark:text-white mb-2 tracking-tight">
              {t('welcome.greeting')}
            </h1>
            <p className="text-gray-500 dark:text-gray-400 text-sm">
              {t('welcome.subtitle')}
            </p>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-3 gap-3 mb-8 w-full max-w-lg">
            {suggestions.map((s) => (
              <button
                key={s.label}
                onClick={() => onSend(t('welcome.useFeature', { feature: s.label }))}
                className="flex items-center space-x-2 px-4 py-3 bg-gray-50 dark:bg-gray-800/50 border border-gray-100 dark:border-gray-700 rounded-xl hover:bg-gray-100 dark:hover:bg-gray-700/50 hover:border-gray-200 dark:hover:border-gray-600 hover:shadow-sm transition-all duration-200 text-sm text-gray-700 dark:text-gray-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
              >
                <Icon name={s.icon} className={s.color} />
                <span>{s.label}</span>
              </button>
            ))}
          </div>
        </div>
      </div>

      <div className="pb-6 pt-2 px-4 bg-gradient-to-t from-white via-white to-transparent dark:from-background-dark dark:via-background-dark">
        <ChatInput onSend={onSend} sendShortcut={sendShortcut} />
      </div>
    </>
  )
}
