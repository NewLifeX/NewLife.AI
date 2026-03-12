using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>用户设置。用户的个性化配置</summary>
public partial class UserSettingModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>用户。设置所属用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>语言。zh-CN/zh-TW/en</summary>
    public String Language { get; set; }

    /// <summary>主题。light/dark/system</summary>
    public String Theme { get; set; }

    /// <summary>字体大小。14~20，默认16</summary>
    public Int32 FontSize { get; set; }

    /// <summary>发送快捷键。Enter或Ctrl+Enter</summary>
    public String SendShortcut { get; set; }

    /// <summary>默认模型。新会话的默认模型编码</summary>
    public String DefaultModel { get; set; }

    /// <summary>默认思考模式。Auto=0, Think=1, Fast=2</summary>
    public NewLife.AI.ChatAI.ThinkingMode DefaultThinkingMode { get; set; }

    /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
    public Int32 ContextRounds { get; set; }

    /// <summary>系统提示词。全局System Prompt</summary>
    public String SystemPrompt { get; set; }

    /// <summary>允许训练。是否允许反馈数据用于模型改进</summary>
    public Boolean AllowTraining { get; set; }

    /// <summary>启用MCP。是否启用MCP工具调用</summary>
    public Boolean McpEnabled { get; set; }

    /// <summary>默认技能。新会话的默认技能编码</summary>
    public String DefaultSkill { get; set; }

    /// <summary>流式速度。流式输出速度等级，1~5，默认3</summary>
    public Int32 StreamingSpeed { get; set; }

    /// <summary>创建用户</summary>
    public Int32 CreateUserID { get; set; }

    /// <summary>创建地址</summary>
    public String CreateIP { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>更新用户</summary>
    public Int32 UpdateUserID { get; set; }

    /// <summary>更新地址</summary>
    public String UpdateIP { get; set; }

    /// <summary>更新时间</summary>
    public DateTime UpdateTime { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(UserSettingModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        Language = model.Language;
        Theme = model.Theme;
        FontSize = model.FontSize;
        SendShortcut = model.SendShortcut;
        DefaultModel = model.DefaultModel;
        DefaultThinkingMode = model.DefaultThinkingMode;
        ContextRounds = model.ContextRounds;
        SystemPrompt = model.SystemPrompt;
        AllowTraining = model.AllowTraining;
        McpEnabled = model.McpEnabled;
        DefaultSkill = model.DefaultSkill;
        StreamingSpeed = model.StreamingSpeed;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
    }
    #endregion
}
