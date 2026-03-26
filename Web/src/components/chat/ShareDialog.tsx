import { useState, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { Modal } from '@/components/common/Modal'
import { Icon } from '@/components/common/Icon'
import { createShareLink, revokeShareLink } from '@/lib/api'

interface ShareDialogProps {
  open: boolean
  onClose: () => void
  conversationId: string
}

export function ShareDialog({ open, onClose, conversationId }: ShareDialogProps) {
  const { t } = useTranslation()
  const [shareUrl, setShareUrl] = useState<string | null>(null)
  const [shareToken, setShareToken] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [revoking, setRevoking] = useState(false)
  const [copied, setCopied] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleCreate = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await createShareLink(conversationId)
      // result.url 已包含 /share/{token}，直接拼接 origin
      const fullUrl = `${window.location.origin}${result.url}`
      setShareUrl(fullUrl)
      // 从 url 中提取 token（最后一段路径）
      const token = result.url.split('/').pop() ?? ''
      setShareToken(token)
    } catch {
      setError(t('share.createFailed'))
    } finally {
      setLoading(false)
    }
  }, [conversationId, t])

  const handleRevoke = useCallback(async () => {
    if (!shareToken) return
    if (!confirm(t('share.revokeConfirm'))) return
    setRevoking(true)
    setError(null)
    try {
      await revokeShareLink(shareToken)
      setShareUrl(null)
      setShareToken(null)
    } catch {
      setError(t('share.revokeFailed'))
    } finally {
      setRevoking(false)
    }
  }, [shareToken, t])

  const handleCopy = useCallback(async () => {
    if (!shareUrl) return
    try {
      await navigator.clipboard.writeText(shareUrl)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // fallback for non-HTTPS environments
      const ta = document.createElement('textarea')
      ta.value = shareUrl
      ta.style.position = 'fixed'
      ta.style.opacity = '0'
      document.body.appendChild(ta)
      ta.select()
      document.execCommand('copy')
      document.body.removeChild(ta)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }, [shareUrl])

  const handleClose = useCallback(() => {
    setShareUrl(null)
    setShareToken(null)
    setCopied(false)
    setError(null)
    onClose()
  }, [onClose])

  return (
    <Modal open={open} onClose={handleClose} maxWidth="max-w-md">
      <div className="p-6 space-y-4 w-full">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100 pr-8">{t('share.title')}</h2>
        {!shareUrl ? (
          <>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              {t('share.description')}
            </p>
            {error && (
              <p className="text-sm text-red-500">{error}</p>
            )}
            <button
              onClick={handleCreate}
              disabled={loading}
              className="w-full py-2 px-4 bg-primary text-white rounded-lg hover:bg-blue-600 disabled:opacity-50 transition-colors text-sm font-medium flex items-center justify-center gap-2"
            >
              {loading ? (
                <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              ) : (
                <Icon name="link" size="base" />
              )}
              {t('share.createLink')}
            </button>
          </>
        ) : (
          <>
            <div className="flex items-center gap-2 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
              <input
                type="text"
                readOnly
                value={shareUrl}
                className="flex-1 bg-transparent text-sm text-gray-700 dark:text-gray-300 outline-none select-all"
              />
              <button
                onClick={handleCopy}
                className="flex-shrink-0 p-2 rounded-md hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors"
                title={t('common.copy')}
              >
                <Icon
                  name={copied ? 'check' : 'content_copy'}
                  size="base"
                  className={copied ? 'text-green-500' : 'text-gray-500'}
                />
              </button>
            </div>
            <p className="text-xs text-gray-400 dark:text-gray-500">
              {t('share.linkHint')}
            </p>
            {error && (
              <p className="text-sm text-red-500">{error}</p>
            )}
            <button
              onClick={handleRevoke}
              disabled={revoking}
              className="w-full py-2 px-4 bg-red-50 dark:bg-red-900/20 text-red-600 dark:text-red-400 rounded-lg hover:bg-red-100 dark:hover:bg-red-900/40 disabled:opacity-50 transition-colors text-sm font-medium flex items-center justify-center gap-2"
            >
              {revoking ? (
                <div className="w-4 h-4 border-2 border-red-400/30 border-t-red-400 rounded-full animate-spin" />
              ) : (
                <Icon name="link_off" size="base" />
              )}
              {t('share.revokeLink')}
            </button>
          </>
        )}
      </div>
    </Modal>
  )
}
