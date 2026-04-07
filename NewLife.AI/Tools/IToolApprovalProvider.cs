namespace NewLife.AI.Tools;

/// <summary>工具审批提供者。在工具执行前拦截并请求用户确认（如桌面端弹窗审批）</summary>
/// <remarks>
/// 可选挂入 <see cref="ToolChatClient"/>，未设置时所有工具直接执行。
/// 典型实现：StarWing 的 WinForm 弹窗审批、Web 端 SSE 暂停审批等。
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
