import { useRef, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import { ChatInput } from '@/components/input/ChatInput'
import { useSettingsStore } from '@/stores'
import type { Attachment } from '@/types'
import type { SuggestedQuestion } from '@/lib/api'

interface WelcomePageProps {
  onSend: (message: string) => void
  siteTitle?: string
  welcomeMessage?: string
  welcomeSubtitle?: string
  suggestedQuestions?: SuggestedQuestion[]
  attachments?: Attachment[]
  onAttachmentAdd?: (file: File) => void
  onAttachmentRemove?: (id: number) => void
  prefillValue?: string
  onPrefillConsumed?: () => void
}

export function WelcomePage({ onSend, siteTitle, welcomeMessage, welcomeSubtitle, suggestedQuestions, attachments = [], onAttachmentAdd, onAttachmentRemove, prefillValue, onPrefillConsumed }: WelcomePageProps) {
  const { t } = useTranslation()
  const sendShortcut = useSettingsStore((s) => s.sendShortcut)
  const contentWidth = useSettingsStore((s) => s.contentWidth)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const handleAttachClick = useCallback(() => {
    fileInputRef.current?.click()
  }, [])

  const handleFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (files) {
      Array.from(files).forEach((f) => onAttachmentAdd?.(f))
    }
    e.target.value = ''
  }, [onAttachmentAdd])

  const defaultSuggestions = [
    { icon: 'bolt', label: t('welcome.quick'), color: 'text-yellow-500' },
    { icon: 'image', label: t('welcome.imageGen'), color: 'text-pink-500' },
    { icon: 'code', label: t('welcome.coding'), color: 'text-blue-500' },
    { icon: 'edit_note', label: t('welcome.writing'), color: 'text-green-500' },
    { icon: 'travel_explore', label: t('welcome.research'), color: 'text-purple-500' },
    { icon: 'smart_display', label: t('welcome.videoGen'), color: 'text-red-500' },
  ]

  const hasQuestions = suggestedQuestions && suggestedQuestions.length > 0

  return (
    <>
      <div className="flex-1 overflow-y-auto custom-scrollbar px-4 md:px-0">
        <div className={`${(contentWidth ?? 960) >= 1200 ? 'max-w-5xl' : (contentWidth ?? 960) < 960 ? 'max-w-3xl' : 'max-w-4xl'} mx-auto w-full flex flex-col items-center justify-center h-full relative`}>
          {/* 极光光晕背景装饰 */}
          <div className="aurora-blob -z-0" aria-hidden="true" />

          <div className="text-center mb-12 relative z-10">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-[image:var(--gradient-brand)] mb-6 shadow-[0_12px_32px_-8px_rgba(16,185,129,0.55)]">
              <span className="text-white text-2xl font-bold">N</span>
            </div>
            {siteTitle && (
              <p className="text-lg font-semibold text-[var(--color-text-secondary)] mb-1 tracking-wide">
                {siteTitle}
              </p>
            )}
            <h1 className="text-3xl font-bold mb-2 tracking-tight text-gradient-brand">
              {welcomeMessage || t('welcome.greeting')}
            </h1>
            <p className="text-[var(--color-text-tertiary)] text-sm">
              {welcomeSubtitle || t('welcome.subtitle')}
            </p>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-3 gap-3 mb-8 w-full max-w-2xl relative z-10">
            {hasQuestions
              ? suggestedQuestions!.map((q) => (
                  <button
                    key={q.question}
                    onClick={() => onSend(q.question)}
                    title={q.title && q.question !== q.title ? q.question : undefined}
                    className="group flex items-center space-x-2 px-4 py-3 bg-[var(--color-surface-0)]/70 backdrop-blur-sm border border-[var(--color-border-subtle)] rounded-xl hover:border-[color:var(--color-brand-300)]/60 dark:hover:border-[color:var(--color-brand-500)]/50 hover:shadow-[0_8px_24px_-12px_rgba(16,185,129,0.45)] hover:-translate-y-0.5 transition-[transform,box-shadow,border-color,background-color] duration-200 ease-out text-sm text-left text-[var(--color-text-secondary)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/55"
                  >
                    <Icon name={q.icon || 'chat_bubble_outline'} className={`${q.color || 'text-[color:var(--color-brand-500)]'} group-hover:scale-110 transition-transform flex-shrink-0`} />
                    <span className="line-clamp-2 group-hover:text-[var(--color-text-primary)] transition-colors">{q.title || q.question}</span>
                  </button>
                ))
              : defaultSuggestions.map((s) => (
                  <button
                    key={s.label}
                    onClick={() => onSend(t('welcome.useFeature', { feature: s.label }))}
                    className="group flex items-center space-x-2 px-4 py-3 bg-[var(--color-surface-0)]/70 backdrop-blur-sm border border-[var(--color-border-subtle)] rounded-xl hover:border-[color:var(--color-brand-300)]/60 dark:hover:border-[color:var(--color-brand-500)]/50 hover:shadow-[0_8px_24px_-12px_rgba(16,185,129,0.45)] hover:-translate-y-0.5 transition-[transform,box-shadow,border-color,background-color] duration-200 ease-out text-sm text-[var(--color-text-secondary)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[color:var(--color-brand-500)]/55"
                  >
                    <Icon name={s.icon} className={`${s.color} group-hover:scale-110 transition-transform flex-shrink-0`} />
                    <span className="group-hover:text-[var(--color-text-primary)] transition-colors">{s.label}</span>
                  </button>
                ))}
          </div>
        </div>
      </div>

      <div className="relative z-20 pb-6 pt-2 px-4 bg-gradient-to-t from-[var(--color-surface-0)] via-[var(--color-surface-0)]/95 to-transparent">
        <input
          ref={fileInputRef}
          type="file"
          multiple
          className="hidden"
          accept=".jpg,.jpeg,.png,.gif,.webp,.pdf,.docx,.doc,.xls,.xlsx,.ppt,.pptx,.txt,.md,.csv"
          onChange={handleFileChange}
        />
        <ChatInput
          onSend={onSend}
          sendShortcut={sendShortcut}
          attachments={attachments}
          onAttachmentAdd={handleAttachClick}
          onAttachmentRemove={onAttachmentRemove}
          onFilePaste={onAttachmentAdd}
          prefillValue={prefillValue}
          onPrefillConsumed={onPrefillConsumed}
        />
      </div>
    </>
  )
}
