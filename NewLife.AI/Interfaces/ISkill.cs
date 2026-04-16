using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

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

    /// <summary>触发词。逗号分隔的关键词列表，用户消息包含任一词时自动激活该技能，如：翻译,translate,帮我译</summary>
    String? Triggers { get; set; }

    /// <summary>延续提示词。逗号分隔，匹配时保持技能活跃，如：继续翻译,再翻一段</summary>
    String? ContinueHints { get; set; }

    /// <summary>退出提示词。逗号分隔，匹配时清除会话技能，如：不用翻译了,换个话题</summary>
    String? ExitHints { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>系统。是否系统内置，内置技能不可删除</summary>
    Boolean IsSystem { get; set; }

    /// <summary>版本。每次编辑自增</summary>
    Int32 Version { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
