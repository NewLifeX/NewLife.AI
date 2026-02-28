import { type ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { IconButton } from '@/components/atoms/IconButton'

interface TerminalBlockProps {
  title?: string
  children: ReactNode
  className?: string
}

export function TerminalBlock({
  title = 'root@server:~',
  children,
  className,
}: TerminalBlockProps) {
  return (
    <div className={cn('rounded-lg overflow-hidden border border-gray-200 dark:border-gray-700', className)}>
      <div className="bg-gray-100 dark:bg-gray-900 px-4 py-2 flex items-center justify-between border-b border-gray-200 dark:border-gray-700">
        <div className="flex items-center space-x-1.5">
          <div className="w-3 h-3 rounded-full bg-red-400" />
          <div className="w-3 h-3 rounded-full bg-yellow-400" />
          <div className="w-3 h-3 rounded-full bg-green-400" />
        </div>
        <div className="flex items-center space-x-2">
          <span className="text-xs text-gray-500 font-mono">{title}</span>
          <IconButton icon="content_copy" size="sm" label="复制" />
        </div>
      </div>
      <div className="bg-[#1e1e1e] p-4 text-xs font-mono text-gray-300 terminal-window overflow-x-auto">
        {children}
      </div>
    </div>
  )
}
