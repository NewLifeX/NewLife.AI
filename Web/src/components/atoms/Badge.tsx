import { type ReactNode } from 'react'
import { cn } from '@/lib/utils'

export type BadgeVariant = 'default' | 'primary' | 'success' | 'warning' | 'info'

interface BadgeProps {
  children: ReactNode
  variant?: BadgeVariant
  className?: string
}

const variantStyles: Record<BadgeVariant, string> = {
  default: 'bg-[var(--color-surface-2)] text-[var(--color-text-secondary)] border border-[var(--color-border-subtle)]',
  primary: 'bg-[color:var(--color-brand-50)] dark:bg-[color:var(--color-brand-900)]/40 text-[color:var(--color-brand-700)] dark:text-[color:var(--color-brand-200)] border border-[color:var(--color-brand-100)]/60 dark:border-[color:var(--color-brand-700)]/40',
  success: 'bg-emerald-50 dark:bg-emerald-900/25 text-emerald-700 dark:text-emerald-300 border border-emerald-100/70 dark:border-emerald-800/50',
  warning: 'bg-amber-50 dark:bg-amber-900/25 text-amber-700 dark:text-amber-300 border border-amber-100/70 dark:border-amber-800/50',
  info: 'bg-cyan-50 dark:bg-cyan-900/25 text-cyan-700 dark:text-cyan-300 border border-cyan-100/70 dark:border-cyan-800/50',
}

export function Badge({ children, variant = 'default', className }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center text-[10px] font-medium px-1.5 py-0.5 rounded',
        variantStyles[variant],
        className,
      )}
    >
      {children}
    </span>
  )
}
