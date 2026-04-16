using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>模型配置。后端接入的大语言模型，关联到具体的提供商实例</summary>
public partial interface IModelConfig
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>提供商。关联的提供商实例ID</summary>
    Int32 ProviderId { get; set; }

    /// <summary>编码。模型唯一标识</summary>
    String? Code { get; set; }

    /// <summary>名称。显示名称</summary>
    String? Name { get; set; }

    /// <summary>上下文长度。模型支持的上下文窗口大小（令牌数）</summary>
    Int32 ContextLength { get; set; }

    /// <summary>思考。是否支持思考模式</summary>
    Boolean SupportThinking { get; set; }

    /// <summary>函数调用。是否支持Function Calling</summary>
    Boolean SupportFunctionCalling { get; set; }

    /// <summary>视觉。是否支持图片输入</summary>
    Boolean SupportVision { get; set; }

    /// <summary>音频。是否支持音频输入输出</summary>
    Boolean SupportAudio { get; set; }

    /// <summary>图像。是否支持文生图</summary>
    Boolean SupportImageGeneration { get; set; }

    /// <summary>视频生成。是否支持文生视频</summary>
    Boolean SupportVideoGeneration { get; set; }

    /// <summary>系统提示词。模型级System Prompt，发送给上游的系统消息</summary>
    String? SystemPrompt { get; set; }

    /// <summary>角色组。逗号分隔的角色ID列表，为空时不限制</summary>
    String? RoleIds { get; set; }

    /// <summary>部门组。逗号分隔的部门ID列表，为空时不限制</summary>
    String? DepartmentIds { get; set; }

    /// <summary>模型时间。提供商侧的模型创建或最后更新时间</summary>
    DateTime ModelTime { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
