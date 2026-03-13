import { useState, useRef, useCallback, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import { cn } from '@/lib/utils'

interface ImageEditDialogProps {
  imageUrl: string
  onSubmit: (image: Blob, mask: Blob, prompt: string) => void
  onClose: () => void
}

export function ImageEditDialog({ imageUrl, onSubmit, onClose }: ImageEditDialogProps) {
  const { t } = useTranslation()
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const maskCanvasRef = useRef<HTMLCanvasElement>(null)
  const [prompt, setPrompt] = useState('')
  const [brushSize, setBrushSize] = useState(30)
  const [isDrawing, setIsDrawing] = useState(false)
  const [imageLoaded, setImageLoaded] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const imageRef = useRef<HTMLImageElement | null>(null)

  // 加载图片到 canvas
  useEffect(() => {
    const img = new Image()
    img.crossOrigin = 'anonymous'
    img.onload = () => {
      imageRef.current = img
      const canvas = canvasRef.current
      const maskCanvas = maskCanvasRef.current
      if (!canvas || !maskCanvas) return

      canvas.width = img.naturalWidth
      canvas.height = img.naturalHeight
      maskCanvas.width = img.naturalWidth
      maskCanvas.height = img.naturalHeight

      const ctx = canvas.getContext('2d')
      if (ctx) {
        ctx.drawImage(img, 0, 0)
      }

      // 初始化 mask 为全透明
      const maskCtx = maskCanvas.getContext('2d')
      if (maskCtx) {
        maskCtx.clearRect(0, 0, maskCanvas.width, maskCanvas.height)
      }

      setImageLoaded(true)
    }
    img.src = imageUrl
  }, [imageUrl])

  const getCanvasPoint = useCallback((e: React.MouseEvent<HTMLCanvasElement>) => {
    const canvas = canvasRef.current
    if (!canvas) return null
    const rect = canvas.getBoundingClientRect()
    const scaleX = canvas.width / rect.width
    const scaleY = canvas.height / rect.height
    return {
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY,
    }
  }, [])

  const drawMaskDot = useCallback((x: number, y: number) => {
    const canvas = canvasRef.current
    const maskCanvas = maskCanvasRef.current
    if (!canvas || !maskCanvas) return

    // 在显示 canvas 上画半透明红色
    const ctx = canvas.getContext('2d')
    if (ctx) {
      ctx.globalAlpha = 0.4
      ctx.fillStyle = '#ff0000'
      ctx.beginPath()
      ctx.arc(x, y, brushSize, 0, Math.PI * 2)
      ctx.fill()
      ctx.globalAlpha = 1.0
    }

    // 在 mask canvas 上画白色（表示要编辑的区域）
    const maskCtx = maskCanvas.getContext('2d')
    if (maskCtx) {
      maskCtx.fillStyle = '#ffffff'
      maskCtx.beginPath()
      maskCtx.arc(x, y, brushSize, 0, Math.PI * 2)
      maskCtx.fill()
    }
  }, [brushSize])

  const handleMouseDown = useCallback((e: React.MouseEvent<HTMLCanvasElement>) => {
    setIsDrawing(true)
    const pt = getCanvasPoint(e)
    if (pt) drawMaskDot(pt.x, pt.y)
  }, [getCanvasPoint, drawMaskDot])

  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLCanvasElement>) => {
    if (!isDrawing) return
    const pt = getCanvasPoint(e)
    if (pt) drawMaskDot(pt.x, pt.y)
  }, [isDrawing, getCanvasPoint, drawMaskDot])

  const handleMouseUp = useCallback(() => {
    setIsDrawing(false)
  }, [])

  const handleClearMask = useCallback(() => {
    const canvas = canvasRef.current
    const maskCanvas = maskCanvasRef.current
    if (!canvas || !maskCanvas || !imageRef.current) return

    // 重绘原图
    const ctx = canvas.getContext('2d')
    if (ctx) {
      ctx.clearRect(0, 0, canvas.width, canvas.height)
      ctx.drawImage(imageRef.current, 0, 0)
    }

    // 清空 mask
    const maskCtx = maskCanvas.getContext('2d')
    if (maskCtx) {
      maskCtx.clearRect(0, 0, maskCanvas.width, maskCanvas.height)
    }
  }, [])

  const handleSubmit = useCallback(async () => {
    const canvas = canvasRef.current
    const maskCanvas = maskCanvasRef.current
    if (!canvas || !maskCanvas || !prompt.trim()) return

    setSubmitting(true)
    try {
      const imageBlob = await new Promise<Blob>((resolve, reject) => {
        // 使用原图
        const origCanvas = document.createElement('canvas')
        origCanvas.width = canvas.width
        origCanvas.height = canvas.height
        const ctx = origCanvas.getContext('2d')
        if (ctx && imageRef.current) {
          ctx.drawImage(imageRef.current, 0, 0)
        }
        origCanvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Failed to convert image'))), 'image/png')
      })

      const maskBlob = await new Promise<Blob>((resolve, reject) => {
        maskCanvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Failed to convert mask'))), 'image/png')
      })

      onSubmit(imageBlob, maskBlob, prompt.trim())
    } finally {
      setSubmitting(false)
    }
  }, [prompt, onSubmit])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60" onClick={onClose}>
      <div
        className="bg-white dark:bg-gray-900 rounded-2xl shadow-2xl max-w-4xl w-full mx-4 max-h-[90vh] flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        {/* 标题栏 */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">
            {t('imageEdit.title', '图像编辑')}
          </h2>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors">
            <Icon name="close" variant="outlined" size="base" />
          </button>
        </div>

        {/* Canvas 区域 */}
        <div className="flex-1 overflow-auto p-6">
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-3">
            {t('imageEdit.hint', '在图片上涂抹红色区域，标记需要重绘的部分')}
          </p>

          <div className="relative inline-block border border-gray-300 dark:border-gray-600 rounded-lg overflow-hidden">
            <canvas
              ref={canvasRef}
              className="max-w-full h-auto cursor-crosshair"
              style={{ maxHeight: '50vh' }}
              onMouseDown={handleMouseDown}
              onMouseMove={handleMouseMove}
              onMouseUp={handleMouseUp}
              onMouseLeave={handleMouseUp}
            />
            <canvas ref={maskCanvasRef} className="hidden" />
          </div>

          {/* 工具栏 */}
          <div className="flex items-center gap-4 mt-4">
            <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-300">
              {t('imageEdit.brushSize', '画笔大小')}
              <input
                type="range"
                min={5}
                max={80}
                value={brushSize}
                onChange={(e) => setBrushSize(Number(e.target.value))}
                className="w-32"
              />
              <span className="text-xs w-8">{brushSize}px</span>
            </label>
            <button
              onClick={handleClearMask}
              className="text-sm text-blue-600 dark:text-blue-400 hover:underline"
            >
              {t('imageEdit.clearMask', '清除涂抹')}
            </button>
          </div>
        </div>

        {/* 提示词输入 + 提交 */}
        <div className="px-6 py-4 border-t border-gray-200 dark:border-gray-700">
          <div className="flex gap-3">
            <input
              type="text"
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder={t('imageEdit.promptPlaceholder', '描述你想要的编辑效果...')}
              className={cn(
                'flex-1 px-4 py-2.5 rounded-xl border border-gray-300 dark:border-gray-600',
                'bg-gray-50 dark:bg-gray-800 text-gray-900 dark:text-gray-100',
                'focus:outline-none focus:ring-2 focus:ring-primary/50',
              )}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey && prompt.trim() && imageLoaded) {
                  e.preventDefault()
                  handleSubmit()
                }
              }}
            />
            <button
              onClick={handleSubmit}
              disabled={!prompt.trim() || !imageLoaded || submitting}
              className={cn(
                'px-6 py-2.5 rounded-xl font-medium text-white transition-colors',
                'bg-primary hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed',
              )}
            >
              {submitting ? t('common.loading') : t('imageEdit.submit', '开始编辑')}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
