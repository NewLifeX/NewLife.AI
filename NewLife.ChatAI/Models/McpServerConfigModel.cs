using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>MCP服务配置。MCP Server列表及工具发现信息</summary>
public partial class McpServerConfigModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>名称。服务名称</summary>
    public String Name { get; set; }

    /// <summary>接口地址。MCP Server地址</summary>
    public String Endpoint { get; set; }

    /// <summary>传输类型。Http/Sse/Stdio</summary>
    public NewLife.AI.ChatAI.McpTransportType TransportType { get; set; }

    /// <summary>认证类型。None/Bearer/ApiKey</summary>
    public String AuthType { get; set; }

    /// <summary>认证令牌</summary>
    public String AuthToken { get; set; }

    /// <summary>可用工具。已发现的工具列表，JSON格式</summary>
    public String AvailableTools { get; set; }

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
    public void Copy(McpServerConfigModel model)
    {
        Id = model.Id;
        Name = model.Name;
        Endpoint = model.Endpoint;
        TransportType = model.TransportType;
        AuthType = model.AuthType;
        AuthToken = model.AuthToken;
        AvailableTools = model.AvailableTools;
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
