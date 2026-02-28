# NewLife.AI Web Frontend

React SPA frontend for NewLife.AI multi-model chat platform.

## Tech Stack

- **Framework**: React 19 + TypeScript
- **Build**: Vite 7 + pnpm
- **Styling**: TailwindCSS v4
- **State**: Zustand 5 (persist middleware)
- **I18n**: i18next + react-i18next (zh / en)
- **Icons**: Google Material Icons (CDN)

## Development

```bash
pnpm install
pnpm dev        # http://localhost:5173
pnpm build      # output to dist/
```

## Project Rules

1. **禁止使用 Emoji** - 所有界面、组件、翻译文件中不得出现任何 Emoji 字符。仅使用 Material Icons 图标和纯文本。
2. **生产环境零 Node.js 运行时依赖** - 构建后为纯静态文件，集成到 ASP.NET Core wwwroot。
3. **Form 原子组件自定义** - 不使用第三方 UI 组件库，所有表单组件手写。
4. **路径别名** - `@/` 映射到 `./src/`。
