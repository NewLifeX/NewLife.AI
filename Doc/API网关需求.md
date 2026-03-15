# API 网关 / 函数调用 / MCP 需求

> 系统：对话交互式 AI 系统  
> 更新：2026-03-15  
> 版本：v1.0

---

## 1. API 网关

### 1.1 定位与目标

系统对外提供兼容主流 AI 标准协议的统一 API，使其他业务系统无需关心具体模型提供商的差异，通过本系统的 API 网关即可调用已配置好的模型。

核心价值：
- **统一入口**：一个 API 地址，接入 33 个模型服务商。
- **协议兼容**：对外暴露标准 API，内部适配不同提供商的原生协议。
- **透明路由**：根据请求中的模型名称，自动路由到对应的模型提供商。

### 1.2 支持的 API 协议

API 网关对外兼容以下标准协议（实现在 NewLife.AI 基础库中）：

| # | 协议 | 路径 | 说明 |
|---|------|------|------|
| 1 | OpenAI Chat Completions | `/v1/chat/completions` | 业界最广泛使用的标准，支持对话生成、流式输出、函数调用、视觉理解 |
| 2 | OpenAI Response API | `/v1/responses` | 用于 o3/o4-mini/gpt-5 等推理模型，支持思考概要和完整思考过程 |
| 3 | Anthropic Messages API | `/v1/messages` | Claude 系列模型原生协议，支持交错思考、扩展上下文窗口、工具使用 |
| 4 | Google Gemini API | `/v1/gemini` | Google AI Gemini 模型原生协议，支持多模态输入、代码执行、搜索增强 |
| 5 | 图像生成 API | `/v1/images/generations` | OpenAI 图像生成协议，支持文本到图像生成 |
| 6 | 图像编辑 API | `/v1/images/edits` | OpenAI 图像编辑协议，支持提示编辑和修复 |

> **协议适配策略**：任何符合 OpenAI Chat Completion API 协议的模型提供商均可直接接入。Anthropic、Gemini 等使用各自原生协议的提供商，由 NewLife.AI 内部做协议转换。

### 1.3 支持的模型服务商

NewLife.AI 基础库内置以下 33 个模型服务商适配（按协议分类）：

#### OpenAI Chat Completions 协议（28 个）

| # | 服务商 | Provider 名称 | 说明 |
|---|--------|--------------|------|
| 1 | OpenAI | OpenAI | GPT-4o、o3、o4-mini、gpt-5 等 |
| 2 | Azure AI Foundry | AzureAI | 微软 Azure 托管的 OpenAI 及开源模型 |
| 3 | 阿里百炼 | DashScope | Qwen/通义千问全系列 |
| 4 | DeepSeek | DeepSeek | DeepSeek-V3、R1 推理模型，支持思考过程 |
| 5 | 火山方舟 | VolcEngine | 字节跳动豆包系列 |
| 6 | 智谱 AI | Zhipu | GLM-4/CogView 系列 |
| 7 | 月之暗面 | Moonshot | Kimi 系列，超长上下文，支持思考过程 |
| 8 | 腾讯混元 | Hunyuan | 混元大模型系列 |
| 9 | 百度千帆 | Qianfan | 文心一言系列 |
| 10 | 讯飞星火 | Spark | 星火认知大模型 |
| 11 | 零一万物 | Yi | Yi 系列 |
| 12 | MiniMax | MiniMax | MiniMax 多模态大模型 |
| 13 | 硅基流动 | SiliconFlow | 推理加速聚合平台 |
| 14 | x.AI | XAI | Grok 系列 |
| 15 | GitHub Models | GitHubModels | GitHub 模型市场 |
| 16 | OpenRouter | OpenRouter | 模型聚合代理平台 |
| 17 | Ollama | Ollama | 本地部署开源模型 |
| 18 | 小米 MiMo | MiMo | MiMo 推理模型，支持思考过程 |
| 19 | Together AI | TogetherAI | 开源模型云端推理 |
| 20 | Groq | Groq | LPU 高速推理平台 |
| 21 | Mistral AI | Mistral | Mistral/Mixtral/Codestral 系列 |
| 22 | Cohere | Cohere | Command R 企业级模型 |
| 23 | Perplexity | Perplexity | 联网搜索增强 AI |
| 24 | 无问芯穹 | Infini | 国产推理云平台 |
| 25 | Cerebras | Cerebras | 晶圆级芯片超高速推理 |
| 26 | Fireworks AI | Fireworks | 高速模型推理平台 |
| 27 | SambaNova | SambaNova | RDU 架构推理平台 |
| 28 | 小马算力 | XiaomaPower | GPU 算力平台 |

#### 本地推理引擎（3 个）

| # | 服务商 | Provider 名称 | 说明 |
|---|--------|--------------|------|
| 29 | LM Studio | LMStudio | 桌面端本地模型运行工具 |
| 30 | vLLM | vLLM | 高吞吐量推理引擎（自部署） |
| 31 | OneAPI | OneAPI | 开源 LLM API 管理分发系统 |

#### Anthropic Messages 协议（1 个）

| # | 服务商 | Provider 名称 | 说明 |
|---|--------|--------------|------|
| 32 | Anthropic | Anthropic | Claude 系列，支持交错思考 |

#### Google Gemini 协议（1 个）

| # | 服务商 | Provider 名称 | 说明 |
|---|--------|--------------|------|
| 33 | Google AI | Gemini | Gemini 系列，多模态 |

> **自定义扩展**：用户还可添加任何兼容上述协议的自定义模型提供商，通过继承 `OpenAiProvider` / `AnthropicProvider` / `GeminiProvider` 或实现 `IAiProvider` 接口即可接入。

### 1.4 API 网关路由

- 请求通过 `model` 字段指定模型名称，网关根据 `ModelConfig` 表自动匹配对应的提供商和接口地址。
- 网关自动处理请求格式转换（如 OpenAI 格式 → Anthropic Messages 格式）。
- 支持流式和非流式两种响应模式。
- 请求和响应均记录到用量统计，并按 AppKey 维度独立统计。
- API 网关请求通过 `Authorization: Bearer <appkey>` 进行认证，系统校验 AppKey 有效性后放行。

### 1.5 API Key 管理

系统支持 **应用密钥（AppKey）** 管理，用于外部系统通过 API 网关调用模型服务时的身份认证和用量追踪。

#### 1.5.1 AppKey 生命周期

- **创建**：用户在设置页或管理后台创建 AppKey，系统生成不可猜测的随机密钥（如 `sk-` 前缀 + 随机字符串）。
- **启用/禁用**：管理员可随时启用或禁用某个 AppKey，禁用后该 Key 的请求立即返回 `401 Unauthorized`。
- **删除**：删除 AppKey 后不可恢复，关联的用量记录保留。

#### 1.5.2 AppKey 属性

| 属性 | 说明 |
|------|------|
| 名称 | 用户自定义的标识名称，便于区分用途（如"业务系统A"、"测试环境"） |
| 密钥 | 系统生成的随机字符串，创建时仅展示一次 |
| 所属用户 | 创建该 Key 的用户，用量归属到该用户 |
| 启用状态 | 是否允许使用 |
| 过期时间 | 可选，到期后自动失效 |

#### 1.5.3 用量归属

- 通过 AppKey 发起的 API 网关请求，用量同时记录到 **用户维度** 和 **AppKey 维度**。
- 用户可在设置页查看各 AppKey 的独立用量统计。
- 管理后台可查看全局 AppKey 用量排行。

### 1.6 上游限流重试

- 当上游模型服务返回 HTTP 429（限流）时，在尚未产生任何输出前自动重试。
- 最多重试 5 次，采用指数退避（1s、2s、4s、8s…最大 30s）+ 0~250ms 随机抖动。
- 5 次均失败后返回 `RATE_LIMITED` 错误。

---

## 2. 函数调用（Function Calling）

### 2.1 概述

所有 API 接口均支持函数调用能力，使 AI 能够在生成回复过程中调用外部工具和函数。这是构建 Agent 化产品的核心基础。

### 2.2 调用流程

```
用户提问 → AI 判断需要调用工具 → 发出 tool_call → 后端执行工具 → 返回结果 → AI 继续推理 → 最终回复
```

支持单次和多次工具调用，AI 可根据工具返回结果决定是否继续调用其他工具。

### 2.3 SSE 事件扩展

函数调用在 SSE 流中增加以下事件类型：

#### tool_call_start — 工具调用开始

```
data: {"type":"tool_call_start","toolCallId":"call_001","name":"get_weather","arguments":"{\"city\":\"北京\"}"}
```

#### tool_call_done — 工具调用完成

```
data: {"type":"tool_call_done","toolCallId":"call_001","result":"{\"temp\":25,\"weather\":\"晴\"}","success":true}
```

#### tool_call_error — 工具调用失败

```
data: {"type":"tool_call_error","toolCallId":"call_001","error":"服务不可用"}
```

### 2.4 前端展示

- 工具调用过程在对话流中实时展示（详见需求规格说明 §6.10）。
- 用户可查看每次调用的参数和返回结果。
- 调用失败时显示错误原因。

### 2.5 原生 .NET 工具注册（Native Tool Registration）

#### 2.5.1 设计理念

借鉴 OpenClaw.NET 的工程实践，NewLife.AI 基础库通过 **反射 + XML 注释** 机制，让开发者直接将 C# 方法注册为 AI 可调用工具，无需手写 JSON Schema，无需处理序列化/反序列化。

> **实现选择**：本项目采用框架依赖发布（Framework-Dependent），运行时反射完整可用，因此直接以反射驱动工具注册和参数反序列化，无需 NativeAOT 源生成方案。

在智能体系统中，让 LLM 准确理解并调用本地工具是最核心的机制。原生 .NET 工具注册方案通过 `UseFunctionInvocation` 与反射机制，将 C# 方法签名实时翻译为模型能理解的标准化 JSON Schema；当模型决定调用工具时，又自动将模型输出的 JSON 文本反序列化为强类型 C# 对象并精准触发本地方法执行。

```
C# 方法签名 + XML 注释
       ↓（反射 + 源生成）
标准化 JSON Schema（tool definition）
       ↓（注入模型上下文）
模型决策调用 → 输出 JSON 参数
       ↓（自动反序列化）
C# 强类型对象 → 精准调用本地方法
```

#### 2.5.2 工具定义方式

开发者用标准 C# 方法定义工具，XML 文档注释自动驱动 tool 描述生成：

```csharp
/// <summary>根据城市名称查询当前天气</summary>
/// <param name="city">城市名称，如"北京"、"上海"</param>
/// <param name="unit">温度单位，celsius（摄氏）或 fahrenheit（华氏）</param>
[ToolDescription("get_weather")]
public async Task<WeatherResult> GetWeatherAsync(String city, String unit = "celsius")
{
    // 查询天气 API ...
}
```

| 要素 | 对应 JSON Schema 字段 | 说明 |
|------|----------------------|------|
| `<summary>` XML 注释 | `description` | AI 理解该工具用途的主要依据 |
| `<param>` XML 注释 | 参数的 `description` | 描述每个参数的含义和取值范围 |
| 方法名 / `[ToolDescription]` | `name` | 工具标识名，特性可覆盖默认方法名 |
| 强类型参数（`String`/`Int32`/`Boolean` 等） | `type` / `properties` | 自动映射为 JSON Schema 基础类型 |
| 复杂类型参数（`class` / `record`） | `object` + `properties` | 递归展开属性，属性注释同步生成描述 |
| 有默认值的参数 | `required` 数组排除 | 不含在 required 列表中，为可选参数 |
| 返回值 | — | 自动序列化为 JSON 字符串回填给模型 |

#### 2.5.3 批量注册

支持将整个服务类或程序集中的工具方法批量注册：

```csharp
// 注册单个委托
client.AddTool("get_weather", GetWeatherAsync);

// 注册整个服务类中所有标注 [ToolDescription] 的方法
client.AddTools<WeatherService>();

// 扫描当前程序集，自动注册所有 [ToolDescription] 方法
client.AddToolsFromAssembly(Assembly.GetExecutingAssembly());
```

#### 2.5.4 执行链路

框架内部通过 `FunctionInvocationFilter`（对标 SK 的 `IFunctionInvocationFilter`，见 [AI编排框架需求.md](AI编排框架需求.md) §1.3）拦截整个调用链：

| 阶段 | 说明 |
|------|------|
| **注册** | 扫描 `[ToolDescription]` / `[Description]` 注解，反射提取方法签名，生成 `AiTool`（含 JSON Schema） |
| **注入** | 构建请求时将 `AiTool[]` 按目标协议（OpenAI / Anthropic / Gemini）序列化并注入 tools 数组 |
| **拦截** | 模型返回 `tool_call` 后，`FunctionInvocationFilter` 自动拦截，无需应用层手动处理 |
| **反序列化** | 将模型生成的 JSON 参数文本反序列化为对应 C# 强类型对象 |
| **执行** | 通过反射调用本地方法，超时使用 `CancellationToken` 控制，异常被捕获并转为工具错误 |
| **回填** | 执行结果序列化后作为 `tool` 角色消息追加到对话历史，触发模型继续推理 |

#### 2.5.5 与 MCP 工具的关系

| 维度 | 原生 .NET 工具（§2.5） | MCP 工具（第 3 章） |
|------|--------------------|------------------|
| 部署方式 | 进程内，直接引用 .NET 程序集 | 进程外，通过标准协议连接 |
| 注册方式 | C# 方法 + XML 注释 / 特性 | MCP Server 动态发现 |
| 调用延迟 | 极低（纳秒级方法调用） | 低（进程间通信） |
| 适用场景 | 核心业务逻辑、数据库操作、内部服务 | 第三方工具、外部 API、跨语言工具 |
| 热更新 | 需重启应用 | Server 独立更新，无需重启 |
| 权限隔离 | 与宿主进程共享权限 | 沙箱隔离，可独立控制权限边界 |

两者共用相同的 SSE 事件格式（§2.3）和前端 `ToolCallBlock` 组件，对用户完全透明。

---

## 3. MCP 工具调用

### 3.1 概述

系统支持 **MCP（Model Context Protocol）** 协议，使 AI 能够通过标准化接口与外部工具、数据源、API 进行交互。MCP 是 Agent 化的关键基础设施。

### 3.2 架构

```
用户 ←→ ChatAI（Host） ←→ MCP Client ←→ MCP Server（工具/数据源）
```

- **ChatAI 作为 MCP Host**：管理 MCP 连接和工具授权。
- **MCP Client**：由 NewLife.AI 基础库提供，负责与 MCP Server 通信。
- **MCP Server**：外部工具提供方，可以是本地进程或远程 HTTP 服务。

### 3.3 工具管理

- 管理员在后台配置可用的 MCP Server 列表（地址、认证信息等）。
- 系统自动从 MCP Server 抓取可用工具列表（Tool Discovery）。
- 支持启用/禁用特定工具。

### 3.4 会话级工具调用

- 用户在会话中发送消息时，后端自动将已启用的 MCP 工具列表注入到模型上下文中。
- AI 根据用户意图决定是否调用 MCP 工具。
- 工具调用过程通过 SSE 事件实时推送到前端展示（复用 §2.3 的事件格式）。

### 3.5 与函数调用的关系

- MCP 工具调用是函数调用的一种实现方式。
- 在 SSE 事件中，MCP 工具调用与普通函数调用使用相同的事件格式。
- 前端展示也复用相同的 ToolCallBlock 组件。
