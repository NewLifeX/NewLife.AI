using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>对话预设。保存模型+技能+SystemPrompt组合为预设模板</summary>
public partial interface IChatPreset
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>用户。0=系统级预设</summary>
    Int32 UserId { get; set; }

    /// <summary>名称。预设模板名称</summary>
    String? Name { get; set; }

    /// <summary>模型。关联的模型配置Id</summary>
    Int32 ModelId { get; set; }

    /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
    String? ModelName { get; set; }

    /// <summary>技能编码。关联的技能Code</summary>
    String? SkillCode { get; set; }

    /// <summary>系统提示词。预设的System Prompt</summary>
    String? SystemPrompt { get; set; }

    /// <summary>提示词。选中预设时自动填入用户输入框的引导文本</summary>
    String? Prompt { get; set; }

    /// <summary>思考模式。Auto=0, Think=1, Fast=2</summary>
    NewLife.AI.Models.ThinkingMode ThinkingMode { get; set; }

    /// <summary>默认预设。是否为用户的默认预设</summary>
    Boolean IsDefault { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }
    #endregion
}
