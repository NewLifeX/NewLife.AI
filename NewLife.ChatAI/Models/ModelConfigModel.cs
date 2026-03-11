using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>模型配置。后端接入的大语言模型，关联到具体的提供商实例</summary>
public partial class ModelConfigModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>提供商。关联的提供商实例ID</summary>
    public Int32 ProviderId { get; set; }

    /// <summary>编码。模型唯一标识</summary>
    public String Code { get; set; }

    /// <summary>名称。显示名称</summary>
    public String Name { get; set; }

    /// <summary>最大令牌数</summary>
    public Int32 MaxTokens { get; set; }

    /// <summary>思考。是否支持思考模式</summary>
    public Boolean SupportThinking { get; set; }

    /// <summary>视觉。是否支持图片输入</summary>
    public Boolean SupportVision { get; set; }

    /// <summary>图像。是否支持文生图</summary>
    public Boolean SupportImageGeneration { get; set; }

    /// <summary>函数调用。是否支持Function Calling</summary>
    public Boolean SupportFunctionCalling { get; set; }

    /// <summary>系统提示词。模型级System Prompt，发送给上游的系统消息</summary>
    public String SystemPrompt { get; set; }

    /// <summary>角色组。逗号分隔的角色ID列表，为空时不限制</summary>
    public String RoleIds { get; set; }

    /// <summary>部门组。逗号分隔的部门ID列表，为空时不限制</summary>
    public String DepartmentIds { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>排序。越小越靠前</summary>
    public Int32 Sort { get; set; }

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
    public void Copy(ModelConfigModel model)
    {
        Id = model.Id;
        ProviderId = model.ProviderId;
        Code = model.Code;
        Name = model.Name;
        MaxTokens = model.MaxTokens;
        SupportThinking = model.SupportThinking;
        SupportVision = model.SupportVision;
        SupportImageGeneration = model.SupportImageGeneration;
        SupportFunctionCalling = model.SupportFunctionCalling;
        SystemPrompt = model.SystemPrompt;
        RoleIds = model.RoleIds;
        DepartmentIds = model.DepartmentIds;
        Enable = model.Enable;
        Sort = model.Sort;
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
