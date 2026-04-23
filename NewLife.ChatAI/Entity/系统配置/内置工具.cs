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

namespace NewLife.ChatAI.Entity;

/// <summary>内置工具。系统内置的.NET工具函数，启动时自动扫描注册，管理员可在后台管理</summary>
[Serializable]
[DataObject]
[Description("内置工具。系统内置的.NET工具函数，启动时自动扫描注册，管理员可在后台管理")]
[BindIndex("IU_NativeTool_Name", true, "Name")]
[BindIndex("IX_NativeTool_Enable", false, "Enable")]
[BindTable("NativeTool", Description = "内置工具。系统内置的.NET工具函数，启动时自动扫描注册，管理员可在后台管理", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class NativeTool : INativeTool, IEntity<INativeTool>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Name;
    /// <summary>名称。工具唯一标识（snake_case），如get_current_time</summary>
    [DisplayName("名称")]
    [Description("名称。工具唯一标识（snake_case），如get_current_time")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。工具唯一标识（snake_case），如get_current_time", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _DisplayName;
    /// <summary>显示名称。工具的中文名称，用于后台展示</summary>
    [DisplayName("显示名称")]
    [Description("显示名称。工具的中文名称，用于后台展示")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DisplayName", "显示名称。工具的中文名称，用于后台展示", "")]
    public String? DisplayName { get => _DisplayName; set { if (OnPropertyChanging("DisplayName", value)) { _DisplayName = value; OnPropertyChanged("DisplayName"); } } }

    private String? _ClassName;
    /// <summary>类名。工具方法所在类的全限定类名，如NewLife.AI.Tools.BuiltinToolService</summary>
    [DisplayName("类名")]
    [Description("类名。工具方法所在类的全限定类名，如NewLife.AI.Tools.BuiltinToolService")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("ClassName", "类名。工具方法所在类的全限定类名，如NewLife.AI.Tools.BuiltinToolService", "")]
    public String? ClassName { get => _ClassName; set { if (OnPropertyChanging("ClassName", value)) { _ClassName = value; OnPropertyChanged("ClassName"); } } }

    private String? _MethodName;
    /// <summary>方法名。工具对应的C#方法名，如GetCurrentTime</summary>
    [DisplayName("方法名")]
    [Description("方法名。工具对应的C#方法名，如GetCurrentTime")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("MethodName", "方法名。工具对应的C#方法名，如GetCurrentTime", "")]
    public String? MethodName { get => _MethodName; set { if (OnPropertyChanging("MethodName", value)) { _MethodName = value; OnPropertyChanged("MethodName"); } } }

    private String? _Description;
    /// <summary>描述。工具功能说明，自动从XML注释提取，锁定后不再覆盖</summary>
    [DisplayName("描述")]
    [Description("描述。工具功能说明，自动从XML注释提取，锁定后不再覆盖")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Description", "描述。工具功能说明，自动从XML注释提取，锁定后不再覆盖", "", ItemType = "markdown")]
    public String? Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String? _Parameters;
    /// <summary>参数Schema。JSON格式的函数参数定义，锁定后不再覆盖</summary>
    [DisplayName("参数Schema")]
    [Description("参数Schema。JSON格式的函数参数定义，锁定后不再覆盖")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Parameters", "参数Schema。JSON格式的函数参数定义，锁定后不再覆盖", "", ItemType = "json", ShowIn = "Auto,-List,-Search")]
    public String? Parameters { get => _Parameters; set { if (OnPropertyChanging("Parameters", value)) { _Parameters = value; OnPropertyChanged("Parameters"); } } }

    private String? _Triggers;
    /// <summary>触发词。逗号分隔的关键词列表，消息包含任一词时自动激活该工具（仅IsSystem=false生效）</summary>
    [DisplayName("触发词")]
    [Description("触发词。逗号分隔的关键词列表，消息包含任一词时自动激活该工具（仅IsSystem=false生效）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Triggers", "触发词。逗号分隔的关键词列表，消息包含任一词时自动激活该工具（仅IsSystem=false生效）", "")]
    public String? Triggers { get => _Triggers; set { if (OnPropertyChanging("Triggers", value)) { _Triggers = value; OnPropertyChanged("Triggers"); } } }

    private Boolean _Enable;
    /// <summary>启用。是否启用此工具，禁用后不传给LLM调用</summary>
    [DisplayName("启用")]
    [Description("启用。是否启用此工具，禁用后不传给LLM调用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用。是否启用此工具，禁用后不传给LLM调用", "", DefaultValue = "true")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private Boolean _IsSystem;
    /// <summary>系统工具。每次LLM请求自动携带，无需@引用，如当前时间、当前用户信息</summary>
    [DisplayName("系统工具")]
    [Description("系统工具。每次LLM请求自动携带，无需@引用，如当前时间、当前用户信息")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsSystem", "系统工具。每次LLM请求自动携带，无需@引用，如当前时间、当前用户信息", "")]
    public Boolean IsSystem { get => _IsSystem; set { if (OnPropertyChanging("IsSystem", value)) { _IsSystem = value; OnPropertyChanged("IsSystem"); } } }

    private Boolean _IsLocked;
    /// <summary>锁定。锁定后启动扫描时不再覆盖描述和参数Schema信息，可用于手工调整</summary>
    [DisplayName("锁定")]
    [Description("锁定。锁定后启动扫描时不再覆盖描述和参数Schema信息，可用于手工调整")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsLocked", "锁定。锁定后启动扫描时不再覆盖描述和参数Schema信息，可用于手工调整", "")]
    public Boolean IsLocked { get => _IsLocked; set { if (OnPropertyChanging("IsLocked", value)) { _IsLocked = value; OnPropertyChanged("IsLocked"); } } }

    private String? _RoleIds;
    /// <summary>角色组。逗号分隔角色ID列表，命中即放行；为空时不限制</summary>
    [Category("安全")]
    [DisplayName("角色组")]
    [Description("角色组。逗号分隔角色ID列表，命中即放行；为空时不限制")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("RoleIds", "角色组。逗号分隔角色ID列表，命中即放行；为空时不限制", "")]
    public String? RoleIds { get => _RoleIds; set { if (OnPropertyChanging("RoleIds", value)) { _RoleIds = value; OnPropertyChanged("RoleIds"); } } }

    private String? _DepartmentIds;
    /// <summary>部门组。逗号分隔部门ID列表，命中即放行；为空时不限制</summary>
    [Category("安全")]
    [DisplayName("部门组")]
    [Description("部门组。逗号分隔部门ID列表，命中即放行；为空时不限制")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("DepartmentIds", "部门组。逗号分隔部门ID列表，命中即放行；为空时不限制", "")]
    public String? DepartmentIds { get => _DepartmentIds; set { if (OnPropertyChanging("DepartmentIds", value)) { _DepartmentIds = value; OnPropertyChanged("DepartmentIds"); } } }

    private String? _ProjectIds;
    /// <summary>项目组。逗号分隔项目ID列表，用户在该项目内即放行；为空时不限制</summary>
    [Category("安全")]
    [DisplayName("项目组")]
    [Description("项目组。逗号分隔项目ID列表，用户在该项目内即放行；为空时不限制")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ProjectIds", "项目组。逗号分隔项目ID列表，用户在该项目内即放行；为空时不限制", "")]
    public String? ProjectIds { get => _ProjectIds; set { if (OnPropertyChanging("ProjectIds", value)) { _ProjectIds = value; OnPropertyChanged("ProjectIds"); } } }

    private String? _Providers;
    /// <summary>服务提供者。多个逗号分隔，按顺序尝试，如pconline,ipapi或bing,duckduckgo</summary>
    [DisplayName("服务提供者")]
    [Description("服务提供者。多个逗号分隔，按顺序尝试，如pconline,ipapi或bing,duckduckgo")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Providers", "服务提供者。多个逗号分隔，按顺序尝试，如pconline,ipapi或bing,duckduckgo", "")]
    public String? Providers { get => _Providers; set { if (OnPropertyChanging("Providers", value)) { _Providers = value; OnPropertyChanged("Providers"); } } }

    private String? _Endpoint;
    /// <summary>远程地址。工具依赖的远程服务地址，如https://ai.newlifex.com</summary>
    [DisplayName("远程地址")]
    [Description("远程地址。工具依赖的远程服务地址，如https://ai.newlifex.com")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Endpoint", "远程地址。工具依赖的远程服务地址，如https://ai.newlifex.com", "", ItemType = "url")]
    public String? Endpoint { get => _Endpoint; set { if (OnPropertyChanging("Endpoint", value)) { _Endpoint = value; OnPropertyChanged("Endpoint"); } } }

    private String? _ApiKey;
    /// <summary>API密钥。调用远程服务所需的访问密钥</summary>
    [DisplayName("API密钥")]
    [Description("API密钥。调用远程服务所需的访问密钥")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ApiKey", "API密钥。调用远程服务所需的访问密钥", "", ShowIn = "Auto,-List,-Search")]
    public String? ApiKey { get => _ApiKey; set { if (OnPropertyChanging("ApiKey", value)) { _ApiKey = value; OnPropertyChanged("ApiKey"); } } }

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
    public void Copy(INativeTool model)
    {
        Id = model.Id;
        Name = model.Name;
        DisplayName = model.DisplayName;
        ClassName = model.ClassName;
        MethodName = model.MethodName;
        Description = model.Description;
        Parameters = model.Parameters;
        Triggers = model.Triggers;
        Enable = model.Enable;
        IsSystem = model.IsSystem;
        IsLocked = model.IsLocked;
        RoleIds = model.RoleIds;
        DepartmentIds = model.DepartmentIds;
        ProjectIds = model.ProjectIds;
        Providers = model.Providers;
        Endpoint = model.Endpoint;
        ApiKey = model.ApiKey;
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
            "Name" => _Name,
            "DisplayName" => _DisplayName,
            "ClassName" => _ClassName,
            "MethodName" => _MethodName,
            "Description" => _Description,
            "Parameters" => _Parameters,
            "Triggers" => _Triggers,
            "Enable" => _Enable,
            "IsSystem" => _IsSystem,
            "IsLocked" => _IsLocked,
            "RoleIds" => _RoleIds,
            "DepartmentIds" => _DepartmentIds,
            "ProjectIds" => _ProjectIds,
            "Providers" => _Providers,
            "Endpoint" => _Endpoint,
            "ApiKey" => _ApiKey,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "DisplayName": _DisplayName = Convert.ToString(value); break;
                case "ClassName": _ClassName = Convert.ToString(value); break;
                case "MethodName": _MethodName = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "Parameters": _Parameters = Convert.ToString(value); break;
                case "Triggers": _Triggers = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "IsSystem": _IsSystem = value.ToBoolean(); break;
                case "IsLocked": _IsLocked = value.ToBoolean(); break;
                case "RoleIds": _RoleIds = Convert.ToString(value); break;
                case "DepartmentIds": _DepartmentIds = Convert.ToString(value); break;
                case "ProjectIds": _ProjectIds = Convert.ToString(value); break;
                case "Providers": _Providers = Convert.ToString(value); break;
                case "Endpoint": _Endpoint = Convert.ToString(value); break;
                case "ApiKey": _ApiKey = Convert.ToString(value); break;
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
    public static NativeTool? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据名称查找</summary>
    /// <param name="name">名称</param>
    /// <returns>实体对象</returns>
    public static NativeTool? FindByName(String? name)
    {
        if (name == null) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as NativeTool;

        //return Find(_.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="enable">启用。是否启用此工具，禁用后不传给LLM调用</param>
    /// <param name="isSystem">系统工具。每次LLM请求自动携带，无需@引用，如当前时间、当前用户信息</param>
    /// <param name="isLocked">锁定。锁定后启动扫描时不再覆盖描述和参数Schema信息，可用于手工调整</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<NativeTool> Search(Boolean? enable, Boolean? isSystem, Boolean? isLocked, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (enable != null) exp &= _.Enable == enable;
        if (isSystem != null) exp &= _.IsSystem == isSystem;
        if (isLocked != null) exp &= _.IsLocked == isLocked;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得内置工具字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称。工具唯一标识（snake_case），如get_current_time</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>显示名称。工具的中文名称，用于后台展示</summary>
        public static readonly Field DisplayName = FindByName("DisplayName");

        /// <summary>类名。工具方法所在类的全限定类名，如NewLife.AI.Tools.BuiltinToolService</summary>
        public static readonly Field ClassName = FindByName("ClassName");

        /// <summary>方法名。工具对应的C#方法名，如GetCurrentTime</summary>
        public static readonly Field MethodName = FindByName("MethodName");

        /// <summary>描述。工具功能说明，自动从XML注释提取，锁定后不再覆盖</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>参数Schema。JSON格式的函数参数定义，锁定后不再覆盖</summary>
        public static readonly Field Parameters = FindByName("Parameters");

        /// <summary>触发词。逗号分隔的关键词列表，消息包含任一词时自动激活该工具（仅IsSystem=false生效）</summary>
        public static readonly Field Triggers = FindByName("Triggers");

        /// <summary>启用。是否启用此工具，禁用后不传给LLM调用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>系统工具。每次LLM请求自动携带，无需@引用，如当前时间、当前用户信息</summary>
        public static readonly Field IsSystem = FindByName("IsSystem");

        /// <summary>锁定。锁定后启动扫描时不再覆盖描述和参数Schema信息，可用于手工调整</summary>
        public static readonly Field IsLocked = FindByName("IsLocked");

        /// <summary>角色组。逗号分隔角色ID列表，命中即放行；为空时不限制</summary>
        public static readonly Field RoleIds = FindByName("RoleIds");

        /// <summary>部门组。逗号分隔部门ID列表，命中即放行；为空时不限制</summary>
        public static readonly Field DepartmentIds = FindByName("DepartmentIds");

        /// <summary>项目组。逗号分隔项目ID列表，用户在该项目内即放行；为空时不限制</summary>
        public static readonly Field ProjectIds = FindByName("ProjectIds");

        /// <summary>服务提供者。多个逗号分隔，按顺序尝试，如pconline,ipapi或bing,duckduckgo</summary>
        public static readonly Field Providers = FindByName("Providers");

        /// <summary>远程地址。工具依赖的远程服务地址，如https://ai.newlifex.com</summary>
        public static readonly Field Endpoint = FindByName("Endpoint");

        /// <summary>API密钥。调用远程服务所需的访问密钥</summary>
        public static readonly Field ApiKey = FindByName("ApiKey");

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

    /// <summary>取得内置工具字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称。工具唯一标识（snake_case），如get_current_time</summary>
        public const String Name = "Name";

        /// <summary>显示名称。工具的中文名称，用于后台展示</summary>
        public const String DisplayName = "DisplayName";

        /// <summary>类名。工具方法所在类的全限定类名，如NewLife.AI.Tools.BuiltinToolService</summary>
        public const String ClassName = "ClassName";

        /// <summary>方法名。工具对应的C#方法名，如GetCurrentTime</summary>
        public const String MethodName = "MethodName";

        /// <summary>描述。工具功能说明，自动从XML注释提取，锁定后不再覆盖</summary>
        public const String Description = "Description";

        /// <summary>参数Schema。JSON格式的函数参数定义，锁定后不再覆盖</summary>
        public const String Parameters = "Parameters";

        /// <summary>触发词。逗号分隔的关键词列表，消息包含任一词时自动激活该工具（仅IsSystem=false生效）</summary>
        public const String Triggers = "Triggers";

        /// <summary>启用。是否启用此工具，禁用后不传给LLM调用</summary>
        public const String Enable = "Enable";

        /// <summary>系统工具。每次LLM请求自动携带，无需@引用，如当前时间、当前用户信息</summary>
        public const String IsSystem = "IsSystem";

        /// <summary>锁定。锁定后启动扫描时不再覆盖描述和参数Schema信息，可用于手工调整</summary>
        public const String IsLocked = "IsLocked";

        /// <summary>角色组。逗号分隔角色ID列表，命中即放行；为空时不限制</summary>
        public const String RoleIds = "RoleIds";

        /// <summary>部门组。逗号分隔部门ID列表，命中即放行；为空时不限制</summary>
        public const String DepartmentIds = "DepartmentIds";

        /// <summary>项目组。逗号分隔项目ID列表，用户在该项目内即放行；为空时不限制</summary>
        public const String ProjectIds = "ProjectIds";

        /// <summary>服务提供者。多个逗号分隔，按顺序尝试，如pconline,ipapi或bing,duckduckgo</summary>
        public const String Providers = "Providers";

        /// <summary>远程地址。工具依赖的远程服务地址，如https://ai.newlifex.com</summary>
        public const String Endpoint = "Endpoint";

        /// <summary>API密钥。调用远程服务所需的访问密钥</summary>
        public const String ApiKey = "ApiKey";

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
