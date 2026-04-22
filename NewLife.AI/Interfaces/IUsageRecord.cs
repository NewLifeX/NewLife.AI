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

    /// <summary>项目。所属项目编号，0=个人</summary>
    Int32 ProjectId { get; set; }

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

    /// <summary>输入费用。单位：元</summary>
    Decimal InputCost { get; set; }

    /// <summary>输出费用。单位：元</summary>
    Decimal OutputCost { get; set; }

    /// <summary>总费用。单位：元</summary>
    Decimal TotalCost { get; set; }

    /// <summary>图片数量。本次生成图片数（多模态计费）</summary>
    Int32 ImageCount { get; set; }

    /// <summary>视频时长。本次生成视频秒数（多模态计费）</summary>
    Int32 VideoSeconds { get; set; }

    /// <summary>向量化条数。Embedding 调用的批量条数</summary>
    Int32 EmbeddingCount { get; set; }

    /// <summary>分辨率。如720P/1080P/4K，匹配ModelConfig.PriceTiers</summary>
    String? Resolution { get; set; }

    /// <summary>父消息。额外LLM调用（标题/压缩/记忆/知识等）关联到主对话消息编号，0表示主调用</summary>
    Int64 ParentMessageId { get; set; }

    /// <summary>耗时。毫秒</summary>
    Int32 ElapsedMs { get; set; }

    /// <summary>请求来源。Chat=主对话/Gateway=网关/Title=标题生成/Compact=上下文压缩/Memory=记忆提取/Knowledge=知识分析/Image=图片生成/Video=视频生成/Embedding=向量化</summary>
    String? Source { get; set; }
    #endregion
}
