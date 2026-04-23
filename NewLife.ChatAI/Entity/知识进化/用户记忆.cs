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

/// <summary>用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据</summary>
[Serializable]
[DataObject]
[Description("用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据")]
[BindIndex("IX_UserMemory_UserId_ProjectId_Category_Key", false, "UserId,ProjectId,Category,Key")]
[BindIndex("IX_UserMemory_UserId_Category_Key", false, "UserId,Category,Key")]
[BindIndex("IX_UserMemory_UserId_Key", false, "UserId,Key")]
[BindIndex("IX_UserMemory_ConversationId", false, "ConversationId")]
[BindTable("UserMemory", Description = "用户记忆。AI从对话和反馈中提取的用户信息碎片，是自学习系统的原始数据", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class UserMemory : IUserMemory, IEntity<IUserMemory>
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

    private Int32 _ProjectId;
    /// <summary>项目。0=全局记忆</summary>
    [DisplayName("项目")]
    [Description("项目。0=全局记忆")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目。0=全局记忆", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private Int64 _ConversationId;
    /// <summary>来源会话。提取该记忆的会话编号</summary>
    [DisplayName("来源会话")]
    [Description("来源会话。提取该记忆的会话编号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConversationId", "来源会话。提取该记忆的会话编号", "")]
    public Int64 ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private String? _Category;
    /// <summary>分类。偏好/习惯/兴趣/背景</summary>
    [DisplayName("分类")]
    [Description("分类。偏好/习惯/兴趣/背景")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "分类。偏好/习惯/兴趣/背景", "")]
    public String? Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String? _Key;
    /// <summary>主题。记忆的关键词/主题，如编程语言、工作行业</summary>
    [DisplayName("主题")]
    [Description("主题。记忆的关键词/主题，如编程语言、工作行业")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Key", "主题。记忆的关键词/主题，如编程语言、工作行业", "", Master = true)]
    public String? Key { get => _Key; set { if (OnPropertyChanging("Key", value)) { _Key = value; OnPropertyChanged("Key"); } } }

    private String? _Value;
    /// <summary>内容。提取到的具体信息</summary>
    [DisplayName("内容")]
    [Description("内容。提取到的具体信息")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Value", "内容。提取到的具体信息", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? Value { get => _Value; set { if (OnPropertyChanging("Value", value)) { _Value = value; OnPropertyChanged("Value"); } } }

    private Int32 _Confidence;
    /// <summary>置信度。0~100，越高越可信</summary>
    [DisplayName("置信度")]
    [Description("置信度。0~100，越高越可信")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Confidence", "置信度。0~100，越高越可信", "")]
    public Int32 Confidence { get => _Confidence; set { if (OnPropertyChanging("Confidence", value)) { _Confidence = value; OnPropertyChanged("Confidence"); } } }

    private String? _Scope;
    /// <summary>作用域。user=用户级/team=团队级/global=全局</summary>
    [DisplayName("作用域")]
    [Description("作用域。user=用户级/team=团队级/global=全局")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Scope", "作用域。user=用户级/team=团队级/global=全局", "")]
    public String? Scope { get => _Scope; set { if (OnPropertyChanging("Scope", value)) { _Scope = value; OnPropertyChanged("Scope"); } } }

    private Int32 _Status;
    /// <summary>状态。0=待审核/1=已生效/2=已拒绝/3=已废弃</summary>
    [DisplayName("状态")]
    [Description("状态。0=待审核/1=已生效/2=已拒绝/3=已废弃")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Status", "状态。0=待审核/1=已生效/2=已拒绝/3=已废弃", "")]
    public Int32 Status { get => _Status; set { if (OnPropertyChanging("Status", value)) { _Status = value; OnPropertyChanged("Status"); } } }

    private Int32 _ReviewUserId;
    /// <summary>审核人</summary>
    [DisplayName("审核人")]
    [Description("审核人")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ReviewUserId", "审核人", "")]
    public Int32 ReviewUserId { get => _ReviewUserId; set { if (OnPropertyChanging("ReviewUserId", value)) { _ReviewUserId = value; OnPropertyChanged("ReviewUserId"); } } }

    private DateTime _ReviewTime;
    /// <summary>审核时间</summary>
    [DisplayName("审核时间")]
    [Description("审核时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ReviewTime", "审核时间", "")]
    public DateTime ReviewTime { get => _ReviewTime; set { if (OnPropertyChanging("ReviewTime", value)) { _ReviewTime = value; OnPropertyChanged("ReviewTime"); } } }

    private Int32 _Version;
    /// <summary>版本号。每次修改递增</summary>
    [DisplayName("版本号")]
    [Description("版本号。每次修改递增")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Version", "版本号。每次修改递增", "")]
    public Int32 Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private Int64 _ParentId;
    /// <summary>父记忆。融合来源，0表示原始提取</summary>
    [DisplayName("父记忆")]
    [Description("父记忆。融合来源，0表示原始提取")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ParentId", "父记忆。融合来源，0表示原始提取", "")]
    public Int64 ParentId { get => _ParentId; set { if (OnPropertyChanging("ParentId", value)) { _ParentId = value; OnPropertyChanged("ParentId"); } } }

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

    private String? _TraceId;
    /// <summary>链路。方便问题排查</summary>
    [Category("扩展")]
    [DisplayName("链路")]
    [Description("链路。方便问题排查")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "链路。方便问题排查", "")]
    public String? TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

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
    public void Copy(IUserMemory model)
    {
        Id = model.Id;
        UserId = model.UserId;
        ProjectId = model.ProjectId;
        ConversationId = model.ConversationId;
        Category = model.Category;
        Key = model.Key;
        Value = model.Value;
        Confidence = model.Confidence;
        Scope = model.Scope;
        Status = model.Status;
        ReviewUserId = model.ReviewUserId;
        ReviewTime = model.ReviewTime;
        Version = model.Version;
        ParentId = model.ParentId;
        Enable = model.Enable;
        ExpireTime = model.ExpireTime;
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
            "ConversationId" => _ConversationId,
            "Category" => _Category,
            "Key" => _Key,
            "Value" => _Value,
            "Confidence" => _Confidence,
            "Scope" => _Scope,
            "Status" => _Status,
            "ReviewUserId" => _ReviewUserId,
            "ReviewTime" => _ReviewTime,
            "Version" => _Version,
            "ParentId" => _ParentId,
            "Enable" => _Enable,
            "ExpireTime" => _ExpireTime,
            "TraceId" => _TraceId,
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
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "ConversationId": _ConversationId = value.ToLong(); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Key": _Key = Convert.ToString(value); break;
                case "Value": _Value = Convert.ToString(value); break;
                case "Confidence": _Confidence = value.ToInt(); break;
                case "Scope": _Scope = Convert.ToString(value); break;
                case "Status": _Status = value.ToInt(); break;
                case "ReviewUserId": _ReviewUserId = value.ToInt(); break;
                case "ReviewTime": _ReviewTime = value.ToDateTime(); break;
                case "Version": _Version = value.ToInt(); break;
                case "ParentId": _ParentId = value.ToLong(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "ExpireTime": _ExpireTime = value.ToDateTime(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
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

    /// <summary>来源会话</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Conversation? Conversation => Extends.Get(nameof(Conversation), k => Conversation.FindById(ConversationId));

    /// <summary>来源会话</summary>
    [Map(nameof(ConversationId), typeof(Conversation), "Id")]
    public String? Title => Conversation?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static UserMemory? FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据用户、分类、主题查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="category">分类</param>
    /// <param name="key">主题</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindAllByUserIdAndCategoryAndKey(Int32 userId, String? category, String? key)
    {
        if (userId < 0) return [];
        if (category == null) return [];
        if (key == null) return [];

        return FindAll(_.UserId == userId & _.Category == category & _.Key == key);
    }

    /// <summary>根据用户、主题查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="key">主题</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindAllByUserIdAndKey(Int32 userId, String? key)
    {
        if (userId < 0) return [];
        if (key == null) return [];

        return FindAll(_.UserId == userId & _.Key == key);
    }

    /// <summary>根据来源会话查找</summary>
    /// <param name="conversationId">来源会话</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindAllByConversationId(Int64 conversationId)
    {
        if (conversationId < 0) return [];

        return FindAll(_.ConversationId == conversationId);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> FindAllByUserId(Int32 userId)
    {
        if (userId < 0) return [];

        return FindAll(_.UserId == userId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户</param>
    /// <param name="projectId">项目。0=全局记忆</param>
    /// <param name="conversationId">来源会话。提取该记忆的会话编号</param>
    /// <param name="category">分类。偏好/习惯/兴趣/背景</param>
    /// <param name="enable">启用</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UserMemory> Search(Int32 userId, Int32 projectId, Int64 conversationId, String? category, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (conversationId >= 0) exp &= _.ConversationId == conversationId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.Id.Between(start, end, Meta.Factory.Snow);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 数据清理
    /// <summary>清理指定时间段内的数据</summary>
    /// <param name="start">开始时间。未指定时清理小于指定时间的所有数据</param>
    /// <param name="end">结束时间</param>
    /// <param name="maximumRows">最大删除行数。清理历史数据时，避免一次性删除过多导致数据库IO跟不上，0表示所有</param>
    /// <returns>清理行数</returns>
    public static Int32 DeleteWith(DateTime start, DateTime end, Int32 maximumRows = 0)
    {
        return Delete(_.Id.Between(start, end, Meta.Factory.Snow), maximumRows);
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

        /// <summary>项目。0=全局记忆</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>来源会话。提取该记忆的会话编号</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");

        /// <summary>分类。偏好/习惯/兴趣/背景</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>主题。记忆的关键词/主题，如编程语言、工作行业</summary>
        public static readonly Field Key = FindByName("Key");

        /// <summary>内容。提取到的具体信息</summary>
        public static readonly Field Value = FindByName("Value");

        /// <summary>置信度。0~100，越高越可信</summary>
        public static readonly Field Confidence = FindByName("Confidence");

        /// <summary>作用域。user=用户级/team=团队级/global=全局</summary>
        public static readonly Field Scope = FindByName("Scope");

        /// <summary>状态。0=待审核/1=已生效/2=已拒绝/3=已废弃</summary>
        public static readonly Field Status = FindByName("Status");

        /// <summary>审核人</summary>
        public static readonly Field ReviewUserId = FindByName("ReviewUserId");

        /// <summary>审核时间</summary>
        public static readonly Field ReviewTime = FindByName("ReviewTime");

        /// <summary>版本号。每次修改递增</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>父记忆。融合来源，0表示原始提取</summary>
        public static readonly Field ParentId = FindByName("ParentId");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>过期时间。null表示永不过期</summary>
        public static readonly Field ExpireTime = FindByName("ExpireTime");

        /// <summary>链路。方便问题排查</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新时间</summary>
        public static readonly Field UpdateTime = FindByName("UpdateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得用户记忆字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户</summary>
        public const String UserId = "UserId";

        /// <summary>项目。0=全局记忆</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>来源会话。提取该记忆的会话编号</summary>
        public const String ConversationId = "ConversationId";

        /// <summary>分类。偏好/习惯/兴趣/背景</summary>
        public const String Category = "Category";

        /// <summary>主题。记忆的关键词/主题，如编程语言、工作行业</summary>
        public const String Key = "Key";

        /// <summary>内容。提取到的具体信息</summary>
        public const String Value = "Value";

        /// <summary>置信度。0~100，越高越可信</summary>
        public const String Confidence = "Confidence";

        /// <summary>作用域。user=用户级/team=团队级/global=全局</summary>
        public const String Scope = "Scope";

        /// <summary>状态。0=待审核/1=已生效/2=已拒绝/3=已废弃</summary>
        public const String Status = "Status";

        /// <summary>审核人</summary>
        public const String ReviewUserId = "ReviewUserId";

        /// <summary>审核时间</summary>
        public const String ReviewTime = "ReviewTime";

        /// <summary>版本号。每次修改递增</summary>
        public const String Version = "Version";

        /// <summary>父记忆。融合来源，0表示原始提取</summary>
        public const String ParentId = "ParentId";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>过期时间。null表示永不过期</summary>
        public const String ExpireTime = "ExpireTime";

        /// <summary>链路。方便问题排查</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新时间</summary>
        public const String UpdateTime = "UpdateTime";
    }
    #endregion
}
