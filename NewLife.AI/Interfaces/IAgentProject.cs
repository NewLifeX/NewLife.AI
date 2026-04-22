using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>智能体项目。多用户协作的AI资源容器，统一管理对话、知识、技能、成员</summary>
public partial interface IAgentProject
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>所有者。项目创建人/所有者</summary>
    Int32 OwnerId { get; set; }

    /// <summary>编码。英文唯一标识，#引用用</summary>
    String? Code { get; set; }

    /// <summary>名称。项目显示名称</summary>
    String? Name { get; set; }

    /// <summary>图标。表情符号图标，最多2字符，如🚀📊💡</summary>
    String? Icon { get; set; }

    /// <summary>颜色。预设颜色：blue/green/purple/orange/red/yellow/gray</summary>
    String? Color { get; set; }

    /// <summary>描述。项目用途说明</summary>
    String? Description { get; set; }

    /// <summary>系统提示词。项目级AI行为指令，注入System Prompt最高优先级</summary>
    String? SystemPrompt { get; set; }

    /// <summary>记忆模式。0=共享全局记忆/1=项目独立隔离记忆</summary>
    Int32 MemoryMode { get; set; }

    /// <summary>默认模型。新会话的默认模型配置Id</summary>
    Int32 DefaultModel { get; set; }

    /// <summary>文档数。关联的知识文档总数</summary>
    Int32 DocumentCount { get; set; }

    /// <summary>文章数。清洗生成的Wiki文章总数</summary>
    Int32 ArticleCount { get; set; }

    /// <summary>总Token数。全部文章的Token累计 + 对话累计</summary>
    Int64 TotalTokens { get; set; }

    /// <summary>总费用。累计消耗费用，单位：元，由用量记录汇总</summary>
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

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>创建用户</summary>
    Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    String? CreateIP { get; set; }

    /// <summary>创建时间</summary>
    DateTime CreateTime { get; set; }

    /// <summary>更新用户</summary>
    Int32 UpdateUserID { get; set; }

    /// <summary>更新地址</summary>
    String? UpdateIP { get; set; }

    /// <summary>更新时间</summary>
    DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
