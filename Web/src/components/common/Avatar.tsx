import { useState, useEffect } from 'react'
import { cn } from '@/lib/utils'

interface AvatarProps {
  type: 'user' | 'ai'
  src?: string
  alt?: string
  size?: 'sm' | 'md' | 'lg'
  letter?: string
  className?: string
}

const sizeStyles = {
  sm: 'w-7 h-7 text-[8px]',
  md: 'w-8 h-8 text-xs',
  lg: 'w-10 h-10 text-sm',
}

export function Avatar({ type, src, alt, size = 'md', letter, className }: AvatarProps) {
  const [imgError, setImgError] = useState(false)

  // src 变更时重置加载错误状态
  useEffect(() => {
    setImgError(false)
  }, [src])

  if (type === 'ai') {
    return (
      <div
        className={cn(
          'rounded-full bg-[image:var(--gradient-brand)] shadow-[0_4px_12px_-4px_rgba(91,91,255,0.55)]',
          'flex items-center justify-center text-white font-bold shadow-sm',
          sizeStyles[size],
          className,
        )}
      >
        {letter ?? 'N'}
      </div>
    )
  }

  // 空字符串视为无头像
  const validSrc = src && src.trim() ? src : undefined

  return (
    <div
      className={cn(
        'bg-blue-100 rounded-full flex items-center justify-center overflow-hidden',
        sizeStyles[size],
        className,
      )}
    >
      {validSrc && !imgError ? (
        <img
          src={validSrc}
          alt={alt ?? 'User'}
          className="w-full h-full object-cover"
          onError={() => setImgError(true)}
        />
      ) : (
        <span className="text-blue-500 font-semibold">{letter ?? 'U'}</span>
      )}
    </div>
  )
}
