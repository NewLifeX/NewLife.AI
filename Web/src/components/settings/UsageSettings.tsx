import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import { fetchUsageSummary, fetchDailyUsage, fetchModelUsage, type UsageSummary, type DailyUsage, type ModelUsage } from '@/lib/api'

export function UsageSettings() {
  const { t } = useTranslation()
  const [summary, setSummary] = useState<UsageSummary | null>(null)
  const [dailyUsage, setDailyUsage] = useState<DailyUsage[]>([])
  const [modelUsage, setModelUsage] = useState<ModelUsage[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    setLoading(true)
    Promise.all([fetchUsageSummary(), fetchDailyUsage(), fetchModelUsage()])
      .then(([s, d, m]) => {
        setSummary(s)
        setDailyUsage(d)
        setModelUsage(m)
      })
      .catch(() => {})
      .finally(() => setLoading(false))
  }, [])

  const formatNumber = (n: number) => n.toLocaleString()

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20 text-gray-400">
        <Icon name="hourglass_empty" className="animate-spin mr-2" />
        {t('common.loading')}
      </div>
    )
  }

  return (
    <div className="mb-10">
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-green-100 dark:bg-green-900/40 text-green-600 p-1 rounded mr-3">
          <Icon name="bar_chart" variant="filled" size="lg" />
        </span>
        {t('usage.title')}
      </h3>

      {/* Summary cards */}
      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
          <SummaryCard icon="chat" color="blue" label={t('usage.conversations')} value={formatNumber(summary.conversations)} />
          <SummaryCard icon="message" color="purple" label={t('usage.messages')} value={formatNumber(summary.messages)} />
          <SummaryCard icon="token" color="amber" label={t('usage.totalTokens')} value={formatNumber(summary.totalTokens)} />
          <SummaryCard icon="schedule" color="green" label={t('usage.lastActive')} value={summary.lastActiveTime ? new Date(summary.lastActiveTime).toLocaleDateString() : '-'} />
        </div>
      )}

      {/* Token breakdown */}
      {summary && (
        <div className="mb-8 p-4 rounded-lg bg-gray-50 dark:bg-gray-800/50 border border-gray-100 dark:border-gray-700">
          <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">{t('usage.tokenBreakdown')}</h4>
          <div className="flex items-center gap-6">
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-blue-500" />
              <span className="text-sm text-gray-600 dark:text-gray-400">{t('usage.promptTokens')}: {formatNumber(summary.promptTokens)}</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-green-500" />
              <span className="text-sm text-gray-600 dark:text-gray-400">{t('usage.completionTokens')}: {formatNumber(summary.completionTokens)}</span>
            </div>
          </div>
          {summary.totalTokens > 0 && (
            <div className="mt-3 w-full h-2 rounded-full bg-gray-200 dark:bg-gray-700 overflow-hidden flex">
              <div
                className="h-full bg-blue-500 rounded-l-full"
                style={{ width: `${(summary.promptTokens / summary.totalTokens) * 100}%` }}
              />
              <div
                className="h-full bg-green-500 rounded-r-full"
                style={{ width: `${(summary.completionTokens / summary.totalTokens) * 100}%` }}
              />
            </div>
          )}
        </div>
      )}

      {/* Model usage distribution */}
      {modelUsage.length > 0 && (
        <div className="mb-8">
          <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">{t('usage.modelDistribution')}</h4>
          <div className="space-y-2">
            {modelUsage.map((m) => {
              const maxCalls = Math.max(...modelUsage.map((x) => x.calls))
              return (
                <div key={m.modelCode} className="flex items-center gap-3">
                  <span className="text-sm text-gray-700 dark:text-gray-300 w-32 truncate" title={m.modelCode}>
                    {m.modelCode}
                  </span>
                  <div className="flex-1 h-6 bg-gray-100 dark:bg-gray-800 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-primary/80 rounded-full flex items-center justify-end pr-2 text-xs text-white font-medium transition-all"
                      style={{ width: `${Math.max((m.calls / maxCalls) * 100, 8)}%` }}
                    >
                      {m.calls}
                    </div>
                  </div>
                  <span className="text-xs text-gray-500 w-20 text-right">{formatNumber(m.totalTokens)} tokens</span>
                </div>
              )
            })}
          </div>
        </div>
      )}

      {/* Daily usage table */}
      {dailyUsage.length > 0 && (
        <div>
          <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">{t('usage.dailyUsage')}</h4>
          <div className="overflow-x-auto rounded-lg border border-gray-100 dark:border-gray-700">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 dark:bg-gray-800">
                <tr>
                  <th className="px-3 py-2 text-left font-medium text-gray-600 dark:text-gray-400">{t('usage.date')}</th>
                  <th className="px-3 py-2 text-right font-medium text-gray-600 dark:text-gray-400">{t('usage.calls')}</th>
                  <th className="px-3 py-2 text-right font-medium text-gray-600 dark:text-gray-400">{t('usage.promptTokens')}</th>
                  <th className="px-3 py-2 text-right font-medium text-gray-600 dark:text-gray-400">{t('usage.completionTokens')}</th>
                  <th className="px-3 py-2 text-right font-medium text-gray-600 dark:text-gray-400">{t('usage.totalTokens')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-gray-700">
                {dailyUsage.slice(-14).reverse().map((d) => (
                  <tr key={d.date} className="hover:bg-gray-50 dark:hover:bg-gray-800/50">
                    <td className="px-3 py-2 text-gray-700 dark:text-gray-300">{new Date(d.date).toLocaleDateString()}</td>
                    <td className="px-3 py-2 text-right text-gray-600 dark:text-gray-400">{d.calls}</td>
                    <td className="px-3 py-2 text-right text-gray-600 dark:text-gray-400">{formatNumber(d.promptTokens)}</td>
                    <td className="px-3 py-2 text-right text-gray-600 dark:text-gray-400">{formatNumber(d.completionTokens)}</td>
                    <td className="px-3 py-2 text-right font-medium text-gray-700 dark:text-gray-300">{formatNumber(d.totalTokens)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Empty state */}
      {!summary && modelUsage.length === 0 && dailyUsage.length === 0 && (
        <div className="text-center py-10 text-gray-400">
          <Icon name="bar_chart" size="xl" className="mx-auto mb-2 opacity-50" />
          <p>{t('usage.empty')}</p>
        </div>
      )}
    </div>
  )
}

function SummaryCard({ icon, color, label, value }: { icon: string; color: string; label: string; value: string }) {
  const colorClasses: Record<string, string> = {
    blue: 'bg-blue-50 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400',
    purple: 'bg-purple-50 dark:bg-purple-900/30 text-purple-600 dark:text-purple-400',
    amber: 'bg-amber-50 dark:bg-amber-900/30 text-amber-600 dark:text-amber-400',
    green: 'bg-green-50 dark:bg-green-900/30 text-green-600 dark:text-green-400',
  }

  return (
    <div className="p-4 rounded-lg border border-gray-100 dark:border-gray-700 bg-white dark:bg-gray-800/50">
      <div className="flex items-center gap-2 mb-2">
        <span className={`p-1 rounded ${colorClasses[color] ?? colorClasses.blue}`}>
          <Icon name={icon} size="base" />
        </span>
        <span className="text-xs text-gray-500 dark:text-gray-400">{label}</span>
      </div>
      <div className="text-xl font-bold text-gray-900 dark:text-white">{value}</div>
    </div>
  )
}
