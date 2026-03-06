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

/// <summary>共享会话。通过链接分享的对话快照</summary>
[Serializable]
[DataObject]
[Description("共享会话。通过链接分享的对话快照")]
[BindIndex("IU_SharedConversation_ShareToken", true, "ShareToken")]
[BindIndex("IX_SharedConversation_ConversationId_Id", false, "ConversationId,Id")]
[BindIndex("IX_SharedConversation_CreatorUserId_Id", false, "CreatorUserId,Id")]
[BindTable("SharedConversation", Description = "共享会话。通过链接分享的对话快照", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class SharedConversation : IEntity<SharedConversationModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, false, false, 0)]
    [BindColumn("Id", "编号", "", DataScale = "time")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _ConversationId;
    /// <summary>会话。被分享的会话</summary>
    [DisplayName("会话")]
    [Description("会话。被分享的会话")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConversationId", "会话。被分享的会话", "")]
    public Int64 ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private String _ShareToken;
    /// <summary>分享令牌。唯一标识，用于生成分享URL</summary>
    [DisplayName("分享令牌")]
    [Description("分享令牌。唯一标识，用于生成分享URL")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ShareToken", "分享令牌。唯一标识，用于生成分享URL", "")]
    public String ShareToken { get => _ShareToken; set { if (OnPropertyChanging("ShareToken", value)) { _ShareToken = value; OnPropertyChanged("ShareToken"); } } }

    private Int64 _SnapshotMessageId;
    /// <summary>快照消息。截止到的最后一条消息编号</summary>
    [DisplayName("快照消息")]
    [Description("快照消息。截止到的最后一条消息编号")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SnapshotMessageId", "快照消息。截止到的最后一条消息编号", "")]
    public Int64 SnapshotMessageId { get => _SnapshotMessageId; set { if (OnPropertyChanging("SnapshotMessageId", value)) { _SnapshotMessageId = value; OnPropertyChanged("SnapshotMessageId"); } } }

    private Int32 _CreatorUserId;
    /// <summary>创建者。分享发起用户</summary>
    [DisplayName("创建者")]
    [Description("创建者。分享发起用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CreatorUserId", "创建者。分享发起用户", "")]
    public Int32 CreatorUserId { get => _CreatorUserId; set { if (OnPropertyChanging("CreatorUserId", value)) { _CreatorUserId = value; OnPropertyChanged("CreatorUserId"); } } }

    private DateTime _ExpireTime;
    /// <summary>过期时间。null表示永不过期</summary>
    [DisplayName("过期时间")]
    [Description("过期时间。null表示永不过期")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("ExpireTime", "过期时间。null表示永不过期", "")]
    public DateTime ExpireTime { get => _ExpireTime; set { if (OnPropertyChanging("ExpireTime", value)) { _ExpireTime = value; OnPropertyChanged("ExpireTime"); } } }

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
    public void Copy(SharedConversationModel model)
    {
        Id = model.Id;
        ConversationId = model.ConversationId;
        ShareToken = model.ShareToken;
        SnapshotMessageId = model.SnapshotMessageId;
        CreatorUserId = model.CreatorUserId;
        ExpireTime = model.ExpireTime;
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
            "ShareToken" => _ShareToken,
            "SnapshotMessageId" => _SnapshotMessageId,
            "CreatorUserId" => _CreatorUserId,
            "ExpireTime" => _ExpireTime,
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
                case "ShareToken": _ShareToken = Convert.ToString(value); break;
                case "SnapshotMessageId": _SnapshotMessageId = value.ToLong(); break;
                case "CreatorUserId": _CreatorUserId = value.ToInt(); break;
                case "ExpireTime": _ExpireTime = value.ToDateTime(); break;
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
    public static SharedConversation FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据分享令牌查找</summary>
    /// <param name="shareToken">分享令牌</param>
    /// <returns>实体对象</returns>
    public static SharedConversation FindByShareToken(String shareToken)
    {
        if (shareToken.IsNullOrEmpty()) return null;

        return Find(_.ShareToken == shareToken);
    }

    /// <summary>根据会话查找</summary>
    /// <param name="conversationId">会话</param>
    /// <returns>实体列表</returns>
    public static IList<SharedConversation> FindAllByConversationId(Int64 conversationId)
    {
        if (conversationId < 0) return [];

        return FindAll(_.ConversationId == conversationId);
    }

    /// <summary>根据创建者查找</summary>
    /// <param name="creatorUserId">创建者</param>
    /// <returns>实体列表</returns>
    public static IList<SharedConversation> FindAllByCreatorUserId(Int32 creatorUserId)
    {
        if (creatorUserId < 0) return [];

        return FindAll(_.CreatorUserId == creatorUserId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="conversationId">会话。被分享的会话</param>
    /// <param name="shareToken">分享令牌。唯一标识，用于生成分享URL</param>
    /// <param name="creatorUserId">创建者。分享发起用户</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<SharedConversation> Search(Int64 conversationId, String shareToken, Int32 creatorUserId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (conversationId >= 0) exp &= _.ConversationId == conversationId;
        if (!shareToken.IsNullOrEmpty()) exp &= _.ShareToken == shareToken;
        if (creatorUserId >= 0) exp &= _.CreatorUserId == creatorUserId;
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
    /// <summary>取得共享会话字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话。被分享的会话</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");

        /// <summary>分享令牌。唯一标识，用于生成分享URL</summary>
        public static readonly Field ShareToken = FindByName("ShareToken");

        /// <summary>快照消息。截止到的最后一条消息编号</summary>
        public static readonly Field SnapshotMessageId = FindByName("SnapshotMessageId");

        /// <summary>创建者。分享发起用户</summary>
        public static readonly Field CreatorUserId = FindByName("CreatorUserId");

        /// <summary>过期时间。null表示永不过期</summary>
        public static readonly Field ExpireTime = FindByName("ExpireTime");

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

    /// <summary>取得共享会话字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>会话。被分享的会话</summary>
        public const String ConversationId = "ConversationId";

        /// <summary>分享令牌。唯一标识，用于生成分享URL</summary>
        public const String ShareToken = "ShareToken";

        /// <summary>快照消息。截止到的最后一条消息编号</summary>
        public const String SnapshotMessageId = "SnapshotMessageId";

        /// <summary>创建者。分享发起用户</summary>
        public const String CreatorUserId = "CreatorUserId";

        /// <summary>过期时间。null表示永不过期</summary>
        public const String ExpireTime = "ExpireTime";

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
