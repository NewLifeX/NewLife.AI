import { type SelectHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

interface SelectOption {
  value: string
  label: string
}

interface SelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'onChange'> {
  options: SelectOption[]
  value: string
  onChange: (value: string) => void
  placeholder?: string
  className?: string
}

export function Select({
  options,
  value,
  onChange,
  placeholder,
  className,
  ...props
}: SelectProps) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className={cn(
        'block rounded-lg border border-gray-200 dark:border-gray-700',
        'bg-gray-50 dark:bg-gray-800',
        'text-sm text-gray-900 dark:text-gray-100',
        'focus:border-primary focus:ring-primary focus:ring-1 focus:outline-none',
        'shadow-sm transition-colors cursor-pointer',
        'px-3 py-2 pr-8',
        'appearance-none',
        'bg-[url("data:image/svg+xml,%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20width%3D%2220%22%20height%3D%2220%22%20viewBox%3D%220%200%2020%2020%22%3E%3Cpath%20fill%3D%22%236b7280%22%20d%3D%22M7%207l3%203%203-3%22%2F%3E%3C%2Fsvg%3E")]',
        'bg-no-repeat bg-[right_0.5rem_center] bg-[length:1.25rem]',
        className,
      )}
      {...props}
    >
      {placeholder && (
        <option value="" disabled>
          {placeholder}
        </option>
      )}
      {options.map((opt) => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  )
}
