import type mermaid from 'mermaid'

let _mermaidPromise: Promise<typeof mermaid> | null = null
let _initialized = false

/**
 * 按需加载 mermaid 并执行一次性初始化。
 * 多个组件可并发调用，内部保证只加载一次。
 */
export function getMermaid(): Promise<typeof mermaid> {
  if (_mermaidPromise) return _mermaidPromise

  _mermaidPromise = (async () => {
    const mod = await import('mermaid')
    if (!_initialized) {
      mod.default.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'loose' })
      _initialized = true
    }
    return mod.default
  })()

  return _mermaidPromise
}
