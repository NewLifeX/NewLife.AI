import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

/** show_slide 工具返回的 JSON 结构 */
export interface SlideData {
  slideId: string
  title: string
  slideCount: number
  downloadUrl: string
  attachmentId: number
  fileSize: number
  slideTitles: string[]
  theme?: string
}

interface SlideBlockProps {
  data: SlideData
  className?: string
}

/** 内置主题的预览色（与后端 ThemeColors 对应） */
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

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

/** 幻灯片预览缩略图（主题色色块 + 页标题） */
function SlideThumbnail({ title, themeKey }: { title: string; themeKey?: string }) {
  const colors = THEME_COLORS[themeKey ?? 'blue'] ?? THEME_COLORS['blue']
  return (
    <div
      className="flex flex-col items-center justify-center rounded-md overflow-hidden border border-gray-200/60 dark:border-gray-600/60 shrink-0"
      style={{
        width: 72, height: 48,
        backgroundColor: colors.bg,
        fontSize: 10,
        lineHeight: 1.2,
      }}
    >
      {/* 模拟标题线条 */}
      <div
        className="w-10 rounded-sm mb-1"
        style={{ height: 3, backgroundColor: colors.accent, opacity: 0.9 }}
      />
      {/* 页标题 */}
      <span
        className="px-1 text-center line-clamp-2 leading-tight"
        style={{ color: colors.text, fontSize: 9, maxWidth: 64 }}
      >
        {title || '…'}
      </span>
    </div>
  )
}

/** show_slide 工具结果渲染块。展示元数据预览卡 + 下载按钮 */
export function SlideBlock({ data, className }: SlideBlockProps) {
  const themeKey = data.theme ?? 'blue'
  const colors = THEME_COLORS[themeKey] ?? THEME_COLORS['blue']

  return (
    <div
      className={cn(
        'my-2 rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800/60 overflow-hidden',
        className,
      )}
      data-testid="slide-block"
      data-slide-id={data.slideId}
    >
      {/* 头部：标题 + 页数徽章 */}
      <div
        className="flex items-center justify-between px-3 py-2 border-b border-gray-100 dark:border-gray-700"
        style={{ backgroundColor: colors.bg }}
      >
        <div className="flex items-center gap-2 min-w-0">
          <span className="shrink-0 opacity-80" style={{ color: colors.text }}>
            <Icon name="slideshow" size="sm" />
          </span>
          <span
            className="text-sm font-medium truncate"
            style={{ color: colors.text }}
          >
            {data.title}
          </span>
        </div>
        <span
          className="shrink-0 ml-2 text-xs font-medium px-2 py-0.5 rounded-full"
          style={{ backgroundColor: 'rgba(255,255,255,0.18)', color: colors.text }}
        >
          {data.slideCount} 页
        </span>
      </div>

      {/* 缩略图列表 */}
      <div className="px-3 py-3">
        <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-thin scrollbar-thumb-gray-200 dark:scrollbar-thumb-gray-600">
          {data.slideTitles.map((t, i) => (
            <div key={i} className="flex flex-col items-center gap-1 shrink-0">
              <SlideThumbnail title={t} themeKey={themeKey} />
              <span className="text-[10px] text-gray-400 dark:text-gray-500 tabular-nums">
                {i + 1}
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* 底部：文件大小 + 下载按钮 */}
      <div className="flex items-center justify-between px-3 pb-3 gap-3">
        <span className="text-xs text-gray-400 dark:text-gray-500">
          PPTX · {formatFileSize(data.fileSize)}
        </span>
        <a
          href={data.downloadUrl}
          download
          className={cn(
            'flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium text-white transition-opacity hover:opacity-90 active:opacity-75',
          )}
          style={{ backgroundColor: colors.accent }}
          data-testid="slide-download-btn"
        >
          <Icon name="download" size="sm" />
          下载 PPTX
        </a>
      </div>
    </div>
  )
}

/** 从工具结果 JSON 解析 SlideData。格式错误时返回 null */
export function parseSlideData(result?: string): SlideData | null {
  if (!result) return null
  try {
    const r = JSON.parse(result) as Partial<SlideData>
    if (
      !r.slideId ||
      !r.downloadUrl ||
      typeof r.slideCount !== 'number'
    ) return null
    return {
      slideId: String(r.slideId),
      title: typeof r.title === 'string' ? r.title : '',
      slideCount: r.slideCount,
      downloadUrl: String(r.downloadUrl),
      attachmentId: typeof r.attachmentId === 'number' ? r.attachmentId : 0,
      fileSize: typeof r.fileSize === 'number' ? r.fileSize : 0,
      slideTitles: Array.isArray(r.slideTitles) ? r.slideTitles.map(String) : [],
      theme: typeof r.theme === 'string' ? r.theme : undefined,
    }
  } catch {
    return null
  }
}
