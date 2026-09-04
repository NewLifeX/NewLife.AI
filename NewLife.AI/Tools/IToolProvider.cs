using NewLife.AI.Models;

namespace NewLife.AI.Tools;

/// <summary>工具提供者接口。抽象工具的发现与调用，<c>ToolChatClient</c> 支持注册多个提供者并按顺序依次尝试</summary>
/// <remarks>
/// 典型实现：<see cref="ToolRegistry"/>（原生 .NET 工具）、<c>DbToolProvider</c>（DB 开关 + Registry 路由）、<c>McpClientService</c>（MCP 协议）。<br/>
/// 工具未找到时应抛 <see cref="KeyNotFoundException"/>，<c>ToolChatClient</c> 将自动尝试下一个提供者。
/// </remarks>
public interface IToolProvider
{
    /// <summary>获取此提供者暴露的工具定义列表</summary>
    /// <param name="filterNames">
    /// 工具可见性过滤集合：<c>null</c> 不过滤，返回全量工具（目录展示/路由表构建/无过滤注入）；
    /// 非 <c>null</c>（含空集合）仅返回系统工具 + filterNames 指定工具（AI 请求注入场景，系统工具每次请求自动携带），空集合等价"仅系统工具"；
    /// 无系统工具概念的提供者（如 MCP）退化为仅返回 filterNames 匹配工具，空集合返回空
    /// </param>
    /// <returns>工具定义列表，供注入 <c>ChatCompletionRequest.Tools</c></returns>
    IList<ChatTool> GetTools(ISet<String>? filterNames = null);

    /// <summary>按名称调用工具并返回结构化结果</summary>
    /// <param name="toolName">工具名称（与 <see cref="GetTools"/> 返回的 Function.Name 一致）</param>
    /// <param name="arguments">参数 JSON 字符串（模型返回的 tool_call.arguments 原文）</param>
    /// <param name="context">工具调用上下文，含请求信息与当前工具调用 ID；不需要上下文的实现可忽略（null）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>结构化工具结果，含受众分流的 Contents 列表；工具未找到时抛 <see cref="KeyNotFoundException"/></returns>
    Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default);
}
