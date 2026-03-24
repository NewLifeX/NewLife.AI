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
    /// <returns>工具定义列表，供注入 <c>ChatCompletionRequest.Tools</c></returns>
    IList<ChatTool> GetTools();

    /// <summary>按名称调用工具并返回文本结果</summary>
    /// <param name="toolName">工具名称（与 <see cref="GetTools"/> 返回的 Function.Name 一致）</param>
    /// <param name="argumentsJson">参数 JSON 字符串（模型返回的 tool_call.arguments 原文）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>工具执行结果文本，供追加 tool 角色消息回传模型；工具未找到时抛 <see cref="KeyNotFoundException"/></returns>
    Task<String> CallToolAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken = default);
}
