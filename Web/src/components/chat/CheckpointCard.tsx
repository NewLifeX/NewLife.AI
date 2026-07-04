import { useState, useEffect, useMemo, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { respondCheckpoint } from '@/lib/api'
import { useChatStore } from '@/stores/chatStore'
import type { CheckpointData, CheckpointOption, QuestionGroup, GroupAnswer } from '@/types'

interface CheckpointCardProps {
  /** 检查点数据（从 tool_call_start arguments 解析） */
  data: CheckpointData
  /** 工具调用状态 */
  status: 'calling' | 'done' | 'error'
  className?: string
}

interface GroupAnswerState {
  selectedIds: string[]
  customText: string
}

/**
 * 决策检查点卡片。当 AI 遇到多条可行路径时，在消息气泡内渲染交互式选项卡片。
 * 支持多问题组（Tab 切换），所有组均回答后方可提交。
 * 用户提交后 POST 到 /api/checkpoint/{id}/respond，解除 AI 流的等待阻塞。
 */
export function CheckpointCard({ data, status, className }: CheckpointCardProps) {
  const { t } = useTranslation()
  const setCheckpointSelection = useChatStore((s) => s.setCheckpointSelection)
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [activeGroupId, setActiveGroupId] = useState(() => data.questionGroups[0]?.id ?? '')
  const cardRef = useRef<HTMLDivElement>(null)
  // 首次挂载且未已确认时触发注意力动画 + 自动滚入视野
  const [isNew, setIsNew] = useState(() => status !== 'done' && !(data.groupAnswers != null && data.groupAnswers.length > 0))

  // 从预存的 groupAnswers 初始化答案状态（跨设备/历史场景）
  const [answers, setAnswers] = useState<Map<string, GroupAnswerState>>(() => {
    const map = new Map<string, GroupAnswerState>()
    for (const g of data.questionGroups) {
      const existing = data.groupAnswers?.find((a) => a.groupId === g.id)
      map.set(g.id, {
        selectedIds: existing?.selectedOptionIds ?? [],
        customText: existing?.customText ?? '',
      })
    }
    return map
  })

  // 已确认：后端 status=done、本次已提交成功、或历史/跨设备场景已有选择数据
  const isResolved = status === 'done' || submitted || (data.groupAnswers != null && data.groupAnswers.length > 0)

  // 新卡片挂载时：延迟滚入视野 + 2.8s 后清除光晕动画状态
  useEffect(() => {
    if (!isNew) return
    const scrollTimer = setTimeout(() => {
      cardRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
    }, 200)
    const clearTimer = setTimeout(() => setIsNew(false), 2800)
    return () => { clearTimeout(scrollTimer); clearTimeout(clearTimer) }
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  // 所有问题组均已回答（有选项或有自定义文本）
  const allAnswered = useMemo(() => {
    return data.questionGroups.every((g) => {
      const a = answers.get(g.id)
      return (a?.selectedIds.length ?? 0) > 0 || (a?.customText.trim().length ?? 0) > 0
    })
  }, [answers, data.questionGroups])

  // 倒计时：仅在未决策且有超时时间时运行
  const initTimeout = (data.timeoutSeconds ?? 0) > 0 ? data.timeoutSeconds! : 0
  const [remaining, setRemaining] = useState(initTimeout)
  useEffect(() => {
    if (initTimeout <= 0 || isResolved || remaining <= 0) return
    const id = setTimeout(() => setRemaining((r) => r - 1), 1000)
    return () => clearTimeout(id)
  }, [remaining, isResolved, initTimeout])

  function getGroupAnswer(groupId: string): GroupAnswerState {
    return answers.get(groupId) ?? { selectedIds: [], customText: '' }
  }

  function isGroupAnswered(groupId: string): boolean {
    const a = answers.get(groupId)
    return (a?.selectedIds.length ?? 0) > 0 || (a?.customText.trim().length ?? 0) > 0
  }

  function toggleOption(group: QuestionGroup, optId: string) {
    if (isResolved || submitting) return
    const current = getGroupAnswer(group.id)
    const next = group.allowMultiple
      ? current.selectedIds.includes(optId)
        ? current.selectedIds.filter((id) => id !== optId)
        : [...current.selectedIds, optId]
      : current.selectedIds.includes(optId)
        ? []
        : [optId]
    setAnswers((prev) => new Map(prev).set(group.id, { ...current, selectedIds: next }))
    // 单选且刚选中（非取消），自动切换到下一个问题组
    if (!group.allowMultiple && next.length > 0) {
      const currentIdx = data.questionGroups.findIndex((g) => g.id === group.id)
      if (currentIdx !== -1 && currentIdx < data.questionGroups.length - 1) {
        setActiveGroupId(data.questionGroups[currentIdx + 1].id)
      }
    }
  }

  function updateCustomText(groupId: string, text: string) {
    const current = getGroupAnswer(groupId)
    setAnswers((prev) => new Map(prev).set(groupId, { ...current, customText: text }))
  }

  async function handleSubmit() {
    if (!allAnswered || submitting || isResolved) return
    const groupAnswers: GroupAnswer[] = data.questionGroups.map((g) => {
      const a = answers.get(g.id) ?? { selectedIds: [], customText: '' }
      return {
        groupId: g.id,
        selectedOptionIds: a.selectedIds,
        customText: a.customText.trim() || undefined,
      }
    })
    setSubmitting(true)
    try {
      await respondCheckpoint(data.checkpointId, groupAnswers)
      // 回写 store，确保 status 变为 done 后仍能高亮已选项
      setCheckpointSelection(data.checkpointId, groupAnswers)
      setSubmitted(true)
    } catch {
      // 忽略：超时后后端已返回 404，前端静默处理
    } finally {
      setSubmitting(false)
    }
  }

  // 获取展示用答案：已确认时优先用 data.groupAnswers（来自服务端）
  function getDisplayAnswer(groupId: string): GroupAnswerState {
    if (isResolved && data.groupAnswers) {
      const ga = data.groupAnswers.find((a) => a.groupId === groupId)
      if (ga) return { selectedIds: ga.selectedOptionIds, customText: ga.customText ?? '' }
    }
    return getGroupAnswer(groupId)
  }

  const activeGroup = data.questionGroups.find((g) => g.id === activeGroupId) ?? data.questionGroups[0]

  return (
    <div
      ref={cardRef}
      className={cn(
        'my-3 max-w-xl rounded-xl border border-[color:var(--color-brand-200)]/60 dark:border-[color:var(--color-brand-700)]/40 bg-[color:var(--color-brand-50)]/60 dark:bg-[color:var(--color-brand-900)]/20 overflow-hidden',
        isNew && !isResolved && 'animate-checkpoint-glow',
        className,
      )}
    >
      {/* 标题栏 */}
      <div className="flex items-center gap-2 px-4 py-2 border-b border-[color:var(--color-brand-200)]/40 dark:border-[color:var(--color-brand-700)]/30">
        <span className="text-[color:var(--color-brand-600)] dark:text-[color:var(--color-brand-400)]">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" />
            <line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
        </span>
        <span className="text-sm font-medium text-[color:var(--color-brand-700)] dark:text-[color:var(--color-brand-300)]">
          {isResolved ? t('checkpoint.resolved', '已确认方向') : t('checkpoint.pending', '需要您来决策')}
        </span>
        <div className="ml-auto flex items-center gap-2">
          {!isResolved && initTimeout > 0 && (
            <span className={cn(
              'text-xs tabular-nums',
              remaining <= 10 ? 'text-red-500 dark:text-red-400 font-semibold' : 'text-[var(--color-text-tertiary)]',
            )}>
              {remaining > 0
                ? t('checkpoint.timeout', '{{s}}s 后 AI 自行选择', { s: remaining })
                : t('checkpoint.timeoutExpired', 'AI 已自行选择')}
            </span>
          )}
          {submitting && (
            <span className="text-xs text-[var(--color-text-tertiary)] animate-pulse">{t('checkpoint.submitting', '提交中…')}</span>
          )}
        </div>
      </div>

      {/* Tab 切换栏：问题组大于 1 时显示 */}
      {data.questionGroups.length > 1 && (
        <div className="flex border-b border-[color:var(--color-brand-200)]/40 dark:border-[color:var(--color-brand-700)]/30 px-4 gap-1 pt-2">
          {data.questionGroups.map((g, idx) => {
            const answered = isResolved || isGroupAnswered(g.id)
            const isActive = g.id === activeGroupId
            return (
              <button
                key={g.id}
                type="button"
                onClick={() => setActiveGroupId(g.id)}
                className={cn(
                  'px-3 py-1.5 text-xs font-medium rounded-t-md border-b-2 transition-colors',
                  isActive
                    ? 'border-[color:var(--color-brand-500)] text-[color:var(--color-brand-700)] dark:text-[color:var(--color-brand-300)] bg-[var(--color-surface-0)]'
                    : 'border-transparent text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                )}
              >
                {answered ? (
                  <span className="flex items-center gap-1">
                    <svg width="10" height="10" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="text-green-500">
                      <polyline points="2 6 5 9 10 3" />
                    </svg>
                    {idx + 1}
                  </span>
                ) : idx + 1}
              </button>
            )
          })}
        </div>
      )}

      {/* 当前激活的问题组面板 */}
      {activeGroup && (
        <GroupPanel
          key={activeGroup.id}
          group={activeGroup}
          answer={getDisplayAnswer(activeGroup.id)}
          isResolved={isResolved}
          submitting={submitting}
          onToggleOption={(optId) => toggleOption(activeGroup, optId)}
          onCustomTextChange={(text) => updateCustomText(activeGroup.id, text)}
        />
      )}

      {/* 底部全局确认按钮 */}
      {!isResolved && (
        <div className="px-4 pb-3 flex justify-end">
          <button
            type="button"
            disabled={!allAnswered || submitting}
            onClick={handleSubmit}
            className="rounded-lg bg-[color:var(--color-brand-600)] hover:bg-[color:var(--color-brand-700)] text-white text-sm font-medium px-6 py-2 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {submitting ? t('checkpoint.submitting', '提交中…') : t('checkpoint.confirm', '确认选择')}
          </button>
        </div>
      )}
    </div>
  )
}

// ─── 内部子组件：单个问题组面板 ───────────────────────────────

interface GroupPanelProps {
  group: QuestionGroup
  answer: GroupAnswerState
  isResolved: boolean
  submitting: boolean
  onToggleOption: (optId: string) => void
  onCustomTextChange: (text: string) => void
}

function GroupPanel({ group, answer, isResolved, submitting, onToggleOption, onCustomTextChange }: GroupPanelProps) {
  const { t } = useTranslation()
  const useCompact = group.options.every((o) => !o.description && !o.preview)
  return (
    <div className="px-4 pt-2 pb-3 flex flex-col gap-1.5">
      {/* 问题文本 */}
      <div className="text-sm text-[var(--color-text-primary)] leading-relaxed pb-1">
        {group.question}
      </div>

      {/* 选项列表 */}
      <div className={useCompact ? 'flex flex-wrap gap-2' : 'flex flex-col gap-2'}>
        {group.options.map((opt: CheckpointOption, idx: number) => {
          const isSelected = answer.selectedIds.includes(opt.id)
          return (
            <button
              key={opt.id}
              type="button"
              disabled={isResolved || submitting}
              onClick={() => onToggleOption(opt.id)}
              className={cn(
                useCompact
                  ? 'inline-flex items-center gap-2 rounded-lg border px-3 py-2 transition-all text-sm'
                  : 'w-full text-left rounded-lg border px-4 py-3 transition-all text-sm leading-snug',
                isSelected
                  ? 'border-[color:var(--color-brand-500)] bg-[color:var(--color-brand-100)] dark:bg-[color:var(--color-brand-800)]/40 text-[color:var(--color-brand-700)] dark:text-[color:var(--color-brand-300)] font-medium'
                  : isResolved
                    ? 'border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/30 text-gray-400 dark:text-gray-600 cursor-not-allowed'
                    : 'border-[var(--color-border-subtle)] bg-[var(--color-surface-0)] text-[var(--color-text-primary)] hover:border-[color:var(--color-brand-400)] hover:bg-[color:var(--color-brand-50)] dark:hover:bg-[color:var(--color-brand-900)]/30 cursor-pointer',
              )}
            >
              <span className={cn(
                'flex-shrink-0 w-5 h-5 rounded-full border text-xs flex items-center justify-center font-semibold',
                !useCompact && 'mt-0.5',
                isSelected
                  ? 'border-[color:var(--color-brand-500)] bg-[color:var(--color-brand-500)] text-white'
                  : 'border-[var(--color-border-default)] text-[var(--color-text-tertiary)]',
              )}>
                {isSelected ? (
                  <svg width="10" height="10" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <polyline points="2 6 5 9 10 3" />
                  </svg>
                ) : String.fromCharCode(65 + idx)}
              </span>
              {useCompact ? (
                <span className="font-medium">{opt.label}</span>
              ) : (
                <div className="flex-1 min-w-0">
                  <div className="font-medium">{opt.label}</div>
                  {opt.description && (
                    <div className="mt-0.5 text-xs text-[var(--color-text-secondary)]">{opt.description}</div>
                  )}
                  {opt.preview && (
                    <pre className="mt-2 max-h-32 overflow-auto rounded border border-[var(--color-border-subtle)] bg-[var(--color-surface-1)] px-2 py-1.5 text-[11px] font-mono text-[var(--color-text-secondary)] whitespace-pre-wrap break-words">{opt.preview}</pre>
                  )}
                </div>
              )}
            </button>
          )
        })}
      </div>

      {/* 自定义补充文本 */}
      {isResolved ? (
        answer.customText && (
          <div className="mt-1 rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/30 px-3 py-2 text-sm text-gray-600 dark:text-gray-400 italic">
            {t('checkpoint.customTextLabel', '补充说明：')}{answer.customText}
          </div>
        )
      ) : (
        <textarea
          rows={1}
          disabled={submitting}
          value={answer.customText}
          onChange={(e) => onCustomTextChange(e.target.value)}
          placeholder={t('checkpoint.customTextPlaceholder', '或补充说明您的需求（可选）…')}
          className="mt-1 w-full resize-none rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800/50 px-3 py-2 text-sm text-gray-700 dark:text-gray-300 placeholder-gray-400 dark:placeholder-gray-600 focus:outline-none focus:border-[color:var(--color-brand-400)] transition-colors disabled:opacity-50"
        />
      )}
    </div>
  )
}
