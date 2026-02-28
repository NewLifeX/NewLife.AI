import { cn } from '@/lib/utils'

interface ToggleProps {
  checked: boolean
  onChange: (checked: boolean) => void
  size?: 'sm' | 'md'
  disabled?: boolean
  className?: string
  id?: string
}

export function Toggle({
  checked,
  onChange,
  size = 'md',
  disabled = false,
  className,
  id,
}: ToggleProps) {
  const trackSize = size === 'sm' ? 'w-9 h-5' : 'w-11 h-6'
  const thumbSize = size === 'sm' ? 'w-4 h-4' : 'w-5 h-5'
  const thumbTranslate = size === 'sm' ? 'translate-x-4' : 'translate-x-5'

  return (
    <button
      id={id}
      role="switch"
      type="button"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => !disabled && onChange(!checked)}
      className={cn(
        'relative inline-flex flex-shrink-0 cursor-pointer rounded-full transition-colors duration-300 ease-in-out',
        trackSize,
        checked ? 'bg-primary' : 'bg-gray-300 dark:bg-gray-600',
        disabled && 'opacity-50 cursor-not-allowed',
        className,
      )}
    >
      <span
        className={cn(
          'pointer-events-none inline-block rounded-full bg-white shadow-sm transform transition-transform duration-300 ease-in-out',
          thumbSize,
          'mt-0.5 ml-0.5',
          checked && thumbTranslate,
        )}
      />
    </button>
  )
}
