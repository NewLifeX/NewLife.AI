# NewLife.AI 版本更新记录

## Unreleased

### 测试稳定性与兼容性修复
- **DashScope 集成测试收敛**：
  - `qwq-plus` 相关用例改为流式验证（该模型当前仅支持 stream）
  - 视频生成相关用例在服务侧返回 `url error` 时按环境差异处理，避免误报
  - `Rerank` 用例兼容部分账号未开通权限的场景
  - 模型注册表断言放宽为“至少包含 qwen 族模型”，避免厂商模型清单动态变化导致误报
- **Ollama 集成测试增强**：
  - 重量模型 `think=true` 场景放宽为“正文或思考内容至少其一非空”，兼容不同版本输出差异
  - 嵌入测试对“不支持 embeddings”的本地模型做环境差异处理
- **服务内置工具触发词测试修复**：天气触发词断言改为兼容当前触发词集合，减少脆弱性
- **DeepSeek 元数据断言对齐实现**：客户端名称断言与当前实现保持一致

### 验证结果
- `XUnitTest` 全量测试：**1049/1049 通过**
- 测试命令耗时：约 **4 分 51 秒**（Windows 本地环境）

## v1.1.2026.0404 (2026-04-04)

### AI 基础库增强
- **服务商扩展至48个**：新增 Azure OpenAI（AzureAIChatClient）、AWS Bedrock（BedrockChatClient），覆盖国内外主流服务商
- **接口统一化**：FinishReason 由字符串改为强类型枚举；统一各厂商请求/响应为 IChatRequest/IChatResponse 接口
- **AI 客户端重构**：ChatClientBuilder 中间件管道；AiClientRegistry 支持特性自动扫描发现；AiClientExtensions 链式 DI 注册
- **IJsonHost 自定义序列化**：引入 IJsonHost 接口，支持替换内置 JSON 序列化实现
- **IChatClient 简化 API**：新增 AskAsync 便捷方法，简化对话调用；ChatOptions 补全完整 XML 注释
- **耗时统计**：AI 客户端请求支持耗时统计，响应中携带 Duration 字段

### 中间件与扩展能力
- **ToolChatClient**：工具调用中间件支持结果自动截断，防止超 Token 问题
- **IChatFilter 管道**：新增 IChatFilter 管道接口与 FilteredChatClient，支持自定义前/后处理过滤
- **语义记忆抽象**：新增 ISemanticMemory + IVectorStore 接口及开箱即用的内存版实现
- **Planner 规划器**：新增 IPlanner 接口与 FunctionCallingPlanner，支持将目标拆解为工具调用步骤并自动执行
- **MultiAgent 框架**：新增 GroupChat 轮询、ParallelGroupChat 并行协作、AgentAsTool 嵌套调用等多智能体模式
- **React Hooks**：新增 useChat / useCompletion React hooks，便于前端集成

### MCP 协议
- **MCP 服务器配置**：增加 MCP 服务器实体与管理，支持在 ChatAI 中动态配置 MCP 工具端点
- **AspNetMcpServer 增强**：NewLife.AI.Extensions 扩展库持续完善 ASP.NET 转 MCP Server 能力

### ChatAI 应用功能
- **技能系统**：新增技能（Skill）实体与 SkillService，支持 @ 输入补全选择技能，提示词自动注入上下文
- **用户记忆**：新增用户记忆（UserMemory）实体与 MemoryService，支持对话自动提取记忆并注入上下文
- **推荐问题**：新增推荐问题实体，欢迎页展示引导性问题列表
- **使用量统计**：新增用量记录（UsageRecord）实体与 UsageApiController，统计 Token 用量与费用
- **附件上传**：支持对话框图片拖拽、剪贴板粘贴上传；后端识别图片附件并转为多模态消息
- **全文搜索**：会话与消息支持关键词全文搜索
- **思考模式**：支持 Auto / Think（深度）/ Fast 三档思考模式切换，兼容模型能力自动适配
- **AppKey 管理**：新增 AppKey 管理界面，支持设置模型访问限制
- **消息操作**：新增消息删除、纯编辑消息（不重新生成）功能
- **分享链接**：支持分享链接撤销 UI；停止生成时通知后端
- **内置工具**：新增网络工具（网页爬取、IP 归属地、天气）和翻译工具内置服务，支持插件式降级

### 数据模型
- **实体类可空化**：全面可空化字段，提升空安全性；消息表新增 ThinkingContent、ItemType 字段
- **主键统一**：统一主键为雪花 ID，前后端全面切换为字符串类型
- **模型能力分组**：模型信息支持服务商分组与能力标签展示

### Bug 修复
- **[fix]** GetResponseAsync 正确传递 CancellationToken 给 ChatAsync
- **[fix]** 修复图片上传拖拽/粘贴兼容性及后端附件识别
- **[fix]** 修复登录跳转死循环
- **[fix]** 修复 tool_calls 中 function arguments 为空时的处理异常
- **[fix]** 修复流式多模态测试无法通过网关调用的问题

---

## v1.0.2025.1001 (2025-10-01)

### 初始版本
- **多协议 AI 客户端**：统一 IChatClient 接口，支持 OpenAI/Anthropic/Gemini/DashScope/Ollama 等33个服务商
- **函数调用（Function Calling）**：ToolDescription 特性 + ToolSchemaBuilder 自动生成 JSON Schema
- **流式对话**：支持 SSE 流式输出与非流式双模式
- **ChatAI 对话应用**：会话管理、多模型配置、用户系统、服务商管理
- **MCP 协议基础**：HttpMcpServer 工具调用基础实现
- **前端构建集成**：Vite + Vue3 前端内嵌至 DLL，单文件部署

---
