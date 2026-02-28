using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>共享会话。通过链接分享的对话快照</summary>
public partial class SharedConversationModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>会话。被分享的会话</summary>
    public Int64 ConversationId { get; set; }

    /// <summary>分享令牌。唯一标识，用于生成分享URL</summary>
    public String ShareToken { get; set; }

    /// <summary>快照消息。截止到的最后一条消息编号</summary>
    public Int64 SnapshotMessageId { get; set; }

    /// <summary>创建者。分享发起用户</summary>
    public Int32 CreatorUserId { get; set; }

    /// <summary>过期时间。null表示永不过期</summary>
    public DateTime ExpireTime { get; set; }

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
    public void Copy(SharedConversationModel model)
    {
        Id = model.Id;
        ConversationId = model.ConversationId;
        ShareToken = model.ShareToken;
        SnapshotMessageId = model.SnapshotMessageId;
        CreatorUserId = model.CreatorUserId;
        ExpireTime = model.ExpireTime;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
    }
    #endregion
}
