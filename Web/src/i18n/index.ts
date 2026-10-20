import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import zh from './locales/zh'

/** 非默认语言的动态加载表。切换语言时按需 import，减少首屏打包体积。 */
type LocaleModule = { default: Record<string, unknown> }
const localeLoaders: Record<string, () => Promise<LocaleModule>> = {
  'zh-TW': () => import('./locales/zh-TW'),
  en: () => import('./locales/en'),
  ja: () => import('./locales/ja'),
  ko: () => import('./locales/ko'),
  de: () => import('./locales/de'),
  fr: () => import('./locales/fr'),
  es: () => import('./locales/es'),
  pt: () => import('./locales/pt'),
  ru: () => import('./locales/ru'),
  ar: () => import('./locales/ar'),
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

/** 从右向左书写的语言 */
const rtlLanguages = new Set(['ar', 'fa', 'he', 'ur'])

/** 同步 <html lang> 与 dir：RTL 语言整页镜像，语言代码统一成 BCP 47（zh → zh-Hans） */
function applyDocumentLocale(lng: string) {
  if (typeof document === 'undefined') return
  const lang = lng === 'zh' ? 'zh-Hans' : lng === 'zh-TW' ? 'zh-Hant' : lng
  document.documentElement.lang = lang
  document.documentElement.dir = rtlLanguages.has(lng) ? 'rtl' : 'ltr'
}

applyDocumentLocale(i18n.language)
i18n.on('languageChanged', applyDocumentLocale)

// 包装 changeLanguage：切换语言时先按需加载对应语言包
const _originalChangeLanguage = i18n.changeLanguage.bind(i18n)
i18n.changeLanguage = async function (lng, ...args) {
  if (lng && lng !== 'zh' && !i18n.hasResourceBundle(lng, 'translation')) {
    const loader = localeLoaders[lng]
    if (loader) {
      const mod = await loader()
      i18n.addResourceBundle(lng, 'translation', mod.default, true, true)
    }
  }
  return _originalChangeLanguage(lng, ...args) as ReturnType<typeof _originalChangeLanguage>
}

export default i18n
