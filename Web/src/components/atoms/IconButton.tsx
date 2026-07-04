import { type ButtonHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'
import { Icon, type IconVariant } from '@/components/common/Icon'

interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  icon: string
  iconVariant?: IconVariant
  size?: 'xs' | 'sm' | 'md' | 'lg'
  variant?: 'ghost' | 'filled' | 'circle'
  label?: string
}

const sizeStyles = {
  xs: 'p-0.5',
  sm: 'p-1',
  md: 'p-1.5',
  lg: 'p-2',
}

const variantStyles = {
  ghost: 'text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] hover:bg-[var(--color-surface-2)] rounded-full',
  filled: 'text-white bg-[image:var(--gradient-brand)] hover:brightness-110 rounded-full shadow-[0_4px_12px_-2px_rgba(91,91,255,0.5)] hover:-translate-y-px active:translate-y-0',
  circle: 'text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)] bg-[var(--color-surface-2)] hover:bg-[var(--color-surface-3)] rounded-full',
}

export function IconButton({
  icon,
  iconVariant = 'outlined',
  size = 'md',
  variant = 'ghost',
  label,
  className,
  ...props
}: IconButtonProps) {
  return (
    <button
      className={cn(
        'inline-flex items-center justify-center transition-[transform,box-shadow,background-color,color] duration-200 ease-out',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/55',
        'disabled:opacity-50 disabled:cursor-not-allowed',
        sizeStyles[size],
        variantStyles[variant],
        className,
      )}
      title={label}
      aria-label={label}
      {...props}
    >
      <Icon name={icon} variant={iconVariant} size={size === 'lg' ? 'lg' : size === 'md' ? 'base' : size === 'sm' ? 'sm' : 'xs'} />
    </button>
  )
}
