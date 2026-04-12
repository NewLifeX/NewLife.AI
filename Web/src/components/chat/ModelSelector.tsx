import { useState, useRef, useEffect, useMemo } from 'react'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import type { ModelInfo } from '@/types'

interface ModelSelectorProps {
  models: ModelInfo[]
  currentModel: number
  defaultModelId?: number
  onModelChange: (modelId: number) => void
  className?: string
}

// 能力标签配置：key / 显示文字 / 颜色
const CAPABILITIES: { key: keyof ModelInfo; label: string; cls: string }[] = [
  { key: 'supportThinking', label: '思考', cls: 'text-purple-600 bg-purple-50 dark:text-purple-400 dark:bg-purple-900/25' },
  { key: 'supportFunctionCalling', label: '工具', cls: 'text-blue-600 bg-blue-50 dark:text-blue-400 dark:bg-blue-900/25' },
  { key: 'supportVision', label: '视觉', cls: 'text-green-600 bg-green-50 dark:text-green-400 dark:bg-green-900/25' },
  { key: 'supportAudio', label: '音频', cls: 'text-teal-600 bg-teal-50 dark:text-teal-400 dark:bg-teal-900/25' },
  { key: 'supportImageGeneration', label: '图像', cls: 'text-orange-600 bg-orange-50 dark:text-orange-400 dark:bg-orange-900/25' },
  { key: 'supportVideoGeneration', label: '视频', cls: 'text-rose-600 bg-rose-50 dark:text-rose-400 dark:bg-rose-900/25' },
]

export function ModelSelector({
  models,
  currentModel,
  defaultModelId,
  onModelChange,
  className,
}: ModelSelectorProps) {
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const ref = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  // 模型超过 8 个时启用服务商分组
  const manyModels = models.length > 8
  const selected = models.find((m) => m.id === currentModel)

  // 默认模型排第一，其余保持原顺序
  const sortedModels = useMemo(() => {
    if (!defaultModelId) return models
    return [
      ...models.filter((m) => m.id === defaultModelId),
      ...models.filter((m) => m.id !== defaultModelId),
    ]
  }, [models, defaultModelId])

  // 搜索过滤（支持按服务商名搜索）
  const filteredModels = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return sortedModels
    return sortedModels.filter(
      (m) =>
        m.name.toLowerCase().includes(q) ||
        m.code.toLowerCase().includes(q) ||
        (m.provider ?? '').toLowerCase().includes(q),
    )
  }, [sortedModels, search])

  // 按服务商分组（仅当模型 > 8 个时）
  const groupedModels = useMemo(() => {
    if (!manyModels) return [{ provider: '', models: filteredModels }]
    const map = new Map<string, ModelInfo[]>()
    for (const m of filteredModels) {
      const key = m.provider ?? ''
      if (!map.has(key)) map.set(key, [])
      map.get(key)!.push(m)
    }
    return Array.from(map.entries()).map(([provider, list]) => ({ provider, models: list }))
  }, [filteredModels, manyModels])

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false)
    }
    if (open) document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [open])

  // 打开下拉时自动聚焦搜索框
  useEffect(() => {
    if (open) setTimeout(() => inputRef.current?.focus(), 50)
    else setSearch('')
  }, [open])

  return (
    <div ref={ref} className={cn('relative', className)}>
      <button
        onClick={() => setOpen(!open)}
        className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors text-sm font-medium text-gray-700 dark:text-gray-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
      >
        <span>{selected?.name ?? '选择模型'}</span>
        <Icon name="expand_more" size="base" className={cn('transition-transform', open && 'rotate-180')} />
      </button>

      {open && (
        <div
          className="fixed z-[200] animate-slide-up"
          style={{ top: (ref.current?.getBoundingClientRect().bottom ?? 0) + 4, left: ref.current?.getBoundingClientRect().left ?? 0 }}
        >
          <div
            className="w-72 bg-white dark:bg-gray-800 rounded-xl shadow-menu dark:shadow-black/40 border border-gray-100 dark:border-gray-700 flex flex-col"
            style={{ maxHeight: 'min(480px, calc(100vh - 80px))' }}
          >
            {/* 搜索框 */}
            {models.length > 5 && (
              <div className="p-2 border-b border-gray-100 dark:border-gray-700 flex-shrink-0">
                <div className="flex items-center space-x-2 px-2 py-1.5 rounded-lg bg-gray-50 dark:bg-gray-700/50">
                  <Icon name="search" size="sm" className="text-gray-400 flex-shrink-0" />
                  <input
                    ref={inputRef}
                    type="text"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder={manyModels ? '搜索模型或服务商...' : '搜索模型...'}
                    className="flex-1 text-sm bg-transparent outline-none text-gray-700 dark:text-gray-200 placeholder-gray-400"
                  />
                  {search && (
                    <button onClick={() => setSearch('')} className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300">
                      <Icon name="close" size="xs" />
                    </button>
                  )}
                </div>
              </div>
            )}

            {/* 模型列表 */}
            <div className="p-1.5 overflow-y-auto custom-scrollbar flex-1">
              {filteredModels.length === 0 ? (
                <p className="text-center text-sm text-gray-400 py-6">未找到匹配的模型</p>
              ) : (
                groupedModels.map(({ provider, models: groupModels }, gIdx) => (
                  <div key={provider || gIdx}>
                    {/* 服务商分组标题 */}
                    {manyModels && provider && (
                      <div className={cn('px-2 pb-1', gIdx > 0 ? 'pt-3' : 'pt-1')}>
                        <span className="text-[11px] font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wide">
                          {provider}
                        </span>
                      </div>
                    )}
                    {groupModels.map((model) => {
                      const isActive = model.id === currentModel
                      const isDefault = model.id === defaultModelId
                      return (
                        <button
                          key={model.id}
                          onClick={() => { onModelChange(model.id); setOpen(false) }}
                          className={cn(
                            'w-full flex items-center justify-between px-3 py-2 rounded-lg text-sm transition-colors text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50',
                            isActive
                              ? 'bg-blue-50 dark:bg-blue-900/20 text-primary dark:text-blue-400'
                              : 'text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700/50',
                          )}
                        >
                          {/* 左侧：图标 + 模型名 */}
                          <div className="flex items-center space-x-2 min-w-0">
                            <Icon name="smart_toy" size="base" className={cn('flex-shrink-0', isActive ? 'text-primary' : 'text-gray-400')} />
                            <div className="min-w-0">
                              <span className="font-medium truncate block">{model.name}</span>
                              {isDefault && !isActive && (
                                <span className="text-xs text-gray-400 dark:text-gray-500">默认</span>
                              )}
                            </div>
                          </div>
                          {/* 右侧：能力标签 + 选中勾 */}
                          <div className="flex items-center gap-1 flex-shrink-0 ml-2">
                            {CAPABILITIES.map(({ key, label, cls }) =>
                              model[key] ? (
                                <span key={key} className={cn('text-[10px] font-medium leading-none px-1 py-0.5 rounded', cls)}>
                                  {label}
                                </span>
                              ) : null,
                            )}
                            {isActive && <Icon name="check" size="sm" className="text-primary ml-0.5" />}
                          </div>
                        </button>
                      )
                    })}
                  </div>
                ))
              )}
            </div>

            {/* 底部：模型数量提示 */}
            {models.length > 5 && (
              <div className="px-3 py-1.5 border-t border-gray-100 dark:border-gray-700 flex-shrink-0">
                <p className="text-xs text-gray-400 dark:text-gray-500 text-center">
                  {search ? `${filteredModels.length} / ${models.length} 个模型` : `共 ${models.length} 个模型`}
                </p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
