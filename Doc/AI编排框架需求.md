# AI 编排框架 / 多 Agent 协作 / 前端 Hook 需求

> 归属系统：对话交互式 AI 系统  
> 版本：v0.9（已完成）  
> 日期：2026-03-15  
> 参考：Microsoft Semantic Kernel / AutoGen / Vercel AI SDK  
> 实现位置：`NewLife.AI` 核心库 + `Web/src/hooks/`

---

## 1. AI 编排框架扩展（SK 模式）

参考：Microsoft Semantic Kernel Filters / Memory / VectorStore / Planner

### 1.1 语义记忆（Semantic Memory）

- **描述**：对文本片段生成浮点向量嵌入，并支持相似度检索，为 RAG 场景提供统一记忆接口
- **用户故事**：作为开发者，我希望调用 `ISemanticMemory.SaveAsync` 存储文本、调用 `SearchAsync` 检索相似内容，以便构建知识库问答 Agent
- **验收条件**：
  - [x] `ISemanticMemory` 接口定义 `SaveAsync / GetAsync / SearchAsync / RemoveAsync`
  - [x] `MemoryEntry` 数据类型（Id、Text、Vector、Metadata）
  - [x] `InMemorySemanticMemory` 内存实现，使用余弦相似度
  - [x] 单元测试：存入 3 条，检索返回正确 Top-N
- **优先级**：Must

### 1.2 向量存储（Vector Store）

- **描述**：独立的向量存储抽象，与 Memory 分离，可承载更大规模数据，将来可替换为 Redis/Qdrant 等后端
- **用户故事**：作为开发者，我希望用 `IVectorStore` 存取高维向量记录，以便独立于业务逻辑管理向量索引
- **验收条件**：
  - [x] `IVectorStore` 接口定义 `UpsertAsync / GetAsync / SearchAsync / DeleteAsync`
  - [x] `VectorRecord`（Id、Vector、Payload）
  - [x] `InMemoryVectorStore` 实现，余弦距离 Top-K
  - [x] 单元测试
- **优先级**：Must

### 1.3 过滤器（Filters）

- **描述**：在 IChatClient 调用链和函数调用链中插入横切关注点（审计、内容安全、限流），对标 SK 的 `IFunctionInvocationFilter`
- **用户故事**：作为开发者，我希望通过注册 Filter 在 Chat 调用前后注入自定义逻辑，而无需修改核心代码
- **验收条件**：
  - [x] `IChatFilter` 接口（`OnChatAsync`，有 next delegate）
  - [x] `IFunctionInvocationFilter` 接口（`OnFunctionInvocationAsync`，有 next delegate）
  - [x] `FilteredChatClient : DelegatingChatClient`，支持注册多个 IChatFilter 链式执行
  - [x] 单元测试：验证 Filter 执行顺序和 context 透传
- **优先级**：Must

### 1.4 规划器（Planner）

- **描述**：给定自然语言目标，规划器利用模型的 Function Calling 能力自动生成并顺序/并行执行一组 Tool 调用步骤
- **用户故事**：作为开发者，我希望调用 `IPlanner.CreatePlanAsync(goal, tools)` 获得步骤列表，再调用 `ExecuteAsync` 逐步执行，以便实现复杂多步骤任务
- **验收条件**：
  - [x] `IPlan` 接口（Steps、Status、ExecuteAsync）
  - [x] `IPlanner` 接口（`CreatePlanAsync`）
  - [x] `FunctionCallingPlanner`：第一次调用模型生成工具调用序列，后续逐步执行
  - [x] `PlanStep`（ToolName、Arguments、Result、Status）
  - [x] 单元测试：Mock IChatClient 验证规划流程
- **优先级**：Must

---

## 2. 多 Agent 协作框架（AutoGen 模式）

参考：Microsoft AutoGen AgentChat 协议 / ConversableAgent / GroupChat  
实现位置：`NewLife.AI` 核心库 `Agents/` 目录

### 2.1 AgentChat 协议

- **描述**：标准化 Agent 间消息格式，包含 TextMessage / ToolCallMessage / ToolCallResultMessage / SystemMessage，确保不同 Agent 间通信格式一致
- **验收条件**：
  - [x] `AgentMessage` 抽象基类（Source、Type）
  - [x] `TextMessage`、`SystemMessage`、`StopMessage`
  - [x] `ToolCallMessage`（FunctionCallContent[]）、`ToolCallResultMessage`（FunctionResultContent[]）
  - [x] 消息到 `ChatMessage` 的转换方法
- **优先级**：Must

### 2.2 ConversableAgent

- **描述**：具有系统提示词、工具集、终止条件的自主 Agent，能响应消息并返回 AgentMessage
- **验收条件**：
  - [x] `IAgent` 接口（Name、Description、`HandleAsync(messages, ct)`）
  - [x] `ConversableAgent`（IChatClient、SystemPrompt、ToolInfos[]、MaxRounds）
  - [x] 支持工具调用循环（调用工具后将结果反馈 → 继续生成）
  - [x] 单元测试：纯文本回复 + 工具调用两种场景
- **优先级**：Must

### 2.3 GroupChat

- **描述**：多 Agent 按策略轮流发言，管理全局对话历史，支持终止检测
- **验收条件**：
  - [x] `IGroupChatSelector` 接口（`SelectNextAsync(agents, history, ct)`）
  - [x] `RoundRobinSelector` 轮询选择器
  - [x] `GroupChat`（Agents、Selector、MaxRounds、`RunAsync(initialMessage, ct)`）
  - [x] 单元测试：2 个 Mock Agent 按 RoundRobin 轮流，StopMessage 终止
- **优先级**：Must

---

## 3. 前端 Hook 体系（Vercel AI SDK 模式）

参考：Vercel AI SDK `useChat` / `useCompletion`  
实现位置：`Web/src/hooks/`（React + TypeScript）

### 3.1 useChat Hook

- **描述**：封装完整的多轮对话 SSE 流式状态管理，统一处理消息列表、加载状态、流式接收、错误处理和操作（append/reload/stop）
- **验收条件**：
  - [x] 返回 `{ messages, isLoading, error, append, reload, stop, setMessages }`
  - [x] `append(message)` 触发 SSE 流式请求，逐块拼接到最后一条消息
  - [x] `stop()` 中止当前流
  - [x] `reload()` 重新生成最后一条 AI 消息
  - [x] `onFinish(message)` / `onError(error)` 回调
  - [x] TypeScript 类型完整
- **优先级**：Must

### 3.2 useCompletion Hook

- **描述**：封装单次文本补全（非对话）的流式状态管理，适合纯文本续写、摘要生成等场景
- **验收条件**：
  - [x] 返回 `{ completion, isLoading, error, complete, stop }`
  - [x] `complete(prompt)` 触发流式请求，累积到 `completion` 字符串
  - [x] `stop()` 中止当前流
  - [x] `onFinish(completion)` / `onError(error)` 回调
  - [x] TypeScript 类型完整
- **优先级**：Should
