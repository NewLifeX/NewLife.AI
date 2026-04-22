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

/// <summary>对话预设。保存模型+技能+SystemPrompt组合为预设模板</summary>
[Serializable]
[DataObject]
[Description("对话预设。保存模型+技能+SystemPrompt组合为预设模板")]
[BindIndex("IU_ChatPreset_UserId_ProjectId_Name", true, "UserId,ProjectId,Name")]
[BindTable("ChatPreset", Description = "对话预设。保存模型+技能+SystemPrompt组合为预设模板", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class ChatPreset : IChatPreset, IEntity<IChatPreset>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _UserId;
    /// <summary>用户。0=系统级预设</summary>
    [DisplayName("用户")]
    [Description("用户。0=系统级预设")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户。0=系统级预设", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private Int32 _ProjectId;
    /// <summary>项目。所属项目，0=个人/系统预设</summary>
    [DisplayName("项目")]
    [Description("项目。所属项目，0=个人/系统预设")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目。所属项目，0=个人/系统预设", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String? _Name;
    /// <summary>名称。预设模板名称</summary>
    [DisplayName("名称")]
    [Description("名称。预设模板名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "名称。预设模板名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private Int32 _ModelId;
    /// <summary>模型。关联的模型配置Id</summary>
    [DisplayName("模型")]
    [Description("模型。关联的模型配置Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ModelId", "模型。关联的模型配置Id", "")]
    public Int32 ModelId { get => _ModelId; set { if (OnPropertyChanging("ModelId", value)) { _ModelId = value; OnPropertyChanged("ModelId"); } } }

    private String? _ModelName;
    /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
    [DisplayName("模型名称")]
    [Description("模型名称。冗余存储模型名称，方便历史数据检索")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ModelName", "模型名称。冗余存储模型名称，方便历史数据检索", "")]
    public String? ModelName { get => _ModelName; set { if (OnPropertyChanging("ModelName", value)) { _ModelName = value; OnPropertyChanged("ModelName"); } } }

    private String? _SkillCode;
    /// <summary>技能编码。关联的技能Code</summary>
    [DisplayName("技能编码")]
    [Description("技能编码。关联的技能Code")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SkillCode", "技能编码。关联的技能Code", "")]
    public String? SkillCode { get => _SkillCode; set { if (OnPropertyChanging("SkillCode", value)) { _SkillCode = value; OnPropertyChanged("SkillCode"); } } }

    private String? _SystemPrompt;
    /// <summary>系统提示词。预设的System Prompt</summary>
    [DisplayName("系统提示词")]
    [Description("系统提示词。预设的System Prompt")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("SystemPrompt", "系统提示词。预设的System Prompt", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? SystemPrompt { get => _SystemPrompt; set { if (OnPropertyChanging("SystemPrompt", value)) { _SystemPrompt = value; OnPropertyChanged("SystemPrompt"); } } }

    private String? _Prompt;
    /// <summary>提示词。选中预设时自动填入用户输入框的引导文本</summary>
    [DisplayName("提示词")]
    [Description("提示词。选中预设时自动填入用户输入框的引导文本")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Prompt", "提示词。选中预设时自动填入用户输入框的引导文本", "")]
    public String? Prompt { get => _Prompt; set { if (OnPropertyChanging("Prompt", value)) { _Prompt = value; OnPropertyChanged("Prompt"); } } }

    private NewLife.AI.Models.ThinkingMode _ThinkingMode;
    /// <summary>思考模式。Auto=0, Think=1, Fast=2</summary>
    [DisplayName("思考模式")]
    [Description("思考模式。Auto=0, Think=1, Fast=2")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThinkingMode", "思考模式。Auto=0, Think=1, Fast=2", "")]
    public NewLife.AI.Models.ThinkingMode ThinkingMode { get => _ThinkingMode; set { if (OnPropertyChanging("ThinkingMode", value)) { _ThinkingMode = value; OnPropertyChanged("ThinkingMode"); } } }

    private Boolean _IsDefault;
    /// <summary>默认预设。是否为用户的默认预设</summary>
    [DisplayName("默认预设")]
    [Description("默认预设。是否为用户的默认预设")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsDefault", "默认预设。是否为用户的默认预设", "")]
    public Boolean IsDefault { get => _IsDefault; set { if (OnPropertyChanging("IsDefault", value)) { _IsDefault = value; OnPropertyChanged("IsDefault"); } } }

    private Int32 _Sort;
    /// <summary>排序。越大越靠前</summary>
    [DisplayName("排序")]
    [Description("排序。越大越靠前")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序。越大越靠前", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

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
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IChatPreset model)
    {
        Id = model.Id;
        UserId = model.UserId;
        ProjectId = model.ProjectId;
        Name = model.Name;
        ModelId = model.ModelId;
        ModelName = model.ModelName;
        SkillCode = model.SkillCode;
        SystemPrompt = model.SystemPrompt;
        Prompt = model.Prompt;
        ThinkingMode = model.ThinkingMode;
        IsDefault = model.IsDefault;
        Sort = model.Sort;
        Enable = model.Enable;
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
            "UserId" => _UserId,
            "ProjectId" => _ProjectId,
            "Name" => _Name,
            "ModelId" => _ModelId,
            "ModelName" => _ModelName,
            "SkillCode" => _SkillCode,
            "SystemPrompt" => _SystemPrompt,
            "Prompt" => _Prompt,
            "ThinkingMode" => _ThinkingMode,
            "IsDefault" => _IsDefault,
            "Sort" => _Sort,
            "Enable" => _Enable,
            "CreateUserID" => _CreateUserID,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            "UpdateUserID" => _UpdateUserID,
            "UpdateIP" => _UpdateIP,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "ModelId": _ModelId = value.ToInt(); break;
                case "ModelName": _ModelName = Convert.ToString(value); break;
                case "SkillCode": _SkillCode = Convert.ToString(value); break;
                case "SystemPrompt": _SystemPrompt = Convert.ToString(value); break;
                case "Prompt": _Prompt = Convert.ToString(value); break;
                case "ThinkingMode": _ThinkingMode = (NewLife.AI.Models.ThinkingMode)value.ToInt(); break;
                case "IsDefault": _IsDefault = value.ToBoolean(); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>用户</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public XCode.Membership.User? User => Extends.Get(nameof(User), k => XCode.Membership.User.FindByID(UserId));

    /// <summary>用户</summary>
    [Map(nameof(UserId), typeof(XCode.Membership.User), "ID")]
    public String? UserName => User?.ToString();

    /// <summary>项目</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public AgentProject? Project => Extends.Get(nameof(Project), k => AgentProject.FindById(ProjectId));

    /// <summary>项目</summary>
    [Map(nameof(ProjectId), typeof(AgentProject), "Id")]
    public String? ProjectName => Project?.ToString();

    /// <summary>模型</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public ModelConfig? Model => Extends.Get(nameof(Model), k => ModelConfig.FindById(ModelId));

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ChatPreset? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据用户、项目、名称查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="projectId">项目</param>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static ChatPreset? FindByUserIdAndProjectIdAndName(Int32 userId, Int32 projectId, String? name)
    {
        if (userId < 0) return null;
        if (projectId < 0) return null;
        if (name == null) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.UserId == userId && e.ProjectId == projectId && e.Name.EqualIgnoreCase(name));

        return Find(_.UserId == userId & _.ProjectId == projectId & _.Name == name);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<ChatPreset> FindAllByUserId(Int32 userId)
    {
        if (userId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.UserId == userId);

        return FindAll(_.UserId == userId);
    }

    /// <summary>根据用户、项目查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<ChatPreset> FindAllByUserIdAndProjectId(Int32 userId, Int32 projectId)
    {
        if (userId < 0) return [];
        if (projectId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.UserId == userId && e.ProjectId == projectId);

        return FindAll(_.UserId == userId & _.ProjectId == projectId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户。0=系统级预设</param>
    /// <param name="projectId">项目。所属项目，0=个人/系统预设</param>
    /// <param name="modelId">模型。关联的模型配置Id</param>
    /// <param name="thinkingMode">思考模式。Auto=0, Think=1, Fast=2</param>
    /// <param name="isDefault">默认预设。是否为用户的默认预设</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatPreset> Search(Int32 userId, Int32 projectId, Int32 modelId, NewLife.AI.Models.ThinkingMode thinkingMode, Boolean? isDefault, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (modelId >= 0) exp &= _.ModelId == modelId;
        if (thinkingMode >= 0) exp &= _.ThinkingMode == thinkingMode;
        if (isDefault != null) exp &= _.IsDefault == isDefault;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得对话预设字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>用户。0=系统级预设</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>项目。所属项目，0=个人/系统预设</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>名称。预设模板名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>模型。关联的模型配置Id</summary>
        public static readonly Field ModelId = FindByName("ModelId");

        /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
        public static readonly Field ModelName = FindByName("ModelName");

        /// <summary>技能编码。关联的技能Code</summary>
        public static readonly Field SkillCode = FindByName("SkillCode");

        /// <summary>系统提示词。预设的System Prompt</summary>
        public static readonly Field SystemPrompt = FindByName("SystemPrompt");

        /// <summary>提示词。选中预设时自动填入用户输入框的引导文本</summary>
        public static readonly Field Prompt = FindByName("Prompt");

        /// <summary>思考模式。Auto=0, Think=1, Fast=2</summary>
        public static readonly Field ThinkingMode = FindByName("ThinkingMode");

        /// <summary>默认预设。是否为用户的默认预设</summary>
        public static readonly Field IsDefault = FindByName("IsDefault");

        /// <summary>排序。越大越靠前</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

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

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得对话预设字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户。0=系统级预设</summary>
        public const String UserId = "UserId";

        /// <summary>项目。所属项目，0=个人/系统预设</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>名称。预设模板名称</summary>
        public const String Name = "Name";

        /// <summary>模型。关联的模型配置Id</summary>
        public const String ModelId = "ModelId";

        /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
        public const String ModelName = "ModelName";

        /// <summary>技能编码。关联的技能Code</summary>
        public const String SkillCode = "SkillCode";

        /// <summary>系统提示词。预设的System Prompt</summary>
        public const String SystemPrompt = "SystemPrompt";

        /// <summary>提示词。选中预设时自动填入用户输入框的引导文本</summary>
        public const String Prompt = "Prompt";

        /// <summary>思考模式。Auto=0, Think=1, Fast=2</summary>
        public const String ThinkingMode = "ThinkingMode";

        /// <summary>默认预设。是否为用户的默认预设</summary>
        public const String IsDefault = "IsDefault";

        /// <summary>排序。越大越靠前</summary>
        public const String Sort = "Sort";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

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
    }
    #endregion
}
