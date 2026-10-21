# 前端 vs 后端 API 对比分析

> 生成时间：2026-03-26

## 对比结果摘要

| 状态 | 数量 | 说明 |
|---|---|---|
| ✅ 已实现（前后端匹配） | 21 | 前端调用、后端已有对应 Controller |
| ❌ 后端缺失 | 5 | 前端调用但后端无 Controller |
| ⚠️ 前端未调用（后端多出） | 9 | 后端有 Controller 但前端未使用 |

---

## 一、已匹配的 API（前后端均已实现）✅

| # | 前端调用 | 后端 Controller | 路由 |
|---|---|---|---|
| 1 | `fetchConversations` | `ConversationsController.QueryAsync` | `GET /api/conversations?page=&pageSize=` |
| 2 | `createConversation` | `ConversationsController.CreateAsync` | `POST /api/conversations` |
| 3 | `updateConversation` | `ConversationsController.UpdateAsync` | `PUT /api/conversations/{id}` |
| 4 | `deleteConversation` | `ConversationsController.DeleteAsync` | `DELETE /api/conversations/{id}` |
| 5 | `pinConversation` | `ConversationsController.SetPinAsync` | `PATCH /api/conversations/{id}/pin?isPinned=` |
| 6 | `fetchMessages` | `MessagesController.QueryAsync` | `GET /api/conversations/{id}/messages` |
| 7 | `streamMessage` (SSE) | `MessagesController.StreamSendAsync` | `POST /api/conversations/{id}/messages` |
| 8 | `editMessage` | `MessagesController.EditAsync` | `PUT /api/messages/{id}` |
| 9 | `streamRegenerate` (SSE) | `MessagesController.StreamRegenerateAsync` | `POST /api/messages/{id}/regenerate/stream` |
| 10 | `streamEditAndResend` (SSE) | `MessagesController.EditAndResendStreamAsync` | `POST /api/messages/{id}/edit-and-resend` |
| 11 | `stopGeneration` | `MessagesController.StopAsync` | `POST /api/messages/{id}/stop` |
| 12 | `submitFeedback` | `FeedbackController.SubmitAsync` | `POST /api/messages/{id}/feedback` |
| 13 | `deleteFeedback` | `FeedbackController.DeleteAsync` | `DELETE /api/messages/{id}/feedback` |
| 14 | `fetchModels` | `ModelsController` (HttpGet) | `GET /api/models` |
| 15 | `fetchUserSettings` | `UserSettingsController.GetSettingsAsync` | `GET /api/user/settings` |
| 16 | `saveUserSettings` | `UserSettingsController.SaveSettingsAsync` | `PUT /api/user/settings` |
| 17 | `fetchUserProfile` | `UserSettingsController.GetProfile` | `GET /api/user/profile` |
| 18 | `createShareLink` | `ShareController.CreateAsync` | `POST /api/conversations/{id}/share` |
| 19 | `fetchSharedConversation` | `ShareController.GetAsync` | `GET /api/share/{token}` |
| 20 | `revokeShareLink` | `ShareController.DeleteAsync` | `DELETE /api/share/{token}` |
| 21 | `uploadAttachment` | `AttachmentsController.UploadAsync` | `POST /api/attachments` |

### 额外路径匹配（同功能双路由）

前端 `exportUserData` 调用 `/api/user/data/export`，后端有**两个**实现：
- `UserSettingsController.ExportAsync` → `GET /api/user/data/export` ✅ **匹配**
- `DataController.ExportAsync` → `GET /api/data/export` （冗余，前端未调用）

前端 `clearUserData` 调用 `/api/user/data/clear`，后端有**两个**实现：
- `UserSettingsController.ClearAsync` → `DELETE /api/user/data/clear` ✅ **匹配**
- `DataController.ClearAsync` → `DELETE /api/data/clear` （冗余，前端未调用）

前端 `fetchSystemConfig` 调用 `/api/system/config`：
- `SystemConfigController.GetConfig` → `GET /api/system/config` ✅ **匹配**

前端 `editImage` 调用 `/v1/images/edits`：
- `GatewayController.ImageEditsAsync` → `POST /v1/images/edits` ✅ **匹配**（但需 AppKey 认证，前端是否传了 Bearer token 需确认）

---

## 二、后端缺失的 API ❌

以下端点前端已声明并调用，但后端项目中**没有对应的 Controller 文件或路由**：

| # | 前端函数 | 期望端点 | 前端调用位置 | 缺失说明 |
|---|---|---|---|---|
| **1** | `fetchMcpServers` | `GET /api/mcp/servers` | `SettingsModal` | ❌ **无 McpApiController** — 需新建 |
| **2** | `toggleMcpServer` | `PUT /api/mcp/servers/{id}` | `SettingsModal` | ❌ **无 McpApiController** — 需新建 |
| **3** | `fetchUsageSummary` | `GET /api/usage/summary` | `UsageSettings` | ❌ **无 UsageApiController** — 需新建 |
| **4** | `fetchDailyUsage` | `GET /api/usage/daily?start=&end=` | `UsageSettings` | ❌ **无 UsageApiController** — 需新建 |
| **5** | `fetchModelUsage` | `GET /api/usage/models` | `UsageSettings` | ❌ **无 UsageApiController** — 需新建 |

> **注意**：架构设计文档 (`Doc/架构设计.md`) 中这些 API 被标记为"✅ 完成"，但代码中未找到对应实现文件。
> 可能的原因：文档超前于实现；或文件因某种原因被删除/未提交。

---

## 三、前端未调用的后端 API ⚠️

以下端点后端已实现，但前端当前未调用：

| # | 后端路由 | Controller | 说明 |
|---|---|---|---|
| 1 | `GET /api/health` | `HealthController` | 健康检查（通常由运维调用） |
| 2 | `GET /api/data/export` | `DataController` | 与 `/api/user/data/export` 冗余 |
| 3 | `DELETE /api/data/clear` | `DataController` | 与 `/api/user/data/clear` 冗余 |
| 4 | `GET /api/appkeys` | `AppKeyApiController` | AppKey 管理 — 前端无对应 UI |
| 5 | `POST /api/appkeys` | `AppKeyApiController` | AppKey 创建 — 前端无对应 UI |
| 6 | `PUT /api/appkeys/{id}` | `AppKeyApiController` | AppKey 更新 — 前端无对应 UI |
| 7 | `DELETE /api/appkeys/{id}` | `AppKeyApiController` | AppKey 删除 — 前端无对应 UI |
| 8 | `POST /api/messages/{id}/regenerate` | `MessagesController.RegenerateAsync` | 非流式重新生成（前端只用流式版 `/stream`） |
| 9 | `GET /api/attachments/{id}` | `AttachmentsController.GetAsync` | 附件下载/预览（前端可能通过 URL 直接引用） |

### Gateway 端点（通过 AppKey 认证，非前端 Web UI 直接调用）

| # | 后端路由 | 说明 |
|---|---|---|
| 10 | `GET /v1/models` | OpenAI 兼容模型列表 |
| 11 | `POST /v1/chat/completions` | OpenAI 兼容聊天补全 |
| 12 | `POST /v1/responses` | Responses API |
| 13 | `POST /v1/messages` | Anthropic 兼容消息 |
| 14 | `POST /v1/gemini` | Gemini 兼容接口 |
| 15 | `POST /v1/images/generations` | 图像生成 |

### 工具 API（供 AI Agent tool calling 使用）

| # | 后端路由 | 说明 |
|---|---|---|
| 16 | `GET /api/ip` | IP 查询工具 |
| 17 | `GET /api/weather` | 天气查询工具 |
| 18 | `GET /api/translate` | 翻译工具 |
| 19 | `GET /api/search` | 搜索工具 |
| 20 | `GET /api/fetch` | 网页抓取工具 |

---

## 四、需要注意的问题

### 4.1 `/v1/images/edits` 认证方式不匹配
- **后端**：`GatewayController.ImageEditsAsync` 使用 `gatewayService.ValidateAppKey(Request.Headers.Authorization)` 进行 AppKey 认证
- **前端**：`editImage()` 使用 `fetch(url, { method: 'POST', body: formData })` 直接发请求，**没有传 Authorization header**
- **结论**：前端调用 `/v1/images/edits` 会收到 401 Unauthorized，此功能实际不可用或需要前端补充 Bearer token

### 4.2 DataController 冗余
- `DataController`（`api/data/export` + `api/data/clear`）与 `UserSettingsController` 中的 `data/export` + `data/clear` 功能完全重复
- 前端只调 `/api/user/data/export` 和 `/api/user/data/clear`
- 建议删除 `DataController` 避免混淆

### 4.3 前端设置页 Account Tab 空白
- SettingsModal 有 `account` tab，但选中后无具体内容渲染
- 可能是预留的待实现功能

### 4.4 前端 AppKey 管理无 UI
- 后端有完整的 AppKey CRUD（`AppKeyApiController`: GET/POST/PUT/DELETE `/api/appkeys`）
- 前端 SettingsModal 没有 AppKey 管理 Tab，用户无法在 Web UI 中管理 AppKey
- 架构设计中有"密钥管理"需求

---

## 五、后端缺失 API 的开发优先级建议

| 优先级 | 端点 | 原因 |
|---|---|---|
| 🔴 高 | `GET /api/usage/summary` | 设置页 Usage Tab 已实现，打开即调用，会 404 |
| 🔴 高 | `GET /api/usage/daily` | 同上 |
| 🔴 高 | `GET /api/usage/models` | 同上 |
| 🟡 中 | `GET /api/mcp/servers` | 设置页 MCP Tab 已实现，打开即调用。但前端有 catch 兜底不崩 |
| 🟡 中 | `PUT /api/mcp/servers/{id}` | MCP 服务器启停操作 |

---

## 六、数据结构对比

### 前端 UserSettings vs 后端 UserSettingsDto

| 字段 | 前端 | 后端 | 匹配 |
|---|---|---|---|
| language | ✅ | ✅ | ✅ |
| theme | ✅ | ✅ | ✅ |
| fontSize | ✅ | ✅ | ✅ |
| sendShortcut | ✅ | ✅ | ✅ |
| defaultModel | ✅ | ✅ | ✅ |
| defaultThinkingMode | ✅ | ✅ | ✅ |
| contextRounds | ✅ | ✅ | ✅ |
| systemPrompt | ✅ | ✅ | ✅ |
| mcpEnabled | ✅ | ✅ | ✅ |
| streamingSpeed | ✅ | ✅ | ✅ |
| **allowTraining** | ✅ | ❌ | ❌ **后端缺少此字段** |

> 前端 `DataSettings` 组件有 `allowTraining` toggle，保存时通过 `saveUserSettings` 发送。
> 后端 `UserSettingsDto` record 不包含 `AllowTraining` 字段，前端发送的值会被忽略。
