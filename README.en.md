# NewLife.AI

<p align="center">
  <a href="https://www.nuget.org/packages/NewLife.AI"><img src="https://img.shields.io/nuget/v/NewLife.AI.svg" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/NewLife.AI"><img src="https://img.shields.io/nuget/dt/NewLife.AI.svg" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/.NET-netstandard2.1%20%7C%20net8.0%20%7C%20net10.0-blue" alt=".NET">
  <a href="https://github.com/NewLifeX/NewLife.AI/blob/main/LICENSE"><img src="https://img.shields.io/github/license/NewLifeX/NewLife.AI.svg" alt="License"></a>
</p>

<p align="center">
  <b>English</b> |
  <a href="README.md">简体中文</a> |
  <a href="README.ja.md">日本語</a> |
  <a href="README.ko.md">한국어</a> |
  <a href="README.es.md">Español</a> |
  <a href="README.fr.md">Français</a> |
  <a href="README.de.md">Deutsch</a> |
  <a href="README.ru.md">Русский</a> |
  <a href="README.pt.md">Português</a>
</p>

## Introduction

**NewLife.AI** is an open-source AI foundation library for the .NET ecosystem, providing a unified `IChatClient` interface that wraps **46 mainstream LLM providers**. It features built-in function calling, MCP protocol, streaming output, multimodal support, multi-agent capabilities, and can be embedded as a NuGet package in any .NET project (`net45 / netstandard2.1`).

**NewLife.ChatAI** is a complete web chat application built on NewLife.AI (ASP.NET Core), offering an out-of-the-box multi-model chat frontend, a unified AI gateway, and automatic memory evolution. It can be deployed standalone or embedded into existing ASP.NET Core projects via NuGet.

---

## Core Features

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
| [Feature Module List](Doc/功能模块清单.md) | Complete feature inventory |

---

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
