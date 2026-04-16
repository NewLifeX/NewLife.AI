using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatData.Entity;

/// <summary>MCP服务配置。MCP Server列表及工具发现信息</summary>
public partial interface IMcpServerConfig
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>名称。服务名称</summary>
    String? Name { get; set; }

    /// <summary>接口地址。MCP Server地址</summary>
    String? Endpoint { get; set; }

    /// <summary>传输类型。Http/Sse/Stdio</summary>
    NewLife.AI.Models.McpTransportType TransportType { get; set; }

    /// <summary>认证类型。None/Bearer/ApiKey</summary>
    String? AuthType { get; set; }

    /// <summary>认证令牌</summary>
    String? AuthToken { get; set; }

    /// <summary>可用工具。已发现的工具列表，JSON格式</summary>
    String? AvailableTools { get; set; }

    /// <summary>触发词。逗号分隔的关键词列表，消息包含任一词时自动加载该服务下的MCP工具；为空表示每轮默认可用</summary>
    String? Triggers { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
