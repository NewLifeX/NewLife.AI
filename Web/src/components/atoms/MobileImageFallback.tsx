/**
 * 移动端复制图片降级对话框。
 *
 * 当 navigator.clipboard.write 不被支持（微信/企业微信等内置浏览器）时显示。
 * 展示图片预览，提示用户长按保存，底部提供"保存图片"下载链接。
 */

import { useEffect } from 'react'
import { createPortal } from 'react-dom'
import { savePngBlob } from '@/utils/imageCapture'

interface MobileImageFallbackProps {
  /** 是否显示 */
  open: boolean
  /** PNG Blob */
  blob: Blob | null
  /** 关闭回调 */
  onClose: () => void
  /** 建议文件名 */
  filename?: string
}

export function MobileImageFallback({ open, blob, onClose, filename = 'image.png' }: MobileImageFallbackProps) {
  // ESC 关闭
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open || !blob || typeof document === 'undefined') return null

  const objectUrl = URL.createObjectURL(blob)

  function handleClose() {
    URL.revokeObjectURL(objectUrl)
    onClose()
  }

  return createPortal(
    <div
      className="fixed inset-0 z-[90] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
      onClick={handleClose}
    >
      <div
        className="relative flex flex-col items-center gap-4 rounded-2xl bg-white dark:bg-gray-900 p-5 shadow-2xl max-w-sm w-full"
        onClick={e => e.stopPropagation()}
      >
        {/* 关闭按钮 */}
        <button
          type="button"
          onClick={handleClose}
          className="absolute right-3 top-3 flex h-7 w-7 items-center justify-center rounded-full text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-600 transition-colors"
          title="关闭"
          aria-label="关闭"
        >
          ✕
        </button>

        {/* 标题 */}
        <p className="text-sm font-semibold text-gray-800 dark:text-gray-100 mt-1">复制图片</p>

        {/* 图片预览 */}
        <img
          src={objectUrl}
          alt="预览"
          className="max-h-64 w-full rounded-xl object-contain border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800"
        />

        {/* 提示文字 */}
        <p className="text-xs text-center text-gray-500 dark:text-gray-400 leading-relaxed">
          当前环境不支持直接复制图片，<br />
          <span className="font-medium text-gray-700 dark:text-gray-300">长按图片</span>可保存到相册或复制
        </p>

        {/* 另存为按钮 */}
        <button
          type="button"
          onClick={() => { savePngBlob(blob, filename); handleClose() }}
          className="w-full rounded-xl bg-blue-500 hover:bg-blue-600 active:bg-blue-700 text-white text-sm font-medium py-2.5 transition-colors"
        >
          保存图片
        </button>
      </div>
    </div>,
    document.body,
  )
}
