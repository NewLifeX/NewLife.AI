using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>提供商配置。AI服务商的连接信息，一个协议类型可以有多个实例</summary>
public partial class ProviderConfigModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>编码。提供商实例唯一标识，如my-openai</summary>
    public String Code { get; set; }

    /// <summary>名称。显示名称，如公司OpenAI账号</summary>
    public String Name { get; set; }

    /// <summary>实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider</summary>
    public String Provider { get; set; }

    /// <summary>接口地址。API地址</summary>
    public String Endpoint { get; set; }

    /// <summary>密钥。API访问密钥</summary>
    public String ApiKey { get; set; }

    /// <summary>API协议。ChatCompletions/ResponseApi/AnthropicMessages/Gemini</summary>
    public String ApiProtocol { get; set; }

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
    public void Copy(ProviderConfigModel model)
    {
        Id = model.Id;
        Code = model.Code;
        Name = model.Name;
        Provider = model.Provider;
        Endpoint = model.Endpoint;
        ApiKey = model.ApiKey;
        ApiProtocol = model.ApiProtocol;
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
