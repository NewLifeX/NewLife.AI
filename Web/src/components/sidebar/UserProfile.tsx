import { cn } from '@/lib/utils'
import { Avatar } from '@/components/common/Avatar'

interface UserProfileProps {
  name: string
  avatarUrl?: string
  onClick?: () => void
  className?: string
}

export function UserProfile({ name, avatarUrl, onClick, className }: UserProfileProps) {
  return (
    <div className={cn('p-3 border-t border-gray-100 dark:border-gray-800', className)}>
      <button
        onClick={onClick}
        className="w-full flex items-center space-x-3 px-2 py-2 rounded-lg hover:bg-gray-200/50 dark:hover:bg-gray-700/50 text-sm text-gray-700 dark:text-gray-300 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
      >
        <Avatar type="user" src={avatarUrl} size="sm" />
        <span className="font-medium truncate">{name}</span>
      </button>
    </div>
  )
}
