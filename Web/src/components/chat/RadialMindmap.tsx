import { useMemo, useState, useEffect } from 'react'
import type { MindmapSpec } from './MindmapBlock'

// ── 类型 ─────────────────────────────────────────────────────────────────────

/** 脑图布局模式 */
export type MindmapLayout = 'tree' | 'radial' | 'lr' | 'rl' | 'tb' | 'bt'

/** 布局标签 */
export const LAYOUT_LABELS: Record<MindmapLayout, string> = {
  tree: '缩进树',
  radial: '左右放射',
  lr: '中心→右',
  rl: '中心→左',
  tb: '中心→下',
  bt: '中心→上',
}

/** 脑图放射方向 */
export type MindmapDirection = 'both' | 'right' | 'left' | 'down' | 'up'

/** MindmapLayout → 放射方向映射 */
export function layoutToDirection(layout: string): MindmapDirection {
  switch (layout) {
    case 'lr': return 'right'
    case 'rl': return 'left'
    case 'tb': return 'down'
    case 'bt': return 'up'
    default:   return 'both'   // radial / tree fallback
  }
}

// ── 常量 ─────────────────────────────────────────────────────────────────────

const VIEW_W = 800
const BASE_VIEW_H = 600
const CX = VIEW_W / 2
const CY = BASE_VIEW_H / 2
const ROOT_R = 32
const BASE_BRANCH_DIST = 140    // 基础分支距离
const SUB_GAP = 26
const SUB_OFFSET = 72

const DEFAULT_COLORS = [
  '#5470c6', '#91cc75', '#fac858', '#ee6666',
  '#73c0de', '#3ba272', '#fc8452', '#9a60b4',
]

// ── 工具 ─────────────────────────────────────────────────────────────────────

function estTextWidth(text: string, fontSize: number): number {
  return text.length * fontSize * 0.82 + 16
}

/** 智能适配文本：先缩字号，再截断。返回 { display, fontSize } */
function fitText(text: string, maxWidth: number, baseFontSize: number): { display: string; fontSize: number } {
  if (estTextWidth(text, baseFontSize) <= maxWidth)
    return { display: text, fontSize: baseFontSize }
  // 尝试缩 1px
  const small = baseFontSize - 1
  if (estTextWidth(text, small) <= maxWidth)
    return { display: text, fontSize: small }
  // 截断
  let t = text
  while (t.length > 2 && estTextWidth(t + '…', small) > maxWidth)
    t = t.slice(0, -1)
  return { display: t + '…', fontSize: small }
}

/** 在 [fromDeg, toDeg] 区间均匀分布 n 个角度 */
function spreadAngles(n: number, fromDeg: number, toDeg: number): number[] {
  if (n <= 1) return [(fromDeg + toDeg) / 2]
  return Array.from({ length: n }, (_, i) =>
    fromDeg + (i / (n - 1)) * (toDeg - fromDeg),
  )
}

/** 动态扇角：分支越多扇角越大 */
function dynamicSpan(count: number): number {
  if (count <= 2) return 60
  if (count <= 4) return 90
  if (count <= 6) return 120
  if (count <= 8) return 140
  return 160
}

/** 动态分支距离：分支越多推得越远 */
function dynamicBranchDist(count: number): number {
  return Math.min(BASE_BRANCH_DIST + Math.max(0, count - 3) * 18, 240)
}


// ── 布局引擎 ─────────────────────────────────────────────────────────────────

interface BranchLayout {
  branch: MindmapSpec['branches'][number]
  angle: number        // 弧度
  cos: number
  sin: number
  color: string
  bx: number           // 分支节点 X
  by: number           // 分支节点 Y
  textRight: boolean   // 子节点文字在右侧？
  subNodes: Array<{ id: string; label: string; x: number; y: number }>
}

function computeLayout(spec: MindmapSpec, direction: MindmapDirection, colors: string[]): { items: BranchLayout[]; viewH: number; cy: number } {
  const branches = spec.branches
  const n = branches.length
  if (n === 0) return { items: [], viewH: BASE_VIEW_H, cy: CY }

  // 动态参数
  const branchDist = dynamicBranchDist(n)
  let angleDegs: number[]

  switch (direction) {
    case 'both': {
      const leftIdx: number[] = []
      const rightIdx: number[] = []
      for (let i = 0; i < n; i++) {
        if (i % 2 === 0) leftIdx.push(i)
        else rightIdx.push(i)
      }
      const lSpan = dynamicSpan(leftIdx.length)
      const rSpan = dynamicSpan(rightIdx.length)
      const leftDegs = spreadAngles(leftIdx.length, 180 - lSpan / 2, 180 + lSpan / 2)
      const rightDegs = spreadAngles(rightIdx.length, -rSpan / 2, rSpan / 2)
      angleDegs = new Array(n).fill(0)
      leftIdx.forEach((idx, i) => { angleDegs[idx] = leftDegs[i] })
      rightIdx.forEach((idx, i) => { angleDegs[idx] = rightDegs[i] })
      break
    }
    case 'right': {
      const span = dynamicSpan(n)
      angleDegs = spreadAngles(n, -span / 2, span / 2)
      break
    }
    case 'left': {
      const span = dynamicSpan(n)
      angleDegs = spreadAngles(n, 180 - span / 2, 180 + span / 2)
      break
    }
    case 'down': {
      const span = dynamicSpan(n)
      angleDegs = spreadAngles(n, 90 - span / 2, 90 + span / 2)
      break
    }
    case 'up': {
      const span = dynamicSpan(n)
      angleDegs = spreadAngles(n, 270 - span / 2, 270 + span / 2)
      break
    }
  }

  // 初始布局
  const items: BranchLayout[] = branches.map((branch, idx) => {
    const deg = angleDegs[idx]
    const angle = deg * Math.PI / 180
    const color = colors[idx % colors.length]
    const cos = Math.cos(angle)
    const sin = Math.sin(angle)

    const bx = CX + branchDist * cos
    const by = CY + branchDist * sin

    const px = -sin
    const py = cos
    const textRight = cos >= 0

    const m = branch.children.length
    const subNodes = branch.children.map((child, ci) => {
      const offset = (ci - (m - 1) / 2) * SUB_GAP
      const sx = bx + offset * px + SUB_OFFSET * cos
      const sy = by + offset * py + SUB_OFFSET * sin
      return { id: child.id, label: child.label, x: sx, y: sy }
    })

    return { branch, angle, cos, sin, color, bx, by, textRight, subNodes }
  })

  // ── 单方向布局碰撞检测与移位 ──
  // 仅对 right/left/down/up 做单轴碰撞检测；'both' 左右交替天然分离
  if (direction !== 'both' && n > 1) {
    // 按主轴垂直方向排序
    const isHorizontal = direction === 'right' || direction === 'left'
    const sorted = [...items].sort((a, b) => (isHorizontal ? a.by - b.by : a.bx - b.bx))

    for (let i = 1; i < sorted.length; i++) {
      const prev = sorted[i - 1]
      const curr = sorted[i]

      // 计算前一个分支的子节点底边
      const prevSubBottom = prev.subNodes.length > 0
        ? Math.max(...prev.subNodes.map(s => s.y)) + 13   // 圆 + 半高文字底板
        : prev.by + 14
      const prevBottom = Math.max(prev.by + 14, prevSubBottom)

      // 当前分支的顶边
      const currSubTop = curr.subNodes.length > 0
        ? Math.min(...curr.subNodes.map(s => s.y)) - 13
        : curr.by - 14
      const currTop = Math.min(curr.by - 14, currSubTop)

      const overlap = prevBottom - currTop + 6  // 6px 安全间距
      if (overlap > 0) {
        const shift = overlap
        // 当前及后续分支全部下移（或右移）
        for (let j = i; j < sorted.length; j++) {
          const item = sorted[j]
          if (isHorizontal) {
            item.by += shift
            item.subNodes.forEach(s => { s.y += shift })
          } else {
            item.bx += shift
            item.subNodes.forEach(s => { s.x += shift })
          }
        }
      }
    }
  }

  // 动态 viewBox 高度
  const allY = items.flatMap(it => [it.by, ...it.subNodes.map(s => s.y)])
  const minY = Math.min(...allY)
  const maxY = Math.max(...allY)
  const dy = Math.max(maxY - minY + 120, BASE_VIEW_H)
  const viewH = Math.max(BASE_VIEW_H, Math.min(dy, 1200))
  const cy = viewH / 2

  // 将 Y 坐标居中
  const yOffset = cy - (minY + maxY) / 2
  for (const item of items) {
    item.by += yOffset
    item.subNodes.forEach(s => { s.y += yOffset })
  }

  return { items, viewH, cy: viewH / 2 }
}

// ── 工具：边缘点计算 ─────────────────────────────────────────────────────────

const SUB_R = 4  // 子节点圆半径

/** pill 面向根节点一侧的边缘点（根→分支连线的终点） */
function pillInnerEdge(bx: number, by: number, w: number, h: number, cos: number, sin: number) {
  return Math.abs(cos) >= Math.abs(sin)
    ? { x: bx - Math.sign(cos) * w / 2, y: by }
    : { x: bx, y: by - Math.sign(sin) * h / 2 }
}

/** pill 背离根节点一侧的边缘点（分支→子节点连线的起点） */
function pillOuterEdge(bx: number, by: number, w: number, h: number, cos: number, sin: number) {
  return Math.abs(cos) >= Math.abs(sin)
    ? { x: bx + Math.sign(cos) * w / 2, y: by }
    : { x: bx, y: by + Math.sign(sin) * h / 2 }
}

/** 子节点圆面向分支一侧的边缘点（分支→子节点连线的终点） */
function subInnerEdge(sx: number, sy: number, cos: number, sin: number) {
  return Math.abs(cos) >= Math.abs(sin)
    ? { x: sx - Math.sign(cos) * SUB_R, y: sy }
    : { x: sx, y: sy - Math.sign(sin) * SUB_R }
}

// ── 组件 ─────────────────────────────────────────────────────────────────────

interface RadialMindmapProps {
  spec: MindmapSpec
  direction: MindmapDirection
}

export function RadialMindmap({ spec, direction }: RadialMindmapProps) {
  const [isDark, setIsDark] = useState(() =>
    typeof document !== 'undefined' && document.documentElement.classList.contains('dark'),
  )

  useEffect(() => {
    const check = () => setIsDark(document.documentElement.classList.contains('dark'))
    const observer = new MutationObserver(check)
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
    return () => observer.disconnect()
  }, [])

  const colors = spec.branchColors && spec.branchColors.length > 0
    ? spec.branchColors
    : DEFAULT_COLORS

  const layoutResult = useMemo(
    () => computeLayout(spec, direction, colors),
    [spec, direction, colors],
  )

  if (layoutResult.items.length === 0) return null

  const layout = layoutResult.items
  const viewH = layoutResult.viewH
  const cy = layoutResult.cy

  const bgColor = isDark ? '#111827' : '#fafafa'
  const subTextColor = isDark ? '#9ca3af' : '#6b7280'
  const subBgColor = isDark ? '#1f2937' : '#f3f4f6'
  const rootColor = isDark ? '#6366f1' : '#4f46e5'

  return (
    <svg
      viewBox={`0 0 ${VIEW_W} ${viewH}`}
      className="w-full h-auto"
      style={{ background: bgColor, borderRadius: '12px' }}
      aria-label={spec.title || '思维导图'}
    >
      <defs>
        <filter id="mm-shadow" x="-20%" y="-20%" width="140%" height="140%">
          <feDropShadow dx="0" dy="1.5" stdDeviation="2.5" floodColor="#00000025" />
        </filter>
      </defs>

      {/* ── 根→分支：从根圆边缘到 pill 内侧边缘，S 曲线 ── */}
      {layout.map(({ branch, color, cos, sin, bx, by }) => {
        const sx = CX + ROOT_R * cos
        const sy = cy + ROOT_R * sin

        // 分支标签尺寸
        const { display: blabel, fontSize: bfontSize } = fitText(branch.label, 130, 12)
        const nodeW = Math.max(estTextWidth(blabel, bfontSize), 36)
        const nodeH = 28

        const inner = pillInnerEdge(bx, by, nodeW, nodeH, cos, sin)

        const dist = Math.hypot(inner.x - sx, inner.y - sy)
        const cp1x = sx + cos * dist * 0.4
        const cp1y = sy + sin * dist * 0.4
        const cp2x = inner.x - cos * dist * 0.4
        const cp2y = inner.y - sin * dist * 0.4

        return (
          <path
            key={`line-${branch.id}`}
            d={`M ${sx.toFixed(1)} ${sy.toFixed(1)} C ${cp1x.toFixed(1)} ${cp1y.toFixed(1)} ${cp2x.toFixed(1)} ${cp2y.toFixed(1)} ${inner.x.toFixed(1)} ${inner.y.toFixed(1)}`}
            stroke={color}
            strokeWidth={3.5}
            fill="none"
            strokeLinecap="round"
            opacity={0.82}
          />
        )
      })}

      {/* ── 分支节点 pills + 子节点 ── */}
      {layout.map(({ branch, color, cos, sin, bx, by, textRight, subNodes }) => {
        const { display: blabel, fontSize: bfontSize } = fitText(branch.label, 130, 12)
        const textW = estTextWidth(blabel, bfontSize)
        const nodeW = Math.max(textW, 36)
        const nodeH = 28

        const outer = pillOuterEdge(bx, by, nodeW, nodeH, cos, sin)

        return (
          <g key={`g-${branch.id}`}>
            <rect
              x={bx - nodeW / 2}
              y={by - nodeH / 2}
              width={nodeW}
              height={nodeH}
              rx={14}
              fill={color}
              filter="url(#mm-shadow)"
            />
            <text
              x={bx}
              y={by + 5}
              textAnchor="middle"
              fill="#fff"
              fontSize={bfontSize}
              fontWeight="bold"
              fontFamily="system-ui, -apple-system, sans-serif"
            >{blabel}</text>

            {/* 子节点连线：pill 外侧边缘 → 子节点圆内侧边缘 */}
            {subNodes.map(sub => {
              const subIn = subInnerEdge(sub.x, sub.y, cos, sin)
              const hDist = subIn.x - outer.x

              const cp1x = outer.x + hDist * 0.35
              const cp1y = outer.y
              const cp2x = subIn.x - hDist * 0.35
              const cp2y = subIn.y

              return (
                <path
                  key={`subl-${sub.id}`}
                  d={`M ${outer.x.toFixed(1)} ${outer.y.toFixed(1)} C ${cp1x.toFixed(1)} ${cp1y.toFixed(1)} ${cp2x.toFixed(1)} ${cp2y.toFixed(1)} ${subIn.x.toFixed(1)} ${subIn.y.toFixed(1)}`}
                  stroke={color}
                  strokeWidth={1.8}
                  fill="none"
                  opacity={0.45}
                  strokeLinecap="round"
                />
              )
            })}

            {/* 子节点 */}
            {subNodes.map(sub => {
              const { display: subLabel, fontSize: subFontSize } = fitText(sub.label, 160, 10)
              const subW = estTextWidth(subLabel, subFontSize)
              const tx = textRight ? sub.x + 7 : sub.x - 7 - subW
              const tcx = textRight ? sub.x + 7 + subW / 2 : sub.x - 7 - subW + subW / 2

              return (
                <g key={`sub-${sub.id}`}>
                  <circle cx={sub.x} cy={sub.y} r={SUB_R} fill={color} opacity={0.3} />
                  <rect x={tx} y={sub.y - 9} width={subW} height={18} rx={9} fill={subBgColor} />
                  <text
                    x={tcx} y={sub.y + 4}
                    textAnchor="middle"
                    fill={subTextColor}
                    fontSize={subFontSize}
                    fontFamily="system-ui, -apple-system, sans-serif"
                  >{subLabel}</text>
                </g>
              )
            })}
          </g>
        )
      })}

      {/* ── 中心根节点 ── */}
      <circle cx={CX} cy={cy} r={ROOT_R} fill={rootColor} filter="url(#mm-shadow)" />
      <text
        x={CX}
        y={cy + 5}
        textAnchor="middle"
        fill="#fff"
        fontSize={14}
        fontWeight="bold"
        fontFamily="system-ui, -apple-system, sans-serif"
      >{fitText(spec.rootLabel, 100, 14).display}</text>
    </svg>
  )
}
