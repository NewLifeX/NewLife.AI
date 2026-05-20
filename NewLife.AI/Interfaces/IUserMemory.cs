using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.AI.Interfaces;

/// <summary>用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据</summary>
public partial interface IUserMemory
{
    #region 属性
    /// <summary>编号</summary>
    Int64 Id { get; set; }

    /// <summary>用户</summary>
    Int32 UserId { get; set; }

    /// <summary>来源会话。提取该记忆的会话编号</summary>
    Int64 ConversationId { get; set; }

    /// <summary>分类。偏好/习惯/兴趣/背景</summary>
    String? Category { get; set; }

    /// <summary>主题。记忆的关键词/主题，如编程语言、工作行业</summary>
    String? Key { get; set; }

    /// <summary>内容。提取到的具体信息</summary>
    String? Value { get; set; }

    /// <summary>置信度。0~100，越高越可信</summary>
    Int32 Confidence { get; set; }

    /// <summary>作用域。user=用户级/team=团队级/global=全局</summary>
    String? Scope { get; set; }

    /// <summary>状态。0=待审核/1=已生效/2=已拒绝/3=已废弃</summary>
    Int32 Status { get; set; }

    /// <summary>审核人</summary>
    Int32 ReviewUserId { get; set; }

    /// <summary>审核时间</summary>
    DateTime ReviewTime { get; set; }

    /// <summary>版本号。每次修改递增</summary>
    Int32 Version { get; set; }

    /// <summary>父记忆。融合来源，0表示原始提取</summary>
    Int64 ParentId { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>过期时间。null表示永不过期</summary>
    DateTime ExpireTime { get; set; }
    #endregion
}
