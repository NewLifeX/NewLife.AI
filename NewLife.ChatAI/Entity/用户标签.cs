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

/// <summary>用户标签。附加在用户画像上的特征标签，支持权重排序</summary>
[Serializable]
[DataObject]
[Description("用户标签。附加在用户画像上的特征标签，支持权重排序")]
[BindIndex("IU_UserTag_UserId_Name", true, "UserId,Name")]
[BindIndex("IX_UserTag_UserId_Category_Weight", false, "UserId,Category,Weight")]
[BindTable("UserTag", Description = "用户标签。附加在用户画像上的特征标签，支持权重排序", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class UserTag : IEntity<UserTagModel>
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

    private String _Name;
    /// <summary>标签名称</summary>
    [DisplayName("标签名称")]
    [Description("标签名称")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("Name", "标签名称", "", Master = true)]
    public String Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String _Category;
    /// <summary>分类。preference=偏好/habit=习惯/interest=兴趣/background=背景</summary>
    [DisplayName("分类")]
    [Description("分类。preference=偏好/habit=习惯/interest=兴趣/background=背景")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "分类。preference=偏好/habit=习惯/interest=兴趣/background=背景", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private Int32 _Weight;
    /// <summary>权重。0~100，越高越重要，用于排序显示</summary>
    [DisplayName("权重")]
    [Description("权重。0~100，越高越重要，用于排序显示")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Weight", "权重。0~100，越高越重要，用于排序显示", "")]
    public Int32 Weight { get => _Weight; set { if (OnPropertyChanging("Weight", value)) { _Weight = value; OnPropertyChanged("Weight"); } } }

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
    public void Copy(UserTagModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        Name = model.Name;
        Category = model.Category;
        Weight = model.Weight;
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
            "Name" => _Name,
            "Category" => _Category,
            "Weight" => _Weight,
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
                case "Name": _Name = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Weight": _Weight = value.ToInt(); break;
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
    public static UserTag FindById(Int32 id)
    {
        if (id <= 0) return null;
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);
        return Meta.SingleCache[id];
    }

    /// <summary>根据用户查找所有标签（按权重降序）</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<UserTag> FindAllByUserId(Int32 userId)
    {
        if (userId <= 0) return [];
        return FindAll(_.UserId == userId, _.Weight.Desc(), null, 0, 0);
    }

    /// <summary>根据用户和分类查找标签</summary>
    /// <param name="userId">用户</param>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<UserTag> FindAllByUserIdAndCategory(Int32 userId, String category)
    {
        if (userId <= 0) return [];
        var exp = _.UserId == userId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        return FindAll(exp, _.Weight.Desc(), null, 0, 0);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户</param>
    /// <param name="category">分类</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数</param>
    /// <returns>实体列表</returns>
    public static IList<UserTag> Search(Int32 userId, String category, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (userId >= 0) exp &= _.UserId == userId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得用户标签字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>用户</summary>
        public static readonly Field UserId = FindByName("UserId");
        /// <summary>标签名称</summary>
        public static readonly Field Name = FindByName("Name");
        /// <summary>分类</summary>
        public static readonly Field Category = FindByName("Category");
        /// <summary>权重</summary>
        public static readonly Field Weight = FindByName("Weight");
        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");
        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得用户标签字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";
        /// <summary>用户</summary>
        public const String UserId = "UserId";
        /// <summary>标签名称</summary>
        public const String Name = "Name";
        /// <summary>分类</summary>
        public const String Category = "Category";
        /// <summary>权重</summary>
        public const String Weight = "Weight";
        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
