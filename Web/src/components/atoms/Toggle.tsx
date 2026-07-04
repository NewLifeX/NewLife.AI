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
  const thumbOffset = size === 'sm' ? 16 : 20

  return (
    <button
      id={id}
      role="switch"
      type="button"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => !disabled && onChange(!checked)}
      className={cn(
        'relative inline-flex flex-shrink-0 cursor-pointer rounded-full transition-[background-color,box-shadow] duration-300 ease-out',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/55 focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--color-surface-0)]',
        trackSize,
        checked
          ? 'bg-primary shadow-[0_2px_8px_-2px_rgba(91,91,255,0.55)]'
          : 'bg-[var(--color-surface-3)]',
        disabled && 'opacity-50 cursor-not-allowed',
        className,
      )}
    >
      <span
        className={cn(
          'pointer-events-none inline-block rounded-full bg-white shadow-sm',
          thumbSize,
          'mt-0.5 ml-0.5',
        )}
        style={{
          translate: `${checked ? thumbOffset : 0}px 0`,
          transition: 'translate 0.3s ease-in-out',
        }}
      />
    </button>
  )
}
