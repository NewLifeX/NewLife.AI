using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>消息反馈。用户对AI回复的点赞或点踩</summary>
public partial class MessageFeedbackModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>消息。被反馈的消息</summary>
    public Int64 MessageId { get; set; }

    /// <summary>用户。反馈用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>反馈类型。Like=1点赞, Dislike=2点踩</summary>
    public Int32 FeedbackType { get; set; }

    /// <summary>原因。点踩原因</summary>
    public String Reason { get; set; }

    /// <summary>允许训练。是否允许用于模型学习训练</summary>
    public Boolean AllowTraining { get; set; }

    /// <summary>创建用户</summary>
    public Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    public String CreateIP { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新用户</summary>
    public Int32 UpdateUserID { get; set; }

    /// <summary>更新地址</summary>
    public String UpdateIP { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(MessageFeedbackModel model)
    {
        Id = model.Id;
        MessageId = model.MessageId;
        UserId = model.UserId;
        FeedbackType = model.FeedbackType;
        Reason = model.Reason;
        AllowTraining = model.AllowTraining;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
    }
    #endregion
}
