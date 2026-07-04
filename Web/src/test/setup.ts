// vitest jsdom 环境下 mock 缺失的浏览器 API
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: String) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})
