using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;

namespace NewLife.AI.ModelContextProtocol;

/// <summary>模型上下文协议服务器</summary>
/// <remarks>
/// 继承 <see cref="ApiHost"/> 以获得 IApiHost/ILogFeature/ITracerFeature/IExtend 基础设施，
/// 使 <see cref="ApiHandler.Host"/> 可绑定到本服务器，ApiHandler 才能从 Host 解析出 <see cref="IApiManager"/> 完成工具调用。
/// </remarks>
public class McpServer : ApiHost, IServiceProvider
{
    #region 属性
    /// <summary>接口动作管理器</summary>
    public IApiManager Manager { get; }

    /// <summary>服务提供者</summary>
    public IServiceProvider ServiceProvider { get; set; } = null!;

    private IApiHandler _handler;

    /// <summary>资源集合。uri → 资源定义与读取器</summary>
    private readonly Dictionary<String, McpResource> _resources = [];

    /// <summary>提示词集合。name → 提示词定义与处理器</summary>
    private readonly Dictionary<String, McpPrompt> _prompts = [];
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public McpServer()
    {
        Name = "Mcp";

        // 使用 JSON 编码器，与 ApiServer 初始化方式一致
        Encoder = new JsonEncoder { JsonHost = JsonHelper.Default };

        Manager = new McpToolManager(this);

        // 将处理器绑定到本服务器，使 ApiHandler 能从 Host 解析到 IApiManager（否则 tools/call 必然 NotFound）
        _handler = new McpHandler { Host = this };
    }
    #endregion

    #region 方法
    /// <summary>添加工具</summary>
    /// <typeparam name="TTools"></typeparam>
    /// <param name="serviceProvider"></param>
    public void AddTool<TTools>(IServiceProvider serviceProvider) where TTools : class
    {
        Manager.Register<TTools>();
    }

    /// <summary>添加工具类型。其公共方法将作为 MCP 工具暴露（snake_case 命名）</summary>
    /// <param name="type">工具类型</param>
    public void AddTool(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        // 基类 Register(object, string) 传 Type 会被当作实例注册（AddSingleton(Type)）导致类型转换异常；
        // 走泛型 Register<T>() 等价路径：controller=null，仅注册类型供按需创建
        var method = typeof(IApiManager).GetMethod(nameof(IApiManager.Register), Type.EmptyTypes);
        method!.MakeGenericMethod(type).Invoke(Manager, null);
    }

    /// <summary>添加资源</summary>
    /// <param name="uri">资源标识，如 knowledge://articles/123</param>
    /// <param name="name">资源名称</param>
    /// <param name="description">资源描述</param>
    /// <param name="mimeType">MIME 类型，默认 text/plain</param>
    /// <param name="read">资源读取委托。接收 uri，返回内容文本或对象</param>
    public void AddResource(String uri, String name, String? description = null, String? mimeType = null, Func<String?, Object?>? read = null)
    {
        _resources[uri] = new McpResource(new ResourceDefinition(uri, name, description, mimeType ?? "text/plain"), read);
    }

    /// <summary>添加提示词</summary>
    /// <param name="name">提示词名称</param>
    /// <param name="description">提示词描述</param>
    /// <param name="arguments">参数定义</param>
    /// <param name="get">提示词获取委托。接收参数字典，返回文本或 <see cref="IList{PromptMessage}"/></param>
    public void AddPrompt(String name, String? description = null, IList<PromptArgument>? arguments = null, Func<IDictionary<String, Object?>?, Object?>? get = null)
    {
        _prompts[name] = new McpPrompt(new PromptDefinition(name, description, arguments), get);
    }

    /// <summary>处理MCP请求</summary>
    public JsonRpcResponse Process(JsonRpcRequest request, McpContext context)
    {
        if (request == null) throw new ApiException(McpErrorCode.InvalidRequest, "异常请求！");

        // A-34：校验 JSON-RPC 版本（原未校验，协议漂移时静默返回错误格式）
        if (request.JsonRpc != "2.0")
            return new("2.0", null, new JsonRpcError(McpErrorCode.InvalidRequest, "不支持的 JSON-RPC 版本，仅支持 2.0"), request.Id);

        try
        {
            Object? result = null;
            result = request.Method switch
            {
                "initialize" => OnInitialize(context, request),
                "notifications/initialized" => null,   // JSON-RPC notification 无 Id，不得响应
                "ping" => OnPing(),
                "tools/list" => OnToolList(context, request),
                "tools/call" => OnToolCall(context, request, context.Services),
                "resources/list" => OnResourceList(context, request),
                "resources/read" => OnResourceRead(context, request),
                "prompts/list" => OnPromptList(context, request),
                "prompts/get" => OnPromptGet(context, request),
                "notifications/cancelled" => null,      // JSON-RPC notification，无需响应
                _ => throw new ApiException(McpErrorCode.MethodNotFound, $"Method '{request.Method}' not found in MCP server capabilities."),
            };
            if (result is JsonRpcResponse response) return response;

            // notification（无 Id）不响应；仅对普通请求返回结果
            if (request.Id == null) return null!;

            return new("2.0", result, null, request.Id);
        }
        catch (Exception ex)
        {
            // 错误码对齐 MCP/JSON-RPC 规范：参数/资源/方法相关异常映射为协议错误码，其余为内部错误
            var code = McpErrorCode.InternalError;
            if (ex is ApiException apiEx)
                code = MapToMcpErrorCode(apiEx.Code);
            else if (ex is ArgumentException or KeyNotFoundException or InvalidCastException or FormatException)
                code = McpErrorCode.InvalidParams;

            WriteLog("MCP 处理 {0} 失败：{1}", request.Method, ex.Message);
            return new("2.0", null, new JsonRpcError(code, ex.Message), request.Id);
        }
    }

    /// <summary>映射错误码为 MCP/JSON-RPC 规范错误码。底层 ApiHandler 抛出的 ApiException 使用 HTTP 风格码（400/404/500），此处映射；本服务器抛出的 MCP 错误码（负值）直接透传</summary>
    /// <param name="code">原始错误码</param>
    /// <returns>MCP 规范错误码</returns>
    private static Int32 MapToMcpErrorCode(Int32 code)
    {
        // 已使用 MCP 错误码（负值）直接透传
        if (code < 0) return code;

        return code switch
        {
            ApiCode.BadRequest => McpErrorCode.InvalidParams,
            ApiCode.Unauthorized or ApiCode.Forbidden => McpErrorCode.InvalidRequest,
            ApiCode.NotFound => McpErrorCode.MethodNotFound,
            _ => McpErrorCode.InternalError,
        };
    }
    #endregion

    #region 初始化
    /// <summary>初始化</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    protected virtual InitializeResult OnInitialize(McpContext context, JsonRpcRequest request)
    {
        var sessionId = context.GetRequest("Mcp-Session-Id");
        if (sessionId.IsNullOrEmpty())
        {
            // 如果没有提供 Session ID，则生成一个新的并回写响应头，客户端据此维持会话
            sessionId = Rand.NextString(16);
            context.SetResponse("Mcp-Session-Id", sessionId);
        }

        return new InitializeResult("2025-06-18",
            new ServerCapabilities(
                new { listChanged = true },
                new { subscribe = false, listChanged = false },
                new { listChanged = false }
            ),
            new ClientInfo("PureAspNetCoreMcpServer", "1.0.0")
        );
    }

    /// <summary>心跳。MCP ping 请求，返回空对象表示服务存活</summary>
    protected virtual Object OnPing() => new { };
    #endregion

    #region 工具
    /// <summary>工具列表</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    protected virtual ToolListResult OnToolList(McpContext context, JsonRpcRequest request)
    {
        SetSessionId(context);

        var list = new List<ToolDefinition>();
        foreach (var item in Manager.Services)
        {
            var api = item.Value;

            var schema = api["schema"];
            if ((schema == null))
            {
                Dictionary<String, Object> properties = [];
                List<String> required = [];
                foreach (var param in api.Method.GetParameters())
                {
                    if (param.ParameterType == typeof(IProgress<ProgressValue>)) continue;

                    properties[param.Name] = new { type = GetJsonType(param.ParameterType) };
                    if (!param.IsOptional) required.Add(param.Name!);
                }
                api["schema"] = schema = new { type = "object", properties, required };
            }

            var info = new ToolDefinition(api.Name, api.Method.GetDescription(), schema);
            list.Add(info);
        }

        return new(list);
    }

    /// <summary>工具调用</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    /// <param name="serviceProvider"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    protected virtual ToolCallResult OnToolCall(McpContext context, JsonRpcRequest request, IServiceProvider serviceProvider)
    {
        if (request.Params == null) throw new ArgumentNullException(nameof(request.Params), "Tool call parameters cannot be null.");

        SetSessionId(context);

        var ps = ConvertParams<ToolCallParams>(request.Params);
        if (ps == null) throw new ArgumentOutOfRangeException(nameof(request.Params), "Tool call parameters are invalid.");

        //var tool = Tools.FirstOrDefault(t => t.Name.Equals(ps.Name, StringComparison.OrdinalIgnoreCase));
        //if (tool == null) throw new ArgumentOutOfRangeException(nameof(request.Params), $"Tool '{ps.Name}' not found in the server capabilities.");

        // 提供非空 IApiSession：ApiHandler.Prepare 首行访问会话 Encoder，传 null 必然 NRE（此前工具调用从未真正成功过）
        var session = new McpSession(this, context.GetRequest("Mcp-Session-Id"));
        var result = _handler.Execute(session, ps.Name, ps.Arguments, null!, serviceProvider);

        List<ContentItem> content = [new("text", result?.ToString() ?? String.Empty)];
        return new(content);
    }

    private static void SetSessionId(McpContext context)
    {
        var sessionId = context.GetRequest("Mcp-Session-Id");
        if (sessionId != null)
        {
            context.SetResponse("Mcp-Session-Id", sessionId);
        }
    }
    #endregion

    #region 资源与提示词
    /// <summary>资源列表</summary>
    /// <param name="context">MCP 上下文</param>
    /// <param name="request">请求</param>
    /// <returns>资源列表结果</returns>
    protected virtual ResourceListResult OnResourceList(McpContext context, JsonRpcRequest request)
    {
        var list = _resources.Values.Select(e => e.Definition).ToList();
        return new ResourceListResult(list);
    }

    /// <summary>资源读取</summary>
    /// <param name="context">MCP 上下文</param>
    /// <param name="request">请求</param>
    /// <returns>资源读取结果</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    protected virtual ReadResourceResult OnResourceRead(McpContext context, JsonRpcRequest request)
    {
        if (request.Params == null) throw new ArgumentNullException(nameof(request.Params), "Resource read parameters cannot be null.");

        var ps = ConvertParams<ResourceReadParams>(request.Params);
        if (ps == null || ps.Uri.IsNullOrEmpty()) throw new ArgumentOutOfRangeException(nameof(request.Params), "Resource read parameters are invalid.");

        if (!_resources.TryGetValue(ps.Uri, out var res)) throw new ApiException(McpErrorCode.ResourceNotFound, $"Resource '{ps.Uri}' not found in the server capabilities.");

        var data = res.Read?.Invoke(ps.Uri) ?? String.Empty;
        return new ReadResourceResult([new ResourceContentItem(ps.Uri, data?.ToString() ?? String.Empty, res.Definition.MimeType)]);
    }

    /// <summary>提示词列表</summary>
    /// <param name="context">MCP 上下文</param>
    /// <param name="request">请求</param>
    /// <returns>提示词列表结果</returns>
    protected virtual PromptListResult OnPromptList(McpContext context, JsonRpcRequest request)
    {
        var list = _prompts.Values.Select(e => e.Definition).ToList();
        return new PromptListResult(list);
    }

    /// <summary>提示词获取</summary>
    /// <param name="context">MCP 上下文</param>
    /// <param name="request">请求</param>
    /// <returns>提示词获取结果</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    protected virtual GetPromptResult OnPromptGet(McpContext context, JsonRpcRequest request)
    {
        if (request.Params == null) throw new ArgumentNullException(nameof(request.Params), "Prompt get parameters cannot be null.");

        var ps = ConvertParams<PromptGetParams>(request.Params);
        if (ps == null || ps.Name.IsNullOrEmpty()) throw new ArgumentOutOfRangeException(nameof(request.Params), "Prompt get parameters are invalid.");

        // 官方 SDK：未知提示词名属于协议级无效参数（InvalidParams）
        if (!_prompts.TryGetValue(ps.Name, out var prompt)) throw new ApiException(McpErrorCode.InvalidParams, $"Prompt '{ps.Name}' not found in the server capabilities.");

        var content = prompt.Get?.Invoke(ps.Arguments) ?? String.Empty;
        if (content is IList<PromptMessage> messages) return new GetPromptResult(prompt.Definition.Description, messages);

        return new GetPromptResult(prompt.Definition.Description, [new PromptMessage("user", new ContentItem("text", content?.ToString() ?? String.Empty))]);
    }
    #endregion

    #region 辅助
    /// <summary>转换请求参数。兼容 NewLife JsonReader 解码的 Dictionary 与 System.Text.Json 反序列化产生的 JsonElement（其 ToString() 返回原始 JSON 文本）</summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="p">参数对象</param>
    /// <returns>转换结果</returns>
    private static T? ConvertParams<T>(Object? p) where T : class
    {
        if (p == null) return default;

        if (p is T t) return t;

        // JsonElement.ToString() 返回原始 JSON 文本；Dictionary.ToString() 返回类型名，不会命中此分支
        if (p is not System.Collections.IDictionary)
        {
            var json = p.ToString();
            if (!json.IsNullOrEmpty() && (json[0] == '{' || json[0] == '['))
                return json.ToJsonEntity<T>();
        }

        return JsonHelper.Convert<T>(p);
    }

    private static String GetJsonType(Type type) => Type.GetTypeCode(type) switch
    {
        TypeCode.String => "string",
        TypeCode.Int32 or TypeCode.Int64 or TypeCode.Int16 or TypeCode.UInt32 => "integer",
        TypeCode.Double or TypeCode.Single or TypeCode.Decimal => "number",
        TypeCode.Boolean => "boolean",
        _ => "object"
    };

    Object IServiceProvider.GetService(Type serviceType)
    {
        if (serviceType == typeof(McpServer)) return this;

        // 让 ApiHandler 能从 Host 解析到本服务器的工具管理器（A-27：此前返回 null 导致 tools/call 必然 NotFound）
        if (serviceType == typeof(IApiManager)) return Manager;

        return ServiceProvider?.GetService(serviceType)!;
    }

    /// <summary>资源定义与读取器</summary>
    /// <param name="definition">资源定义</param>
    /// <param name="read">读取委托</param>
    private sealed class McpResource(ResourceDefinition definition, Func<String?, Object?>? read)
    {
        /// <summary>资源定义</summary>
        public ResourceDefinition Definition { get; } = definition;

        /// <summary>读取委托</summary>
        public Func<String?, Object?>? Read { get; } = read;
    }

    /// <summary>提示词定义与处理器</summary>
    /// <param name="definition">提示词定义</param>
    /// <param name="get">获取委托</param>
    private sealed class McpPrompt(PromptDefinition definition, Func<IDictionary<String, Object?>?, Object?>? get)
    {
        /// <summary>提示词定义</summary>
        public PromptDefinition Definition { get; } = definition;

        /// <summary>获取委托</summary>
        public Func<IDictionary<String, Object?>?, Object?>? Get { get; } = get;
    }
    #endregion
}
