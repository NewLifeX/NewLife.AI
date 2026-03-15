import { useState, useCallback, useEffect, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Textarea } from '@/components/atoms/Textarea'
import { IconButton } from '@/components/atoms/IconButton'
import { SkillBar, type SkillItem } from './SkillBar'
import { SkillPopover } from './SkillPopover'
import { AttachmentChip } from './AttachmentChip'
import { ThinkingModeToggle, type ThinkingMode } from './ThinkingModeToggle'
import { fetchUserSkills, fetchAllSkills, type SkillInfo } from '@/lib/api'
import type { Attachment } from '@/types'

interface ChatInputProps {
  onSend: (message: string, skillCode?: string) => void
  onStop?: () => void
  isGenerating?: boolean
  skills?: SkillItem[]
  attachments?: Attachment[]
  onAttachmentRemove?: (id: string) => void
  onAttachmentAdd?: () => void
  thinkingMode?: ThinkingMode
  onThinkingModeChange?: (mode: ThinkingMode) => void
  showThinkingToggle?: boolean
  sendShortcut?: 'Enter' | 'Ctrl+Enter'
  className?: string
}

function useDefaultSkills(): SkillItem[] {
  const { t } = useTranslation()
  return [
    { id: 'attach', icon: 'attach_file', label: '' },
    { id: 'general', icon: 'smart_toy', label: t('welcome.quick') },
    { id: 'coder', icon: 'code', label: t('welcome.coding') },
    { id: 'writer', icon: 'edit_note', label: t('welcome.writing') },
    { id: 'more', icon: 'grid_view', label: t('sidebar.more') },
  ]
}

/** 将后端 SkillInfo 转为前端 SkillItem */
function toSkillItem(s: SkillInfo, activeCode?: string): SkillItem {
  return {
    id: s.code,
    icon: s.icon || 'smart_toy',
    label: s.name,
    active: s.code === activeCode,
  }
}

export function ChatInput({
  onSend,
  onStop,
  isGenerating = false,
  skills,
  attachments = [],
  onAttachmentRemove,
  onAttachmentAdd,
  thinkingMode = 'auto',
  onThinkingModeChange,
  showThinkingToggle = false,
  sendShortcut = 'Enter',
  className,
}: ChatInputProps) {
  const { t } = useTranslation()
  const defaultSkills = useDefaultSkills()
  const [serverSkills, setServerSkills] = useState<SkillItem[] | null>(null)
  const [allSkills, setAllSkills] = useState<SkillInfo[]>([])
  const [activeSkillCode, setActiveSkillCode] = useState<string | undefined>()
  const [value, setValue] = useState('')
  const [showSkillPopover, setShowSkillPopover] = useState(false)
  const [atTriggerMode, setAtTriggerMode] = useState(false)

  // 从后端加载 SkillBar 技能列表
  useEffect(() => {
    fetchUserSkills()
      .then((list) => {
        if (list.length > 0) {
          const items: SkillItem[] = [
            { id: 'attach', icon: 'attach_file', label: '' },
            ...list.map((s) => toSkillItem(s, activeSkillCode)),
            { id: 'more', icon: 'grid_view', label: t('sidebar.more') },
          ]
          setServerSkills(items)
        }
      })
      .catch(() => { /* 静默回退到默认技能 */ })
  }, []) // eslint-disable-line react-hooks/exhaustive-deps

  // 更新技能选中态
  useEffect(() => {
    if (serverSkills) {
      setServerSkills((prev) =>
        prev?.map((s) => ({
          ...s,
          active: s.id !== 'attach' && s.id !== 'more' && s.id === activeSkillCode,
        })) ?? null,
      )
    }
  }, [activeSkillCode]) // eslint-disable-line react-hooks/exhaustive-deps

  const resolvedSkills = skills ?? serverSkills ?? defaultSkills

  const MAX_LENGTH = 6000
  const isOverLimit = value.length > MAX_LENGTH

  const handleSend = useCallback(() => {
    const trimmed = value.trim()
    if (!trimmed || isGenerating || trimmed.length > MAX_LENGTH) return
    onSend(trimmed, activeSkillCode)
    setValue('')
  }, [value, isGenerating, onSend, activeSkillCode])

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (sendShortcut === 'Ctrl+Enter') {
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
        e.preventDefault()
        handleSend()
      }
    } else if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
    if (e.key === '/' && value === '') {
      setShowSkillPopover(true)
    }
    if (e.key === 'Escape') {
      setShowSkillPopover(false)
      setAtTriggerMode(false)
    }
  }

  const handleChange = (v: string) => {
    setValue(v)
    // @ 技能补全触发逻辑
    const atMatch = v.match(/@([\w\u4e00-\u9fff]*)$/)
    if (atMatch) {
      setAtTriggerMode(true)
      if (allSkills.length === 0) {
        fetchAllSkills().then(setAllSkills).catch(() => {})
      }
      setShowSkillPopover(true)
      return
    }
    // / 触发逻辑
    if (v === '/' && !showSkillPopover) {
      setShowSkillPopover(true)
    } else if (!v.startsWith('/')) {
      if (atTriggerMode) setAtTriggerMode(false)
      setShowSkillPopover(false)
    }
  }

  const handleSkillSelect = (id: string) => {
    setShowSkillPopover(false)
    if (id === 'attach' || id === 'attachment') {
      onAttachmentAdd?.()
      return
    }
    if (atTriggerMode) {
      // @ 模式：将末尾的 @xxx 替换为 @技能名 + 空格
      setAtTriggerMode(false)
      const skillInfo = allSkills.find((s) => s.code === id)
      const skillName = skillInfo?.name ?? id
      setValue((prev) => prev.replace(/@[\w\u4e00-\u9fff]*$/, `@${skillName} `))
      return
    }
    // SkillBar 点击或 / 模式：切换激活技能码
    setActiveSkillCode((prev) => (prev === id ? undefined : id))
  }

  // 加载全部技能供 SkillPopover 展示
  const handleOpenSkillPopover = useCallback(() => {
    if (allSkills.length === 0) {
      fetchAllSkills()
        .then(setAllSkills)
        .catch(() => {})
    }
    setShowSkillPopover((prev) => !prev)
  }, [allSkills.length])

  // 构建 SkillPopover 选项
  const popoverOptions = allSkills.length > 0
    ? allSkills.map((s) => ({
        id: s.code,
        icon: s.icon || 'smart_toy',
        iconBg: 'bg-blue-100 dark:bg-blue-900/40',
        iconColor: 'text-primary dark:text-blue-400',
        label: s.name,
        description: s.description,
      }))
    : undefined

  return (
    <div className={cn('w-full', className)}>
      <div className="w-full max-w-3xl mx-auto relative group">
        <SkillPopover
          open={showSkillPopover}
          onSelect={handleSkillSelect}
          onClose={() => setShowSkillPopover(false)}
          options={popoverOptions}
        />

        <div
          className={cn(
            'bg-white dark:bg-gray-800',
            'border border-gray-200 dark:border-gray-700',
            'group-focus-within:border-primary/40 dark:group-focus-within:border-primary/40',
            'rounded-2xl shadow-input dark:shadow-none',
            'transition-all duration-200 p-3 pb-2 relative',
          )}
        >
          {attachments.length > 0 && (
            <div className="flex items-center gap-2 px-2 pb-2 mb-1 overflow-x-auto no-scrollbar">
              {attachments.map((att) => (
                <AttachmentChip
                  key={att.id}
                  attachment={att}
                  onRemove={() => onAttachmentRemove?.(att.id)}
                />
              ))}
            </div>
          )}

          <div className="flex items-start">
            {showSkillPopover && (
              <div className="py-3 px-1 text-base text-primary font-bold">
                {atTriggerMode ? '@' : '/'}
              </div>
            )}
            <Textarea
              value={value}
              onChange={handleChange}
              placeholder={t('chat.placeholder')}
              minRows={1}
              maxRows={8}
              className="py-3 px-2"
              onKeyDown={handleKeyDown}
            />
          </div>

          <div className="flex items-center justify-between mt-1 px-1">
            <SkillBar
              skills={resolvedSkills}
              onSkillClick={(id) => {
                if (id === 'attach') onAttachmentAdd?.()
                else if (id === 'more') handleOpenSkillPopover()
                else setActiveSkillCode((prev) => (prev === id ? undefined : id))
              }}
            />
            <div className="flex items-center gap-2 flex-shrink-0">
              {showThinkingToggle && onThinkingModeChange && (
                <ThinkingModeToggle mode={thinkingMode} onChange={onThinkingModeChange} />
              )}
              {!isGenerating && (
                <IconButton icon="mic" size="sm" variant="ghost" label={t('chat.voiceInput')} />
              )}
              <button
                onClick={isGenerating ? onStop : handleSend}
                disabled={!isGenerating && (!value.trim() || isOverLimit)}
                className={cn(
                  'w-8 h-8 rounded-full flex items-center justify-center transition-all shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 flex-shrink-0',
                  isGenerating
                    ? 'bg-gray-900 dark:bg-white hover:bg-gray-700 dark:hover:bg-gray-200 text-white dark:text-gray-900'
                    : value.trim() && !isOverLimit
                      ? 'bg-primary hover:bg-blue-600 text-white'
                      : 'bg-gray-100 dark:bg-gray-700 text-gray-400 cursor-not-allowed',
                )}
                title={isGenerating ? t('chat.stopGen') : undefined}
                aria-label={isGenerating ? t('chat.stopGen') : undefined}
              >
                <Icon name={isGenerating ? 'stop' : 'arrow_upward'} variant="filled" size="base" />
              </button>
            </div>
          </div>
        </div>

        {isOverLimit && (
          <div className="text-xs text-red-500 text-right mt-1 mr-2">
            {t('chat.charLimit', { current: value.length, max: MAX_LENGTH })}
          </div>
        )}

        <div className="text-center mt-2">
          <p className="text-[10px] text-gray-400">{t('common.aiDisclaimer')}</p>
        </div>
      </div>
    </div>
  )
}
