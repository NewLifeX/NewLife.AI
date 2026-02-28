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

/// <summary>对话附件。上传的文件，关联到消息</summary>
[Serializable]
[DataObject]
[Description("对话附件。上传的文件，关联到消息")]
[BindIndex("IX_ChatAttachment_MessageId", false, "MessageId")]
[BindIndex("IX_ChatAttachment_UserId", false, "UserId")]
[BindTable("ChatAttachment", Description = "对话附件。上传的文件，关联到消息", ConnName = "Cube", DbType = DatabaseType.None)]
public partial class ChatAttachment : IEntity<ChatAttachmentModel>
{
    #region 属性
    private Int64 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int64 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int64 _MessageId;
    /// <summary>消息。所属消息，发送前为0</summary>
    [DisplayName("消息")]
    [Description("消息。所属消息，发送前为0")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MessageId", "消息。所属消息，发送前为0", "")]
    public Int64 MessageId { get => _MessageId; set { if (OnPropertyChanging("MessageId", value)) { _MessageId = value; OnPropertyChanged("MessageId"); } } }

    private Int32 _UserId;
    /// <summary>用户。上传用户</summary>
    [DisplayName("用户")]
    [Description("用户。上传用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户。上传用户", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private String _FileName;
    /// <summary>文件名</summary>
    [DisplayName("文件名")]
    [Description("文件名")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("FileName", "文件名", "")]
    public String FileName { get => _FileName; set { if (OnPropertyChanging("FileName", value)) { _FileName = value; OnPropertyChanged("FileName"); } } }

    private String _FilePath;
    /// <summary>文件路径。服务端存储路径</summary>
    [DisplayName("文件路径")]
    [Description("文件路径。服务端存储路径")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("FilePath", "文件路径。服务端存储路径", "", ShowIn = "Auto,-List,-Search")]
    public String FilePath { get => _FilePath; set { if (OnPropertyChanging("FilePath", value)) { _FilePath = value; OnPropertyChanged("FilePath"); } } }

    private String _ContentType;
    /// <summary>内容类型。MIME类型，如image/png</summary>
    [DisplayName("内容类型")]
    [Description("内容类型。MIME类型，如image/png")]
    [DataObjectField(false, false, true, 100)]
    [BindColumn("ContentType", "内容类型。MIME类型，如image/png", "")]
    public String ContentType { get => _ContentType; set { if (OnPropertyChanging("ContentType", value)) { _ContentType = value; OnPropertyChanged("ContentType"); } } }

    private Int64 _FileSize;
    /// <summary>文件大小。字节数</summary>
    [DisplayName("文件大小")]
    [Description("文件大小。字节数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FileSize", "文件大小。字节数", "")]
    public Int64 FileSize { get => _FileSize; set { if (OnPropertyChanging("FileSize", value)) { _FileSize = value; OnPropertyChanged("FileSize"); } } }

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
    public void Copy(ChatAttachmentModel model)
    {
        Id = model.Id;
        MessageId = model.MessageId;
        UserId = model.UserId;
        FileName = model.FileName;
        FilePath = model.FilePath;
        ContentType = model.ContentType;
        FileSize = model.FileSize;
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
            "MessageId" => _MessageId,
            "UserId" => _UserId,
            "FileName" => _FileName,
            "FilePath" => _FilePath,
            "ContentType" => _ContentType,
            "FileSize" => _FileSize,
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
                case "MessageId": _MessageId = value.ToLong(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "FileName": _FileName = Convert.ToString(value); break;
                case "FilePath": _FilePath = Convert.ToString(value); break;
                case "ContentType": _ContentType = Convert.ToString(value); break;
                case "FileSize": _FileSize = value.ToLong(); break;
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
    public static ChatAttachment FindById(Int64 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据消息查找</summary>
    /// <param name="messageId">消息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatAttachment> FindAllByMessageId(Int64 messageId)
    {
        if (messageId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.MessageId == messageId);

        return FindAll(_.MessageId == messageId);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<ChatAttachment> FindAllByUserId(Int32 userId)
    {
        if (userId < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.UserId == userId);

        return FindAll(_.UserId == userId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="messageId">消息。所属消息，发送前为0</param>
    /// <param name="userId">用户。上传用户</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatAttachment> Search(Int64 messageId, Int32 userId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (messageId >= 0) exp &= _.MessageId == messageId;
        if (userId >= 0) exp &= _.UserId == userId;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得对话附件字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>消息。所属消息，发送前为0</summary>
        public static readonly Field MessageId = FindByName("MessageId");

        /// <summary>用户。上传用户</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>文件名</summary>
        public static readonly Field FileName = FindByName("FileName");

        /// <summary>文件路径。服务端存储路径</summary>
        public static readonly Field FilePath = FindByName("FilePath");

        /// <summary>内容类型。MIME类型，如image/png</summary>
        public static readonly Field ContentType = FindByName("ContentType");

        /// <summary>文件大小。字节数</summary>
        public static readonly Field FileSize = FindByName("FileSize");

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

    /// <summary>取得对话附件字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>消息。所属消息，发送前为0</summary>
        public const String MessageId = "MessageId";

        /// <summary>用户。上传用户</summary>
        public const String UserId = "UserId";

        /// <summary>文件名</summary>
        public const String FileName = "FileName";

        /// <summary>文件路径。服务端存储路径</summary>
        public const String FilePath = "FilePath";

        /// <summary>内容类型。MIME类型，如image/png</summary>
        public const String ContentType = "ContentType";

        /// <summary>文件大小。字节数</summary>
        public const String FileSize = "FileSize";

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
