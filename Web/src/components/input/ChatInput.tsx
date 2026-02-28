import { useState, useCallback, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Textarea } from '@/components/atoms/Textarea'
import { IconButton } from '@/components/atoms/IconButton'
import { SkillBar, type SkillItem } from './SkillBar'
import { SkillPopover } from './SkillPopover'
import { AttachmentChip } from './AttachmentChip'
import { ThinkingModeToggle, type ThinkingMode } from './ThinkingModeToggle'
import type { Attachment } from '@/types'

interface ChatInputProps {
  onSend: (message: string) => void
  onStop?: () => void
  isGenerating?: boolean
  skills?: SkillItem[]
  attachments?: Attachment[]
  onAttachmentRemove?: (id: string) => void
  onAttachmentAdd?: () => void
  thinkingMode?: ThinkingMode
  onThinkingModeChange?: (mode: ThinkingMode) => void
  showThinkingToggle?: boolean
  className?: string
}

function useDefaultSkills(): SkillItem[] {
  const { t } = useTranslation()
  return [
    { id: 'attach', icon: 'attach_file', label: '' },
    { id: 'fast', icon: 'bolt', label: t('welcome.quick') },
    { id: 'image', icon: 'image', label: t('welcome.imageGen') },
    { id: 'code', icon: 'code', label: t('welcome.coding') },
    { id: 'write', icon: 'edit_note', label: t('welcome.writing') },
    { id: 'research', icon: 'travel_explore', label: t('welcome.research') },
    { id: 'video', icon: 'smart_display', label: t('welcome.videoGen') },
    { id: 'more', icon: 'grid_view', label: t('sidebar.more') },
  ]
}

export function ChatInput({
  onSend,
  onStop,
  isGenerating = false,
  skills,
  attachments = [],
  onAttachmentRemove,
  onAttachmentAdd,
  thinkingMode = 'balanced',
  onThinkingModeChange,
  showThinkingToggle = false,
  className,
}: ChatInputProps) {
  const { t } = useTranslation()
  const defaultSkills = useDefaultSkills()
  const resolvedSkills = skills ?? defaultSkills
  const [value, setValue] = useState('')
  const [showSkillPopover, setShowSkillPopover] = useState(false)

  const handleSend = useCallback(() => {
    const trimmed = value.trim()
    if (!trimmed || isGenerating) return
    onSend(trimmed)
    setValue('')
  }, [value, isGenerating, onSend])

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
    if (e.key === '/' && value === '') {
      setShowSkillPopover(true)
    }
    if (e.key === 'Escape') {
      setShowSkillPopover(false)
    }
  }

  const handleChange = (v: string) => {
    setValue(v)
    if (v === '/' && showSkillPopover === false) {
      setShowSkillPopover(true)
    } else if (!v.startsWith('/')) {
      setShowSkillPopover(false)
    }
  }

  const handleSkillSelect = (id: string) => {
    setShowSkillPopover(false)
    if (id === 'attach' || id === 'attachment') {
      onAttachmentAdd?.()
    }
  }

  return (
    <div className={cn('w-full', className)}>
      <div className="w-full max-w-3xl mx-auto relative group">
        {showThinkingToggle && onThinkingModeChange && (
          <div className="absolute left-0 -top-12 z-30">
            <ThinkingModeToggle mode={thinkingMode} onChange={onThinkingModeChange} />
          </div>
        )}

        <SkillPopover
          open={showSkillPopover}
          onSelect={handleSkillSelect}
          onClose={() => setShowSkillPopover(false)}
        />

        {isGenerating && (
          <div className="absolute right-3 bottom-3 z-30">
            <IconButton
              icon="stop"
              variant="filled"
              size="md"
              label={t('chat.stopGen')}
              onClick={onStop}
            />
          </div>
        )}

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
              <div className="py-3 px-1 text-base text-primary font-bold">/</div>
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

          <div className={cn(
            'flex items-center justify-between mt-1 px-1',
            isGenerating && 'pr-10',
          )}>
            <SkillBar
              skills={resolvedSkills}
              onSkillClick={(id) => {
                if (id === 'attach') onAttachmentAdd?.()
              }}
            />
            {!isGenerating && (
              <div className="flex items-center space-x-2 flex-shrink-0">
                <IconButton icon="mic" size="sm" variant="ghost" label={t('chat.voiceInput')} />
                <button
                  onClick={handleSend}
                  disabled={!value.trim()}
                  className={cn(
                    'w-8 h-8 rounded-full flex items-center justify-center transition-all shadow-sm',
                    value.trim()
                      ? 'bg-primary hover:bg-blue-600 text-white'
                      : 'bg-gray-100 dark:bg-gray-700 text-gray-400 cursor-not-allowed',
                  )}
                >
                  <Icon name="arrow_upward" variant="filled" size="sm" />
                </button>
              </div>
            )}
          </div>
        </div>

        <div className="text-center mt-2">
          <p className="text-[10px] text-gray-400">{t('common.aiDisclaimer')}</p>
        </div>
      </div>
    </div>
  )
}
