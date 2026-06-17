using NewLife.AI.Models;

namespace NewLife.AI.Clients;

/// <summary>余额查询能力接口。查询当前账号/密钥的余额信息</summary>
/// <remarks>
/// 已实现：DeepSeek；其余按官方文档支持情况扩展。
/// </remarks>
public interface IBalanceClient
{
    /// <summary>查询账号余额</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>余额信息，查询失败时返回 null</returns>
    Task<BalanceResponse?> GetBalanceAsync(CancellationToken cancellationToken = default);
}
