import { useToastStore } from '@/stores/toastStore'
import { Icon } from '@/components/common/Icon'

const iconMap = {
  error: 'error',
  warning: 'warning',
  success: 'check_circle',
  info: 'info',
} as const

const accentMap = {
  error: 'before:bg-red-500',
  warning: 'before:bg-amber-500',
  success: 'before:bg-emerald-500',
  info: 'before:bg-[color:var(--color-brand-500)]',
} as const

const iconColorMap = {
  error: 'text-red-500',
  warning: 'text-amber-500',
  success: 'text-emerald-500',
  info: 'text-[color:var(--color-brand-500)]',
} as const

export function ToastContainer() {
  const toasts = useToastStore((s) => s.toasts)
  const removeToast = useToastStore((s) => s.removeToast)

  if (toasts.length === 0) return null

  return (
    <div className="fixed top-4 right-4 z-[9999] flex flex-col gap-2 max-w-sm">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className={`relative flex items-start gap-3 pl-5 pr-4 py-3 rounded-xl glass-panel shadow-menu text-[var(--color-text-primary)] animate-slide-in-right before:content-[''] before:absolute before:left-0 before:top-2 before:bottom-2 before:w-1 before:rounded-full ${accentMap[toast.type]}`}
        >
          <Icon
            name={iconMap[toast.type]}
            variant="filled"
            size="base"
            className={`mt-0.5 flex-shrink-0 ${iconColorMap[toast.type]}`}
          />
          <p className="text-sm flex-1 leading-relaxed">{toast.message}</p>
          <button
            onClick={() => removeToast(toast.id)}
            className="flex-shrink-0 opacity-60 hover:opacity-100 transition-opacity mt-0.5 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
          >
            <Icon name="close" size="sm" />
          </button>
        </div>
      ))}
    </div>
  )
}
