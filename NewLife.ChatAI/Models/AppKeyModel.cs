using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace NewLife.ChatAI.Entity;

/// <summary>应用密钥。API网关访问凭证，用于外部系统调用模型服务</summary>
public partial class AppKeyModel
{
    #region 属性
    /// <summary>编号</summary>
    public Int32 Id { get; set; }

    /// <summary>用户。密钥所属用户</summary>
    public Int32 UserId { get; set; }

    /// <summary>名称。用户自定义标识，如业务系统A</summary>
    public String Name { get; set; }

    /// <summary>密钥。sk-前缀的随机字符串，创建时仅展示一次</summary>
    public String Secret { get; set; }

    /// <summary>启用</summary>
    public Boolean Enable { get; set; }

    /// <summary>过期时间。null表示永不过期</summary>
    public DateTime ExpireTime { get; set; }

    /// <summary>最后调用时间</summary>
    public DateTime LastCallTime { get; set; }

    /// <summary>调用次数。累计API请求数</summary>
    public Int64 Calls { get; set; }

    /// <summary>总Token数。累计消耗Token</summary>
    public Int64 TotalTokens { get; set; }

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
    public void Copy(AppKeyModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        Name = model.Name;
        Secret = model.Secret;
        Enable = model.Enable;
        ExpireTime = model.ExpireTime;
        LastCallTime = model.LastCallTime;
        Calls = model.Calls;
        TotalTokens = model.TotalTokens;
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
