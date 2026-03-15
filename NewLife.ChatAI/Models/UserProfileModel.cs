using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>用户画像。对用户记忆的汇总分析结果，每人一条</summary>
public partial class UserProfileModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>总结。AI生成的用户综合描述</summary>
    public String Summary { get; set; }

    /// <summary>偏好。json格式，存储用户偏好键值对</summary>
    public String Preferences { get; set; }

    /// <summary>习惯。json格式，存储行为习惯</summary>
    public String Habits { get; set; }

    /// <summary>兴趣。json格式，存储兴趣爱好列表</summary>
    public String Interests { get; set; }

    /// <summary>记忆数量。关联的有效记忆碎片总数</summary>
    public Int32 MemoryCount { get; set; }

    /// <summary>最后分析时间</summary>
    public DateTime LastAnalyzeTime { get; set; }

    /// <summary>分析次数。累计执行自学习分析的次数</summary>
    public Int32 AnalyzeCount { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion
}
