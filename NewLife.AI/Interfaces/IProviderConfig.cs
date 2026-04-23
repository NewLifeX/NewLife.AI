using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.AI.Interfaces;

/// <summary>提供商配置。AI服务商的连接信息，一个协议类型可以有多个实例</summary>
public partial interface IProviderConfig
{
    #region 属性
    /// <summary>编号</summary>
    Int32 Id { get; set; }

    /// <summary>编码。提供商实例唯一标识，如my-openai</summary>
    String? Code { get; set; }

    /// <summary>名称。显示名称，如公司OpenAI账号</summary>
    String? Name { get; set; }

    /// <summary>实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider</summary>
    String? Provider { get; set; }

    /// <summary>接口地址。API地址</summary>
    String? Endpoint { get; set; }

    /// <summary>密钥。API访问密钥</summary>
    String? ApiKey { get; set; }

    /// <summary>API协议。ChatCompletions/ResponseApi/AnthropicMessages/Gemini</summary>
    String? ApiProtocol { get; set; }

    /// <summary>模型过滤。逗号分隔的模型前缀或关键词，为空时发现全部。如：qwen-plus,qwen-max,deepseek</summary>
    String? ModelFilter { get; set; }

    /// <summary>发现上限。单次模型发现的最大数量，0表示不限制，默认10</summary>
    Int32 ModelLimit { get; set; }

    /// <summary>角色组。逗号分隔的角色ID列表，为空时不限制</summary>
    String? RoleIds { get; set; }

    /// <summary>部门组。逗号分隔的部门ID列表，为空时不限制</summary>
    String? DepartmentIds { get; set; }

    /// <summary>启用</summary>
    Boolean Enable { get; set; }

    /// <summary>排序。越大越靠前</summary>
    Int32 Sort { get; set; }

    /// <summary>备注</summary>
    String? Remark { get; set; }
    #endregion
}
