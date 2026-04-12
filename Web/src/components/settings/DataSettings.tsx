import { useState, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Icon } from '@/components/common/Icon'
import { Toggle } from '@/components/atoms/Toggle'
import { UsageSettings } from './UsageSettings'
import { exportUserData, clearUserData, importUserData } from '@/lib/api'

interface DataSettingsProps {
  onDataCleared?: () => void
  allowTraining?: boolean
  onAllowTrainingChange?: (v: boolean) => void
}

export function DataSettings({ onDataCleared, allowTraining = false, onAllowTrainingChange }: DataSettingsProps) {
  const { t } = useTranslation()
  const [exporting, setExporting] = useState(false)
  const [importing, setImporting] = useState(false)
  const [clearing, setClearing] = useState(false)
  const [showConfirm, setShowConfirm] = useState(false)
  const importFileRef = useRef<HTMLInputElement>(null)

  const handleExport = async () => {
    setExporting(true)
    try {
      const blob = await exportUserData()
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `newlife-chat-export-${new Date().toISOString().slice(0, 10)}.json`
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      URL.revokeObjectURL(url)
    } catch {
      /* silent */
    } finally {
      setExporting(false)
    }
  }

  const handleImportClick = () => importFileRef.current?.click()

  const handleImportFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    e.target.value = ''
    setImporting(true)
    try {
      await importUserData(file)
      onDataCleared?.() // 刷新会话列表
    } catch {
      /* silent */
    } finally {
      setImporting(false)
    }
  }

  const handleClear = async () => {
    setClearing(true)
    try {
      await clearUserData()
      setShowConfirm(false)
      onDataCleared?.()
    } catch {
      /* silent */
    } finally {
      setClearing(false)
    }
  }

  return (
    <div className="mb-10">
      {/* 使用统计区域 */}
      <UsageSettings />

      <div className="my-8 border-t border-gray-200 dark:border-gray-700" />

      {/* 数据管理区域 */}
      <h3 className="text-lg font-bold text-gray-900 dark:text-white mb-6 flex items-center">
        <span className="bg-amber-100 dark:bg-amber-900/40 text-amber-600 p-1 rounded mr-3">
          <Icon name="storage" variant="filled" size="lg" />
        </span>
        {t('settings.dataManagement')}
      </h3>

      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div className="flex-1 mr-4">
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">
              {t('settings.allowTraining')}
            </div>
            <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
              {t('settings.allowTrainingDesc')}
            </div>
          </div>
          <Toggle checked={allowTraining} onChange={(v) => onAllowTrainingChange?.(v)} />
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        <div className="flex items-center justify-between">
          <div className="flex-1 mr-4">
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">
              {t('settings.exportData')}
            </div>
            <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
              {t('settings.exportDataDesc')}
            </div>
          </div>
          <button
            onClick={handleExport}
            disabled={exporting}
            className="px-4 py-2 text-sm font-medium rounded-lg bg-primary text-white hover:bg-blue-600 disabled:opacity-50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
          >
            {exporting ? t('common.loading') : t('settings.exportBtn')}
          </button>
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        <div className="flex items-center justify-between">
          <div className="flex-1 mr-4">
            <div className="text-sm font-medium text-gray-700 dark:text-gray-200">
              {t('settings.importData')}
            </div>
            <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
              {t('settings.importDataDesc')}
            </div>
          </div>
          <input
            ref={importFileRef}
            type="file"
            accept=".json"
            className="hidden"
            onChange={handleImportFile}
          />
          <button
            onClick={handleImportClick}
            disabled={importing}
            className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-200 hover:bg-gray-50 dark:hover:bg-gray-800 disabled:opacity-50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
          >
            {importing ? t('common.loading') : t('settings.importBtn')}
          </button>
        </div>

        <div className="border-b border-gray-100 dark:border-gray-800" />

        <div className="flex items-center justify-between">
          <div className="flex-1 mr-4">
            <div className="text-sm font-medium text-red-600 dark:text-red-400">
              {t('settings.clearData')}
            </div>
            <div className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
              {t('settings.clearDataDesc')}
            </div>
          </div>
          {showConfirm ? (
            <div className="flex items-center space-x-2">
              <button
                onClick={() => setShowConfirm(false)}
                className="px-3 py-1.5 text-sm rounded-lg border border-gray-300 dark:border-gray-600 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
              >
                {t('common.cancel')}
              </button>
              <button
                onClick={handleClear}
                disabled={clearing}
                className="px-3 py-1.5 text-sm font-medium rounded-lg bg-red-500 text-white hover:bg-red-600 disabled:opacity-50 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500/50"
              >
                {clearing ? t('common.loading') : t('common.confirm')}
              </button>
            </div>
          ) : (
            <button
              onClick={() => setShowConfirm(true)}
              className="px-4 py-2 text-sm font-medium rounded-lg border border-red-300 dark:border-red-700 text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500/50"
            >
              {t('settings.clearBtn')}
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
