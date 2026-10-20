import { afterAll, describe, expect, it } from 'vitest'
import i18n, { normalizeLanguage } from './index'

describe('语言切换与文档属性同步', () => {
  afterAll(async () => {
    await i18n.changeLanguage('zh')
  })

  it('切到英文后写入 en 并保持 ltr', async () => {
    await i18n.changeLanguage('en')
    expect(document.documentElement.lang).toBe('en')
    expect(document.documentElement.dir).toBe('ltr')
  })

  it('切到繁体中文后写入 zh-Hant', async () => {
    await i18n.changeLanguage('zh-TW')
    expect(document.documentElement.lang).toBe('zh-Hant')
    expect(document.documentElement.dir).toBe('ltr')
  })

  it('切回简体中文后恢复 ltr 并把 zh 归一化为 zh-Hans', async () => {
    await i18n.changeLanguage('zh')
    expect(document.documentElement.dir).toBe('ltr')
    expect(document.documentElement.lang).toBe('zh-Hans')
  })

  it('后端与浏览器语言代码归一化为三种受支持语言', () => {
    expect(normalizeLanguage('zh')).toBe('zh')
    expect(normalizeLanguage('zh-CN')).toBe('zh')
    expect(normalizeLanguage('zh-Hans')).toBe('zh')
    expect(normalizeLanguage('zh-HK')).toBe('zh-TW')
    expect(normalizeLanguage('zh-TW')).toBe('zh-TW')
    expect(normalizeLanguage('en-US')).toBe('en')
    expect(normalizeLanguage('en')).toBe('en')
    expect(normalizeLanguage('ja-JP')).toBe('zh')
    expect(normalizeLanguage(null)).toBe('zh')
  })
})
