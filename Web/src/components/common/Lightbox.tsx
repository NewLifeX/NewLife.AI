import { useState, useEffect, useCallback, useRef } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

interface LightboxProps {
  images: string[]
  initialIndex?: number
  open: boolean
  onClose: () => void
  onEdit?: (imageUrl: string) => void
}

export function Lightbox({ images, initialIndex = 0, open, onClose, onEdit }: LightboxProps) {
  const [currentIndex, setCurrentIndex] = useState(initialIndex)
  const [scale, setScale] = useState(1)
  const backdropRef = useRef<HTMLDivElement>(null)
  const { t } = useTranslation()

  const handlePrev = useCallback(() => {
    setCurrentIndex((i) => (i > 0 ? i - 1 : images.length - 1))
    setScale(1)
  }, [images.length])

  const handleNext = useCallback(() => {
    setCurrentIndex((i) => (i < images.length - 1 ? i + 1 : 0))
    setScale(1)
  }, [images.length])

  const handleZoomIn = useCallback(() => {
    setScale((s) => Math.min(s + 0.5, 5))
  }, [])

  const handleZoomOut = useCallback(() => {
    setScale((s) => Math.max(s - 0.5, 0.5))
  }, [])

  useEffect(() => {
    if (!open) return
    const handleKeyDown = (e: KeyboardEvent) => {
      switch (e.key) {
        case 'Escape':
          onClose()
          break
        case 'ArrowLeft':
          handlePrev()
          break
        case 'ArrowRight':
          handleNext()
          break
        case '+':
        case '=':
          handleZoomIn()
          break
        case '-':
          handleZoomOut()
          break
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [open, onClose, handlePrev, handleNext, handleZoomIn, handleZoomOut])

  if (!open || images.length === 0) return null

  return createPortal(
    <div
      ref={backdropRef}
      className={cn('fixed inset-0 z-[9999] flex flex-col items-center justify-center bg-black/90 transition-opacity duration-200 opacity-100')}
      onClick={(e) => {
        if (e.target === backdropRef.current) onClose()
      }}
    >
      {/* Top toolbar */}
      <div className="absolute top-0 left-0 right-0 flex items-center justify-between px-4 py-3 z-10">
        <span className="text-white/70 text-sm">
          {currentIndex + 1} / {images.length}
        </span>
        <div className="flex items-center gap-2">
          <button
            onClick={handleZoomOut}
            className="text-white/70 hover:text-white p-2 rounded-full hover:bg-white/10 transition-colors"
            title="缩小"
          >
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <path d="M21 21l-4.35-4.35" />
              <path d="M8 11h6" />
            </svg>
          </button>
          <span className="text-white/70 text-xs min-w-[3em] text-center">{Math.round(scale * 100)}%</span>
          <button
            onClick={handleZoomIn}
            className="text-white/70 hover:text-white p-2 rounded-full hover:bg-white/10 transition-colors"
            title="放大"
          >
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <path d="M21 21l-4.35-4.35" />
              <path d="M8 11h6" />
              <path d="M11 8v6" />
            </svg>
          </button>
          {onEdit && (
            <button
              onClick={() => onEdit(images[currentIndex])}
              className="text-white/70 hover:text-white p-2 rounded-full hover:bg-white/10 transition-colors ml-1"
              title={t('imageEdit.editImage', '编辑图片')}
            >
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
            </button>
          )}
          <button
            onClick={onClose}
            className="text-white/70 hover:text-white p-2 rounded-full hover:bg-white/10 transition-colors ml-2"
            title="关闭 (Esc)"
          >
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M18 6L6 18" />
              <path d="M6 6l12 12" />
            </svg>
          </button>
        </div>
      </div>

      {/* Navigation arrows */}
      {images.length > 1 && (
        <>
          <button
            onClick={handlePrev}
            className="absolute left-4 top-1/2 -translate-y-1/2 text-white/60 hover:text-white p-3 rounded-full hover:bg-white/10 transition-colors z-10"
            title="上一张 (←)"
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M15 18l-6-6 6-6" />
            </svg>
          </button>
          <button
            onClick={handleNext}
            className="absolute right-4 top-1/2 -translate-y-1/2 text-white/60 hover:text-white p-3 rounded-full hover:bg-white/10 transition-colors z-10"
            title="下一张 (→)"
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M9 18l6-6-6-6" />
            </svg>
          </button>
        </>
      )}

      {/* Main image */}
      <div className="flex-1 flex items-center justify-center overflow-auto w-full px-16 py-16">
        <img
          src={images[currentIndex]}
          alt=""
          className="max-w-full max-h-full object-contain transition-transform duration-200 select-none"
          style={{ transform: `scale(${scale})` }}
          draggable={false}
        />
      </div>

      {/* Thumbnail strip */}
      {images.length > 1 && (
        <div className="absolute bottom-0 left-0 right-0 flex items-center justify-center gap-2 px-4 py-3 bg-black/50">
          {images.map((src, i) => (
            <button
              key={i}
              onClick={() => { setCurrentIndex(i); setScale(1) }}
              className={cn(
                'w-12 h-12 rounded overflow-hidden border-2 transition-all flex-shrink-0',
                i === currentIndex ? 'border-white opacity-100' : 'border-transparent opacity-50 hover:opacity-80',
              )}
            >
              <img src={src} alt="" className="w-full h-full object-cover" />
            </button>
          ))}
        </div>
      )}
    </div>,
    document.body,
  )
}
