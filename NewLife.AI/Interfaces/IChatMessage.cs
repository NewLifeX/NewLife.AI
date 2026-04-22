using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>对话消息。会话中的单条发言，包括用户消息和AI回复</summary>
public partial interface IChatMessage
{
    #region 属性
    /// <summary>编号</summary>
    Int64 Id { get; set; }

    /// <summary>会话。所属会话</summary>
    Int64 ConversationId { get; set; }

    /// <summary>父消息。消息树父节点ID，0表示对话根节点。用于对话分叉时记录消息的层级关系</summary>
    Int64 ParentId { get; set; }

    /// <summary>分支序号。同一父节点下的分支序号，0=主线，1/2...=分叉支线</summary>
    Int32 BranchIndex { get; set; }

    /// <summary>角色。User=用户, Assistant=AI助手</summary>
    String? Role { get; set; }

    /// <summary>内容。Markdown格式文本</summary>
    String? Content { get; set; }

    /// <summary>思考内容。role=assistant时存AI推理过程；role=user时存本轮注入的系统上下文全文（含技能提示词与记忆注入，调试用）</summary>
    String? ThinkingContent { get; set; }

    /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
    NewLife.AI.Models.ThinkingMode ThinkingMode { get; set; }

    /// <summary>附件列表。存储魔方附件ID数组</summary>
    String? Attachments { get; set; }

    /// <summary>技能列表。本轮激活的技能名称，多个逗号分隔</summary>
    String? SkillNames { get; set; }

    /// <summary>工具列表。role=user时记录本轮可用工具名（逗号分隔）；role=assistant时记录实际调用的工具名（逗号分隔）</summary>
    String? ToolNames { get; set; }

    /// <summary>工具调用。role=assistant时存实际调用链路ToolCallDto[]（含入参Arguments和执行结果Result）</summary>
    String? ToolCalls { get; set; }

    /// <summary>模型名称。实际使用的模型编码，方便回溯</summary>
    String? ModelName { get; set; }

    /// <summary>最大Token数。本次请求的最大生成Token数限制</summary>
    Int32 MaxTokens { get; set; }

    /// <summary>温度。本次请求的采样温度参数</summary>
    Double Temperature { get; set; }

    /// <summary>完成原因。stop=正常结束/length=截断/tool_calls=工具调用/error=异常</summary>
    String? FinishReason { get; set; }

    /// <summary>输入Token数</summary>
    Int32 InputTokens { get; set; }

    /// <summary>输出Token数</summary>
    Int32 OutputTokens { get; set; }

    /// <summary>总Token数</summary>
    Int32 TotalTokens { get; set; }

    /// <summary>总费用。本条消息消耗费用，单位：元</summary>
    Decimal TotalCost { get; set; }

    /// <summary>耗时。毫秒</summary>
    Int32 ElapsedMs { get; set; }
    #endregion
}
