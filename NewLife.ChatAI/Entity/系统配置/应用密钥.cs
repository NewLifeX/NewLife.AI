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

/// <summary>应用密钥。API网关访问凭证，用于外部系统调用模型服务</summary>
[Serializable]
[DataObject]
[Description("应用密钥。API网关访问凭证，用于外部系统调用模型服务")]
[BindIndex("IU_AppKey_Secret", true, "Secret")]
[BindIndex("IX_AppKey_UserId", false, "UserId")]
[BindTable("AppKey", Description = "应用密钥。API网关访问凭证，用于外部系统调用模型服务", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class AppKey
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
    /// <summary>用户。密钥所属用户</summary>
    [DisplayName("用户")]
    [Description("用户。密钥所属用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户。密钥所属用户", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private String? _Name;
    /// <summary>名称。用户自定义标识，如业务系统A</summary>
    [DisplayName("名称")]
    [Description("名称。用户自定义标识，如业务系统A")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Name", "名称。用户自定义标识，如业务系统A", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Secret;
    /// <summary>密钥。sk-前缀的随机字符串，创建时仅展示一次</summary>
    [DisplayName("密钥")]
    [Description("密钥。sk-前缀的随机字符串，创建时仅展示一次")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Secret", "密钥。sk-前缀的随机字符串，创建时仅展示一次", "", ShowIn = "Auto,-List,-Search")]
    public String? Secret { get => _Secret; set { if (OnPropertyChanging("Secret", value)) { _Secret = value; OnPropertyChanged("Secret"); } } }

    private String? _Models;
    /// <summary>可用模型。逗号分隔的模型名称或编码，为空时不限制</summary>
    [DisplayName("可用模型")]
    [Description("可用模型。逗号分隔的模型名称或编码，为空时不限制")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Models", "可用模型。逗号分隔的模型名称或编码，为空时不限制", "")]
    public String? Models { get => _Models; set { if (OnPropertyChanging("Models", value)) { _Models = value; OnPropertyChanged("Models"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

    private DateTime _ExpireTime;
    /// <summary>过期时间。null表示永不过期</summary>
    [DisplayName("过期时间")]
    [Description("过期时间。null表示永不过期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ExpireTime", "过期时间。null表示永不过期", "")]
    public DateTime ExpireTime { get => _ExpireTime; set { if (OnPropertyChanging("ExpireTime", value)) { _ExpireTime = value; OnPropertyChanged("ExpireTime"); } } }

    private DateTime _LastCallTime;
    /// <summary>最后调用时间</summary>
    [DisplayName("最后调用时间")]
    [Description("最后调用时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastCallTime", "最后调用时间", "")]
    public DateTime LastCallTime { get => _LastCallTime; set { if (OnPropertyChanging("LastCallTime", value)) { _LastCallTime = value; OnPropertyChanged("LastCallTime"); } } }

    private Int64 _Calls;
    /// <summary>调用次数。累计API请求数</summary>
    [DisplayName("调用次数")]
    [Description("调用次数。累计API请求数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Calls", "调用次数。累计API请求数", "")]
    public Int64 Calls { get => _Calls; set { if (OnPropertyChanging("Calls", value)) { _Calls = value; OnPropertyChanged("Calls"); } } }

    private Int64 _TotalTokens;
    /// <summary>总Token数。累计消耗Token</summary>
    [DisplayName("总Token数")]
    [Description("总Token数。累计消耗Token")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalTokens", "总Token数。累计消耗Token", "")]
    public Int64 TotalTokens { get => _TotalTokens; set { if (OnPropertyChanging("TotalTokens", value)) { _TotalTokens = value; OnPropertyChanged("TotalTokens"); } } }

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
            "UserId" => _UserId,
            "Name" => _Name,
            "Secret" => _Secret,
            "Models" => _Models,
            "Enable" => _Enable,
            "ExpireTime" => _ExpireTime,
            "LastCallTime" => _LastCallTime,
            "Calls" => _Calls,
            "TotalTokens" => _TotalTokens,
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
                case "UserId": _UserId = value.ToInt(); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Secret": _Secret = Convert.ToString(value); break;
                case "Models": _Models = Convert.ToString(value); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "ExpireTime": _ExpireTime = value.ToDateTime(); break;
                case "LastCallTime": _LastCallTime = value.ToDateTime(); break;
                case "Calls": _Calls = value.ToLong(); break;
                case "TotalTokens": _TotalTokens = value.ToLong(); break;
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
    public static AppKey? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据密钥查找</summary>
    /// <param name="secret">密钥</param>
    /// <returns>实体对象</returns>
    public static AppKey? FindBySecret(String? secret)
    {
        if (secret == null) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Secret.EqualIgnoreCase(secret));

        return Find(_.Secret == secret);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<AppKey> FindAllByUserId(Int32 userId)
    {
        if (userId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.UserId == userId);

        return FindAll(_.UserId == userId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户。密钥所属用户</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppKey> Search(Int32 userId, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得应用密钥字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>用户。密钥所属用户</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>名称。用户自定义标识，如业务系统A</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>密钥。sk-前缀的随机字符串，创建时仅展示一次</summary>
        public static readonly Field Secret = FindByName("Secret");

        /// <summary>可用模型。逗号分隔的模型名称或编码，为空时不限制</summary>
        public static readonly Field Models = FindByName("Models");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>过期时间。null表示永不过期</summary>
        public static readonly Field ExpireTime = FindByName("ExpireTime");

        /// <summary>最后调用时间</summary>
        public static readonly Field LastCallTime = FindByName("LastCallTime");

        /// <summary>调用次数。累计API请求数</summary>
        public static readonly Field Calls = FindByName("Calls");

        /// <summary>总Token数。累计消耗Token</summary>
        public static readonly Field TotalTokens = FindByName("TotalTokens");

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

    /// <summary>取得应用密钥字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户。密钥所属用户</summary>
        public const String UserId = "UserId";

        /// <summary>名称。用户自定义标识，如业务系统A</summary>
        public const String Name = "Name";

        /// <summary>密钥。sk-前缀的随机字符串，创建时仅展示一次</summary>
        public const String Secret = "Secret";

        /// <summary>可用模型。逗号分隔的模型名称或编码，为空时不限制</summary>
        public const String Models = "Models";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>过期时间。null表示永不过期</summary>
        public const String ExpireTime = "ExpireTime";

        /// <summary>最后调用时间</summary>
        public const String LastCallTime = "LastCallTime";

        /// <summary>调用次数。累计API请求数</summary>
        public const String Calls = "Calls";

        /// <summary>总Token数。累计消耗Token</summary>
        public const String TotalTokens = "TotalTokens";

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
