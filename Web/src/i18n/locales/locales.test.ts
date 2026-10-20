import { describe, expect, it } from 'vitest'
import en from './en'
import zh from './zh'
import zhTW from './zh-TW'

type Dict = Record<string, unknown>

// ChatAI 只支持三种界面语言，三份词表的键集合必须与英文源文完全一致
const locales: Record<string, Dict> = { zh, 'zh-TW': zhTW }

/** 递归收集叶子节点路径，用于比对键集合 */
function leafPaths(node: Dict, prefix = ''): string[] {
  const result: string[] = []
  for (const [key, value] of Object.entries(node)) {
    const path = prefix ? `${prefix}.${key}` : key
    if (value && typeof value === 'object' && !Array.isArray(value)) result.push(...leafPaths(value as Dict, path))
    else result.push(path)
  }
  return result
}

/** 取叶子值，缺失返回 undefined */
function leafValue(node: Dict, path: string): unknown {
  return path.split('.').reduce<unknown>((acc, key) => (acc as Dict | undefined)?.[key], node)
}

/** 提取插值占位符集合（{{name}} 与 {name} 两种写法） */
function placeholders(value: unknown): string[] {
  if (typeof value !== 'string') return []
  return [...value.matchAll(/\{\{?\s*\w+\s*\}?\}/g)].map(m => m[0].replace(/\s/g, '')).sort()
}

const basePaths = leafPaths(en as Dict).sort()

describe('i18n 语言包一致性', () => {
  for (const [code, dict] of Object.entries(locales)) {
    it(`${code} 键集合与英文完全一致`, () => {
      expect(leafPaths(dict).sort()).toEqual(basePaths)
    })

    it(`${code} 无空值且占位符与英文一致`, () => {
      const mismatches: string[] = []
      for (const path of basePaths) {
        const value = leafValue(dict, path)
        if (typeof value !== 'string' || value.trim() === '') {
          mismatches.push(`${path} 为空或非字符串`)
          continue
        }
        const expected = placeholders(leafValue(en as Dict, path))
        const actual = placeholders(value)
        if (expected.join('|') !== actual.join('|')) mismatches.push(`${path}: ${expected.join(',')} != ${actual.join(',')}`)
      }
      expect(mismatches).toEqual([])
    })
  }
})
