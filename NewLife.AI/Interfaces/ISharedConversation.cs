using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.AI.Interfaces;

/// <summary>共享会话。通过链接分享的对话快照</summary>
public partial interface ISharedConversation
{
    #region 属性
    /// <summary>编号</summary>
    Int64 Id { get; set; }

    /// <summary>会话。被分享的会话</summary>
    Int64 ConversationId { get; set; }

    /// <summary>分享令牌。唯一标识，用于生成分享URL</summary>
    String? ShareToken { get; set; }

    /// <summary>快照标题</summary>
    String? SnapshotTitle { get; set; }

    /// <summary>快照消息。截止到的最后一条消息编号</summary>
    Int64 SnapshotMessageId { get; set; }

    /// <summary>过期时间。null表示永不过期</summary>
    DateTime ExpireTime { get; set; }
    #endregion
}
