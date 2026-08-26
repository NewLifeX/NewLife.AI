# MCP 服务端接入指南（NewLife.AI.Extensions）

> 目标：任何 ASP.NET Core Web 应用只需引入 `NewLife.AI.Extensions`，即可把自身业务能力包装为标准 MCP 服务接口，供外部 AI 客户端（Claude Desktop、Cursor、VS Code 等）或编程方式调用。
> 能力对标 NuGet 官方 `ModelContextProtocol` 包（v2.x）的 P0+P1 常用能力，自研实现保持零外部依赖。
> 非 Web 场景（控制台、桌面工具等）使用 `NewLife.AI` 内置的 `HttpMcpServer` / `StdioMcpServer`，见第 7 节；宿主关系见第 8 节。

---

## 1. 最小接入（三步）

在目标 Web 应用的 `Program.cs`：

```csharp
using NewLife.AI.Extensions; // MapMcp / AddMcp

var builder = WebApplication.CreateBuilder(args);

// 1. 注册工具服务到 DI（工具方法可注入依赖）
builder.Services.AddMcp<MyTools>();

var app = builder.Build();

// 2. 暴露 MCP 端点（默认 /mcp）
app.MapMcp<MyTools>("/mcp");

app.Run();

// 3. 定义工具类：公共方法即工具（方法名自动转 snake_case）
public class MyTools
{
    /// <summary>获取当前时间</summary>
    public String GetTime() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>计算两数之和</summary>
    /// <param name="a">第一个数</param>
    /// <param name="b">第二个数</param>
    public Int32 Add(Int32 a, Int32 b) => a + b;
}
```

启动后，`POST /mcp` 即为 Streamable HTTP MCP 端点：

```bash
curl -X POST http://localhost:5000/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
```

---

## 2. 能力清单

| 能力 | 支持 | 说明 |
|------|------|------|
| Streamable HTTP（POST 单端点） | ✅ | 按 `Accept` 头协商返回 `application/json` 或 `text/event-stream` |
| initialize / tools/list / tools/call | ✅ | 完整工具发现与调用，工具名 snake_case |
| ping / 取消通知 | ✅ | JSON-RPC 心跳与客户端取消 |
| resources/list / resources/read | ✅ | 把知识库等数据暴露为资源（`server.AddResource`） |
| prompts/list / prompts/get | ✅ | 预设提示词模板（`server.AddPrompt`） |
| legacy SSE（GET /sse + POST /message） | ✅（可选） | 兼容旧客户端，`EnableLegacySse = true` 开启 |
| Bearer 认证 | ✅（可选） | 配置后所有端点需带 `Authorization: Bearer` |
| 多工具类型批量注册 | ✅ | `MapMcp("/mcp", typeof(T1), typeof(T2))` |
| 非 Web HTTP 宿主 | ✅ | `HttpMcpServer`，基于 NewLife.Core HttpServer，零 ASP.NET 依赖（见第 7 节） |
| stdio 服务端 | ✅ | `StdioMcpServer`，供 IDE 子进程拉起 |
| 请求体限制 | ✅ | 1MB 上限，防 DoS |

---

## 3. 进阶配置

### 3.1 多工具类型 + 服务器配置

```csharp
app.MapMcp("/mcp", server =>
{
    server.ResponseFormat = McpResponseFormat.Auto;   // Auto/Json/Sse
    server.EnableLegacySse = true;                     // 额外暴露 /mcp/sse + /mcp/message
    server.AuthToken = "your-token";                   // 或通过配置节读取，见 3.2
}, typeof(ToolsA), typeof(ToolsB));
```

### 3.2 认证（推荐用配置节）

`appsettings.json`：

```json
{
  "Mcp": {
    "AuthToken": "your-secret-token"
  }
}
```

`MapMcp` 自动从 `Mcp:AuthToken` 读取；配置后所有 MCP 端点（含 legacy SSE）必须携带：

```
Authorization: Bearer your-secret-token
```

未配置则开放（内网/调试场景）。

### 3.3 资源与提示词

```csharp
// 把知识库文章暴露为 resource（客户端可 resources/list + resources/read）
server.AddResource("knowledge://articles/1", "文章1", "示例文章", "text/plain",
    uri => "这是文章内容");

// 预设提示词模板（客户端可 prompts/list + prompts/get）
server.AddPrompt("知识问答", "基于知识库回答",
    arguments: [new PromptArgument("question", "问题", Required = true)],
    get: args => "回答：" + args?["question"]);
```

### 3.4 DI 注入工具依赖

工具方法需要注入服务时，先注册到 DI（`AddMcp<T>` 已注册 scoped）：

```csharp
builder.Services.AddMcp<KnowledgeMcpTools>();   // 工具内部注入 KnowledgeRetrievalService 等
```

未注册到 DI 时，工具类需有无参构造（`CreateController` 回退 `Activator.CreateInstance`）。

---

## 4. 客户端如何连接

### 4.1 用官方 MCP 客户端（互操作验证）

```csharp
// ModelContextProtocol 官方包（仅测试/外部使用）
using ModelContextProtocol.Client;

var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("http://localhost:5000/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp,
});
await using var client = await McpClient.CreateAsync(transport);

foreach (var tool in await client.ListToolsAsync())
    Console.WriteLine($"{tool.Name}: {tool.Description}");

var result = await client.CallToolAsync("add", new() { ["a"] = 1, ["b"] = 2 });
```

### 4.2 用 Claude Desktop / Cursor 等 IDE

在客户端 MCP 配置中增加远程服务：

```json
{
  "mcpServers": {
    "my-web-app": {
      "type": "http",
      "url": "http://localhost:5000/mcp",
      "headers": { "Authorization": "Bearer your-secret-token" }
    }
  }
}
```

### 4.3 用本库客户端（NewLife.AI 生态）

`McpClientService`（NewLife.ChatAI）连接外部 MCP Server 的完整流程见《星语产品手册》MCP 章节：添加服务 → 工具发现 → 触发词/禁用配置 → 对话中使用。

---

## 5. 传输与协议说明

- **Streamable HTTP**（默认）：客户端 `POST` JSON-RPC 到 `/mcp`，服务端按 `Accept` 头返回单条 JSON 或 SSE。会话通过 `Mcp-Session-Id` 响应头回写。
- **legacy SSE**（可选）：客户端 `GET /mcp/sse` 建立事件流，`POST /mcp/message` 提交消息，响应经事件流推送。
- **JSON-RPC 2.0**：`jsonrpc` 字段全小写（规范要求），错误码对齐规范；`notifications/*`（无 Id）不响应。
- **序列化**：统一 camelCase + 省略空值，兼容官方与主流第三方客户端。

---

## 6. 常见问题

| 问题 | 处理 |
|------|------|
| 工具调用返回 500 / 参数丢失 | 工具类需 public + 无参构造或已注册 DI；方法参数为标量类型（`ToolCallContext` 等特殊参数不受 MCP 协议支持） |
| 官方客户端连不上 | 确认端点路径（默认 `/mcp`）、认证头、协议版本兼容（服务端声明 2025-06-18，客户端会协商降级） |
| 需要知识库/自定义资源 | 用 `AddResource` 注册，客户端走 `resources/list` + `resources/read` |
| 公网部署安全 | 务必配置 `Mcp:AuthToken`；按需配置 `AllowedHosts` 与 CORS（见 ASP.NET Core 文档） |

---

## 7. 非 Web 场景：HttpMcpServer 与 StdioMcpServer

前 6 节针对 ASP.NET Core Web 应用（Kestrel 宿主）。目标应用**不是 Web** 时（控制台、Windows 服务、桌面工具、嵌入式设备），使用 `NewLife.AI` 包内置的两种轻量宿主，零 ASP.NET 依赖，工具/资源/提示词注册 API 与 Web 场景完全一致。

### 7.1 控制台应用：HttpMcpServer（HTTP 宿主）

```csharp
using NewLife.AI.ModelContextProtocol;   // HttpMcpServer / McpResponseFormat

var server = new HttpMcpServer
{
    Port = 8080,                          // 0 表示自动分配，Start 后从 server.Port 回读实际端口
    ResponseFormat = McpResponseFormat.Auto, // Auto/Json/Sse
};
server.AddTool<MyTools>(server);          // 注册工具类
server.Start();

Console.WriteLine($"MCP 服务已启动：http://localhost:{server.Port}/");
Console.ReadLine();
```

- 基于 `NewLife.Core` 的 `HttpServer`，无 ASP.NET 依赖，适合控制台/Windows 服务/嵌入式设备。
- 已兼容官方 MCP 客户端（含 `Transfer-Encoding: chunked` 请求体解码），`POST http://host:port/` 即为 Streamable HTTP 端点。
- 端口 `0` 时由系统自动分配，启动后从 `server.Port` 读实际端口（便于动态注册与测试）。

### 7.2 桌面/CLI 工具：StdioMcpServer（标准输入输出）

```csharp
using NewLife.AI.ModelContextProtocol;   // StdioMcpServer

var server = new StdioMcpServer();
server.AddTool<MyTools>(server);
server.Run();                             // 阻塞读取 stdin，响应写入 stdout
```

- 被外部进程（IDE、Claude Desktop、Cursor 等）作为**子进程**拉起：客户端启动本程序并接管 stdin/stdout。
- 每条 JSON-RPC 消息独占一行（newline-delimited JSON），UTF-8 无 BOM。
- 适合桌面客户端内的本地工具进程（如 StarWing 的 MCP 工具）。

Claude Desktop 配置 stdio 服务：

```json
{
  "mcpServers": {
    "my-cli": { "command": "MyTool.exe", "args": [] }
  }
}
```

---

## 8. 宿主关系澄清：McpServer 与三种宿主

| 类型 | 所在包 | 职责 | 适用场景 |
|------|--------|------|----------|
| `McpServer` | NewLife.AI | **协议核心**：JSON-RPC 2.0 处理、工具/资源/提示词管理 | 被各宿主复用，不直接对外 |
| `AspNetMcpServer` | NewLife.AI.Extensions | ASP.NET Core（Kestrel）宿主 | Web 应用（推荐，最标准成熟） |
| `HttpMcpServer` | NewLife.AI | NewLife.Core HttpServer 宿主 | 非 Web：控制台/服务/嵌入式 |
| `StdioMcpServer` | NewLife.AI | stdin/stdout 宿主 | 本地工具被 IDE 等子进程拉起 |

- `McpServer` 继承 `ApiHost`（NewLife.Remoting 基础架构：编码器/日志/追踪/DI/IExtend），**不是**基于 `ApiServer`（网络 RPC 服务器，面向网关↔内部服务 RPC，HttpCodec 按 URL 路由 action，不适合 MCP 语义）。
- 选择原则：
  - 目标应用是 **Web 应用** → `AspNetMcpServer`（`MapMcp`），最标准、官方客户端实测互通
  - 目标应用**不是 Web** 但需要 HTTP 对外 → `HttpMcpServer`
  - 本地工具被 AI 客户端**子进程拉起** → `StdioMcpServer`
- 三者共享同一协议核心，工具/资源/提示词注册 API 完全一致，切换宿主零成本。

