# NewLife.AI

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.AI?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.AI?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.AI?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.AI?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml)
[![NativeAOT](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml)
<!-- metrics:start -->
[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-37%2F37-brightgreen?logo=dotnet)](Doc/AOT兼容性分析.md)
[![TFM](https://img.shields.io/badge/TFM-5%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.AI)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20x86%20%7C%20arm64%20%7C%20arm%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.AI?logo=github)](https://github.com/NewLifeX/NewLife.AI/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ℹ️ 本中文版与 [English README](README.md) 同批维护；若两者内容不一致，以英文版为对外口径。


## 项目介绍

**NewLife.AI** 是面向 .NET 生态的**开源 AI 基础库**，通过统一的 `IChatClient` 接口封装 **46 个主流大模型服务商**，内置函数调用、MCP 协议、流式输出、多模态、多智能体等能力，可作为 NuGet 包嵌入任意 .NET 项目（`net45 → net10.0`，原生 AOT 就绪）。

库内置 **31 种运行时语言**（英文为源文与兜底），覆盖 Windows / Linux / macOS / Android / iOS 与 x64 / x86 / arm64 / arm / loongarch64 / riscv64 / s390x。

**NewLife.ChatAI** 是构建于 NewLife.AI 之上的**完整 Web 对话应用**（ASP.NET Core），提供即开即用的多模型对话前端、统一 AI 网关与自动记忆进化，既可独立部署，也可通过 NuGet 嵌入已有 ASP.NET Core 项目。

| 产品/工程 | 定位 | 说明 |
|-----------|------|------|
| `NewLife.AI` | 开源核心 AI 基础设施 | 统一接入主流模型服务商，覆盖底层 AI 能力 |
| `NewLife.ChatAI` | 开源标准 Web 对话系统 | 本仓库 —— 标准对话应用与 AI 网关 |

---

## 核心特性

- **原生 AOT 就绪**：net8.0/net10.0 启用 `IsAotCompatible`，37 场景 NativeAOT 冒烟矩阵覆盖 Windows / Linux / macOS 与 x64 / arm64 / loongarch64
- **31 种运行时语言**：英文源文 + 编译期语言表，缺失译文自动回退英文，新增语言无需改代码
- **46 家 AI 服务商，6 种协议**：OpenAI / Anthropic / Gemini / 通义 DashScope / Ollama / AWS Bedrock，一行代码任意切换
- **统一 `IChatClient` 接口**：对齐 MEAI 规范，单轮、流式、函数调用、多模态全部统一 API
- **函数调用（工具）**：`[ToolDescription]` 特性自动生成 JSON Schema，`ToolChatClient` 多轮循环，内置搜索 / 天气 / 翻译 / 网页抓取 / IP 定位等工具
- **MCP 双向支持**：客户端对接外部 MCP Server（stdio / HTTP SSE），服务端将本系统工具暴露为标准 MCP 服务
- **完整对话内核**：`IChatHandler` 三段式处理链（OnBefore → Execute → OnAfter），内置 Handler（技能激活 / 记忆注入 / 持久化 / 用量统计 / 标题生成），可插拔 `IChatFilter`
- **用户记忆进化**：自动从对话中提取 10 类结构化记忆，越用越懂用户
- **统一 AI 网关**：兼容 OpenAI / Anthropic / Gemini / Ollama 协议，snake_case/camelCase 自动适配，AppKey 多租户，上游 429 指数退避重试
- **技能系统**：Markdown 提示词复用，`@` 递归引用，触发词自动激活
- **多智能体与规划器**：`ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner` / 反思修正
- **React 19 Web 前端**：SSE 流式 + 对话预设 + 工具调用可视化 + 推理展示 + 多模态输入（图片/文档）
- **嵌入/向量检索**：内置 HashTextEmbedder v2 与向量存储，支持语义搜索
- **全局系统指令**：全局系统指令统一注入所有对话行为约束
- **工具调用增强**：ToolCallContext 上下文透传、结构化错误返回、工具渐进式发现
- **编程智能体 CodingAgent**：支持分析/只读与代码修改任务分流执行，自动选择最优工具链
- **人机决策检查点**：人类可在 AI 多路径中实时选择，支持多问题组模式的决策检查与人工干预
- **IM 多渠道接入**：内置 11 个 IM 渠道（企业微信/公众号/微信客服/QQ/Discord/钉钉/飞书/Telegram/Slack 等），支持多渠道对话接入
- **Docker 部署**：支持 ChatAI 一键 Docker 部署

---

## 支持的 AI 服务商

46 家服务商，**9 个独立协议客户端 + 37 个 OpenAI 兼容适配**。

### 独立协议实现（9 个）

| 服务商 | 协议 | 特性 |
|--------|------|------|
| OpenAI | ChatCompletions / Responses | 视觉 / 函数调用 / 图像生成 / o3 推理 |
| DeepSeek | DeepSeek API | reasoning_content / DeepSeek v4 专属参数 |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| 阿里云 | DashScope | qwen-plus / qwen-max / Omni 全模态 / Files API |
| Azure AI | Azure OpenAI | 部署名称 URL + api-key |
| Ollama | Ollama API | 本地 llama / deepseek / qwen |
| AWS Bedrock | SigV4 签名 | Claude / Llama / Titan / Mistral |
| NewLifeAI | 级联代理 | 聚合多服务商 |
| （其他） | OpenAI 兼容 | 37 个兼容平台 |

### OpenAI 兼容家族（37 个）

豆包（火山引擎）、智谱清言（GLM）、文心一言、月之暗面（Kimi）、MiniMax、阶跃星辰（StepFun）、百川、讯飞星火、零一万物、Moonshot、Mistral、Perplexity、Cohere、Together AI、Fireworks、OpenRouter、SiliconCloud、DeepInfra、Groq、Cerebras、Hyperbolic、Nebius、Novita、Lepton、302.AI、xAI（Grok）……以及其他 OpenAI 兼容平台。

所有服务商通过 `[AiClient]` 特性声明，`AiClientRegistry` 启动时自动扫描注册，新增服务商零配置。

---

## 快速开始

### 1. 仅使用 AI 基础库（SDK）

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// 单轮问答
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("用三句话介绍一下大语言模型");
Console.WriteLine(reply);

// 多角色消息（元组数组，无需手动构造 ChatMessage）
var reply2 = await client.ChatAsync([
    ("system", "你是一名专业的 C# 开发助手"),
    ("user", "解释一下 ValueTask 和 Task 的区别"),
]);

// 流式输出
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "写一首关于代码的短诗" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. ASP.NET Core 依赖注入

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// 注册服务
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Keyed 多服务商并存
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// 注入使用
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. 函数调用（工具）

```csharp
public class MyTools
{
    /// <summary>获取指定城市的天气</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("城市名")] String city)
        => $"{city} 今天晴，22°C";
}

// 注册工具
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// 挂入管道，自动多轮循环工具调用
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// 模型自动调用 get_weather("北京")，返回最终文本答案
var reply = await client.ChatAsync("北京今天天气怎么样？");
```

### 4. 约束推理路径（ReAct 模式）

当 AI 对复杂问题容易猜测或走弯路时，在 System Prompt 中使用 **ReAct 格式**强制逐步推理。
`ToolChatClient` 已内置 `while` 大循环（`MaxIterations = 10`），无需手写 Agent；
搭配 ReAct System Prompt 可进一步约束每步的推理方向，防止跳步猜测。

```csharp
// UseTools 管道装配：ToolChatClient 自动循环至模型不再发起工具调用为止
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// ReAct System Prompt：Thought → Action → Observation → Answer 强制逐步推理
var reply = await client.ChatAsync([
    ("system",
        "你必须按如下格式逐步推理，不允许跳过步骤或直接猜测：\n" +
        "Thought: <分析当前需要做什么>\n" +
        "Action: <调用哪个工具及参数>\n" +
        "Observation: <分析工具返回结果>\n" +
        "（重复 Thought/Action/Observation 直到信息充分）\n" +
        "Answer: <基于以上观测得出最终结论>\n\n" +
        "规则：禁止在未调用工具的情况下直接给出 Answer。"),
    ("user", "北京今天天气适合户外运动吗？"),
]);
```

### 5. 运行完整 Web 对话应用

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# 构建前端（需要 Node.js + pnpm）
cd Web && pnpm install && pnpm build && cd ..

# 启动
cd NewLife.ChatAI
dotnet run --framework net8.0
```

浏览器访问 `http://localhost:5000`，默认 SQLite，开箱即用。首次启动通过 `/Admin` 配置服务商 API Key。

也可将 `NewLife.ChatAI` 通过 NuGet 嵌入已有项目：

```bash
dotnet add package NewLife.ChatAI
```

```csharp
using NewLife.ChatAI;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddChatAI();

var app = builder.Build();
app.UseChatAI(redirectToChat: true);
app.Run();
```

---

## API 网关

NewLife.ChatAI 内置多协议 AI 网关，第三方系统无需改造即可接入。默认以**纯净转发**模式运行（鉴权 / 限流 / 配额 / 用量，不记录对话）；开启**网关领域模式**（EnableGatewayDomainMode）后走完整处理器链，享受记忆注入与技能增强并自动记录对话。

| 协议 | 路由 | 说明 |
|------|------|------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | 流式 / 非流式 / 函数调用 / 视觉 |
| OpenAI Responses | `POST /v1/responses` | o3 / gpt-5 推理模型 |
| Anthropic Messages | `POST /v1/messages` | Claude 系列 |
| Google Gemini | `POST /v1/gemini/...` | Gemini 系列 |
| Ollama | `POST /api/chat` `/api/tags` 等 | 伪装 Ollama 服务，兼容 Ollama 客户端 |
| 图像生成 | `POST /v1/images/generations` | Text-to-Image |
| 图像编辑 | `POST /v1/images/edits` | Inpainting（multipart/form-data）|
| 嵌入向量 | `POST /v1/embeddings` | OpenAI 兼容 |
| 语音 | `POST /v1/audio/speech` `/v1/audio/transcriptions` | TTS / ASR |
| 视频生成 | `POST /v1/video/generations` | 任务式提交与状态查询 |
| 重排序 | `POST /v1/reranks` | Rerank |
| 模型发现 | `GET /v1/models` | 可用模型列表 |

**认证**：`Authorization: Bearer sk-xxxx`（AppKey）

**特性**：上游 429 指数退避重试（随机抖动，最多 5 次）、Token 用量自动记录、按 AppKey + 用户双维度统计

---

## 扩展开发

### 新增 AI 服务商

继承 `OpenAIChatClient`，添加 `[AiClient]` 特性，`AiClientRegistry` 启动自动扫描注册：

```csharp
[AiClient("MyAI", "我的服务", "https://api.myai.com/v1",
    Description = "自定义 AI 服务")]
[AiClientModel("myai-latest", "MyAI Latest", Code = "MyAI",
    FunctionCalling = true, Vision = true)]
public class MyAiChatClient : OpenAIChatClient
{
    public MyAiChatClient() { }
    public MyAiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : base(apiKey, model, endpoint) { }
}
```

### 新增工具

```csharp
public class MyTools
{
    /// <summary>查询当前时间</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// DI 场景
services.AddSingleton<IToolProvider>(_ =>
{
    var r = new ToolRegistry();
    r.AddTools<MyTools>(new MyTools());
    return r;
});
```

### 新增 IChatHandler

在对话事前（OnBefore）或事后（OnAfter）注入自定义逻辑（如上下文注入、审计、记录）：

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"当前时间：{DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

// DI 注册，MessageFlow 自动按 ChatHandlerOrderAttribute 排序调用
services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

### 新增过滤器

洋葱圈模型，可在对话前后插入日志、审计、内容审核等逻辑：

```csharp
public class AuditFilter : IChatFilter
{
    public async Task OnChatAsync(
        ChatFilterContext ctx,
        Func<ChatFilterContext, CancellationToken, Task> next,
        CancellationToken ct)
    {
        // before：记录输入 / 敏感词过滤
        await next(ctx, ct);
        // after：记录输出 / 写审计日志
    }

    public Task OnStreamCompletedAsync(ChatFilterContext ctx, CancellationToken ct)
        => Task.CompletedTask;
}
```

---

## 文档

| 文档 | 说明 | 对应模块 |
|------|------|:--------:|
| [需求文档](Doc/需求文档.md) | 产品愿景、核心目标、功能需求（模块编码体系） | ALL |
| [功能清单](Doc/功能清单.md) | 完整功能清单，三维状态追踪 | ALL |
| [架构设计](Doc/架构设计.md) | 四层架构、各模块设计细节 | ALL |
| [协议客户端分析](Doc/协议客户端分析.md) | 协议客户端 chat/stream/think 健康度分析、缺口修复记录与测试覆盖 | AI |
| [基础库审计](Doc/基础库审计.md) | 基础库 12 维度全景审计：分 15+ 轮登记与修复问题（协议客户端/MCP/工具链/记忆等） | ALL |
| [AI 编排框架需求](Doc/AI编排框架需求.md) | 工具 / 智能体 / 规划器设计 | AI / AGENT |
| [API 网关需求](Doc/API网关需求.md) | 网关协议适配详解 | GW |
| [MCP 架构](Doc/MCP架构.md) | MCP 客户端与服务端设计 | TOOL |
| [技能管理需求](Doc/技能管理需求.md) | 技能系统详细设计 | TOOL |
| [自学习系统需求](Doc/自学习系统需求.md) | 对话分析 + 记忆提取 | EVO |
| [对话数据保存流程](Doc/对话数据保存流程.md) | MessageFlow 详细流程 | CHAT |
| [AOT 兼容性分析](Doc/AOT兼容性分析.md) | NativeAOT 支持边界、冒烟矩阵与平台/架构覆盖 | ALL |
| [多语言本地化](Doc/多语言本地化.md) | 31 种运行时语言、词表维护与贡献流程 | ALL |

---

## 生态版图

NewLife 生态按依赖分层，各项目独立版本、独立发版。下面的版图在所有 NewLife 仓库中逐字一致；每个项目链接到代码仓库与主 NuGet 包。

| 项目 | 一句话定位 | 下载量 |
| --- | --- | --- |
| **基础底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日志、配置、缓存、网络、序列化、定时调度与 APM 追踪。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 数据中间件：单表百亿级、自动分表、读写分离，支持 10 多种数据库。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能网络库：单机 177 万 pps、14.5 Gbps（TCP 1KB，2026-10 实测），400 万 Tcp 长连接。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 协议通信与 RPC 框架，面向 CS 应用与物联网设备。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 把 .NET 应用安装为操作系统守护进程：Windows 服务与 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 项目模板：控制台、Web、WebApi、服务、数据与物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **数据存储与消息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 客户端：微秒级延迟、百万级吞吐，内置消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 纯托管 RocketMQ 客户端，支持 Apache RocketMQ 与阿里云消息队列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 客户端与服务端，客户端支持阿里云物联网。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合数据库：嵌入/网络双模，关系型、时序、消息队列与 KV 于一体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql 驱动与数据访问辅助层，基于 NewLife 数据栈实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 与分布式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 魔方快速开发平台：用户权限、SSO、OAuth 服务端与 CRUD 脚手架。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 星尘分布式服务平台：节点管理、APM、配置中心、注册中心与发布中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 蚂蚁调度：分布式任务与实时数据平台，支持分片、重试与回溯补算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 统一接入 46 家大模型服务商，附带开箱即用的 ChatAI Web 对话应用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工业与物联网** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT 标准库，定义物联网通信协议契约与设备抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 西门子 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 欧姆龙 PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 三菱 Melsec PLC 协议，基于 NewLife.IoT 实现。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC 客户端，用于工业数据采集。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 楼宇自控协议栈。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 开源轻量级 IoT 平台，含网关与设备管理。 | — |
| **车联网** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 车辆定位协议：70+ 消息体，覆盖 2011/2013/2019 三个版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部标平台互联协议，面向运输管理部门。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 车载视频：RTP 解析、FLV/fMP4 转封装与直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **领域组件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一个包覆盖 Excel、Word、PPT、PDF 等 15+ 文档格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音频：编解码、设备 IO、DSP、容器与流式处理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 视频编解码、封装格式、帧处理与流媒体。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度/高德/腾讯/天地图统一接口，支持地理编码与坐标转换。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 归属地：离线内存映射库 + 在线 API 双源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具与产品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 码神工具：网络、串口、加解密、正则、Modbus 与 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自托管 Git 源代码管理系统。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 从低版本宿主启动时，自动安装匹配的 .NET 运行时。 | — |

> 徽章取各项目主包；主题包、扩展包与卫星包见对应 NuGet 页面。未在表中列出的 30 多个包（FTP、LoRa、Office 扩展、各类协议驱动等）见 [NewLifeX 组织](https://github.com/NewLifeX) 与 [newlifex.com](https://newlifex.com)。

## 新生命开发团队

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命团队成立于 2002 年，二十余年专注 .NET 与物联网技术生态，主导开源项目近百个（生态版图收录 34 个主干项目），为电力、物流、医疗、工控、互联网等行业提供软硬件一体化解决方案；以“学无先后达者为师”为信条。

开源组件在 NuGet 累计下载 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生态口径，截至 2026-10）。

网站：https://newlifex.com · 开源组织：https://github.com/NewLifeX

<details>
<summary>微信公众号：智能大石头（扫码关注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 许可证

[MIT License](LICENSE)

欢迎提交 Issue 与 Pull Request。

- GitHub：https://github.com/NewLifeX/NewLife.AI
- 官网：https://newlifex.com
- QQ 群：1600800

## 相关项目

- [NewLife.Core](https://github.com/NewLifeX/X) — .NET 基础库
- [XCode](https://github.com/NewLifeX/X) — ORM 数据框架
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — 魔方快速开发平台
