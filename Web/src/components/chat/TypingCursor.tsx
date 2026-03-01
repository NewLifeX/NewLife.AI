import { cn } from '@/lib/utils'

interface TypingCursorProps {
  className?: string
}

export function TypingCursor({ className }: TypingCursorProps) {
  return <span className={cn('typing-cursor', className)} />
}
