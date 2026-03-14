import { useEffect, useState } from 'react'
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
            <span className="font-semibold text-gray-900 dark:text-white">{t('sharePage.title')}</span>
          </div>
          <div className="text-xs text-gray-400">
            {t('sharePage.sharedAt')}: {new Date(data.createTime).toLocaleString()}
            {data.expireTime && ` · ${t('sharePage.expiresAt')}: ${new Date(data.expireTime).toLocaleString()}`}
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
      <div className="max-w-4xl mx-auto px-4 py-6 space-y-6">
        {data.messages.map((msg) => (
          <div
            key={msg.id}
            className={cn(
              'flex',
              msg.role === 'user' ? 'justify-end' : 'justify-start',
            )}
          >
            <div
              className={cn(
                'max-w-[85%] rounded-2xl px-4 py-3',
                msg.role === 'user'
                  ? 'bg-primary text-white rounded-br-sm'
                  : 'bg-white dark:bg-gray-800 border border-gray-100 dark:border-gray-700 rounded-bl-sm',
              )}
            >
              {msg.thinkingContent && (
                <details className="mb-2">
                  <summary className="text-xs opacity-60 cursor-pointer">{t('chat.thinkingProcess')}</summary>
                  <div className="mt-1 text-xs opacity-70 whitespace-pre-wrap">{msg.thinkingContent}</div>
                </details>
              )}
              {msg.role === 'assistant' ? (
                <MarkdownRenderer content={msg.content} />
              ) : (
                <div className="whitespace-pre-wrap">{msg.content}</div>
              )}
              <div
                className={cn(
                  'text-[10px] mt-1',
                  msg.role === 'user' ? 'text-white/60' : 'text-gray-400',
                )}
              >
                {new Date(msg.createdAt).toLocaleTimeString()}
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Footer */}
      <footer className="max-w-4xl mx-auto px-4 pb-8 text-center text-xs text-gray-400">
        {t('common.aiDisclaimer')}
      </footer>
    </div>
  )
}
