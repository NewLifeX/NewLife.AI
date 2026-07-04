import { type ButtonHTMLAttributes, type ReactNode } from 'react'
import { cn } from '@/lib/utils'

export type ButtonVariant = 'primary' | 'secondary' | 'soft' | 'ghost' | 'danger'
export type ButtonSize = 'sm' | 'md' | 'lg'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
  icon?: ReactNode
  children?: ReactNode
}

const variantStyles: Record<ButtonVariant, string> = {
  primary:
    'text-white shadow-[0_8px_24px_-8px_rgba(91,91,255,0.45),0_2px_6px_rgba(91,91,255,0.18)] ' +
    'hover:shadow-[0_12px_28px_-8px_rgba(91,91,255,0.55),0_2px_8px_rgba(91,91,255,0.22)] ' +
    'hover:-translate-y-px active:translate-y-0 ' +
    'bg-[image:var(--gradient-brand)] bg-[length:200%_100%] hover:bg-[length:160%_100%]',
  secondary:
    'bg-[var(--color-surface-2)] hover:bg-[var(--color-surface-3)] text-[var(--color-text-primary)] ' +
    'border border-[var(--color-border-subtle)]',
  soft:
    'bg-[image:var(--gradient-brand-soft)] text-[color:var(--color-brand-700)] dark:text-[color:var(--color-brand-300)] ' +
    'hover:brightness-105 border border-[var(--color-border-subtle)]',
  ghost:
    'hover:bg-[var(--color-surface-2)] text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
  danger:
    'bg-red-500 hover:bg-red-600 text-white shadow-sm hover:shadow-md hover:-translate-y-px active:translate-y-0',
}

const sizeStyles: Record<ButtonSize, string> = {
  sm: 'px-2.5 py-1.5 text-xs rounded-lg',
  md: 'px-4 py-2 text-sm rounded-lg',
  lg: 'px-5 py-2.5 text-base rounded-xl',
}

export function Button({
  variant = 'primary',
  size = 'md',
  icon,
  children,
  className,
  disabled,
  ...props
}: ButtonProps) {
  return (
    <button
      className={cn(
        'inline-flex items-center justify-center font-medium select-none',
        'transition-[transform,box-shadow,background-position,background-color,color] duration-200 ease-out',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/55 focus-visible:ring-offset-1 focus-visible:ring-offset-[var(--color-surface-0)]',
        'disabled:opacity-50 disabled:cursor-not-allowed disabled:shadow-none disabled:translate-y-0 disabled:hover:translate-y-0',
        variantStyles[variant],
        sizeStyles[size],
        icon && children && 'space-x-1.5',
        className,
      )}
      disabled={disabled}
      {...props}
    >
      {icon}
      {children && <span>{children}</span>}
    </button>
  )
}
