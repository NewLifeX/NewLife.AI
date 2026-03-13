using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>技能。可复用的AI行为指令，Markdown格式的结构化提示文本</summary>
public partial class SkillModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>编码。英文标识，唯一，如coder、translator</summary>
    public String Code { get; set; }

    /// <summary>名称。技能展示名称，同时也是@引用的标识</summary>
    public String Name { get; set; }

    /// <summary>图标。Material Icon名称，如code、translate</summary>
    public String Icon { get; set; }

    /// <summary>分类。通用/开发/创作/分析</summary>
    public String Category { get; set; }

    /// <summary>描述。一句话说明该技能做什么</summary>
    public String Description { get; set; }

    /// <summary>技能正文。Markdown格式，包含完整的行为指令、规则和示例</summary>
    public String Content { get; set; }

    /// <summary>排序。数值大的排前面</summary>
    public Int32 Sort { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>系统。是否系统内置，内置技能不可删除</summary>
    public Boolean IsSystem { get; set; }

    /// <summary>版本。每次编辑自增</summary>
    public Int32 Version { get; set; }

    /// <summary>创建者</summary>
    public String CreateUser { get; set; }

    /// <summary>创建用户</summary>
    public Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    public String CreateIP { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新者</summary>
    public String UpdateUser { get; set; }

    /// <summary>更新用户</summary>
    public Int32 UpdateUserID { get; set; }

    /// <summary>更新地址</summary>
    public String UpdateIP { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }

    /// <summary>备注</summary>
    public String Remark { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(SkillModel model)
    {
        Id = model.Id;
        Code = model.Code;
        Name = model.Name;
        Icon = model.Icon;
        Category = model.Category;
        Description = model.Description;
        Content = model.Content;
        Sort = model.Sort;
        Enable = model.Enable;
        IsSystem = model.IsSystem;
        Version = model.Version;
        CreateUser = model.CreateUser;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUser = model.UpdateUser;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
        Remark = model.Remark;
    }
    #endregion
}
