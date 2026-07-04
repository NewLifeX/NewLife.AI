import { useCallback, useRef, useState } from 'react'
import { cn } from '@/lib/utils'
import { captureDomAsPng, copyImageOrFallback, savePngBlob } from '@/utils/imageCapture'
import { MobileImageFallback } from '@/components/atoms/MobileImageFallback'
import { Icon } from '@/components/common/Icon'
import { RadialMindmap, LAYOUT_LABELS, layoutToDirection } from './RadialMindmap'
import { MindmapECharts } from './MindmapECharts'
import type { MindmapLayout } from './RadialMindmap'

// ── 类型 ─────────────────────────────────────────────────────────────────────

interface TreeNode {
  id: string
  label: string
  level: number     // 1 = 一级分支（##）, 2 = 二级（###）
  children: TreeNode[]
}

export interface MindmapSpec {
  mindmapId: string
  title: string
  content: string   // Markdown 大纲
  rootLabel: string // 从 # 行提取的中心节点文本
  branches: TreeNode[]
  /** AI 建议的布局模式，用户可在前端切换 */
  layout?: MindmapLayout
  /** AI 可传入的配色数组，覆盖默认 BRANCH_COLORS */
  branchColors?: string[]
  /** 初始折叠的节点 ID 列表 */
  collapsed?: string[]
  /** 最大可见深度（1=仅一级分支，2=一二级），默认无限制 */
  maxDepth?: number
}

const BRANCH_COLORS = [
  '#5470c6', '#91cc75', '#fac858', '#ee6666',
  '#73c0de', '#3ba272', '#fc8452', '#9a60b4',
]

// ── 数据解析 ─────────────────────────────────────────────────────────────────

/** 将 Markdown 大纲（# / ## / ###）解析为树结构 */
function parseOutline(content: string): { rootLabel: string; branches: TreeNode[] } {
  const lines = content.split('\n').map(l => l.trim()).filter(Boolean)
  let rootLabel = '中心主题'
  const branches: TreeNode[] = []
  let currentBranch: TreeNode | null = null
  let currentL2: TreeNode | null = null
  let nodeId = 0

  for (const line of lines) {
    if (line.startsWith('### ')) {
      const label = line.slice(4).trim()
      const node: TreeNode = { id: `n${++nodeId}`, label, level: 2, children: [] }
      if (currentL2) { currentL2.children.push(node) }
      else if (currentBranch) { currentBranch.children.push(node) }
    } else if (line.startsWith('## ')) {
      const label = line.slice(3).trim()
      currentL2 = { id: `n${++nodeId}`, label, level: 1, children: [] }
      branches.push(currentL2)
      currentBranch = currentL2
    } else if (/^#(?!#)\s/.test(line)) {
      rootLabel = line.replace(/^#+\s+/, '').trim()
    } else if (/^[-*]\s/.test(line)) {
      const label = line.slice(2).trim()
      const node: TreeNode = { id: `n${++nodeId}`, label, level: 2, children: [] }
      if (currentL2) { currentL2.children.push(node) }
      else if (currentBranch) { currentBranch.children.push(node) }
    }
  }

  return { rootLabel, branches }
}

export function parseMindmapData(result: string): MindmapSpec | null {
  try {
    const raw = JSON.parse(result)
    if (!raw?.mindmapId) return null
    const content = String(raw.content ?? '')
    const { rootLabel, branches } = parseOutline(content)
    // 解析 AI 传入的配色数组
    var branchColors: string[] | undefined;
    if (Array.isArray(raw.branchColors) && raw.branchColors.every((c: unknown) => typeof c === 'string'))
      branchColors = raw.branchColors as string[];
    // 解析初始折叠节点 ID 列表
    var collapsed: string[] | undefined;
    if (Array.isArray(raw.collapsed) && raw.collapsed.every((c: unknown) => typeof c === 'string'))
      collapsed = raw.collapsed as string[];
    // 解析布局模式
    var layout: MindmapLayout | undefined;
    if (typeof raw.layout === 'string' && ['tree', 'radial', 'lr', 'rl', 'tb', 'bt'].includes(raw.layout))
      layout = raw.layout as MindmapLayout;
    // 解析最大深度
    var maxDepth: number | undefined;
    if (typeof raw.maxDepth === 'number' && raw.maxDepth >= 1)
      maxDepth = raw.maxDepth;
    return {
      mindmapId: String(raw.mindmapId),
      title: String(raw.title ?? ''),
      content,
      rootLabel,
      branches,
      layout,
      branchColors,
      collapsed,
      maxDepth,
    }
  } catch {
    return null
  }
}

// ── 子组件：分支节点 ──────────────────────────────────────────────────────────

interface BranchNodeProps {
  node: TreeNode
  color: string
  isRoot?: boolean
  collapsedIds?: Set<string>
  maxDepth?: number
}

function BranchNode({ node, color, isRoot = false, collapsedIds, maxDepth }: BranchNodeProps) {
  const [open, setOpen] = useState(!collapsedIds?.has(node.id))
  const hasChildren = node.children.length > 0
  // 超过最大深度时不渲染子节点
  const showChildren = hasChildren && maxDepth != null ? node.level < maxDepth : hasChildren

  return (
    <div>
      <button
        type="button"
        onClick={() => hasChildren && setOpen(v => !v)}
        className={cn(
          'w-full text-left rounded-lg px-3 py-1.5 text-sm transition-colors',
          isRoot
            ? 'font-semibold text-white text-xs'
            : node.level === 1
              ? 'font-medium text-gray-800 dark:text-gray-100 bg-gray-50 dark:bg-gray-800/70 border border-gray-200 dark:border-gray-700 hover:bg-gray-100 dark:hover:bg-gray-800'
              : 'text-gray-600 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200',
          hasChildren ? 'cursor-pointer' : 'cursor-default',
        )}
        style={isRoot ? { backgroundColor: color } : {}}
      >
        <span className="flex items-center gap-1.5">
          {hasChildren && !isRoot && (
            <span
              className="text-[9px] text-gray-400 shrink-0 transition-transform duration-150 inline-block"
              style={{ transform: open ? 'rotate(0deg)' : 'rotate(-90deg)' }}
            >▼</span>
          )}
          {!hasChildren && !isRoot && (
            <span className="w-1.5 h-1.5 rounded-full shrink-0" style={{ backgroundColor: `${color}90` }} />
          )}
          {node.label}
        </span>
      </button>

      {showChildren && open && (
        <div className="ml-4 mt-1 pl-3 space-y-1" style={{ borderLeft: `2px solid ${color}30` }}>
          {node.children.map(child => (
            <BranchNode key={child.id} node={child} color={color} collapsedIds={collapsedIds} maxDepth={maxDepth} />
          ))}
        </div>
      )}
    </div>
  )
}

// ── 主组件 ────────────────────────────────────────────────────────────────────

interface MindmapBlockProps {
  spec: MindmapSpec
  className?: string
}

export function MindmapBlock({ spec, className }: MindmapBlockProps) {
  const rootRef = useRef<HTMLDivElement>(null)
  const [layout, setLayout] = useState<MindmapLayout>(spec.layout ?? 'tree')
  const [imageCopied, setImageCopied] = useState(false)
  const [imageCopyErr, setImageCopyErr] = useState(false)
  const [imageSaved, setImageSaved] = useState(false)
  const [fallbackBlob, setFallbackBlob] = useState<Blob | null>(null)
  const isTreeLayout = layout === 'tree'
  // ECharts 正交树：lr / rl / tb / bt
  const isEChartsLayout = layout === 'lr' || layout === 'rl' || layout === 'tb' || layout === 'bt'

  const copyImage = useCallback(async () => {
    if (!rootRef.current) return
    try {
      const blob = await captureDomAsPng(rootRef.current)
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
  }, [])

  const saveImage = useCallback(async () => {
    if (!rootRef.current) return
    try {
      const blob = await captureDomAsPng(rootRef.current)
      savePngBlob(blob, `${spec.title || 'mindmap'}-${Date.now()}.png`)
      setImageSaved(true)
      setTimeout(() => setImageSaved(false), 1500)
    } catch {
      /* ignore */
    }
  }, [spec.title])

  const iconBtnClass = 'flex items-center justify-center w-6 h-6 rounded transition-colors hover:bg-gray-100 dark:hover:bg-gray-700'

  if (spec.branches.length === 0) {
    return (
      <div className={cn('rounded-xl border border-gray-200 dark:border-gray-700 p-4 text-sm text-gray-500', className)}>
        思维导图暂无数据
      </div>
    )
  }

  return (
    <>
    <div
      ref={rootRef}
      data-testid="mindmap-block"
      className={cn(
        'rounded-xl border border-gray-200 dark:border-gray-700',
        'bg-white dark:bg-gray-900 overflow-hidden',
        className,
      )}
    >
      {/* 标题栏 */}
      {spec.title && (
        <div className="flex items-center justify-between px-5 pt-4 pb-3 border-b border-gray-100 dark:border-gray-800">
          <h3 className="text-sm font-semibold text-gray-800 dark:text-gray-100">{spec.title}</h3>
          <div className="flex items-center gap-0.5" data-no-capture>
            {/* 布局切换按钮组 */}
            <div className="flex items-center gap-0.5 mr-2 pr-2 border-r border-gray-200 dark:border-gray-700">
              {(['tree', 'radial', 'lr', 'rl', 'tb', 'bt'] as MindmapLayout[]).map(mode => (
                <button
                  key={mode}
                  type="button"
                  onClick={() => setLayout(mode)}
                  title={LAYOUT_LABELS[mode]}
                  className={cn(
                    iconBtnClass,
                    layout === mode
                      ? 'text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/30'
                      : 'text-gray-400 hover:text-gray-600 dark:text-gray-500 dark:hover:text-gray-300',
                  )}
                >
                  <Icon name={
                    mode === 'tree' ? 'account_tree' :
                    mode === 'radial' ? 'hub' :
                    mode === 'lr' ? 'arrow_forward' :
                    mode === 'rl' ? 'arrow_back' :
                    mode === 'tb' ? 'arrow_downward' : 'arrow_upward'
                  } size="sm" />
                </button>
              ))}
            </div>
            <button
              type="button"
              onClick={copyImage}
              title={imageCopyErr ? '复制失败' : imageCopied ? '已复制' : '复制图片'}
              className={cn(
                iconBtnClass,
                imageCopyErr ? 'text-red-500' : 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400',
              )}
            >
              <Icon name={imageCopyErr ? 'error' : imageCopied ? 'check' : 'content_copy'} size="sm" />
            </button>
            <button
              type="button"
              onClick={saveImage}
              title={imageSaved ? '已保存' : '另存为图片'}
              className={cn(iconBtnClass, 'text-gray-400 hover:text-blue-600 dark:text-gray-500 dark:hover:text-blue-400')}
            >
              <Icon name={imageSaved ? 'check' : 'save_alt'} size="sm" />
            </button>
          </div>
        </div>
      )}

      {/* 导图主体 */}
      {isTreeLayout ? (
        <div className="px-5 py-4">
          {/* 中心节点 */}
          <div className="flex items-center gap-2 mb-4">
            <div className="w-2.5 h-2.5 rounded-full bg-primary shrink-0" />
            <span className="text-base font-bold text-gray-900 dark:text-gray-50">{spec.rootLabel}</span>
          </div>

          {/* 分支列表 */}
          <div className="space-y-3 pl-4 border-l-2 border-gray-200 dark:border-gray-700">
            {spec.branches.map((branch, idx) => {
              const colors = spec.branchColors && spec.branchColors.length > 0 ? spec.branchColors : BRANCH_COLORS;
              const collapsedIds = spec.collapsed ? new Set(spec.collapsed) : undefined;
              return (
              <BranchNode
                key={branch.id}
                node={branch}
                color={colors[idx % colors.length]}
                collapsedIds={collapsedIds}
                maxDepth={spec.maxDepth}
              />
            )})}
          </div>
        </div>
      ) : isEChartsLayout ? (
        <MindmapECharts spec={spec} layout={layout} className="px-1" />
      ) : (
        <RadialMindmap spec={spec} direction={layoutToDirection(layout)} />
      )}
    </div>
    <MobileImageFallback
      open={fallbackBlob !== null}
      blob={fallbackBlob}
      onClose={() => setFallbackBlob(null)}
      filename={`${spec.title || 'mindmap'}-${Date.now()}.png`}
    />
    </>
  )
}
