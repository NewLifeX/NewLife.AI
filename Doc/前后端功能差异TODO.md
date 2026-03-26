# NewLife.ChatAI 前后端功能差异 TODO

> 更新时间：2026-03-26（第二轮扫描）
> 对比范围：前端 `Web/src/` ↔ 后端 `NewLife.ChatAI/Controllers/`

---

## 一、功能对齐总览

| # | 功能模块 | 前端 | 后端 | 状态 |
|---|---------|------|------|------|
| 1 | 会话 CRUD（创建/列表/更新/删除/置顶） | ✅ | ✅ | ✅ 已对齐 |
| 2 | 消息流式发送（SSE） | ✅ | ✅ | ✅ 已对齐 |
| 3 | 消息编辑/重新生成/停止 | ⚠️ | ✅ | ⚠️ 见 TODO-7 |
| 4 | 消息反馈（点赞/点踩） | ✅ | ✅ | ✅ 已对齐 |
| 5 | 模型列表 | ✅ | ✅ | ✅ 已对齐 |
| 6 | 用户设置（读取/保存） | ✅ | ✅ | ✅ 已对齐 |
| 7 | 用户资料 | ✅ | ✅ | ✅ 已对齐 |
| 8 | 会话分享（创建/查看） | ✅ | ✅ | ⚠️ 撤销见 TODO-8 |
| 9 | 文件上传（附件） | ✅ | ✅ | ✅ 已对齐 |
| 10 | 系统公开配置 | ✅ | ✅ | ✅ 已对齐 |
| 11 | MCP 服务器列表/启停 | ⚠️ | ✅ | ⚠️ 见 TODO-10 |
| 12 | 用量统计（汇总/每日/按模型） | ✅ | ✅ | ✅ 已对齐 |
| 13 | 数据导出/清除 | ✅ | ✅ | ✅ 已对齐 |
| 14 | 图片编辑 | ⚠️ | ✅ | ⚠️ 见 TODO-9 |
| 15 | AppKey 管理（CRUD） | ✅ | ✅ | ✅ 已完成 |
| 16 | 会话服务端搜索 | ✅ | ✅ | ✅ 已完成 |
| 17 | 消息全文搜索 | ✅ | ✅ | ✅ 已完成 |
| 18 | 附件下载/预览 | ✅ | ✅ | ✅ 已完成 |
| 19 | 模型切换持久化 | ✅ | ✅ | ✅ 已实现 |
| 20 | StreamingSpeed 支持 | ✅ | ✅ | ✅ 已实现 |
| 21 | 语音输入 | ❌ | — | ❌ 见 TODO-11 |
| 22 | 网关 API | — | ✅ | — 外部消费者用 |
| 23 | 工具 API | — | ✅ | — AI 函数调用用 |
| 24 | 健康检查 | — | ✅ | — 运维用 |
| 25 | 后台管理（Cube Admin） | — | ✅ | — 管理员后台 |

---

## 二、已完成的 TODO（第一轮）

### TODO-1：AppKey 管理界面 ✅ 已完成（cca2899）
### TODO-2：会话服务端搜索 ✅ 已完成（a96e141）
### TODO-3：附件下载/预览 ✅ 已完成（80ed157 + ca5c2fc）
### TODO-4：消息全文搜索 ✅ 已完成（663c313）
### TODO-5：模型切换持久化 ✅ 已实现（无需改动）
### TODO-6：StreamingSpeed 支持 ✅ 已实现（无需改动）

---

## 三、新发现的 TODO（第二轮扫描）

### TODO-7：停止生成未调用后端 API 🔴 高

**现状：** 前端 `stopGenerating()` 仅在客户端 abort SSE 连接，未调用后端 `POST /api/messages/{id}/stop`。

**后端已有：** `MessagesController.cs` → `POST /messages/{id}/stop`

**前端位置：** `Web/src/stores/chatStore.ts` 第 401 行

**风险：** 客户端断开 SSE 后，后端可能继续调用 AI Provider 消耗 Token。

**需要：**
- [ ] `stopGenerating` 中额外调用 `stopGeneration(messageId)` 通知后端
- [ ] 需要在 chatStore 中保存当前正在生成的消息 ID

---

### TODO-8：分享链接撤销 UI 🔴 高

**现状：** `api.ts` 中已定义 `revokeShareLink(token)` 函数，但从未被任何组件导入或调用。

**后端已有：** `ShareController.cs` → `DELETE /api/share/{token}`

**前端位置：** `Web/src/components/chat/ShareDialog.tsx` — 只有创建分享，无撤销。

**需要：**
- [ ] ShareDialog 中创建链接后显示「撤销分享」按钮
- [ ] 点击后调用 `revokeShareLink(token)` + 确认对话框
- [ ] 撤销成功后清除分享状态

---

### TODO-9：图片编辑模型硬编码 🟡 中

**现状：** `MarkdownRenderer.tsx` 中图片编辑模型硬编码为 `dall-e-2`。

**后端支持：** `ImageEditController.cs` 接受 `model` 参数。

**需要：**
- [ ] 图片编辑对话框增加模型选择下拉框
- [ ] 从后端模型列表中过滤出图片编辑能力的模型

---

### TODO-10：MCP 设置页硬编码占位数据 🟡 中

**现状：** `McpSettings.tsx` 中有两个硬编码的 defaultPlugins（GitHub Copilot Bridge / Local File Access），实际数据从后端加载。

**问题：** 后端返回空列表时 UI 显示假插件。`autoApproveRead` / `confirmDangerous` 开关未关联后端。

**需要：**
- [ ] 移除 `defaultPlugins` 硬编码
- [ ] 后端无数据时显示空态提示

---

### TODO-11：语音输入按钮无功能 🟡 中

**现状：** `ChatInput.tsx` 有麦克风按钮但无 onClick。

**需要（二选一）：**
- [ ] 方案 A：接入 Web Speech API
- [ ] 方案 B：暂时隐藏按钮

---

### TODO-12：前端未使用的 API 函数 🟢 低

| 函数 | 说明 |
|------|------|
| `regenerateMessage(id)` | 非流式版本，前端用流式 |
| `stopGeneration(id)` | TODO-7 完成后使用 |
| `revokeShareLink(token)` | TODO-8 完成后使用 |

---

### TODO-13：设置页 API 错误静默吞没 🟢 低

`SettingsModal.tsx`、`AppKeySettings.tsx`、`UsageSettings.tsx` 等使用 `.catch(() => {})`。

**需要：** 失败时显示错误提示或至少 console.error。

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
| 🔴 高 | TODO-7 | 停止生成未调用后端，可能白费 Token |
| 🔴 高 | TODO-8 | 分享撤销 UI，后端已有前端缺入口 |
| 🟡 中 | TODO-9 | 图片编辑模型选择，硬编码 dall-e-2 |
| 🟡 中 | TODO-10 | MCP 硬编码占位数据 |
| 🟡 中 | TODO-11 | 语音输入按钮无功能 |
| 🟢 低 | TODO-12 | 未使用 API 函数（死代码） |
| 🟢 低 | TODO-13 | 设置页错误处理 |

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
