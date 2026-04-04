# NewLife.AI

[![NuGet](https://img.shields.io/nuget/v/NewLife.AI.svg?style=flat-square)](https://www.nuget.org/packages/NewLife.AI/)
[![NuGet Download](https://img.shields.io/nuget/dt/NewLife.AI.svg?style=flat-square)](https://www.nuget.org/packages/NewLife.AI/)
[![.NET](https://img.shields.io/badge/.NET-Standard2.1%2Bnet8%2Bnet10-purple?style=flat-square)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

**NewLife.AI** 是统一 AI 网关基础库，支持 48 个主流 AI 服务商的接入与编排。
**NewLife.ChatAI** 是基于 NewLife.AI 构建的完整 AI 对话应用，提供对话 Web 前端、API 网关路由、使用量统计等全套业务能力。

---

## 核心特性

- **48 个服务商支持**：OpenAI、Azure OpenAI、AWS Bedrock、Anthropic（Claude）、Google Gemini、阿里百炼（通义千问）、DeepSeek、Kimi、豆包、智谱、百度、讯飞、腾讯混元、Ollama（本地）等
- **统一接口 `IChatClient`**：屏蔽协议差异，流式与非流式双模式，对标 Microsoft MEAI 规范
- **`AiClientRegistry` 自动注册**：通过 `[AiClient]` 特性标注，反射扫描自动发现所有服务商
- **`ChatClientBuilder` 中间件管道**：`UseFilters()` + `UseTools()` 链式组装，灵活扩展
- **原生函数调用（Function Calling）**：`[ToolDescription]` 特性 + `ToolSchemaBuilder` 自动生成 JSON Schema，`ToolChatClient` 多轮调用循环
- **MCP 协议支持**：`HttpMcpServer` 工具调用，`NewLife.AI.Extensions` 快速将 ASP.NET 应用扩展为 MCP Server
- **思考模式（Thinking Mode）**：Auto / Think / Fast 三档，支持交错思考（think–tool–think）
- **多模态**：图像理解（Vision）、图像生成、图像编辑（Inpainting）
- **Planner 规划器**：`FunctionCallingPlanner` 将目标拆解为工具调用步骤并执行
- **MultiAgent 框架**：`GroupChat` 多 Agent 轮询、`ParallelGroupChat` 并行协作、`AgentAsTool` 嵌套
- **语义记忆抽象**：`ISemanticMemory` + `IVectorStore` 接口，内存版实现开箱即用

---

## 支持的协议与服务商

### 通信协议（6 种）

| 协议 | 客户端类 | 适用服务商 | 认证方式 |
|------|---------|---------|---------|
| **OpenAI** | `OpenAiChatClient` | ~44 个 OpenAI 兼容服务商 | `Authorization: Bearer` |
| **Azure OpenAI** | `AzureAIChatClient` | Azure OpenAI（部署名称 URL） | `api-key` 请求头 |
| **Anthropic** | `AnthropicChatClient` | Anthropic Claude 系列 | `x-api-key` 请求头 |
| **Google Gemini** | `GeminiChatClient` | Google Gemini 系列 | `?key=` 查询参数 |
| **AWS Bedrock** | `BedrockChatClient` | AWS Bedrock Converse API | AWS SigV4 签名 |
| **DashScope** | `DashScopeChatClient` | 阿里百炼（兼容 OpenAI 路由） | `Authorization: Bearer` |
| **Ollama** | `OllamaChatClient` | 本地 Ollama 服务 | 无需认证 |

---

### 国内服务商（18 个）

| 编码 | 服务商 | 代表模型 | 特色 |
|------|-------|---------|------|
| `DeepSeek` | 深度求索 ★ | DeepSeek-V3 / R1 | 推理能力强，开源友好 |
| `DashScope` | 阿里百炼 / 通义千问 | Qwen-Max / Qwen-Plus | 阿里云生态，模型最全 |
| `VolcEngine` | 字节火山方舟（企业版） | 豆包 1.5 Pro | 字节跳动企业端入口 |
| `Doubao` | 抖音豆包（消费端） | — | 字节跳动消费端入口 |
| `Zhipu` | 智谱 AI | GLM-4 / CogView-3 | 代码 + 图像生成 |
| `Moonshot` | 月之暗面 Kimi | Kimi-K1.5 / 128K | 超长上下文，网页阅读 |
| `Hunyuan` | 腾讯混元 | 混元 T1 | 腾讯云生态 |
| `Qianfan` | 百度文心千帆 | ERNIE 4.5 Turbo | 百度搜索生态 |
| `Spark` | 讯飞星火 | 星火 4.0 Ultra | 语音 + 教育场景 |
| `Stepfun` | 阶跃星辰 | Step-2 | 强推理，多模态 |
| `Baichuan` | 百川智能 | 百川4 Turbo | 医疗 / 法律专项 |
| `SenseNova` | 商汤日日新 | SenseNova 系列 | 商汤视觉 AI 融合 |
| `MiniMax` | MiniMax | MiniMax-Text-01 | 超长上下文 |
| `SiliconFlow` | 硅基流动 | Qwen / DeepSeek 托管 | 国内开源模型推理平台 |
| `Yi` | 零一万物 | Yi-Large | 李开复旗下 |
| `MiMo` | 小米 MiMo | MiMo 系列 | 小米推理模型 |
| `Infini` | 无问芯穹 | 开源模型托管 | 国产 AI 算力平台 |
| `XiaomaPower` | 小马算力 | 开源模型托管 | GPU 算力平台 |

---

### 国际主流服务商（7 个）

| 编码 | 服务商 | 代表模型 | 特色 |
|------|-------|---------|------|
| `OpenAI` | OpenAI ★ | GPT-4o / o3 / o4-mini | 行业标准，函数调用最成熟 |
| `AzureAI` | Azure OpenAI | GPT-4o 部署 | 企业合规，私有部署 |
| `Anthropic` | Anthropic Claude ★ | Claude 3.7 Sonnet | 长文本推理，安全对齐 |
| `Gemini` | Google Gemini | Gemini 2.0 Flash | 多模态，代码执行 |
| `Bedrock` | AWS Bedrock | Claude / Llama / Nova | 云原生，SigV4 认证 |
| `XAI` | xAI Grok | Grok-3 / Grok-3 Mini | 马斯克旗下，实时信息 |
| `Mistral` | Mistral AI | Mistral Large | 欧盟合规，高效模型 |

---

### 国际聚合 / 新兴平台（16 个）

| 编码 | 服务商 | 特色 |
|------|-------|------|
| `OpenRouter` ★ | OpenRouter | 统一路由 300+ 模型，价格对比 |
| `GitHubModels` | GitHub Models | GitHub 内置模型市场 |
| `HuggingFace` | Hugging Face | 数千种开源模型推理路由 |
| `NvidiaNIM` | Nvidia NIM | GPU 专用推理微服务，Llama / Nemotron |
| `Groq` | Groq | 极速推理（LPU 芯片），Llama/Gemma |
| `Cerebras` | Cerebras | 晶圆级芯片，超高吞吐 |
| `Perplexity` | Perplexity | 搜索增强推理 |
| `Cohere` | Cohere | 企业 RAG 优化，Command 系列 |
| `TogetherAI` | Together AI | 开源模型聚合平台 |
| `Fireworks` | Fireworks AI | 生产级开源模型托管 |
| `SambaNova` | SambaNova | RDU 架构，Llama 系列超速推理 |
| `DeepInfra` | DeepInfra | 经济型开源模型推理 |
| `Hyperbolic` | Hyperbolic | 去中心化 GPU 推理 |
| `NovitaAI` | Novita AI | 图像 + 文本多模型平台 |
| `AI21` | AI21 Labs | Jamba（SSM+Transformer 混合架构）|
| `CloudflareAI` | Cloudflare AI | Workers AI，边缘推理 |

---

### 本地 / 私有部署（4 个）

| 编码 | 工具 | 特色 |
|------|-----|------|
| `Ollama` ★ | Ollama | 一键本地运行 Llama/Qwen/Gemma |
| `LMStudio` | LM Studio | 桌面端 GUI 管理本地模型 |
| `vLLM` | vLLM | 高吞吐量生产级推理引擎 |
| `OneAPI` | OneAPI | 开源 LLM API 管理与分发 |

> ★ 为各类别内最常用服务商。完整服务商列表见 [BuiltinChatClient.cs](NewLife.AI/Clients/BuiltinChatClient.cs)。

---

## 快速开始

### 安装 NuGet 包

```bash
# 核心基础库
dotnet add package NewLife.AI

# ASP.NET Core DI 扩展（可选）
dotnet add package NewLife.AI.Extensions
```

### IChatClient 四种使用模式

#### 模式一：直接 `new`（命令行 / 脚本 / 单文件，最简）

```csharp
// 所有 6 个客户端均提供 (apiKey, model?, endpoint?) 便捷构造
var client = new DashScopeChatClient("your-api-key", "qwen-plus");

// 可选：替换底层 HttpClient（设置代理、超时等）
client.HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

// 发送单条消息
var reply = await client.ChatAsync("你好，请介绍一下你自己");
Console.WriteLine(reply);

// 多角色消息（元组数组，无需构造 ChatMessage）
var reply2 = await client.ChatAsync([
    ("system", "你是一名专业的 C# 开发助手"),
    ("user", "请解释什么是依赖注入"),
]);
```

可用的便捷构造：

| 客户端 | 说明 |
|--------|------|
| `new OpenAIChatClient(apiKey, model?, endpoint?)` | OpenAI 及全部兼容服务商（~30 个） |
| `new DashScopeChatClient(apiKey, model?, endpoint?)` | 阿里百炼 |
| `new AnthropicChatClient(apiKey, model?, endpoint?)` | Claude |
| `new GeminiChatClient(apiKey, model?, endpoint?)` | Google Gemini |
| `new AzureAIChatClient(apiKey, model?, endpoint?)` | Azure OpenAI（部署名称作为 model） |
| `new BedrockChatClient(accessKeyId, secretAccessKey, model?, region?)` | AWS Bedrock（SigV4 签名认证） |
| `new OllamaChatClient(apiKey?, model?, endpoint?)` | Ollama（本地部署 apiKey 可为 null）|
| `new NewLifeAiChatClient(apiKey, model?, endpoint?)` | 级联 NewLife.AI 实例 |

#### 模式二：AiClientRegistry（配置驱动 / 动态切换服务商）

```csharp
// 通过服务商编码 + 选项创建（适合从数据库/配置文件读取参数）
var client = AiClientRegistry.Default.CreateClient("DashScope", "your-api-key", "qwen-plus");

// 从 AiClientOptions（含 Code 属性）批量创建
var opts = new AiClientOptions { Code = "OpenAI", ApiKey = "sk-xxx", Model = "gpt-4o" };
var client2 = AiClientRegistry.Default.CreateClient(opts.Code!, opts);
```

#### 模式三：ChatClientBuilder（中间件管道）

```csharp
// MEAI 风格：先设置服务商，再链式叠加中间件
var client = new ChatClientBuilder()
    .UseDashScope("your-api-key", "qwen-plus")   // UseOpenAI / UseAnthropic / UseGemini / UseOllama / UseNewLifeAI
    .UseFilters(new LoggingFilter())               // 日志 / 审计 / 速率限制
    .UseTools(toolRegistry)                        // 自动多轮 Function Calling
    .Build();
```

#### 模式四：DI 注入（ASP.NET Core，需 `NewLife.AI.Extensions`）

```csharp
// 单服务商 —— 专属便捷注册（推荐）
services.AddDashScope("your-api-key", "qwen-plus");
// 注入：IChatClient

// 多服务商（.NET 8+，Keyed Services）
services.AddKeyedDashScope("fast",   "sk-xxx", "qwen3.5-flash");
services.AddKeyedOpenAI  ("strong", "sk-xxx", "gpt-4o");
// 注入：[FromKeyedServices("fast")] IChatClient fastClient
```

全部专属 DI 方法：`AddOpenAI` / `AddDashScope` / `AddAnthropic` / `AddGemini` / `AddAzureAI` / `AddBedrock` / `AddOllama` / `AddNewLifeAI`，均有对应 `AddKeyed*` 变体（.NET 8+）。

#### 多模态流式输出（视觉理解）

```csharp
var message = new ChatMessage
{
    Role = "user",
    Contents = [
        new ImageContent { Uri = "https://example.com/image.jpg" },
        new TextContent("请描述这张图片的内容"),
    ]
};
await foreach (var chunk in client.GetStreamingResponseAsync([message]))
{
    var delta = chunk.Choices?.FirstOrDefault()?.Delta;
    if (delta?.Content is String text && !String.IsNullOrEmpty(text))
        Console.Write(text);
}
```

#### 自定义工具（原生 .NET 方法）

```csharp
public class WeatherService : IToolProvider
{
    [ToolDescription("获取城市实时天气")]
    public async Task<String> GetWeatherAsync(
        [Description("城市名称")] String city)
    {
        return $"{city} 今天晴，25°C";
    }
}

services.AddSingleton<IToolProvider, WeatherService>();
```

#### MultiAgent 协作

```csharp
var researcher = new ConversableAgent("researcher", researchClient)
    .AddTools(searchTool);

var writer = new ConversableAgent("writer", writingClient);

var groupChat = new GroupChat([researcher, writer], orchestratorClient);
var result = await groupChat.RunAsync("分析人工智能行业趋势并写一篇报告");
```

---

## 项目结构

```text
NewLife.AI.sln
├── NewLife.AI/               # 核心基础库（netstandard2.1）
│   ├── Clients/              # IChatClient 实现（OpenAI/Anthropic/Gemini 等）
│   ├── Providers/            # AiClientRegistry + ChatClientBuilder
│   ├── Filters/              # IChatFilter 过滤器体系
│   ├── Tools/                # 工具注册 + 内置工具（搜索/天气/翻译）
│   ├── Agents/               # MultiAgent 框架
│   ├── Planner/              # FunctionCallingPlanner
│   ├── Memory/               # ISemanticMemory + IVectorStore
│   └── ModelContextProtocol/ # MCP 协议实现
│
├── NewLife.AI.Extensions/    # ASP.NET Core 扩展（net6/8/10）
│   └── AspNetMcpServer.cs   # 将 ASP.NET 应用扩展为 MCP Server
│
├── NewLife.ChatAI/           # 完整 Web 应用（net8/10）
│   ├── Controllers/          # 16 个 API 控制器
│   ├── Services/             # 业务服务层
│   ├── Entity/               # XCode 实体类（8 张表）
│   └── wwwroot/              # React 前端（内嵌到 DLL）
│
└── Web/                      # 前端源码（React 19 + TypeScript + Vite）
```

---

## NewLife.ChatAI 完整应用

### 主要功能

| 功能 | 说明 |
|------|------|
| 多轮对话 | SSE 流式输出，思考过程可视化，思考/快速/自动三档 |
| 技能系统 | 内置与用户自定义技能，@ 输入补全选择，提示词自动注入上下文 |
| 用户记忆 | 自动从对话提取记忆并注入上下文，支持手动管理记忆条目 |
| 推荐问题 | 欢迎页展示引导性推荐问题，辅助用户快速上手 |
| 工具调用 | Function Calling + MCP 工具，前端折叠展示 ToolCallBlock |
| 图像多模态 | 图像上传理解（拖拽/粘贴）、文生图、图像编辑（Inpainting） |
| API 网关 | 兼容 OpenAI / Anthropic / Gemini 标准协议，AppKey 认证 |
| 会话管理 | 置顶、分组、关键词全文搜索、分享链接 |
| 使用量统计 | 按用户 / AppKey 维度的 Token 消耗统计 |
| 管理后台 | 基于 NewLife.Cube，提供模型配置、服务商管理、用户管理 |

### 部署方式

#### 独立可执行文件（推荐）

```bash
# 从发布包启动
dotnet NewLife.ChatAI.dll

# 或直接运行
./NewLife.ChatAI
```

应用启动后访问 `http://localhost:5000`。首次启动通过魔方管理后台（`/Admin`）配置服务商与模型。

#### Docker

```bash
docker run -d -p 5000:80 -v ./data:/app/Data newlife/chatai:latest
```

### API 网关端点

| 端点 | 协议 | 说明 |
|------|------|------|
| `POST /v1/chat/completions` | OpenAI | 聊天补全，支持流式/函数调用/视觉 |
| `POST /v1/responses` | OpenAI Responses | 推理模型（o3/gpt-5 等）|
| `POST /v1/messages` | Anthropic | Claude 系列 |
| `POST /v1/gemini` | Google Gemini | Gemini 系列 |
| `POST /v1/images/generations` | OpenAI | 文字生图 |
| `GET /v1/models` | OpenAI | 可用模型列表 |

所有网关端点通过 `Authorization: Bearer sk-xxxx` AppKey 认证。

---

## 自定义扩展

### 新增 AI 服务商

```csharp
[AiClient("myai", "MyAI", "https://api.myai.com/v1")]
[AiClientModel("myai-latest", FunctionCalling = true, Vision = true)]
public class MyAiChatClient : DelegatingChatClient
{
    // 实现协议转换逻辑
}
// AiClientRegistry 启动时自动发现，无需手动注册
```

### 新增 IChatFilter

```csharp
public class ContentAuditFilter : IChatFilter
{
    public async Task OnRequestAsync(FilterContext ctx)
    {
        // 内容检测、敏感词过滤
    }
    public async Task OnStreamCompletedAsync(FilterContext ctx)
    {
        // 审计日志、学习触发
    }
}
```

---

## 文档

| 文档 | 说明 |
|------|------|
| [需求规格说明](Doc/需求规格说明.md) | 完整功能规格，含 SSE 协议、UI 交互细节 |
| [架构设计](Doc/架构设计.md) | 系统架构、数据模型、API 接口规范 |
| [API 网关需求](Doc/API网关需求.md) | API 网关详细设计 |
| [AI 编排框架需求](Doc/AI编排框架需求.md) | Planner / MultiAgent / Memory 详细设计 |

---

## 依赖

- **NewLife.Core**：基础工具库（字符串扩展、配置、序列化）
- **NewLife.Cube**（ChatAI 应用）：Web 框架，提供权限、管理后台、配置中心
- **XCode**（ChatAI 应用）：ORM 数据库框架，支持 SQLite / MySQL / SQL Server 等

---

## License

MIT © NewLife Dev Team
