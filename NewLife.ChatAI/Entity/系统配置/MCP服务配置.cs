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

/// <summary>MCP服务配置。MCP Server列表及工具发现信息</summary>
[Serializable]
[DataObject]
[Description("MCP服务配置。MCP Server列表及工具发现信息")]
[BindIndex("IU_McpServerConfig_Name", true, "Name")]
[BindTable("McpServerConfig", Description = "MCP服务配置。MCP Server列表及工具发现信息", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class McpServerConfig
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
    /// <summary>名称。服务名称</summary>
    [DisplayName("名称")]
    [Description("名称。服务名称")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。服务名称", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Endpoint;
    /// <summary>接口地址。MCP Server地址</summary>
    [DisplayName("接口地址")]
    [Description("接口地址。MCP Server地址")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Endpoint", "接口地址。MCP Server地址", "", ItemType = "url")]
    public String? Endpoint { get => _Endpoint; set { if (OnPropertyChanging("Endpoint", value)) { _Endpoint = value; OnPropertyChanged("Endpoint"); } } }

    private NewLife.AI.Models.McpTransportType _TransportType;
    /// <summary>传输类型。Http/Sse/Stdio</summary>
    [DisplayName("传输类型")]
    [Description("传输类型。Http/Sse/Stdio")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TransportType", "传输类型。Http/Sse/Stdio", "")]
    public NewLife.AI.Models.McpTransportType TransportType { get => _TransportType; set { if (OnPropertyChanging("TransportType", value)) { _TransportType = value; OnPropertyChanged("TransportType"); } } }

    private String? _AuthType;
    /// <summary>认证类型。None/Bearer/ApiKey</summary>
    [DisplayName("认证类型")]
    [Description("认证类型。None/Bearer/ApiKey")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("AuthType", "认证类型。None/Bearer/ApiKey", "")]
    public String? AuthType { get => _AuthType; set { if (OnPropertyChanging("AuthType", value)) { _AuthType = value; OnPropertyChanged("AuthType"); } } }

    private String? _AuthToken;
    /// <summary>认证令牌</summary>
    [DisplayName("认证令牌")]
    [Description("认证令牌")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("AuthToken", "认证令牌", "", ShowIn = "Auto,-List,-Search")]
    public String? AuthToken { get => _AuthToken; set { if (OnPropertyChanging("AuthToken", value)) { _AuthToken = value; OnPropertyChanged("AuthToken"); } } }

    private String? _AvailableTools;
    /// <summary>可用工具。已发现的工具列表，JSON格式</summary>
    [DisplayName("可用工具")]
    [Description("可用工具。已发现的工具列表，JSON格式")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("AvailableTools", "可用工具。已发现的工具列表，JSON格式", "", ShowIn = "Auto,-List,-Search")]
    public String? AvailableTools { get => _AvailableTools; set { if (OnPropertyChanging("AvailableTools", value)) { _AvailableTools = value; OnPropertyChanged("AvailableTools"); } } }

    private String? _Triggers;
    /// <summary>触发词。逗号分隔；消息命中任一关键词时自动激活该 MCP 服务（仅非系统工具过滤场景）</summary>
    [DisplayName("触发词")]
    [Description("触发词。逗号分隔；消息命中任一关键词时自动激活该 MCP 服务（仅非系统工具过滤场景）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Triggers", "触发词。逗号分隔；消息命中任一关键词时自动激活该 MCP 服务（仅非系统工具过滤场景）", "")]
    public String? Triggers { get => _Triggers; set { if (OnPropertyChanging("Triggers", value)) { _Triggers = value; OnPropertyChanged("Triggers"); } } }

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
            "Name" => _Name,
            "Endpoint" => _Endpoint,
            "TransportType" => _TransportType,
            "AuthType" => _AuthType,
            "AuthToken" => _AuthToken,
            "AvailableTools" => _AvailableTools,
            "Triggers" => _Triggers,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "Endpoint": _Endpoint = Convert.ToString(value); break;
                case "TransportType": _TransportType = (NewLife.AI.Models.McpTransportType)value.ToInt(); break;
                case "AuthType": _AuthType = Convert.ToString(value); break;
                case "AuthToken": _AuthToken = Convert.ToString(value); break;
                case "AvailableTools": _AvailableTools = Convert.ToString(value); break;
                case "Triggers": _Triggers = Convert.ToString(value); break;
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
    public static McpServerConfig? FindById(Int32 id)
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
    public static McpServerConfig? FindByName(String? name)
    {
        if (name == null) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Name.EqualIgnoreCase(name));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(name) as McpServerConfig;

        //return Find(_.Name == name);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="transportType">传输类型。Http/Sse/Stdio</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<McpServerConfig> Search(NewLife.AI.Models.McpTransportType transportType, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (transportType >= 0) exp &= _.TransportType == transportType;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得MCP服务配置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>名称。服务名称</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>接口地址。MCP Server地址</summary>
        public static readonly Field Endpoint = FindByName("Endpoint");

        /// <summary>传输类型。Http/Sse/Stdio</summary>
        public static readonly Field TransportType = FindByName("TransportType");

        /// <summary>认证类型。None/Bearer/ApiKey</summary>
        public static readonly Field AuthType = FindByName("AuthType");

        /// <summary>认证令牌</summary>
        public static readonly Field AuthToken = FindByName("AuthToken");

        /// <summary>可用工具。已发现的工具列表，JSON格式</summary>
        public static readonly Field AvailableTools = FindByName("AvailableTools");

        /// <summary>触发词。逗号分隔；消息命中任一关键词时自动激活该 MCP 服务（仅非系统工具过滤场景）</summary>
        public static readonly Field Triggers = FindByName("Triggers");

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

    /// <summary>取得MCP服务配置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>名称。服务名称</summary>
        public const String Name = "Name";

        /// <summary>接口地址。MCP Server地址</summary>
        public const String Endpoint = "Endpoint";

        /// <summary>传输类型。Http/Sse/Stdio</summary>
        public const String TransportType = "TransportType";

        /// <summary>认证类型。None/Bearer/ApiKey</summary>
        public const String AuthType = "AuthType";

        /// <summary>认证令牌</summary>
        public const String AuthToken = "AuthToken";

        /// <summary>可用工具。已发现的工具列表，JSON格式</summary>
        public const String AvailableTools = "AvailableTools";

        /// <summary>触发词。逗号分隔；消息命中任一关键词时自动激活该 MCP 服务（仅非系统工具过滤场景）</summary>
        public const String Triggers = "Triggers";

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
