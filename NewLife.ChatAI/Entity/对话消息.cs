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

/// <summary>对话消息。会话中的单条发言，包括用户消息和AI回复</summary>
[Serializable]
[DataObject]
[Description("对话消息。会话中的单条发言，包括用户消息和AI回复")]
[BindIndex("IX_ChatMessage_ConversationId_CreateTime", false, "ConversationId,CreateTime")]
[BindIndex("IX_ChatMessage_ParentMessageId", false, "ParentMessageId")]
[BindTable("ChatMessage", Description = "对话消息。会话中的单条发言，包括用户消息和AI回复", ConnName = "Cube", DbType = DatabaseType.None)]
public partial class ChatMessage : IEntity<ChatMessageModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ConversationId;
    /// <summary>会话。所属会话</summary>
    [DisplayName("会话")]
    [Description("会话。所属会话")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConversationId", "会话。所属会话", "")]
    public Int64 ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private String _Role;
    /// <summary>角色。User=用户, Assistant=AI助手</summary>
    [DisplayName("角色")]
    [Description("角色。User=用户, Assistant=AI助手")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Role", "角色。User=用户, Assistant=AI助手", "")]
    public String Role { get => _Role; set { if (OnPropertyChanging("Role", value)) { _Role = value; OnPropertyChanged("Role"); } } }

    private String _Content;
    /// <summary>内容。Markdown格式文本</summary>
    [DisplayName("内容")]
    [Description("内容。Markdown格式文本")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Content", "内容。Markdown格式文本", "", ShowIn = "Auto,-List,-Search")]
    public String Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

    private Int32 _ThinkingMode;
    /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
    [DisplayName("思考模式")]
    [Description("思考模式。Auto=0自动, Think=1思考, Fast=2快速")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThinkingMode", "思考模式。Auto=0自动, Think=1思考, Fast=2快速", "")]
    public Int32 ThinkingMode { get => _ThinkingMode; set { if (OnPropertyChanging("ThinkingMode", value)) { _ThinkingMode = value; OnPropertyChanged("ThinkingMode"); } } }

    private Int64 _ParentMessageId;
    /// <summary>父消息。编辑或重新生成时的分支引用</summary>
    [DisplayName("父消息")]
    [Description("父消息。编辑或重新生成时的分支引用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ParentMessageId", "父消息。编辑或重新生成时的分支引用", "")]
    public Int64 ParentMessageId { get => _ParentMessageId; set { if (OnPropertyChanging("ParentMessageId", value)) { _ParentMessageId = value; OnPropertyChanged("ParentMessageId"); } } }

    private String _Attachments;
    /// <summary>附件列表。JSON格式</summary>
    [DisplayName("附件列表")]
    [Description("附件列表。JSON格式")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("Attachments", "附件列表。JSON格式", "", ShowIn = "Auto,-List,-Search")]
    public String Attachments { get => _Attachments; set { if (OnPropertyChanging("Attachments", value)) { _Attachments = value; OnPropertyChanged("Attachments"); } } }

    private Int32 _CreateUserID;
    /// <summary>创建用户</summary>
    [Category("扩展")]
    [DisplayName("创建用户")]
    [Description("创建用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreateUserID", "创建用户", "")]
    public Int32 CreateUserID { get => _CreateUserID; set { if (OnPropertyChanging("CreateUserID", value)) { _CreateUserID = value; OnPropertyChanged("CreateUserID"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }

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

    private String _UpdateIP;
    /// <summary>更新地址</summary>
    [Category("扩展")]
    [DisplayName("更新地址")]
    [Description("更新地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateIP", "更新地址", "")]
    public String UpdateIP { get => _UpdateIP; set { if (OnPropertyChanging("UpdateIP", value)) { _UpdateIP = value; OnPropertyChanged("UpdateIP"); } } }

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
    public void Copy(ChatMessageModel model)
    {
        Id = model.Id;
        ConversationId = model.ConversationId;
        Role = model.Role;
        Content = model.Content;
        ThinkingMode = model.ThinkingMode;
        ParentMessageId = model.ParentMessageId;
        Attachments = model.Attachments;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
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
            "ConversationId" => _ConversationId,
            "Role" => _Role,
            "Content" => _Content,
            "ThinkingMode" => _ThinkingMode,
            "ParentMessageId" => _ParentMessageId,
            "Attachments" => _Attachments,
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
                case "Id": _Id = value.ToLong(); break;
                case "ConversationId": _ConversationId = value.ToLong(); break;
                case "Role": _Role = Convert.ToString(value); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "ThinkingMode": _ThinkingMode = value.ToInt(); break;
                case "ParentMessageId": _ParentMessageId = value.ToLong(); break;
                case "Attachments": _Attachments = Convert.ToString(value); break;
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
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ChatMessage FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据父消息查找</summary>
    /// <param name="parentMessageId">父消息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> FindAllByParentMessageId(Int64 parentMessageId)
    {
        if (parentMessageId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ParentMessageId == parentMessageId);

        return FindAll(_.ParentMessageId == parentMessageId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="conversationId">会话。所属会话</param>
    /// <param name="parentMessageId">父消息。编辑或重新生成时的分支引用</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> Search(Int64 conversationId, Int64 parentMessageId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (conversationId >= 0) exp &= _.ConversationId == conversationId;
        if (parentMessageId >= 0) exp &= _.ParentMessageId == parentMessageId;
        exp &= _.CreateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得对话消息字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话。所属会话</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");

        /// <summary>角色。User=用户, Assistant=AI助手</summary>
        public static readonly Field Role = FindByName("Role");

        /// <summary>内容。Markdown格式文本</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
        public static readonly Field ThinkingMode = FindByName("ThinkingMode");

        /// <summary>父消息。编辑或重新生成时的分支引用</summary>
        public static readonly Field ParentMessageId = FindByName("ParentMessageId");

        /// <summary>附件列表。JSON格式</summary>
        public static readonly Field Attachments = FindByName("Attachments");

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

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得对话消息字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>会话。所属会话</summary>
        public const String ConversationId = "ConversationId";

        /// <summary>角色。User=用户, Assistant=AI助手</summary>
        public const String Role = "Role";

        /// <summary>内容。Markdown格式文本</summary>
        public const String Content = "Content";

        /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
        public const String ThinkingMode = "ThinkingMode";

        /// <summary>父消息。编辑或重新生成时的分支引用</summary>
        public const String ParentMessageId = "ParentMessageId";

        /// <summary>附件列表。JSON格式</summary>
        public const String Attachments = "Attachments";

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
