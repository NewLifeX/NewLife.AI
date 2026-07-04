import { type InputHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

interface SliderProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'onChange' | 'type'> {
  value: number
  onChange: (value: number) => void
  min?: number
  max?: number
  step?: number
  labelLeft?: string
  labelRight?: string
  className?: string
}

export function Slider({
  value,
  onChange,
  min = 0,
  max = 100,
  step = 1,
  labelLeft,
  labelRight,
  className,
  ...props
}: SliderProps) {
  const percent = ((value - min) / (max - min)) * 100

  return (
    <div className={cn('w-full', className)}>
      <input
        type="range"
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
        min={min}
        max={max}
        step={step}
        className={cn(
          'w-full h-2 rounded-lg cursor-pointer',
          'appearance-none outline-none',
          'bg-[var(--slider-track)]',
          '[&::-webkit-slider-thumb]:appearance-none',
          '[&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4',
          '[&::-webkit-slider-thumb]:rounded-full',
          '[&::-webkit-slider-thumb]:bg-[var(--color-brand-500)]',
          '[&::-webkit-slider-thumb]:shadow-[var(--shadow-brand-glow)]',
          '[&::-webkit-slider-thumb]:cursor-pointer',
          '[&::-webkit-slider-thumb]:transition-transform',
          '[&::-webkit-slider-thumb]:hover:scale-110',
          '[&::-moz-range-thumb]:w-4 [&::-moz-range-thumb]:h-4',
          '[&::-moz-range-thumb]:rounded-full',
          '[&::-moz-range-thumb]:bg-[var(--color-brand-500)]',
          '[&::-moz-range-thumb]:border-none',
          '[&::-moz-range-thumb]:cursor-pointer',
        )}
        style={{
          background: `linear-gradient(to right, var(--color-brand-500) 0%, var(--color-accent-500) ${percent}%, var(--slider-track) ${percent}%, var(--slider-track) 100%)`,
        }}
        {...props}
      />
      {(labelLeft || labelRight) && (
        <div className="flex justify-between mt-1 text-[10px] text-gray-400">
          <span>{labelLeft}</span>
          <span>{labelRight}</span>
        </div>
      )}
    </div>
  )
}
