using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>会话。一次完整的多轮对话上下文</summary>
public partial interface IConversation
{
    #region 属性
    /// <summary>编号</summary>
    Int64 Id { get; set; }

    /// <summary>用户。会话所属用户</summary>
    Int32 UserId { get; set; }

    /// <summary>项目。所属项目编号，0=个人对话</summary>
    Int32 ProjectId { get; set; }

    /// <summary>用户名</summary>
    String? UserName { get; set; }

    /// <summary>标题。会话标题，显示在侧边栏</summary>
    String? Title { get; set; }

    /// <summary>模型。当前使用的模型Id，引用ModelConfig.Id</summary>
    Int32 ModelId { get; set; }

    /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
    String? ModelName { get; set; }

    /// <summary>技能。当前会话使用的技能，引用Skill.Id</summary>
    Int32 SkillId { get; set; }

    /// <summary>技能名称。冗余存储技能名称，方便历史数据检索</summary>
    String? SkillName { get; set; }

    /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
    NewLife.AI.Models.ThinkingMode ThinkingMode { get; set; }

    /// <summary>置顶。是否置顶显示</summary>
    Boolean IsPinned { get; set; }

    /// <summary>消息数</summary>
    Int32 MessageCount { get; set; }

    /// <summary>最后消息时间。用于排序</summary>
    DateTime LastMessageTime { get; set; }

    /// <summary>输入Token数。会话累计消耗的输入Token数（含额外LLM调用）</summary>
    Int32 InputTokens { get; set; }

    /// <summary>输出Token数。会话累计消耗的输出Token数（含额外LLM调用）</summary>
    Int32 OutputTokens { get; set; }

    /// <summary>总Token数。会话累计消耗的总Token数（含额外LLM调用）</summary>
    Int32 TotalTokens { get; set; }

    /// <summary>总费用。会话累计消耗的总费用，单位：元（含标题/压缩/记忆等额外LLM调用）</summary>
    Decimal TotalCost { get; set; }

    /// <summary>耗时。毫秒</summary>
    Int32 ElapsedMs { get; set; }

    /// <summary>来源。Web/Gateway/Channel等，标识对话入口</summary>
    String? Source { get; set; }

    /// <summary>分叉来源会话。从哪个会话分叉而来，0表示原始会话</summary>
    Int64 ForkConversationId { get; set; }

    /// <summary>分叉来源消息。从哪条消息分叉而来，0表示未分叉</summary>
    Int64 ForkMessageId { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
