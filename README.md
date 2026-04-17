# NewLife.AI

<p align="center">
  <a href="https://www.nuget.org/packages/NewLife.AI"><img src="https://img.shields.io/nuget/v/NewLife.AI.svg" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/NewLife.AI"><img src="https://img.shields.io/nuget/dt/NewLife.AI.svg" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/.NET-netstandard2.1%20%7C%20net8.0%20%7C%20net10.0-blue" alt=".NET">
  <a href="https://github.com/NewLifeX/NewLife.AI/blob/main/LICENSE"><img src="https://img.shields.io/github/license/NewLifeX/NewLife.AI.svg" alt="License"></a>
</p>

## 项目介绍

**NewLife.AI** 是面向 .NET 生态的**开源 AI 基础库**，通过统一的 `IChatClient` 接口封装 **46 个主流大模型服务商**，内置函数调用、MCP 协议、流式输出、多模态、多智能体等能力，可作为 NuGet 包嵌入任意 .NET 项目（`netstandard2.1`）。

**NewLife.ChatAI** 是构建于 NewLife.AI 之上的**完整 Web 对话应用**（ASP.NET Core），提供即开即用的多模型对话前端、统一 AI 网关与自动记忆进化，既可独立部署，也可通过 NuGet 嵌入已有 ASP.NET Core 项目。

---

## 核心特性

- **46 家 AI 服务商，5 种协议**：OpenAI / Anthropic / Gemini / 通义 DashScope / Ollama + AWS Bedrock SigV4，一行代码任意切换
- **统一 `IChatClient` 接口**：对齐 MEAI 规范，单轮、流式、函数调用、多模态全部统一 API
- **函数调用（工具）**：`[ToolDescription]` 特性自动生成 JSON Schema，`ToolChatClient` 多轮循环，内置搜索 / 天气 / 翻译 / 网页抓取 / IP 定位等工具
- **MCP 双向支持**：客户端对接外部 MCP Server（stdio / HTTP SSE），服务端将本系统工具暴露为标准 MCP 服务
- **完整对话内核**：`MessageFlow` 五阶段模板（Validate → Prepare → Execute → Persist → PostProcess），可插拔 `IChatFilter` 与 `IContextEnricher`
- **用户记忆进化**：自动从对话中提取 10 类结构化记忆，越用越懂用户
- **统一 AI 网关**：兼容 OpenAI / Anthropic / Gemini 协议，AppKey 多租户，上游 429 指数退避重试
- **技能系统**：Markdown 提示词复用，`@` 递归引用，触发词自动激活
- **多智能体**：`ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner`
- **React 19 Web 前端**：SSE 流式 + 思考过程（Chain-of-Thought）+ 工具调用可视化 + Markdown + KaTeX + Mermaid + 多模态

---

## 支持的 AI 服务商

46 家服务商，**9 个独立协议客户端 + 37 个 OpenAI 兼容适配**。

### 独立协议实现（9 个）

| 服务商 | 协议 | 特性 |
|--------|------|------|
| OpenAI | ChatCompletions / Responses | 视觉 / 函数调用 / 图像生成 / o3 推理 |
| DeepSeek | OpenAI 兼容 | deepseek-chat / deepseek-reasoner |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| 阿里云 | DashScope | qwen-plus / qwen-max / qwen-vl |
| Azure AI | Azure OpenAI | 部署名称 URL + api-key |
| Ollama | Ollama API | 本地 llama / deepseek / qwen |
| AWS Bedrock | SigV4 签名 | Claude / Llama / Titan / Mistral |
| NewLifeAI | 级联代理 | 聚合多服务商 |

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

### 4. 运行完整 Web 对话应用

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

NewLife.ChatAI 内置多协议 AI 网关，第三方系统无需改造即可接入，全路径经过记忆注入与技能增强。

| 协议 | 路由 | 说明 |
|------|------|------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | 流式 / 非流式 / 函数调用 / 视觉 |
| OpenAI Responses | `POST /v1/responses` | o3 / gpt-5 推理模型 |
| Anthropic Messages | `POST /v1/messages` | Claude 系列 |
| Google Gemini | `POST /v1/gemini/...` | Gemini 系列 |
| 图像生成 | `POST /v1/images/generations` | Text-to-Image |
| 图像编辑 | `POST /v1/images/edits` | Inpainting（multipart/form-data）|
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

### 新增上下文增强器

在每次对话 Prepare 阶段自动注入内容（如实时数据、用户状态）：

```csharp
public class CurrentTimeEnricher : IContextEnricher
{
    public Int32 Order => 150;   // 控制与其他 Enricher 的执行顺序

    public Task EnrichAsync(MessageFlowContext ctx, CancellationToken ct)
    {
        ctx.Messages.Insert(0,
            new ChatMessage("system", $"当前时间：{DateTime.Now:yyyy-MM-dd HH:mm}"));
        return Task.CompletedTask;
    }
}

// DI 注册后自动按 Order 升序调用
services.AddSingleton<IContextEnricher, CurrentTimeEnricher>();
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

| 文档 | 说明 |
|------|------|
| [需求规格说明](Doc/需求规格说明.md) | 产品目标、功能清单、非功能需求 |
| [架构设计](Doc/架构设计.md) | 四层架构、各模块设计细节 |
| [AI 编排框架需求](Doc/AI编排框架需求.md) | 工具 / 智能体 / 规划器设计 |
| [API 网关需求](Doc/API网关需求.md) | 网关协议适配详解 |
| [MCP 架构](Doc/MCP架构.md) | MCP 客户端与服务端设计 |
| [技能管理需求](Doc/技能管理需求.md) | 技能系统详细设计 |
| [自学习系统需求](Doc/自学习系统需求.md) | 对话分析 + 记忆提取 |
| [对话数据保存流程](Doc/对话数据保存流程.md) | MessageFlow 详细流程 |
| [功能模块清单](Doc/功能模块清单.md) | 完整功能清单 |

---

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
