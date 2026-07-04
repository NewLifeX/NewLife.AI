import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'

/** build_excel 工具返回的 JSON 结构 */
export interface SpreadsheetData {
  buildId: string
  title: string
  sheetCount: number
  sheetNames: string[]
  downloadUrl: string
  attachmentId: number
  fileSize: number
  theme?: string
}

interface SpreadsheetBlockProps { data: SpreadsheetData; className?: string }

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

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function resolveColors(theme?: string) {
  return THEME_COLORS[theme ?? 'blue'] ?? THEME_COLORS['blue']
}

export function SpreadsheetBlock({ data, className }: SpreadsheetBlockProps) {
  const colors = resolveColors(data.theme)
  return (
    <div className={cn('my-2 rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800/60 overflow-hidden', className)} data-testid="spreadsheet-block">
      {/* 头部 */}
      <div className="flex items-center justify-between px-3 py-2" style={{ backgroundColor: colors.bg }}>
        <div className="flex items-center gap-2 min-w-0">
          <span style={{ color: colors.text, opacity: 0.85 }}><Icon name="table_chart" size="sm" /></span>
          <span className="text-sm font-medium truncate" style={{ color: colors.text }}>{data.title}</span>
        </div>
        <span className="shrink-0 ml-2 text-xs px-2 py-0.5 rounded-full font-medium" style={{ backgroundColor: 'rgba(255,255,255,0.18)', color: colors.text }}>{data.sheetCount} 表</span>
      </div>
      {/* 工作表标签预览 */}
      <div className="px-3 pt-3 pb-1 flex gap-2 overflow-x-auto">
        {data.sheetNames.map((name, i) => (
          <div key={i} className="flex flex-col items-center gap-1 shrink-0">
            <div className="flex items-center justify-center rounded border" style={{ width: 80, height: 40, backgroundColor: colors.bg, opacity: 0.85 }}>
              <span className="text-[10px] font-medium truncate px-1 max-w-full" style={{ color: colors.text }}>{name || `Sheet${i + 1}`}</span>
            </div>
            <span className="text-[10px] text-gray-400 tabular-nums">{i + 1}</span>
          </div>
        ))}
      </div>
      {/* 下载 */}
      <div className="flex items-center justify-between px-3 py-3 gap-3">
        <span className="text-xs text-gray-400 dark:text-gray-500">XLSX · {formatSize(data.fileSize)}</span>
        <a href={data.downloadUrl} download className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium text-white transition-opacity hover:opacity-90" style={{ backgroundColor: colors.accent }}><Icon name="download" size="sm" />下载 XLSX</a>
      </div>
    </div>
  )
}

export function parseSpreadsheetData(result?: string): SpreadsheetData | null {
  if (!result) return null
  try {
    const r = JSON.parse(result) as Partial<SpreadsheetData>
    if (!r.buildId || !r.downloadUrl) return null
    return {
      buildId: String(r.buildId), title: typeof r.title === 'string' ? r.title : '',
      sheetCount: typeof r.sheetCount === 'number' ? r.sheetCount : 0,
      sheetNames: Array.isArray(r.sheetNames) ? r.sheetNames.map(String) : [],
      downloadUrl: String(r.downloadUrl), attachmentId: typeof r.attachmentId === 'number' ? r.attachmentId : 0,
      fileSize: typeof r.fileSize === 'number' ? r.fileSize : 0,
      theme: typeof r.theme === 'string' ? r.theme : undefined,
    }
  } catch { return null }
}
