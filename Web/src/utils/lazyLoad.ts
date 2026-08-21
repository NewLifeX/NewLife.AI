import { lazy, type ComponentType } from 'react'

/** 距离上次自动刷新的最短间隔（毫秒），避免持续失败时无限刷新循环 */
const RELOAD_COOLDOWN = 5000

let lastReloadAt = 0

/**
 * 重置内部刷新冷却状态（仅供测试使用，生产代码无需调用）。
 * 用于隔离用例间的冷却计时，保证每个测试从全新状态开始。
 */
export function __resetLazyLoadState(): void {
  lastReloadAt = 0
}

/**
 * 判断是否为动态导入 chunk 加载失败。
 * 典型场景：Vite 构建清空 wwwroot 后，已打开页面仍引用被删除的旧 chunk（404）。
 * 覆盖 Chrome / Firefox / Safari 三种浏览器的错误消息。
 */
export function isChunkLoadError(err: unknown): boolean {
  const msg = err instanceof Error ? err.message : String(err)
  return (
    msg.includes('Failed to fetch dynamically imported module') ||
    msg.includes('error loading dynamically imported module') ||
    msg.includes('Importing a module script failed')
  )
}

/**
 * React.lazy 包装：动态导入失败时自动刷新页面一次，拉取最新构建产物。
 *
 * 背景：`pnpm build` 会清空 wwwroot 并生成新的带 hash chunk。若页面在构建前已打开，
 * 期间发生重新构建，懒加载模块将请求已删除的旧 chunk 而 404。
 * 这里在失败时自动 reload（带冷却防抖），让页面自愈；仍抛出错误由 ErrorBoundary 兜底。
 */
export function lazyLoad<T extends ComponentType<any>>(
  factory: () => Promise<{ default: T }>,
) {
  return lazy(() =>
    factory().catch((err: unknown) => {
      if (isChunkLoadError(err) && Date.now() - lastReloadAt > RELOAD_COOLDOWN) {
        lastReloadAt = Date.now()
        window.location.reload()
      }
      throw err
    }),
  )
}
