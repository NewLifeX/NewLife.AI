import { afterAll, describe, expect, it } from 'vitest'
import i18n from './index'

describe('语言切换与文档属性同步', () => {
  afterAll(async () => {
    await i18n.changeLanguage('zh')
  })

  it('阿拉伯语切换为从右向左，并写入 BCP 47 语言代码', async () => {
    await i18n.changeLanguage('ar')
    expect(document.documentElement.dir).toBe('rtl')
    expect(document.documentElement.lang).toBe('ar')
  })

  it('切回简体中文后恢复 ltr 并把 zh 归一化为 zh-Hans', async () => {
    await i18n.changeLanguage('zh')
    expect(document.documentElement.dir).toBe('ltr')
    expect(document.documentElement.lang).toBe('zh-Hans')
  })
})
