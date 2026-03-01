import { type ReactNode, type HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

interface ScrollAreaProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode
  className?: string
}

export function ScrollArea({ children, className, ...props }: ScrollAreaProps) {
  return (
    <div
      className={cn('overflow-y-auto custom-scrollbar', className)}
      {...props}
    >
      {children}
    </div>
  )
}
