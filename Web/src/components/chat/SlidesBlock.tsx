import { useMemo, useState } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

/** build_slides 工具返回的 JSON 结构 */
export interface SlidesData {
  artifactId: number
  title: string
  pageCount: number
  pageTitles: string[]
  pages: string[]
  theme?: string
  version: number
  downloadUrl?: string
  attachmentId?: string
}

interface SlidesBlockProps {
  data: SlidesData
  className?: string
  /** 点击"修改此制品"时回调，artifactId 由父组件注入下一条用户消息 */
  onEditArtifact?: (artifactId: number) => void
}

/** 内置主题色（与后端 ThemeColors 对应，供头部样式） */
const THEME_COLORS: Record<string, { bg: string; accent: string; text: string }> = {
  blue:      { bg: '#1E40AF', accent: '#2563EB', text: '#FFFFFF' },
  dark:      { bg: '#0F172A', accent: '#6366F1', text: '#F1F5F9' },
  corporate: { bg: '#374151', accent: '#1F2937', text: '#FFFFFF' },
  warm:      { bg: '#C2410C', accent: '#EA580C', text: '#FFFFFF' },
  green:     { bg: '#15803D', accent: '#16A34A', text: '#FFFFFF' },
  minimal:   { bg: '#18181B', accent: '#71717A', text: '#FAFAFA' },
  ocean:     { bg: '#0C4A6E', accent: '#0EA5E9', text: '#FFFFFF' },
  sunset:    { bg: '#1E1B4B', accent: '#F97316', text: '#FFFFFF' },
  forest:    { bg: '#064E3B', accent: '#059669', text: '#FFFFFF' },
  slate:     { bg: '#0F172A', accent: '#64748B', text: '#F8FAFC' },
  amber:     { bg: '#451A03', accent: '#F59E0B', text: '#FFFFFF' },
}

/**
 * 构建单页 HTML 的 srcDoc（sandbox iframe 隔离渲染）。
 * 幻灯片页面为 16:9 画布（960x540），在 iframe 中缩放适配宽度显示。
 */
function buildSlideSrcDoc(html: string, _pageIndex: number): string {
  // 页面固定 960x540，通过 transform: scale 适配 iframe 宽度，保持 16:9 视觉
  return `<!doctype html>
<html><head><meta charset="utf-8">
<style>
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; background: transparent; }
  .stage { width: 960px; height: 540px; transform-origin: top left; overflow: hidden; }
  .frame { position: relative; }
  .loader { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; font-family: system-ui, sans-serif; font-size: 13px; color: #9ca3af; }
</style>
</head>
<body>
  <div class="frame">
    <div class="loader">加载中…</div>
    <div class="stage">${html}</div>
  </div>
  <script>
  (function(){
    function fit(){
      var w = window.innerWidth;
      var scale = w / 960;
      var stage = document.querySelector('.stage');
      if (stage) {
        stage.style.transform = 'scale(' + scale + ')';
        stage.style.height = 540 + 'px';
      }
      var loader = document.querySelector('.loader');
      if (loader) loader.remove();
    }
    window.addEventListener('load', fit);
    window.addEventListener('resize', fit);
    fit();
  })();
  </script>
</body></html>`
}

/** HTML 幻灯片缩略图（960x540 缩略） */
function SlideThumb({ title, index, active, onClick }: { title: string; index: number; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'flex flex-col items-center gap-1 shrink-0 rounded-lg p-1.5 transition-colors border',
        active
          ? 'border-blue-500 bg-blue-50 dark:bg-blue-500/10'
          : 'border-transparent hover:bg-gray-100 dark:hover:bg-gray-700/40',
      )}
      data-testid={`slide-thumb-${index}`}
    >
      {/* 16:9 缩略占位（64x36） */}
      <div className="flex items-center justify-center rounded-md overflow-hidden border border-gray-200/60 dark:border-gray-600/60 bg-gradient-to-br from-gray-100 to-gray-200 dark:from-gray-800 dark:to-gray-700" style={{ width: 64, height: 36 }}>
        <span className="text-[9px] text-gray-400 dark:text-gray-500 px-1 text-center line-clamp-2 leading-tight">
          {title || `第 ${index + 1} 页`}
        </span>
      </div>
      <span className="text-[10px] tabular-nums text-gray-400 dark:text-gray-500">
        {index + 1}
      </span>
    </button>
  )
}

/** 全屏预览对话框（逐页 iframe 渲染） */
function SlidesFullscreenDialog({ data, initial, onClose }: { data: SlidesData; initial: number; onClose: () => void }) {
  const [page, setPage] = useState(initial)

  // 键盘左右翻页
  useMemo(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
      if (e.key === 'ArrowRight' || e.key === 'PageDown') setPage(p => Math.min(data.pageCount - 1, p + 1))
      if (e.key === 'ArrowLeft' || e.key === 'PageUp') setPage(p => Math.max(0, p - 1))
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data.pageCount])

  const currentHtml = data.pages[page] ?? ''
  const srcDoc = useMemo(() => buildSlideSrcDoc(currentHtml, page), [currentHtml, page])

  if (typeof document === 'undefined') return null

  return createPortal(
    <div className="fixed inset-0 z-[80] bg-black/80 backdrop-blur-sm flex flex-col" data-testid="slides-fullscreen-dialog">
      <div className="absolute inset-0" onClick={onClose} />
      <div className="relative flex flex-col h-full">
        {/* 标题栏 */}
        <div className="flex-none flex items-center justify-between gap-4 border-b border-white/10 px-4 py-3 text-white">
          <div className="flex items-center gap-3 min-w-0">
            <span className="text-sm font-medium truncate">{data.title}</span>
            <span className="text-xs text-white/50 shrink-0">
              {page + 1} / {data.pageCount} 页
            </span>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <button
              type="button"
              onClick={() => setPage(p => Math.max(0, p - 1))}
              disabled={page === 0}
              className="flex h-8 w-8 items-center justify-center rounded-lg border border-white/15 bg-white/10 hover:bg-white/15 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
              title="上一页 (←)"
            >
              <Icon name="chevron_left" size="sm" />
            </button>
            <button
              type="button"
              onClick={() => setPage(p => Math.min(data.pageCount - 1, p + 1))}
              disabled={page >= data.pageCount - 1}
              className="flex h-8 w-8 items-center justify-center rounded-lg border border-white/15 bg-white/10 hover:bg-white/15 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
              title="下一页 (→)"
            >
              <Icon name="chevron_right" size="sm" />
            </button>
            <button
              type="button"
              onClick={onClose}
              className="flex h-8 w-8 items-center justify-center rounded-lg border border-white/15 bg-white/10 hover:bg-white/15 transition-colors"
              title="关闭 (ESC)"
            >
              <Icon name="close" size="sm" />
            </button>
          </div>
        </div>
        {/* 全屏预览区：居中显示 16:9 页面 */}
        <div className="flex-1 relative overflow-auto flex items-center justify-center p-4 sm:p-8">
          <div className="w-full max-w-[960px] aspect-video bg-white rounded-lg shadow-2xl overflow-hidden relative">
            <iframe
              sandbox="allow-scripts"
              srcDoc={srcDoc}
              title={`${data.title} - 第 ${page + 1} 页`}
              className="absolute inset-0 w-full h-full border-0"
              data-testid="slides-fullscreen-frame"
            />
          </div>
        </div>
      </div>
    </div>,
    document.body,
  )
}

/** build_slides 工具结果渲染块。展示多页 HTML 幻灯片，支持逐页预览与迭代修改 */
export function SlidesBlock({ data, className, onEditArtifact }: SlidesBlockProps) {
  const [activePage, setActivePage] = useState(0)
  const [fullscreen, setFullscreen] = useState(false)

  const themeKey = data.theme ?? 'blue'
  const colors = THEME_COLORS[themeKey] ?? THEME_COLORS['blue']

  const currentHtml = data.pages[activePage] ?? ''
  const srcDoc = useMemo(() => buildSlideSrcDoc(currentHtml, activePage), [currentHtml, activePage])

  return (
    <div
      className={cn(
        'my-2 rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800/60 overflow-hidden',
        className,
      )}
      data-testid="slides-block"
      data-artifact-id={data.artifactId}
    >
      {/* 头部：标题 + 页数徽章 + 版本 */}
      <div
        className="flex items-center justify-between px-3 py-2 border-b border-gray-100 dark:border-gray-700"
        style={{ backgroundColor: colors.bg }}
      >
        <div className="flex items-center gap-2 min-w-0">
          <span className="shrink-0 opacity-80" style={{ color: colors.text }}>
            <Icon name="slideshow" size="sm" />
          </span>
          <span className="text-sm font-medium truncate" style={{ color: colors.text }}>
            {data.title}
          </span>
        </div>
        <span
          className="shrink-0 ml-2 text-xs font-medium px-2 py-0.5 rounded-full"
          style={{ backgroundColor: 'rgba(255,255,255,0.18)', color: colors.text }}
        >
          {data.pageCount} 页{data.version > 1 ? ` · v${data.version}` : ''}
        </span>
      </div>

      {/* 主预览区：当前页 16:9 iframe */}
      <div className="relative w-full bg-gray-50 dark:bg-gray-900/40" data-testid="slides-preview">
        <div className="w-full aspect-video relative">
          <iframe
            sandbox="allow-scripts"
            srcDoc={srcDoc}
            title={`${data.title} - 第 ${activePage + 1} 页`}
            className="absolute inset-0 w-full h-full border-0 bg-white"
            data-testid="slides-preview-frame"
          />
        </div>
        {/* 全屏按钮 */}
        <button
          type="button"
          onClick={() => setFullscreen(true)}
          className="absolute top-2 right-2 flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium bg-black/50 text-white hover:bg-black/70 transition-colors"
          data-testid="slides-fullscreen-btn"
        >
          <Icon name="fullscreen" size="xs" />
          全屏
        </button>
      </div>

      {/* 缩略图横向列表 */}
      <div className="px-3 py-2">
        <div className="flex gap-1 overflow-x-auto pb-1 scrollbar-thin scrollbar-thumb-gray-200 dark:scrollbar-thumb-gray-600">
          {data.pageTitles.map((t, i) => (
            <SlideThumb
              key={i}
              title={t}
              index={i}
              active={i === activePage}
              onClick={() => setActivePage(i)}
            />
          ))}
        </div>
      </div>

      {/* 底部：修改此制品 + 导出（可选） */}
      <div className="flex items-center justify-between px-3 pb-3 gap-3">
        <span className="text-xs text-gray-400 dark:text-gray-500">
          HTML 幻灯片 · #{data.artifactId}
        </span>
        <div className="flex items-center gap-2">
          {onEditArtifact && (
            <button
              type="button"
              onClick={() => onEditArtifact(data.artifactId)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium text-white transition-opacity hover:opacity-90 active:opacity-75"
              style={{ backgroundColor: colors.accent }}
              data-testid="slides-edit-btn"
            >
              <Icon name="edit" size="sm" />
              修改此制品
            </button>
          )}
        </div>
      </div>

      {fullscreen && (
        <SlidesFullscreenDialog data={data} initial={activePage} onClose={() => setFullscreen(false)} />
      )}
    </div>
  )
}

/** 从工具结果 JSON 解析 SlidesData。格式错误时返回 null */
export function parseSlidesData(result?: string): SlidesData | null {
  if (!result) return null
  try {
    const r = JSON.parse(result) as Partial<SlidesData>
    if (
      typeof r.artifactId !== 'number' ||
      !Array.isArray(r.pages) ||
      r.pages.length === 0
    ) return null
    return {
      artifactId: r.artifactId,
      title: typeof r.title === 'string' ? r.title : '',
      pageCount: typeof r.pageCount === 'number' ? r.pageCount : r.pages.length,
      pageTitles: Array.isArray(r.pageTitles) ? r.pageTitles.map(String) : [],
      pages: r.pages.map(String),
      theme: typeof r.theme === 'string' ? r.theme : undefined,
      version: typeof r.version === 'number' ? r.version : 1,
      downloadUrl: typeof r.downloadUrl === 'string' ? r.downloadUrl : undefined,
      attachmentId: typeof r.attachmentId === 'string' ? r.attachmentId : undefined,
    }
  } catch {
    return null
  }
}
