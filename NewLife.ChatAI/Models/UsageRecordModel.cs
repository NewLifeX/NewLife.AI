using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计</summary>
public partial class UsageRecordModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>应用密钥。通过API网关调用时关联的AppKey</summary>
    public Int32 AppKeyId { get; set; }

    /// <summary>会话</summary>
    public Int64 ConversationId { get; set; }

    /// <summary>消息。对应的AI回复消息</summary>
    public Int64 MessageId { get; set; }

    /// <summary>模型编码</summary>
    public String ModelCode { get; set; }

    /// <summary>提示Token数</summary>
    public Int32 PromptTokens { get; set; }

    /// <summary>回复Token数</summary>
    public Int32 CompletionTokens { get; set; }

    /// <summary>总Token数</summary>
    public Int32 TotalTokens { get; set; }

    /// <summary>请求来源。Chat=对话/Gateway=网关</summary>
    public String Source { get; set; }

    /// <summary>链路追踪。方便问题排查</summary>
    public String TraceId { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>创建地址</summary>
    public String CreateIP { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(UsageRecordModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        AppKeyId = model.AppKeyId;
        ConversationId = model.ConversationId;
        MessageId = model.MessageId;
        ModelCode = model.ModelCode;
        PromptTokens = model.PromptTokens;
        CompletionTokens = model.CompletionTokens;
        TotalTokens = model.TotalTokens;
        Source = model.Source;
        TraceId = model.TraceId;
        CreateTime = model.CreateTime;
        CreateIP = model.CreateIP;
    }
    #endregion
}
