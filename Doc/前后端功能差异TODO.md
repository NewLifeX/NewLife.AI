# NewLife.ChatAI 前后端功能差异 TODO

> 生成时间：2026-03-26  
> 对比范围：前端 `Web/src/` ↔ 后端 `NewLife.ChatAI/Controllers/`

---

## 一、功能对齐总览

| # | 功能模块 | 前端 | 后端 | 状态 |
|---|---------|------|------|------|
| 1 | 会话 CRUD（创建/列表/更新/删除/置顶） | ✅ | ✅ | ✅ 已对齐 |
| 2 | 消息流式发送（SSE） | ✅ | ✅ | ✅ 已对齐 |
| 3 | 消息编辑/重新生成/停止 | ✅ | ✅ | ✅ 已对齐 |
| 4 | 消息反馈（点赞/点踩） | ✅ | ✅ | ✅ 已对齐 |
| 5 | 模型列表 | ✅ | ✅ | ✅ 已对齐 |
| 6 | 用户设置（读取/保存） | ✅ | ✅ | ✅ 已对齐 |
| 7 | 用户资料 | ✅ | ✅ | ✅ 已对齐 |
| 8 | 会话分享（创建/查看/撤销） | ✅ | ✅ | ✅ 已对齐 |
| 9 | 文件上传（附件） | ✅ | ✅ | ✅ 已对齐 |
| 10 | 系统公开配置 | ✅ | ✅ | ✅ 已对齐 |
| 11 | MCP 服务器列表/启停 | ✅ | ✅ | ✅ 已对齐 |
| 12 | 用量统计（汇总/每日/按模型） | ✅ | ✅ | ✅ 已对齐 |
| 13 | 数据导出/清除 | ✅ | ✅ | ✅ 已对齐 |
| 14 | 图片编辑 | ✅ | ✅ | ✅ 已对齐 |
| 15 | AppKey 管理（CRUD） | ✅ | ✅ | ✅ 已完成 |
| 16 | 会话服务端搜索 | ✅ | ✅ | ✅ 已完成 |
| 17 | 网关 API（OpenAI/Anthropic/Gemini 兼容） | — 不适用 | ✅ | — 外部消费者用 |
| 18 | 工具 API（IP/天气/翻译/搜索/网页抓取） | — 不适用 | ✅ | — AI 函数调用用 |
| 19 | 健康检查 | — 不适用 | ✅ | — 运维用 |
| 20 | 后台管理（Cube Admin Area） | — 不适用 | ✅ | — 管理员后台 |

---

## 二、前端有界面但后端缺实现

> 目前无。前端调用的所有 API 端点在后端均已有对应 Controller 实现。

---

## 三、后端有 API 但前端未实现的功能

### TODO-1：AppKey 管理界面 ✅ 已完成

**后端已有：** `AppKeyApiController.cs`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/appkeys` | 列出用户的 AppKey（密钥脱敏显示） |
| POST | `/api/appkeys` | 创建 AppKey（仅创建时返回完整密钥） |
| PUT | `/api/appkeys/{id}` | 更新 AppKey（名称/启用/过期时间/模型限制） |
| DELETE | `/api/appkeys/{id}` | 删除 AppKey |

**前端需要：**
- [ ] 在设置弹窗中新增「API 密钥」Tab 页
- [ ] 密钥列表组件：显示名称、脱敏密钥、启用状态、调用次数、Token 用量、创建/过期时间
- [ ] 创建密钥对话框：名称、过期时间、模型限制选择
- [ ] 创建成功后**一次性显示**完整密钥，提示用户复制保存
- [ ] 编辑/删除操作
- [ ] 在 `api.ts` 中添加 AppKey CRUD 接口函数
- [ ] 在 `types/` 中添加 AppKey 相关类型定义

**参考后端返回字段：** `Id, Name, SecretMask, Enable, Models, ExpireTime, Calls, TotalTokens, LastCallTime, CreateTime`

---

### TODO-2：会话服务端搜索 ✅ 已完成

**现状：** 前端 `ConversationList.tsx` 中有搜索框，但仅对已加载到内存中的会话进行客户端过滤。当会话量大时，搜索无法覆盖未加载的历史会话。

**需要：**
- [ ] **后端**：`GET /api/conversations` 增加 `keyword` 查询参数，支持按标题模糊搜索
- [ ] **前端**：当用户输入搜索关键词时，调用后端搜索 API 而非纯客户端过滤
- [ ] 支持 debounce 搜索（300ms 延迟），避免频繁请求

---

### TODO-3：附件下载/预览 ✅ 已完成

**后端已有：** `GET /api/attachments/{id}` — 下载/预览附件

**前端现状：** 上传附件后通过返回的 `url` 字段直接引用，但没有在 UI 中提供独立的附件下载/预览入口。

**可选改进：**
- [ ] 消息中的附件（非图片类型如 PDF）显示可点击的下载链接
- [ ] 图片附件支持点击放大预览（Lightbox 组件已有，需确认是否已集成到附件场景）

---

## 四、前后端体验改进建议

### TODO-4：会话搜索需要消息内容全文搜索 ✅ 已完成

**现状：** 搜索仅按会话标题匹配。用户往往记住的是消息内容而非标题。

**需要：**
- [ ] **后端**：已实现 `GET /api/messages/search?keyword=xxx` 端点，搜索消息内容并返回匹配结果
- [ ] **前端**：搜索结果已在 ConversationList 中展示匹配的消息内容

---

### TODO-5：模型切换持久化 ✅ 已实现

**现状：** 用户设置中有 `defaultModel`，新建会话时使用默认模型。但会话中途切换模型后，该选择仅在当前会话生效。

**确认：** 后端 `PUT /api/conversations/{id}` 已支持 `modelId` 更新 — ✅ 已实现

---

### TODO-6：前端缺少对后端 `streamingSpeed` 设置的完整支持 ✅ 已实现

**现状：** 前端 `settingsStore` 有 `streamingSpeed`，后端 `UserSettingsDto` 也有，确认已对齐。

**确认：** ✅ 已实现

---

## 五、后端仅供外部消费/运维的 API（不需要前端界面）

以下后端 API 是面向外部消费者或运维的，**不需要**在 Chat 前端中实现界面：

| API | 用途 | 说明 |
|-----|------|------|
| `GET /v1/models` | OpenAI 兼容模型列表 | 给第三方客户端用 |
| `POST /v1/chat/completions` | OpenAI 兼容聊天 | 给第三方客户端用 |
| `POST /v1/responses` | OpenAI Response API | 推理模型用 |
| `POST /v1/messages` | Anthropic 兼容 | 给第三方客户端用 |
| `POST /v1/gemini` | Gemini 兼容 | 给第三方客户端用 |
| `POST /v1/images/generations` | 图片生成网关 | 给第三方客户端用 |
| `POST /v1/images/edits` | 图片编辑网关 | 给第三方客户端用 |
| `GET /api/ip` | IP 归属地 | AI 函数调用工具 |
| `GET /api/weather` | 天气查询 | AI 函数调用工具 |
| `GET /api/translate` | 文本翻译 | AI 函数调用工具 |
| `GET /api/search` | 网络搜索 | AI 函数调用工具 |
| `GET /api/fetch` | 网页抓取 | AI 函数调用工具 |
| `GET /api/health` | 健康检查 | 运维监控用 |
| `Areas/ChatAI/*` | 后台管理界面 | Cube Admin 管理后台 |

---

## 六、优先级排序

| 优先级 | TODO | 工作量预估 |
|--------|------|-----------|
| 🔴 高 | TODO-1：AppKey 管理界面 | 前端新增 Tab + CRUD 组件 |
| 🟡 中 | TODO-2：会话服务端搜索 | 前端+后端各少量改动 |
| 🟡 中 | TODO-4：消息全文搜索 | 后端新增搜索端点 + 前端 |
| 🟢 低 | TODO-3：附件下载/预览增强 | 前端小改动 |

---

## 七、已确认对齐清单

以下 API 已确认前后端完全对齐，无需额外工作：

- `POST /api/conversations` — 创建会话
- `GET /api/conversations` — 分页查询会话
- `PUT /api/conversations/{id}` — 更新会话
- `DELETE /api/conversations/{id}` — 删除会话
- `PATCH /api/conversations/{id}/pin` — 置顶/取消
- `GET /api/conversations/{id}/messages` — 查询消息列表
- `POST /api/conversations/{id}/messages` — 流式发送（SSE）
- `PUT /api/messages/{id}` — 编辑消息
- `POST /api/messages/{id}/regenerate` — 重新生成
- `POST /api/messages/{id}/regenerate/stream` — 重新生成（流式）
- `POST /api/messages/{id}/edit-and-resend` — 编辑后重发（流式）
- `POST /api/messages/{id}/stop` — 停止生成
- `POST /api/messages/{id}/feedback` — 提交反馈
- `DELETE /api/messages/{id}/feedback` — 取消反馈
- `GET /api/models` — 模型列表
- `GET /api/user/profile` — 用户资料
- `GET /api/user/settings` — 获取设置
- `PUT /api/user/settings` — 保存设置
- `GET /api/user/data/export` — 导出数据
- `DELETE /api/user/data/clear` — 清除数据
- `POST /api/conversations/{id}/share` — 创建分享
- `GET /api/share/{token}` — 查看分享（公开）
- `DELETE /api/share/{token}` — 撤销分享
- `POST /api/attachments` — 上传附件
- `GET /api/system/config` — 系统配置
- `GET /api/mcp/servers` — MCP 服务器列表
- `PUT /api/mcp/servers/{id}` — 启停 MCP 服务器
- `GET /api/usage/summary` — 用量汇总
- `GET /api/usage/daily` — 每日用量
- `GET /api/usage/models` — 模型用量
- `POST /api/images/edits` — 图片编辑
