// NewLife.AI NativeAOT 冒烟：核心链路逐项 PASS/FAIL，任一失败以非 0 退出码结束。
// 同一工程双跑对照：JIT（dotnet run -c Release）与 NativeAOT（dotnet publish -c Release -r <rid> 后运行产物）。
// 背景与边界见 Doc/AOT兼容性分析.md。
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Embedding;
using NewLife.AI.ModelContextProtocol;
using NewLife.AI.Models;
using NewLife.AI.Planner;
using NewLife.AI.Services;
using NewLife.AI.Tools;
using NewLife.Serialization;

var failures = new List<String>();

void Check(String name, Boolean ok)
{
    Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}");
    if (!ok) failures.Add(name);
}

void Run(String name, Func<Boolean> func)
{
    try
    {
        Check(name, func());
    }
    catch (Exception ex)
    {
        var e = ex;
        while (e is TypeInitializationException && e.InnerException != null) e = e.InnerException;
        var lines = e.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"[FAIL] {name} -> {String.Join(Environment.NewLine, lines.Take(8))}");
        failures.Add(name);
    }
}

void RunAsync(String name, Func<Task<Boolean>> func)
{
    try
    {
        Check(name, func().GetAwaiter().GetResult());
    }
    catch (Exception ex)
    {
        var e = ex;
        while (e is TypeInitializationException && e.InnerException != null) e = e.InnerException;
        var lines = e.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"[FAIL] {name} -> {String.Join(Environment.NewLine, lines.Take(8))}");
        failures.Add(name);
    }
}

Console.WriteLine($"IsDynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported}");
Console.WriteLine($"Assembly.Location={GetAILocation()}");

[UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "AOT 判真探针：以 Location 是否为空区分原生发布件与 JIT 运行，不用于路径判断")]
static String? GetAILocation() => typeof(ChatMessage).Assembly.Location;

// 1. 基础类型转换（Core 能力）
Run("1.Utility 基础类型转换", () => "123".ToInt() == 123 && "3.14".ToDouble() == 3.14 && "true".ToBoolean());

// 2. 对话消息 JSON 往返（JsonHelper 反射序列化路径）
Run("2.ChatMessage JSON 往返", () =>
{
    var msg = new ChatMessage { Role = "user", Content = "hello aot", ReasoningContent = "think" };
    var json = JsonHelper.ToJson(msg);
    var back = JsonHelper.ToJsonEntity<ChatMessage>(json);
    Console.WriteLine($"    (json={json})");
    return back != null && back.Role == "user" && back.Content is String s && s == "hello aot";
});

// 3. 服务商注册表默认扫描
Run("3.AiClientRegistry 默认扫描", () =>
{
    var registry = AiClientRegistry.Default;
    var count = registry.Descriptors.Count;
    Console.WriteLine($"    (providers={count})");
    return count >= 40 && registry.GetDescriptor("OpenAI") != null;
});

// 4. 服务商描述符元数据（模型/能力/价格）
Run("4.服务商描述符元数据", () =>
{
    var descriptor = AiClientRegistry.Default.GetDescriptor("OpenAI");
    if (descriptor == null || descriptor.Models.Length == 0) return false;
    var info = descriptor.FindModelInfo(descriptor.Models[0].Model);
    Console.WriteLine($"    (code={descriptor.Code} models={descriptor.Models.Length} first={descriptor.Models[0].Model})");
    return info != null && descriptor.DefaultEndpoint.Length > 0;
});

// 5. 按服务商编码创建客户端（不发网络请求）
Run("5.按编码创建客户端", () =>
{
    using var client = AiClientRegistry.Default.CreateClient("OpenAI", "sk-aot-smoke");
    Console.WriteLine($"    (client={client.GetType().Name})");
    return client is AiClientBase baseClient && baseClient.Name.Length > 0;
});

// 6. ChatClientBuilder 管道装配
Run("6.ChatClientBuilder 管道装配", () =>
{
    using var raw = AiClientRegistry.Default.CreateClient("DashScope", "sk-aot-smoke");
    var registry = new ToolRegistry();
    registry.AddTools(new SmokeTools());
    using var client = new ChatClientBuilder(raw).UseTools(registry).Build();
    return client is ToolChatClient toolClient && toolClient.Providers.Count == 1;
});

// 7. 工具 Schema 构建（基础类型/枚举/数组/嵌套对象）
Run("7.工具 Schema 构建", () =>
{
    var method = typeof(SmokeTools).GetMethod(nameof(SmokeTools.SearchAsync))!;
    var tool = ToolSchemaBuilder.BuildFromMethod(method);
    var json = JsonHelper.ToJson(tool);
    Console.WriteLine($"    (tool={tool.Function?.Name} schema={json?.Length})");
    return tool.Function?.Name == "search" && json!.Contains("parameters", StringComparison.OrdinalIgnoreCase) && json.Contains("properties", StringComparison.OrdinalIgnoreCase);
});

// 8. 工具注册与调用（参数类型转换）
RunAsync("8.工具注册与调用", async () =>
{
    var registry = new ToolRegistry();
    registry.AddTools(new SmokeTools());
    var result = await registry.InvokeAsync("search", "{\"keyword\":\"newlife\",\"limit\":3}");
    Console.WriteLine($"    (result={result})");
    return result.Contains("newlife") && result.Contains("3");
});

// 9. 工具别名与未知工具
RunAsync("9.工具别名与未知工具", async () =>
{
    var registry = new ToolRegistry();
    registry.AddTools(new SmokeTools());
    registry.AddToolAlias("find", "search");
    var result = await registry.InvokeAsync("find", "{\"keyword\":\"alias\"}");
    var missing = await registry.TryInvokeAsync("not_exists", "{}");
    return result.Contains("alias") && missing.Contains("not_exists");
});

// 10. ToolChatClient 多轮工具循环（离线假客户端）
RunAsync("10.ToolChatClient 多轮工具循环", async () =>
{
    var registry = new ToolRegistry();
    registry.AddTools(new SmokeTools());
    using var fake = new FakeChatClient();
    using var client = new ChatClientBuilder(fake).UseTools(registry).Build();
    var reply = await client.GetResponseAsync([new ChatMessage { Role = "user", Content = "search newlife" }]);
    Console.WriteLine($"    (calls={fake.CallCount} reply={reply.Text})");
    return fake.CallCount == 2 && reply.Text == "final answer";
});

// 11. Token 估算与预算截断
Run("11.Token 估算与预算截断", () =>
{
    var messages = new List<ChatMessage>
    {
        new() { Role = "system", Content = "you are helpful" },
        new() { Role = "user", Content = new String('a', 4000) },
    };
    var before = TokenEstimator.EstimateTokens(messages);
    TokenEstimator.TryTruncateToBudget(messages, 256);
    var after = TokenEstimator.EstimateTokens(messages);
    Console.WriteLine($"    (before={before} after={after})");
    return before > after && after <= 300;
});

// 12. 哈希嵌入与向量相似度
Run("12.哈希嵌入与向量相似度", () =>
{
    var embedder = new HashTextEmbedder(128);
    var a = embedder.Embed("NewLife AI gateway");
    var b = embedder.Embed("NewLife AI gateway");
    var c = embedder.Embed("unrelated text about weather");
    var dot = 0d; var na = 0d; var nc = 0d;
    for (var i = 0; i < a.Length; i++) { dot += a[i] * c[i]; na += a[i] * a[i]; nc += c[i] * c[i]; }
    var simSame = a.SequenceEqual(b);
    var simDiff = dot / Math.Sqrt(na * nc);
    Console.WriteLine($"    (same={simSame} diff={simDiff:F3})");
    return simSame && simDiff < 0.9;
});

// 13. MCP 工具管理器注册与反射调用
RunAsync("13.MCP 工具管理器调用", async () =>
{
    var manager = new McpToolManager();
    manager.Register<SmokeTools>();
    var names = manager.Tools.Keys.ToList();
    var tool = manager.Tools["search"];
    var result = (String)tool.Invoke(new SmokeTools(), new Dictionary<String, Object?> { ["keyword"] = "mcp", ["limit"] = 2L })!;
    Console.WriteLine($"    (tools={names.Count} result={result})");
    return names.Count >= 2 && result.Contains("mcp");
});

// 14. MCP 服务器 JSON-RPC initialize / tools/list
Run("14.MCP 服务器 JSON-RPC", () =>
{
    var server = new McpServer();
    server.AddTool(typeof(SmokeTools));
    var context = new McpContext
    {
        Services = new EmptyServiceProvider(),
        GetRequest = _ => null,
        SetResponse = (_, _) => { },
    };
    var init = server.Process(new JsonRpcRequest("2.0", "initialize", new InitializeParams("2025-06-18", new ClientInfo("aot-smoke", "1.0")), 1), context);
    var list = server.Process(new JsonRpcRequest("2.0", "tools/list", null, 2), context);
    var listJson = JsonHelper.ToJson(list);
    Console.WriteLine($"    (init={init.Result?.GetType().Name} list={listJson?.Length})");
    return init.Error == null && list.Error == null && listJson != null && listJson.Contains("search");
});

// 15. 流式事件模型
Run("15.ChatStreamEvent 事件与 JSON", () =>
{
    var evt = ChatStreamEvent.MessageStart(1, "qwen-plus");
    var json = JsonHelper.ToJson(evt);
    return evt.Type == "message_start" && json != null && json.Contains("qwen-plus");
});

// 16. 错误分类
Run("16.错误分类", () =>
{
    var info = ChatErrorHelper.Classify("This model's maximum context length is 8192 tokens", "STREAM_ERROR");
    Console.WriteLine($"    (code={info.Code} ctx={info.ContextLength})");
    return info.Code == ChatErrorHelper.ContextTooLongCode && ChatErrorHelper.IsContextLengthError(info.Message);
});

// 17. 模型家族能力推断
Run("17.模型家族能力推断", () =>
{
    var caps = ModelFamilyRegistry.Match("qwen-max-latest");
    var family = ModelFamilyRegistry.MatchFamily("claude-3-5-sonnet");
    Console.WriteLine($"    (qwen={caps != null} family={family?.Name})");
    return caps != null && family != null;
});

// 18. 消息限流器
Run("18.消息限流器", () =>
{
    var limiter = new MessageRateLimiter();
    var first = limiter.IsAllowed("user", 42, 2);
    var second = limiter.IsAllowed("user", 42, 2);
    var third = limiter.IsAllowed("user", 42, 2);
    var other = limiter.IsAllowed("group", 42, 2);
    return first && second && !third && other;
});

// 19. 内置工具（时间）
Run("19.内置工具 时间", () =>
{
    var service = new BuiltinToolService();
    var time = service.GetCurrentTime();
    return time != null && time.Length > 10;
});

// 20. 编码计划 JSON 往返
Run("20.CodingPlan JSON 往返", () =>
{
    var plan = new NewLife.AI.Coding.Models.CodingPlan
    {
        Requirement = "add aot smoke",
        Summary = "smoke",
        Tasks = [new NewLife.AI.Coding.Models.CodingTask { Id = "F001", Description = "task1" }],
    };
    var json = plan.ToJson();
    var back = NewLife.AI.Coding.Models.CodingPlan.FromJson(json);
    Console.WriteLine($"    (tasks={back?.Tasks.Count})");
    return back != null && back.Requirement == "add aot smoke" && back.Tasks.Count == 1;
});

// 21. 规划器（离线假客户端返回计划 JSON）
RunAsync("21.FunctionCallingPlanner 解析", async () =>
{
    using var fake = new FakeChatClient();
    var planner = new FunctionCallingPlanner();
    var registry = new ToolRegistry();
    registry.AddTools(new SmokeTools());
    var plan = await planner.CreatePlanAsync("plan something", registry.Tools.ToList(), fake);
    Console.WriteLine($"    (steps={plan.Steps.Count})");
    return plan.Steps.Count == 2 && plan.Steps[0].ToolName == "search";
});

// 22. 本地化回退（InvariantGlobalization 下具名区域性不可用，只断言英文源文）
Run("22.本地化英文回退", () => Localization.Translate("Invalid parameter: {0}", 42) == "Invalid parameter: 42");

Console.WriteLine();
if (failures.Count > 0)
{
    Console.WriteLine($"FAILED {failures.Count}: {String.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("ALL PASS");
return 0;

sealed class EmptyServiceProvider : IServiceProvider
{
    public Object? GetService(Type serviceType) => null;
}

sealed class FakeChatClient : IChatClient
{
    public Int32 CallCount { get; private set; }

    public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        var response = new ChatResponse { Model = request.Model ?? "fake" };

        if (CallCount == 1)
        {
            response.Add(null, null, FinishReason.ToolCalls);
            response.Messages![0].Message = new ChatMessage
            {
                Role = "assistant",
                ToolCalls =
                [
                    new ToolCall
                    {
                        Id = "call_1",
                        Function = new FunctionCall { Name = "search", Arguments = "{\"keyword\":\"newlife\"}" },
                    },
                    new ToolCall
                    {
                        Id = "call_2",
                        Function = new FunctionCall { Name = "get_current_time", Arguments = "{}" },
                    },
                ],
            };
        }
        else
        {
            response.Add("final answer", null, FinishReason.Stop);
        }

        return Task.FromResult<IChatResponse>(response);
    }

    public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return await GetResponseAsync(request, cancellationToken);
    }

    public void Dispose() { }
}

sealed class SmokeTools
{
    [ToolDescription("search", Triggers = "搜索,查找")]
    [Description("Search text")]
    public Task<String> SearchAsync(
        [Description("Search keyword")] String keyword,
        [Description("Max results")] Int32 limit = 5)
        => Task.FromResult($"search:{keyword}:{limit}");

    [ToolDescription("get_current_time")]
    public String GetCurrentTime() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}
