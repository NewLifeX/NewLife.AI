using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>用户技能。记录用户最近使用的技能，用于SkillBar展示排序</summary>
public partial class UserSkillModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>技能</summary>
    public Int32 SkillId { get; set; }

    /// <summary>最后使用时间。用于SkillBar按最近使用排序</summary>
    public DateTime LastUseTime { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(UserSkillModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        SkillId = model.SkillId;
        LastUseTime = model.LastUseTime;
        CreateTime = model.CreateTime;
        UpdateTime = model.UpdateTime;
    }
    #endregion
}
