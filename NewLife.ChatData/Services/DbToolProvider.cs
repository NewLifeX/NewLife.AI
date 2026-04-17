using NewLife.AI.Models;
using NewLife.AI.Tools;
using NewLife.ChatData.Entity;
using NewLife.Serialization;

namespace NewLife.ChatData.Services;

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
    #region IToolProvider
    /// <summary>从 DB 读取已启用工具的定义列表</summary>
    /// <returns>工具定义列表；<see cref="IChatSetting.EnableFunctionCalling"/> 为 false 时返回空列表</returns>
    public IList<ChatTool> GetTools() => GetFilteredTools(null);

    /// <summary>根据 IsSystem 标志和指定工具名集合从 DB 读取启用工具</summary>
    /// <param name="selectedTools">消息中 @引用 的非系统工具名集合；null 表示仅返回系统工具</param>
    /// <returns>工具定义列表</returns>
    public IList<ChatTool> GetFilteredTools(ISet<String>? selectedTools)
    {
        if (!chatSetting.EnableFunctionCalling) return [];

        var tools = new List<ChatTool>();
        var dbTools = NativeTool.FindAllEnabled();
        foreach (var nt in dbTools)
        {
            if (nt.Name.IsNullOrEmpty()) continue;

            // 系统工具始终携带；非系统工具仅在 @引用 时携带
            if (!nt.IsSystem && (selectedTools == null || !selectedTools.Contains(nt.Name!))) continue;

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
        return tools;
    }

    /// <summary>通过 <see cref="ToolRegistry"/> 执行原生工具</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="argumentsJson">参数 JSON 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果文本；工具不在 Registry 中时抛 <see cref="KeyNotFoundException"/></returns>
    public Task<String> CallToolAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken = default)
        => registry.InvokeAsync(toolName, argumentsJson, cancellationToken);

    #endregion
}
