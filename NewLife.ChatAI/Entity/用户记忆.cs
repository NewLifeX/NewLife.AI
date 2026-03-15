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

/// <summary>用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据</summary>
[Serializable]
[DataObject]
[Description("用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据")]
[BindIndex("IX_UserMemory_UserId_Category_Key", false, "UserId,Category,Key")]
[BindIndex("IX_UserMemory_UserId_IsActive_Id", false, "UserId,IsActive,Id")]
[BindIndex("IX_UserMemory_ConversationId", false, "ConversationId")]
[BindTable("UserMemory", Description = "用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class UserMemory : IEntity<UserMemoryModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _UserId;
    /// <summary>用户</summary>
    [DisplayName("用户")]
    [Description("用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private Int64 _ConversationId;
    /// <summary>来源会话。提取该记忆的会话编号</summary>
    [DisplayName("来源会话")]
    [Description("来源会话。提取该记忆的会话编号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConversationId", "来源会话。提取该记忆的会话编号", "")]
    public Int64 ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private String _Category;
    /// <summary>分类。preference=偏好/habit=习惯/interest=兴趣/background=背景</summary>
    [DisplayName("分类")]
    [Description("分类。preference=偏好/habit=习惯/interest=兴趣/background=背景")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "分类。preference=偏好/habit=习惯/interest=兴趣/background=背景", "")]
    public String Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String _Key;
    /// <summary>主题。记忆的关键词/主题，如编程语言、工作行业</summary>
    [DisplayName("主题")]
    [Description("主题。记忆的关键词/主题，如编程语言、工作行业")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Key", "主题。记忆的关键词/主题，如编程语言、工作行业", "", Master = true)]
    public String Key { get => _Key; set { if (OnPropertyChanging("Key", value)) { _Key = value; OnPropertyChanged("Key"); } } }

    private String _Value;
    /// <summary>内容。提取到的具体信息</summary>
    [DisplayName("内容")]
    [Description("内容。提取到的具体信息")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Value", "内容。提取到的具体信息", "", ShowIn = "Auto,-List,-Search")]
    public String Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private Int32 _Confidence;
    /// <summary>置信度。0~100，越高越可信</summary>
    [DisplayName("置信度")]
    [Description("置信度。0~100，越高越可信")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Confidence", "置信度。0~100，越高越可信", "")]
    public Int32 Confidence { get => _Confidence; set { if (OnPropertyChanging("Confidence", value)) { _Confidence = value; OnPropertyChanged("Confidence"); } } }

    private Boolean _IsActive;
    /// <summary>有效。是否仍然有效，可被覆盖或废弃</summary>
    [DisplayName("有效")]
    [Description("有效。是否仍然有效，可被覆盖或废弃")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsActive", "有效。是否仍然有效，可被覆盖或废弃", "")]
    public Boolean IsActive { get => _IsActive; set { if (OnPropertyChanging("IsActive", value)) { _IsActive = value; OnPropertyChanged("IsActive"); } } }

    private DateTime _ExpireTime;
    /// <summary>过期时间。null表示永不过期</summary>
    [DisplayName("过期时间")]
    [Description("过期时间。null表示永不过期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ExpireTime", "过期时间。null表示永不过期", "")]
    public DateTime ExpireTime { get => _ExpireTime; set { if (OnPropertyChanging("ExpireTime", value)) { _ExpireTime = value; OnPropertyChanged("ExpireTime"); } } }

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
    public void Copy(UserMemoryModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        ConversationId = model.ConversationId;
        Category = model.Category;
        Key = model.Key;
        Value = model.Value;
        Confidence = model.Confidence;
        IsActive = model.IsActive;
        ExpireTime = model.ExpireTime;
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
            "ConversationId" => _ConversationId,
            "Category" => _Category,
            "Key" => _Key,
            "Value" => _Value,
            "Confidence" => _Confidence,
            "IsActive" => _IsActive,
            "ExpireTime" => _ExpireTime,
            "CreateTime" => _CreateTime,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "ConversationId": _ConversationId = value.ToLong(); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Key": _Key = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Confidence": _Confidence = value.ToInt(); break;
                case "IsActive": _IsActive = value.ToBoolean(); break;
                case "ExpireTime": _ExpireTime = value.ToDateTime(); break;
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
    public static UserMemory FindById(Int64 id)
    {
        if (id <= 0) return null;
        return Find(_.Id == id);
    }

    /// <summary>根据用户查找所有有效记忆</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindAllByUserId(Int32 userId)
    {
        if (userId <= 0) return [];
        return FindAll(_.UserId == userId & _.IsActive == true);
    }

    /// <summary>根据用户和键名精确查找记忆</summary>
    /// <param name="userId">用户</param>
    /// <param name="key">键名</param>
    /// <returns>实体对象，不存在返回 null</returns>
    public static UserMemory? FindByUserIdAndKey(Int32 userId, String key)
    {
        if (userId <= 0 || key.IsNullOrEmpty()) return null;
        return Find(_.UserId == userId & _.Key == key);
    }

    /// <summary>获取用户有效记忆，按置信度降序</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindActiveByUserId(Int32 userId)
    {
        if (userId <= 0) return [];
        return FindAll(_.UserId == userId & _.IsActive == true, _.Confidence.Desc(), null, 0, 0);
    }

    /// <summary>根据用户和分类查找记忆</summary>
    /// <param name="userId">用户</param>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindAllByUserIdAndCategory(Int32 userId, String category)
    {
        if (userId <= 0) return [];
        var exp = _.UserId == userId & _.IsActive == true;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        return FindAll(exp);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户</param>
    /// <param name="category">分类</param>
    /// <param name="isActive">有效</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> Search(Int32 userId, String category, Boolean? isActive, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();
        if (userId >= 0) exp &= _.UserId == userId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (isActive != null) exp &= _.IsActive == isActive;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Key.Contains(key) | _.Value.Contains(key);
        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得用户记忆字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");
        /// <summary>用户</summary>
        public static readonly Field UserId = FindByName("UserId");
        /// <summary>来源会话</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");
        /// <summary>分类</summary>
        public static readonly Field Category = FindByName("Category");
        /// <summary>主题</summary>
        public static readonly Field Key = FindByName("Key");
        /// <summary>内容</summary>
        public static readonly Field Value = FindByName("Value");
        /// <summary>置信度</summary>
        public static readonly Field Confidence = FindByName("Confidence");
        /// <summary>有效</summary>
        public static readonly Field IsActive = FindByName("IsActive");
        /// <summary>过期时间</summary>
        public static readonly Field ExpireTime = FindByName("ExpireTime");
        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");
        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得用户记忆字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";
        /// <summary>用户</summary>
        public const String UserId = "UserId";
        /// <summary>来源会话</summary>
        public const String ConversationId = "ConversationId";
        /// <summary>分类</summary>
        public const String Category = "Category";
        /// <summary>主题</summary>
        public const String Key = "Key";
        /// <summary>内容</summary>
        public const String Value = "Value";
        /// <summary>置信度</summary>
        public const String Confidence = "Confidence";
        /// <summary>有效</summary>
        public const String IsActive = "IsActive";
        /// <summary>过期时间</summary>
        public const String ExpireTime = "ExpireTime";
        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
