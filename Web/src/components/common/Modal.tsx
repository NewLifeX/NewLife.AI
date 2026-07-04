import { type ReactNode, useEffect } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { Icon } from './Icon'

interface ModalProps {
  open: boolean
  onClose: () => void
  children: ReactNode
  className?: string
  maxWidth?: string
  'data-testid'?: string
}

export function Modal({
  open,
  onClose,
  children,
  className,
  maxWidth = 'max-w-4xl',
  'data-testid': testId,
}: ModalProps) {
  useEffect(() => {
    if (!open) return
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', handler)
    return () => document.removeEventListener('keydown', handler)
  }, [open, onClose])

  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden'
    } else {
      document.body.style.overflow = ''
    }
    return () => {
      document.body.style.overflow = ''
    }
  }, [open])

  if (!open) return null

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 max-md:p-0">
      <div
        className="absolute inset-0 bg-slate-950/45 backdrop-blur-md animate-fade-in"
        onClick={onClose}
      />
      <div
        role="dialog"
        aria-modal="true"
        data-testid={testId}
        className={cn(
          'relative bg-[var(--color-surface-0)] w-full rounded-2xl shadow-modal',
          'border border-[var(--color-border-subtle)]',
          'flex overflow-hidden animate-scale-in',
          'max-md:rounded-none max-md:h-full max-md:max-w-none max-md:flex-col',
          maxWidth,
          className,
        )}
      >
        <button
          onClick={onClose}
          className="absolute top-4 right-4 z-20 w-7 h-7 flex items-center justify-center text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)] transition-colors rounded-full bg-[var(--color-surface-2)] hover:bg-[var(--color-surface-3)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/55 max-md:hidden"
        >
          <Icon name="close" size="sm" />
        </button>
        {children}
      </div>
    </div>,
    document.body,
  )
}
