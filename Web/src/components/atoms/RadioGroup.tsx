import { type ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface RadioOption<T extends string> {
  value: T
  label: string
  render?: () => ReactNode
}

interface RadioGroupProps<T extends string> {
  name: string
  value: T
  onChange: (value: T) => void
  options: RadioOption<T>[]
  className?: string
  optionClassName?: string
}

export function RadioGroup<T extends string>({
  name,
  value,
  onChange,
  options,
  className,
  optionClassName,
}: RadioGroupProps<T>) {
  return (
    <div className={cn('grid gap-4', className)}>
      {options.map((opt) => (
        <label key={opt.value} className={cn('cursor-pointer group relative', optionClassName)}>
          <input
            type="radio"
            name={name}
            value={opt.value}
            checked={value === opt.value}
            onChange={() => onChange(opt.value)}
            className="sr-only peer"
          />
          {opt.render ? (
            opt.render()
          ) : (
            <div
              className={cn(
                'h-24 rounded-xl border-2 border-transparent peer-checked:border-primary',
                'bg-gray-100 dark:bg-gray-800 overflow-hidden relative',
                'hover:shadow-md transition-all',
                'flex items-center justify-center',
              )}
            >
              <div className="absolute right-2 bottom-2 text-primary opacity-0 peer-checked:opacity-100">
                <span className="material-icons text-lg">check_circle</span>
              </div>
            </div>
          )}
          <span className="block text-center text-xs mt-2 text-gray-500 peer-checked:text-primary font-medium">
            {opt.label}
          </span>
        </label>
      ))}
    </div>
  )
}
