/**
 * ChartBlock — 基于 ECharts 的交互式图表渲染组件
 *
 * 与 show_chart 工具配套：后端只输出 JSON 规范（~200 tokens），
 * 前端 ECharts 即时渲染，比 show_widget 生成完整 HTML 快约 20 倍。
 *
 * 支持：bar / line / pie / scatter / radar / map_china
 *       heatmap / gauge / funnel / treemap
 */

import { useCallback, useEffect, useRef, useState } from 'react'
import type { ECharts, EChartsOption } from 'echarts'
import { cn } from '@/lib/utils'

// ── ECharts 懒加载（首屏不加载 ~1MB 的图表库，首次渲染图表时才拉取）───────────
let _echartsModule: typeof import('echarts') | null = null
let _echartsLoadPromise: Promise<typeof import('echarts')> | null = null

function loadECharts(): Promise<typeof import('echarts')> {
  if (_echartsModule) return Promise.resolve(_echartsModule)
  if (!_echartsLoadPromise)
    _echartsLoadPromise = import('echarts').then(m => { _echartsModule = m; return m })
  return _echartsLoadPromise
}
import { copyImageOrFallback, savePngBlob } from '@/utils/imageCapture'
import { MobileImageFallback } from '@/components/atoms/MobileImageFallback'
import { Icon } from '@/components/common/Icon'

// ── China GeoJSON 懒加载（首次渲染中国地图时触发，后续复用缓存）───────────────
let _chinaGeoPromise: Promise<void> | null = null

/** 中国地图 GeoJSON 数据源 URL 列表（主备切换）*/
const CHINA_GEO_URLS = [
  // 主方案：本地文件（已备份到 public/geo/china.json）
  '/geo/china.json',
  // 备选1：阿里云新版省级边界（FeatureCollection，含省份）
  'https://geo.datav.aliyun.com/areas_v3/bound/100000_full.json',
  // 备选2：阿里云旧版省级边界（FeatureCollection，含省份）
  'https://geo.datav.aliyun.com/areas/bound/100000_full.json',
  // 备选3：阿里云 geojson 接口（code=100000_full）
  'https://geo.datav.aliyun.com/areas_v3/bound/geojson?code=100000_full',
]

function ensureChinaMap(): Promise<void> {
  if (_chinaGeoPromise) return _chinaGeoPromise
  
  // 尝试多个数据源，直到有一个成功
  _chinaGeoPromise = (async () => {
    let lastError: Error | null = null
    for (const url of CHINA_GEO_URLS) {
      try {
        const response = await fetch(url, { signal: AbortSignal.timeout(10000) })
        if (!response.ok) {
          lastError = new Error(`HTTP ${response.status} from ${url}`)
          continue
        }
        const geo = await response.json()
        const echarts = await loadECharts()
        echarts.registerMap('china', geo as Parameters<typeof echarts.registerMap>[1])
        console.debug(`[ChartBlock] 中国地图数据源加载成功：${url}`)
        return
      } catch (e) {
        lastError = e instanceof Error ? e : new Error(String(e))
        console.debug(`[ChartBlock] 地图数据源失败（${url}）：${lastError.message}，尝试下一个...`)
        continue
      }
    }
    // 所有源都失败
    _chinaGeoPromise = null   // 允许重试
    throw lastError || new Error('无法加载中国地图数据（所有数据源均不可用）')
  })()
  
  return _chinaGeoPromise
}

// ── 类型定义 ─────────────────────────────────────────────────────────────────

export type ChartType =
  | 'bar' | 'line' | 'pie' | 'scatter' | 'radar'
  | 'map_china' | 'heatmap' | 'gauge' | 'funnel' | 'treemap'

const CHART_TYPES: ChartType[] = [
  'bar', 'line', 'pie', 'scatter', 'radar',
  'map_china', 'heatmap', 'gauge', 'funnel', 'treemap',
]

/** show_chart 工具返回结果的解析形式 */
export interface ChartSpec {
  chartId: string
  type: ChartType
  title: string
  data: {
    /** bar/line 横轴类目；heatmap 横轴类目 */
    xAxis?: string[] | { name?: string }
    /** bar/line 纵轴配置；scatter 纵轴名 */
    yAxis?: { name?: string; min?: number; max?: number }
    /** heatmap 纵轴类目（区别于 yAxis.name） */
    yAxisCategories?: string[]
    /** radar 雷达维度定义 */
    indicators?: Array<{ name: string; max?: number }>
    /** 数据系列 */
    series: Array<{
      name?: string
      /** bar/line: number[]；pie/funnel/treemap: {name,value}[]；scatter: [x,y][]；
       *  radar: number[]；map_china: {name,value}[]；heatmap: [xi,yi,value][]；
       *  gauge: {name,value}[]；  */
      data: unknown
      smooth?: boolean
      area?: boolean
      stack?: string
    }>
    unit?: string
    legend?: boolean
    /** 覆盖默认配色 */
    colors?: string[]
  }
}

interface ChartBlockProps {
  spec: ChartSpec
  className?: string
  /** 分享页等只读场景下隐藏悬浮复制/保存按钒 */
  hideActions?: boolean
}

// ── 解析工具结果 ──────────────────────────────────────────────────────────────

function isChartType(value: unknown): value is ChartType {
  return typeof value === 'string' && CHART_TYPES.includes(value as ChartType)
}

function asObject(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null
}

function normalizeChartData(type: ChartType, title: string, rawData: unknown): ChartSpec['data'] | null {
  let parsedData = rawData
  if (typeof parsedData === 'string') {
    try {
      parsedData = JSON.parse(parsedData)
    } catch {
      return null
    }
  }

  const wrapSeries = (seriesData: unknown, seriesName?: string, extra?: Record<string, unknown>): ChartSpec['data'] => ({
    ...(extra ?? {}),
    series: [{
      name: (seriesName ?? title) || '数据',
      data: seriesData,
    }],
  }) as ChartSpec['data']

  if (Array.isArray(parsedData)) {
    switch (type) {
      case 'pie':
      case 'funnel':
      case 'treemap':
      case 'gauge':
        return wrapSeries(parsedData)
      default:
        return null
    }
  }

  const dataObj = asObject(parsedData)
  if (!dataObj) return null
  if (Array.isArray(dataObj.series)) return dataObj as unknown as ChartSpec['data']

  switch (type) {
    case 'pie':
    case 'funnel':
    case 'treemap':
    case 'gauge': {
      if (Array.isArray(dataObj.data)) {
        const { data: seriesData, name, ...rest } = dataObj
        return wrapSeries(
          seriesData,
          typeof name === 'string' ? name : undefined,
          rest,
        )
      }

      if (type === 'gauge' && typeof dataObj.value === 'number') {
        return wrapSeries([
          {
            name: typeof dataObj.name === 'string' ? dataObj.name : title || '数值',
            value: dataObj.value,
          },
        ])
      }

      break
    }
  }

  return dataObj as unknown as ChartSpec['data']
}

export function parseChartData(result?: string): ChartSpec | null {
  if (!result) return null
  try {
    const parsed = JSON.parse(result) as Omit<ChartSpec, 'type' | 'data'> & { type?: unknown; data?: unknown }
    if (!parsed.chartId || !isChartType(parsed.type) || parsed.data === undefined) return null

    const normalizedData = normalizeChartData(parsed.type, parsed.title ?? '', parsed.data)
    if (!normalizedData) return null

    return {
      chartId: parsed.chartId,
      type: parsed.type,
      title: parsed.title ?? '',
      data: normalizedData,
    }
  } catch {
    return null
  }
}

// ── ECharts 配置构建 ──────────────────────────────────────────────────────────

const DEFAULT_COLORS = [
  '#5470c6', '#91cc75', '#fac858', '#ee6666', '#73c0de',
  '#3ba272', '#fc8452', '#9a60b4', '#ea7ccc', '#27727b',
]

function buildOption(spec: ChartSpec, isDark: boolean): EChartsOption {
  const { type, title, data } = spec
  const {
    series: rawSeries = [],
    xAxis: rawXAxis,
    yAxis: rawYAxis,
    yAxisCategories,
    unit,
    legend,
    colors,
    indicators,
    // LLM 有时用 indicator（单数）代替 indicators
    indicator: indicatorAlt,
    // radar 图 LLM 可能输出 categories + maxValues 格式（未经后端归一化时兜底）
    categories: rawCategories,
    maxValues: rawMaxValues,
    // LLM treemap 有时用 tree 字段代替 series[0].data
    tree: rawTree,
  } = data as typeof data & { indicator?: typeof indicators; categories?: unknown[]; maxValues?: unknown[]; tree?: unknown[] }

  const palette = colors ?? DEFAULT_COLORS
  const textColor  = isDark ? '#e5e7eb' : '#374151'
  const mutedColor = isDark ? '#9ca3af' : '#6b7280'
  const lineColor  = isDark ? '#374151' : '#e5e7eb'

  const showLegend = legend !== false && rawSeries.length > 1

  // 标题
  const titleCfg: EChartsOption['title'] = title
    ? { text: title, left: 'center', top: 4, textStyle: { fontSize: 14, fontWeight: 'normal', color: textColor } }
    : undefined

  // 工具笱已移除，改用悬浮图标按钒
  const toolboxCfg: EChartsOption['toolbox'] = undefined

  // 图例
  const legendCfg: EChartsOption['legend'] = showLegend
    ? { bottom: 4, textStyle: { color: mutedColor } }
    : undefined

  // 基础网格
  const gridCfg = {
    top:    title ? 52 : 20,
    bottom: showLegend ? 36 : 16,
    left: 16, right: 16,
    containLabel: true,
  }

  // 轴公共样式
  const axisStyle = {
    axisLabel:  { color: mutedColor },
    axisLine:   { lineStyle: { color: lineColor } },
    splitLine:  { lineStyle: { color: lineColor } },
    axisTick:   { lineStyle: { color: lineColor } },
  }

  // 通用 tooltip formatter（带单位）
  const unitSuffix = unit ? ` ${unit}` : ''

  // ── 各图表类型 ────────────────────────────────────────────────────────────

  switch (type) {

    // ─ 柱状图 / 折线图 ──────────────────────────────────────────────────────
    case 'bar':
    case 'line': {
      const isLine = type === 'line'
      const xCategories = Array.isArray(rawXAxis) ? rawXAxis : []
      const yName = (rawYAxis as { name?: string } | undefined)?.name ?? (unit ? `(${unit})` : '')
      return {
        color: palette, backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg, legend: legendCfg, grid: gridCfg,
        tooltip: { trigger: 'axis' },
        xAxis: { type: 'category', data: xCategories, ...axisStyle },
        yAxis: {
          type: 'value', name: yName,
          min: (rawYAxis as { min?: number } | undefined)?.min,
          max: (rawYAxis as { max?: number } | undefined)?.max,
          nameTextStyle: { color: mutedColor },
          ...axisStyle,
        },
        series: rawSeries.map(s => ({
          type: isLine ? ('line' as const) : ('bar' as const),
          name: s.name,
          data: s.data as number[],
          smooth: isLine ? (s.smooth ?? true) : undefined,
          areaStyle: isLine && s.area ? { opacity: 0.25 } : undefined,
          stack: s.stack,
        })),
      }
    }

    // ─ 饼图 / 环形图 ────────────────────────────────────────────────────────
    case 'pie': {
      const pieCategories = Array.isArray(rawXAxis) ? rawXAxis as string[] : []
      // LLM 有时用 xAxis + 纯数值 series.data，有时直接输出 {name,value} 对象数组，均兼容
      const normalizePieData = (
        d: unknown[],
        cats: string[],
      ): Array<{ name: string; value: number }> =>
        d.map((item, i) =>
          typeof item === 'object' && item !== null
            ? (item as { name: string; value: number })
            : { name: cats[i] ?? `item${i}`, value: item as number },
        )

      // 兜底：多 series 且无 xAxis，且每个 series.data 是等长数值数组时，合并为单 series
      // LLM 可能把类别名写成 series[].name，每个 series.data 是相同的完整数值数组
      let mergedSeries = rawSeries
      if (
        rawSeries.length > 1 &&
        pieCategories.length === 0 &&
        rawSeries.every(s => Array.isArray(s.data) && (s.data as unknown[]).every(v => typeof v === 'number'))
      ) {
        const lens = rawSeries.map(s => (s.data as number[]).length)
        const allSameLen = lens.every(l => l === lens[0])
        // series 数量 == 每个 data 数组长度时，取 series[i].data[i] 合并
        if (allSameLen && rawSeries.length === lens[0]) {
          const merged = rawSeries.map((s, i) => ({
            name: s.name ?? `item${i}`,
            value: (s.data as number[])[i],
          }))
          mergedSeries = [{ name: title || '数据', data: merged }]
        }
      }

      return {
        color: palette, backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg,
        // 饼图始终显示图例（类目标签来自数据，不受系列数量限制）
        legend: { type: 'scroll' as const, bottom: 4, textStyle: { color: mutedColor } },
        tooltip: {
          trigger: 'item' as const,
          formatter: `{b}: {c}${unitSuffix} ({d}%)`,
        },
        series: mergedSeries.map(s => ({
          type: 'pie' as const,
          name: s.name,
          radius: ['35%', '62%'],
          center: ['50%', title ? '54%' : '50%'],
          data: normalizePieData(s.data as unknown[], pieCategories),
          emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.3)' } },
          label: { color: textColor },
          labelLine: { lineStyle: { color: mutedColor } },
        })),
      }
    }

    // ─ 散点图 ───────────────────────────────────────────────────────────────
    case 'scatter': {
      const xName = Array.isArray(rawXAxis)
        ? (rawXAxis[0] ?? '')
        : (rawXAxis as { name?: string } | undefined)?.name ?? ''
      const yName = (rawYAxis as { name?: string } | undefined)?.name ?? ''
      return {
        color: palette, backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg, legend: legendCfg, grid: gridCfg,
        tooltip: {
          trigger: 'item' as const,
          formatter: (p: unknown) => {
            const pt = p as { seriesName: string; value: [number, number] }
            return `${pt.seriesName}: (${pt.value[0]}, ${pt.value[1]})`
          },
        },
        xAxis: { type: 'value' as const, name: xName, nameLocation: 'end', ...axisStyle },
        yAxis: { type: 'value' as const, name: yName, nameLocation: 'end', ...axisStyle },
        series: rawSeries.map(s => ({
          type: 'scatter' as const,
          name: s.name,
          data: s.data as number[][],
          symbolSize: 8,
        })),
      }
    }

    // ─ 雷达图 ───────────────────────────────────────────────────────────────
    case 'radar': {
      // 兼容 indicator（LLM 常用单数）和 indicators（类型定义）两种字段名
      let radarIndicators = indicators ?? indicatorAlt ?? []

      // 前端兜底：若后端未归一化 categories + maxValues → indicators，前端补齐
      if (radarIndicators.length === 0 && Array.isArray(rawCategories) && rawCategories.length > 0) {
        const maxVals = Array.isArray(rawMaxValues) ? rawMaxValues as number[] : []
        radarIndicators = rawCategories.map((cat, i) => {
          const name = typeof cat === 'object' && cat !== null && 'name' in cat
            ? (cat as { name: string }).name
            : String(cat ?? '')
          return { name, max: maxVals[i] }
        })
      }

      const indLen = radarIndicators.length
      return {
        color: palette, backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg, legend: legendCfg,
        tooltip: { trigger: 'item' as const },
        radar: {
          indicator: radarIndicators.map(ind => ({
            name: ind.name,
            max: ind.max ?? 100,
          })),
          axisName: { color: textColor },
          splitLine: { lineStyle: { color: lineColor } },
          splitArea: { areaStyle: { color: isDark ? ['#1f2937', '#111827'] : ['#f9fafb', '#ffffff'] } },
        },
        series: rawSeries.map(s => {
          // LLM 有时将数据包为 [[v1,v2,...]] 格式，取内层数组
          const rawVal = s.data as number[] | number[][]
          let value = Array.isArray(rawVal[0]) ? (rawVal as number[][])[0] : (rawVal as number[])
          // 对齐长度：补零或截断，防止 indicators/data 不一致时 ECharts 崩溃（最后防线）
          if (indLen > 0 && value.length !== indLen)
            value = Array.from({ length: indLen }, (_, i) => value[i] ?? 0)
          return {
            type: 'radar' as const,
            name: s.name,
            data: [{ value, name: s.name }],
            areaStyle: { opacity: 0.2 },
            lineStyle: { width: 2 },
          }
        }),
      }
    }

    // ─ 中国省份热力地图 ──────────────────────────────────────────────────────
    case 'map_china': {
      const rawMapData = rawSeries[0]?.data as Array<{ name: string; value: number }> ?? []
      // 应用省名全名映射，防止山东→山东省等短名与 GeoJSON 不匹配导致 NaN
      const mapData = rawMapData.map(d => ({ ...d, name: normalizeProvinceName(d.name) }))
      const values  = mapData.map(d => d.value)
      const minVal  = values.length ? Math.min(...values) : 0
      const maxVal  = values.length ? Math.max(...values) : 100
      return {
        backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg,
        tooltip: {
          trigger: 'item' as const,
          formatter: `{b}: {c}${unitSuffix}`,
        },
        visualMap: {
          min: minVal, max: maxVal,
          text: [`高${unitSuffix}`, `低${unitSuffix}`],
          textStyle: { color: mutedColor },
          realtime: false, calculable: true,
          inRange: { color: ['#e0f3f8', '#abd9e9', '#74add1', '#4575b4', '#313695'] },
          bottom: 20, right: 10,
        },
        series: [{
          type: 'map' as const,
          name: rawSeries[0]?.name ?? '',
          map: 'china',
          roam: true,
          data: mapData,
          emphasis: {
            label: { show: true, color: '#fff' },
            itemStyle: { areaColor: '#f4e925' },
          },
          label: { show: false, color: textColor, fontSize: 10 },
          itemStyle: {
            areaColor: isDark ? '#1e3a5f' : '#dbeafe',
            borderColor: isDark ? '#374151' : '#bfdbfe',
            borderWidth: 0.5,
          },
          select: { disabled: false },
        }],
      }
    }

    // ─ 矩阵热力图 ────────────────────────────────────────────────────────────
    case 'heatmap': {
      const heatData = rawSeries[0]?.data as number[][] ?? []
      const vals     = heatData.map(d => d[2] ?? 0)
      const xCats    = Array.isArray(rawXAxis) ? rawXAxis : []
      const yCats    = yAxisCategories ?? []
      return {
        backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg, grid: gridCfg,
        tooltip: {
          position: 'top' as const,
          formatter: (p: unknown) => {
            const pt = p as { value: [number, number, number] }
            const x  = xCats[pt.value[0]] ?? pt.value[0]
            const y  = yCats[pt.value[1]] ?? pt.value[1]
            return `${x} / ${y}: <b>${pt.value[2]}</b>${unitSuffix}`
          },
        },
        xAxis: { type: 'category' as const, data: xCats, splitArea: { show: true }, ...axisStyle },
        yAxis: { type: 'category' as const, data: yCats, splitArea: { show: true }, ...axisStyle },
        visualMap: {
          min: vals.length ? Math.min(...vals) : 0,
          max: vals.length ? Math.max(...vals) : 100,
          calculable: true, orient: 'horizontal' as const,
          left: 'center', bottom: showLegend ? 36 : 4,
        },
        series: [{
          type: 'heatmap' as const,
          name: rawSeries[0]?.name,
          data: heatData,
          label: { show: heatData.length <= 100 },
          emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.5)' } },
        }],
      }
    }

    // ─ 仪表盘 ───────────────────────────────────────────────────────────────
    case 'gauge': {
      type GaugeItem = { name: string; value: number }
      const raw  = rawSeries[0]?.data
      const gData: GaugeItem[] = Array.isArray(raw) && typeof raw[0] === 'number'
        ? [{ name: rawSeries[0]?.name ?? '', value: raw[0] as number }]
        : (raw ?? []) as GaugeItem[]
      return {
        backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg,
        series: [{
          type: 'gauge' as const,
          radius: '78%', center: ['50%', '58%'],
          data: gData,
          detail: { formatter: `{value}${unitSuffix}`, fontSize: 22, color: textColor },
          title:  { fontSize: 13, color: mutedColor, offsetCenter: [0, '70%'] },
          axisLabel: { color: mutedColor, fontSize: 11 },
          axisTick:  { lineStyle: { color: isDark ? '#4b5563' : '#d1d5db' } },
          splitLine: { length: 15, lineStyle: { color: isDark ? '#6b7280' : '#9ca3af' } },
          axisLine:  {
            lineStyle: {
              color: [[0.3, '#67e0e3'], [0.7, '#37a2da'], [1, '#fd666d']],
              width: 18,
            },
          },
          pointer: { itemStyle: { color: 'auto' } },
        }],
      }
    }

    // ─ 漏斗图 ───────────────────────────────────────────────────────────────
    case 'funnel': {
      return {
        color: palette, backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg, legend: legendCfg,
        tooltip: {
          trigger: 'item' as const,
          formatter: `{b}: {c}${unitSuffix} ({d}%)`,
        },
        series: rawSeries.map(s => ({
          type: 'funnel' as const,
          name: s.name,
          left: '8%', right: '8%',
          top:    title ? 52 : 16,
          bottom: showLegend ? 36 : 16,
          data: s.data as Array<{ name: string; value: number }>,
          label: { color: textColor },
          emphasis: { label: { fontSize: 15, fontWeight: 'bold' as const } },
        })),
      }
    }

    // ─ 矩形树图 ─────────────────────────────────────────────────────────────
    case 'treemap': {
      // 兼容 data.tree（LLM 层级格式）和 data.series[0].data（标准格式）
      const treemapData = rawTree
        ?? (rawSeries[0]?.data as Array<{ name: string; value: number; children?: unknown[] }> | undefined)
        ?? []
      return {
        color: palette, backgroundColor: 'transparent',
        title: titleCfg, toolbox: toolboxCfg,
        tooltip: {
          formatter: (p: unknown) => {
            const pt = p as { name: string; value: number }
            return `${pt.name}: ${pt.value}${unitSuffix}`
          },
        },
        series: [{
          type: 'treemap' as const,
          name: rawSeries[0]?.name,
          top:    title ? 52 : 8,
          bottom: 8, left: 8, right: 8,
          data: treemapData as Array<{ name: string; value: number }>,
          label: { show: true, formatter: '{b}\n{c}', color: '#fff', fontSize: 12 },
          breadcrumb: { show: false },
          roam: false,
          itemStyle: { borderWidth: 2, borderColor: isDark ? '#111827' : '#f9fafb', gapWidth: 2 },
        }],
      }
    }

    default:
      return { backgroundColor: 'transparent' }
  }
}

// ── 省份名称规范化（LLM 常省略"省"/"市"后缀，与 GeoJSON 不匹配导致 NaN）──────────────

const PROVINCE_FULLNAME: Record<string, string> = {
  '北京': '北京市', '天津': '天津市', '上海': '上海市', '重庆': '重庆市',
  '河北': '河北省', '山西': '山西省', '辽宁': '辽宁省', '吉林': '吉林省', '黑龙江': '黑龙江省',
  '江苏': '江苏省', '浙江': '浙江省', '安徽': '安徽省', '福建': '福建省', '江西': '江西省',
  '山东': '山东省', '河南': '河南省', '湖北': '湖北省', '湖南': '湖南省', '广东': '广东省',
  '海南': '海南省', '四川': '四川省', '贵州': '贵州省', '云南': '云南省', '陕西': '陕西省',
  '甘肃': '甘肃省', '青海': '青海省', '台湾': '台湾省',
  '内蒙古': '内蒙古自治区', '广西': '广西壮族自治区',
  '西藏': '西藏自治区', '宁夏': '宁夏回族自治区', '新疆': '新疆维吾尔自治区',
  '香港': '香港特别行政区', '澳门': '澳门特别行政区',
}

function normalizeProvinceName(name: string): string {
  return PROVINCE_FULLNAME[name] ?? name
}

// ── 各图表类型默认高度（px）────────────────────────────────────────────────────

const CHART_HEIGHT: Partial<Record<ChartType, number>> = {
  map_china: 480,
  gauge:     280,
  radar:     360,
  treemap:   360,
  heatmap:   320,
}
const DEFAULT_HEIGHT = 360

// ── 组件 ─────────────────────────────────────────────────────────────────────

export function ChartBlock({ spec, className, hideActions }: ChartBlockProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const chartRef     = useRef<ECharts | null>(null)
  const lastChartIdRef = useRef<String | null>(null)
  const [error,   setError]   = useState<string | null>(null)
  const [loading, setLoading] = useState(spec.type === 'map_china')
  const [imageCopied, setImageCopied] = useState(false)
  const [imageCopyErr, setImageCopyErr] = useState(false)
  const [imageSaved, setImageSaved] = useState(false)
  const [fallbackBlob, setFallbackBlob] = useState<Blob | null>(null)

  const height = CHART_HEIGHT[spec.type] ?? DEFAULT_HEIGHT

  // map_china：先异步加载 GeoJSON，完成后才允许初始化 ECharts（容器此时可见）
  useEffect(() => {
    if (spec.type !== 'map_china') return
    let cancelled = false
    ensureChinaMap()
      .then(() => { if (!cancelled) setLoading(false) })
      .catch((e: unknown) => {
        if (!cancelled) {
          setError(e instanceof Error ? e.message : '地图数据加载失败')
          setLoading(false)
        }
      })
    return () => { cancelled = true }
  }, [spec.type])

  // 初始化 ECharts（非 map_china 直接执行；map_china 等 loading=false DOM 更新后执行）
  // 关键：不在 effect 中返回 cleanup，避免流式渲染期间 React 重复执行 dispose 导致动画"抖动"
  // 图表销毁统一由下方独立 unmount effect 处理
  useEffect(() => {
    if (loading) return
    if (!containerRef.current) return
    const isDark = document.documentElement.classList.contains('dark')

    // 同一 chartId 且图表已存在：仅更新 option，不销毁重建
    // 使用默认 merge 模式（notMerge=false），ECharts 对比新旧数据，数据相同时不触发动画
    if (lastChartIdRef.current === spec.chartId && chartRef.current) {
      try {
        chartRef.current.setOption(buildOption(spec, isDark))
      } catch (e) {
        setError(e instanceof Error ? e.message : '图表更新失败')
      }
      return
    }

    // chartId 变化或首次初始化：先手动清理旧实例，再创建新实例
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
        chart.setOption(buildOption(spec, isDark))
        lastChartIdRef.current = spec.chartId
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : '图表渲染失败')
      }
    })

    return () => { cancelled = true }
  }, [spec, loading])

  // 组件卸载时清理图表实例
  useEffect(() => {
    return () => {
      chartRef.current?.dispose()
      chartRef.current = null
      lastChartIdRef.current = null
    }
  }, [])

  // 暗色模式切换时更新图表主题（不销毁重建，merge 模式避免动画重启）
  useEffect(() => {
    const chart = chartRef.current
    if (!chart) return
    const observer = new MutationObserver(() => {
      if (!chartRef.current) return
      const dark = document.documentElement.classList.contains('dark')
      try {
        // merge 模式：仅更新颜色等差异项，不触发初始动画
        chartRef.current.setOption(buildOption(spec, dark))
      } catch { /* 忽略主题切换时的渲染错误 */ }
    })
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
    return () => observer.disconnect()
  })
  // 注：不设 deps 数组，每次 render 后重新绑定 observer 以确保 spec 闭包最新；
  // MutationObserver 本身开销极小，且 cleanup 自动断开旧 observer

  // 容器尺寸变化时自动 resize
  useEffect(() => {
    const el = containerRef.current
    if (!el) return
    const ro = new ResizeObserver(() => chartRef.current?.resize())
    ro.observe(el)
    return () => ro.disconnect()
  }, [])

  const getChartBlob = useCallback(async (): Promise<Blob> => {
    const chart = chartRef.current
    if (!chart) throw new Error('chart not ready')
    const dataUrl = chart.getDataURL({ type: 'png', pixelRatio: 2, backgroundColor: '#fff' })
    const res = await fetch(dataUrl)
    return res.blob()
  }, [])

  const copyImage = useCallback(async () => {
    try {
      const blob = await getChartBlob()
      const ok = await copyImageOrFallback(blob)
      if (ok) {
        setImageCopied(true)
        setTimeout(() => setImageCopied(false), 1500)
      } else {
        setFallbackBlob(blob)
      }
    } catch {
      setImageCopyErr(true)
      setTimeout(() => setImageCopyErr(false), 2000)
    }
  }, [getChartBlob])

  const saveImage = useCallback(async () => {
    try {
      const blob = await getChartBlob()
      savePngBlob(blob, `${spec.title || 'chart'}-${Date.now()}.png`)
      setImageSaved(true)
      setTimeout(() => setImageSaved(false), 1500)
    } catch {
      /* ignore */
    }
  }, [getChartBlob, spec.title])

  return (
    <div
      data-testid="chart-block"
      data-chart-type={spec.type}
      className={cn(
        'relative group',
        'rounded-xl border border-gray-200 dark:border-gray-700',
        'bg-white dark:bg-gray-900 overflow-hidden',
        className,
      )}
    >
      {/* 悬浮图标按钒（hover 时显示），ECharts canvas 截图不包含此层 */}
      {!error && !hideActions && (
        <div className="absolute top-2 right-2 z-10 flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity duration-150">
          <button
            type="button"
            onClick={copyImage}
            title={imageCopyErr ? '复制失败' : imageCopied ? '已复制' : '复制图片'}
            className={cn(
              'flex items-center justify-center w-7 h-7 rounded-md',
              'bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm border border-gray-200 dark:border-gray-700 shadow-sm transition-colors',
              imageCopyErr ? 'text-red-500' : 'text-gray-500 hover:text-blue-600 dark:text-gray-400 dark:hover:text-blue-400',
            )}
          >
            <Icon name={imageCopyErr ? 'error' : imageCopied ? 'check' : 'content_copy'} size="xs" />
          </button>
          <button
            type="button"
            onClick={saveImage}
            title={imageSaved ? '已保存' : '另存为图片'}
            className="flex items-center justify-center w-7 h-7 rounded-md bg-white/80 dark:bg-gray-800/80 backdrop-blur-sm border border-gray-200 dark:border-gray-700 shadow-sm text-gray-500 hover:text-blue-600 dark:text-gray-400 dark:hover:text-blue-400 transition-colors"
          >
            <Icon name={imageSaved ? 'check' : 'save_alt'} size="xs" />
          </button>
        </div>
      )}
      {error && (
        <div className="m-3 rounded-lg border border-red-200 dark:border-red-800/50 bg-red-50 dark:bg-red-900/20 p-3 text-sm text-red-600 dark:text-red-400">
          图表渲染失败：{error}
        </div>
      )}
      {loading && (
        <div className="flex items-center justify-center" style={{ height }}>
          <span className="text-sm text-gray-400 dark:text-gray-500 animate-pulse">
            正在加载地图数据…
          </span>
        </div>
      )}
      <div
        ref={containerRef}
        style={{ height, display: loading ? 'none' : undefined }}
      />
      <MobileImageFallback
        open={fallbackBlob !== null}
        blob={fallbackBlob}
        onClose={() => setFallbackBlob(null)}
        filename={`${spec.title || 'chart'}-${Date.now()}.png`}
      />
    </div>
  )
}
