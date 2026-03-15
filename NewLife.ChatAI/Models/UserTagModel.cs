using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>用户标签。附加在用户画像上的特征标签，支持权重排序</summary>
public partial class UserTagModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>标签名称</summary>
    public String Name { get; set; }

    /// <summary>分类。preference=偏好/habit=习惯/interest=兴趣/background=背景</summary>
    public String Category { get; set; }

    /// <summary>权重。0~100，越高越重要，用于排序显示</summary>
    public Int32 Weight { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion
}
