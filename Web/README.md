# NewLife.AI Web 前端

NewLife.AI 多模型对话 AI 平台的 Web 前端，基于 React 19 构建的单页应用。

## 项目简介

NewLife.AI 是一个统一多模型对话交互式 AI 平台，支持 33+ 个 AI 服务商的模型接入。本前端提供：

- **多轮对话** — SSE 流式输出、思考过程展示、工具调用可视化
- **会话管理** — 创建/删除/重命名/置顶会话，支持无限滚动分页加载
- **模型切换** — 运行时切换不同 AI 模型（通义千问、DeepSeek、GPT-4o 等）
- **附件上传** — 支持图片、PDF、文件上传并关联到消息
- **分享功能** — 生成会话分享链接，支持过期时间设置
- **MCP 服务** — Model Context Protocol 服务配置管理
- **国际化** — 中文/英文双语支持
- **响应式布局** — 适配桌面端和移动端

## 技术栈

| 类别 | 技术 | 版本 |
|------|------|------|
| 框架 | React + TypeScript | 19 |
| 构建 | Vite + pnpm | 7 |
| 样式 | TailwindCSS | v4 |
| 状态管理 | Zustand (persist middleware) | 5 |
| 国际化 | i18next + react-i18next | 25 |
| Markdown | react-markdown + remark-gfm + rehype-highlight | - |
| 路由 | react-router-dom | 7 |
| 图标 | Google Material Icons (CDN) | - |

## 目录结构

```
src/
├── components/
│   ├── atoms/        # 自定义表单原子组件 (Button, Toggle, Select, Slider 等)
│   ├── chat/         # 聊天组件 (MessageBubble, ToolCallBadge, ThinkingIndicator 等)
│   ├── common/       # 公共组件 (Icon, Avatar, Modal, Tooltip, ScrollArea)
│   ├── input/        # 输入区组件 (ChatInput, SkillBar, AttachmentChip 等)
│   ├── settings/     # 设置组件 (SettingsModal, GeneralSettings, McpSettings 等)
│   └── sidebar/      # 侧边栏组件 (Sidebar, ConversationList, UserProfile)
├── layouts/          # 页面布局 (ChatLayout - 响应式侧边栏)
├── pages/            # 页面 (WelcomePage, ChatPage)
├── stores/           # Zustand 状态管理 (chatStore, settingsStore, uiStore)
├── types/            # TypeScript 类型定义
├── styles/           # TailwindCSS v4 主题变量
├── lib/              # 工具函数 (api.ts, cn.ts)
└── i18n/             # 国际化配置和语言包
```

## 快速开始

### 环境要求

- Node.js >= 18
- pnpm >= 8

### 开发

```bash
# 安装依赖
pnpm install

# 启动开发服务器 (默认 http://localhost:5173)
pnpm dev

# 类型检查
npx tsc --noEmit

# 代码检查
pnpm lint
```

### 构建

```bash
# 构建生产版本，输出到 dist/
pnpm build

# 本地预览构建结果
pnpm preview
```

### 后端联调

默认连接后端地址 `http://localhost:5080`，可通过环境变量覆盖：

```bash
VITE_API_BASE_URL=http://your-backend:5080 pnpm dev
```

构建后的静态文件可直接部署到 ASP.NET Core 的 `wwwroot/` 目录。

## 核心功能说明

### SSE 流式对话

前端通过 Server-Sent Events 接收 AI 回复，支持以下事件类型：

- `message_start` — 消息开始，获取消息 ID
- `thinking_delta` — 思考过程增量输出
- `content_delta` — 正文内容增量输出
- `tool_call_start` — 工具调用开始
- `tool_call_done` — 工具调用完成
- `message_done` — 消息完成，携带 Token 用量
- `error` — 错误信息

### 状态管理

- **chatStore** — 会话列表、消息、生成状态、附件、模型列表
- **settingsStore** — 用户设置（主题、语言、模型偏好等），使用 persist 持久化
- **uiStore** — UI 状态（侧边栏折叠、弹窗开关）

## 开发规范

1. **禁止使用 Emoji** — 所有界面、组件、翻译文件中不得出现任何 Emoji 字符，仅使用 Material Icons 和纯文本
2. **零运行时依赖** — 生产环境构建后为纯静态文件，不依赖 Node.js 运行时
3. **自定义原子组件** — 不使用第三方 UI 组件库，所有表单组件手写实现
4. **路径别名** — `@/` 映射到 `./src/`
5. **类型安全** — 严格 TypeScript，所有 API 响应和状态都有明确类型定义

## 相关项目

- [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) — 基础库，多协议模型适配（netstandard2.1）
- [NewLife.ChatAI](../NewLife.ChatAI/) — 后端应用层，ASP.NET Core + XCode ORM

## 许可证

Apache-2.0
