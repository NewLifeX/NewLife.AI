import { create } from 'zustand'

export interface ToastItem {
  id: string
  type: 'error' | 'warning' | 'success' | 'info'
  message: string
  duration?: number
}

interface ToastState {
  toasts: ToastItem[]
  addToast: (toast: Omit<ToastItem, 'id'>) => void
  removeToast: (id: string) => void
}

let toastSeq = 0

export const useToastStore = create<ToastState>((set, get) => ({
  toasts: [],
  addToast: (toast) => {
    // 防重：相同类型+内容的 Toast 不重复弹出
    const existing = get().toasts
    if (existing.some((t) => t.type === toast.type && t.message === toast.message)) return
    const id = `toast-${++toastSeq}`
    const duration = toast.duration ?? 5000
    set((s) => ({ toasts: [...s.toasts, { ...toast, id }] }))
    if (duration > 0) {
      setTimeout(() => {
        set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) }))
      }, duration)
    }
  },
  removeToast: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}))

/** 在非 React 上下文（如 api.ts）中直接调用 */
export function showToast(type: ToastItem['type'], message: string, duration?: number) {
  useToastStore.getState().addToast({ type, message, duration })
}
