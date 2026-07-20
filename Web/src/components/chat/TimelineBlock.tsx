import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'
import { captureDomAsPng, copyImageOrFallback, savePngBlob } from '@/utils/imageCapture'
import { MobileImageFallback } from '@/components/atoms/MobileImageFallback'
import { Select } from '@/components/atoms/Select'
import { Icon } from '@/components/common/Icon'

// ── 类型 ─────────────────────────────────────────────────────────────────────

export interface TimelineItem {
  date: string
  title: string
  description?: string
  color?: string
  category?: string
}

export type TimelineLayout =
  | 'vertical'
  | 'alternating-top'
  | 'alternating-bottom'
  | 'horizontal-left'
  | 'horizontal-right'
  | 's-curve'
  | 'fishbone-left'
  | 'fishbone-right'

export interface TimelineSpec {
  timelineId: string
  title: string
  items: TimelineItem[]
  layout?: TimelineLayout
  palette?: string[]
  density?: 'compact' | 'normal' | 'relaxed'
}

const PALETTE = ['#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de', '#3ba272', '#fc8452', '#9a60b4']

// ── 数据解析 ─────────────────────────────────────────────────────────────────

const VALID_LAYOUTS: TimelineLayout[] = [
  'vertical', 'alternating-top', 'alternating-bottom',
  'horizontal-left', 'horizontal-right',
  's-curve', 'fishbone-left', 'fishbone-right',
]

export function parseTimelineData(result: string): TimelineSpec | null {
  try {
    const raw = JSON.parse(result)
    if (!raw?.timelineId || !Array.isArray(raw.items)) return null
    var palette: string[] | undefined
    if (Array.isArray(raw.palette) && raw.palette.every((c: unknown) => typeof c === 'string'))
      palette = raw.palette as string[]
    var density: TimelineSpec['density']
    if (raw.density === 'compact' || raw.density === 'normal' || raw.density === 'relaxed')
      density = raw.density
    var layout: TimelineSpec['layout']
    if (typeof raw.layout === 'string' && VALID_LAYOUTS.includes(raw.layout as TimelineLayout))
      layout = raw.layout as TimelineLayout
    return {
      timelineId: String(raw.timelineId),
      title: String(raw.title ?? ''),
      layout,
      palette,
      density,
      items: (raw.items as Record<string, unknown>[]).map((item) => ({
        date: String(item['date'] ?? ''),
        title: String(item['title'] ?? ''),
        description: item['description'] ? String(item['description']) : undefined,
        color: item['color'] ? String(item['color']) : undefined,
        category: item['category'] ? String(item['category']) : undefined,
      })),
    }
  } catch { return null }
}

// ── 内部类型 ─────────────────────────────────────────────────────────────────

interface TimelineItemWithColor extends TimelineItem { color: string }

interface LayoutRendererProps {
  items: TimelineItemWithColor[]
  density: 'compact' | 'normal' | 'relaxed'
  perRow?: number
}

// ── 卡片子组件 ───────────────────────────────────────────────────────────────

function TimelineCard({ item, className, showDate, showTitle, showDescription }: { item: TimelineItemWithColor; className?: string; showDate?: boolean; showTitle?: boolean; showDescription?: boolean }) {
  const date = showDate ?? true
  const title = showTitle ?? true
  const desc = showDescription ?? true
  return (
    <div className={cn('rounded-lg border px-3 py-2 bg-gray-50 dark:bg-gray-800/50', className)}
      style={{ borderColor: `${item.color}40` }}
      title={[item.date, item.title, item.description].filter(Boolean).join(' · ')}>
      {date && <span className="text-[10px] font-medium text-gray-400 dark:text-gray-500 block mb-0.5">{item.date}</span>}
      {title && <p className="text-sm font-medium text-gray-800 dark:text-gray-100 leading-snug line-clamp-2">{item.title}</p>}
      {desc && item.description && <p className="text-xs text-gray-500 dark:text-gray-400 mt-1 leading-relaxed line-clamp-2">{item.description}</p>}
    </div>
  )
}

function TimelineDot({ color }: { color: string }) {
  return (
    <div className="relative z-10 mt-1 shrink-0 mx-0">
      <div className="w-3 h-3 rounded-full ring-2 ring-white dark:ring-gray-900" style={{ backgroundColor: color }} />
    </div>
  )
}

// ── 布局渲染器 ──────────────────────────────────────────────────────────────

/** 纵向时间轴。左侧 date + category 各一行右对齐靠主轴，右侧 title+description 卡片 */
function VerticalLayout({ items, density }: LayoutRendererProps) {
  const gapClass = density === 'compact' ? 'space-y-2' : density === 'relaxed' ? 'space-y-6' : 'space-y-4'
  return (
    <div className="relative">
      <div className="absolute left-[7rem] top-3 bottom-3 w-px bg-gray-200 dark:bg-gray-700" />
      <div className={gapClass}>
        {items.map((item, idx) => (
          <div key={idx} className="flex items-start gap-0">
            <div className="w-28 shrink-0 text-right pr-3">
              <div className="text-xs font-medium text-gray-500 dark:text-gray-400 leading-tight">{item.date}</div>
              {item.category && <div className="text-[10px] text-gray-400 dark:text-gray-500 leading-tight mt-0.5">{item.category}</div>}
            </div>
            <TimelineDot color={item.color} />
            <div className="ml-3 flex-1 min-w-0">
              <TimelineCard item={item} showDate={false} />
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}

/** 交替上：垂直中线，左右交替，倒序。上行 category+date（左 date 靠右，右 date 靠左），下行 title+description 卡片 */
function AlternatingTopLayout({ items, density }: LayoutRendererProps) {
  const gapClass = density === 'compact' ? 'space-y-2' : density === 'relaxed' ? 'space-y-6' : 'space-y-4'
  const reversed = useMemo(() => [...items].reverse(), [items])
  return (
    <div className="relative">
      <div className="absolute left-1/2 top-3 bottom-3 w-px bg-gray-200 dark:bg-gray-700" />
      <div className={gapClass}>
        {reversed.map((item, idx) => {
          const isLeft = idx % 2 === 0
          return (
            <div key={idx} className="flex items-start">
              <div className="w-[calc(50%-0.375rem)] shrink-0">
                {isLeft && (
                  <div className="flex flex-col items-end pr-2">
                    {/* 上行：category + date，date 靠右（近时间轴） */}
                    <div className="flex items-center gap-1 text-xs">
                      {item.category && <span className="text-[10px] text-gray-400 dark:text-gray-500">{item.category}</span>}
                      <span className="font-medium text-gray-500 dark:text-gray-400">{item.date}</span>
                    </div>
                    {/* 下行：title+description 卡片 */}
                    <div className="mt-1 max-w-[260px] w-full">
                      <TimelineCard item={item} showDate={false} className="text-right" />
                    </div>
                  </div>
                )}
              </div>
              <TimelineDot color={item.color} />
              <div className="w-[calc(50%-0.375rem)] shrink-0">
                {!isLeft && (
                  <div className="flex flex-col items-start pl-2">
                    {/* 上行：date + category，date 靠左（近时间轴） */}
                    <div className="flex items-center gap-1 text-xs">
                      <span className="font-medium text-gray-500 dark:text-gray-400">{item.date}</span>
                      {item.category && <span className="text-[10px] text-gray-400 dark:text-gray-500">{item.category}</span>}
                    </div>
                    {/* 下行：title+description 卡片 */}
                    <div className="mt-1 max-w-[260px] w-full">
                      <TimelineCard item={item} showDate={false} />
                    </div>
                  </div>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/** 交替下：垂直中线，左右交替，顺序。上行 category+date（左 date 靠右，右 date 靠左），下行 title+description 卡片 */
function AlternatingBottomLayout({ items, density }: LayoutRendererProps) {
  const gapClass = density === 'compact' ? 'space-y-2' : density === 'relaxed' ? 'space-y-6' : 'space-y-4'
  return (
    <div className="relative">
      <div className="absolute left-1/2 top-3 bottom-3 w-px bg-gray-200 dark:bg-gray-700" />
      <div className={gapClass}>
        {items.map((item, idx) => {
          const isLeft = idx % 2 === 0
          return (
            <div key={idx} className="flex items-start">
              <div className="w-[calc(50%-0.375rem)] shrink-0">
                {isLeft && (
                  <div className="flex flex-col items-end pr-2">
                    <div className="flex items-center gap-1 text-xs">
                      {item.category && <span className="text-[10px] text-gray-400 dark:text-gray-500">{item.category}</span>}
                      <span className="font-medium text-gray-500 dark:text-gray-400">{item.date}</span>
                    </div>
                    <div className="mt-1 max-w-[260px] w-full">
                      <TimelineCard item={item} showDate={false} className="text-right" />
                    </div>
                  </div>
                )}
              </div>
              <TimelineDot color={item.color} />
              <div className="w-[calc(50%-0.375rem)] shrink-0">
                {!isLeft && (
                  <div className="flex flex-col items-start pl-2">
                    <div className="flex items-center gap-1 text-xs">
                      <span className="font-medium text-gray-500 dark:text-gray-400">{item.date}</span>
                      {item.category && <span className="text-[10px] text-gray-400 dark:text-gray-500">{item.category}</span>}
                    </div>
                    <div className="mt-1 max-w-[260px] w-full">
                      <TimelineCard item={item} showDate={false} />
                    </div>
                  </div>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/** 横向左：水平主轴在上，items 倒序，箭头向左。圆点在主轴交汇处，连接线从圆点引下 */
function HorizontalLeftLayout({ items, density }: LayoutRendererProps) {
  const colW = density === 'compact' ? 140 : density === 'relaxed' ? 200 : 165
  const reversed = useMemo(() => [...items].reverse(), [items])
  return (
    <div className="overflow-x-auto pb-2 -mx-2 px-2">
      <div className="relative flex gap-0 min-w-max" style={{ minHeight: '220px' }}>
        {/* 水平主轴 */}
        <div className="absolute left-0 right-0 h-0.5 bg-gray-300 dark:bg-gray-600" style={{ top: 5 }} />
        {reversed.map((item, idx) => (
          <div key={idx} className="relative flex flex-col items-center shrink-0" style={{ width: colW }}>
            {/* 圆点在主轴交汇处 */}
            <div className="w-3 h-3 rounded-full ring-2 ring-white dark:ring-gray-900 shrink-0 relative z-10" style={{ backgroundColor: item.color }} />
            {/* 连接线上段：圆点 → 标签 */}
            <div className="w-0.5 h-2.5 bg-gray-300 dark:bg-gray-600" />
            {/* date+category 在线中段 */}
            <span className="text-[10px] text-gray-500 dark:text-gray-400 whitespace-nowrap bg-white dark:bg-gray-900 px-1 leading-none relative z-10">
              <span className="font-medium">{item.date}</span>
              {item.category && <span className="text-gray-400 dark:text-gray-500 ml-1">{item.category}</span>}
            </span>
            {/* 连接线下段：标签 → 卡片 */}
            <div className="w-0.5 h-2.5 bg-gray-300 dark:bg-gray-600" />
            {/* 卡片：title+description */}
            <div className="w-full px-1">
              <TimelineCard item={item} showDate={false} />
            </div>
            {/* 左箭头（首项） */}
            {idx === 0 && <div className="absolute top-[3px] -left-2 w-0 h-0 border-t-[6px] border-t-transparent border-b-[6px] border-b-transparent border-r-[10px] border-r-gray-300 dark:border-r-gray-600" />}
          </div>
        ))}
      </div>
    </div>
  )
}

/** 横向右：水平主轴在上，items 顺序，箭头向右。圆点在主轴交汇处，连接线从圆点引下 */
function HorizontalRightLayout({ items, density }: LayoutRendererProps) {
  const colW = density === 'compact' ? 140 : density === 'relaxed' ? 200 : 165
  return (
    <div className="overflow-x-auto pb-2 -mx-2 px-2">
      <div className="relative flex gap-0 min-w-max" style={{ minHeight: '220px' }}>
        <div className="absolute left-0 right-0 h-0.5 bg-gray-300 dark:bg-gray-600" style={{ top: 5 }} />
        {items.map((item, idx) => (
          <div key={idx} className="relative flex flex-col items-center shrink-0" style={{ width: colW }}>
            <div className="w-3 h-3 rounded-full ring-2 ring-white dark:ring-gray-900 shrink-0 relative z-10" style={{ backgroundColor: item.color }} />
            <div className="w-0.5 h-2.5 bg-gray-300 dark:bg-gray-600" />
            <span className="text-[10px] text-gray-500 dark:text-gray-400 whitespace-nowrap bg-white dark:bg-gray-900 px-1 leading-none relative z-10">
              <span className="font-medium">{item.date}</span>
              {item.category && <span className="text-gray-400 dark:text-gray-500 ml-1">{item.category}</span>}
            </span>
            <div className="w-0.5 h-2.5 bg-gray-300 dark:bg-gray-600" />
            <div className="w-full px-1">
              <TimelineCard item={item} showDate={false} />
            </div>
            {idx === items.length - 1 && <div className="absolute top-[3px] -right-2 w-0 h-0 border-t-[6px] border-t-transparent border-b-[6px] border-b-transparent border-l-[10px] border-l-gray-300 dark:border-l-gray-600" />}
          </div>
        ))}
      </div>
    </div>
  )
}

/** S 形时间轴（蛇形）。ResizeObserver 自适应容器宽度，卡片在轴线下方不重叠 */
function SCurveLayout({ items, density, perRow }: LayoutRendererProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [containerW, setContainerW] = useState(600)

  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    // 低版本浏览器 fallback：ResizeObserver 不可用时降级为 window.resize
    try {
      const ro = new ResizeObserver(([entry]) => { if (entry) setContainerW(entry.contentRect.width) })
      ro.observe(el)
      setContainerW(el.clientWidth)
      return () => ro.disconnect()
    } catch {
      const onResize = () => setContainerW(el.clientWidth)
      setContainerW(el.clientWidth)
      window.addEventListener('resize', onResize)
      return () => window.removeEventListener('resize', onResize)
    }
  }, [])

  const CARD_W = density === 'compact' ? 120 : density === 'relaxed' ? 180 : 145
  const H_GAP = density === 'compact' ? 14 : density === 'relaxed' ? 28 : 18
  const ROW_H = density === 'compact' ? 120 : density === 'relaxed' ? 160 : 130
  const PADDING = 16
  const DOT_R = 4
  const CARD_TOP = density === 'compact' ? 32 : density === 'relaxed' ? 28 : 24 // 卡片距圆点下方距离，紧凑模式增大避免碰撞

  const availW = Math.max(containerW - PADDING * 2, CARD_W)
  const ITEMS_PER_ROW = perRow && perRow >= 1 ? Math.min(perRow, items.length) : Math.max(1, Math.min(items.length, Math.floor(availW / (CARD_W + H_GAP))))

  const rows: TimelineItemWithColor[][] = []
  for (let i = 0; i < items.length; i += ITEMS_PER_ROW) rows.push(items.slice(i, i + ITEMS_PER_ROW))

  const maxItemsInRow = Math.max(...rows.map(r => r.length), 1)
  const totalContentW = maxItemsInRow * CARD_W + (maxItemsInRow - 1) * H_GAP
  const offsetX = Math.max(0, (availW - totalContentW) / 2) + PADDING
  const totalHeight = rows.length * ROW_H + PADDING * 2 + CARD_TOP + 80

  const dots: { x: number; y: number; item: TimelineItemWithColor; globalIdx: number }[] = []
  let globalIdx = 0
  const stepW = CARD_W + H_GAP // 单个步长（用于计算行宽）
  const maxRowWidth = (maxItemsInRow - 1) * stepW
  for (let r = 0; r < rows.length; r++) {
    const rowItems = rows[r]
    const isLTR = r % 2 === 0
    const step = rowItems.length > 1 ? stepW : 0
    // LTR 行始终从左开始（不足时右留空），RTL 行始终从右开始（不足时左留空）
    const startX = isLTR ? offsetX : offsetX + maxRowWidth
    const rowY = r * ROW_H + PADDING + CARD_TOP + DOT_R
    for (let c = 0; c < rowItems.length; c++) {
      const x = isLTR ? startX + c * step : startX - c * step
      dots.push({ x, y: rowY, item: rowItems[c], globalIdx: globalIdx++ })
    }
  }

  const pathD = (() => {
    if (dots.length === 0) return ''
    let d = `M ${dots[0].x} ${dots[0].y}`
    for (let i = 1; i < dots.length; i++) {
      const prev = dots[i - 1]
      const curr = dots[i]
      const prevRow = Math.floor((i - 1) / ITEMS_PER_ROW)
      const currRow = Math.floor(i / ITEMS_PER_ROW)
      if (prevRow !== currRow) {
        // 跨行：LTR→RTL 两端在右（弧向外右凸），RTL→LTR 两端在左（弧向外左凸）
        const midY = (prev.y + curr.y) / 2
        const avgX = (prev.x + curr.x) / 2
        // LTR→RTL 时两端在右，应向右侧外凸
        const bulgeRight = prevRow % 2 === 0
        const bulgeX = bulgeRight ? avgX + 60 : avgX - 60
        d += ` C ${bulgeX} ${midY - 20} ${bulgeX} ${midY + 20} ${curr.x} ${curr.y}`
      } else { d += ` L ${curr.x} ${curr.y}` }
    }
    return d
  })()

  return (
    <div ref={containerRef} className="relative" style={{ minHeight: totalHeight }}>
      <svg className="absolute inset-0 w-full h-full pointer-events-none overflow-visible" style={{ zIndex: 0 }}>
        <path d={pathD} fill="none" stroke={items[0]?.color ?? '#5470c6'} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" opacity="0.7" />
        {dots.map((d) => <circle key={d.globalIdx} cx={d.x} cy={d.y} r={DOT_R} fill={d.item.color} stroke="white" strokeWidth="2" />)}
      </svg>
      {dots.map(({ x, y, item, globalIdx }) => {
        const cardLeft = Math.max(0, x - CARD_W / 2)
        const SEG_H = 8 // 短线段高度（与横向布局 h-2.5 一致）
        return (
          <div key={globalIdx} className="absolute flex flex-col items-center"
            style={{ top: y, left: cardLeft, width: CARD_W, zIndex: 1 }}>
            {/* 连接线上段：圆点下方 8px */}
            <div className="w-px bg-gray-200 dark:bg-gray-700" style={{ height: SEG_H }} />
            {/* date+category 悬浮在连接线中间（连接线穿过标签中心） */}
            <span className="text-[10px] text-gray-500 dark:text-gray-400 whitespace-nowrap bg-white dark:bg-gray-900 px-1 leading-none relative z-10">
              {item.date}{item.category && <span className="text-gray-400 dark:text-gray-500 ml-1">{item.category}</span>}
            </span>
            {/* 连接线下段：标签下方到卡片 */}
            <div className="w-px bg-gray-200 dark:bg-gray-700" style={{ height: CARD_TOP - SEG_H }} />
            <div className="w-full">
              <TimelineCard item={item} showDate={false} />
            </div>
          </div>
        )
      })}
    </div>
  )
}

/** 鱼骨左：中央脊柱，items 倒序（时间从右往左），卡片在脊柱右侧，鱼刺从卡片朝左下方/左上方连接脊柱，箭头向左。date+category 在 rib 中点纯文本标签，卡片仅 title+description */
function FishboneLeftLayout({ items, density }: LayoutRendererProps) {
  const SPINE_Y = 180
  const RIB = density === 'compact' ? 75 : density === 'relaxed' ? 115 : 95
  const H_GAP = density === 'compact' ? 120 : density === 'relaxed' ? 180 : 150
  const CW = density === 'compact' ? 200 : density === 'relaxed' ? 320 : 260
  const CARD_H = 52
  const PAD = 40
  const reversed = useMemo(() => [...items].reverse(), [items])
  const tw = reversed.length * H_GAP + PAD * 2
  return (
    <div className="overflow-x-auto pb-2 -mx-2 px-2">
      <div className="relative" style={{ width: tw, height: SPINE_Y * 2 + 20 }}>
        <svg className="absolute inset-0 w-full h-full pointer-events-none" style={{ zIndex: 0 }}>
          <line x1={PAD} y1={SPINE_Y} x2={tw - PAD} y2={SPINE_Y} stroke={reversed[0]?.color ?? '#5470c6'} strokeWidth="3.5" strokeLinecap="round" />
          <polygon points={`${PAD},${SPINE_Y} ${PAD + 10},${SPINE_Y - 5} ${PAD + 10},${SPINE_Y + 5}`} fill={reversed[0]?.color ?? '#5470c6'} />
          {reversed.map((item, idx) => {
            const sx = PAD + idx * H_GAP + H_GAP / 2
            const isAbove = idx % 2 === 0
            const ry = isAbove ? SPINE_Y - RIB : SPINE_Y + RIB
            const tx = sx + RIB * 0.5
            const mx = (sx + tx) / 2
            const my = (SPINE_Y + ry) / 2
            return (
              <g key={idx}>
                <line x1={sx} y1={SPINE_Y} x2={tx} y2={ry} stroke={item.color} strokeWidth="1.8" strokeLinecap="round" opacity="0.7" />
                <circle cx={sx} cy={SPINE_Y} r="5" fill={item.color} stroke="white" strokeWidth="2" />
                <line x1={tx} y1={ry} x2={tx + 6} y2={ry} stroke={item.color} strokeWidth="1.2" opacity="0.5" />
                <text x={mx} y={my + 3} textAnchor="middle" fill="#6b7280" fontSize="10" fontWeight="500">
                  {item.date}{item.category ? ` ${item.category}` : ''}
                </text>
              </g>
            )
          })}
        </svg>
        {reversed.map((item, idx) => {
          const sx = PAD + idx * H_GAP + H_GAP / 2
          const isAbove = idx % 2 === 0
          const ry = isAbove ? SPINE_Y - RIB : SPINE_Y + RIB
          const tx = sx + RIB * 0.5
          const cl = tx - CW / 2
          const ct = isAbove ? ry - CARD_H : ry
          return <div key={idx} className="absolute" style={{ top: ct, left: cl, width: CW, zIndex: 1 }}><TimelineCard item={item} showDate={false} /></div>
        })}
      </div>
    </div>
  )
}

/** 鱼骨右：中央脊柱，items 顺序（时间从左往右），卡片在脊柱左侧，鱼刺从卡片朝右下方/右上方连接脊柱，箭头向右。date+category 在 rib 中点纯文本标签，卡片仅 title+description */
function FishboneRightLayout({ items, density }: LayoutRendererProps) {
  const SPINE_Y = 180
  const RIB = density === 'compact' ? 75 : density === 'relaxed' ? 115 : 95
  const H_GAP = density === 'compact' ? 120 : density === 'relaxed' ? 180 : 150
  const CW = density === 'compact' ? 200 : density === 'relaxed' ? 320 : 260
  const CARD_H = 52
  const PAD = density === 'compact' ? 90 : density === 'relaxed' ? 140 : 120
  const tw = items.length * H_GAP + PAD * 2
  return (
    <div className="overflow-x-auto pb-2 -mx-2 px-2">
      <div className="relative" style={{ width: tw, height: SPINE_Y * 2 + 20 }}>
        <svg className="absolute inset-0 w-full h-full pointer-events-none" style={{ zIndex: 0 }}>
          <line x1={PAD} y1={SPINE_Y} x2={tw - PAD} y2={SPINE_Y} stroke={items[0]?.color ?? '#5470c6'} strokeWidth="3.5" strokeLinecap="round" />
          <polygon points={`${tw - PAD},${SPINE_Y} ${tw - PAD - 10},${SPINE_Y - 5} ${tw - PAD - 10},${SPINE_Y + 5}`} fill={items[0]?.color ?? '#5470c6'} />
          {items.map((item, idx) => {
            const sx = PAD + idx * H_GAP + H_GAP / 2
            const isAbove = idx % 2 === 0
            const ry = isAbove ? SPINE_Y - RIB : SPINE_Y + RIB
            const tx = sx - RIB * 0.5
            const mx = (sx + tx) / 2
            const my = (SPINE_Y + ry) / 2
            return (
              <g key={idx}>
                <line x1={sx} y1={SPINE_Y} x2={tx} y2={ry} stroke={item.color} strokeWidth="1.8" strokeLinecap="round" opacity="0.7" />
                <circle cx={sx} cy={SPINE_Y} r="5" fill={item.color} stroke="white" strokeWidth="2" />
                <line x1={tx - 6} y1={ry} x2={tx} y2={ry} stroke={item.color} strokeWidth="1.2" opacity="0.5" />
                <text x={mx} y={my + 3} textAnchor="middle" fill="#6b7280" fontSize="10" fontWeight="500">
                  {item.date}{item.category ? ` ${item.category}` : ''}
                </text>
              </g>
            )
          })}
        </svg>
        {items.map((item, idx) => {
          const sx = PAD + idx * H_GAP + H_GAP / 2
          const isAbove = idx % 2 === 0
          const ry = isAbove ? SPINE_Y - RIB : SPINE_Y + RIB
          const tx = sx - RIB * 0.5
          const cl = tx - CW / 2
          const ct = isAbove ? ry - CARD_H : ry
          return <div key={idx} className="absolute" style={{ top: ct, left: cl, width: CW, zIndex: 1 }}><TimelineCard item={item} showDate={false} /></div>
        })}
      </div>
    </div>
  )
}

// ── 布局注册 ────────────────────────────────────────────────────────────────

const LAYOUT_RENDERERS: Record<TimelineLayout, React.FC<LayoutRendererProps>> = {
  vertical: VerticalLayout,
  'alternating-top': AlternatingTopLayout,
  'alternating-bottom': AlternatingBottomLayout,
  'horizontal-left': HorizontalLeftLayout,
  'horizontal-right': HorizontalRightLayout,
  's-curve': SCurveLayout,
  'fishbone-left': FishboneLeftLayout,
  'fishbone-right': FishboneRightLayout,
}

const LAYOUT_LABELS: Record<TimelineLayout, string> = {
  vertical: '纵向',
  'alternating-top': '交替上',
  'alternating-bottom': '交替下',
  'horizontal-left': '横向左',
  'horizontal-right': '横向右',
  's-curve': 'S形',
  'fishbone-left': '鱼骨左',
  'fishbone-right': '鱼骨右',
}

const ALL_LAYOUTS: TimelineLayout[] = [
  'vertical', 'alternating-top', 'alternating-bottom',
  'horizontal-left', 'horizontal-right',
  's-curve', 'fishbone-left', 'fishbone-right',
]

const DENSITY_OPTIONS = [
  { value: 'compact', label: '紧凑' },
  { value: 'normal', label: '默认' },
  { value: 'relaxed', label: '宽松' },
]

const PER_ROW_OPTIONS = [
  { value: '0', label: '自动' },
  { value: '2', label: '2个' },
  { value: '3', label: '3个' },
  { value: '4', label: '4个' },
  { value: '5', label: '5个' },
]

function resolveLayout(layout: TimelineLayout | undefined, itemCount: number): TimelineLayout {
  if (layout) return layout
  if (itemCount >= 12) return 'vertical'
  if (itemCount >= 8) return 'horizontal-left'
  if (itemCount >= 4) return 'alternating-bottom'
  return 'vertical'
}

// ── 主组件 ──────────────────────────────────────────────────────────────────

interface TimelineBlockProps {
  spec: TimelineSpec
  className?: string
}

export function TimelineBlock({ spec, className }: TimelineBlockProps) {
  const rootRef = useRef<HTMLDivElement>(null)
  const [imageCopied, setImageCopied] = useState(false)
  const [imageCopyErr, setImageCopyErr] = useState(false)
  const [imageSaved, setImageSaved] = useState(false)
  const [fallbackBlob, setFallbackBlob] = useState<Blob | null>(null)

  const [editedJson, setEditedJson] = useState('')
  const [dataOverrides, setDataOverrides] = useState<{ items?: TimelineItem[]; title?: string; palette?: string[] } | null>(null)

  const items = useMemo(() => {
    const source = dataOverrides?.items ?? spec.items
    const colors = dataOverrides?.palette ?? spec.palette ?? PALETTE
    return source.map((it, i) => ({ ...it, color: it.color ?? colors[i % colors.length] }))
  }, [spec.items, spec.palette, dataOverrides])

  const [layout, setLayout] = useState<TimelineLayout>(() => resolveLayout(spec.layout, items.length))
  const [density, setDensity] = useState(spec.density ?? 'normal')
  const [perRow, setPerRow] = useState(0) // 0=自动，>0=指定每行条目数
  const [codeMode, setCodeMode] = useState(false)
  const [fullscreen, setFullscreen] = useState(false)

  const rawJson = useMemo(() => {
    const obj: Record<string, unknown> = {
      timelineId: spec.timelineId,
      title: dataOverrides?.title ?? spec.title,
      items: dataOverrides?.items ?? spec.items,
    }
    if (dataOverrides?.palette ?? spec.palette) obj.palette = dataOverrides?.palette ?? spec.palette
    return JSON.stringify(obj, null, 2)
  }, [spec, dataOverrides])

  const layoutOptions = ALL_LAYOUTS.map((lt) => ({ value: lt, label: LAYOUT_LABELS[lt] }))

  const copyImage = useCallback(async () => {
    if (!rootRef.current) return
    try {
      const blob = await captureDomAsPng(rootRef.current)
      const ok = await copyImageOrFallback(blob)
      if (ok) { setImageCopied(true); setTimeout(() => setImageCopied(false), 1500) }
      else { setFallbackBlob(blob) }
    } catch { setImageCopyErr(true); setTimeout(() => setImageCopyErr(false), 2000) }
  }, [])

  const saveImage = useCallback(async () => {
    if (!rootRef.current) return
    try {
      const blob = await captureDomAsPng(rootRef.current)
      savePngBlob(blob, `${spec.title || 'timeline'}-${Date.now()}.png`)
      setImageSaved(true); setTimeout(() => setImageSaved(false), 1500)
    } catch { /* ignore */ }
  }, [spec.title])

  const iconBtnClass = 'flex items-center justify-center w-6 h-6 rounded transition-colors hover:bg-gray-100 dark:hover:bg-gray-700'

  if (items.length === 0) {
    return <div className={cn('rounded-xl border border-gray-200 dark:border-gray-700 p-4 text-sm text-gray-500', className)}>时间轴暂无数据</div>
  }

  const LayoutRenderer = LAYOUT_RENDERERS[layout]

  const titleBar = spec.title && (
    <div className="flex items-center justify-between px-5 pt-4 pb-3 border-b border-gray-100 dark:border-gray-800">
      <h3 className="text-sm font-semibold text-gray-800 dark:text-gray-100 shrink-0">{dataOverrides?.title ?? spec.title}</h3>
      <div className="flex items-center gap-1.5" data-no-capture>
        <Select options={layoutOptions} value={layout} onChange={(v) => setLayout(v as TimelineLayout)} className="w-[84px] [&_button]:text-[10px] [&_button]:px-1.5 [&_button]:py-1 [&_button]:min-h-0" />
        <Select options={DENSITY_OPTIONS} value={density} onChange={(v) => setDensity(v as 'compact' | 'normal' | 'relaxed')} className="w-[68px] [&_button]:text-[10px] [&_button]:px-1.5 [&_button]:py-1 [&_button]:min-h-0" />
        {layout === 's-curve' && (
          <Select options={PER_ROW_OPTIONS} value={String(perRow)} onChange={(v) => setPerRow(Number(v))} className="w-[64px] [&_button]:text-[10px] [&_button]:px-1.5 [&_button]:py-1 [&_button]:min-h-0" />
        )}
        <button type="button" onClick={() => {
          if (codeMode) {
            try {
              const parsed = JSON.parse(editedJson)
              if (parsed?.items && Array.isArray(parsed.items) && parsed.items.length > 0) {
                setDataOverrides({
                  items: parsed.items.map((it: Record<string, unknown>) => ({
                    date: String(it['date'] ?? ''), title: String(it['title'] ?? ''),
                    description: it['description'] ? String(it['description']) : undefined,
                    color: it['color'] ? String(it['color']) : undefined,
                    category: it['category'] ? String(it['category']) : undefined,
                  })),
                  title: parsed.title ? String(parsed.title) : undefined,
                  palette: Array.isArray(parsed.palette) ? parsed.palette : undefined,
                })
                if (parsed.layout && ALL_LAYOUTS.includes(parsed.layout)) setLayout(parsed.layout)
                if (parsed.density && ['compact', 'normal', 'relaxed'].includes(parsed.density)) setDensity(parsed.density as 'compact' | 'normal' | 'relaxed')
              }
            } catch { /* invalid JSON, keep old data */ }
          } else { setEditedJson(rawJson) }
          setCodeMode((v) => !v)
        }}
          title={codeMode ? '退出代码模式' : '代码模式'}
          className={cn(iconBtnClass, codeMode ? 'text-blue-600 dark:text-blue-400' : 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}>
          <Icon name="code" size="sm" />
        </button>
        <button type="button" onClick={() => setFullscreen(true)}
          title="全屏查看" className={cn(iconBtnClass, 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}>
          <Icon name="fullscreen" size="sm" />
        </button>
        <button type="button" onClick={copyImage}
          title={imageCopyErr ? '复制失败' : imageCopied ? '已复制' : '复制图片'}
          className={cn(iconBtnClass, imageCopyErr ? 'text-red-500' : 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}>
          <Icon name={imageCopyErr ? 'error' : imageCopied ? 'check' : 'content_copy'} size="sm" />
        </button>
        <button type="button" onClick={saveImage}
          title={imageSaved ? '已保存' : '另存为图片'}
          className={cn(iconBtnClass, 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}>
          <Icon name={imageSaved ? 'check' : 'save_alt'} size="sm" />
        </button>
      </div>
    </div>
  )

  const timelineBody = (
    <div ref={rootRef} data-testid="timeline-block"
      className={cn('rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 overflow-hidden', className)}>
      {titleBar}
      <div className="px-5 py-4">
        {codeMode ? (
          <div className="space-y-1">
            <textarea value={editedJson} onChange={(e) => setEditedJson(e.target.value)}
              className="w-full text-xs text-gray-600 dark:text-gray-400 font-mono whitespace-pre-wrap break-all bg-gray-50 dark:bg-gray-800/50 rounded-lg p-3 border border-gray-200 dark:border-gray-700 focus:outline-none focus:border-blue-400 dark:focus:border-blue-500 resize-y"
              style={{ minHeight: '200px', maxHeight: '500px' }} spellCheck={false} />
            <p className="text-[10px] text-gray-400 dark:text-gray-500 px-1">编辑 JSON 后再次点击 <Icon name="code" size="sm" className="inline" /> 退出以重新渲染</p>
          </div>
        ) : (
          <LayoutRenderer items={items} density={density} perRow={perRow > 0 ? perRow : undefined} />
        )}
      </div>
    </div>
  )

  return (
    <>
      {timelineBody}
      {fullscreen && createPortal(
        <div className="fixed inset-0 z-[9999] bg-white dark:bg-gray-900 flex flex-col">
          <div className="flex-none flex items-center justify-between px-5 py-3 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
            <h3 className="text-sm font-semibold text-gray-800 dark:text-gray-100">{dataOverrides?.title ?? spec.title}</h3>
            <div className="flex items-center gap-1" data-no-capture>
              <button type="button" onClick={copyImage}
                title={imageCopyErr ? '复制失败' : imageCopied ? '已复制' : '复制图片'}
                className={cn(iconBtnClass, imageCopyErr ? 'text-red-500' : 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}>
                <Icon name={imageCopyErr ? 'error' : imageCopied ? 'check' : 'content_copy'} size="sm" />
              </button>
              <button type="button" onClick={saveImage}
                title={imageSaved ? '已保存' : '另存为图片'}
                className={cn(iconBtnClass, 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}>
                <Icon name={imageSaved ? 'check' : 'save_alt'} size="sm" />
              </button>
              <button type="button" onClick={() => setFullscreen(false)}
                className={cn(iconBtnClass, 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')} title="退出全屏">
                <Icon name="close" size="sm" />
              </button>
            </div>
          </div>
          <div className="flex-1 overflow-auto px-5 py-4">
            {codeMode ? (
              <textarea value={editedJson} onChange={(e) => setEditedJson(e.target.value)}
                className="w-full h-full text-xs text-gray-600 dark:text-gray-400 font-mono whitespace-pre-wrap break-all bg-gray-50 dark:bg-gray-800/50 rounded-lg p-3 border border-gray-200 dark:border-gray-700 focus:outline-none resize-none" spellCheck={false} />
            ) : (
              <LayoutRenderer items={items} density={density} perRow={perRow > 0 ? perRow : undefined} />
            )}
          </div>
        </div>, document.body)}
      <MobileImageFallback open={fallbackBlob !== null} blob={fallbackBlob}
        onClose={() => setFallbackBlob(null)} filename={`${spec.title || 'timeline'}-${Date.now()}.png`} />
    </>
  )
}
