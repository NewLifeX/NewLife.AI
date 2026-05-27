import { useState, useCallback, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Modal } from '@/components/common/Modal'
import { Icon } from '@/components/common/Icon'
import { createShareLink, revokeShareLink, fetchSystemConfig } from '@/lib/api'
import { useChatStore } from '@/stores'

const QUICK_PRESETS = [
  { min: 10, zh: '10分', en: '10m' },
  { min: 30, zh: '30分', en: '30m' },
  { min: 120, zh: '2时', en: '2h' },
  { min: 1440, zh: '1天', en: '1d' },
  { min: 10080, zh: '1周', en: '1wk' },
  { min: 43200, zh: '1月', en: '1mo' },
  { min: 525600, zh: '1年', en: '1yr' },
]
const TICKS = [
  { min: 10, label: '10分', labelEn: '10m' },
  { min: 60, label: '1时', labelEn: '1h' },
  { min: 360, label: '6时', labelEn: '6h' },
  { min: 1440, label: '1天', labelEn: '1d' },
  { min: 10080, label: '1周', labelEn: '1wk' },
  { min: 43200, label: '1月', labelEn: '1mo' },
  { min: 525600, label: '1年', labelEn: '1yr' },
]
const LOG_MIN = Math.log(10)
const LOG_MAX = Math.log(525600)

function minutesToSlider(minutes: number): number {
  return ((Math.log(Math.max(10, minutes)) - LOG_MIN) / (LOG_MAX - LOG_MIN)) * 100
}

function sliderToMinutes(value: number): number {
  const raw = Math.exp(LOG_MIN + (value / 100) * (LOG_MAX - LOG_MIN))
  if (raw < 60) return Math.max(10, Math.round(raw))
  if (raw < 1440) return Math.round(raw / 30) * 30
  return Math.round(raw / 1440) * 1440
}

function fmtDur(minutes: number, lang: string): string {
  const zh = lang.startsWith('zh')
  if (minutes < 60) return zh ? `${minutes} 分钟` : `${minutes} min`
  if (minutes < 1440) {
    const h = minutes / 60
    const hs = h % 1 === 0 ? `${h}` : h.toFixed(1)
    return zh ? `${hs} 小时` : `${hs} hr${h !== 1 ? 's' : ''}`
  }
  if (minutes < 43200) {
    const d = minutes / 1440
    const ds = d % 1 === 0 ? `${d}` : d.toFixed(1)
    return zh ? `${ds} 天` : `${ds} day${d !== 1 ? 's' : ''}`
  }
  if (minutes < 525600) {
    const mo = Math.round(minutes / 43200)
    return zh ? `${mo} 月` : `${mo} mo`
  }
  return zh ? '1 年' : '1 yr'
}

interface ShareDialogProps {
  open: boolean
  onClose: () => void
  conversationId: string
}

export function ShareDialog({ open, onClose, conversationId }: ShareDialogProps) {
  const { t, i18n } = useTranslation()
  const [shareUrl, setShareUrl] = useState<string | null>(null)
  const [shareToken, setShareToken] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [revoking, setRevoking] = useState(false)
  const [copiedUrl, setCopiedUrl] = useState(false)
  const [copiedText, setCopiedText] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [expireMinutes, setExpireMinutes] = useState<number>(30)

  const convTitle = useChatStore((s) => s.conversations.find((c) => c.id === conversationId)?.title ?? '')

  // 打开时拉取默认有效期
  useEffect(() => {
    if (!open) return
    fetchSystemConfig()
      .then((cfg) => {
        const val = cfg.shareExpireMinutes ?? 0
        setExpireMinutes(val > 0 ? sliderToMinutes(minutesToSlider(val)) : 30)
      })
      .catch(() => {})
  }, [open])

  const writeText = useCallback(async (text: string) => {
    try {
      await navigator.clipboard.writeText(text)
    } catch {
      const ta = document.createElement('textarea')
      ta.value = text
      ta.style.position = 'fixed'
      ta.style.opacity = '0'
      document.body.appendChild(ta)
      ta.select()
      document.execCommand('copy')
      document.body.removeChild(ta)
    }
  }, [])

  const handleCreate = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await createShareLink(conversationId, expireMinutes)
      // result.url 已包含 /share/{token}，直接拼接 origin
      const fullUrl = `${window.location.origin}${result.url}`
      setShareUrl(fullUrl)
      // 从 url 中提取 token（最后一段路径）
      const token = result.url.split('/').pop() ?? ''
      setShareToken(token)
      // 创建成功后自动复制：有标题则复制"标题\nURL"，否则复制纯 URL
      if (convTitle) {
        await writeText(`${convTitle}\n${fullUrl}`)
        setCopiedText(true)
        setTimeout(() => setCopiedText(false), 2000)
      } else {
        await writeText(fullUrl)
        setCopiedUrl(true)
        setTimeout(() => setCopiedUrl(false), 2000)
      }
    } catch {
      setError(t('share.createFailed'))
    } finally {
      setLoading(false)
    }
  }, [conversationId, expireMinutes, convTitle, writeText, t])

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

  const handleCopyUrl = useCallback(async () => {
    if (!shareUrl) return
    await writeText(shareUrl)
    setCopiedUrl(true)
    setTimeout(() => setCopiedUrl(false), 2000)
  }, [shareUrl, writeText])

  const handleCopyWithTitle = useCallback(async () => {
    if (!shareUrl) return
    await writeText(`${convTitle}\n${shareUrl}`)
    setCopiedText(true)
    setTimeout(() => setCopiedText(false), 2000)
  }, [shareUrl, convTitle, writeText])

  const handleClose = useCallback(() => {
    setShareUrl(null)
    setShareToken(null)
    setCopiedUrl(false)
    setCopiedText(false)
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
            <div className="space-y-3">
              <label className="text-sm text-gray-600 dark:text-gray-400">
                {t('share.expireLabel')}
              </label>
              {/* 快捷预设 */}
              <div className="flex flex-wrap gap-1.5">
                {QUICK_PRESETS.map(({ min, zh, en }) => (
                  <button
                    key={min}
                    type="button"
                    onClick={() => setExpireMinutes(min)}
                    className={`px-3 py-1 text-xs rounded-full border transition-all font-medium ${
                      expireMinutes === min
                        ? 'bg-primary text-white border-primary'
                        : 'border-gray-200 dark:border-gray-700 text-gray-600 dark:text-gray-400 hover:border-primary hover:text-primary'
                    }`}
                  >
                    {i18n.language.startsWith('zh') ? zh : en}
                  </button>
                ))}
              </div>
              {/* 对数滑块 */}
              <div className="space-y-1 pt-0.5">
                <input
                  type="range"
                  min={0}
                  max={100}
                  step={0.1}
                  value={minutesToSlider(expireMinutes)}
                  onChange={(e) => setExpireMinutes(sliderToMinutes(Number(e.target.value)))}
                  className="w-full h-1.5 rounded-full appearance-none cursor-pointer dark:[--track-empty:#374151] [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4 [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-primary [&::-webkit-slider-thumb]:shadow-sm [&::-webkit-slider-thumb]:cursor-pointer [&::-moz-range-thumb]:w-4 [&::-moz-range-thumb]:h-4 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-primary [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:cursor-pointer"
                  style={{
                    background: `linear-gradient(to right, var(--color-primary) 0%, var(--color-primary) ${minutesToSlider(expireMinutes).toFixed(1)}%, var(--track-empty, #e2e8f0) ${minutesToSlider(expireMinutes).toFixed(1)}%, var(--track-empty, #e2e8f0) 100%)`
                  }}
                />
                {/* 刻度标签 */}
                <div className="relative h-4">
                  {TICKS.map(({ min, label, labelEn }, idx) => (
                    <span
                      key={min}
                      style={{
                        left: `${minutesToSlider(min).toFixed(1)}%`,
                        transform: idx === 0 ? 'none' : idx === TICKS.length - 1 ? 'translateX(-100%)' : 'translateX(-50%)'
                      }}
                      className="absolute text-[10px] text-gray-400 dark:text-gray-500 select-none"
                    >
                      {i18n.language.startsWith('zh') ? label : labelEn}
                    </span>
                  ))}
                </div>
              </div>
              {/* 当前值 */}
              <div className="text-center py-1.5 rounded-lg bg-primary/10 dark:bg-primary/15">
                <span className="text-sm font-semibold text-primary">{fmtDur(expireMinutes, i18n.language)}</span>
              </div>
            </div>
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
                onClick={handleCopyUrl}
                className="flex-shrink-0 p-2 rounded-md hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors"
                title={t('share.copyUrl')}
              >
                <Icon
                  name={copiedUrl ? 'check' : 'content_copy'}
                  size="base"
                  className={copiedUrl ? 'text-green-500' : 'text-gray-500'}
                />
              </button>
            </div>
            {convTitle && (
              <button
                onClick={handleCopyWithTitle}
                className="w-full py-2 px-4 bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-200 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors text-sm font-medium flex items-center justify-center gap-2"
              >
                <Icon
                  name={copiedText ? 'check' : 'share'}
                  size="base"
                  className={copiedText ? 'text-green-500' : ''}
                />
                {copiedText ? t('common.copied') : t('share.copyWithTitle')}
              </button>
            )}
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
