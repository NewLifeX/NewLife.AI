using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>会话。一次完整的多轮对话上下文</summary>
public partial class ConversationModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int64 Id { get; set; }

    /// <summary>用户。会话所属用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>标题。会话标题，显示在侧边栏</summary>
    public String Title { get; set; }

    /// <summary>模型编码。当前使用的模型</summary>
    public String ModelCode { get; set; }

    /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
    public Int32 ThinkingMode { get; set; }

    /// <summary>置顶。是否置顶显示</summary>
    public Boolean IsPinned { get; set; }

    /// <summary>消息数</summary>
    public Int32 MessageCount { get; set; }

    /// <summary>最后消息时间。用于排序</summary>
    public DateTime LastMessageTime { get; set; }

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

    /// <summary>备注</summary>
    public String Remark { get; set; }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ConversationModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        Title = model.Title;
        ModelCode = model.ModelCode;
        ThinkingMode = model.ThinkingMode;
        IsPinned = model.IsPinned;
        MessageCount = model.MessageCount;
        LastMessageTime = model.LastMessageTime;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
        Remark = model.Remark;
    }
    #endregion
}
