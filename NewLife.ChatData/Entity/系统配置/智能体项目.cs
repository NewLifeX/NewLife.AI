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
using NewLife.AI.Interfaces;

namespace NewLife.ChatData.Entity;

/// <summary>智能体项目。多用户协作的AI资源容器，统一管理对话、知识、技能、成员</summary>
[Serializable]
[DataObject]
[Description("智能体项目。多用户协作的AI资源容器，统一管理对话、知识、技能、成员")]
[BindIndex("IU_AgentProject_Code", true, "Code")]
[BindIndex("IX_AgentProject_OwnerId", false, "OwnerId")]
[BindIndex("IX_AgentProject_OwnerId_Enable_Sort", false, "OwnerId,Enable,Sort")]
[BindTable("AgentProject", Description = "智能体项目。多用户协作的AI资源容器，统一管理对话、知识、技能、成员", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class AgentProject : IAgentProject, IEntity<IAgentProject>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _OwnerId;
    /// <summary>所有者。项目创建人/所有者</summary>
    [DisplayName("所有者")]
    [Description("所有者。项目创建人/所有者")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OwnerId", "所有者。项目创建人/所有者", "")]
    public Int32 OwnerId { get => _OwnerId; set { if (OnPropertyChanging("OwnerId", value)) { _OwnerId = value; OnPropertyChanged("OwnerId"); } } }

    private String? _Code;
    /// <summary>编码。英文唯一标识，#引用用</summary>
    [DisplayName("编码")]
    [Description("编码。英文唯一标识，#引用用")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "编码。英文唯一标识，#引用用", "")]
    public String? Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String? _Name;
    /// <summary>名称。项目显示名称</summary>
    [DisplayName("名称")]
    [Description("名称。项目显示名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。项目显示名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Icon;
    /// <summary>图标。表情符号图标，最多2字符，如🚀📊💡</summary>
    [DisplayName("图标")]
    [Description("图标。表情符号图标，最多2字符，如🚀📊💡")]
    [DataObjectField(false, false, true, 10)]
    [BindColumn("Icon", "图标。表情符号图标，最多2字符，如🚀📊💡", "")]
    public String? Icon { get => _Icon; set { if (OnPropertyChanging("Icon", value)) { _Icon = value; OnPropertyChanged("Icon"); } } }

    private String? _Color;
    /// <summary>颜色。预设颜色：blue/green/purple/orange/red/yellow/gray</summary>
    [DisplayName("颜色")]
    [Description("颜色。预设颜色：blue/green/purple/orange/red/yellow/gray")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Color", "颜色。预设颜色：blue/green/purple/orange/red/yellow/gray", "")]
    public String? Color { get => _Color; set { if (OnPropertyChanging("Color", value)) { _Color = value; OnPropertyChanged("Color"); } } }

    private String? _Description;
    /// <summary>描述。项目用途说明</summary>
    [DisplayName("描述")]
    [Description("描述。项目用途说明")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Description", "描述。项目用途说明", "")]
    public String? Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String? _SystemPrompt;
    /// <summary>系统提示词。项目级AI行为指令，注入System Prompt最高优先级</summary>
    [DisplayName("系统提示词")]
    [Description("系统提示词。项目级AI行为指令，注入System Prompt最高优先级")]
    [DataObjectField(false, false, true, 1000)]
    [BindColumn("SystemPrompt", "系统提示词。项目级AI行为指令，注入System Prompt最高优先级", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? SystemPrompt { get => _SystemPrompt; set { if (OnPropertyChanging("SystemPrompt", value)) { _SystemPrompt = value; OnPropertyChanged("SystemPrompt"); } } }

    private Int32 _MemoryMode;
    /// <summary>记忆模式。0=共享全局记忆/1=项目独立隔离记忆</summary>
    [DisplayName("记忆模式")]
    [Description("记忆模式。0=共享全局记忆/1=项目独立隔离记忆")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MemoryMode", "记忆模式。0=共享全局记忆/1=项目独立隔离记忆", "", DefaultValue = "0")]
    public Int32 MemoryMode { get => _MemoryMode; set { if (OnPropertyChanging("MemoryMode", value)) { _MemoryMode = value; OnPropertyChanged("MemoryMode"); } } }

    private Int32 _DefaultModel;
    /// <summary>默认模型。新会话的默认模型配置Id</summary>
    [DisplayName("默认模型")]
    [Description("默认模型。新会话的默认模型配置Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DefaultModel", "默认模型。新会话的默认模型配置Id", "")]
    public Int32 DefaultModel { get => _DefaultModel; set { if (OnPropertyChanging("DefaultModel", value)) { _DefaultModel = value; OnPropertyChanged("DefaultModel"); } } }

    private Int32 _DocumentCount;
    /// <summary>文档数。关联的知识文档总数</summary>
    [DisplayName("文档数")]
    [Description("文档数。关联的知识文档总数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DocumentCount", "文档数。关联的知识文档总数", "")]
    public Int32 DocumentCount { get => _DocumentCount; set { if (OnPropertyChanging("DocumentCount", value)) { _DocumentCount = value; OnPropertyChanged("DocumentCount"); } } }

    private Int32 _ArticleCount;
    /// <summary>文章数。清洗生成的Wiki文章总数</summary>
    [DisplayName("文章数")]
    [Description("文章数。清洗生成的Wiki文章总数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ArticleCount", "文章数。清洗生成的Wiki文章总数", "")]
    public Int32 ArticleCount { get => _ArticleCount; set { if (OnPropertyChanging("ArticleCount", value)) { _ArticleCount = value; OnPropertyChanged("ArticleCount"); } } }

    private Int64 _TotalTokens;
    /// <summary>总Token数。全部文章的Token累计 + 对话累计</summary>
    [DisplayName("总Token数")]
    [Description("总Token数。全部文章的Token累计 + 对话累计")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalTokens", "总Token数。全部文章的Token累计 + 对话累计", "")]
    public Int64 TotalTokens { get => _TotalTokens; set { if (OnPropertyChanging("TotalTokens", value)) { _TotalTokens = value; OnPropertyChanged("TotalTokens"); } } }

    private Decimal _TotalCost;
    /// <summary>总费用。累计消耗费用，单位：元，由用量记录汇总</summary>
    [Category("限额")]
    [DisplayName("总费用")]
    [Description("总费用。累计消耗费用，单位：元，由用量记录汇总")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalCost", "总费用。累计消耗费用，单位：元，由用量记录汇总", "", Precision = 18, Scale = 6)]
    public Decimal TotalCost { get => _TotalCost; set { if (OnPropertyChanging("TotalCost", value)) { _TotalCost = value; OnPropertyChanged("TotalCost"); } } }

    private Int64 _DailyTokenLimit;
    /// <summary>日Token限额。每日Token使用上限，0表示不限制</summary>
    [Category("限额")]
    [DisplayName("日Token限额")]
    [Description("日Token限额。每日Token使用上限，0表示不限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DailyTokenLimit", "日Token限额。每日Token使用上限，0表示不限制", "")]
    public Int64 DailyTokenLimit { get => _DailyTokenLimit; set { if (OnPropertyChanging("DailyTokenLimit", value)) { _DailyTokenLimit = value; OnPropertyChanged("DailyTokenLimit"); } } }

    private Int64 _MonthlyTokenLimit;
    /// <summary>月Token限额。每月Token使用上限，0表示不限制</summary>
    [Category("限额")]
    [DisplayName("月Token限额")]
    [Description("月Token限额。每月Token使用上限，0表示不限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MonthlyTokenLimit", "月Token限额。每月Token使用上限，0表示不限制", "")]
    public Int64 MonthlyTokenLimit { get => _MonthlyTokenLimit; set { if (OnPropertyChanging("MonthlyTokenLimit", value)) { _MonthlyTokenLimit = value; OnPropertyChanged("MonthlyTokenLimit"); } } }

    private Int64 _TotalTokenLimit;
    /// <summary>总Token限额。永久累计Token上限，0表示不限制</summary>
    [Category("限额")]
    [DisplayName("总Token限额")]
    [Description("总Token限额。永久累计Token上限，0表示不限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalTokenLimit", "总Token限额。永久累计Token上限，0表示不限制", "")]
    public Int64 TotalTokenLimit { get => _TotalTokenLimit; set { if (OnPropertyChanging("TotalTokenLimit", value)) { _TotalTokenLimit = value; OnPropertyChanged("TotalTokenLimit"); } } }

    private Decimal _DailyCostLimit;
    /// <summary>日费用限额。每日费用上限，单位：元，0表示不限制</summary>
    [Category("限额")]
    [DisplayName("日费用限额")]
    [Description("日费用限额。每日费用上限，单位：元，0表示不限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DailyCostLimit", "日费用限额。每日费用上限，单位：元，0表示不限制", "", Precision = 18, Scale = 6)]
    public Decimal DailyCostLimit { get => _DailyCostLimit; set { if (OnPropertyChanging("DailyCostLimit", value)) { _DailyCostLimit = value; OnPropertyChanged("DailyCostLimit"); } } }

    private Decimal _MonthlyCostLimit;
    /// <summary>月费用限额。每月费用上限，单位：元，0表示不限制</summary>
    [Category("限额")]
    [DisplayName("月费用限额")]
    [Description("月费用限额。每月费用上限，单位：元，0表示不限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MonthlyCostLimit", "月费用限额。每月费用上限，单位：元，0表示不限制", "", Precision = 18, Scale = 6)]
    public Decimal MonthlyCostLimit { get => _MonthlyCostLimit; set { if (OnPropertyChanging("MonthlyCostLimit", value)) { _MonthlyCostLimit = value; OnPropertyChanged("MonthlyCostLimit"); } } }

    private Decimal _TotalCostLimit;
    /// <summary>总费用限额。永久累计费用上限，单位：元，0表示不限制</summary>
    [Category("限额")]
    [DisplayName("总费用限额")]
    [Description("总费用限额。永久累计费用上限，单位：元，0表示不限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalCostLimit", "总费用限额。永久累计费用上限，单位：元，0表示不限制", "", Precision = 18, Scale = 6)]
    public Decimal TotalCostLimit { get => _TotalCostLimit; set { if (OnPropertyChanging("TotalCostLimit", value)) { _TotalCostLimit = value; OnPropertyChanged("TotalCostLimit"); } } }

    private Int32 _RateLimitPerMinute;
    /// <summary>分钟限流。每分钟请求上限，0表示不限制</summary>
    [Category("限额")]
    [DisplayName("分钟限流")]
    [Description("分钟限流。每分钟请求上限，0表示不限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("RateLimitPerMinute", "分钟限流。每分钟请求上限，0表示不限制", "")]
    public Int32 RateLimitPerMinute { get => _RateLimitPerMinute; set { if (OnPropertyChanging("RateLimitPerMinute", value)) { _RateLimitPerMinute = value; OnPropertyChanged("RateLimitPerMinute"); } } }

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

    private String? _CreateUser;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建者", "")]
    public String? CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

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

    private String? _UpdateUser;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新者", "")]
    public String? UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

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
    public void Copy(IAgentProject model)
    {
        Id = model.Id;
        OwnerId = model.OwnerId;
        Code = model.Code;
        Name = model.Name;
        Icon = model.Icon;
        Color = model.Color;
        Description = model.Description;
        SystemPrompt = model.SystemPrompt;
        MemoryMode = model.MemoryMode;
        DefaultModel = model.DefaultModel;
        DocumentCount = model.DocumentCount;
        ArticleCount = model.ArticleCount;
        TotalTokens = model.TotalTokens;
        TotalCost = model.TotalCost;
        DailyTokenLimit = model.DailyTokenLimit;
        MonthlyTokenLimit = model.MonthlyTokenLimit;
        TotalTokenLimit = model.TotalTokenLimit;
        DailyCostLimit = model.DailyCostLimit;
        MonthlyCostLimit = model.MonthlyCostLimit;
        TotalCostLimit = model.TotalCostLimit;
        RateLimitPerMinute = model.RateLimitPerMinute;
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
    public override Object? this[String name]
    {
        get => name switch
        {
            "Id" => _Id,
            "OwnerId" => _OwnerId,
            "Code" => _Code,
            "Name" => _Name,
            "Icon" => _Icon,
            "Color" => _Color,
            "Description" => _Description,
            "SystemPrompt" => _SystemPrompt,
            "MemoryMode" => _MemoryMode,
            "DefaultModel" => _DefaultModel,
            "DocumentCount" => _DocumentCount,
            "ArticleCount" => _ArticleCount,
            "TotalTokens" => _TotalTokens,
            "TotalCost" => _TotalCost,
            "DailyTokenLimit" => _DailyTokenLimit,
            "MonthlyTokenLimit" => _MonthlyTokenLimit,
            "TotalTokenLimit" => _TotalTokenLimit,
            "DailyCostLimit" => _DailyCostLimit,
            "MonthlyCostLimit" => _MonthlyCostLimit,
            "TotalCostLimit" => _TotalCostLimit,
            "RateLimitPerMinute" => _RateLimitPerMinute,
            "Enable" => _Enable,
            "Sort" => _Sort,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            "UpdateUser" => _UpdateUser,
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
                case "OwnerId": _OwnerId = value.ToInt(); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Icon": _Icon = Convert.ToString(value); break;
                case "Color": _Color = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "SystemPrompt": _SystemPrompt = Convert.ToString(value); break;
                case "MemoryMode": _MemoryMode = value.ToInt(); break;
                case "DefaultModel": _DefaultModel = value.ToInt(); break;
                case "DocumentCount": _DocumentCount = value.ToInt(); break;
                case "ArticleCount": _ArticleCount = value.ToInt(); break;
                case "TotalTokens": _TotalTokens = value.ToLong(); break;
                case "TotalCost": _TotalCost = Convert.ToDecimal(value); break;
                case "DailyTokenLimit": _DailyTokenLimit = value.ToLong(); break;
                case "MonthlyTokenLimit": _MonthlyTokenLimit = value.ToLong(); break;
                case "TotalTokenLimit": _TotalTokenLimit = value.ToLong(); break;
                case "DailyCostLimit": _DailyCostLimit = Convert.ToDecimal(value); break;
                case "MonthlyCostLimit": _MonthlyCostLimit = Convert.ToDecimal(value); break;
                case "TotalCostLimit": _TotalCostLimit = Convert.ToDecimal(value); break;
                case "RateLimitPerMinute": _RateLimitPerMinute = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
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
    /// <summary>所有者</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public XCode.Membership.User? Owner => Extends.Get(nameof(Owner), k => XCode.Membership.User.FindByID(OwnerId));

    /// <summary>所有者</summary>
    [Map(nameof(OwnerId), typeof(XCode.Membership.User), "ID")]
    public String? OwnerName => Owner?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AgentProject? FindById(Int32 id)
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
    public static AgentProject? FindByCode(String? code)
    {
        if (code == null) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        return Find(_.Code == code);
    }

    /// <summary>根据所有者查找</summary>
    /// <param name="ownerId">所有者</param>
    /// <returns>实体列表</returns>
    public static IList<AgentProject> FindAllByOwnerId(Int32 ownerId)
    {
        if (ownerId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.OwnerId == ownerId);

        return FindAll(_.OwnerId == ownerId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="ownerId">所有者。项目创建人/所有者</param>
    /// <param name="code">编码。英文唯一标识，#引用用</param>
    /// <param name="enable">启用</param>
    /// <param name="sort">排序。越大越靠前</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AgentProject> Search(Int32 ownerId, String? code, Boolean? enable, Int32 sort, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (ownerId >= 0) exp &= _.OwnerId == ownerId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (enable != null) exp &= _.Enable == enable;
        if (sort >= 0) exp &= _.Sort == sort;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得智能体项目字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>所有者。项目创建人/所有者</summary>
        public static readonly Field OwnerId = FindByName("OwnerId");

        /// <summary>编码。英文唯一标识，#引用用</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>名称。项目显示名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>图标。表情符号图标，最多2字符，如🚀📊💡</summary>
        public static readonly Field Icon = FindByName("Icon");

        /// <summary>颜色。预设颜色：blue/green/purple/orange/red/yellow/gray</summary>
        public static readonly Field Color = FindByName("Color");

        /// <summary>描述。项目用途说明</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>系统提示词。项目级AI行为指令，注入System Prompt最高优先级</summary>
        public static readonly Field SystemPrompt = FindByName("SystemPrompt");

        /// <summary>记忆模式。0=共享全局记忆/1=项目独立隔离记忆</summary>
        public static readonly Field MemoryMode = FindByName("MemoryMode");

        /// <summary>默认模型。新会话的默认模型配置Id</summary>
        public static readonly Field DefaultModel = FindByName("DefaultModel");

        /// <summary>文档数。关联的知识文档总数</summary>
        public static readonly Field DocumentCount = FindByName("DocumentCount");

        /// <summary>文章数。清洗生成的Wiki文章总数</summary>
        public static readonly Field ArticleCount = FindByName("ArticleCount");

        /// <summary>总Token数。全部文章的Token累计 + 对话累计</summary>
        public static readonly Field TotalTokens = FindByName("TotalTokens");

        /// <summary>总费用。累计消耗费用，单位：元，由用量记录汇总</summary>
        public static readonly Field TotalCost = FindByName("TotalCost");

        /// <summary>日Token限额。每日Token使用上限，0表示不限制</summary>
        public static readonly Field DailyTokenLimit = FindByName("DailyTokenLimit");

        /// <summary>月Token限额。每月Token使用上限，0表示不限制</summary>
        public static readonly Field MonthlyTokenLimit = FindByName("MonthlyTokenLimit");

        /// <summary>总Token限额。永久累计Token上限，0表示不限制</summary>
        public static readonly Field TotalTokenLimit = FindByName("TotalTokenLimit");

        /// <summary>日费用限额。每日费用上限，单位：元，0表示不限制</summary>
        public static readonly Field DailyCostLimit = FindByName("DailyCostLimit");

        /// <summary>月费用限额。每月费用上限，单位：元，0表示不限制</summary>
        public static readonly Field MonthlyCostLimit = FindByName("MonthlyCostLimit");

        /// <summary>总费用限额。永久累计费用上限，单位：元，0表示不限制</summary>
        public static readonly Field TotalCostLimit = FindByName("TotalCostLimit");

        /// <summary>分钟限流。每分钟请求上限，0表示不限制</summary>
        public static readonly Field RateLimitPerMinute = FindByName("RateLimitPerMinute");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>排序。越大越靠前</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建用户</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

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

    /// <summary>取得智能体项目字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>所有者。项目创建人/所有者</summary>
        public const String OwnerId = "OwnerId";

        /// <summary>编码。英文唯一标识，#引用用</summary>
        public const String Code = "Code";

        /// <summary>名称。项目显示名称</summary>
        public const String Name = "Name";

        /// <summary>图标。表情符号图标，最多2字符，如🚀📊💡</summary>
        public const String Icon = "Icon";

        /// <summary>颜色。预设颜色：blue/green/purple/orange/red/yellow/gray</summary>
        public const String Color = "Color";

        /// <summary>描述。项目用途说明</summary>
        public const String Description = "Description";

        /// <summary>系统提示词。项目级AI行为指令，注入System Prompt最高优先级</summary>
        public const String SystemPrompt = "SystemPrompt";

        /// <summary>记忆模式。0=共享全局记忆/1=项目独立隔离记忆</summary>
        public const String MemoryMode = "MemoryMode";

        /// <summary>默认模型。新会话的默认模型配置Id</summary>
        public const String DefaultModel = "DefaultModel";

        /// <summary>文档数。关联的知识文档总数</summary>
        public const String DocumentCount = "DocumentCount";

        /// <summary>文章数。清洗生成的Wiki文章总数</summary>
        public const String ArticleCount = "ArticleCount";

        /// <summary>总Token数。全部文章的Token累计 + 对话累计</summary>
        public const String TotalTokens = "TotalTokens";

        /// <summary>总费用。累计消耗费用，单位：元，由用量记录汇总</summary>
        public const String TotalCost = "TotalCost";

        /// <summary>日Token限额。每日Token使用上限，0表示不限制</summary>
        public const String DailyTokenLimit = "DailyTokenLimit";

        /// <summary>月Token限额。每月Token使用上限，0表示不限制</summary>
        public const String MonthlyTokenLimit = "MonthlyTokenLimit";

        /// <summary>总Token限额。永久累计Token上限，0表示不限制</summary>
        public const String TotalTokenLimit = "TotalTokenLimit";

        /// <summary>日费用限额。每日费用上限，单位：元，0表示不限制</summary>
        public const String DailyCostLimit = "DailyCostLimit";

        /// <summary>月费用限额。每月费用上限，单位：元，0表示不限制</summary>
        public const String MonthlyCostLimit = "MonthlyCostLimit";

        /// <summary>总费用限额。永久累计费用上限，单位：元，0表示不限制</summary>
        public const String TotalCostLimit = "TotalCostLimit";

        /// <summary>分钟限流。每分钟请求上限，0表示不限制</summary>
        public const String RateLimitPerMinute = "RateLimitPerMinute";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>排序。越大越靠前</summary>
        public const String Sort = "Sort";

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建用户</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新者</summary>
        public const String UpdateUser = "UpdateUser";

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
