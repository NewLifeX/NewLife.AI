namespace NewLife.AI.Tools;

/// <summary>工具权限档位。遵循代码强制原则，由 <see cref="IToolTierProvider.GetToolTier"/> 返回，不依赖提示词约束</summary>
public enum ToolApprovalTier
{
    /// <summary>低风险。代码层自动放行，不调用 <see cref="IToolApprovalProvider.RequestApprovalAsync"/></summary>
    Allow = 0,

    /// <summary>中等风险。调用 <see cref="IToolApprovalProvider.RequestApprovalAsync"/> 请求用户确认</summary>
    Ask = 1,

    /// <summary>高风险。代码层强制阻断，不调用 <see cref="IToolApprovalProvider.RequestApprovalAsync"/>，直接返回拒绝错误</summary>
    Deny = 2,
}

/// <summary>工具审批提供者。在工具执行前拦截并请求用户确认（如桌面端弹窗审批）</summary>
/// <remarks>
/// 可选挂入 <see cref="ToolChatClient"/>，未设置时所有工具直接执行。
/// 典型实现：StarWing 的 WinForm 弹窗审批、Web 端 SSE 暂停审批等。
/// 如需三档权限分类，额外实现 <see cref="IToolTierProvider"/>。
/// </remarks>
public interface IToolApprovalProvider
{
    /// <summary>请求用户审批工具调用</summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="argumentsJson">参数 JSON 字符串（模型原文）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审批结果</returns>
    Task<ToolApprovalResult> RequestApprovalAsync(String toolName, String? argumentsJson, CancellationToken cancellationToken = default);
}

/// <summary>可选接口：支持三档权限分类的工具审批提供者。与 <see cref="IToolApprovalProvider"/> 组合使用</summary>
/// <remarks>
/// 未实现此接口时，<see cref="ToolChatClient"/> 默认将所有工具视为 <see cref="ToolApprovalTier.Ask"/>。
/// </remarks>
public interface IToolTierProvider
{
    /// <summary>查询工具的权限档位</summary>
    /// <param name="toolName">工具名称</param>
    /// <returns>
    /// <see cref="ToolApprovalTier.Allow"/>：低风险，自动放行；
    /// <see cref="ToolApprovalTier.Ask"/>：中等风险，调用 <see cref="IToolApprovalProvider.RequestApprovalAsync"/> 确认；
    /// <see cref="ToolApprovalTier.Deny"/>：高风险，代码强制阻断
    /// </returns>
    ToolApprovalTier GetToolTier(String toolName);
}

/// <summary>工具审批提供者扩展方法</summary>
public static class ToolApprovalProviderExtensions
{
    /// <summary>查询工具权限档位。若 provider 同时实现 <see cref="IToolTierProvider"/> 则返回其值，否则返回 <see cref="ToolApprovalTier.Ask"/></summary>
    /// <param name="provider">审批提供者</param>
    /// <param name="toolName">工具名称</param>
    public static ToolApprovalTier GetToolTier(this IToolApprovalProvider provider, String toolName)
    {
        if (provider is IToolTierProvider tierProvider)
            return tierProvider.GetToolTier(toolName);
        return ToolApprovalTier.Ask;
    }
}

/// <summary>工具审批结果</summary>
public class ToolApprovalResult
{
    /// <summary>是否批准执行</summary>
    public Boolean Approved { get; set; }

    /// <summary>是否始终允许此工具（加入白名单，后续调用自动通过）</summary>
    public Boolean AlwaysAllow { get; set; }

    /// <summary>批准</summary>
    public static ToolApprovalResult Allow => new() { Approved = true };

    /// <summary>拒绝</summary>
    public static ToolApprovalResult Deny => new() { Approved = false };
}
