# NewLife.AI 版本更新记录

## v1.3.2026.0608 (2026-06-08)

### 知识进化层
- **知识进化层落地**：自动从对话中提炼知识构建可检索知识库，支持 TOC 目录式浏览与向量语义检索
- **记忆整合服务**：支持记忆去重/合并/过期机制及前端触发
- **学习记忆功能**：新增学习记忆功能及设置项支持，自学习逻辑统一为 LearningHandler
- **痛觉记忆与好奇心队列**：支持痛觉记忆/好奇心队列的项目维度管理与 Handler 拆分

### TTS 语音合成
- **DashScope TTS**：完善 DashScope TTS 语音合成及模型命名规范
- **CosyVoice V3.5**：CosyVoice TTS 升级 V3.5，新增流式合成接口文档
- **前端 TTS 接口**：新增前端专用 TTS 接口与配置支持

### 多智能体增强
- **反思代理与评审代理**：新增 ReflectionAgent/ReviewAgent 及单元测试
- **复杂任务拆分**：支持复杂任务拆分与批量技能并行聚合
- **子代理执行**：重构技能工具，新增子代理执行模式

### 人机决策检查点
- **人类决策检查点**：新增 HumanCheckpoint 功能，支持 AI 多路径人工选择
- **多问题组决策**：支持多问题组模式的决策检查点
- **澄清模式**：支持澄清模式与可视化 Widget 工具，深度思考等同澄清

### 工具调用增强
- **ToolCallContext 上下文透传**：引入 ToolCallContext 实现工具调用上下文透传
- **工具熔断器**：引入工具 Provider 熔断器，提升健壮性与容错
- **权限体系**：支持工具结构化错误返回与三档权限体系
- **会话级可见性过滤**：会话级工具可见性过滤与接口统一适配
- **工具响应路由**：支持工具响应路由，优化 ToolCallId 及目录调用
- **客户端管道重构**：重构客户端管道组装，提升可扩展性与复用性

### 嵌入与向量检索
- **Embedding 合并**：合并 Embedding 能力至 OpenAIChatClient，移除独立实现
- **HashTextEmbedder v2**：HashTextEmbedder 升级 v2，新增向量存储全量测试
- **知识库向量检索**：支持知识库向量检索与本地哈希嵌入
- **TOC 知识综合**：知识综合与目录功能实现及检索路由重构

### IChatHandler 架构演进
- **链路正序化**：IChatHandler 链路正序化，合并会话统计逻辑
- **架构升级**：ChatHandlerChain 架构升级，顺序解耦与特性锚点
- **IChatInterceptor 移除**：IChatHandler 三段式能力声明重构，移除 IChatInterceptor
- **Handler 统一基类**：统一 Handler 继承 ChatHandlerBase，提升兼容性与一致性

### 消息流重构
- **场景拆分**：重构消息流，按场景拆分为 Web/Channel/Gateway
- **链路统一**：网关/渠道消息流链路统一与完整链能力开关
- **System 消息注入**：重构 system 消息注入为中间件分段拼接机制

### 用量统计与计费
- **统一用量统计**：统一模型服务用量统计，支持 LLM/嵌入自动记录
- **费用展示**：支持消息费用统计与展示，全端 totalCost 字段
- **流式用量合并**：LLM 流式用量合并机制，完善 Token 统计与多轮支持
- **用量记录优化**：重构 UsageRecordHandler，简化用量处理逻辑

### 前端增强
- **SSE 心跳保活**：引入 SSE 心跳保活机制及异常处理优化
- **Mermaid 增强**：Mermaid 渲染健壮性增强，支持 SVG 自适应与标签内换行
- **Markdown XSS 防护**：支持 rehype-raw 并增强 Markdown XSS 防护
- **主题色体系**：统一全站 UI 颜色体系，支持品牌色动态注入
- **移动端适配**：重构设置页面，优化移动端适配体验
- **代码块折叠与跳转**：优化代码块折叠与消息跳转体验

### 推荐问题与分享
- **推荐问题缓存**：推荐问题支持缓存策略与热度统计，表结构重构
- **分享链接有效期**：分享链接支持分钟级有效期自定义配置，锚点消息分享
- **分享交互升级**：分享弹窗复制功能及多语言文案支持

### 兼容性与性能
- **net45/net462 兼容**：兼容 net45，新增 net4.6.2 目标框架
- **提示缓存**：启用提示缓存配置项及全链路支持
- **会话缓存优化**：会话和对话消息采用单对象缓存，减少查询

### 其他优化
- 部门权限支持祖先链继承，新增 DepartmentHelper
- 系统设置支持更多访问与模型能力配置
- 支持 response_format 透传及测试用例完善
- 支持 IChatRequest 透传生成参数到消息流
- 会话与消息支持软删除，新增 Enable 字段
- 新增助手触发词字段及全链路支持
- 消息体增加模型编码字段，前后端同步
- 思考过程收缩功能全链路支持与前端适配
- 自动推断联网意图，智能激活搜索/爬取能力
- 支持第三方模型流式对话兼容 OpenAI 协议
- 支持配置工具最大轮次与结果长度
- 架构文档升级：两层结构与 IChatHandler 处理器链

### Bug 修复
- **[fix]** 修复 DeepSeek 工具调用推理内容回传问题
- **[fix]** 修复滚动穿透与抖动，优化 ChatPage 体验
- **[fix]** 修复新用户设置未持久化为数据库的问题
- **[fix]** 修复异步链中工具调用上下文丢失问题
- **[fix]** 修复 LLM 生成 Mermaid 代码常见格式错误
- **[fix]** 修复 Mermaid classDef 语法兼容性及无效属性

---

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
