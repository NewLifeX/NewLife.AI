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

/// <summary>用户画像。对用户记忆的汇总分析结果，每人一条</summary>
[Serializable]
[DataObject]
[Description("用户画像。对用户记忆的汇总分析结果，每人一条")]
[BindIndex("IU_UserProfile_UserId", true, "UserId")]
[BindTable("UserProfile", Description = "用户画像。对用户记忆的汇总分析结果，每人一条", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class UserProfile : IEntity<UserProfileModel>
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
    /// <summary>用户</summary>
    [DisplayName("用户")]
    [Description("用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private String _Summary;
    /// <summary>总结。AI生成的用户综合描述</summary>
    [DisplayName("总结")]
    [Description("总结。AI生成的用户综合描述")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Summary", "总结。AI生成的用户综合描述", "", ShowIn = "Auto,-List,-Search")]
    public String Summary { get => _Summary; set { if (OnPropertyChanging("Summary", value)) { _Summary = value; OnPropertyChanged("Summary"); } } }

    private String _Preferences;
    /// <summary>偏好。json格式，存储用户偏好键值对</summary>
    [DisplayName("偏好")]
    [Description("偏好。json格式，存储用户偏好键值对")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Preferences", "偏好。json格式，存储用户偏好键值对", "", ShowIn = "Auto,-List,-Search")]
    public String Preferences { get => _Preferences; set { if (OnPropertyChanging("Preferences", value)) { _Preferences = value; OnPropertyChanged("Preferences"); } } }

    private String _Habits;
    /// <summary>习惯。json格式，存储行为习惯</summary>
    [DisplayName("习惯")]
    [Description("习惯。json格式，存储行为习惯")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Habits", "习惯。json格式，存储行为习惯", "", ShowIn = "Auto,-List,-Search")]
    public String Habits { get => _Habits; set { if (OnPropertyChanging("Habits", value)) { _Habits = value; OnPropertyChanged("Habits"); } } }

    private String _Interests;
    /// <summary>兴趣。json格式，存储兴趣爱好列表</summary>
    [DisplayName("兴趣")]
    [Description("兴趣。json格式，存储兴趣爱好列表")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Interests", "兴趣。json格式，存储兴趣爱好列表", "", ShowIn = "Auto,-List,-Search")]
    public String Interests { get => _Interests; set { if (OnPropertyChanging("Interests", value)) { _Interests = value; OnPropertyChanged("Interests"); } } }

    private Int32 _MemoryCount;
    /// <summary>记忆数量。关联的有效记忆碎片总数</summary>
    [DisplayName("记忆数量")]
    [Description("记忆数量。关联的有效记忆碎片总数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MemoryCount", "记忆数量。关联的有效记忆碎片总数", "")]
    public Int32 MemoryCount { get => _MemoryCount; set { if (OnPropertyChanging("MemoryCount", value)) { _MemoryCount = value; OnPropertyChanged("MemoryCount"); } } }

    private DateTime _LastAnalyzeTime;
    /// <summary>最后分析时间</summary>
    [DisplayName("最后分析时间")]
    [Description("最后分析时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastAnalyzeTime", "最后分析时间", "")]
    public DateTime LastAnalyzeTime { get => _LastAnalyzeTime; set { if (OnPropertyChanging("LastAnalyzeTime", value)) { _LastAnalyzeTime = value; OnPropertyChanged("LastAnalyzeTime"); } } }

    private Int32 _AnalyzeCount;
    /// <summary>分析次数。累计执行自学习分析的次数</summary>
    [DisplayName("分析次数")]
    [Description("分析次数。累计执行自学习分析的次数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AnalyzeCount", "分析次数。累计执行自学习分析的次数", "")]
    public Int32 AnalyzeCount { get => _AnalyzeCount; set { if (OnPropertyChanging("AnalyzeCount", value)) { _AnalyzeCount = value; OnPropertyChanged("AnalyzeCount"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

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
    public void Copy(UserProfileModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        Summary = model.Summary;
        Preferences = model.Preferences;
        Habits = model.Habits;
        Interests = model.Interests;
        MemoryCount = model.MemoryCount;
        LastAnalyzeTime = model.LastAnalyzeTime;
        AnalyzeCount = model.AnalyzeCount;
        CreateTime = model.CreateTime;
        UpdateTime = model.UpdateTime;
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
            "UserId" => _UserId,
            "Summary" => _Summary,
            "Preferences" => _Preferences,
            "Habits" => _Habits,
            "Interests" => _Interests,
            "MemoryCount" => _MemoryCount,
            "LastAnalyzeTime" => _LastAnalyzeTime,
            "AnalyzeCount" => _AnalyzeCount,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "Summary": _Summary = Convert.ToString(value); break;
                case "Preferences": _Preferences = Convert.ToString(value); break;
                case "Habits": _Habits = Convert.ToString(value); break;
                case "Interests": _Interests = Convert.ToString(value); break;
                case "MemoryCount": _MemoryCount = value.ToInt(); break;
                case "LastAnalyzeTime": _LastAnalyzeTime = value.ToDateTime(); break;
                case "AnalyzeCount": _AnalyzeCount = value.ToInt(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
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
    public static UserProfile FindById(Int32 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>根据用户查找画像（每用户唯一）</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体对象</returns>
    public static UserProfile FindByUserId(Int32 userId)
    {
        if (userId <= 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.UserId == userId);
        return Find(_.UserId == userId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数</param>
    /// <returns>实体列表</returns>
    public static IList<UserProfile> Search(Int32 userId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (userId >= 0) exp &= _.UserId == userId;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Summary.Contains(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得用户画像字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>用户</summary>
        public static readonly Field UserId = FindByName("UserId");
        /// <summary>总结</summary>
        public static readonly Field Summary = FindByName("Summary");
        /// <summary>偏好</summary>
        public static readonly Field Preferences = FindByName("Preferences");
        /// <summary>习惯</summary>
        public static readonly Field Habits = FindByName("Habits");
        /// <summary>兴趣</summary>
        public static readonly Field Interests = FindByName("Interests");
        /// <summary>记忆数量</summary>
        public static readonly Field MemoryCount = FindByName("MemoryCount");
        /// <summary>最后分析时间</summary>
        public static readonly Field LastAnalyzeTime = FindByName("LastAnalyzeTime");
        /// <summary>分析次数</summary>
        public static readonly Field AnalyzeCount = FindByName("AnalyzeCount");
        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");
        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得用户画像字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";
        /// <summary>用户</summary>
        public const String UserId = "UserId";
        /// <summary>总结</summary>
        public const String Summary = "Summary";
        /// <summary>偏好</summary>
        public const String Preferences = "Preferences";
        /// <summary>习惯</summary>
        public const String Habits = "Habits";
        /// <summary>兴趣</summary>
        public const String Interests = "Interests";
        /// <summary>记忆数量</summary>
        public const String MemoryCount = "MemoryCount";
        /// <summary>最后分析时间</summary>
        public const String LastAnalyzeTime = "LastAnalyzeTime";
        /// <summary>分析次数</summary>
        public const String AnalyzeCount = "AnalyzeCount";
        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
