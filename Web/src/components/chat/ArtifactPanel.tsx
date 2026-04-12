import { useCallback, useEffect, useMemo, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { useArtifactStore } from '@/stores'
import type { Artifact } from '@/types'
import mermaid from 'mermaid'

/** 可预览的语言类型 */
const PREVIEWABLE = new Set(['html', 'svg', 'mermaid'])

/** 判断语言是否支持 Artifact 预览 */
export function isPreviewable(language: string): boolean {
  return PREVIEWABLE.has(language)
}

/** 构建 sandbox iframe 的 srcdoc。svg/html 直接嵌入，其他返回空字符串 */
function buildSrcdoc(artifact: Artifact): string {
  const { language, code } = artifact
  if (language === 'svg') {
    return `<!DOCTYPE html><html><head><meta charset="utf-8"><style>body{margin:0;display:flex;align-items:center;justify-content:center;min-height:100vh;background:#fff}svg{max-width:100%;height:auto}</style></head><body>${code}</body></html>`
  }
  if (language === 'html') {
    // 若已包含完整 <html> 或 <DOCTYPE>，直接使用；否则包裹一层
    if (/<html[\s>]/i.test(code) || /<!DOCTYPE/i.test(code)) return code
    return `<!DOCTYPE html><html><head><meta charset="utf-8"><style>body{margin:0;font-family:system-ui,sans-serif}</style></head><body>${code}</body></html>`
  }
  return ''
}

export function ArtifactPanel() {
  const { t } = useTranslation()
  const current = useArtifactStore((s) => s.current)
  const close = useArtifactStore((s) => s.close)
  const iframeRef = useRef<HTMLIFrameElement>(null)

  const srcdoc = useMemo(() => (current ? buildSrcdoc(current) : ''), [current])

  const handleCopy = useCallback(() => {
    if (current) {
      navigator.clipboard.writeText(current.code)
    }
  }, [current])

  const handleDownload = useCallback(() => {
    if (!current) return
    const ext = current.language === 'svg' ? 'svg' : 'html'
    const blob = new Blob([current.code], { type: 'text/plain;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `artifact.${ext}`
    a.click()
    URL.revokeObjectURL(url)
  }, [current])

  if (!current) return null

  const title = current.title || current.language.toUpperCase()

  return (
    <div className={cn(
      'h-full flex flex-col border-l border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900',
      'w-[480px] min-w-[360px] max-w-[50vw]',
    )}>
      {/* 标题栏 */}
      <div className="flex items-center justify-between px-4 py-2.5 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/60">
        <div className="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-200 truncate">
          <Icon name="code" size="sm" className="text-primary" />
          <span className="truncate">{title}</span>
        </div>
        <div className="flex items-center gap-1">
          <button
            onClick={handleCopy}
            className="p-1.5 rounded-md text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            title={t('artifact.copy')}
          >
            <Icon name="content_copy" size="sm" />
          </button>
          <button
            onClick={handleDownload}
            className="p-1.5 rounded-md text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            title={t('artifact.download')}
          >
            <Icon name="download" size="sm" />
          </button>
          <button
            onClick={close}
            className="p-1.5 rounded-md text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            title={t('artifact.close')}
          >
            <Icon name="close" size="sm" />
          </button>
        </div>
      </div>

      {/* 预览区域 */}
      <div className="flex-1 overflow-hidden">
        {current.language === 'mermaid' ? (
          <MermaidPreview code={current.code} />
        ) : (
          <iframe
            ref={iframeRef}
            srcDoc={srcdoc}
            sandbox="allow-scripts"
            className="w-full h-full border-0 bg-white"
            title="Artifact Preview"
          />
        )}
      </div>
    </div>
  )
}

/** Mermaid 独立预览，使用 mermaid.render */
function MermaidPreview({ code }: { code: string }) {
  const containerRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!containerRef.current) return
    const id = `artifact-mermaid-${Date.now()}`
    mermaid.render(id, code).then(({ svg }) => {
      if (containerRef.current) containerRef.current.innerHTML = svg
    }).catch(() => {
      if (containerRef.current) containerRef.current.textContent = code
    })
  }, [code])

  return (
    <div
      ref={containerRef}
      className="w-full h-full flex items-center justify-center overflow-auto p-4"
    />
  )
}
