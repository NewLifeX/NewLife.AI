import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import zh from './locales/zh'

/** 非默认语言的动态加载表。切换语言时按需 import，减少首屏打包体积。ChatAI 只支持简体中文、繁体中文、英文三种界面语言。 */
type LocaleModule = { default: Record<string, unknown> }
const localeLoaders: Record<string, () => Promise<LocaleModule>> = {
  'zh-TW': () => import('./locales/zh-TW'),
  en: () => import('./locales/en'),
}

i18n.use(initReactI18next).init({
  resources: {
    zh: { translation: zh },
  },
  lng: 'zh',
  fallbackLng: 'zh',
  interpolation: {
    escapeValue: false,
  },
})

/** 从右向左书写的语言（当前三种界面语言均非 RTL，保留判断以便后续扩展） */
const rtlLanguages = new Set<string>(['ar', 'fa', 'he', 'ur'])

/**
 * 归一化语言代码：后端与浏览器会给出 zh-CN、zh-Hans、en-US 等形式，
 * 统一收敛到 zh / zh-TW / en，未识别时回退简体中文。
 */
export function normalizeLanguage(code?: string | null): string {
  const first = String(code ?? '').split(',')[0].trim()
  if (!first) return 'zh'

  const lower = first.toLowerCase().replace('_', '-')
  if (lower === 'zh-tw' || lower === 'zh-hk' || lower === 'zh-mo' || lower === 'zh-hant') return 'zh-TW'
  if (lower === 'zh' || lower.startsWith('zh-')) return 'zh'
  if (lower === 'en' || lower.startsWith('en-')) return 'en'
  return 'zh'
}

/** 同步 <html lang> 与 dir：RTL 语言整页镜像，语言代码统一成 BCP 47（zh → zh-Hans） */
function applyDocumentLocale(lng: string) {
  if (typeof document === 'undefined') return
  const lang = lng === 'zh' ? 'zh-Hans' : lng === 'zh-TW' ? 'zh-Hant' : lng
  document.documentElement.lang = lang
  document.documentElement.dir = rtlLanguages.has(lng) ? 'rtl' : 'ltr'
}

applyDocumentLocale(i18n.language)
i18n.on('languageChanged', applyDocumentLocale)

// 包装 changeLanguage：先归一化语言代码，再按需加载对应语言包
const _originalChangeLanguage = i18n.changeLanguage.bind(i18n)
i18n.changeLanguage = async function (lng, ...args) {
  const code = normalizeLanguage(lng)
  if (code !== 'zh' && !i18n.hasResourceBundle(code, 'translation')) {
    const loader = localeLoaders[code]
    if (loader) {
      const mod = await loader()
      i18n.addResourceBundle(code, 'translation', mod.default, true, true)
    }
  }
  return _originalChangeLanguage(code, ...args) as ReturnType<typeof _originalChangeLanguage>
}

export default i18n
