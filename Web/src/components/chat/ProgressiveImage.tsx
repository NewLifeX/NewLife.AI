import { useState, useCallback, useRef, useEffect } from 'react'
import { cn } from '@/lib/utils'

interface ProgressiveImageProps {
  src?: string
  alt?: string
  className?: string
  onClick?: () => void
}

export function ProgressiveImage({ src, alt = '', className, onClick }: ProgressiveImageProps) {
  // data: URI 同步加载，跳过渐进动画直接显示；普通 URL 走渐进加载
  const isDataUri = Boolean(src?.startsWith('data:'))
  const [loaded, setLoaded] = useState(isDataUri)
  const [errored, setErrored] = useState(false)
  const imgRef = useRef<HTMLImageElement>(null)

  // src 变化时重置状态；data: URI 直接视为已加载
  useEffect(() => {
    if (src?.startsWith('data:')) {
      setLoaded(true)
      setErrored(false)
      return
    }
    setLoaded(false)
    setErrored(false)
    // 普通 URL 命中浏览器缓存时 load 事件不再触发，需补检 .complete
    const img = imgRef.current
    if (img?.complete) {
      if (img.naturalWidth > 0 || img.naturalHeight > 0) setLoaded(true)
      else setErrored(true)
    }
  }, [src])

  const handleLoad = useCallback(() => {
    setLoaded(true)
  }, [])

  const handleError = useCallback(() => {
    setErrored(true)
  }, [])

  return (
    <div className="relative inline-block">
      {!loaded && !errored && (
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
      {errored && (
        <div className={cn('rounded-lg bg-gray-100 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 flex flex-col items-center justify-center gap-2', className)} style={{ width: 320, height: 200 }}>
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="text-gray-400 dark:text-gray-500">
            <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
            <line x1="3" y1="3" x2="21" y2="21" />
          </svg>
          {alt && (
            <span className="text-xs text-gray-400 dark:text-gray-500 px-4 text-center line-clamp-2">{alt}</span>
          )}
        </div>
      )}
      <img
        ref={imgRef}
        src={src}
        alt={alt}
        data-testid="markdown-image"
        className={cn(
          'rounded-lg max-h-80 cursor-pointer hover:opacity-90 transition-all duration-700',
          loaded ? 'opacity-100 blur-0' : 'opacity-0 blur-md absolute top-0 left-0',
          className,
        )}
        onClick={onClick}
        onLoad={handleLoad}
        onError={handleError}
      />
    </div>
  )
}
