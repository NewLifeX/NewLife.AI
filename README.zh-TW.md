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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ 本譯文可能落後於最新版本；以 [English README](README.md) 為準。


## 專案介紹

**NewLife.AI** 是面向 .NET 生態系的**開源 AI 基礎庫**，透過統一的 `IChatClient` 介面封裝 **46 個主流大型語言模型服務商**，內建函式呼叫、MCP 協定、串流輸出、多模態、多智慧代理等能力，可作為 NuGet 套件嵌入任意 .NET 專案（`net45 → net10.0`，原生 AOT 就緒）。

庫內建 **31 種執行階段語言**（英文為源文與後備語言），涵蓋 Windows / Linux / macOS / Android / iOS 與 x64 / x86 / arm64 / arm / loongarch64 / riscv64 / s390x。

**NewLife.ChatAI** 是建構於 NewLife.AI 之上的**完整 Web 對話應用程式**（ASP.NET Core），提供開箱即用的多模型對話前端、統一 AI 閘道與自動記憶演化，既可獨立部署，也可透過 NuGet 嵌入既有 ASP.NET Core 專案。

| 產品/專案 | 定位 | 說明 |
|-----------|------|------|
| `NewLife.AI` | 開源核心 AI 基礎設施 | 統一接入主流模型服務商，涵蓋底層 AI 能力 |
| `NewLife.ChatAI` | 開源標準 Web 對話系統 | 本儲存庫 —— 標準對話應用與 AI 閘道 |

---

## 核心特色

- **原生 AOT 就緒**：net8.0/net10.0 啟用 `IsAotCompatible`，37 情境 NativeAOT 冒煙矩陣涵蓋 Windows / Linux / macOS 與 x64 / arm64 / loongarch64
- **31 種執行階段語言**：英文源文 + 編譯期語言表，缺少譯文時自動退回英文，新增語言無需修改程式碼
- **46 家 AI 服務商、6 種協定**：OpenAI / Anthropic / Gemini / 通義 DashScope / Ollama / AWS Bedrock，一行程式碼任意切換
- **統一 `IChatClient` 介面**：對齊 MEAI 規範，單輪、串流、函式呼叫、多模態全部統一 API
- **函式呼叫（工具）**：`[ToolDescription]` 特性自動產生 JSON Schema，`ToolChatClient` 多輪迴圈，內建搜尋 / 天氣 / 翻譯 / 網頁擷取 / IP 定位等工具
- **MCP 雙向支援**：用戶端對接外部 MCP Server（stdio / HTTP SSE），伺服器端將本系統工具暴露為標準 MCP 服務
- **完整對話核心**：`IChatHandler` 三段式處理鏈（OnBefore → Execute → OnAfter），內建 Handler（技能啟用 / 記憶注入 / 持久化 / 用量統計 / 標題產生），可插拔 `IChatFilter`
- **使用者記憶演化**：自動從對話中擷取 10 類結構化記憶，越用越懂使用者
- **統一 AI 閘道**：相容 OpenAI / Anthropic / Gemini / Ollama 協定，snake_case/camelCase 自動適配，AppKey 多租戶，上游 429 指數退避重試
- **技能系統**：Markdown 提示詞重複使用，`@` 遞迴引用，觸發詞自動啟用
- **多智慧代理與規劃器**：`ConversableAgent` / `GroupChat` / `ParallelGroupChat` / `FunctionCallingPlanner` / 反思修正
- **React 19 Web 前端**：SSE 串流 + 對話預設 + 工具呼叫視覺化 + 推理展示 + 多模態輸入（圖片/文件）
- **內嵌/向量檢索**：內建 HashTextEmbedder v2 與向量儲存，支援語意搜尋
- **全域系統指令**：全域系統指令統一注入所有對話行為約束
- **工具呼叫增強**：ToolCallContext 內容傳遞、結構化錯誤回傳、工具漸進式探索
- **程式設計智慧代理 CodingAgent**：支援分析/唯讀與程式碼修改任務分流執行，自動選擇最佳工具鏈
- **人機決策檢查點**：人類可在 AI 多路徑中即時選擇，支援多問題組模式的決策檢查與人工介入
- **IM 多管道接入**：內建 11 個 IM 管道（企業微信/公眾號/微信客服/QQ/Discord/釘釘/飛書/Telegram/Slack 等），支援多管道對話接入
- **Docker 部署**：支援 ChatAI 一鍵 Docker 部署

---

## 支援的 AI 服務商

46 家服務商，**9 個獨立協定用戶端 + 37 個 OpenAI 相容適配**。

### 獨立協定實作（9 個）

| 服務商 | 協定 | 特性 |
|--------|------|------|
| OpenAI | ChatCompletions / Responses | 視覺 / 函式呼叫 / 影像產生 / o3 推理 |
| DeepSeek | DeepSeek API | reasoning_content / DeepSeek v4 專屬參數 |
| Anthropic | Messages | Claude 3.5 / Claude 4 |
| Google | Gemini | Gemini 1.5 / 2.0 / 2.5 |
| 阿里雲 | DashScope | qwen-plus / qwen-max / Omni 全模態 / Files API |
| Azure AI | Azure OpenAI | 部署名稱 URL + api-key |
| Ollama | Ollama API | 本機 llama / deepseek / qwen |
| AWS Bedrock | SigV4 簽章 | Claude / Llama / Titan / Mistral |
| NewLifeAI | 串接代理 | 彙整多服務商 |
| （其他） | OpenAI 相容 | 37 個相容平台 |

### OpenAI 相容家族（37 個）

豆包（火山引擎）、智譜清言（GLM）、文心一言、月之暗面（Kimi）、MiniMax、階躍星辰（StepFun）、百川、訊飛星火、零一萬物、Moonshot、Mistral、Perplexity、Cohere、Together AI、Fireworks、OpenRouter、SiliconCloud、DeepInfra、Groq、Cerebras、Hyperbolic、Nebius、Novita、Lepton、302.AI、xAI（Grok）……以及其他 OpenAI 相容平台。

所有服務商透過 `[AiClient]` 特性宣告，`AiClientRegistry` 啟動時自動掃描註冊，新增服務商零設定。

---

## 快速開始

### 1. 僅使用 AI 基礎庫（SDK）

```bash
dotnet add package NewLife.AI
```

```csharp
using NewLife.AI.Clients;

// 單輪問答
using var client = new DashScopeChatClient("your-api-key", "qwen-plus");
var reply = await client.ChatAsync("用三句話介紹一下大型語言模型");
Console.WriteLine(reply);

// 多角色訊息（元組陣列，無需手動建構 ChatMessage）
var reply2 = await client.ChatAsync([
    ("system", "你是一名專業的 C# 開發助理"),
    ("user", "解釋一下 ValueTask 和 Task 的差異"),
]);

// 串流輸出
await foreach (var chunk in client.GetStreamingResponseAsync([
    new ChatMessage { Role = "user", Content = "寫一首關於程式碼的短詩" }
], new ChatOptions()))
{
    Console.Write(chunk.Text);
}
```

### 2. ASP.NET Core 相依性注入

```bash
dotnet add package NewLife.AI.Extensions
```

```csharp
// 註冊服務
builder.Services.AddDashScope("your-api-key", "qwen-plus");

// Keyed 多服務商並存
builder.Services.AddOpenAI("openai-key", serviceKey: "openai");
builder.Services.AddAnthropic("anthropic-key", serviceKey: "anthropic");

// 注入使用
public class MyService(IChatClient chatClient)
{
    public Task<String> ChatAsync(String question)
        => chatClient.ChatAsync(question);
}
```

### 3. 函式呼叫（工具）

```csharp
public class MyTools
{
    /// <summary>取得指定城市的天氣</summary>
    [ToolDescription("get_weather")]
    public async Task<String> GetWeatherAsync(
        [Description("城市名")] String city)
        => $"{city} 今天晴，22°C";
}

// 註冊工具
var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// 掛入管線，自動多輪迴圈工具呼叫
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// 模型自動呼叫 get_weather("北京")，回傳最終文字答案
var reply = await client.ChatAsync("北京今天天氣如何？");
```

### 4. 限制推理路徑（ReAct 模式）

當 AI 面對複雜問題容易猜測或走冤枉路時，在 System Prompt 中使用 **ReAct 格式**強制逐步推理。
`ToolChatClient` 已內建 `while` 大迴圈（`MaxIterations = 10`），無需手寫 Agent；
搭配 ReAct System Prompt 可進一步限制每一步的推理方向，避免跳步猜測。

```csharp
// UseTools 管線組裝：ToolChatClient 自動迴圈至模型不再發起工具呼叫為止
var client = rawClient.AsBuilder()
    .UseTools(registry)
    .Build();

// ReAct System Prompt：Thought → Action → Observation → Answer 強制逐步推理
var reply = await client.ChatAsync([
    ("system",
        "你必須依下列格式逐步推理，不允許跳過步驟或直接猜測：\n" +
        "Thought: <分析目前需要做什麼>\n" +
        "Action: <要呼叫哪個工具及參數>\n" +
        "Observation: <分析工具回傳結果>\n" +
        "（重複 Thought/Action/Observation 直到資訊充分）\n" +
        "Answer: <根據以上觀察得出最終結論>\n\n" +
        "規則：禁止在未呼叫工具的情況下直接給出 Answer。"),
    ("user", "北京今天天氣適合戶外運動嗎？"),
]);
```

### 5. 執行完整 Web 對話應用程式

```bash
git clone https://github.com/NewLifeX/NewLife.AI.git
cd NewLife.AI

# 建置前端（需要 Node.js + pnpm）
cd Web && pnpm install && pnpm build && cd ..

# 啟動
cd NewLife.ChatAI
dotnet run --framework net8.0
```

瀏覽器開啟 `http://localhost:5000`，預設 SQLite，開箱即用。首次啟動請透過 `/Admin` 設定服務商 API Key。

亦可將 `NewLife.ChatAI` 透過 NuGet 嵌入既有專案：

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

## API 閘道

NewLife.ChatAI 內建多協定 AI 閘道，第三方系統無需改造即可接入。預設以**純淨轉發**模式執行（驗證 / 限流 / 配額 / 用量，不記錄對話）；開啟**閘道領域模式**（EnableGatewayDomainMode）後走完整處理器鏈，享受記憶注入與技能增強並自動記錄對話。

| 協定 | 路由 | 說明 |
|------|------|------|
| OpenAI ChatCompletions | `POST /v1/chat/completions` | 串流 / 非串流 / 函式呼叫 / 視覺 |
| OpenAI Responses | `POST /v1/responses` | o3 / gpt-5 推理模型 |
| Anthropic Messages | `POST /v1/messages` | Claude 系列 |
| Google Gemini | `POST /v1/gemini/...` | Gemini 系列 |
| Ollama | `POST /api/chat` `/api/tags` 等 | 偽裝 Ollama 服務，相容 Ollama 用戶端 |
| 影像產生 | `POST /v1/images/generations` | Text-to-Image |
| 影像編輯 | `POST /v1/images/edits` | Inpainting（multipart/form-data）|
| 嵌入向量 | `POST /v1/embeddings` | OpenAI 相容 |
| 語音 | `POST /v1/audio/speech` `/v1/audio/transcriptions` | TTS / ASR |
| 影片產生 | `POST /v1/video/generations` | 任務式提交與狀態查詢 |
| 重排序 | `POST /v1/reranks` | Rerank |
| 模型探索 | `GET /v1/models` | 可用模型清單 |

**驗證**：`Authorization: Bearer sk-xxxx`（AppKey）

**特性**：上游 429 指數退避重試（隨機抖動，最多 5 次）、Token 用量自動記錄、依 AppKey + 使用者雙維度統計

---

## 擴充開發

### 新增 AI 服務商

繼承 `OpenAIChatClient`，加入 `[AiClient]` 特性，`AiClientRegistry` 啟動自動掃描註冊：

```csharp
[AiClient("MyAI", "我的服務", "https://api.myai.com/v1",
    Description = "自訂 AI 服務")]
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
    /// <summary>查詢目前時間</summary>
    [ToolDescription("get_current_time")]
    public String GetCurrentTime()
        => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

var registry = new ToolRegistry();
registry.AddTools<MyTools>(new MyTools());

// DI 情境
services.AddSingleton<IToolProvider>(_ =>
{
    var r = new ToolRegistry();
    r.AddTools<MyTools>(new MyTools());
    return r;
});
```

### 新增 IChatHandler

在對話事前（OnBefore）或事後（OnAfter）注入自訂邏輯（如內容注入、稽核、記錄）：

```csharp
[ChatHandlerOrder(150)]
public class CurrentTimeHandler : ChatHandlerBase
{
    public override Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        context.ContextMessages.Insert(0,
            new ChatMessage { Role = "system", Content = $"目前時間：{DateTime.Now:yyyy-MM-dd HH:mm}" });
        return Task.CompletedTask;
    }
}

// DI 註冊，MessageFlow 自動依 ChatHandlerOrderAttribute 排序呼叫
services.AddSingleton<IChatHandler, CurrentTimeHandler>();
```

### 新增篩選器

洋蔥圈模型，可在對話前後插入日誌、稽核、內容審核等邏輯：

```csharp
public class AuditFilter : IChatFilter
{
    public async Task OnChatAsync(
        ChatFilterContext ctx,
        Func<ChatFilterContext, CancellationToken, Task> next,
        CancellationToken ct)
    {
        // before：記錄輸入 / 敏感詞過濾
        await next(ctx, ct);
        // after：記錄輸出 / 寫入稽核日誌
    }

    public Task OnStreamCompletedAsync(ChatFilterContext ctx, CancellationToken ct)
        => Task.CompletedTask;
}
```

---

## 文件

| 文件 | 說明 | 對應模組 |
|------|------|:--------:|
| [需求文件](Doc/需求文档.md) | 產品願景、核心目標、功能需求（模組編碼體系） | ALL |
| [功能清單](Doc/功能清单.md) | 完整功能清單，三維狀態追蹤 | ALL |
| [架構設計](Doc/架构设计.md) | 四層架構、各模組設計細節 | ALL |
| [協定用戶端分析](Doc/协议客户端分析.md) | 協定用戶端 chat/stream/think 健康度分析、缺口修復記錄與測試涵蓋 | AI |
| [基礎庫稽核](Doc/基础库审计.md) | 基礎庫 12 維度全景稽核：分 15+ 輪登錄與修復問題（協定用戶端/MCP/工具鏈/記憶等） | ALL |
| [AI 編排框架需求](Doc/AI编排框架需求.md) | 工具 / 智慧代理 / 規劃器設計 | AI / AGENT |
| [API 閘道需求](Doc/API网关需求.md) | 閘道協定適配詳解 | GW |
| [MCP 架構](Doc/MCP架构.md) | MCP 用戶端與伺服器端設計 | TOOL |
| [技能管理需求](Doc/技能管理需求.md) | 技能系統詳細設計 | TOOL |
| [自學習系統需求](Doc/自学习系统需求.md) | 對話分析 + 記憶擷取 | EVO |
| [對話資料保存流程](Doc/对话数据保存流程.md) | MessageFlow 詳細流程 | CHAT |
| [AOT 相容性分析](Doc/AOT兼容性分析.md) | NativeAOT 支援邊界、冒煙矩陣與平台/架構涵蓋 | ALL |
| [多語言在地化](Doc/多语言本地化.md) | 31 種執行階段語言、詞表維護與貢獻流程 | ALL |

---

## 生態版圖

NewLife 生態依相依性分層，各專案獨立版本、獨立發行。下方版圖在所有 NewLife 儲存庫中逐字一致；每個專案連結到程式碼儲存庫與主 NuGet 套件。

| 專案 | 一句話定位 | 下載量 |
| --- | --- | --- |
| **基礎底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日誌、設定、快取、網路、序列化、排程與 APM 追蹤。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 資料中介層：單表百億級、自動分表、讀寫分離，支援 10 多種資料庫。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高效能網路庫：單機 177 萬 pps、14.5 Gbps（TCP 1KB，2026-10 實測），400 萬 Tcp 長連線。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 協定通訊與 RPC 框架，面向 CS 應用與物聯網裝置。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 將 .NET 應用程式安裝為作業系統守護行程：Windows 服務與 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 專案範本：主控台、Web、WebApi、服務、資料與物聯網。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **資料儲存與訊息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 用戶端：微秒級延遲、百萬級輸送量，內建訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 純受控 RocketMQ 用戶端，支援 Apache RocketMQ 與阿里雲訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 用戶端與伺服器端，用戶端支援阿里雲物聯網。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合式資料庫：內嵌/網路雙模，結合關聯式、時序、訊息佇列與 KV。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | MySql 驅動程式與資料存取輔助層，以 NewLife 資料堆疊實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 與分散式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 魔方快速開發平台：使用者權限、SSO、OAuth 伺服器端與 CRUD 樣板。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 星塵分散式服務平台：節點管理、APM、設定中心、註冊中心與發布中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 螞蟻調度：分散式任務與即時資料平台，支援分片、重試與回溯補算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 統一接入 46 家大型語言模型服務商，附開箱即用的 ChatAI Web 對話應用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工業與物聯網** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | IoT 標準庫，定義物聯網通訊協定契約與裝置抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | Modbus TCP/RTU/ASCII，以 NewLife.IoT 實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 西門子 PLC 協定，以 NewLife.IoT 實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 歐姆龍 PLC 協定，以 NewLife.IoT 實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 三菱 Melsec PLC 協定，以 NewLife.IoT 實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | OPC 用戶端，用於工業資料擷取。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 樓宇自控協定堆疊。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 開源輕量級 IoT 平台，含閘道與裝置管理。 | — |
| **車聯網** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車輛定位協定：70+ 訊息體，涵蓋 2011/2013/2019 三個版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部標平台互聯協定，面向運輸管理部門。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載影片：RTP 解析、FLV/fMP4 轉封裝與直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **領域元件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一個套件涵蓋 Excel、Word、PPT、PDF 等 15+ 文件格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音訊：編解碼、裝置 IO、DSP、容器與串流處理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 影片編解碼、封裝格式、影格處理與串流媒體。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度/高德/騰訊/天地圖統一介面，支援地理編碼與座標轉換。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 歸屬地：離線記憶體映射庫 + 線上 API 雙來源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具與產品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 碼神工具：網路、序列埠、加解密、正規表達式、Modbus 與 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自架 Git 原始碼管理系統。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 從低版本宿主啟動時，自動安裝相符的 .NET 執行階段。 | — |

> 徽章取各專案主套件；主題套件、擴充套件與衛星套件見對應 NuGet 頁面。未在表中列出的 30 多個套件（FTP、LoRa、Office 擴充、各類協定驅動程式等）見 [NewLifeX 組織](https://github.com/NewLifeX) 與 [newlifex.com](https://newlifex.com)。

## 新生命開發團隊

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命團隊成立於 2002 年，二十餘年專注 .NET 與物聯網技術生態，主導開源專案近百個（生態版圖收錄 34 個主幹專案），為電力、物流、醫療、工控、網際網路等產業提供軟硬體整合解決方案；以「學無先後，達者為師」為信條。

開源元件在 NuGet 累計下載 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生態口徑，截至 2026-10）。

網站：https://newlifex.com · 開源組織：https://github.com/NewLifeX

<details>
<summary>微信公眾號：智能大石头（掃碼關注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 授權條款

[MIT License](LICENSE)

歡迎提交 Issue 與 Pull Request。

- GitHub：https://github.com/NewLifeX/NewLife.AI
- 官網：https://newlifex.com
- QQ 群：1600800

## 相關專案

- [NewLife.Core](https://github.com/NewLifeX/X) — .NET 基礎庫
- [XCode](https://github.com/NewLifeX/X) — ORM 資料框架
- [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) — 魔方快速開發平台
