import { cn } from '@/lib/utils'

export type IconVariant = 'outlined' | 'filled' | 'symbols'

interface IconProps {
  name: string
  variant?: IconVariant
  className?: string
  size?: 'xs' | 'sm' | 'base' | 'lg' | 'xl'
}

const sizeValue: Record<string, number> = {
  xs: 12,
  sm: 14,
  base: 16,
  lg: 18,
  xl: 20,
}

const variantClass: Record<IconVariant, string> = {
  outlined: 'material-icons-outlined',
  filled: 'material-icons',
  symbols: 'material-symbols-outlined',
}

export function Icon({ name, variant = 'outlined', className, size = 'base' }: IconProps) {
  return (
    <span
      className={cn(variantClass[variant], className)}
      style={{ fontSize: sizeValue[size] }}
    >
      {name}
    </span>
  )
}
