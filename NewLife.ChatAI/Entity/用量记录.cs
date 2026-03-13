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

/// <summary>用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计</summary>
[Serializable]
[DataObject]
[Description("用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计")]
[BindIndex("IX_UsageRecord_UserId_Id", false, "UserId,Id")]
[BindIndex("IX_UsageRecord_AppKeyId_Id", false, "AppKeyId,Id")]
[BindIndex("IX_UsageRecord_ModelId_Id", false, "ModelId,Id")]
[BindIndex("IX_UsageRecord_ConversationId", false, "ConversationId")]
[BindTable("UsageRecord", Description = "用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class UsageRecord : IEntity<UsageRecordModel>
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

    private Int32 _AppKeyId;
    /// <summary>应用密钥。通过API网关调用时关联的AppKey</summary>
    [DisplayName("应用密钥")]
    [Description("应用密钥。通过API网关调用时关联的AppKey")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("AppKeyId", "应用密钥。通过API网关调用时关联的AppKey", "")]
    public Int32 AppKeyId { get => _AppKeyId; set { if (OnPropertyChanging("AppKeyId", value)) { _AppKeyId = value; OnPropertyChanged("AppKeyId"); } } }

    private Int64 _ConversationId;
    /// <summary>会话</summary>
    [DisplayName("会话")]
    [Description("会话")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConversationId", "会话", "")]
    public Int64 ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private Int64 _MessageId;
    /// <summary>消息。对应的AI回复消息</summary>
    [DisplayName("消息")]
    [Description("消息。对应的AI回复消息")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MessageId", "消息。对应的AI回复消息", "")]
    public Int64 MessageId { get => _MessageId; set { if (OnPropertyChanging("MessageId", value)) { _MessageId = value; OnPropertyChanged("MessageId"); } } }

    private Int32 _ModelId;
    /// <summary>模型。引用ModelConfig.Id</summary>
    [DisplayName("模型")]
    [Description("模型。引用ModelConfig.Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ModelId", "模型。引用ModelConfig.Id", "")]
    public Int32 ModelId { get => _ModelId; set { if (OnPropertyChanging("ModelId", value)) { _ModelId = value; OnPropertyChanged("ModelId"); } } }

    private Int32 _PromptTokens;
    /// <summary>提示Token数</summary>
    [DisplayName("提示Token数")]
    [Description("提示Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("PromptTokens", "提示Token数", "")]
    public Int32 PromptTokens { get => _PromptTokens; set { if (OnPropertyChanging("PromptTokens", value)) { _PromptTokens = value; OnPropertyChanged("PromptTokens"); } } }

    private Int32 _CompletionTokens;
    /// <summary>回复Token数</summary>
    [DisplayName("回复Token数")]
    [Description("回复Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CompletionTokens", "回复Token数", "")]
    public Int32 CompletionTokens { get => _CompletionTokens; set { if (OnPropertyChanging("CompletionTokens", value)) { _CompletionTokens = value; OnPropertyChanged("CompletionTokens"); } } }

    private Int32 _TotalTokens;
    /// <summary>总Token数</summary>
    [DisplayName("总Token数")]
    [Description("总Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalTokens", "总Token数", "")]
    public Int32 TotalTokens { get => _TotalTokens; set { if (OnPropertyChanging("TotalTokens", value)) { _TotalTokens = value; OnPropertyChanged("TotalTokens"); } } }

    private String _Source;
    /// <summary>请求来源。Chat=对话/Gateway=网关</summary>
    [DisplayName("请求来源")]
    [Description("请求来源。Chat=对话/Gateway=网关")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Source", "请求来源。Chat=对话/Gateway=网关", "")]
    public String Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

    private String _TraceId;
    /// <summary>链路追踪。方便问题排查</summary>
    [DisplayName("链路追踪")]
    [Description("链路追踪。方便问题排查")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "链路追踪。方便问题排查", "")]
    public String TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

    private DateTime _CreateTime;
    /// <summary>创建时间</summary>
    [Category("扩展")]
    [DisplayName("创建时间")]
    [Description("创建时间")]
    [DataObjectField(false, false, true, 0)]
    [BindColumn("CreateTime", "创建时间", "")]
    public DateTime CreateTime { get => _CreateTime; set { if (OnPropertyChanging("CreateTime", value)) { _CreateTime = value; OnPropertyChanged("CreateTime"); } } }

    private String _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(UsageRecordModel model)
    {
        Id = model.Id;
        UserId = model.UserId;
        AppKeyId = model.AppKeyId;
        ConversationId = model.ConversationId;
        MessageId = model.MessageId;
        ModelId = model.ModelId;
        PromptTokens = model.PromptTokens;
        CompletionTokens = model.CompletionTokens;
        TotalTokens = model.TotalTokens;
        Source = model.Source;
        TraceId = model.TraceId;
        CreateTime = model.CreateTime;
        CreateIP = model.CreateIP;
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
            "AppKeyId" => _AppKeyId,
            "ConversationId" => _ConversationId,
            "MessageId" => _MessageId,
            "ModelId" => _ModelId,
            "PromptTokens" => _PromptTokens,
            "CompletionTokens" => _CompletionTokens,
            "TotalTokens" => _TotalTokens,
            "Source" => _Source,
            "TraceId" => _TraceId,
            "CreateTime" => _CreateTime,
            "CreateIP" => _CreateIP,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "UserId": _UserId = value.ToInt(); break;
                case "AppKeyId": _AppKeyId = value.ToInt(); break;
                case "ConversationId": _ConversationId = value.ToLong(); break;
                case "MessageId": _MessageId = value.ToLong(); break;
                case "ModelId": _ModelId = value.ToInt(); break;
                case "PromptTokens": _PromptTokens = value.ToInt(); break;
                case "CompletionTokens": _CompletionTokens = value.ToInt(); break;
                case "TotalTokens": _TotalTokens = value.ToInt(); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
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
    public static UsageRecord FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByUserId(Int32 userId)
    {
        if (userId < 0) return [];

        return FindAll(_.UserId == userId);
    }

    /// <summary>根据应用密钥查找</summary>
    /// <param name="appKeyId">应用密钥</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByAppKeyId(Int32 appKeyId)
    {
        if (appKeyId < 0) return [];

        return FindAll(_.AppKeyId == appKeyId);
    }

    /// <summary>根据模型查找</summary>
    /// <param name="modelId">模型</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByModelId(Int32 modelId)
    {
        if (modelId < 0) return [];

        return FindAll(_.ModelId == modelId);
    }

    /// <summary>根据会话查找</summary>
    /// <param name="conversationId">会话</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByConversationId(Int64 conversationId)
    {
        if (conversationId < 0) return [];

        return FindAll(_.ConversationId == conversationId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户</param>
    /// <param name="appKeyId">应用密钥。通过API网关调用时关联的AppKey</param>
    /// <param name="conversationId">会话</param>
    /// <param name="modelId">模型。引用ModelConfig.Id</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> Search(Int32 userId, Int32 appKeyId, Int64 conversationId, Int32 modelId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (appKeyId >= 0) exp &= _.AppKeyId == appKeyId;
        if (conversationId >= 0) exp &= _.ConversationId == conversationId;
        if (modelId >= 0) exp &= _.ModelId == modelId;
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
    /// <summary>取得用量记录字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>用户</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>应用密钥。通过API网关调用时关联的AppKey</summary>
        public static readonly Field AppKeyId = FindByName("AppKeyId");

        /// <summary>会话</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");

        /// <summary>消息。对应的AI回复消息</summary>
        public static readonly Field MessageId = FindByName("MessageId");

        /// <summary>模型。引用ModelConfig.Id</summary>
        public static readonly Field ModelId = FindByName("ModelId");

        /// <summary>提示Token数</summary>
        public static readonly Field PromptTokens = FindByName("PromptTokens");

        /// <summary>回复Token数</summary>
        public static readonly Field CompletionTokens = FindByName("CompletionTokens");

        /// <summary>总Token数</summary>
        public static readonly Field TotalTokens = FindByName("TotalTokens");

        /// <summary>请求来源。Chat=对话/Gateway=网关</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>链路追踪。方便问题排查</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name);
    }

    /// <summary>取得用量记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户</summary>
        public const String UserId = "UserId";

        /// <summary>应用密钥。通过API网关调用时关联的AppKey</summary>
        public const String AppKeyId = "AppKeyId";

        /// <summary>会话</summary>
        public const String ConversationId = "ConversationId";

        /// <summary>消息。对应的AI回复消息</summary>
        public const String MessageId = "MessageId";

        /// <summary>模型。引用ModelConfig.Id</summary>
        public const String ModelId = "ModelId";

        /// <summary>提示Token数</summary>
        public const String PromptTokens = "PromptTokens";

        /// <summary>回复Token数</summary>
        public const String CompletionTokens = "CompletionTokens";

        /// <summary>总Token数</summary>
        public const String TotalTokens = "TotalTokens";

        /// <summary>请求来源。Chat=对话/Gateway=网关</summary>
        public const String Source = "Source";

        /// <summary>链路追踪。方便问题排查</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";
    }
    #endregion
}
