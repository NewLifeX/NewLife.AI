import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Badge } from '@/components/atoms/Badge'

export interface NavItem {
  id: string
  icon: string
  label: string
  badge?: string
  hasArrow?: boolean
  href?: string
}

interface NavLinksProps {
  items?: NavItem[]
  onItemClick?: (id: string) => void
  className?: string
}

function useDefaultItems(): NavItem[] {
  const { t } = useTranslation()
  return [
    { id: 'creation', icon: 'auto_awesome', label: t('sidebar.aiCreation'), badge: 'Seedance 2.0' },
    { id: 'drive', icon: 'folder_open', label: t('sidebar.cloudDrive') },
    { id: 'more', icon: 'grid_view', label: t('sidebar.more'), hasArrow: true },
  ]
}

export function NavLinks({
  items,
  onItemClick,
  className,
}: NavLinksProps) {
  const defaultItems = useDefaultItems()
  const resolved = items ?? defaultItems

  return (
    <nav className={cn('px-3 space-y-0.5 mb-4', className)}>
      {resolved.map((item) => (
        <button
          key={item.id}
          onClick={() => onItemClick?.(item.id)}
          className="flex items-center space-x-3 px-3 py-2 text-sm text-gray-700 dark:text-gray-300 rounded-lg hover:bg-gray-200/50 dark:hover:bg-gray-700/50 transition-colors w-full text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
        >
          <Icon name={item.icon} className="text-gray-500 dark:text-gray-400" size="lg" />
          <span>{item.label}</span>
          {item.badge && (
            <Badge variant="primary" className="ml-auto">{item.badge}</Badge>
          )}
          {item.hasArrow && (
            <Icon name="chevron_right" className="text-gray-400 text-sm ml-auto" />
          )}
        </button>
      ))}
    </nav>
  )
}
