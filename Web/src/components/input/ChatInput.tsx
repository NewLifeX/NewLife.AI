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

interface ChatInputProps {
  onSend: (message: string) => void
  onStop?: () => void
  isGenerating?: boolean
  attachments?: Attachment[]
  onAttachmentRemove?: (id: string) => void
  onAttachmentAdd?: () => void
  onFilePaste?: (file: File) => void
  thinkingMode?: ThinkingMode
  onThinkingModeChange?: (mode: ThinkingMode) => void
  showThinkingToggle?: boolean
  sendShortcut?: 'Enter' | 'Ctrl+Enter'
  className?: string
}

export function ChatInput({
  onSend,
  onStop,
  isGenerating = false,
  attachments = [],
  onAttachmentRemove,
  onAttachmentAdd,
  onFilePaste,
  thinkingMode = 'auto',
  onThinkingModeChange,
  showThinkingToggle = false,
  sendShortcut = 'Enter',
  className,
}: ChatInputProps) {
  const { t } = useTranslation()
  const [value, setValue] = useState('')

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
    <div className={cn('w-full', className)}>
      <div className="w-full max-w-3xl mx-auto relative group">
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

