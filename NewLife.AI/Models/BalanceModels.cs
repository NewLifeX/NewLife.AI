using NewLife.Serialization;

namespace NewLife.AI.Models;

/// <summary>查询余额响应</summary>
public class BalanceResponse
{
    /// <summary>当前账户是否有余额可供 API 调用</summary>
    public Boolean IsAvailable { get; set; }

    /// <summary>余额信息列表（按币种）</summary>
    public BalanceInfo[]? BalanceInfos { get; set; }
}

/// <summary>余额信息（按币种）</summary>
public class BalanceInfo
{
    /// <summary>币种。如 CNY、USD</summary>
    public String? Currency { get; set; }

    /// <summary>总余额</summary>
    public Decimal? TotalBalance { get; set; }

    /// <summary>赠送余额</summary>
    public Decimal? GrantedBalance { get; set; }

    /// <summary>充值余额</summary>
    public Decimal? ToppedUpBalance { get; set; }
}
