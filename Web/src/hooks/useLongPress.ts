import { useRef, useCallback } from 'react'

interface UseLongPressOptions {
  /** 长按触发时间（毫秒），默认 500 */
  delay?: number
  /** 长按回调 */
  onLongPress: (e: TouchEvent | MouseEvent) => void
}

/**
 * 移动端长按手势 Hook
 * 触发长按后阻止 contextmenu 默认行为
 */
export function useLongPress({ delay = 500, onLongPress }: UseLongPressOptions) {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const triggeredRef = useRef(false)

  const start = useCallback(
    (e: React.TouchEvent | React.MouseEvent) => {
      triggeredRef.current = false
      timerRef.current = setTimeout(() => {
        triggeredRef.current = true
        onLongPress(e.nativeEvent)
      }, delay)
    },
    [delay, onLongPress],
  )

  const cancel = useCallback(() => {
    if (timerRef.current) {
      clearTimeout(timerRef.current)
      timerRef.current = null
    }
  }, [])

  const preventContext = useCallback((e: React.MouseEvent | React.TouchEvent) => {
    if (triggeredRef.current) {
      e.preventDefault()
    }
  }, [])

  return {
    onTouchStart: start,
    onTouchEnd: cancel,
    onTouchMove: cancel,
    onContextMenu: preventContext,
  }
}
