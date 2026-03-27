# NewLife.AI

[![NuGet](https://img.shields.io/nuget/v/NewLife.AI.svg?style=flat-square)](https://www.nuget.org/packages/NewLife.AI/)
[![NuGet Download](https://img.shields.io/nuget/dt/NewLife.AI.svg?style=flat-square)](https://www.nuget.org/packages/NewLife.AI/)
[![.NET](https://img.shields.io/badge/.NET-Standard2.1%2Bnet8%2Bnet10-purple?style=flat-square)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

**NewLife.AI** 是统一 AI 网关基础库，支持 33 个主流 AI 服务商的接入与编排。
**NewLife.ChatAI** 是基于 NewLife.AI 构建的完整 AI 对话应用，提供对话 Web 前端、API 网关路由、使用量统计等全套业务能力。

---

## 核心特性

- **33 个服务商支持**：OpenAI、Anthropic（Claude）、Google Gemini、阿里百炼（通义千问）、DeepSeek、Kimi、豆包、智谱、百度、讯飞、腾讯混元、Ollama（本地）等
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

## 快速开始

### 安装 NuGet 包

```bash
# 核心基础库
dotnet add package NewLife.AI

# ASP.NET Core MCP Server 扩展（可选）
dotnet add package NewLife.AI.Extensions
```

### 代码示例

#### 简单 AskAsync

```csharp
using NewLife.AI.Providers;

// 从空 Builder 出发，链式设置服务商，Build() 得到 IChatClient
var client = new ChatClientBuilder()
    .UseDashScope("your-api-key", model: "qwen-plus")
    .Build();

// 发送单条消息，直接返回回复文本
var reply = await client.AskAsync("你好，请介绍一下你自己");
Console.WriteLine(reply);
```

#### 简易双消息 AskAsync

```csharp
// 以元组数组传入多角色消息，无需构造 ChatMessage 对象，每项为 (role, content)
var reply = await client.AskAsync([
    ("system", "你是一名专业的 C# 开发助手"),
    ("user", "请解释什么是依赖注入"),
]);
Console.WriteLine(reply);
```

#### 多模态 GetStreamingResponseAsync（流式 + 图片）

```csharp
// 视觉理解：将图片与文字问题组合为多模态消息
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

#### ChatClientBuilder 中间件管道

```csharp
// MEAI 风格：先设置服务商，再链式叠加中间件
var client = new ChatClientBuilder()
    .UseDashScope("your-api-key", model: "qwen-plus")
    .UseFilters(new LoggingFilter())   // 日志 / 审计 / 速率限制
    .UseTools(toolRegistry)             // 自动多轮 Function Calling
    .Build();
```

#### 自定义工具（原生 .NET 方法）

```csharp
public class WeatherService : IToolProvider
{
    [ToolDescription("获取城市实时天气")]
    public async Task<String> GetWeatherAsync(
        [Description("城市名称")] String city)
    {
        // 调用天气 API
        return $"{city} 今天晴，25°C";
    }
}

// 注册到 DI
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
| 工具调用 | Function Calling + MCP 工具，前端折叠展示 ToolCallBlock |
| 图像多模态 | 图像上传理解、文生图、图像编辑（Inpainting） |
| API 网关 | 兼容 OpenAI / Anthropic / Gemini 标准协议，AppKey 认证 |
| 会话管理 | 置顶、分组、关键词搜索、分享链接 |
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
