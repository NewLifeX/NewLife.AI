import { useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { MarkdownRenderer } from '@/components/chat/MarkdownRenderer'
import { Icon } from '@/components/common/Icon'
import { fetchSharedConversation, type SharedConversationContent } from '@/lib/api'
import { cn } from '@/lib/utils'

export function SharePage() {
  const { token } = useParams<{ token: string }>()
  const { t } = useTranslation()
  const [data, setData] = useState<SharedConversationContent | null>(null)
  const [loading, setLoading] = useState(true)
  const [notFound, setNotFound] = useState(false)
  const anchorRef = useRef<HTMLDivElement | null>(null)

  const safeTime = (s: string | null | undefined, style: 'time' | 'full' = 'full'): string | null => {
    if (!s) return null
    const d = new Date(s)
    return isNaN(d.getTime()) ? null : style === 'time' ? d.toLocaleTimeString() : d.toLocaleString()
  }

  useEffect(() => {
    if (!token) return
    fetchSharedConversation(token)
      .then((result) => {
        if (result) {
          setData(result)
        } else {
          setNotFound(true)
        }
      })
      .catch(() => setNotFound(true))
      .finally(() => setLoading(false))
  }, [token])

  useEffect(() => {
    if (data) {
      document.title = data.snapshotTitle && data.siteTitle
        ? `${data.snapshotTitle} - ${data.siteTitle}`
        : (data.snapshotTitle || data.siteTitle || t('sharePage.title'))
      if (anchorRef.current) {
        anchorRef.current.scrollIntoView({ behavior: 'smooth', block: 'center' })
      }
    }
  }, [data, t])

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900">
        <div className="flex items-center text-gray-400">
          <Icon name="hourglass_empty" className="animate-spin mr-2" />
          {t('common.loading')}
        </div>
      </div>
    )
  }

  if (notFound || !data) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-gray-50 dark:bg-gray-900">
        <Icon name="link_off" size="xl" className="text-gray-300 dark:text-gray-600 mb-4" />
        <h1 className="text-xl font-bold text-gray-700 dark:text-gray-300 mb-2">{t('sharePage.notFound')}</h1>
        <p className="text-gray-500 dark:text-gray-400">{t('sharePage.notFoundDesc')}</p>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900">
      {/* Header */}
      <header className="sticky top-0 z-10 bg-white/80 dark:bg-gray-900/80 backdrop-blur border-b border-gray-200 dark:border-gray-800">
        <div className="max-w-4xl mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Icon name="share" className="text-primary" />
            <span className="font-semibold text-gray-900 dark:text-white">{data.snapshotTitle || t('sharePage.title')}</span>
          </div>
          <div className="text-xs text-gray-400 text-right">
            <div>
              {t('sharePage.sharedAt')}: {safeTime(data.createTime) ?? '—'}
              {data.expireTime && safeTime(data.expireTime) && ` \u00b7 ${t('sharePage.expiresAt')}: ${safeTime(data.expireTime)}`}
            </div>
            {data.creatorName && (
              <div>{t('sharePage.sharedBy')}：{data.creatorName}</div>
            )}
          </div>
        </div>
      </header>

      {/* Banner */}
      <div className="max-w-4xl mx-auto px-4 pt-4">
        <div className="bg-blue-50 dark:bg-blue-900/20 border border-blue-100 dark:border-blue-800 rounded-lg px-4 py-2 text-sm text-blue-700 dark:text-blue-300 flex items-center gap-2">
          <Icon name="info" size="base" />
          {t('sharePage.readOnlyNotice')}
        </div>
      </div>

      {/* Messages */}
      <div className="max-w-4xl mx-auto px-4 py-6 space-y-4">
        {data.messages.map((msg) => {
          const isUser = msg.role === 'user'
          const isAnchor = msg.id === data.anchorMessageId
          const msgTime = safeTime(msg.createdAt, 'time')
          const roleLabel = isUser
            ? (data.creatorName || t('sharePage.you'))
            : msg.modelName ? `${t('sharePage.assistant')} \u00b7 ${msg.modelName}` : t('sharePage.assistant')
          return (
            <div
              key={msg.id}
              id={`msg-${msg.id}`}
              ref={isAnchor ? anchorRef : undefined}
              className={cn(
                'rounded-xl px-5 py-4',
                isUser
                  ? 'bg-blue-50 dark:bg-blue-900/20 border border-blue-100 dark:border-blue-800'
                  : 'bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700',
                isAnchor && 'ring-2 ring-primary/40',
              )}
            >
              {/* Role header */}
              <div className="flex items-center gap-2 mb-3">
                <div
                  className={cn(
                    'w-6 h-6 rounded-full flex items-center justify-center flex-shrink-0',
                    isUser
                      ? 'bg-blue-500 text-white'
                      : 'bg-gray-200 dark:bg-gray-600 text-gray-700 dark:text-white',
                  )}
                >
                  <Icon name={isUser ? 'person' : 'smart_toy'} size="xs" />
                </div>
                <span
                  className={cn(
                    'text-xs font-semibold',
                    isUser ? 'text-blue-700 dark:text-blue-300' : 'text-gray-600 dark:text-gray-300',
                  )}
                >
                  {roleLabel}
                </span>
                {msgTime && <span className="text-[10px] text-gray-400 ml-auto">{msgTime}</span>}
              </div>
              {/* Content */}
              <div className="ml-8">
                {isUser ? (
                  <div className="whitespace-pre-wrap text-gray-900 dark:text-gray-100">{msg.content}</div>
                ) : (
                  <MarkdownRenderer content={msg.content} />
                )}
              </div>
            </div>
          )
        })}
      </div>

      {/* Footer */}
      <footer className="max-w-4xl mx-auto px-4 pb-8 text-center text-xs text-gray-400">
        {t('common.aiDisclaimer')}
      </footer>
    </div>
  )
}
