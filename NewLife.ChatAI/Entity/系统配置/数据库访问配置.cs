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

/// <summary>数据库访问配置。控制AI工具可访问的数据库表，支持白名单/黑名单和按角色授权</summary>
[Serializable]
[DataObject]
[Description("数据库访问配置。控制AI工具可访问的数据库表，支持白名单/黑名单和按角色授权")]
[BindIndex("IU_DbAccessConfig_ConnName", true, "ConnName")]
[BindIndex("IX_DbAccessConfig_Enable", false, "Enable")]
[BindTable("DbAccessConfig", Description = "数据库访问配置。控制AI工具可访问的数据库表，支持白名单/黑名单和按角色授权", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class DbAccessConfig
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _ConnName;
    /// <summary>连接名。对应appsettings.json中ConnectionStrings的Key</summary>
    [DisplayName("连接名")]
    [Description("连接名。对应appsettings.json中ConnectionStrings的Key")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ConnName", "连接名。对应appsettings.json中ConnectionStrings的Key", "", Master = true)]
    public String? ConnName { get => _ConnName; set { if (OnPropertyChanging("ConnName", value)) { _ConnName = value; OnPropertyChanged("ConnName"); } } }

    private String? _DbType;
    /// <summary>数据库类型。SQLite/MySql/SqlServer/PostgreSQL/Oracle等</summary>
    [DisplayName("数据库类型")]
    [Description("数据库类型。SQLite/MySql/SqlServer/PostgreSQL/Oracle等")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("DbType", "数据库类型。SQLite/MySql/SqlServer/PostgreSQL/Oracle等", "")]
    public String? DbType { get => _DbType; set { if (OnPropertyChanging("DbType", value)) { _DbType = value; OnPropertyChanged("DbType"); } } }

    private String? _ConnString;
    /// <summary>连接字符串。DAL已注册的连接可为空；未注册的连接在此填写完整连接串</summary>
    [DisplayName("连接字符串")]
    [Description("连接字符串。DAL已注册的连接可为空；未注册的连接在此填写完整连接串")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ConnString", "连接字符串。DAL已注册的连接可为空；未注册的连接在此填写完整连接串", "")]
    public String? ConnString { get => _ConnString; set { if (OnPropertyChanging("ConnString", value)) { _ConnString = value; OnPropertyChanged("ConnString"); } } }

    private String? _WhiteTables;
    /// <summary>白名单。逗号分隔的表名列表，配置后仅允许访问这些表；为空则不限制</summary>
    [DisplayName("白名单")]
    [Description("白名单。逗号分隔的表名列表，配置后仅允许访问这些表；为空则不限制")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("WhiteTables", "白名单。逗号分隔的表名列表，配置后仅允许访问这些表；为空则不限制", "")]
    public String? WhiteTables { get => _WhiteTables; set { if (OnPropertyChanging("WhiteTables", value)) { _WhiteTables = value; OnPropertyChanged("WhiteTables"); } } }

    private String? _BlackTables;
    /// <summary>黑名单。逗号分隔的表名列表，禁止访问这些表；白名单为空时生效</summary>
    [DisplayName("黑名单")]
    [Description("黑名单。逗号分隔的表名列表，禁止访问这些表；白名单为空时生效")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("BlackTables", "黑名单。逗号分隔的表名列表，禁止访问这些表；白名单为空时生效", "")]
    public String? BlackTables { get => _BlackTables; set { if (OnPropertyChanging("BlackTables", value)) { _BlackTables = value; OnPropertyChanged("BlackTables"); } } }

    private String? _RoleIds;
    /// <summary>角色组。逗号分隔角色ID列表，命中即放行；为空时不限制</summary>
    [Category("安全")]
    [DisplayName("角色组")]
    [Description("角色组。逗号分隔角色ID列表，命中即放行；为空时不限制")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("RoleIds", "角色组。逗号分隔角色ID列表，命中即放行；为空时不限制", "")]
    public String? RoleIds { get => _RoleIds; set { if (OnPropertyChanging("RoleIds", value)) { _RoleIds = value; OnPropertyChanged("RoleIds"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "", DefaultValue = "true")]
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
            "ConnName" => _ConnName,
            "DbType" => _DbType,
            "ConnString" => _ConnString,
            "WhiteTables" => _WhiteTables,
            "BlackTables" => _BlackTables,
            "RoleIds" => _RoleIds,
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
                case "ConnName": _ConnName = Convert.ToString(value); break;
                case "DbType": _DbType = Convert.ToString(value); break;
                case "ConnString": _ConnString = Convert.ToString(value); break;
                case "WhiteTables": _WhiteTables = Convert.ToString(value); break;
                case "BlackTables": _BlackTables = Convert.ToString(value); break;
                case "RoleIds": _RoleIds = Convert.ToString(value); break;
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
    public static DbAccessConfig? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据连接名查找</summary>
    /// <param name="connName">连接名</param>
    /// <returns>实体对象</returns>
    public static DbAccessConfig? FindByConnName(String? connName)
    {
        if (connName == null) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ConnName.EqualIgnoreCase(connName));

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(connName) as DbAccessConfig;

        //return Find(_.ConnName == connName);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<DbAccessConfig> Search(Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得数据库访问配置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>连接名。对应appsettings.json中ConnectionStrings的Key</summary>
        public static readonly Field ConnName = FindByName("ConnName");

        /// <summary>数据库类型。SQLite/MySql/SqlServer/PostgreSQL/Oracle等</summary>
        public static readonly Field DbType = FindByName("DbType");

        /// <summary>连接字符串。DAL已注册的连接可为空；未注册的连接在此填写完整连接串</summary>
        public static readonly Field ConnString = FindByName("ConnString");

        /// <summary>白名单。逗号分隔的表名列表，配置后仅允许访问这些表；为空则不限制</summary>
        public static readonly Field WhiteTables = FindByName("WhiteTables");

        /// <summary>黑名单。逗号分隔的表名列表，禁止访问这些表；白名单为空时生效</summary>
        public static readonly Field BlackTables = FindByName("BlackTables");

        /// <summary>角色组。逗号分隔角色ID列表，命中即放行；为空时不限制</summary>
        public static readonly Field RoleIds = FindByName("RoleIds");

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

    /// <summary>取得数据库访问配置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>连接名。对应appsettings.json中ConnectionStrings的Key</summary>
        public const String ConnName = "ConnName";

        /// <summary>数据库类型。SQLite/MySql/SqlServer/PostgreSQL/Oracle等</summary>
        public const String DbType = "DbType";

        /// <summary>连接字符串。DAL已注册的连接可为空；未注册的连接在此填写完整连接串</summary>
        public const String ConnString = "ConnString";

        /// <summary>白名单。逗号分隔的表名列表，配置后仅允许访问这些表；为空则不限制</summary>
        public const String WhiteTables = "WhiteTables";

        /// <summary>黑名单。逗号分隔的表名列表，禁止访问这些表；白名单为空时生效</summary>
        public const String BlackTables = "BlackTables";

        /// <summary>角色组。逗号分隔角色ID列表，命中即放行；为空时不限制</summary>
        public const String RoleIds = "RoleIds";

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
