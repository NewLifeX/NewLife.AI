# NewLife.ChatAI 前后端功能差异 TODO

> 更新时间：2026-03-26（第三轮扫描）
> 对比范围：前端 `Web/src/` ↔ 后端 `NewLife.ChatAI/Controllers/`

---

## 一、功能对齐总览

| # | 功能模块 | 前端 | 后端 | 状态 |
|---|---------|------|------|------|
| 1 | 会话 CRUD（创建/列表/更新/删除/置顶） | ✅ | ✅ | ✅ 已对齐 |
| 2 | 消息流式发送（SSE） | ✅ | ✅ | ✅ 已对齐 |
| 3 | 消息编辑/重新生成/停止 | ✅ | ✅ | ✅ 已对齐（TODO-7） |
| 4 | 消息反馈（点赞/点踩） | ✅ | ✅ | ✅ 已对齐 |
| 5 | 模型列表 | ✅ | ✅ | ✅ 已对齐 |
| 6 | 用户设置（读取/保存） | ✅ | ✅ | ✅ 已对齐 |
| 7 | 用户资料 | ✅ | ✅ | ✅ 已对齐 |
| 8 | 会话分享（创建/查看/撤销） | ✅ | ✅ | ✅ 已对齐（TODO-8） |
| 9 | 文件上传（附件） | ✅ | ✅ | ✅ 已对齐 |
| 10 | 系统公开配置 | ⚠️ | ✅ | ⚠️ 见 TODO-14 |
| 11 | MCP 服务器列表/启停 | ✅ | ✅ | ✅ 已清理（TODO-10） |
| 12 | 用量统计（汇总/每日/按模型） | ✅ | ✅ | ✅ 已对齐 |
| 13 | 数据导出/清除 | ✅ | ✅ | ✅ 已对齐 |
| 14 | 图片编辑 | ✅ | ✅ | ✅ 已对齐（TODO-9） |
| 15 | AppKey 管理（CRUD） | ✅ | ✅ | ✅ 已完成 |
| 16 | 会话服务端搜索 | ✅ | ✅ | ✅ 已完成 |
| 17 | 消息全文搜索 | ✅ | ✅ | ✅ 已完成 |
| 18 | 附件下载/预览 | ✅ | ✅ | ✅ 已完成 |
| 19 | 模型切换持久化 | ✅ | ✅ | ✅ 已实现 |
| 20 | StreamingSpeed 支持 | ✅ | ✅ | ✅ 已实现 |
| 21 | 语音输入 | — | — | 已隐藏（TODO-11） |
| 22 | 消息删除 | ❌ | ✅ | ⚠️ 见 TODO-15 |
| 23 | 网关 API | — | ✅ | — 外部消费者用 |
| 24 | 工具 API | — | ✅ | — AI 函数调用用 |
| 25 | 健康检查 | — | ✅ | — 运维用 |
| 26 | 后台管理（Cube Admin） | — | ✅ | — 管理员后台 |

---

## 二、已完成的 TODO

### 第一轮（TODO-1 ~ TODO-6）
- TODO-1：AppKey 管理界面 ✅（cca2899）
- TODO-2：会话服务端搜索 ✅（a96e141）
- TODO-3：附件下载/预览 ✅（80ed157 + ca5c2fc）
- TODO-4：消息全文搜索 ✅（663c313）
- TODO-5：模型切换持久化 ✅（无需改动）
- TODO-6：StreamingSpeed 支持 ✅（无需改动）

### 第二轮（TODO-7 ~ TODO-13）
- TODO-7：停止生成通知后端 ✅（5bacdb7）
- TODO-8：分享链接撤销 UI ✅（05c53c5）
- TODO-9：图片编辑模型选择 ✅（9639abf）
- TODO-10：MCP 设置页移除占位数据 ✅（4709ec0）
- TODO-11：语音按钮已隐藏 ✅（4709ec0）
- TODO-12：清理前端死代码 ✅（4f36de7）
- TODO-13：设置页错误处理 ✅（38a8985）

---

## 三、新发现的 TODO（第三轮扫描）

### TODO-14：系统配置（siteTitle / suggestedQuestions）未接入 🟡 中

**现状：** `api.ts` 已有 `fetchSystemConfig()` 函数，但前端没有任何组件调用它。

**后端已有：** `SystemConfigController.cs` → `GET /api/config` 返回 `siteTitle` 和 `suggestedQuestions`。

**影响：** 站点标题、推荐问题等管理员配置无法动态生效，用户始终看到硬编码默认值。

**需要：**
- [ ] 启动时调用 `fetchSystemConfig()` 获取配置
- [ ] `siteTitle` 动态设置 `document.title`
- [ ] `suggestedQuestions` 填充聊天欢迎页的推荐问题列表

---

### TODO-15：消息删除功能 🔴 高

**现状：** 前端 `MessageBubble.tsx` 的操作按钮只有复制/编辑/重新生成，没有删除按钮。`api.ts` 中也没有 `deleteMessage()` 函数。

**后端能力：** 消息可通过后端管理界面删除。需确认是否存在 `DELETE /api/messages/{id}` 端点。

**需要：**
- [ ] 确认后端是否暴露消息删除 API
- [ ] 若有：前端增加 `deleteMessage()` API 函数
- [ ] MessageBubble 添加删除按钮（需二次确认对话框）
- [ ] 删除后更新本地消息列表

---

### TODO-16：纯编辑消息（不触发重新生成） 🟡 中

**现状：** 编辑用户消息时，前端调用 `editAndResend()` 会删除后续所有消息并重新生成 AI 回复。

**后端已有：** `PUT /api/messages/{id}` 支持纯编辑（不触发重新生成），前端 `editMessage()` 函数存在但仅在重新生成流程中使用。

**需要：**
- [ ] 编辑 UI 提供两个按钮：「编辑并重新生成」和「仅保存修改」
- [ ] 「仅保存修改」调用 `editMessage()` 后更新本地消息，不触发流式请求

---

### TODO-17：AppKey 模型限制字段 UI 🟢 低

**现状：** 后端 `PUT /api/appkeys/{id}` 接受 `models` 字段（逗号分隔的模型列表），但前端 AppKey 编辑界面未暴露该字段。

**需要：**
- [ ] AppKey 创建/编辑表单增加模型限制多选或输入框

---

## 四、后端仅供外部消费/运维的 API（不需要前端界面）

| API | 用途 | 说明 |
|-----|------|------|
| `GET /v1/models` | OpenAI 兼容模型列表 | 第三方客户端 |
| `POST /v1/chat/completions` | OpenAI 兼容聊天 | 第三方客户端 |
| `POST /v1/responses` | OpenAI Response API | 推理模型 |
| `POST /v1/messages` | Anthropic 兼容 | 第三方客户端 |
| `POST /v1/gemini` | Gemini 兼容 | 第三方客户端 |
| `POST /v1/images/generations` | 图片生成网关 | 第三方客户端 |
| `POST /v1/images/edits` | 图片编辑网关 | 第三方客户端 |
| `GET /api/ip` | IP 归属地 | AI 函数调用 |
| `GET /api/weather` | 天气查询 | AI 函数调用 |
| `GET /api/translate` | 文本翻译 | AI 函数调用 |
| `GET /api/search` | 网络搜索 | AI 函数调用 |
| `GET /api/fetch` | 网页抓取 | AI 函数调用 |
| `GET /api/health` | 健康检查 | 运维监控 |
| `Areas/ChatAI/*` | 后台管理 | Cube Admin |

---

## 五、优先级排序

| 优先级 | TODO | 说明 |
|--------|------|------|
| 🔴 高 | TODO-15 | 消息删除——用户无法删除单条消息 |
| 🟡 中 | TODO-14 | 系统配置未接入——siteTitle/suggestedQuestions |
| 🟡 中 | TODO-16 | 纯编辑消息（不重新生成） |
| 🟢 低 | TODO-17 | AppKey 模型限制字段 UI |

---

## 六、已确认对齐清单

- `POST /api/conversations` — 创建会话
- `GET /api/conversations` — 分页查询会话（含 keyword）
- `PUT /api/conversations/{id}` — 更新会话（含 modelId）
- `DELETE /api/conversations/{id}` — 删除会话
- `PATCH /api/conversations/{id}/pin` — 置顶/取消
- `GET /api/conversations/{id}/messages` — 消息列表
- `POST /api/conversations/{id}/messages` — 流式发送（SSE）
- `PUT /api/messages/{id}` — 编辑消息
- `POST /api/messages/{id}/regenerate/stream` — 流式重新生成
- `POST /api/messages/{id}/edit-and-resend` — 编辑重发
- `GET /api/messages/search` — 消息全文搜索
- `POST /api/messages/{id}/feedback` — 提交反馈
- `DELETE /api/messages/{id}/feedback` — 取消反馈
- `GET /api/models` — 模型列表
- `GET /api/user/profile` — 用户资料
- `GET /api/user/settings` — 获取设置
- `PUT /api/user/settings` — 保存设置
- `GET /api/user/data/export` — 导出数据
- `DELETE /api/user/data/clear` — 清除数据
- `POST /api/conversations/{id}/share` — 创建分享
- `GET /api/share/{token}` — 查看分享
- `DELETE /api/share/{token}` — 撤销分享（后端已有）
- `POST /api/attachments` — 上传附件
- `GET /api/attachments/{id}` — 下载/预览附件
- `GET /api/attachments/info` — 批量附件元信息
- `GET /api/system/config` — 系统配置
- `GET /api/mcp/servers` — MCP 服务器列表
- `PUT /api/mcp/servers/{id}` — 启停 MCP 服务器
- `GET /api/usage/summary` — 用量汇总
- `GET /api/usage/daily` — 每日用量
- `GET /api/usage/models` — 模型用量
- `POST /api/images/edits` — 图片编辑
- `GET /api/appkeys` — AppKey 列表
- `POST /api/appkeys` — 创建 AppKey
- `PUT /api/appkeys/{id}` — 更新 AppKey
- `DELETE /api/appkeys/{id}` — 删除 AppKey
