import { type TextareaHTMLAttributes, useCallback, useEffect, useRef } from 'react'
import { cn } from '@/lib/utils'

interface TextareaProps extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'onChange'> {
  value: string
  onChange: (value: string) => void
  autoResize?: boolean
  minRows?: number
  maxRows?: number
  className?: string
}

export function Textarea({
  value,
  onChange,
  autoResize = true,
  minRows = 1,
  maxRows = 8,
  className,
  ...props
}: TextareaProps) {
  const ref = useRef<HTMLTextAreaElement>(null)

  const adjustHeight = useCallback(() => {
    const el = ref.current
    if (!el || !autoResize) return

    el.style.height = 'auto'
    const lineHeight = parseInt(getComputedStyle(el).lineHeight) || 24
    const minHeight = lineHeight * minRows
    const maxHeight = lineHeight * maxRows
    const scrollHeight = el.scrollHeight

    el.style.height = `${Math.min(Math.max(scrollHeight, minHeight), maxHeight)}px`
    el.style.overflowY = scrollHeight > maxHeight ? 'auto' : 'hidden'
  }, [autoResize, minRows, maxRows])

  useEffect(() => {
    adjustHeight()
  }, [value, adjustHeight])

  return (
    <textarea
      ref={ref}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      rows={minRows}
      className={cn(
        'w-full bg-transparent border-none',
        'text-gray-800 dark:text-gray-100',
        'placeholder-gray-400 dark:placeholder-gray-500',
        'focus:ring-0 focus:outline-none',
        'resize-none text-base leading-relaxed',
        className,
      )}
      {...props}
    />
  )
}
