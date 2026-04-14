using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;

namespace NewLife.ChatAI.Entity;

/// <summary>提供商配置。AI服务商的连接信息，一个协议类型可以有多个实例</summary>
[Serializable]
[DataObject]
[Description("提供商配置。AI服务商的连接信息，一个协议类型可以有多个实例")]
[BindIndex("IU_ProviderConfig_Code", true, "Code")]
[BindIndex("IX_ProviderConfig_Provider", false, "Provider")]
[BindTable("ProviderConfig", Description = "提供商配置。AI服务商的连接信息，一个协议类型可以有多个实例", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class ProviderConfig
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Code;
    /// <summary>编码。提供商实例唯一标识，如my-openai</summary>
    [DisplayName("编码")]
    [Description("编码。提供商实例唯一标识，如my-openai")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "编码。提供商实例唯一标识，如my-openai", "")]
    public String? Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String? _Name;
    /// <summary>名称。显示名称，如公司OpenAI账号</summary>
    [DisplayName("名称")]
    [Description("名称。显示名称，如公司OpenAI账号")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。显示名称，如公司OpenAI账号", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Provider;
    /// <summary>实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider</summary>
    [DisplayName("实现类")]
    [Description("实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Provider", "实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider", "")]
    public String? Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private String? _Endpoint;
    /// <summary>接口地址。API地址</summary>
    [DisplayName("接口地址")]
    [Description("接口地址。API地址")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Endpoint", "接口地址。API地址", "", ItemType = "url")]
    public String? Endpoint { get => _Endpoint; set { if (OnPropertyChanging("Endpoint", value)) { _Endpoint = value; OnPropertyChanged("Endpoint"); } } }

    private String? _ApiKey;
    /// <summary>密钥。API访问密钥</summary>
    [DisplayName("密钥")]
    [Description("密钥。API访问密钥")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ApiKey", "密钥。API访问密钥", "", ShowIn = "Auto,-List,-Search")]
    public String? ApiKey { get => _ApiKey; set { if (OnPropertyChanging("ApiKey", value)) { _ApiKey = value; OnPropertyChanged("ApiKey"); } } }

    private String? _ApiProtocol;
    /// <summary>API协议。ChatCompletions/ResponseApi/AnthropicMessages/Gemini</summary>
    [DisplayName("API协议")]
    [Description("API协议。ChatCompletions/ResponseApi/AnthropicMessages/Gemini")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ApiProtocol", "API协议。ChatCompletions/ResponseApi/AnthropicMessages/Gemini", "")]
    public String? ApiProtocol { get => _ApiProtocol; set { if (OnPropertyChanging("ApiProtocol", value)) { _ApiProtocol = value; OnPropertyChanged("ApiProtocol"); } } }

    private String? _ModelFilter;
    /// <summary>模型过滤。逗号分隔的模型前缀或关键词，为空时发现全部。如：qwen-plus,qwen-max,deepseek</summary>
    [DisplayName("模型过滤")]
    [Description("模型过滤。逗号分隔的模型前缀或关键词，为空时发现全部。如：qwen-plus,qwen-max,deepseek")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ModelFilter", "模型过滤。逗号分隔的模型前缀或关键词，为空时发现全部。如：qwen-plus,qwen-max,deepseek", "", ShowIn = "Auto,-List")]
    public String? ModelFilter { get => _ModelFilter; set { if (OnPropertyChanging("ModelFilter", value)) { _ModelFilter = value; OnPropertyChanged("ModelFilter"); } } }

    private Int32 _ModelLimit;
    /// <summary>发现上限。单次模型发现的最大数量，0表示不限制，默认10</summary>
    [DisplayName("发现上限")]
    [Description("发现上限。单次模型发现的最大数量，0表示不限制，默认10")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ModelLimit", "发现上限。单次模型发现的最大数量，0表示不限制，默认10", "", DefaultValue = "10")]
    public Int32 ModelLimit { get => _ModelLimit; set { if (OnPropertyChanging("ModelLimit", value)) { _ModelLimit = value; OnPropertyChanged("ModelLimit"); } } }

    private String? _RoleIds;
    /// <summary>角色组。逗号分隔的角色ID列表，为空时不限制</summary>
    [DisplayName("角色组")]
    [Description("角色组。逗号分隔的角色ID列表，为空时不限制")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("RoleIds", "角色组。逗号分隔的角色ID列表，为空时不限制", "")]
    public String? RoleIds { get => _RoleIds; set { if (OnPropertyChanging("RoleIds", value)) { _RoleIds = value; OnPropertyChanged("RoleIds"); } } }

    private String? _DepartmentIds;
    /// <summary>部门组。逗号分隔的部门ID列表，为空时不限制</summary>
    [DisplayName("部门组")]
    [Description("部门组。逗号分隔的部门ID列表，为空时不限制")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DepartmentIds", "部门组。逗号分隔的部门ID列表，为空时不限制", "")]
    public String? DepartmentIds { get => _DepartmentIds; set { if (OnPropertyChanging("DepartmentIds", value)) { _DepartmentIds = value; OnPropertyChanged("DepartmentIds"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _Sort;
    /// <summary>排序。越大越靠前</summary>
    [DisplayName("排序")]
    [Description("排序。越大越靠前")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序。越大越靠前", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private Int32 _CreateUserID;
    /// <summary>创建用户</summary>
    [Category("扩展")]
    [DisplayName("创建用户")]
    [Description("创建用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserID", "创建用户", "")]
    public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

    private String? _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String? CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private Int32 _UpdateUserID;
    /// <summary>更新用户</summary>
    [Category("扩展")]
    [DisplayName("更新用户")]
    [Description("更新用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UpdateUserID", "更新用户", "")]
    public Int32 UpdateUserID { get => _UpdateUserID; set { if (OnPropertyChanging("UpdateUserID", value)) { _UpdateUserID = value; OnPropertyChanged("UpdateUserID"); } } }

    private String? _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String? UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String? _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String? Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Code" => _Code,
            "Name" => _Name,
            "Provider" => _Provider,
            "Endpoint" => _Endpoint,
            "ApiKey" => _ApiKey,
            "ApiProtocol" => _ApiProtocol,
            "ModelFilter" => _ModelFilter,
            "ModelLimit" => _ModelLimit,
            "RoleIds" => _RoleIds,
            "DepartmentIds" => _DepartmentIds,
            "Enable" => _Enable,
            "Sort" => _Sort,
            "CreateUserID" => _CreateUserID,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            "UpdateUserID" => _UpdateUserID,
            "UpdateIP" => _UpdateIP,
            "UpdateTime" => _UpdateTime,
            "Remark" => _Remark,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Provider": _Provider = Convert.ToString(value); break;
                case "Endpoint": _Endpoint = Convert.ToString(value); break;
                case "ApiKey": _ApiKey = Convert.ToString(value); break;
                case "ApiProtocol": _ApiProtocol = Convert.ToString(value); break;
                case "ModelFilter": _ModelFilter = Convert.ToString(value); break;
                case "ModelLimit": _ModelLimit = value.ToInt(); break;
                case "RoleIds": _RoleIds = Convert.ToString(value); break;
                case "DepartmentIds": _DepartmentIds = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                case "Remark": _Remark = Convert.ToString(value); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ProviderConfig? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据实现类查找</summary>
    /// <param name="provider">实现类</param>
    /// <returns>实体列表</returns>
    public static IList<ProviderConfig> FindAllByProvider(String? provider)
    {
        if (provider == null) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Provider.EqualIgnoreCase(provider));

        return FindAll(_.Provider == provider);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="code">编码。提供商实例唯一标识，如my-openai</param>
    /// <param name="provider">实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ProviderConfig> Search(String? code, String? provider, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!provider.IsNullOrEmpty()) exp &= _.Provider == provider;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得提供商配置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>编码。提供商实例唯一标识，如my-openai</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>名称。显示名称，如公司OpenAI账号</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider</summary>
        public static readonly Field Provider = FindByName("Provider");

        /// <summary>接口地址。API地址</summary>
        public static readonly Field Endpoint = FindByName("Endpoint");

        /// <summary>密钥。API访问密钥</summary>
        public static readonly Field ApiKey = FindByName("ApiKey");

        /// <summary>API协议。ChatCompletions/ResponseApi/AnthropicMessages/Gemini</summary>
        public static readonly Field ApiProtocol = FindByName("ApiProtocol");

        /// <summary>模型过滤。逗号分隔的模型前缀或关键词，为空时发现全部。如：qwen-plus,qwen-max,deepseek</summary>
        public static readonly Field ModelFilter = FindByName("ModelFilter");

        /// <summary>发现上限。单次模型发现的最大数量，0表示不限制，默认10</summary>
        public static readonly Field ModelLimit = FindByName("ModelLimit");

        /// <summary>角色组。逗号分隔的角色ID列表，为空时不限制</summary>
        public static readonly Field RoleIds = FindByName("RoleIds");

        /// <summary>部门组。逗号分隔的部门ID列表，为空时不限制</summary>
        public static readonly Field DepartmentIds = FindByName("DepartmentIds");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>排序。越大越靠前</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>创建用户</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新用户</summary>
        public static readonly Field UpdateUserID = FindByName("UpdateUserID");

        /// <summary>更新地址</summary>
        public static readonly Field UpdateIP = FindByName("UpdateIP");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        /// <summary>备注</summary>
        public static readonly Field Remark = FindByName("Remark");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得提供商配置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>编码。提供商实例唯一标识，如my-openai</summary>
        public const String Code = "Code";

        /// <summary>名称。显示名称，如公司OpenAI账号</summary>
        public const String Name = "Name";

        /// <summary>实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider</summary>
        public const String Provider = "Provider";

        /// <summary>接口地址。API地址</summary>
        public const String Endpoint = "Endpoint";

        /// <summary>密钥。API访问密钥</summary>
        public const String ApiKey = "ApiKey";

        /// <summary>API协议。ChatCompletions/ResponseApi/AnthropicMessages/Gemini</summary>
        public const String ApiProtocol = "ApiProtocol";

        /// <summary>模型过滤。逗号分隔的模型前缀或关键词，为空时发现全部。如：qwen-plus,qwen-max,deepseek</summary>
        public const String ModelFilter = "ModelFilter";

        /// <summary>发现上限。单次模型发现的最大数量，0表示不限制，默认10</summary>
        public const String ModelLimit = "ModelLimit";

        /// <summary>角色组。逗号分隔的角色ID列表，为空时不限制</summary>
        public const String RoleIds = "RoleIds";

        /// <summary>部门组。逗号分隔的部门ID列表，为空时不限制</summary>
        public const String DepartmentIds = "DepartmentIds";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>排序。越大越靠前</summary>
        public const String Sort = "Sort";

        /// <summary>创建用户</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新用户</summary>
        public const String UpdateUserID = "UpdateUserID";

        /// <summary>更新地址</summary>
        public const String UpdateIP = "UpdateIP";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";

        /// <summary>备注</summary>
        public const String Remark = "Remark";
    }
    #endregion
}
