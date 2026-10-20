/**
 * 移动端复制图片降级对话框。
 *
 * 当 navigator.clipboard.write 不被支持（微信/企业微信等内置浏览器）时显示。
 * 展示图片预览，提示用户长按保存，底部提供"保存图片"下载链接。
 */

import { useEffect } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
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
  const { t } = useTranslation()

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
          title={t('common.close')}
          aria-label={t('common.close')}
        >
          ✕
        </button>

        {/* 标题 */}
        <p className="text-sm font-semibold text-gray-800 dark:text-gray-100 mt-1">{t('common.copyImage')}</p>

        {/* 图片预览 */}
        <img
          src={objectUrl}
          alt={t('common.preview')}
          className="max-h-64 w-full rounded-xl object-contain border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800"
        />

        {/* 提示文字 */}
        <p className="text-xs text-center text-gray-500 dark:text-gray-400 leading-relaxed">
          {t('imageFallback.unsupported')}<br />
          <span className="font-medium text-gray-700 dark:text-gray-300">{t('imageFallback.longPress')}</span>{' '}{t('imageFallback.longPressTail')}
        </p>

        {/* 另存为按钮 */}
        <button
          type="button"
          onClick={() => { savePngBlob(blob, filename); handleClose() }}
          className="w-full rounded-xl bg-blue-500 hover:bg-blue-600 active:bg-blue-700 text-white text-sm font-medium py-2.5 transition-colors"
        >
          {t('imageFallback.saveImage')}
        </button>
      </div>
    </div>,
    document.body,
  )
}
