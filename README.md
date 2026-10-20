# NewLife.AI

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.AI?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.AI?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.AI?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.AI?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/test.yml)
[![NativeAOT](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.AI/actions/workflows/aot.yml)
<!-- metrics:start -->
[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-38%2F38-brightgreen?logo=dotnet)](Doc/AOT兼容性分析.md)
[![TFM](https://img.shields.io/badge/TFM-5%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.AI)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
<!-- metrics:end -->
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20x86%20%7C%20arm64%20%7C%20arm%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.AI?logo=github)](https://github.com/NewLifeX/NewLife.AI/graphs/contributors)

<!-- lang:start -->
🌐 [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->


## Introduction

**NewLife.AI** is an open-source AI foundation library for the .NET ecosystem, providing a unified `IChatClient` interface that wraps **46 mainstream LLM providers**. It features built-in function calling, MCP protocol, streaming output, multimodal support, multi-agent capabilities, and can be embedded as a NuGet package in any .NET project (`net45 → net10.0`, NativeAOT ready).

The library ships **31 runtime languages** (English source with automatic fallback) and targets Windows, Linux, macOS, Android and iOS across x64 / x86 / arm64 / arm / loongarch64 / riscv64 / s390x.

**NewLife.ChatAI** is a complete web chat application built on NewLife.AI (ASP.NET Core), offering an out-of-the-box multi-model chat frontend, a unified AI gateway, and automatic memory evolution. It can be deployed standalone or embedded into existing ASP.NET Core projects via NuGet.

---

## Core Features

- **NativeAOT Ready**: `IsAotCompatible` on net8.0/net10.0, 38-scenario NativeAOT smoke matrix across Windows / Linux / macOS and x64 / arm64 / loongarch64
- **31 Runtime Languages**: English source messages with compiled locale tables; missing translations fall back to English, no code changes needed for new languages
- **46 AI Providers, 6 Protocols**: OpenAI / Anthropic / Gemini / Tongyi DashScope / Ollama / AWS Bedrock — switch with a single line of code
- **Unified `IChatClient` Interface**: Aligned with MEAI specification — single-turn, streaming, function calling, multimodal all via a single API
- **Function Calling (Tools)**: `[ToolDescription]` attribute auto-generates JSON Schema; `ToolChatClient` multi-turn loop; built-in search / weather / translation / web scraping / IP geolocation tools
- **Bidirectional MCP Support**: Client connects to external MCP Servers (stdio / HTTP SSE); Server exposes local tools as standard MCP services
- **Complete Chat Kernel**: `IChatHandler` three-stage pipeline (OnBefore → Execute → OnAfter), built-in handlers (skill activation / memory injection / persistence / usage tracking / title generation), pluggable `IChatFilter`
- **User Memory Evolution**: Automatically extracts 10 categories of structured memories from conversations — the more you chat, the better it understands you
- **Unified AI Gateway**: Compatible with OpenAI / Anthropic / Gemini protocols; snake_case/camelCase auto-adaptation; AppKey multi-tenancy; upstream 429 exponential backoff retry
- **Skill System**: Markdown prompt reuse, `@` recursive references, trigger-word auto-activation
- **Multi-Agent**: `ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **React 19 Web Frontend**: SSE streaming + chat presets + Artifact real-time preview (HTML/SVG/Mermaid) + conversation branching + tool call visualization + reasoning timing + multimodal
- **Knowledge Evolution Layer**: Automatically distills knowledge from conversations, builds a searchable knowledge base with TOC browsing and vector semantic search
- **TTS Speech Synthesis**: DashScope TTS and CosyVoice V3.5 support; streaming speech synthesis; dedicated frontend TTS API
- **Embedding / Vector Retrieval**: Built-in HashTextEmbedder v2 and vector storage; knowledge base document vectorization and semantic search
- **Multi-Agent Enhancement**: New ReflectionAgent and ReviewAgent; complex task decomposition with parallel sub-agent aggregation
- **Human-in-the-Loop Checkpoints**: Real-time human selection among AI multi-path options; multi-question group decision checkpoints
- **Tool Call Enhancement**: ToolCallContext context propagation; tool Provider circuit breaker; session-level tool visibility filtering; structured error returns with three-tier permission system
- **Memory Evolution**: Memory consolidation service with dedup/merge/expiry; learning memory auto-extracts structured knowledge from conversations

---

## Supported AI Providers

46 providers: **9 independent protocol clients + 37 OpenAI-compatible adapters**.

### Independent Protocol Implementations (9)

| Provider | Protocol | Features |
|----------|----------|----------|
| OpenAI | ChatCompletions / Responses | Vision / Function Calling / Image Generation / o3 Reasoning |
| DeepSeek | DeepSeek API | reasoning_content / DeepSeek v4 exclusive parameters |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| Alibaba Cloud | DashScope | qwen-plus / qwen-max / Omni multimodal / Files API |
| Azure AI | Azure OpenAI | Deployment name URL + api-key |
| Ollama | Ollama API | Local llama / deepseek / qwen |
| AWS Bedrock | SigV4 Signature | Claude / Llama / Titan / Mistral |
| NewLifeAI | Cascade Proxy | Multi-provider aggregation |
| (Others) | OpenAI Compatible | 37 compatible platforms |

### OpenAI-Compatible Family (37)

Doubao (Volcano Engine), Zhipu Qingyan (GLM), Wenxin Yiyan, Moonshot (Kimi), MiniMax, StepFun, Baichuan, iFlytek Spark, 01.AI, Mistral, Perplexity, Cohere, Together AI, Fireworks, OpenRouter, SiliconCloud, DeepInfra, Groq, Cerebras, Hyperbolic, Nebius, Novita, Lepton, 302.AI, xAI (Grok)… and other OpenAI-compatible platforms.

All providers are declared via the `[AiClient]` attribute; `AiClientRegistry` auto-scans and registers at startup — zero configuration for new providers.

---

## Quick Start

### 1. AI SDK Only

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// Single-turn chat
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("Explain large language models in three sentences");
Console.WriteLine(reply);

// Multi-role messages (tuple array, no manual ChatMessage construction)
var reply2 = await client.ChatAsync([
    ("system", "You are a professional C# development assistant"),
    ("user", "Explain the difference between ValueTask and Task"),
]);

// Streaming output
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "Write a short poem about code" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. ASP.NET Core Dependency Injection

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// Register service
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Keyed multi-provider coexistence
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// Inject and use
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. Function Calling (Tools)

```csharp
public class MyTools
{
    /// <summary>Get weather for a specified city</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("City name")] String city)
        => $"{city}: Sunny, 22°C today";
}

// Register tools
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// Add to pipeline — automatic multi-turn tool calling loop
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// Model auto-calls get_weather("Beijing"), returns final text answer
var reply = await client.ChatAsync("What's the weather like in Beijing today?");
```

### 4. Constrained Reasoning (ReAct Pattern)

When the AI tends to guess or take shortcuts on complex problems, use the **ReAct format** in the System Prompt to enforce step-by-step reasoning. `ToolChatClient` has a built-in `while` loop (`MaxIterations = 10`) — no need to hand-write an Agent. Combine with a ReAct System Prompt to further constrain the reasoning direction at each step.

```csharp
// UseTools pipeline: ToolChatClient loops automatically until the model stops calling tools
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// ReAct System Prompt: Thought → Action → Observation → Answer enforces step-by-step reasoning
var reply = await client.ChatAsync([
    ("system",
        "You must reason step by step in the following format. Do not skip steps or guess directly:\n" +
        "Thought: <analyze what needs to be done>\n" +
        "Action: <which tool to call and parameters>\n" +
        "Observation: <analyze the tool result>\n" +
        "(Repeat Thought/Action/Observation until sufficient information is gathered)\n" +
        "Answer: <final conclusion based on the above observations>\n\n" +
        "Rule: Do not give an Answer without first calling a tool."),
    ("user", "Is the weather in Beijing suitable for outdoor sports today?"),
]);
```

### 5. Run the Full Web Chat Application

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# Build frontend (requires Node.js + pnpm)
cd Web && pnpm install && pnpm build && cd ..

# Start
cd NewLife.ChatAI
dotnet run --framework net8.0
```

Open `http://localhost:5000` in your browser — SQLite by default, ready out of the box. On first launch, configure provider API keys via `/Admin`.

You can also embed `NewLife.ChatAI` into an existing project via NuGet:

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

## API Gateway

NewLife.ChatAI includes a built-in multi-protocol AI gateway. Third-party systems can integrate without modification — all paths go through memory injection and skill enhancement.

| Protocol | Route | Description |
|----------|-------|-------------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | Streaming / non-streaming / function calling / vision |
| OpenAI Responses | `POST /v1/responses` | o3 / gpt-5 reasoning models |
| Anthropic Messages | `POST /v1/messages` | Claude series |
| Google Gemini | `POST /v1/gemini/...` | Gemini series |
| Image Generation | `POST /v1/images/generations` | Text-to-Image |
| Image Edit | `POST /v1/images/edits` | Inpainting (multipart/form-data) |
| Model Discovery | `GET /v1/models` | Available model list |

**Authentication**: `Authorization: Bearer sk-xxxx` (AppKey)

**Features**: Upstream 429 exponential backoff retry (random jitter, up to 5 retries); automatic Token usage recording; dual-dimension statistics by AppKey + User

---

## Extension Development

### Adding a New AI Provider

Inherit from `OpenAIChatClient`, add the `[AiClient]` attribute — `AiClientRegistry` auto-scans at startup:

```csharp
[AiClient("MyAI", "My Service", "https://api.myai.com/v1",
    Description = "Custom AI Service")]
[AiClientModel("myai-latest", "MyAI Latest", Code = "MyAI",
    FunctionCalling = true, Vision = true)]
public class MyAiChatClient : OpenAIChatClient
{
    public MyAiChatClient() { }
    public MyAiChatClient(String apiKey, String? model = null, String? endpoint = null)
        : base(apiKey, model, endpoint) { }
}
```

### Adding a New Tool

```csharp
public class MyTools
{
    /// <summary>Query current time</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// DI scenario
services.AddSingleton<IToolProvider>(_ =>
{
    var r = new ToolRegistry();
    r.AddTools<MyTools>(new MyTools());
    return r;
});
```

### Adding a New IChatHandler

Inject custom logic before (OnBefore) or after (OnAfter) chat — e.g., context injection, auditing, logging:

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"Current time: {DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

// DI registration — MessageFlow auto-sorts by ChatHandlerOrderAttribute
services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

### Adding a New Filter

Onion-ring model for injecting logging, auditing, content moderation before/after chat:

```csharp
public class AuditFilter : IChatFilter
{
    public async Task OnChatAsync(
        ChatFilterContext ctx,
        Func<ChatFilterContext, CancellationToken, Task> next,
        CancellationToken ct)
    {
        // before: log input / filter sensitive content
        await next(ctx, ct);
        // after: log output / write audit log
    }

    public Task OnStreamCompletedAsync(ChatFilterContext ctx, CancellationToken ct)
        => Task.CompletedTask;
}
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [Requirements Specification](Doc/需求规格说明.md) | Product goals, feature list, non-functional requirements |
| [Architecture Design](Doc/架构设计.md) | Four-layer architecture, module design details |
| [AI Orchestration Framework](Doc/AI编排框架需求.md) | Tools / Agents / Planner design |
| [API Gateway Requirements](Doc/API网关需求.md) | Gateway protocol adaptation details |
| [MCP Architecture](Doc/MCP架构.md) | MCP client and server design |
| [Skill Management](Doc/技能管理需求.md) | Skill system detailed design |
| [Self-Learning System](Doc/自学习系统需求.md) | Conversation analysis + memory extraction |
| [Chat Data Persistence Flow](Doc/对话数据保存流程.md) | MessageFlow detailed process |
| [AOT Compatibility](Doc/AOT兼容性分析.md) | NativeAOT support boundary, smoke matrix and platform/architecture coverage |
| [Runtime Localization](Doc/多语言本地化.md) | 31 runtime languages, locale tables and contribution workflow |
| [Feature Module List](Doc/功能清单.md) | Complete feature inventory |

---

## Ecosystem Map

NewLife is a layered ecosystem of independently versioned open-source projects. The map below is shared verbatim by every NewLife repository; each project links to its repository and its primary NuGet package.

| Project | What it does | Downloads |
| --- | --- | --- |
| **Foundation** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | Logging, configuration, caching, networking, serialization, scheduling and APM tracing. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | Data middleware: single tables at the tens-of-billions scale, automatic sharding, read-write splitting, 10+ databases. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | High-performance network library: 1.77M pps / 14.5 Gbps on a single machine (TCP 1KB, 2026-10), 4M long-lived TCP connections. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | Protocol communication and RPC framework for client/server applications and IoT devices. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | Installs .NET applications as OS daemons: Windows services and Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` project templates for console, web, API, service, data and IoT projects. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **Data & Messaging** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis client with microsecond latency, millions of ops/s and built-in message queues. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Pure managed RocketMQ client for Apache RocketMQ and Alibaba Cloud Message Queue. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT client and server, including Alibaba Cloud IoT connectivity. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | Hybrid database in C#: embedded or networked, relational, time-series, message queue and KV in one. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql driver and helper layer built on the NewLife data stack. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web, AI & Distributed** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | Rapid admin-platform framework: permissions, SSO, OAuth server and CRUD scaffolding. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | Distributed service platform: node management, APM, configuration center, registry and release center. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | Distributed task and real-time data scheduling platform with sharding, retries and backfill. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | Unified client for 46 LLM providers plus ChatAI, a ready-to-run web chat application. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **Industrial & IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT standard library defining protocol contracts and device abstractions. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII implementation on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | Siemens PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | Omron PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | Mitsubishi Melsec PLC protocol on top of NewLife.IoT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC client for industrial data access. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet building-automation protocol stack. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | Lightweight open-source IoT platform with gateway and device management. | — |
| **Vehicle Telematics** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 vehicle positioning protocol: 70+ message bodies, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 platform-to-platform protocol for transport authorities. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 in-vehicle video: RTP parsing, FLV/fMP4 remuxing and live streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **Domain Components** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | One package for Excel, Word, PPT, PDF and 15+ document formats. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | Cross-platform audio: codecs, device I/O, DSP, containers and streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | Video codecs, container formats, frame processing and streaming. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | Unified interface over Baidu, AMap, Tencent and Tianditu with geocoding and coordinate conversion. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 geolocation with an offline memory-mapped database and an online API fallback. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **Tools & Products** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | Developer toolbox: network, serial port, crypto, regex, Modbus and MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | Self-hosted Git source code management system. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | Installs the matching .NET runtime automatically when launched from an older host. | — |

> Badges show the primary package of each project; theme, extension and satellite packages are listed on their NuGet pages.The 30+ packages not listed here (FTP, LoRa, Office extensions, protocol drivers and more) are available under the [NewLifeX organization](https://github.com/NewLifeX) and on [newlifex.com](https://newlifex.com).

## Team

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

Founded in 2002, the NewLife team has spent more than two decades on the .NET and IoT ecosystem, maintains nearly 100 open-source projects (34 of them core to this map), and delivers integrated software and hardware solutions for power, logistics, healthcare, industrial control and internet industries. We live by the motto "learning has no seniority — mastery earns respect".

Open-source components have been downloaded <!--nuget-total:start-->6.48M<!--nuget-total:end--> times from NuGet (ecosystem-wide, as of 2026-10).

Website: https://newlifex.com · Open source organization: https://github.com/NewLifeX

<details>
<summary>WeChat Official Account: 智能大石头 (scan to follow)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## License

[MIT License](LICENSE)

Issues and Pull Requests are welcome.

- GitHub: https://github.com/NewLifeX/NewLife.AI
- Website: https://newlifex.com
- QQ Group: 1600800

## Related Projects

- [NewLife.Core](https://github.com/NewLifeX/X) — .NET Foundation Library
- [XCode](https://github.com/NewLifeX/X) — ORM Data Framework
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — Rapid Development Platform
