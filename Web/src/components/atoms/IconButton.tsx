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
  ghost: 'text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-full',
  filled: 'text-white bg-gray-900 dark:bg-white dark:text-gray-900 hover:bg-gray-700 dark:hover:bg-gray-200 rounded-full shadow-sm',
  circle: 'text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 bg-gray-100 dark:bg-gray-700 hover:bg-gray-200 dark:hover:bg-gray-600 rounded-full',
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
        'inline-flex items-center justify-center transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
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
