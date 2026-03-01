import { useToastStore } from '@/stores/toastStore'
import { Icon } from '@/components/common/Icon'

const iconMap = {
  error: 'error',
  warning: 'warning',
  success: 'check_circle',
  info: 'info',
} as const

const colorMap = {
  error: 'bg-red-50 dark:bg-red-950/60 border-red-200 dark:border-red-800 text-red-800 dark:text-red-200',
  warning: 'bg-yellow-50 dark:bg-yellow-950/60 border-yellow-200 dark:border-yellow-800 text-yellow-800 dark:text-yellow-200',
  success: 'bg-green-50 dark:bg-green-950/60 border-green-200 dark:border-green-800 text-green-800 dark:text-green-200',
  info: 'bg-blue-50 dark:bg-blue-950/60 border-blue-200 dark:border-blue-800 text-blue-800 dark:text-blue-200',
} as const

const iconColorMap = {
  error: 'text-red-500',
  warning: 'text-yellow-500',
  success: 'text-green-500',
  info: 'text-blue-500',
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
          className={`flex items-start gap-3 px-4 py-3 rounded-lg border shadow-lg animate-slide-in-right ${colorMap[toast.type]}`}
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
            className="flex-shrink-0 opacity-60 hover:opacity-100 transition-opacity mt-0.5"
          >
            <Icon name="close" size="sm" />
          </button>
        </div>
      ))}
    </div>
  )
}
