using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>用户设置。用户的个性化配置</summary>
public partial interface IUserSetting
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>用户。设置所属用户</summary>
    Int32 UserId { get; set; }

    /// <summary>语言。zh-CN/zh-TW/en</summary>
    String? Language { get; set; }

    /// <summary>主题。light/dark/system</summary>
    String? Theme { get; set; }

    /// <summary>字体大小。14~20，默认16</summary>
    Int32 FontSize { get; set; }

    /// <summary>发送快捷键。Enter或Ctrl+Enter</summary>
    String? SendShortcut { get; set; }

    /// <summary>默认模型。新会话的默认模型配置Id</summary>
    Int32 DefaultModel { get; set; }

    /// <summary>默认思考模式。Auto=0, Think=1, Fast=2</summary>
    NewLife.AI.Models.ThinkingMode DefaultThinkingMode { get; set; }

    /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
    Int32 ContextRounds { get; set; }

    /// <summary>AI称呼。你希望AI怎么称呼你</summary>
    String? Nickname { get; set; }

    /// <summary>用户背景。你希望AI了解你的哪些信息，如职业、专长、偏好等</summary>
    String? UserBackground { get; set; }

    /// <summary>回应风格。AI回复的风格偏好。Balanced=0, Precise=1, Vivid=2, Creative=3</summary>
    NewLife.AI.Models.ResponseStyle ResponseStyle { get; set; }

    /// <summary>系统提示词。全局System Prompt</summary>
    String? SystemPrompt { get; set; }

    /// <summary>允许训练。是否允许反馈数据用于模型改进</summary>
    Boolean AllowTraining { get; set; }

    /// <summary>启用MCP。是否启用MCP工具调用</summary>
    Boolean McpEnabled { get; set; }

    /// <summary>显示工具调用。是否在对话中显示工具调用的入参和出参详情</summary>
    Boolean ShowToolCalls { get; set; }

    /// <summary>默认技能。新会话的默认技能编码</summary>
    String? DefaultSkill { get; set; }

    /// <summary>流式速度。流式输出速度等级，1~5，默认3</summary>
    Int32 StreamingSpeed { get; set; }

    /// <summary>启用个人学习。用户级自学习开关，全局开关开启后此项生效</summary>
    Boolean EnableLearning { get; set; }

    /// <summary>学习模型。用户自选的记忆提取模型，为空则使用系统配置</summary>
    String? LearningModel { get; set; }

    /// <summary>记忆注入条数。用户自定义每次对话注入的记忆上限，0 表示使用系统配置</summary>
    Int32 MemoryInjectNum { get; set; }

    /// <summary>内容区宽度。标准960/宽屏1200/自适应0</summary>
    Int32 ContentWidth { get; set; }
    #endregion
}
