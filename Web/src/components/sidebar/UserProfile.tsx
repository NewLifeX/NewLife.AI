import { useRef, useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Avatar } from '@/components/common/Avatar'
import { Icon } from '@/components/common/Icon'

interface UserProfileProps {
  name: string
  avatarUrl?: string
  isSystem?: boolean
  onSettingsClick?: () => void
  onAdminClick?: () => void
  onLogoutClick?: () => void
  className?: string
}

export function UserProfile({ name, avatarUrl, isSystem, onSettingsClick, onAdminClick, onLogoutClick, className }: UserProfileProps) {
  const { t } = useTranslation()
  const letter = name ? name.charAt(0).toUpperCase() : 'U'
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const handleClick = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClick)
    return () => document.removeEventListener('mousedown', handleClick)
  }, [open])

  const handleMenuItem = (action?: () => void) => {
    setOpen(false)
    action?.()
  }

  return (
    <div ref={containerRef} className={cn('p-3 border-t border-gray-100 dark:border-gray-800 relative', className)}>
      {/* 弹出菜单 */}
      {open && (
        <div className="absolute bottom-full left-3 right-3 mb-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded-xl shadow-lg py-1 z-50">
          <button
            onClick={() => handleMenuItem(onSettingsClick)}
            className="w-full flex items-center gap-2.5 px-3 py-2 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors rounded-lg mx-0"
          >
            <Icon name="settings" size="base" className="text-gray-400 dark:text-gray-500" />
            {t('menu.settings')}
          </button>
          {isSystem && (
            <button
              onClick={() => handleMenuItem(onAdminClick)}
              className="w-full flex items-center gap-2.5 px-3 py-2 text-sm text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors rounded-lg"
            >
              <Icon name="admin_panel_settings" size="base" className="text-gray-400 dark:text-gray-500" />
              {t('menu.admin')}
            </button>
          )}
          <div className="my-1 mx-2 border-t border-gray-100 dark:border-gray-700" />
          <button
            onClick={() => handleMenuItem(onLogoutClick)}
            className="w-full flex items-center gap-2.5 px-3 py-2 text-sm text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors rounded-lg"
          >
            <Icon name="logout" size="base" className="text-red-400" />
            {t('menu.logout')}
          </button>
        </div>
      )}

      <button
        onClick={() => setOpen((v) => !v)}
        className="w-full flex items-center space-x-3 px-2 py-2 rounded-lg hover:bg-gray-200/50 dark:hover:bg-gray-700/50 text-sm text-gray-700 dark:text-gray-300 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
      >
        <Avatar type="user" src={avatarUrl} letter={letter} size="sm" />
        <span className="font-medium truncate flex-1 text-left">{name}</span>
        <Icon name="more_horiz" size="base" className="text-gray-400 flex-shrink-0" />
      </button>
    </div>
  )
}
