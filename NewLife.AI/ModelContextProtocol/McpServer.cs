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

    /// <summary>处理MCP请求</summary>
    public JsonRpcResponse Process(JsonRpcRequest request, McpContext context)
    {
        if (request == null) throw new ApiException(ApiCode.BadRequest, "异常请求！");

        // A-34：校验 JSON-RPC 版本（原未校验，协议漂移时静默返回错误格式）
        if (request.JsonRpc != "2.0")
            return new("2.0", null, new JsonRpcError(ApiCode.BadRequest, "不支持的 JSON-RPC 版本，仅支持 2.0"), request.Id);

        try
        {
            Object? result = null;
            result = request.Method switch
            {
                "initialize" => OnInitialize(context, request),
                "notifications/initialized" => null,   // JSON-RPC notification 无 Id，不得响应
                "tools/list" => OnToolList(context, request),
                "tools/call" => OnToolCall(context, request, context.Services),
                _ => throw new ApiException(ApiCode.NotFound, $"Method '{request.Method}' not found in MCP server capabilities."),
            };
            if (result is JsonRpcResponse response) return response;

            // notification（无 Id）不响应；仅对普通请求返回结果
            if (request.Id == null) return null!;

            return new("2.0", result, null, request.Id);
        }
        catch (Exception ex)
        {
            var code = ApiCode.InternalServerError;
            if (ex is ApiException apiEx)
                code = apiEx.Code;
            else if (ex is ArgumentException)
                code = ApiCode.BadRequest;

            WriteLog("MCP 处理 {0} 失败：{1}", request.Method, ex.Message);
            return new("2.0", null, new JsonRpcError(code, ex.Message), request.Id);
        }
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
            new ServerCapabilities(new { listChanged = true }),
            new ClientInfo("PureAspNetCoreMcpServer", "1.0.0")
        );
    }
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

        var ps = JsonHelper.Convert<ToolCallParams>(request.Params);
        if (ps == null) throw new ArgumentOutOfRangeException(nameof(request.Params), "Tool call parameters are invalid.");

        //var tool = Tools.FirstOrDefault(t => t.Name.Equals(ps.Name, StringComparison.OrdinalIgnoreCase));
        //if (tool == null) throw new ArgumentOutOfRangeException(nameof(request.Params), $"Tool '{ps.Name}' not found in the server capabilities.");

        var result = _handler.Execute(null!, ps.Name, ps.Arguments, null!, serviceProvider);

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

    #region 辅助
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
    #endregion
}
