using System.Reflection;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;

namespace NewLife.AI.ModelContextProtocol;

/// <summary>模型上下文协议服务器</summary>
/// <remarks>
/// 自包含实现：不依赖 NewLife.Remoting 包。实现 <see cref="ILogFeature"/>/<see cref="ITracerFeature"/> 提供日志与追踪，
/// 通过 <see cref="McpToolManager"/> 完成工具注册、发现与调用（参数绑定/异步解包自实现），对外仅依赖 NewLife.Core。
/// </remarks>
public class McpServer : IServiceProvider, ILogFeature, ITracerFeature, IDisposable
{
    #region 属性
    /// <summary>名称</summary>
    public String Name { get; set; } = "Mcp";

    /// <summary>工具管理器</summary>
    public McpToolManager Manager { get; }

    /// <summary>服务提供者</summary>
    public IServiceProvider ServiceProvider { get; set; } = null!;

    /// <summary>日志。实现 <see cref="ILogFeature"/></summary>
    public ILog Log { get; set; } = Logger.Null;

    /// <summary>链路追踪。实现 <see cref="ITracerFeature"/></summary>
    public ITracer? Tracer { get; set; }

    /// <summary>资源集合。uri → 资源定义与读取器</summary>
    private readonly Dictionary<String, McpResource> _resources = [];

    /// <summary>提示词集合。name → 提示词定义与处理器</summary>
    private readonly Dictionary<String, McpPrompt> _prompts = [];

    /// <summary>写日志。带名称前缀，对齐原 ApiHost 行为</summary>
    /// <param name="format">格式化字符串</param>
    /// <param name="args">格式化参数</param>
    public void WriteLog(String format, params Object?[] args)
    {
        var name = Name;
        if (!name.IsNullOrEmpty()) format = $"[{name}]{format}";
        Log?.Info(format, args);
    }

    /// <summary>释放资源。基类无托管资源，子类宿主（如 HttpMcpServer 的 HttpServer）可重写释放</summary>
    public virtual void Dispose() { }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public McpServer()
    {
        Manager = new McpToolManager();
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

        Manager.Register(type);
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

            // JSON-RPC 2.0：notification（无 Id）异常不得响应（D10——原 catch 无条件回 error envelope 违反规范）
            if (request.Id == null) return null!;

            // 工具业务异常消息保留原样回传（客户端/LLM 需据错误文本修正调用；整体改 isError 结果语义见 D5 待办）
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
        foreach (var tool in Manager.Tools.Values)
        {
            var schema = tool.Schema;
            if (schema == null) schema = tool.Schema = BuildSchema(tool.Method);

            list.Add(new ToolDefinition(tool.Name, tool.Method.GetDescription(), schema));
        }

        return new(list);
    }

    /// <summary>构建工具输入 schema。对齐原 ApiManager 行为：基础设施参数（CancellationToken/IProgress）不暴露给客户端</summary>
    /// <param name="method">工具方法</param>
    /// <returns>JSON Schema 对象</returns>
    private static Object BuildSchema(MethodInfo method)
    {
        Dictionary<String, Object> properties = [];
        List<String> required = [];
        foreach (var param in method.GetParameters())
        {
            // IProgress / CancellationToken 等基础设施参数由框架注入，不进入工具 schema
            if (param.ParameterType == typeof(IProgress<ProgressValue>)) continue;
            if (param.ParameterType == typeof(CancellationToken)) continue;

            properties[param.Name!] = new { type = GetJsonType(param.ParameterType) };
            if (!param.IsOptional) required.Add(param.Name!);
        }

        return new { type = "object", properties, required };
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

        // 查找工具。未知工具名属于协议级无效参数（官方 SDK：tools/call 未知工具返回 InvalidParams）
        var tool = Manager.Find(ps.Name) ?? throw new ApiException(McpErrorCode.InvalidParams, $"Tool '{ps.Name}' not found in the server capabilities.");

        // 静态方法无需实例即可调用（Method.Invoke(null, args)）；实例方法需 DI/反射创建，失败返回 InternalError（D8）
        var instance = tool.Method.IsStatic ? null : tool.CreateInstance(serviceProvider);
        if (!tool.Method.IsStatic && instance == null)
            throw new ApiException(McpErrorCode.InternalError, $"无法创建工具 '{ps.Name}' 实例");
        var result = tool.Invoke(instance, ps.Arguments);

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

        // 让工具管理可从服务提供者解析（A-27 语义保留：此前返回 IApiManager）
        if (serviceType == typeof(McpToolManager)) return Manager;

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
