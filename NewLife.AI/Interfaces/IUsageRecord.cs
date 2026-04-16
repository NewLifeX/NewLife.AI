using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计</summary>
public partial interface IUsageRecord
{
    #region 属性
    /// <summary>编号</summary>
    Int64 Id { get; set; }

    /// <summary>用户</summary>
    Int32 UserId { get; set; }

    /// <summary>应用密钥。通过API网关调用时关联的AppKey</summary>
    Int32 AppKeyId { get; set; }

    /// <summary>会话</summary>
    Int64 ConversationId { get; set; }

    /// <summary>消息。对应的AI回复消息</summary>
    Int64 MessageId { get; set; }

    /// <summary>模型。引用ModelConfig.Id</summary>
    Int32 ModelId { get; set; }

    /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
    String? ModelName { get; set; }

    /// <summary>输入Token数</summary>
    Int32 InputTokens { get; set; }

    /// <summary>输出Token数</summary>
    Int32 OutputTokens { get; set; }

    /// <summary>总Token数</summary>
    Int32 TotalTokens { get; set; }

    /// <summary>缓存输入Token数</summary>
    Int32 CachedInputTokens { get; set; }

    /// <summary>推理Token数</summary>
    Int32 ReasoningTokens { get; set; }

    /// <summary>音频输入Token数</summary>
    Int32 InputAudioTokens { get; set; }

    /// <summary>文本输入Token数</summary>
    Int32 InputTextTokens { get; set; }

    /// <summary>音频输出Token数</summary>
    Int32 OutputAudioTokens { get; set; }

    /// <summary>文本输出Token数</summary>
    Int32 OutputTextTokens { get; set; }

    /// <summary>耗时。毫秒</summary>
    Int32 ElapsedMs { get; set; }

    /// <summary>请求来源。Chat=对话/Gateway=网关</summary>
    String? Source { get; set; }
    #endregion
}
