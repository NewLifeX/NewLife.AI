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

/// <summary>用户技能。记录用户最近使用的技能，用于SkillBar展示排序</summary>
[Serializable]
[DataObject]
[Description("用户技能。记录用户最近使用的技能，用于SkillBar展示排序")]
[BindIndex("IU_UserSkill_UserId_SkillId", true, "UserId,SkillId")]
[BindIndex("IX_UserSkill_UserId_LastUseTime", false, "UserId,LastUseTime")]
[BindTable("UserSkill", Description = "用户技能。记录用户最近使用的技能，用于SkillBar展示排序", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class UserSkill : IEntity<UserSkillModel>
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

    private Int32 _SkillId;
    /// <summary>技能</summary>
    [DisplayName("技能")]
    [Description("技能")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SkillId", "技能", "")]
    public Int32 SkillId { get => _SkillId; set { if (OnPropertyChanging("SkillId", value)) { _SkillId = value; OnPropertyChanged("SkillId"); } } }

    private DateTime _LastUseTime;
    /// <summary>最后使用时间。用于SkillBar按最近使用排序</summary>
    [DisplayName("最后使用时间")]
    [Description("最后使用时间。用于SkillBar按最近使用排序")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastUseTime", "最后使用时间。用于SkillBar按最近使用排序", "")]
    public DateTime LastUseTime { get => _LastUseTime; set { if (OnPropertyChanging("LastUseTime", value)) { _LastUseTime = value; OnPropertyChanged("LastUseTime"); } } }

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
    public void Copy(UserSkillModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        SkillId = model.SkillId;
        LastUseTime = model.LastUseTime;
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
            "SkillId" => _SkillId,
            "LastUseTime" => _LastUseTime,
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
                case "SkillId": _SkillId = value.ToInt(); break;
                case "LastUseTime": _LastUseTime = value.ToDateTime(); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>技能</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Skill Skill => Extends.Get(nameof(Skill), k => Skill.FindById(SkillId));

    /// <summary>技能</summary>
    [Map(nameof(SkillId), typeof(Skill), "Id")]
    public String SkillName => Skill?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static UserSkill FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据用户、技能查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="skillId">技能</param>
    /// <returns>实体对象</returns>
    public static UserSkill FindByUserIdAndSkillId(Int32 userId, Int32 skillId)
    {
        if (userId < 0) return null;
        if (skillId < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserId == userId && e.SkillId == skillId);

        return Find(_.UserId == userId & _.SkillId == skillId);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<UserSkill> FindAllByUserId(Int32 userId)
    {
        if (userId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.UserId == userId);

        return FindAll(_.UserId == userId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户</param>
    /// <param name="skillId">技能</param>
    /// <param name="start">最后使用时间开始</param>
    /// <param name="end">最后使用时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UserSkill> Search(Int32 userId, Int32 skillId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (skillId >= 0) exp &= _.SkillId == skillId;
        exp &= _.LastUseTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得用户技能字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>用户</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>技能</summary>
        public static readonly Field SkillId = FindByName("SkillId");

        /// <summary>最后使用时间。用于SkillBar按最近使用排序</summary>
        public static readonly Field LastUseTime = FindByName("LastUseTime");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得用户技能字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户</summary>
        public const String UserId = "UserId";

        /// <summary>技能</summary>
        public const String SkillId = "SkillId";

        /// <summary>最后使用时间。用于SkillBar按最近使用排序</summary>
        public const String LastUseTime = "LastUseTime";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
