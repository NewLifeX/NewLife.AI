import { useState, useCallback } from 'react'
import { cn } from '@/lib/utils'

interface ProgressiveImageProps {
  src?: string
  alt?: string
  className?: string
  onClick?: () => void
}

export function ProgressiveImage({ src, alt = '', className, onClick }: ProgressiveImageProps) {
  const [loaded, setLoaded] = useState(false)

  const handleLoad = useCallback(() => {
    setLoaded(true)
  }, [])

  return (
    <div className="relative inline-block">
      {!loaded && (
        <div className={cn('rounded-lg bg-gray-200 dark:bg-gray-700 animate-pulse', className)} style={{ width: 320, height: 200 }}>
          <div className="flex items-center justify-center h-full text-gray-400 dark:text-gray-500">
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
              <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
              <circle cx="8.5" cy="8.5" r="1.5" />
              <polyline points="21 15 16 10 5 21" />
            </svg>
          </div>
        </div>
      )}
      <img
        src={src}
        alt={alt}
        className={cn(
          'rounded-lg max-h-80 cursor-pointer hover:opacity-90 transition-all duration-700',
          loaded ? 'opacity-100 blur-0' : 'opacity-0 blur-md',
          className,
        )}
        onClick={onClick}
        onLoad={handleLoad}
      />
    </div>
  )
}
