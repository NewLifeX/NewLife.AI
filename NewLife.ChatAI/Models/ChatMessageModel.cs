using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>对话消息。会话中的单条发言，包括用户消息和AI回复</summary>
public partial class ChatMessageModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>会话。所属会话</summary>
    public Int64 ConversationId { get; set; }

    /// <summary>角色。User=用户, Assistant=AI助手</summary>
    public String Role { get; set; }

    /// <summary>内容。Markdown格式文本</summary>
    public String Content { get; set; }

    /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
    public Int32 ThinkingMode { get; set; }

    /// <summary>父消息。编辑或重新生成时的分支引用</summary>
    public Int64 ParentMessageId { get; set; }

    /// <summary>附件列表。JSON格式</summary>
    public String Attachments { get; set; }

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
    public void Copy(ChatMessageModel model)
    {
        Id = model.Id;
        ConversationId = model.ConversationId;
        Role = model.Role;
        Content = model.Content;
        ThinkingMode = model.ThinkingMode;
        ParentMessageId = model.ParentMessageId;
        Attachments = model.Attachments;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
    }
    #endregion
}
