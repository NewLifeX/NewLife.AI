using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据</summary>
public partial class UserMemoryModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>来源会话。提取该记忆的会话编号</summary>
    public Int64 ConversationId { get; set; }

    /// <summary>分类。preference=偏好/habit=习惯/interest=兴趣/background=背景</summary>
    public String Category { get; set; }

    /// <summary>主题。记忆的关键词/主题，如编程语言、工作行业</summary>
    public String Key { get; set; }

    /// <summary>内容。提取到的具体信息</summary>
    public String Value { get; set; }

    /// <summary>置信度。0~100，越高越可信</summary>
    public Int32 Confidence { get; set; }

    /// <summary>有效。是否仍然有效，可被覆盖或废弃</summary>
    public Boolean IsActive { get; set; }

    /// <summary>过期时间。null表示永不过期</summary>
    public DateTime ExpireTime { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion
}
