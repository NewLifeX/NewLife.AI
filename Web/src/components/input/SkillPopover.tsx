import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

interface SkillOption {
  id: string
  icon: string
  iconBg: string
  iconColor: string
  label: string
  description: string
}

interface SkillPopoverProps {
  open: boolean
  onSelect: (id: string) => void
  onClose: () => void
  options?: SkillOption[]
  className?: string
}

function useDefaultOptions(): SkillOption[] {
  const { t } = useTranslation()
  return [
    { id: 'mcp', icon: 'hub', iconBg: 'bg-blue-100 dark:bg-blue-900/40', iconColor: 'text-primary dark:text-blue-400', label: t('skills.mcp'), description: t('skills.mcpDesc') },
    { id: 'search', icon: 'travel_explore', iconBg: 'bg-purple-100 dark:bg-purple-900/40', iconColor: 'text-purple-600 dark:text-purple-400', label: t('skills.search'), description: t('skills.searchDesc') },
    { id: 'image', icon: 'palette', iconBg: 'bg-pink-100 dark:bg-pink-900/40', iconColor: 'text-pink-600 dark:text-pink-400', label: t('skills.imageGen'), description: t('skills.imageGenDesc') },
    { id: 'data', icon: 'analytics', iconBg: 'bg-green-100 dark:bg-green-900/40', iconColor: 'text-green-600 dark:text-green-400', label: t('skills.dataAnalysis'), description: t('skills.dataAnalysisDesc') },
  ]
}

export function SkillPopover({
  open,
  onSelect,
  onClose,
  options,
  className,
}: SkillPopoverProps) {
  const { t } = useTranslation()
  const defaultOptions = useDefaultOptions()
  const resolved = options ?? defaultOptions

  if (!open) return null

  return (
    <>
      <div className="fixed inset-0 z-40" onClick={onClose} />
      <div
        className={cn(
          'absolute bottom-full mb-3 left-0 w-72',
          'bg-white dark:bg-gray-800 rounded-2xl shadow-menu dark:shadow-black/40',
          'border border-gray-100 dark:border-gray-700 overflow-hidden z-50',
          'animate-slide-up',
          className,
        )}
      >
        <div className="px-3 py-2 bg-gray-50 dark:bg-gray-800/50 border-b border-gray-100 dark:border-gray-700/50 flex items-center justify-between">
          <span className="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">
            {t('skills.title')}
          </span>
          <span className="text-[10px] bg-gray-200 dark:bg-gray-700 text-gray-500 dark:text-gray-300 px-1.5 py-0.5 rounded">
            {t('skills.escClose')}
          </span>
        </div>
        <div className="p-1.5">
          {resolved.map((opt) => (
            <button
              key={opt.id}
              onClick={() => onSelect(opt.id)}
              className="w-full flex items-center p-2 rounded-xl hover:bg-blue-50 dark:hover:bg-blue-900/20 group transition-colors text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
            >
              <div className={cn('w-9 h-9 rounded-lg flex items-center justify-center mr-3 flex-shrink-0', opt.iconBg)}>
                <Icon name={opt.icon} className={opt.iconColor} />
              </div>
              <div>
                <div className="text-sm font-medium text-gray-800 dark:text-gray-200 group-hover:text-primary transition-colors">
                  {opt.label}
                </div>
                <div className="text-xs text-gray-500 dark:text-gray-400">
                  {opt.description}
                </div>
              </div>
            </button>
          ))}
        </div>
      </div>
    </>
  )
}
