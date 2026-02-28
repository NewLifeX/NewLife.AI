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

/// <summary>模型配置。后端接入的大语言模型</summary>
[Serializable]
[DataObject]
[Description("模型配置。后端接入的大语言模型")]
[BindIndex("IU_ModelConfig_Code", true, "Code")]
[BindIndex("IX_ModelConfig_Provider", false, "Provider")]
[BindTable("ModelConfig", Description = "模型配置。后端接入的大语言模型", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class ModelConfig : IEntity<ModelConfigModel>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String _Code;
    /// <summary>编码。模型唯一标识</summary>
    [DisplayName("编码")]
    [Description("编码。模型唯一标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "编码。模型唯一标识", "")]
    public String Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String _Name;
    /// <summary>名称。显示名称</summary>
    [DisplayName("名称")]
    [Description("名称。显示名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。显示名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Provider;
    /// <summary>提供商。OpenAI、Alibaba、DeepSeek等</summary>
    [DisplayName("提供商")]
    [Description("提供商。OpenAI、Alibaba、DeepSeek等")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Provider", "提供商。OpenAI、Alibaba、DeepSeek等", "")]
    public String Provider { get => _Provider; set { if (OnPropertyChanging("Provider", value)) { _Provider = value; OnPropertyChanged("Provider"); } } }

    private String _Endpoint;
    /// <summary>接口地址。API地址</summary>
    [DisplayName("接口地址")]
    [Description("接口地址。API地址")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Endpoint", "接口地址。API地址", "", ShowIn = "Auto,-List,-Search")]
    public String Endpoint { get => _Endpoint; set { if (OnPropertyChanging("Endpoint", value)) { _Endpoint = value; OnPropertyChanged("Endpoint"); } } }

    private String _ApiKey;
    /// <summary>密钥。API访问密钥</summary>
    [DisplayName("密钥")]
    [Description("密钥。API访问密钥")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ApiKey", "密钥。API访问密钥", "", ShowIn = "Auto,-List,-Search")]
    public String ApiKey { get => _ApiKey; set { if (OnPropertyChanging("ApiKey", value)) { _ApiKey = value; OnPropertyChanged("ApiKey"); } } }

    private Int32 _MaxTokens;
    /// <summary>最大令牌数</summary>
    [DisplayName("最大令牌数")]
    [Description("最大令牌数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxTokens", "最大令牌数", "")]
    public Int32 MaxTokens { get => _MaxTokens; set { if (OnPropertyChanging("MaxTokens", value)) { _MaxTokens = value; OnPropertyChanged("MaxTokens"); } } }

    private Boolean _SupportThinking;
    /// <summary>支持思考。是否支持思考模式</summary>
    [DisplayName("支持思考")]
    [Description("支持思考。是否支持思考模式")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SupportThinking", "支持思考。是否支持思考模式", "")]
    public Boolean SupportThinking { get => _SupportThinking; set { if (OnPropertyChanging("SupportThinking", value)) { _SupportThinking = value; OnPropertyChanged("SupportThinking"); } } }

    private Boolean _SupportVision;
    /// <summary>支持视觉。是否支持图片输入</summary>
    [DisplayName("支持视觉")]
    [Description("支持视觉。是否支持图片输入")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SupportVision", "支持视觉。是否支持图片输入", "")]
    public Boolean SupportVision { get => _SupportVision; set { if (OnPropertyChanging("SupportVision", value)) { _SupportVision = value; OnPropertyChanged("SupportVision"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Int32 _Sort;
    /// <summary>排序。越小越靠前</summary>
    [DisplayName("排序")]
    [Description("排序。越小越靠前")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序。越小越靠前", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private Int32 _CreateUserID;
    /// <summary>创建用户</summary>
    [Category("扩展")]
    [DisplayName("创建用户")]
    [Description("创建用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserID", "创建用户", "")]
    public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

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

    private String _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

    private DateTime _UpdateTime;
    /// <summary>更新时间</summary>
    [Category("扩展")]
    [DisplayName("更新时间")]
    [Description("更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("UpdateTime", "更新时间", "")]
    public DateTime UpdateTime { get => _UpdateTime; set { if (OnPropertyChanging("UpdateTime", value)) { _UpdateTime = value; OnPropertyChanged("UpdateTime"); } } }

    private String _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ModelConfigModel model)
    {
        Id = model.Id;
        Code = model.Code;
        Name = model.Name;
        Provider = model.Provider;
        Endpoint = model.Endpoint;
        ApiKey = model.ApiKey;
        MaxTokens = model.MaxTokens;
        SupportThinking = model.SupportThinking;
        SupportVision = model.SupportVision;
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

    #region 获取/设置 字段值
    /// <summary>获取/设置 字段值</summary>
    /// <param name="name">字段名</param>
    /// <returns></returns>
    public override Object this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "Code" => _Code,
            "Name" => _Name,
            "Provider" => _Provider,
            "Endpoint" => _Endpoint,
            "ApiKey" => _ApiKey,
            "MaxTokens" => _MaxTokens,
            "SupportThinking" => _SupportThinking,
            "SupportVision" => _SupportVision,
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
                case "MaxTokens": _MaxTokens = value.ToInt(); break;
                case "SupportThinking": _SupportThinking = value.ToBoolean(); break;
                case "SupportVision": _SupportVision = value.ToBoolean(); break;
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
    public static ModelConfig FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据编码查找</summary>
    /// <param name="code">编码</param>
    /// <returns>实体对象</returns>
    public static ModelConfig FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        return Find(_.Code == code);
    }

    /// <summary>根据提供商查找</summary>
    /// <param name="provider">提供商</param>
    /// <returns>实体列表</returns>
    public static IList<ModelConfig> FindAllByProvider(String provider)
    {
        if (provider.IsNullOrEmpty()) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Provider.EqualIgnoreCase(provider));

        return FindAll(_.Provider == provider);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="code">编码。模型唯一标识</param>
    /// <param name="provider">提供商。OpenAI、Alibaba、DeepSeek等</param>
    /// <param name="supportThinking">支持思考。是否支持思考模式</param>
    /// <param name="supportVision">支持视觉。是否支持图片输入</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ModelConfig> Search(String code, String provider, Boolean? supportThinking, Boolean? supportVision, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!provider.IsNullOrEmpty()) exp &= _.Provider == provider;
        if (supportThinking != null) exp &= _.SupportThinking == supportThinking;
        if (supportVision != null) exp &= _.SupportVision == supportVision;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得模型配置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>编码。模型唯一标识</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>名称。显示名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>提供商。OpenAI、Alibaba、DeepSeek等</summary>
        public static readonly Field Provider = FindByName("Provider");

        /// <summary>接口地址。API地址</summary>
        public static readonly Field Endpoint = FindByName("Endpoint");

        /// <summary>密钥。API访问密钥</summary>
        public static readonly Field ApiKey = FindByName("ApiKey");

        /// <summary>最大令牌数</summary>
        public static readonly Field MaxTokens = FindByName("MaxTokens");

        /// <summary>支持思考。是否支持思考模式</summary>
        public static readonly Field SupportThinking = FindByName("SupportThinking");

        /// <summary>支持视觉。是否支持图片输入</summary>
        public static readonly Field SupportVision = FindByName("SupportVision");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>排序。越小越靠前</summary>
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

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得模型配置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>编码。模型唯一标识</summary>
        public const String Code = "Code";

        /// <summary>名称。显示名称</summary>
        public const String Name = "Name";

        /// <summary>提供商。OpenAI、Alibaba、DeepSeek等</summary>
        public const String Provider = "Provider";

        /// <summary>接口地址。API地址</summary>
        public const String Endpoint = "Endpoint";

        /// <summary>密钥。API访问密钥</summary>
        public const String ApiKey = "ApiKey";

        /// <summary>最大令牌数</summary>
        public const String MaxTokens = "MaxTokens";

        /// <summary>支持思考。是否支持思考模式</summary>
        public const String SupportThinking = "SupportThinking";

        /// <summary>支持视觉。是否支持图片输入</summary>
        public const String SupportVision = "SupportVision";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>排序。越小越靠前</summary>
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
