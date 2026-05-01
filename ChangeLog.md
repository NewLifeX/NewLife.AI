# NewLife.AI 版本更新记录

## v1.2.2026.0502 (2026-05-02)

### IChatHandler 架构重构
- **对话内核升级**：引入 IChatHandler/IChatContext 三段式架构（OnBefore/OnAfter），替代 IChatPipeline 体系，实现 LlmCoreHandler/SystemPromptHandler/PersistHandler 等核心处理器；网关与应用层共用同一处理链
- **IChatInterceptor**：新增拦截器接口，Enricher/PostProcessor 原生化为 IChatHandler，删除独立适配层
- **内容安全过滤**：上下文截断/压缩重构为独立 IChatFilter，增强内容安全与审计日志能力
- **TraceId 链路追踪**：新增 TraceId 字段及链路追踪拦截器，支持全链路请求追踪

### DashScope 多模态增强
- **Omni 全模态与实时对话**：支持 DashScope Omni 模型，新增实时对话能力
- **Files API 文件上传**：新增 FileContent 类，支持通过 DashScope Files API 上传文件并在对话中引用
- **图像编辑协议升级**：DashScopeChatClient 多模态重构，图像编辑支持原生多模态协议，用量字段扩展

### DeepSeek 专属支持
- **DeepSeekChatClient**：新增 DeepSeek 专属客户端，适配 reasoning_content 回传与 DeepSeek v4 参数
- **Qwen3 系列**：新增 qwen3.6 等系列模型能力标记与测试覆盖

### 网关能力增强
- **Anthropic/Gemini 兼容**：增强对 Anthropic 与 Gemini 协议的认证与响应格式适配
- **JSON 自动适配**：网关支持 snake_case/camelCase JSON 自动转换
- **配额校验**：网关处理链集成配额校验，支持资源授权与 ACL 过滤
- **进程内集成测试**：集成测试切换为进程内自托管网关模式，提升回归可靠性

### ChatAI 应用功能
- **对话预设**：新增对话预设功能，支持一键切换多场景配置，自动填充输入框引导文本
- **Artifact 预览**：支持代码块一键预览（HTML/SVG/Mermaid）及流式 Artifact 实时渲染与视频生成展示
- **对话分叉**：支持从任意历史消息创建分叉新会话
- **用户数据导入**：支持 JSON 格式用户数据批量导入，前后端功能完善
- **个性化定制**：AI 称呼/背景风格/指令自定义，全链路适配
- **工具调用可视化**：新增 ShowToolCalls 用户设置，支持 AI 工具调用事件流展示
- **系统欢迎语**：支持系统欢迎语自定义，前后端全链路适配
- **模型名称显示**：消息气泡支持显示当前对话所用模型名称
- **推理计时**：前端支持显示模型推理耗时
- **推荐问题**：推荐问题支持配置短标题展示

### 知识库与多项目
- **项目隔离**：核心实体增加 ProjectId 字段，支持多项目数据隔离
- **知识库重构**：AgentProject 重构为知识库，支持文档批量上传与网页递归爬取任务
- **自学习系统**：新增自学习过滤器与对话分析服务，可配置最低字数阈值

### 用量计费与配额
- **计费系统**：用量记录统一费用统计，前端展示费用明细；Conversation 增加 AppKeyId 字段
- **配额限制**：引入配额与资源授权系统，支持 Token 用量限额管理

### 模型能力扩展
- **多模态能力**：扩展模型能力体系，支持音频/视频生成、上下文长度等字段
- **OpenAI 兼容路由**：支持多模态能力接口与 OpenAI 兼容 API 路由
- **模型服务统一**：重构为 ModelService 统一模型与客户端管理，支持批量发现与同步

### 架构重构
- **删除 ChatData 子项目**：将 NewLife.ChatData 合并回 ChatAI，简化项目结构
- **IChatSetting 解耦**：引入 IChatSetting 接口，实现三层配置分离，ChatSetting 全面 DI 化
- **统一工具描述**：工具描述统一为 Description 特性，支持后台重新扫描注册

### 测试质量
- **集成测试增强**：新增 E2E 与 WireMock 离线录制，DashScope/Ollama/DeepSeek 集成测试全面收敛（1049/1049 通过）
- **OllamaFactAttribute**：本地服务不可用时自动跳过，兼容 CI 环境

### Bug 修复
- **[fix]** 修正 ExtendableConverter 序列化 null 值问题，修复阿里百炼 OpenAI 兼容协议 stream_options 传输错误
- **[fix]** 修正 GLM-5.1 调用错误，修复 DeepSeek 协议客户端参数
- **[fix]** 修复 RemoveAsync 空集合内存泄漏
- **[fix]** 修复 BackgroundGenerationService 流式订阅 race condition，避免订阅者永久挂起
- **[fix]** 修复 DeepSeek 工具调用需回传 reasoning_content

---

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
