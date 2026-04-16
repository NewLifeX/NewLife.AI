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

namespace NewLife.ChatData.Entity;

/// <summary>模型配置。后端接入的大语言模型，关联到具体的提供商实例</summary>
[Serializable]
[DataObject]
[Description("模型配置。后端接入的大语言模型，关联到具体的提供商实例")]
[BindIndex("IU_ModelConfig_ProviderId_Code", true, "ProviderId,Code")]
[BindTable("ModelConfig", Description = "模型配置。后端接入的大语言模型，关联到具体的提供商实例", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class ModelConfig : IModelConfig, IEntity<IModelConfig>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _ProviderId;
    /// <summary>提供商。关联的提供商实例ID</summary>
    [DisplayName("提供商")]
    [Description("提供商。关联的提供商实例ID")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProviderId", "提供商。关联的提供商实例ID", "")]
    public Int32 ProviderId { get => _ProviderId; set { if (OnPropertyChanging("ProviderId", value)) { _ProviderId = value; OnPropertyChanged("ProviderId"); } } }

    private String? _Code;
    /// <summary>编码。模型唯一标识</summary>
    [DisplayName("编码")]
    [Description("编码。模型唯一标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "编码。模型唯一标识", "")]
    public String? Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String? _Name;
    /// <summary>名称。显示名称</summary>
    [DisplayName("名称")]
    [Description("名称。显示名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。显示名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _ContextLength;
    /// <summary>上下文长度。模型支持的上下文窗口大小（令牌数）</summary>
    [DisplayName("上下文长度")]
    [Description("上下文长度。模型支持的上下文窗口大小（令牌数）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ContextLength", "上下文长度。模型支持的上下文窗口大小（令牌数）", "")]
    public Int32 ContextLength { get => _ContextLength; set { if (OnPropertyChanging("ContextLength", value)) { _ContextLength = value; OnPropertyChanged("ContextLength"); } } }

    private Boolean _SupportThinking;
    /// <summary>思考。是否支持思考模式</summary>
    [DisplayName("思考")]
    [Description("思考。是否支持思考模式")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SupportThinking", "思考。是否支持思考模式", "")]
    public Boolean SupportThinking { get => _SupportThinking; set { if (OnPropertyChanging("SupportThinking", value)) { _SupportThinking = value; OnPropertyChanged("SupportThinking"); } } }

    private Boolean _SupportFunctionCalling;
    /// <summary>函数调用。是否支持Function Calling</summary>
    [DisplayName("函数调用")]
    [Description("函数调用。是否支持Function Calling")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SupportFunctionCalling", "函数调用。是否支持Function Calling", "")]
    public Boolean SupportFunctionCalling { get => _SupportFunctionCalling; set { if (OnPropertyChanging("SupportFunctionCalling", value)) { _SupportFunctionCalling = value; OnPropertyChanged("SupportFunctionCalling"); } } }

    private Boolean _SupportVision;
    /// <summary>视觉。是否支持图片输入</summary>
    [DisplayName("视觉")]
    [Description("视觉。是否支持图片输入")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SupportVision", "视觉。是否支持图片输入", "")]
    public Boolean SupportVision { get => _SupportVision; set { if (OnPropertyChanging("SupportVision", value)) { _SupportVision = value; OnPropertyChanged("SupportVision"); } } }

    private Boolean _SupportAudio;
    /// <summary>音频。是否支持音频输入输出</summary>
    [DisplayName("音频")]
    [Description("音频。是否支持音频输入输出")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SupportAudio", "音频。是否支持音频输入输出", "")]
    public Boolean SupportAudio { get => _SupportAudio; set { if (OnPropertyChanging("SupportAudio", value)) { _SupportAudio = value; OnPropertyChanged("SupportAudio"); } } }

    private Boolean _SupportImageGeneration;
    /// <summary>图像。是否支持文生图</summary>
    [DisplayName("图像")]
    [Description("图像。是否支持文生图")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SupportImageGeneration", "图像。是否支持文生图", "")]
    public Boolean SupportImageGeneration { get => _SupportImageGeneration; set { if (OnPropertyChanging("SupportImageGeneration", value)) { _SupportImageGeneration = value; OnPropertyChanged("SupportImageGeneration"); } } }

    private Boolean _SupportVideoGeneration;
    /// <summary>视频生成。是否支持文生视频</summary>
    [DisplayName("视频生成")]
    [Description("视频生成。是否支持文生视频")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SupportVideoGeneration", "视频生成。是否支持文生视频", "")]
    public Boolean SupportVideoGeneration { get => _SupportVideoGeneration; set { if (OnPropertyChanging("SupportVideoGeneration", value)) { _SupportVideoGeneration = value; OnPropertyChanged("SupportVideoGeneration"); } } }

    private String? _SystemPrompt;
    /// <summary>系统提示词。模型级System Prompt，发送给上游的系统消息</summary>
    [DisplayName("系统提示词")]
    [Description("系统提示词。模型级System Prompt，发送给上游的系统消息")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("SystemPrompt", "系统提示词。模型级System Prompt，发送给上游的系统消息", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? SystemPrompt { get => _SystemPrompt; set { if (OnPropertyChanging("SystemPrompt", value)) { _SystemPrompt = value; OnPropertyChanged("SystemPrompt"); } } }

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

    private DateTime _ModelTime;
    /// <summary>模型时间。提供商侧的模型创建或最后更新时间</summary>
    [DisplayName("模型时间")]
    [Description("模型时间。提供商侧的模型创建或最后更新时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ModelTime", "模型时间。提供商侧的模型创建或最后更新时间", "")]
    public DateTime ModelTime { get => _ModelTime; set { if (OnPropertyChanging("ModelTime", value)) { _ModelTime = value; OnPropertyChanged("ModelTime"); } } }

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

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IModelConfig model)
    {
        Id = model.Id;
        ProviderId = model.ProviderId;
        Code = model.Code;
        Name = model.Name;
        ContextLength = model.ContextLength;
        SupportThinking = model.SupportThinking;
        SupportFunctionCalling = model.SupportFunctionCalling;
        SupportVision = model.SupportVision;
        SupportAudio = model.SupportAudio;
        SupportImageGeneration = model.SupportImageGeneration;
        SupportVideoGeneration = model.SupportVideoGeneration;
        SystemPrompt = model.SystemPrompt;
        RoleIds = model.RoleIds;
        DepartmentIds = model.DepartmentIds;
        ModelTime = model.ModelTime;
        Enable = model.Enable;
        Sort = model.Sort;
        Remark = model.Remark;
    }
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
            "ProviderId" => _ProviderId,
            "Code" => _Code,
            "Name" => _Name,
            "ContextLength" => _ContextLength,
            "SupportThinking" => _SupportThinking,
            "SupportFunctionCalling" => _SupportFunctionCalling,
            "SupportVision" => _SupportVision,
            "SupportAudio" => _SupportAudio,
            "SupportImageGeneration" => _SupportImageGeneration,
            "SupportVideoGeneration" => _SupportVideoGeneration,
            "SystemPrompt" => _SystemPrompt,
            "RoleIds" => _RoleIds,
            "DepartmentIds" => _DepartmentIds,
            "ModelTime" => _ModelTime,
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
                case "ProviderId": _ProviderId = value.ToInt(); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "ContextLength": _ContextLength = value.ToInt(); break;
                case "SupportThinking": _SupportThinking = value.ToBoolean(); break;
                case "SupportFunctionCalling": _SupportFunctionCalling = value.ToBoolean(); break;
                case "SupportVision": _SupportVision = value.ToBoolean(); break;
                case "SupportAudio": _SupportAudio = value.ToBoolean(); break;
                case "SupportImageGeneration": _SupportImageGeneration = value.ToBoolean(); break;
                case "SupportVideoGeneration": _SupportVideoGeneration = value.ToBoolean(); break;
                case "SystemPrompt": _SystemPrompt = Convert.ToString(value); break;
                case "RoleIds": _RoleIds = Convert.ToString(value); break;
                case "DepartmentIds": _DepartmentIds = Convert.ToString(value); break;
                case "ModelTime": _ModelTime = value.ToDateTime(); break;
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
    /// <summary>提供商</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public ProviderConfig? Provider => Extends.Get(nameof(Provider), k => ProviderConfig.FindById(ProviderId));

    /// <summary>提供商</summary>
    [Map(nameof(ProviderId), typeof(ProviderConfig), "Id")]
    public String? ProviderName => Provider?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ModelConfig? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据提供商、编码查找</summary>
    /// <param name="providerId">提供商</param>
    /// <param name="code">编码</param>
    /// <returns>实体对象</returns>
    public static ModelConfig? FindByProviderIdAndCode(Int32 providerId, String? code)
    {
        if (providerId < 0) return null;
        if (code == null) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.ProviderId == providerId && e.Code.EqualIgnoreCase(code));

        return Find(_.ProviderId == providerId & _.Code == code);
    }

    /// <summary>根据提供商查找</summary>
    /// <param name="providerId">提供商</param>
    /// <returns>实体列表</returns>
    public static IList<ModelConfig> FindAllByProviderId(Int32 providerId)
    {
        if (providerId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ProviderId == providerId);

        return FindAll(_.ProviderId == providerId);
    }
    #endregion

    #region 字段名
    /// <summary>取得模型配置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>提供商。关联的提供商实例ID</summary>
        public static readonly Field ProviderId = FindByName("ProviderId");

        /// <summary>编码。模型唯一标识</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>名称。显示名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>上下文长度。模型支持的上下文窗口大小（令牌数）</summary>
        public static readonly Field ContextLength = FindByName("ContextLength");

        /// <summary>思考。是否支持思考模式</summary>
        public static readonly Field SupportThinking = FindByName("SupportThinking");

        /// <summary>函数调用。是否支持Function Calling</summary>
        public static readonly Field SupportFunctionCalling = FindByName("SupportFunctionCalling");

        /// <summary>视觉。是否支持图片输入</summary>
        public static readonly Field SupportVision = FindByName("SupportVision");

        /// <summary>音频。是否支持音频输入输出</summary>
        public static readonly Field SupportAudio = FindByName("SupportAudio");

        /// <summary>图像。是否支持文生图</summary>
        public static readonly Field SupportImageGeneration = FindByName("SupportImageGeneration");

        /// <summary>视频生成。是否支持文生视频</summary>
        public static readonly Field SupportVideoGeneration = FindByName("SupportVideoGeneration");

        /// <summary>系统提示词。模型级System Prompt，发送给上游的系统消息</summary>
        public static readonly Field SystemPrompt = FindByName("SystemPrompt");

        /// <summary>角色组。逗号分隔的角色ID列表，为空时不限制</summary>
        public static readonly Field RoleIds = FindByName("RoleIds");

        /// <summary>部门组。逗号分隔的部门ID列表，为空时不限制</summary>
        public static readonly Field DepartmentIds = FindByName("DepartmentIds");

        /// <summary>模型时间。提供商侧的模型创建或最后更新时间</summary>
        public static readonly Field ModelTime = FindByName("ModelTime");

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

    /// <summary>取得模型配置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>提供商。关联的提供商实例ID</summary>
        public const String ProviderId = "ProviderId";

        /// <summary>编码。模型唯一标识</summary>
        public const String Code = "Code";

        /// <summary>名称。显示名称</summary>
        public const String Name = "Name";

        /// <summary>上下文长度。模型支持的上下文窗口大小（令牌数）</summary>
        public const String ContextLength = "ContextLength";

        /// <summary>思考。是否支持思考模式</summary>
        public const String SupportThinking = "SupportThinking";

        /// <summary>函数调用。是否支持Function Calling</summary>
        public const String SupportFunctionCalling = "SupportFunctionCalling";

        /// <summary>视觉。是否支持图片输入</summary>
        public const String SupportVision = "SupportVision";

        /// <summary>音频。是否支持音频输入输出</summary>
        public const String SupportAudio = "SupportAudio";

        /// <summary>图像。是否支持文生图</summary>
        public const String SupportImageGeneration = "SupportImageGeneration";

        /// <summary>视频生成。是否支持文生视频</summary>
        public const String SupportVideoGeneration = "SupportVideoGeneration";

        /// <summary>系统提示词。模型级System Prompt，发送给上游的系统消息</summary>
        public const String SystemPrompt = "SystemPrompt";

        /// <summary>角色组。逗号分隔的角色ID列表，为空时不限制</summary>
        public const String RoleIds = "RoleIds";

        /// <summary>部门组。逗号分隔的部门ID列表，为空时不限制</summary>
        public const String DepartmentIds = "DepartmentIds";

        /// <summary>模型时间。提供商侧的模型创建或最后更新时间</summary>
        public const String ModelTime = "ModelTime";

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
