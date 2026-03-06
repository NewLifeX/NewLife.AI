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

/// <summary>会话。一次完整的多轮对话上下文</summary>
[Serializable]
[DataObject]
[Description("会话。一次完整的多轮对话上下文")]
[BindIndex("IX_Conversation_UserId_Id", false, "UserId,Id")]
[BindIndex("IX_Conversation_UserId_IsPinned_Id", false, "UserId,IsPinned,Id")]
[BindTable("Conversation", Description = "会话。一次完整的多轮对话上下文", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class Conversation : IEntity<ConversationModel>
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
    /// <summary>用户。会话所属用户</summary>
    [DisplayName("用户")]
    [Description("用户。会话所属用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户。会话所属用户", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private String _Title;
    /// <summary>标题。会话标题，显示在侧边栏</summary>
    [DisplayName("标题")]
    [Description("标题。会话标题，显示在侧边栏")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Title", "标题。会话标题，显示在侧边栏", "", Master = true)]
    public String Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private String _ModelCode;
    /// <summary>模型编码。当前使用的模型</summary>
    [DisplayName("模型编码")]
    [Description("模型编码。当前使用的模型")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ModelCode", "模型编码。当前使用的模型", "")]
    public String ModelCode { get => _ModelCode; set { if (OnPropertyChanging("ModelCode", value)) { _ModelCode = value; OnPropertyChanged("ModelCode"); } } }

    private Int32 _ThinkingMode;
    /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
    [DisplayName("思考模式")]
    [Description("思考模式。Auto=0自动, Think=1思考, Fast=2快速")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThinkingMode", "思考模式。Auto=0自动, Think=1思考, Fast=2快速", "")]
    public Int32 ThinkingMode { get => _ThinkingMode; set { if (OnPropertyChanging("ThinkingMode", value)) { _ThinkingMode = value; OnPropertyChanged("ThinkingMode"); } } }

    private Boolean _IsPinned;
    /// <summary>置顶。是否置顶显示</summary>
    [DisplayName("置顶")]
    [Description("置顶。是否置顶显示")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsPinned", "置顶。是否置顶显示", "")]
    public Boolean IsPinned { get => _IsPinned; set { if (OnPropertyChanging("IsPinned", value)) { _IsPinned = value; OnPropertyChanged("IsPinned"); } } }

    private Int32 _MessageCount;
    /// <summary>消息数</summary>
    [DisplayName("消息数")]
    [Description("消息数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MessageCount", "消息数", "")]
    public Int32 MessageCount { get => _MessageCount; set { if (OnPropertyChanging("MessageCount", value)) { _MessageCount = value; OnPropertyChanged("MessageCount"); } } }

    private DateTime _LastMessageTime;
    /// <summary>最后消息时间。用于排序</summary>
    [DisplayName("最后消息时间")]
    [Description("最后消息时间。用于排序")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("LastMessageTime", "最后消息时间。用于排序", "")]
    public DateTime LastMessageTime { get => _LastMessageTime; set { if (OnPropertyChanging("LastMessageTime", value)) { _LastMessageTime = value; OnPropertyChanged("LastMessageTime"); } } }

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

    private String _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(ConversationModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        Title = model.Title;
        ModelCode = model.ModelCode;
        ThinkingMode = model.ThinkingMode;
        IsPinned = model.IsPinned;
        MessageCount = model.MessageCount;
        LastMessageTime = model.LastMessageTime;
        CreateUserID = model.CreateUserID;
        CreateIP = model.CreateIP;
        CreateTime = model.CreateTime;
        UpdateUserID = model.UpdateUserID;
        UpdateIP = model.UpdateIP;
        UpdateTime = model.UpdateTime;
        Remark = model.Remark;
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
            "Title" => _Title,
            "ModelCode" => _ModelCode,
            "ThinkingMode" => _ThinkingMode,
            "IsPinned" => _IsPinned,
            "MessageCount" => _MessageCount,
            "LastMessageTime" => _LastMessageTime,
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
                case "Id": _Id = value.ToLong(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "ModelCode": _ModelCode = Convert.ToString(value); break;
                case "ThinkingMode": _ThinkingMode = value.ToInt(); break;
                case "IsPinned": _IsPinned = value.ToBoolean(); break;
                case "MessageCount": _MessageCount = value.ToInt(); break;
                case "LastMessageTime": _LastMessageTime = value.ToDateTime(); break;
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
    public static Conversation FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<Conversation> FindAllByUserId(Int32 userId)
    {
        if (userId < 0) return [];

        return FindAll(_.UserId == userId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户。会话所属用户</param>
    /// <param name="isPinned">置顶。是否置顶显示</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Conversation> Search(Int32 userId, Boolean? isPinned, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (isPinned != null) exp &= _.IsPinned == isPinned;
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
    /// <summary>取得会话字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>用户。会话所属用户</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>标题。会话标题，显示在侧边栏</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>模型编码。当前使用的模型</summary>
        public static readonly Field ModelCode = FindByName("ModelCode");

        /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
        public static readonly Field ThinkingMode = FindByName("ThinkingMode");

        /// <summary>置顶。是否置顶显示</summary>
        public static readonly Field IsPinned = FindByName("IsPinned");

        /// <summary>消息数</summary>
        public static readonly Field MessageCount = FindByName("MessageCount");

        /// <summary>最后消息时间。用于排序</summary>
        public static readonly Field LastMessageTime = FindByName("LastMessageTime");

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

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得会话字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户。会话所属用户</summary>
        public const String UserId = "UserId";

        /// <summary>标题。会话标题，显示在侧边栏</summary>
        public const String Title = "Title";

        /// <summary>模型编码。当前使用的模型</summary>
        public const String ModelCode = "ModelCode";

        /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
        public const String ThinkingMode = "ThinkingMode";

        /// <summary>置顶。是否置顶显示</summary>
        public const String IsPinned = "IsPinned";

        /// <summary>消息数</summary>
        public const String MessageCount = "MessageCount";

        /// <summary>最后消息时间。用于排序</summary>
        public const String LastMessageTime = "LastMessageTime";

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
