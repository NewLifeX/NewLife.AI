using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>技能。可复用的AI行为指令，Markdown格式的结构化提示文本</summary>
public partial interface ISkill
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>编码。英文标识，唯一，如coder、translator</summary>
    String? Code { get; set; }

    /// <summary>名称。技能展示名称，同时也是@引用的标识</summary>
    String? Name { get; set; }

    /// <summary>图标。Material Icon名称，如code、translate</summary>
    String? Icon { get; set; }

    /// <summary>分类。通用/开发/创作/分析</summary>
    String? Category { get; set; }

    /// <summary>描述。一句话说明该技能做什么</summary>
    String? Description { get; set; }

    /// <summary>技能正文。Markdown格式，包含完整的行为指令、规则和示例</summary>
    String? Content { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>系统。是否系统内置，内置技能不可删除</summary>
    Boolean IsSystem { get; set; }

    /// <summary>版本。每次编辑自增</summary>
    Int32 Version { get; set; }

    /// <summary>创建者</summary>
    String? CreateUser { get; set; }

    /// <summary>更新者</summary>
    String? UpdateUser { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
