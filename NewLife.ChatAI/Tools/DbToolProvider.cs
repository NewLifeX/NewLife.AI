using NewLife.AI.Tools;
using NewLife.Serialization;

namespace NewLife.ChatAI.Tools;

/// <summary>DB 工具提供者。从 <see cref="NativeTool"/> 表读取已启用的工具定义，执行时通过 <see cref="ToolRegistry"/> 路由到原生 .NET 实现</summary>
/// <remarks>
/// 职责（单一）：工具定义的"可见性开关"。
/// <list type="bullet">
/// <item><description><b>GetTools</b>：从 DB 读取已启用工具的 schema，注入 AI 请求（运行时可热开关）</description></item>
/// <item><description><b>CallToolAsync</b>：委托 <see cref="ToolRegistry"/> 执行原生实现；工具未在 Registry 中时抛 <see cref="KeyNotFoundException"/>，由上层 <c>ToolChatClient</c> 继续尝试下一个提供者（如 <c>McpClientService</c>）</description></item>
/// </list>
/// MCP 工具由独立的 <c>McpClientService</c> 实现，不在本类负责范围内。
/// </remarks>
/// <remarks>初始化 DB 工具提供者</remarks>
/// <param name="registry">原生工具注册表（持有全部 .NET 工具实现）</param>
/// <param name="chatSetting">AI对话系统配置</param>
public class DbToolProvider(ToolRegistry registry, IChatSetting chatSetting) : IToolProvider
{
    #region 缓存
    private IList<ChatTool>? _toolsCache;
    private ISet<String>? _systemNamesCache;
    private Int64 _toolsCacheExpiry;
    /// <summary>全量工具列表缓存 TTL（毫秒）。NativeTool 实体缓存 60 s，此处在其上再缓存 ChatTool 构建结果</summary>
    private const Int64 ToolsCacheTtlMs = 30_000;
    #endregion

    #region IToolProvider
    /// <summary>从 DB 读取已启用工具的定义列表。filterNames 为 null 时返回全量（含系统工具与非系统工具），用于目录展示和路由表构建；
    /// 非 null 时返回系统工具 + filterNames 指定工具，用于 AI 请求注入</summary>
    /// <param name="filterNames">工具可见性过滤集合；null 全量，空集合仅系统工具，非空集合系统工具 + 指定工具</param>
    /// <param name="includeSystem">是否附带系统工具。false 时排除 IsSystem 工具</param>
    /// <returns>工具定义列表；<see cref="IChatSetting.EnableFunctionCalling"/> 为 false 时返回空列表</returns>
    public IList<ChatTool> GetTools(ISet<String>? filterNames = null, Boolean includeSystem = true)
    {
        if (!chatSetting.EnableFunctionCalling) return [];

        // 确保全量缓存有效（RefreshCache 同时建立系统工具名集合）
        var now = Runtime.TickCount64;
        if (_toolsCache == null || now >= _toolsCacheExpiry)
            RefreshCache();

        if (filterNames == null)
        {
            if (!includeSystem)
                return [.. _toolsCache!.Where(t => t.Function?.Name is { } name && !_systemNamesCache!.Contains(name))];
            return _toolsCache!;
        }

        // 过滤：系统工具（includeSystem 时）始终携带；非系统工具仅携带 filterNames 中引用的（从缓存中筛选，不再访问 DB）
        return [.. _toolsCache!.Where(t => t.Function?.Name is { } name &&
            ((includeSystem && _systemNamesCache!.Contains(name)) || filterNames.Contains(name)))];
    }

    /// <summary>刷新全量工具缓存与系统工具名集合缓存</summary>
    private void RefreshCache()
    {
        var tools = new List<ChatTool>();
        var sysNames = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        var dbTools = NativeTool.FindAllEnabled();
        foreach (var nt in dbTools)
        {
            if (nt.Name.IsNullOrEmpty()) continue;

            if (nt.IsSystem) sysNames.Add(nt.Name!);

            Object? parameters = null;
            if (!nt.Parameters.IsNullOrEmpty())
            {
                try { parameters = new JsonParser(nt.Parameters!).Decode(); }
                catch { parameters = null; }
            }

            tools.Add(new ChatTool
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = nt.Name!,
                    Description = nt.Description,
                    Parameters = parameters,
                },
            });
        }

        _toolsCache = tools;
        _systemNamesCache = sysNames;
        _toolsCacheExpiry = Runtime.TickCount64 + ToolsCacheTtlMs;
    }

    /// <summary>通过 <see cref="ToolRegistry"/> 执行原生工具</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="arguments">参数 JSON 字符串</param>
    /// <param name="context">调用上下文</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结构化工具结果；工具不在 Registry 中时抛 <see cref="KeyNotFoundException"/></returns>
    async Task<IToolResult> IToolProvider.CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
    {
        var result = await registry.InvokeAsync(toolName, arguments, context, cancellationToken).ConfigureAwait(false);
        return new ToolResult(result);
    }

    IList<ChatTool> IToolProvider.GetTools(ISet<String>? filterNames, Boolean includeSystem)
        => GetTools(filterNames, includeSystem);

    #endregion
}
