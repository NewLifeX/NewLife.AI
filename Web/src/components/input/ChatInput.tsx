import { useState, useCallback, useEffect, useRef, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { Icon } from '@/components/common/Icon'
import { Textarea } from '@/components/atoms/Textarea'
import { IconButton } from '@/components/atoms/IconButton'
import { AttachmentChip } from './AttachmentChip'
import { ThinkingModeToggle, type ThinkingMode } from './ThinkingModeToggle'
import { extractImagesFromClipboard, extractFilesFromDrop } from '@/lib/clipboard'
import type { Attachment } from '@/types'
import { useSettingsStore } from '@/stores'

interface ChatInputProps {
  onSend: (message: string) => void
  onStop?: () => void
  isGenerating?: boolean
  disabled?: boolean
  readonlyReason?: string
  attachments?: Attachment[]
  onAttachmentRemove?: (id: number) => void
  onAttachmentAdd?: () => void
  onFilePaste?: (file: File) => void
  thinkingMode?: ThinkingMode
  onThinkingModeChange?: (mode: ThinkingMode) => void
  showThinkingToggle?: boolean
  sendShortcut?: 'Enter' | 'Ctrl+Enter'
  prefillValue?: string
  onPrefillConsumed?: () => void
  className?: string
}

export function ChatInput({
  onSend,
  onStop,
  isGenerating = false,
  disabled = false,
  readonlyReason,
  attachments = [],
  onAttachmentRemove,
  onAttachmentAdd,
  onFilePaste,
  thinkingMode = 'auto',
  onThinkingModeChange,
  showThinkingToggle = false,
  sendShortcut = 'Enter',
  prefillValue,
  onPrefillConsumed,
  className,
}: ChatInputProps) {
  const { t } = useTranslation()
  const contentWidth = useSettingsStore((s) => s.contentWidth)
  const widthClass = contentWidth === 'narrow' ? 'max-w-2xl' : contentWidth === 'wide' ? 'max-w-5xl' : 'max-w-3xl'
  const [value, setValue] = useState('')

  // 预设提示词自动填入输入框
  useEffect(() => {
    if (prefillValue) {
      setValue(prefillValue)
      onPrefillConsumed?.()
    }
  }, [prefillValue, onPrefillConsumed])

  const MAX_LENGTH = 6000
  const isOverLimit = value.length > MAX_LENGTH

  // 粘贴图片 + 拖拽上传：使用原生 DOM 事件，直接挂载到 textarea 元素，
  // 绕过 React 合成事件层，确保在各种浏览器/扩展环境下可靠触发。
  const inputAreaRef = useRef<HTMLDivElement>(null)
  const onFilePasteRef = useRef(onFilePaste)
  onFilePasteRef.current = onFilePaste

  useEffect(() => {
    const el = inputAreaRef.current?.querySelector('textarea')
    if (!el) return

    const handleNativePaste = (e: ClipboardEvent) => {
      const images = extractImagesFromClipboard(e.clipboardData)
      if (images.length > 0) {
        e.preventDefault()
        images.forEach((f) => onFilePasteRef.current?.(f))
      }
    }

    const handleDragOver = (e: DragEvent) => {
      e.preventDefault()
    }

    const handleDrop = (e: DragEvent) => {
      e.preventDefault()
      e.stopPropagation()
      const files = extractFilesFromDrop(e.dataTransfer)
      files.forEach((f) => onFilePasteRef.current?.(f))
    }

    el.addEventListener('paste', handleNativePaste)
    el.addEventListener('dragover', handleDragOver)
    el.addEventListener('drop', handleDrop)
    return () => {
      el.removeEventListener('paste', handleNativePaste)
      el.removeEventListener('dragover', handleDragOver)
      el.removeEventListener('drop', handleDrop)
    }
  }, [])

  const handleSend = useCallback(() => {
    const trimmed = value.trim()
    if (!trimmed || isGenerating || trimmed.length > MAX_LENGTH) return
    onSend(trimmed)
    setValue('')
  }, [value, isGenerating, onSend])

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
  }

  return (
    <div className={cn('w-full', className)} data-testid="chat-input">
      <div className={cn('w-full mx-auto relative group', widthClass)}>
        <div
          className={cn(
            'bg-[var(--color-surface-0)]',
            'border border-[var(--color-border-default)]',
            'group-focus-within:border-primary/40 dark:group-focus-within:border-primary/40',
            'group-focus-within:shadow-[0_4px_24px_-8px_rgba(91,91,255,0.22),0_2px_6px_rgba(15,23,42,0.06)]',
            'rounded-2xl shadow-input',
            'transition-all duration-200 p-3 pb-2 relative',
            'max-md:p-2 max-md:pb-1.5',
            disabled && 'opacity-50 pointer-events-none',
          )}
        >
          {disabled && readonlyReason && (
            <div className="absolute inset-0 z-10 flex items-center justify-center rounded-2xl bg-[var(--color-surface-0)]/60 backdrop-blur-[1px]">
              <div className="flex items-center gap-2 text-sm text-[var(--color-text-tertiary)]">
                <Icon name="lock" size="sm" />
                <span>{readonlyReason}</span>
              </div>
            </div>
          )}
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

          <div ref={inputAreaRef} className="flex items-start">
            <Textarea
              value={value}
              onChange={setValue}
              placeholder={t('chat.placeholder')}
              minRows={1}
              maxRows={8}
              className="py-3 px-2"
              onKeyDown={handleKeyDown}
            />
          </div>

          <div className="flex items-center justify-between mt-1 px-1">
            <IconButton
              icon="attach_file"
              size="sm"
              variant="ghost"
              label={t('chat.attach')}
              onClick={() => onAttachmentAdd?.()}
            />
            <div className="flex items-center gap-2 flex-shrink-0">
              {showThinkingToggle && onThinkingModeChange && (
                <ThinkingModeToggle mode={thinkingMode} onChange={onThinkingModeChange} />
              )}
              <button
                onClick={isGenerating ? onStop : handleSend}
                disabled={!isGenerating && (!value.trim() || isOverLimit)}
                data-testid="send-button"
                className={cn(
                  'w-8 h-8 rounded-full flex items-center justify-center transition-[transform,box-shadow,background-color] duration-200 shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 flex-shrink-0',
                  isGenerating
                    ? 'bg-gray-900 dark:bg-white hover:bg-gray-700 dark:hover:bg-gray-200 text-white dark:text-gray-900'
                    : value.trim() && !isOverLimit
                      ? 'bg-[image:var(--gradient-brand)] text-white shadow-[0_4px_12px_-2px_rgba(91,91,255,0.5)] hover:-translate-y-px active:translate-y-0 hover:shadow-[0_8px_16px_-4px_rgba(91,91,255,0.55)]'
                      : 'bg-[var(--color-surface-2)] text-[var(--color-text-tertiary)] cursor-not-allowed',
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
          <p className="text-[10px] text-[var(--color-text-tertiary)]">{t('common.aiDisclaimer')}</p>
        </div>
      </div>
    </div>
  )
}

