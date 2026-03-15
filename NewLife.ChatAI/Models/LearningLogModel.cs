using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>学习日志。记录自学习分析任务的执行历史及结果</summary>
public partial class LearningLogModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>触发会话</summary>
    public Int64 ConversationId { get; set; }

    /// <summary>触发原因。Feedback=反馈触发/Scheduled=定时触发/Manual=手动触发</summary>
    public String TriggerReason { get; set; }

    /// <summary>提取记忆数。本次分析新增/更新的记忆数量</summary>
    public Int32 ExtractedCount { get; set; }

    /// <summary>处理反馈数。本次处理的反馈条数</summary>
    public Int32 FeedbackCount { get; set; }

    /// <summary>状态。Success=成功/Failed=失败/Skipped=跳过</summary>
    public String Status { get; set; }

    /// <summary>耗时毫秒</summary>
    public Int32 ElapsedMs { get; set; }

    /// <summary>备注。错误信息或跳过原因</summary>
    public String Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>创建地址</summary>
    public String CreateIP { get; set; }
    #endregion
}
