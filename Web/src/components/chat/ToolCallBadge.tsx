import { cn } from '@/lib/utils'

interface ToolCallBadgeProps {
  name: string
  status: 'calling' | 'done' | 'error'
  className?: string
}

export function ToolCallBadge({ name, status, className }: ToolCallBadgeProps) {
  return (
    <div
      className={cn(
        'flex items-center space-x-2 px-3 py-1.5 rounded-full text-xs font-medium border',
        status === 'calling' && 'bg-green-50 dark:bg-green-900/20 text-green-700 dark:text-green-300 border-green-100 dark:border-green-800/50',
        status === 'done' && 'bg-gray-50 dark:bg-gray-800 text-gray-600 dark:text-gray-300 border-gray-200 dark:border-gray-700',
        status === 'error' && 'bg-red-50 dark:bg-red-900/20 text-red-700 dark:text-red-300 border-red-100 dark:border-red-800/50',
        className,
      )}
    >
      {status === 'calling' && (
        <span className="relative flex h-2 w-2">
          <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-green-400 opacity-75" />
          <span className="relative inline-flex rounded-full h-2 w-2 bg-green-500" />
        </span>
      )}
      {status === 'done' && (
        <span className="material-icons text-green-500 text-sm">check_circle</span>
      )}
      {status === 'error' && (
        <span className="material-icons text-red-500 text-sm">error</span>
      )}
      <span>{name}</span>
    </div>
  )
}
