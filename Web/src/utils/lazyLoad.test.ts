import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

// mock React.lazy：暴露工厂函数，便于在测试中直接触发动态导入逻辑
const { lazyMock } = vi.hoisted(() => ({
  lazyMock: vi.fn((factory: () => Promise<unknown>) => ({ factory })),
}))

vi.mock('react', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react')>()
  return { ...actual, lazy: lazyMock }
})

// mock 页面刷新，避免 jsdom 触发真实导航
const { reloadMock } = vi.hoisted(() => ({ reloadMock: vi.fn() }))

import { lazyLoad, isChunkLoadError, __resetLazyLoadState } from './lazyLoad'

// jsdom 中 window.location.reload 属性不可配置，通过整体替换 location 对象注入 mock
let originalLocation: Location | undefined

describe('isChunkLoadError', () => {
  it('识别 Chrome 动态导入失败', () => {
    const err = new Error('Failed to fetch dynamically imported module: http://localhost:5080/assets/SettingsModal-abc.js')
    expect(isChunkLoadError(err)).toBe(true)
  })

  it('识别 Firefox 动态导入失败', () => {
    expect(isChunkLoadError(new Error('error loading dynamically imported module'))).toBe(true)
  })

  it('识别 Safari 动态导入失败', () => {
    expect(isChunkLoadError(new Error('Importing a module script failed'))).toBe(true)
  })

  it('普通错误不识别为 chunk 加载失败', () => {
    expect(isChunkLoadError(new Error('Something went wrong'))).toBe(false)
    expect(isChunkLoadError('plain string')).toBe(false)
  })
})

describe('lazyLoad', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-01-01T00:00:00Z'))
    __resetLazyLoadState()
    originalLocation = window.location
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { ...originalLocation, reload: reloadMock } as unknown as Location,
    })
  })

  afterEach(() => {
    if (originalLocation) {
      Object.defineProperty(window, 'location', { configurable: true, value: originalLocation })
    }
    vi.useRealTimers()
    vi.restoreAllMocks()
    reloadMock.mockReset()
  })

  it('chunk 加载失败时自动刷新页面一次', async () => {
    const comp = lazyLoad(() =>
      Promise.reject(new Error('Failed to fetch dynamically imported module: http://localhost:5080/assets/SettingsModal-abc.js')),
    )

    await expect((comp as any).factory()).rejects.toThrow()
    expect(reloadMock).toHaveBeenCalledTimes(1)
  })

  it('5 秒冷却期内重复失败不再刷新，避免刷新循环', async () => {
    const comp = lazyLoad(() =>
      Promise.reject(new Error('Failed to fetch dynamically imported module: http://localhost:5080/assets/x.js')),
    )

    // 第一次失败：刷新
    await expect((comp as any).factory()).rejects.toThrow()
    expect(reloadMock).toHaveBeenCalledTimes(1)

    // 冷却期内（<5s）再失败：不刷新
    vi.setSystemTime(new Date('2026-01-01T00:00:04Z'))
    await expect((comp as any).factory()).rejects.toThrow()
    expect(reloadMock).toHaveBeenCalledTimes(1)

    // 超过冷却期后再失败：再次刷新
    vi.setSystemTime(new Date('2026-01-01T00:00:06Z'))
    await expect((comp as any).factory()).rejects.toThrow()
    expect(reloadMock).toHaveBeenCalledTimes(2)
  })

  it('普通错误不刷新，仍抛出由 ErrorBoundary 兜底', async () => {
    const comp = lazyLoad(() => Promise.reject(new Error('normal render error')))

    await expect((comp as any).factory()).rejects.toThrow('normal render error')
    expect(reloadMock).not.toHaveBeenCalled()
  })

  it('加载成功时不刷新，正常返回模块', async () => {
    const Comp = () => null
    const comp = lazyLoad(() => Promise.resolve({ default: Comp }))

    const mod = await (comp as any).factory()
    expect(mod.default).toBe(Comp)
    expect(reloadMock).not.toHaveBeenCalled()
  })
})
