import { cn } from '@/lib/utils'

export type IconVariant = 'outlined' | 'filled' | 'symbols'

interface IconProps {
  name: string
  variant?: IconVariant
  className?: string
  size?: 'xs' | 'sm' | 'base' | 'lg' | 'xl'
}

const sizeMap: Record<string, string> = {
  xs: 'text-[11px]',
  sm: 'text-xs',
  base: 'text-sm',
  lg: 'text-base',
  xl: 'text-lg',
}

const variantClass: Record<IconVariant, string> = {
  outlined: 'material-icons-outlined',
  filled: 'material-icons',
  symbols: 'material-symbols-outlined',
}

export function Icon({ name, variant = 'outlined', className, size = 'base' }: IconProps) {
  return (
    <span className={cn(variantClass[variant], sizeMap[size], className)}>
      {name}
    </span>
  )
}
