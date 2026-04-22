using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>应用密钥。API网关访问凭证，用于外部系统调用模型服务</summary>
public partial interface IAppKey
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>用户。密钥所属用户</summary>
    Int32 UserId { get; set; }

    /// <summary>项目。所属项目，0=个人密鑰</summary>
    Int32 ProjectId { get; set; }

    /// <summary>名称。用户自定义标识，如业务系统A</summary>
    String? Name { get; set; }

    /// <summary>密钥。sk-前缀的随机字符串，创建时仅展示一次</summary>
    String? Secret { get; set; }

    /// <summary>可用模型。逗号分隔的模型名称或编码，为空时不限制</summary>
    String? Models { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>过期时间。null表示永不过期</summary>
    DateTime ExpireTime { get; set; }

    /// <summary>最后调用时间</summary>
    DateTime LastCallTime { get; set; }

    /// <summary>调用次数。累计API请求数</summary>
    Int64 Calls { get; set; }

    /// <summary>总Token数。累计消耗Token</summary>
    Int64 TotalTokens { get; set; }

    /// <summary>总费用。累计消耗费用，单位：元</summary>
    Decimal TotalCost { get; set; }

    /// <summary>日Token限额。每日Token使用上限，0表示不限制</summary>
    Int64 DailyTokenLimit { get; set; }

    /// <summary>月Token限额。每月Token使用上限，0表示不限制</summary>
    Int64 MonthlyTokenLimit { get; set; }

    /// <summary>总Token限额。永久累计Token上限，0表示不限制</summary>
    Int64 TotalTokenLimit { get; set; }

    /// <summary>日费用限额。每日费用上限，单位：元，0表示不限制</summary>
    Decimal DailyCostLimit { get; set; }

    /// <summary>月费用限额。每月费用上限，单位：元，0表示不限制</summary>
    Decimal MonthlyCostLimit { get; set; }

    /// <summary>总费用限额。永久累计费用上限，单位：元，0表示不限制</summary>
    Decimal TotalCostLimit { get; set; }

    /// <summary>分钟限流。每分钟请求上限，0表示不限制</summary>
    Int32 RateLimitPerMinute { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
