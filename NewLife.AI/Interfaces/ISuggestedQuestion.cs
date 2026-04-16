using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>推荐问题。欢迎页展示的推荐问题，支持缓存响应以加速体验</summary>
public partial interface ISuggestedQuestion
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>问题。推荐问题文本</summary>
    String? Question { get; set; }

    /// <summary>响应。缓存的AI回复内容，Markdown格式</summary>
    String? Response { get; set; }

    /// <summary>推理响应。缓存的思考过程内容</summary>
    String? ThinkingResponse { get; set; }

    /// <summary>模型。生成缓存响应时使用的模型</summary>
    Int32 ModelId { get; set; }

    /// <summary>图标。Material Icon名称，如chat_bubble_outline</summary>
    String? Icon { get; set; }

    /// <summary>颜色。图标CSS颜色类，如text-blue-500</summary>
    String? Color { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }
    #endregion
}
