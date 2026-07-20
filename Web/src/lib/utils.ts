import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function formatRelativeTime(dateStr: string, locale: string = 'zh'): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffSec = Math.floor(diffMs / 1000)
  const diffMin = Math.floor(diffSec / 60)
  const diffHour = Math.floor(diffMin / 60)
  const diffDay = Math.floor(diffHour / 24)

  const isZh = locale.startsWith('zh')
  if (diffSec < 60) return isZh ? '刚刚' : 'just now'
  if (diffMin < 60) return isZh ? `${diffMin} 分钟前` : `${diffMin}m ago`
  if (diffHour < 24) return isZh ? `${diffHour} 小时前` : `${diffHour}h ago`
  if (diffDay === 1) return isZh ? '昨天' : 'yesterday'
  if (diffDay < 7) return isZh ? `${diffDay} 天前` : `${diffDay}d ago`
  return date.toLocaleDateString(isZh ? 'zh-CN' : 'en-US')
}

export function formatExactTime(dateStr: string): string {
  const date = new Date(dateStr)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

/**
 * 创建带超时的 AbortSignal
 * AbortSignal.timeout 的低版本浏览器兼容垫片
 */
export function withTimeout(ms: number): AbortSignal {
  if (typeof AbortSignal !== 'undefined' && typeof AbortSignal.timeout === 'function')
    return AbortSignal.timeout(ms)

  const controller = new AbortController()
  setTimeout(() => controller.abort(new DOMException('Timeout', 'TimeoutError')), ms)
  return controller.signal
}

/**
 * 平滑滚动到元素（Safari < 15.4 兼容垫片）
 *
 * scrollIntoView({ behavior: 'smooth' }) 在 Safari 15.4+ / Chrome 61+ 才支持。
 * 低版本浏览器降级为瞬间跳转，避免静默失败。
 */
export function smoothScrollIntoView(el: HTMLElement, options?: ScrollIntoViewOptions): void {
  try {
    el.scrollIntoView({ behavior: 'smooth', ...options })
  } catch {
    // 降级：瞬间跳转
    el.scrollIntoView(options?.block === 'center')
  }
}
