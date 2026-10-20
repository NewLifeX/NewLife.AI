// NewLife.AI NativeAOT 冒烟：核心链路逐项 PASS/FAIL，任一失败以非 0 退出码结束。
// 同一工程双跑对照：JIT（dotnet run -c Release）与 NativeAOT（dotnet publish -c Release -r <rid> 后运行产物）。
// 背景与边界见 Doc/AOT兼容性分析.md。
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Embedding;
using NewLife.AI.Filters;
using NewLife.AI.Memory;
using NewLife.AI.ModelContextProtocol;
using NewLife.AI.Models;
using NewLife.AI.Planner;
using NewLife.AI.Services;
using NewLife.AI.Tools;
using NewLife.Data;
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

// 23. OpenAI 请求体构建与 JSON 序列化
Run("23.OpenAI 请求构建与序列化", () =>
{
    var req = ChatRequest.Create([new ChatMessage { Role = "user", Content = "hello" }], new ChatOptions
    {
        Model = "gpt-4o",
        Temperature = 0.2,
        Tools = [ToolSchemaBuilder.BuildFromMethod(typeof(SmokeTools).GetMethod(nameof(SmokeTools.SearchAsync))!)],
    });
    var body = ChatCompletionRequest.FromChatRequest(req);
    var json = JsonHelper.ToJson(body)!;
    Console.WriteLine($"    (len={json.Length})");
    return body.Model == "gpt-4o"
        && json.Contains("gpt-4o", StringComparison.OrdinalIgnoreCase)
        && json.Contains("search", StringComparison.OrdinalIgnoreCase);
});

// 24. OpenAI 响应解析
Run("24.OpenAI 响应解析", () =>
{
    const String json = """{"id":"chatcmpl-1","object":"chat.completion","created":1700000000,"model":"gpt-4o","choices":[{"index":0,"message":{"role":"assistant","content":"hello"},"finish_reason":"stop"}],"Usage":{"PromptTokens":3,"CompletionTokens":2,"TotalTokens":5}}""";
    var resp = JsonHelper.ToJsonEntity<ChatCompletionResponse>(json);
    var chat = resp?.ToChatResponse();
    Console.WriteLine($"    (choices={resp?.Choices?.Count} text={chat?.Text} in={chat?.Usage?.InputTokens} out={chat?.Usage?.OutputTokens})");
    return chat?.Text == "hello" && chat.Usage?.InputTokens == 3 && chat.Usage?.OutputTokens == 2;
});

// 25. Anthropic 请求/响应解析
Run("25.Anthropic 请求/响应解析", () =>
{
    var req = JsonHelper.ToJsonEntity<AnthropicRequest>("""{"model":"claude-3-5-sonnet","max_tokens":16,"messages":[{"role":"user","content":"hi"}]}""");
    var chatReq = req?.ToChatRequest();
    var resp = JsonHelper.ToJsonEntity<AnthropicResponse>("""{"id":"msg_1","type":"message","role":"assistant","model":"claude-3","content":[{"type":"text","text":"hi"}],"stop_reason":"end_turn","usage":{"input_tokens":3,"output_tokens":2}}""");
    var chat = resp?.ToChatResponse();
    return chatReq?.Messages.Count == 1 && chat?.Text == "hi";
});

// 26. Gemini 请求/响应解析
Run("26.Gemini 请求/响应解析", () =>
{
    var req = JsonHelper.ToJsonEntity<GeminiRequest>("""{"contents":[{"role":"user","parts":[{"text":"hi"}]}]}""");
    var chatReq = req?.ToChatRequest();
    var resp = JsonHelper.ToJsonEntity<GeminiResponse>("""{"candidates":[{"content":{"role":"model","parts":[{"text":"hi"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":3,"candidatesTokenCount":2,"totalTokenCount":5}}""");
    var chat = resp?.ToChatResponse();
    return chatReq?.Messages.Count == 1 && chat?.Text == "hi";
});

// 27. Ollama 响应解析
Run("27.Ollama 响应解析", () =>
{
    var resp = JsonHelper.ToJsonEntity<OllamaChatResponse>("""{"model":"qwen","created_at":"2026-01-01T00:00:00Z","message":{"role":"assistant","content":"hi"},"done":true,"done_reason":"stop","prompt_eval_count":3,"eval_count":2}""");
    var chat = resp?.ToChatResponse();
    return chat?.Text == "hi";
});

// 28. DashScope 请求构建与响应解析
Run("28.DashScope 请求/响应解析", () =>
{
    var req = DashScopeRequest.FromChatRequest(ChatRequest.Create([new ChatMessage { Role = "user", Content = "hi" }], new ChatOptions { Model = "qwen-plus" }));
    var reqJson = JsonHelper.ToJson(req)!;
    var resp = JsonHelper.ToJsonEntity<DashScopeResponse>("""{"request_id":"r1","model":"qwen-plus","output":{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"hi"}}]},"usage":{"input_tokens":3,"output_tokens":2,"total_tokens":5}}""");
    var chat = resp?.ToChatResponse();
    return reqJson.Contains("qwen-plus", StringComparison.OrdinalIgnoreCase) && chat?.Text == "hi";
});

// 29. MCP tools/call 完整 JSON-RPC
Run("29.MCP tools/call", () =>
{
    var server = new McpServer();
    server.AddTool(typeof(SmokeTools));
    var context = new McpContext
    {
        Services = new EmptyServiceProvider(),
        GetRequest = _ => null,
        SetResponse = (_, _) => { },
    };
    var args = new Dictionary<String, Object?> { ["keyword"] = "mcp", ["limit"] = 2L };
    var response = server.Process(new JsonRpcRequest("2.0", "tools/call", new ToolCallParams("search", args, null), 3), context);
    var json = JsonHelper.ToJson(response)!;
    if (response.Error != null) Console.WriteLine($"    (error={JsonHelper.ToJson(response.Error)})");
    Console.WriteLine($"    (len={json.Length})");
    return response.Error == null && json.Contains("search:mcp:2", StringComparison.OrdinalIgnoreCase);
});

// 30. MCP resources/read
Run("30.MCP resources/read", () =>
{
    var server = new McpServer();
    server.AddResource("knowledge://aot", "AOT", read: _ => "resource-body");
    var context = new McpContext
    {
        Services = new EmptyServiceProvider(),
        GetRequest = _ => null,
        SetResponse = (_, _) => { },
    };
    var response = server.Process(new JsonRpcRequest("2.0", "resources/read", new ResourceReadParams("knowledge://aot"), 4), context);
    var json = JsonHelper.ToJson(response)!;
    return response.Error == null && json.Contains("resource-body");
});

// 31. 内存向量库写入与检索
RunAsync("31.内存向量库写入与检索", async () =>
{
    var store = new InMemoryVectorStore();
    var collection = store.GetCollection("smoke");
    var embedder = new HashTextEmbedder(64);
    await collection.UpsertAsync(new VectorRecord { Id = "1", Vector = embedder.Embed("newlife ai gateway") });
    await collection.UpsertAsync(new VectorRecord { Id = "2", Vector = embedder.Embed("weather forecast tomorrow") });
    var hits = await collection.SearchAsync(embedder.Embed("newlife ai gateway"), 1);
    Console.WriteLine($"    (hits={hits.Count})");
    return hits.Count == 1 && hits[0].Record.Id == "1";
});

// 32. 显式注册服务商（自定义程序集场景）
Run("32.显式注册服务商", RegisterExplicitProvider);

[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "冒烟探针：验证 AOT 下显式注册路径，内置服务商构造函数由 TrimmerRootAssembly 保留")]
static Boolean RegisterExplicitProvider()
{
    var registry = new AiClientRegistry();
    registry.Register<OpenAIChatClient>();
    return registry.GetDescriptor("OpenAI") != null;
}

// 33. 内置工具 计算器（System.Data 表达式解析在 AOT 下的行为）
Run("33.内置工具 计算器", () =>
{
    var service = new BuiltinToolService();
    var result = service.Calculate("(3 + 5) * 2");
    var json = JsonHelper.ToJson(result)!;
    Console.WriteLine($"    ({json})");
    return json.Contains("16");
});

// 34. IChatFilter 洋葱圈管道
RunAsync("34.IChatFilter 洋葱圈管道", async () =>
{
    var filter = new CountingFilter();
    var client = new ChatClientBuilder(new FakeChatClient()).UseFilters(filter).Build();
    var request = new ChatRequest { Model = "fake", Messages = [new ChatMessage { Role = "user", Content = "hi" }] };
    var response = await client.GetResponseAsync(request);
    Console.WriteLine($"    (before={filter.Before} after={filter.After} touched={filter.Touched} model={response.Model})");
    return filter.Before == 1 && filter.After == 1 && filter.Touched && response.Model == "fake";
});

// 35. 流式聚合与过滤器回调
RunAsync("35.流式聚合与过滤器回调", async () =>
{
    var filter = new StreamingFilter();
    var client = new ChatClientBuilder(new StreamingChatClient()).UseFilters(filter).Build();
    var request = new ChatRequest { Model = "stream-model", Messages = [new ChatMessage { Role = "user", Content = "hi" }] };

    var text = new StringBuilder();
    await foreach (var chunk in client.GetStreamingResponseAsync(request))
    {
        var delta = chunk.Messages?.FirstOrDefault()?.Delta?.Content as String;
        if (!String.IsNullOrEmpty(delta)) text.Append(delta);
    }

    var completed = await filter.WaitAsync(TimeSpan.FromSeconds(5));
    var summary = filter.Context?.Response;
    Console.WriteLine($"    (text={text} callback={completed} model={summary?.Model} total={summary?.Usage?.TotalTokens})");
    return completed && text.ToString() == "hello world" && summary?.Text == "hello world" && summary.Usage?.TotalTokens == 5;
});

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

sealed class CountingFilter : IChatFilter
{
    public Int32 Before { get; private set; }
    public Int32 After { get; private set; }
    public Boolean Touched { get; private set; }

    public async Task OnChatAsync(ChatFilterContext context, Func<ChatFilterContext, CancellationToken, Task> next, CancellationToken cancellationToken = default)
    {
        Before++;
        await next(context, cancellationToken);
        After++;
        Touched = context.Response != null;
    }

    public Task OnStreamCompletedAsync(ChatFilterContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed class StreamingFilter : IChatFilter
{
    private readonly TaskCompletionSource<Boolean> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ChatFilterContext? Context { get; private set; }

    public Task OnChatAsync(ChatFilterContext context, Func<ChatFilterContext, CancellationToken, Task> next, CancellationToken cancellationToken = default) => next(context, cancellationToken);

    public Task OnStreamCompletedAsync(ChatFilterContext context, CancellationToken cancellationToken = default)
    {
        Context = context;
        _completed.TrySetResult(true);
        return Task.CompletedTask;
    }

    public Task<Boolean> WaitAsync(TimeSpan timeout) => _completed.Task.WaitAsync(timeout);
}

sealed class StreamingChatClient : IChatClient
{
    public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<IChatResponse>(new ChatResponse { Model = request.Model });

    public async IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var head = new ChatResponse { Model = "stream-model" };
        head.AddDelta("hello ");
        yield return head;
        await Task.Yield();

        var body = new ChatResponse();
        body.AddDelta("world");
        yield return body;

        var tail = new ChatResponse { Usage = new UsageDetails { InputTokens = 3, OutputTokens = 2, TotalTokens = 5 } };
        tail.AddDelta(null, null, FinishReason.Stop);
        yield return tail;
    }

    public void Dispose() { }
}
