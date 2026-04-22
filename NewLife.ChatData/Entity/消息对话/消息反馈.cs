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

namespace NewLife.ChatData.Entity;

/// <summary>消息反馈。用户对AI回复的点赞或点踩</summary>
[Serializable]
[DataObject]
[Description("消息反馈。用户对AI回复的点赞或点踩")]
[BindIndex("IU_MessageFeedback_MessageId_UserId", true, "MessageId,UserId")]
[BindIndex("IX_MessageFeedback_UserId", false, "UserId")]
[BindIndex("IX_MessageFeedback_ConversationId", false, "ConversationId")]
[BindTable("MessageFeedback", Description = "消息反馈。用户对AI回复的点赞或点踩", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class MessageFeedback : IMessageFeedback, IEntity<IMessageFeedback>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ConversationId;
    /// <summary>会话。被反馈的会话</summary>
    [DisplayName("会话")]
    [Description("会话。被反馈的会话")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConversationId", "会话。被反馈的会话", "")]
    public Int64 ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private Int64 _MessageId;
    /// <summary>消息。被反馈的消息</summary>
    [DisplayName("消息")]
    [Description("消息。被反馈的消息")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MessageId", "消息。被反馈的消息", "")]
    public Int64 MessageId { get => _MessageId; set { if (OnPropertyChanging("MessageId", value)) { _MessageId = value; OnPropertyChanged("MessageId"); } } }

    private Int32 _UserId;
    /// <summary>用户。反馈用户</summary>
    [DisplayName("用户")]
    [Description("用户。反馈用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户。反馈用户", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private NewLife.AI.Models.FeedbackType _FeedbackType;
    /// <summary>反馈类型。Like=1点赞, Dislike=2点踩</summary>
    [DisplayName("反馈类型")]
    [Description("反馈类型。Like=1点赞, Dislike=2点踩")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FeedbackType", "反馈类型。Like=1点赞, Dislike=2点踩", "")]
    public NewLife.AI.Models.FeedbackType FeedbackType { get => _FeedbackType; set { if (OnPropertyChanging("FeedbackType", value)) { _FeedbackType = value; OnPropertyChanged("FeedbackType"); } } }

    private String? _Reason;
    /// <summary>原因。点踩原因</summary>
    [DisplayName("原因")]
    [Description("原因。点踩原因")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Reason", "原因。点踩原因", "")]
    public String? Reason { get => _Reason; set { if (OnPropertyChanging("Reason", value)) { _Reason = value; OnPropertyChanged("Reason"); } } }

    private Boolean _AllowTraining;
    /// <summary>允许训练。是否允许用于模型学习训练</summary>
    [DisplayName("允许训练")]
    [Description("允许训练。是否允许用于模型学习训练")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AllowTraining", "允许训练。是否允许用于模型学习训练", "")]
    public Boolean AllowTraining { get => _AllowTraining; set { if (OnPropertyChanging("AllowTraining", value)) { _AllowTraining = value; OnPropertyChanged("AllowTraining"); } } }

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
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IMessageFeedback model)
    {
        Id = model.Id;
        ConversationId = model.ConversationId;
        MessageId = model.MessageId;
        UserId = model.UserId;
        FeedbackType = model.FeedbackType;
        Reason = model.Reason;
        AllowTraining = model.AllowTraining;
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
            "ConversationId" => _ConversationId,
            "MessageId" => _MessageId,
            "UserId" => _UserId,
            "FeedbackType" => _FeedbackType,
            "Reason" => _Reason,
            "AllowTraining" => _AllowTraining,
            "CreateUserID" => _CreateUserID,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            "UpdateUserID" => _UpdateUserID,
            "UpdateIP" => _UpdateIP,
            "UpdateTime" => _UpdateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToInt(); break;
                case "ConversationId": _ConversationId = value.ToLong(); break;
                case "MessageId": _MessageId = value.ToLong(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "FeedbackType": _FeedbackType = (NewLife.AI.Models.FeedbackType)value.ToInt(); break;
                case "Reason": _Reason = Convert.ToString(value); break;
                case "AllowTraining": _AllowTraining = value.ToBoolean(); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateUserID": _UpdateUserID = value.ToInt(); break;
                case "UpdateIP": _UpdateIP = Convert.ToString(value); break;
                case "UpdateTime": _UpdateTime = value.ToDateTime(); break;
                default: base[name] = value; break;
            }
        }
    }
    #endregion

    #region 关联映射
    /// <summary>会话</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Conversation? Conversation => Extends.Get(nameof(Conversation), k => Conversation.FindById(ConversationId));

    /// <summary>会话</summary>
    [Map(nameof(ConversationId), typeof(Conversation), "Id")]
    public String? Title => Conversation?.ToString();

    /// <summary>用户</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public XCode.Membership.User? User => Extends.Get(nameof(User), k => XCode.Membership.User.FindByID(UserId));

    /// <summary>用户</summary>
    [Map(nameof(UserId), typeof(XCode.Membership.User), "ID")]
    public String? UserName => User?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static MessageFeedback? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据消息、用户查找</summary>
    /// <param name="messageId">消息</param>
    /// <param name="userId">用户</param>
    /// <returns>实体对象</returns>
    public static MessageFeedback? FindByMessageIdAndUserId(Int64 messageId, Int32 userId)
    {
        if (messageId < 0) return null;
        if (userId < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.MessageId == messageId && e.UserId == userId);

        return Find(_.MessageId == messageId & _.UserId == userId);
    }

    /// <summary>根据消息查找</summary>
    /// <param name="messageId">消息</param>
    /// <returns>实体列表</returns>
    public static IList<MessageFeedback> FindAllByMessageId(Int64 messageId)
    {
        if (messageId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.MessageId == messageId);

        return FindAll(_.MessageId == messageId);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<MessageFeedback> FindAllByUserId(Int32 userId)
    {
        if (userId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.UserId == userId);

        return FindAll(_.UserId == userId);
    }

    /// <summary>根据会话查找</summary>
    /// <param name="conversationId">会话</param>
    /// <returns>实体列表</returns>
    public static IList<MessageFeedback> FindAllByConversationId(Int64 conversationId)
    {
        if (conversationId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.ConversationId == conversationId);

        return FindAll(_.ConversationId == conversationId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="conversationId">会话。被反馈的会话</param>
    /// <param name="messageId">消息。被反馈的消息</param>
    /// <param name="userId">用户。反馈用户</param>
    /// <param name="feedbackType">反馈类型。Like=1点赞, Dislike=2点踩</param>
    /// <param name="allowTraining">允许训练。是否允许用于模型学习训练</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<MessageFeedback> Search(Int64 conversationId, Int64 messageId, Int32 userId, NewLife.AI.Models.FeedbackType feedbackType, Boolean? allowTraining, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (conversationId >= 0) exp &= _.ConversationId == conversationId;
        if (messageId >= 0) exp &= _.MessageId == messageId;
        if (userId >= 0) exp &= _.UserId == userId;
        if (feedbackType >= 0) exp &= _.FeedbackType == feedbackType;
        if (allowTraining != null) exp &= _.AllowTraining == allowTraining;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得消息反馈字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话。被反馈的会话</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");

        /// <summary>消息。被反馈的消息</summary>
        public static readonly Field MessageId = FindByName("MessageId");

        /// <summary>用户。反馈用户</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>反馈类型。Like=1点赞, Dislike=2点踩</summary>
        public static readonly Field FeedbackType = FindByName("FeedbackType");

        /// <summary>原因。点踩原因</summary>
        public static readonly Field Reason = FindByName("Reason");

        /// <summary>允许训练。是否允许用于模型学习训练</summary>
        public static readonly Field AllowTraining = FindByName("AllowTraining");

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

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得消息反馈字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>会话。被反馈的会话</summary>
        public const String ConversationId = "ConversationId";

        /// <summary>消息。被反馈的消息</summary>
        public const String MessageId = "MessageId";

        /// <summary>用户。反馈用户</summary>
        public const String UserId = "UserId";

        /// <summary>反馈类型。Like=1点赞, Dislike=2点踩</summary>
        public const String FeedbackType = "FeedbackType";

        /// <summary>原因。点踩原因</summary>
        public const String Reason = "Reason";

        /// <summary>允许训练。是否允许用于模型学习训练</summary>
        public const String AllowTraining = "AllowTraining";

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
    }
    #endregion
}
