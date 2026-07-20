import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { ECharts, EChartsOption } from 'echarts'
import { cn } from '@/lib/utils'
import type { MindmapSpec } from './MindmapBlock'

// ── ECharts 懒加载（与 ChartBlock 共享同一模块缓存）──────────────────────────
let _echartsModule: typeof import('echarts') | null = null
let _echartsLoadPromise: Promise<typeof import('echarts')> | null = null

function loadECharts(): Promise<typeof import('echarts')> {
  if (_echartsModule) return Promise.resolve(_echartsModule)
  if (!_echartsLoadPromise)
    _echartsLoadPromise = import('echarts').then(m => { _echartsModule = m; return m })
  return _echartsLoadPromise
}

// ── 类型 ─────────────────────────────────────────────────────────────────────

/** 脑图布局模式 */
export type MindmapLayout = 'tree' | 'radial' | 'lr' | 'rl' | 'tb' | 'bt'

/** 布局标签 */
export const LAYOUT_LABELS: Record<MindmapLayout, string> = {
  tree: '缩进树',
  radial: '中心放射',
  lr: '左→右树',
  rl: '右→左树',
  tb: '上→下树',
  bt: '下→上树',
}

// ── 默认配色 ─────────────────────────────────────────────────────────────────

const DEFAULT_COLORS = [
  '#5470c6', '#91cc75', '#fac858', '#ee6666',
  '#73c0de', '#3ba272', '#fc8452', '#9a60b4',
]

/** 将 hex 颜色变亮/变暗（用于子节点淡色） */
function adjustColor(hex: string, amount: number): string {
  const num = parseInt(hex.slice(1), 16)
  const r = Math.min(255, Math.max(0, (num >> 16) + amount))
  const g = Math.min(255, Math.max(0, ((num >> 8) & 0x00FF) + amount))
  const b = Math.min(255, Math.max(0, (num & 0x0000FF) + amount))
  return `#${(1 << 24 | r << 16 | g << 8 | b).toString(16).slice(1)}`
}

// ── 数据转换（注入分支配色、线条、节点样式）───────────────────────────────────

/** 估算中文文本渲染宽度（px），用于动态 pill 宽度 */
function estLabelWidth(text: string, fontSize: number): number {
  let w = 0
  for (const ch of text) {
    w += ch.charCodeAt(0) > 127 ? fontSize : fontSize * 0.6
  }
  return w + 16 // padding
}

function toEChartsTreeData(spec: MindmapSpec, isDark: boolean, layout: MindmapLayout) {
  const colors = spec.branchColors && spec.branchColors.length > 0
    ? spec.branchColors
    : DEFAULT_COLORS

  const collapsedSet = spec.collapsed ? new Set(spec.collapsed) : undefined
  const rootFontSize = 14
  const rootW = Math.max(estLabelWidth(spec.rootLabel, rootFontSize), 72)

  // 叶子标签方向：lr→右，rl→左，tb/bt→右（水平延伸）
  const leafLabelPos = layout === 'rl' ? 'left' as const : 'right' as const
  const leafLabelAlign = layout === 'rl' ? 'right' as const : 'left' as const

  return {
    name: spec.rootLabel,
    // 根节点：圆角 pill，品牌色填充白字，显式 inside 防折叠后飘移
    symbol: 'roundRect',
    symbolSize: [rootW, 34],
    itemStyle: {
      color: isDark ? '#6366f1' : '#4f46e5',
      borderRadius: 17,
      shadowBlur: 6,
      shadowColor: 'rgba(0,0,0,0.18)',
      shadowOffsetX: 2,
      shadowOffsetY: 2,
    },
    label: {
      position: 'inside' as const,
      align: 'center' as const,
      verticalAlign: 'middle' as const,
      color: '#fff',
      fontSize: rootFontSize,
      fontWeight: 'bold' as const,
    },
    children: spec.branches.map((branch, idx) => {
      const color = colors[idx % colors.length]
      const lightColor = adjustColor(color, 50)
      const branchFontSize = 12
      const branchW = Math.max(estLabelWidth(branch.label, branchFontSize), 48)

      return {
        name: branch.label,
        collapsed: collapsedSet?.has(branch.id) || undefined,
        // 一级分支：实心 pill 节点，分支配色，显式 inside 防折叠后飘移
        symbol: 'roundRect',
        symbolSize: [branchW, 28],
        itemStyle: {
          color,
          borderRadius: 14,
          shadowBlur: 3,
          shadowColor: color + '50',
          shadowOffsetX: 1,
          shadowOffsetY: 2,
        },
        label: {
          position: 'inside' as const,
          align: 'center' as const,
          verticalAlign: 'middle' as const,
          color: '#fff',
          fontSize: branchFontSize,
          fontWeight: 'bold' as const,
        },
        // 彩色贝塞尔曲线（高弧度，圆滑连接）
        lineStyle: {
          color,
          width: 3,
          curveness: 0.55,
        },
        children: branch.children.map(child => ({
          name: child.label,
          collapsed: collapsedSet?.has(child.id) || undefined,
          // 二级叶子：淡色实心小圆 + 标签按布局方向延伸
          symbol: 'circle',
          symbolSize: 7,
          itemStyle: {
            color: lightColor,
            borderColor: lightColor,
            borderWidth: 1.5,
            shadowBlur: 2,
            shadowColor: lightColor + '30',
          },
          label: {
            position: leafLabelPos,
            align: leafLabelAlign,
            color: isDark ? '#e5e7eb' : '#374151',
            fontSize: 11,
            backgroundColor: isDark ? '#1f2937cc' : '#ffffffcc',
            borderRadius: 4,
            padding: [2, 6, 2, 6],
          },
          lineStyle: {
            color: lightColor,
            width: 1.8,
            curveness: 0.55,
          },
        })),
      }
    }),
  }
}

// ── ECharts 配置 ─────────────────────────────────────────────────────────────

function buildOption(
  spec: MindmapSpec,
  layout: MindmapLayout,
  isDark: boolean,
  leftMargin: number,
  rightMargin: number,
  topMargin: number,
  bottomMargin: number,
): EChartsOption {
  const isRadial = layout === 'radial'
  const initialDepth = spec.maxDepth != null ? spec.maxDepth : -1

  // 正交布局方向映射
  const orient = layout === 'lr' ? 'LR' as const
    : layout === 'rl' ? 'RL' as const
    : layout === 'tb' ? 'TB' as const
    : layout === 'bt' ? 'BT' as const
    : undefined

  const textColor = isDark ? '#d1d5db' : '#374151'
  const bgColor = isDark ? '#111827' : '#fafafa'

  // 叶子标签延伸方向：lr/tb/bt→右，rl→左
  const leafLabelPos = layout === 'rl' ? 'left' as const : 'right' as const
  const leafLabelAlign = layout === 'rl' ? 'right' as const : 'left' as const

  return {
    backgroundColor: bgColor,
    tooltip: {
      trigger: 'item',
      formatter: (params: unknown) => (params as { name?: string }).name ?? '',
    },
    series: [
      {
        type: 'tree',
        data: [toEChartsTreeData(spec, isDark, layout)],
        layout: isRadial ? 'radial' : 'orthogonal',
        orient: isRadial ? undefined : (orient ?? 'LR'),
        roam: true,
        expandAndCollapse: true,
        initialTreeDepth: initialDepth,
        // 全局默认节点
        symbol: 'circle',
        symbolSize: 7,
        // 有机贝塞尔曲线，高弧度
        edgeShape: 'curve',
        edgeForkPosition: '55%',
        top: topMargin,
        left: isRadial ? 8 : leftMargin,
        bottom: bottomMargin,
        right: isRadial ? 8 : rightMargin,
        // 非叶节点（根 + 一级分支）：标签内置在 pill 矩形中
        label: {
          position: isRadial ? undefined : 'inside',
          verticalAlign: 'middle',
          align: 'center',
          fontSize: 12,
          color: '#fff',
        },
        // 叶子节点标签：按布局方向延伸（lr→右，rl→左，tb/bt→右）
        leaves: {
          label: {
            position: isRadial ? undefined : leafLabelPos,
            verticalAlign: 'middle',
            align: isRadial ? undefined : leafLabelAlign,
            fontSize: 11,
            color: textColor,
            overflow: 'break',
            width: 200,
          },
        },
        // 连线默认样式（被 data 中 per-node lineStyle 覆盖）
        lineStyle: {
          color: isDark ? '#6b7280' : '#cbd5e1',
          width: 2,
          curveness: 0.55,
        },
        itemStyle: {
          borderWidth: 0,
        },
        emphasis: {
          focus: 'descendant',
          lineStyle: {
            width: 3.5,
          },
          itemStyle: {
            shadowBlur: 10,
            shadowColor: 'rgba(0,0,0,0.2)',
          },
        },
      },
    ],
  }
}

// ── 动态尺寸 ─────────────────────────────────────────────────────────────────

/** 根据节点总数和布局方向估算合适高度 */
function estimateHeight(spec: MindmapSpec, layout: MindmapLayout, _containerWidth: number): number {
  const branchCount = spec.branches.length
  const leafCount = spec.branches.reduce((sum, b) => sum + b.children.length, 0)
  const totalNodes = branchCount + leafCount
  // tb/bt 垂直布局需要更大高度容纳各层级
  const isVertical = layout === 'tb' || layout === 'bt'
  if (isVertical) {
    // 垂直布局：每层 ~150px 间距 + 根/叶留白
    const levels = spec.branches.some(b => b.children.length > 0) ? 3 : 2
    const calc = levels * 155 + 80
    return Math.max(420, Math.min(1200, calc))
  }
  // 水平布局：每节点约 32px 行高，至少给叶子节点 28px 间距
  const maxChildren = Math.max(0, ...spec.branches.map(b => b.children.length))
  const perNode = maxChildren > 3 ? 30 : (layout === 'radial' ? 40 : 36)
  const calc = totalNodes * perNode + 50
  return Math.max(260, Math.min(900, calc))
}

/** 根据容器尺寸、根节点文字和布局方向计算四边距。
 *  水平布局（lr/rl）：用 right 边距吸收多余宽度，将层级间距压缩到 ~170px。
 *  垂直布局（tb/bt）：用 bottom 边距吸收多余高度，并确保同级节点水平不重叠。 */
function computeMargins(
  spec: MindmapSpec,
  layout: MindmapLayout,
  containerWidth: number,
  containerHeight: number,
): { left: number; right: number; top: number; bottom: number } {
  const rootW = Math.max(estLabelWidth(spec.rootLabel, 14), 72)
  const pillHalf = Math.ceil(rootW / 2) + 12
  const labelSpace = 228 // 叶子标签 width=200 + 安全间距
  const hasLeaves = spec.branches.some(b => b.children.length > 0)
  const levels = hasLeaves ? 2 : 1

  if (layout === 'rl') {
    // 根在右：left 吸收多余宽度以压缩层级
    const left = Math.max(labelSpace, containerWidth - pillHalf - levels * 170)
    return { left, right: pillHalf, top: 8, bottom: 8 }
  }
  if (layout === 'lr') {
    // 根在左：right 吸收多余宽度以压缩层级
    const right = Math.max(labelSpace, containerWidth - pillHalf - levels * 170)
    return { left: pillHalf, right, top: 8, bottom: 8 }
  }
  if (layout === 'radial') {
    // 放射布局：四边均等
    const m = Math.max(pillHalf, labelSpace, 60)
    return { left: m, right: m, top: m, bottom: m }
  }
  // ── tb / bt 垂直布局 ──
  // 同级节点水平防碰撞：计算同级最大节点数，确保足够宽度
  const maxSiblings = Math.max(
    spec.branches.length,
    ...spec.branches.map(b => b.children.length),
  )
  const minNodeWidth = 130 // 每个节点（pill+间隔）最小水平空间
  const neededH = maxSiblings * minNodeWidth + 40
  const hMargin = neededH < containerWidth
    ? Math.max(12, (containerWidth - neededH) / 2)
    : 8 // 容器不够宽时压到最小
  // 垂直层级间距目标 ~140px
  const vertLevels = levels + 1 // 根+一级+二级共 level+1 层
  if (layout === 'tb') {
    const bottom = Math.max(80, containerHeight - 28 - vertLevels * 140)
    return { left: hMargin, right: hMargin, top: 28, bottom }
  }
  // bt
  const top = Math.max(80, containerHeight - 28 - vertLevels * 140)
  return { left: hMargin, right: hMargin, top, bottom: 28 }
}

// ── 组件 ─────────────────────────────────────────────────────────────────────

interface MindmapEChartsProps {
  spec: MindmapSpec
  layout: MindmapLayout
  className?: string
  onChartReady?: (chart: ECharts) => void
}

export function MindmapECharts({ spec, layout, className, onChartReady }: MindmapEChartsProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const chartRef = useRef<ECharts | null>(null)
  const lastLayoutRef = useRef<MindmapLayout | null>(null)
  const onChartReadyRef = useRef(onChartReady)
  onChartReadyRef.current = onChartReady

  // 追踪容器实际尺寸 + ECharts resize
  const [containerSize, setContainerSize] = useState<{ w: number; h: number }>({ w: 700, h: 400 })
  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    // 低版本浏览器 fallback：ResizeObserver 不可用时降级为 window.resize
    let ro: ResizeObserver | null = null
    try {
      ro = new ResizeObserver(entries => {
        const rect = entries[0]?.contentRect
        if (rect && rect.width > 0) {
          setContainerSize(prev => {
            if (Math.abs(prev.w - rect.width) > 2 || Math.abs(prev.h - rect.height) > 2)
              return { w: rect.width, h: rect.height }
            return prev
          })
        }
        chartRef.current?.resize()
      })
      ro.observe(el)
    } catch {
      const onResize = () => chartRef.current?.resize()
      window.addEventListener('resize', onResize)
      return () => window.removeEventListener('resize', onResize)
    }
    // 初始化时立即读取
    const rect = el.getBoundingClientRect()
    if (rect.width > 0) setContainerSize({ w: rect.width, h: rect.height })
    return () => ro?.disconnect()
  }, [])

  const { w: cw, h: ch } = containerSize
  const height = useMemo(() => estimateHeight(spec, layout, cw), [spec.branches, layout, cw])
  const margins = useMemo(
    () => computeMargins(spec, layout, cw, ch),
    [spec.rootLabel, spec.branches, layout, cw, ch],
  )

  // 初始化 / 重建 ECharts
  useEffect(() => {
    if (!containerRef.current) return
    const isDark = document.documentElement.classList.contains('dark')

    if (lastLayoutRef.current === layout && chartRef.current) {
      try {
        chartRef.current.setOption(buildOption(spec, layout, isDark, margins.left, margins.right, margins.top, margins.bottom))
      } catch {
        /* ignore */
      }
      return
    }

    if (chartRef.current) {
      chartRef.current.dispose()
      chartRef.current = null
    }

    let cancelled = false
    loadECharts().then((echarts) => {
      if (cancelled || !containerRef.current) return
      try {
        const chart = echarts.init(containerRef.current, isDark ? 'dark' : undefined, {
          renderer: 'canvas',
          locale: 'ZH',
        })
        if (cancelled) { chart.dispose(); return }
        chartRef.current = chart
        chart.setOption(buildOption(spec, layout, isDark, margins.left, margins.right, margins.top, margins.bottom))
        lastLayoutRef.current = layout
        onChartReadyRef.current?.(chart)
      } catch {
        /* ignore */
      }
    })

    return () => { cancelled = true }
  }, [spec, layout, margins])

  // 卸载清理
  useEffect(() => {
    return () => {
      chartRef.current?.dispose()
      chartRef.current = null
      lastLayoutRef.current = null
    }
  }, [])

  // 暗色模式跟随
  useEffect(() => {
    const chart = chartRef.current
    if (!chart) return
    const observer = new MutationObserver(() => {
      if (!chartRef.current || lastLayoutRef.current !== layout) return
      const dark = document.documentElement.classList.contains('dark')
      try {
        chartRef.current.setOption(buildOption(spec, layout, dark, margins.left, margins.right, margins.top, margins.bottom))
      } catch {
        /* ignore */
      }
    })
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
    return () => observer.disconnect()
  }, [spec, layout, margins])

  // 图片导出
  const getBlob = useCallback(async (): Promise<Blob> => {
    const chart = chartRef.current
    if (!chart) throw new Error('chart not ready')
    const dataUrl = chart.getDataURL({ type: 'png', pixelRatio: 2, backgroundColor: '#fff' })
    const res = await fetch(dataUrl)
    return res.blob()
  }, [])

  useEffect(() => {
    const el = containerRef.current
    if (el) {
      (el as HTMLDivElement & { __mindmapGetBlob?: () => Promise<Blob> }).__mindmapGetBlob = getBlob
    }
  }, [getBlob])

  return (
    <div
      ref={containerRef}
      className={cn('w-full', className)}
      style={{ height: `${height}px` }}
    />
  )
}
