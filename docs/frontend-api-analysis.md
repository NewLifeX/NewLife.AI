# 前端功能与 API 端点完整分析

> 生成时间：2026-03-26  
> 项目：NewLife.AI Web (React + Vite)

## 一、路由/页面

| 路由 | 页面组件 | 说明 |
|---|---|---|
| `/chat` | `ChatApp` → `WelcomePage` | 新对话欢迎页（无消息时） |
| `/chat/:conversationId` | `ChatApp` → `ChatPage` | 对话聊天页（有消息时） |
| `/share/:token` | `SharePage` | 分享对话只读查看页 |
| `*` | `<Navigate to="/chat">` | 兜底重定向到 `/chat` |

## 二、全部 API 端点清单

### 1. 会话管理 (Conversations)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 获取会话列表（分页） | `GET` | `/api/conversations?page=&pageSize=` | `chatStore.loadConversations` / `loadMoreConversations` |
| 创建会话 | `POST` | `/api/conversations` | `chatStore.sendMessage`（首次发送时自动创建） |
| 更新会话（标题/模型） | `PUT` | `/api/conversations/{id}` | `chatStore.switchModel` / `renameConversation` |
| 删除会话 | `DELETE` | `/api/conversations/{id}` | `chatStore.deleteConversation` / 空会话自动清理 |
| 置顶/取消置顶 | `PATCH` | `/api/conversations/{id}/pin?isPinned=` | `chatStore.pinConversation` |

### 2. 消息 (Messages)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 获取会话消息列表 | `GET` | `/api/conversations/{id}/messages` | `chatStore.setActiveConversation` |
| 发送消息 (SSE 流式) | `POST` | `/api/conversations/{id}/messages` | `chatStore.sendMessage` / `useChat.append` / `useCompletion.complete` |
| 编辑消息 | `PUT` | `/api/messages/{id}` | `chatStore.editMsg`（assistant 消息编辑） |
| 重新生成 (SSE 流式) | `POST` | `/api/messages/{id}/regenerate/stream` | `chatStore.regenerateMsg` |
| 编辑并重发 (SSE 流式) | `POST` | `/api/messages/{id}/edit-and-resend` | `chatStore.editMsg`（user 消息编辑后自动重新生成） |
| 停止生成 | `POST` | `/api/messages/{id}/stop` | `api.stopGeneration`（已定义） |

### 3. 反馈 (Feedback)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 提交反馈（点赞/点踩+原因） | `POST` | `/api/messages/{id}/feedback` | `chatStore.likeMsg` / `dislikeMsg` |
| 取消反馈 | `DELETE` | `/api/messages/{id}/feedback` | `chatStore.likeMsg`（已赞时toggle） / `dislikeMsg` |

### 4. 模型 (Models)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 获取可用模型列表 | `GET` | `/api/models` | `chatStore.loadModels` |

### 5. 用户设置 (User Settings)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 获取用户设置 | `GET` | `/api/user/settings` | `settingsStore.loadFromServer` / `reloadFromServer` |
| 保存用户设置 | `PUT` | `/api/user/settings` | `settingsStore.update` / `reset` |

### 6. 用户资料 (User Profile)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 获取用户资料 | `GET` | `/api/user/profile` | `App.tsx` 启动时加载 |

### 7. 分享 (Share)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 创建分享链接 | `POST` | `/api/conversations/{id}/share` | `ShareDialog` |
| 撤销分享链接 | `DELETE` | `/api/share/{token}` | `api.revokeShareLink`（已定义） |
| 获取分享内容 | `GET` | `/api/share/{token}` | `SharePage` |

### 8. 附件 (Attachments)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 上传附件 | `POST` | `/api/attachments` (multipart/form-data) | `chatStore.addAttachment` |

### 9. MCP 服务器管理

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 获取 MCP 服务器列表 | `GET` | `/api/mcp/servers` | `SettingsModal`（打开时加载） |
| 切换 MCP 服务器启停 | `PUT` | `/api/mcp/servers/{id}` | `SettingsModal.toggleMcpServer` |

### 10. 数据管理 (Data Management)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 导出用户数据 | `GET` | `/api/user/data/export` | `DataSettings.handleExport` |
| 清除用户数据 | `DELETE` | `/api/user/data/clear` | `DataSettings.handleClear` |

### 11. 使用量统计 (Usage Statistics)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 使用量汇总 | `GET` | `/api/usage/summary` | `UsageSettings` |
| 每日使用量 | `GET` | `/api/usage/daily?start=&end=` | `UsageSettings` |
| 按模型使用量 | `GET` | `/api/usage/models` | `UsageSettings` |

### 12. 系统配置 (System Config)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| 获取系统配置 | `GET` | `/api/system/config` | `App.tsx` 启动时（站点标题、推荐问题） |

### 13. 图片编辑 (Image Edit)

| 功能 | 方法 | 端点 | 调用位置 |
|---|---|---|---|
| AI 图片编辑 | `POST` | `/v1/images/edits` (multipart/form-data) | `ImageEditDialog.onSubmit` |

### 14. 认证（外部跳转）

| 功能 | 说明 | 端点 |
|---|---|---|
| 登录跳转 | 401 时自动重定向 | `/Admin/User/Login?r=` |

## 三、功能模块详细清单

### 1. 聊天核心
- **发送消息**：SSE 流式接收（content_delta, thinking_delta, thinking_done, tool_call_start/done/error, message_done, error），支持附件
- **思考模式**：Auto / Think / Fast 三档切换（ThinkingModeToggle），模型不支持时自动回退
- **流式思考展示**：多段思考内容分段显示（ThinkingBlock），含思考耗时
- **Tool Calls**：显示工具调用状态（calling/done/error）和结果（ToolCallBadge）
- **消息操作**：复制、重新生成（流式）、编辑（user 消息编辑后自动重发）、点赞/点踩
- **停止生成**：AbortController 中止流
- **Markdown 渲染**：MarkdownRenderer 渲染 AI 回复内容
- **终端代码块**：TerminalBlock 组件
- **Token 用量**：消息级别显示 promptTokens / completionTokens / totalTokens
- **打字光标**：TypingCursor 流式输入效果
- **组件**：ChatPage, MessageBubble, MessageActions, ChatInput, ThinkingBlock, ThinkingIndicator, ToolCallBadge, TerminalBlock, MarkdownRenderer, TypingCursor

### 2. 对话管理
- **创建对话**：新建空对话 / 首次发消息自动创建
- **切换对话**：侧边栏选择，URL 同步（/chat/{id}）
- **删除对话**：带确认交互
- **重命名对话**：内联编辑标题
- **置顶对话**：pin/unpin
- **空会话自动清理**：切走时无消息的会话自动删除
- **后端自动标题**：message_done 事件中返回 title 自动更新
- **分页加载**：滚动到底部自动加载更多会话
- **搜索/过滤**：会话列表本地搜索（>5 条时显示搜索框）
- **时间分组**：已置顶 / 今天 / 昨天 / 最近7天 / 最近30天 / 更早
- **组件**：ConversationList, Sidebar

### 3. 模型选择
- **下拉选择器**：显示模型名称，图标标记支持 thinking/vision
- **切换模型**：已有对话更新后端 modelId；新对话修改默认模型
- **组件**：ModelSelector

### 4. 分享
- **创建分享链接**：弹窗生成公开链接
- **复制链接**：clipboard API + execCommand fallback
- **查看分享页**：只读展示对话消息、时间戳、过期时间
- **撤销分享**：API 已定义
- **组件**：ShareDialog, SharePage

### 5. 反馈
- **点赞**：toggle 切换（已赞再点取消）
- **点踩 + 原因选择**：弹窗多选原因（不准确/不完整/格式问题/有害内容/其他）
- **组件**：DislikeReasonDialog, MessageActions

### 6. 附件上传
- **文件上传**：点击按钮 / 拖拽上传
- **类型识别**：image / pdf / file
- **附件预览芯片**：显示文件名、删除按钮
- **组件**：AttachmentChip, ChatInput, ChatPage（拖拽区域）

### 7. 图片编辑
- **Canvas 绘制 mask**：用户在图片上画蒙版
- **AI 修图**：调用 /v1/images/edits OpenAI 兼容接口
- **组件**：ImageEditDialog, ProgressiveImage

### 8. 设置 (Settings Modal, 6 个 Tab)
- **通用设置** (GeneralSettings)：语言（中/繁/英）、主题（亮/暗/系统）、字体大小
- **聊天偏好** (ChatSettings)：发送快捷键、默认模型、默认思考模式、上下文轮数、系统提示词、MCP 开关、流式速度
- **MCP 高级** (McpSettings)：插件列表启停、权限设置（自动审批读取、危险操作确认）
- **使用量统计** (UsageSettings)：会话数/消息数/Token 总量/最后活跃时间、Token 分解、按模型分布、每日用量表格
- **数据管理** (DataSettings)：允许训练开关、导出数据（JSON 下载）、清除所有数据
- **账户** (SettingsModal tab `account`)：Tab 存在但内容未详细实现

### 9. 国际化 (i18n)
- 基于 react-i18next，支持 zh / zh-TW / en
- 语言切换实时生效

### 10. 主题系统
- Light / Dark / System 三种模式
- CSS class 切换 dark，支持过渡动画
- 自动监听系统主题变化

### 11. UI/UX 功能
- **骨架屏**：AppSkeleton 初始加载态
- **Toast 通知**：ToastContainer + toastStore（error/warning/success/info）
- **图片灯箱**：Lightbox 组件
- **Tooltip**：Tooltip 组件
- **Modal**：通用弹窗组件
- **ActionSheet**：移动端长按操作面板
- **ScrollArea**：自定义滚动区域
- **响应式布局**：移动端侧边栏 overlay + 自动收起
- **键盘快捷键**：Cmd/Ctrl+K 新建对话
- **回到底部按钮**：聊天滚动时显示
- **SSE 重试**：最多 3 次指数退避重试

### 12. Hooks
- **useChat**：对话流式 Hook（类 Vercel AI SDK），SSE 流式收发
- **useCompletion**：文本补全流式 Hook
- **useLongPress**：移动端长按手势检测

### 13. 欢迎页
- 站点标题显示（从 SystemConfig 获取）
- 推荐问题卡片（后端配置 suggestedQuestions）
- 默认能力快捷入口：快问快答、图片生成、代码、写作、研究、视频生成

## 四、API 端点汇总（去重，共 31 个）

| # | 方法 | 端点 |
|---|---|---|
| 1 | `GET` | `/api/conversations?page=&pageSize=` |
| 2 | `POST` | `/api/conversations` |
| 3 | `PUT` | `/api/conversations/{id}` |
| 4 | `DELETE` | `/api/conversations/{id}` |
| 5 | `PATCH` | `/api/conversations/{id}/pin?isPinned=` |
| 6 | `GET` | `/api/conversations/{id}/messages` |
| 7 | `POST` | `/api/conversations/{id}/messages` (SSE) |
| 8 | `PUT` | `/api/messages/{id}` |
| 9 | `POST` | `/api/messages/{id}/regenerate/stream` (SSE) |
| 10 | `POST` | `/api/messages/{id}/edit-and-resend` (SSE) |
| 11 | `POST` | `/api/messages/{id}/stop` |
| 12 | `POST` | `/api/messages/{id}/feedback` |
| 13 | `DELETE` | `/api/messages/{id}/feedback` |
| 14 | `GET` | `/api/models` |
| 15 | `GET` | `/api/user/settings` |
| 16 | `PUT` | `/api/user/settings` |
| 17 | `GET` | `/api/user/profile` |
| 18 | `POST` | `/api/conversations/{id}/share` |
| 19 | `GET` | `/api/share/{token}` |
| 20 | `DELETE` | `/api/share/{token}` |
| 21 | `POST` | `/api/attachments` (multipart) |
| 22 | `GET` | `/api/mcp/servers` |
| 23 | `PUT` | `/api/mcp/servers/{id}` |
| 24 | `GET` | `/api/user/data/export` |
| 25 | `DELETE` | `/api/user/data/clear` |
| 26 | `GET` | `/api/usage/summary` |
| 27 | `GET` | `/api/usage/daily?start=&end=` |
| 28 | `GET` | `/api/usage/models` |
| 29 | `GET` | `/api/system/config` |
| 30 | `POST` | `/v1/images/edits` (multipart) |
| 31 | — | `/Admin/User/Login?r=` (401 重定向) |

## 五、SSE 事件类型

前端处理的 SSE 流事件（`ChatStreamEvent.type`）：

| 事件类型 | 说明 |
|---|---|
| `message_start` | 流开始，返回 messageId |
| `thinking_delta` | 思考内容增量 |
| `thinking_done` | 思考完成，返回 thinkingTime |
| `content_delta` | 回复内容增量 |
| `tool_call_start` | 工具调用开始 |
| `tool_call_done` | 工具调用完成 |
| `tool_call_error` | 工具调用失败 |
| `message_done` | 流结束，返回 usage、title |
| `error` | 错误信息 |

## 六、数据类型（TypeScript）

### 核心类型
- `Conversation`: id, title, modelId, isPinned, icon, iconColor, updatedAt
- `Message`: id, conversationId, role, content, status, thinkingContent, thinkingSegments, toolCalls, usage, feedbackType
- `ModelInfo`: id, code, name, supportThinking, supportVision, supportImageGeneration, supportFunctionCalling
- `Attachment`: id, name, size, type(pdf/image/file), previewUrl
- `UserSettings`: theme, language, fontSize, sendShortcut, defaultModel, defaultThinkingMode, contextRounds, systemPrompt, mcpEnabled, streamingSpeed, allowTraining
- `ToolCall`: id, name, status(calling/done/error), arguments, result
- `TokenUsage`: promptTokens, completionTokens, totalTokens
- `ThinkingSegment`: content, thinkingTime
