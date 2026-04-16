using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>消息反馈。用户对AI回复的点赞或点踩</summary>
public partial interface IMessageFeedback
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>会话。被反馈的会话</summary>
    Int64 ConversationId { get; set; }

    /// <summary>消息。被反馈的消息</summary>
    Int64 MessageId { get; set; }

    /// <summary>用户。反馈用户</summary>
    Int32 UserId { get; set; }

    /// <summary>反馈类型。Like=1点赞, Dislike=2点踩</summary>
    NewLife.AI.Models.FeedbackType FeedbackType { get; set; }

    /// <summary>原因。点踩原因</summary>
    String? Reason { get; set; }

    /// <summary>允许训练。是否允许用于模型学习训练</summary>
    Boolean AllowTraining { get; set; }
    #endregion
}
