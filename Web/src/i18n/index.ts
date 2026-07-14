import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import zh from './locales/zh'

/** 非默认语言的动态加载表。切换语言时按需 import，减少首屏打包体积。 */
const localeLoaders: Record<string, () => Promise<{ default: any }>> = {
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
