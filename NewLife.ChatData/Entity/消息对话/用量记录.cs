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

namespace NewLife.ChatData.Entity;

/// <summary>用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计</summary>
[Serializable]
[DataObject]
[Description("用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计")]
[BindIndex("IX_UsageRecord_UserId_Id", false, "UserId,Id")]
[BindIndex("IX_UsageRecord_ProjectId_Id", false, "ProjectId,Id")]
[BindIndex("IX_UsageRecord_AppKeyId_Id", false, "AppKeyId,Id")]
[BindIndex("IX_UsageRecord_ModelId_Id", false, "ModelId,Id")]
[BindIndex("IX_UsageRecord_ConversationId", false, "ConversationId")]
[BindIndex("IX_UsageRecord_Source_Id", false, "Source,Id")]
[BindIndex("IX_UsageRecord_ParentMessageId", false, "ParentMessageId")]
[BindTable("UsageRecord", Description = "用量记录。每次AI调用的Token消耗，支持按用户和AppKey双维度统计", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class UsageRecord : IUsageRecord, IEntity<IUsageRecord>
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
    /// <summary>项目。所属项目编号，0=个人</summary>
    [DisplayName("项目")]
    [Description("项目。所属项目编号，0=个人")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目。所属项目编号，0=个人", "")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

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

    private String? _ModelName;
    /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
    [DisplayName("模型名称")]
    [Description("模型名称。冗余存储模型名称，方便历史数据检索")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ModelName", "模型名称。冗余存储模型名称，方便历史数据检索", "")]
    public String? ModelName { get => _ModelName; set { if (OnPropertyChanging("ModelName", value)) { _ModelName = value; OnPropertyChanged("ModelName"); } } }

    private Int32 _InputTokens;
    /// <summary>输入Token数</summary>
    [DisplayName("输入Token数")]
    [Description("输入Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InputTokens", "输入Token数", "")]
    public Int32 InputTokens { get => _InputTokens; set { if (OnPropertyChanging("InputTokens", value)) { _InputTokens = value; OnPropertyChanged("InputTokens"); } } }

    private Int32 _OutputTokens;
    /// <summary>输出Token数</summary>
    [DisplayName("输出Token数")]
    [Description("输出Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OutputTokens", "输出Token数", "")]
    public Int32 OutputTokens { get => _OutputTokens; set { if (OnPropertyChanging("OutputTokens", value)) { _OutputTokens = value; OnPropertyChanged("OutputTokens"); } } }

    private Int32 _TotalTokens;
    /// <summary>总Token数</summary>
    [DisplayName("总Token数")]
    [Description("总Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalTokens", "总Token数", "")]
    public Int32 TotalTokens { get => _TotalTokens; set { if (OnPropertyChanging("TotalTokens", value)) { _TotalTokens = value; OnPropertyChanged("TotalTokens"); } } }

    private Int32 _CachedInputTokens;
    /// <summary>缓存输入Token数</summary>
    [DisplayName("缓存输入Token数")]
    [Description("缓存输入Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("CachedInputTokens", "缓存输入Token数", "")]
    public Int32 CachedInputTokens { get => _CachedInputTokens; set { if (OnPropertyChanging("CachedInputTokens", value)) { _CachedInputTokens = value; OnPropertyChanged("CachedInputTokens"); } } }

    private Int32 _ReasoningTokens;
    /// <summary>推理Token数</summary>
    [DisplayName("推理Token数")]
    [Description("推理Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ReasoningTokens", "推理Token数", "")]
    public Int32 ReasoningTokens { get => _ReasoningTokens; set { if (OnPropertyChanging("ReasoningTokens", value)) { _ReasoningTokens = value; OnPropertyChanged("ReasoningTokens"); } } }

    private Int32 _InputAudioTokens;
    /// <summary>音频输入Token数</summary>
    [DisplayName("音频输入Token数")]
    [Description("音频输入Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InputAudioTokens", "音频输入Token数", "")]
    public Int32 InputAudioTokens { get => _InputAudioTokens; set { if (OnPropertyChanging("InputAudioTokens", value)) { _InputAudioTokens = value; OnPropertyChanged("InputAudioTokens"); } } }

    private Int32 _InputTextTokens;
    /// <summary>文本输入Token数</summary>
    [DisplayName("文本输入Token数")]
    [Description("文本输入Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InputTextTokens", "文本输入Token数", "")]
    public Int32 InputTextTokens { get => _InputTextTokens; set { if (OnPropertyChanging("InputTextTokens", value)) { _InputTextTokens = value; OnPropertyChanged("InputTextTokens"); } } }

    private Int32 _OutputAudioTokens;
    /// <summary>音频输出Token数</summary>
    [DisplayName("音频输出Token数")]
    [Description("音频输出Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OutputAudioTokens", "音频输出Token数", "")]
    public Int32 OutputAudioTokens { get => _OutputAudioTokens; set { if (OnPropertyChanging("OutputAudioTokens", value)) { _OutputAudioTokens = value; OnPropertyChanged("OutputAudioTokens"); } } }

    private Int32 _OutputTextTokens;
    /// <summary>文本输出Token数</summary>
    [DisplayName("文本输出Token数")]
    [Description("文本输出Token数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OutputTextTokens", "文本输出Token数", "")]
    public Int32 OutputTextTokens { get => _OutputTextTokens; set { if (OnPropertyChanging("OutputTextTokens", value)) { _OutputTextTokens = value; OnPropertyChanged("OutputTextTokens"); } } }

    private Decimal _InputCost;
    /// <summary>输入费用。单位：元</summary>
    [DisplayName("输入费用")]
    [Description("输入费用。单位：元")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InputCost", "输入费用。单位：元", "", Precision = 18, Scale = 6)]
    public Decimal InputCost { get => _InputCost; set { if (OnPropertyChanging("InputCost", value)) { _InputCost = value; OnPropertyChanged("InputCost"); } } }

    private Decimal _OutputCost;
    /// <summary>输出费用。单位：元</summary>
    [DisplayName("输出费用")]
    [Description("输出费用。单位：元")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OutputCost", "输出费用。单位：元", "", Precision = 18, Scale = 6)]
    public Decimal OutputCost { get => _OutputCost; set { if (OnPropertyChanging("OutputCost", value)) { _OutputCost = value; OnPropertyChanged("OutputCost"); } } }

    private Decimal _TotalCost;
    /// <summary>总费用。单位：元</summary>
    [DisplayName("总费用")]
    [Description("总费用。单位：元")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalCost", "总费用。单位：元", "", Precision = 18, Scale = 6)]
    public Decimal TotalCost { get => _TotalCost; set { if (OnPropertyChanging("TotalCost", value)) { _TotalCost = value; OnPropertyChanged("TotalCost"); } } }

    private Int32 _ImageCount;
    /// <summary>图片数量。本次生成图片数（多模态计费）</summary>
    [DisplayName("图片数量")]
    [Description("图片数量。本次生成图片数（多模态计费）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ImageCount", "图片数量。本次生成图片数（多模态计费）", "")]
    public Int32 ImageCount { get => _ImageCount; set { if (OnPropertyChanging("ImageCount", value)) { _ImageCount = value; OnPropertyChanged("ImageCount"); } } }

    private Int32 _VideoSeconds;
    /// <summary>视频时长。本次生成视频秒数（多模态计费）</summary>
    [DisplayName("视频时长")]
    [Description("视频时长。本次生成视频秒数（多模态计费）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("VideoSeconds", "视频时长。本次生成视频秒数（多模态计费）", "")]
    public Int32 VideoSeconds { get => _VideoSeconds; set { if (OnPropertyChanging("VideoSeconds", value)) { _VideoSeconds = value; OnPropertyChanged("VideoSeconds"); } } }

    private Int32 _EmbeddingCount;
    /// <summary>向量化条数。Embedding 调用的批量条数</summary>
    [DisplayName("向量化条数")]
    [Description("向量化条数。Embedding 调用的批量条数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("EmbeddingCount", "向量化条数。Embedding 调用的批量条数", "")]
    public Int32 EmbeddingCount { get => _EmbeddingCount; set { if (OnPropertyChanging("EmbeddingCount", value)) { _EmbeddingCount = value; OnPropertyChanged("EmbeddingCount"); } } }

    private String? _Resolution;
    /// <summary>分辨率。如720P/1080P/4K，匹配ModelConfig.PriceTiers</summary>
    [DisplayName("分辨率")]
    [Description("分辨率。如720P/1080P/4K，匹配ModelConfig.PriceTiers")]
    [DataObjectField(false, false, true, 20)]
    [BindColumn("Resolution", "分辨率。如720P/1080P/4K，匹配ModelConfig.PriceTiers", "")]
    public String? Resolution { get => _Resolution; set { if (OnPropertyChanging("Resolution", value)) { _Resolution = value; OnPropertyChanged("Resolution"); } } }

    private Int64 _ParentMessageId;
    /// <summary>父消息。额外LLM调用（标题/压缩/记忆/知识等）关联到主对话消息编号，0表示主调用</summary>
    [DisplayName("父消息")]
    [Description("父消息。额外LLM调用（标题/压缩/记忆/知识等）关联到主对话消息编号，0表示主调用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ParentMessageId", "父消息。额外LLM调用（标题/压缩/记忆/知识等）关联到主对话消息编号，0表示主调用", "")]
    public Int64 ParentMessageId { get => _ParentMessageId; set { if (OnPropertyChanging("ParentMessageId", value)) { _ParentMessageId = value; OnPropertyChanged("ParentMessageId"); } } }

    private Int32 _ElapsedMs;
    /// <summary>耗时。毫秒</summary>
    [DisplayName("耗时")]
    [Description("耗时。毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ElapsedMs", "耗时。毫秒", "")]
    public Int32 ElapsedMs { get => _ElapsedMs; set { if (OnPropertyChanging("ElapsedMs", value)) { _ElapsedMs = value; OnPropertyChanged("ElapsedMs"); } } }

    private String? _Source;
    /// <summary>请求来源。Chat=主对话/Gateway=网关/Title=标题生成/Compact=上下文压缩/Memory=记忆提取/Knowledge=知识分析/Image=图片生成/Video=视频生成/Embedding=向量化</summary>
    [DisplayName("请求来源")]
    [Description("请求来源。Chat=主对话/Gateway=网关/Title=标题生成/Compact=上下文压缩/Memory=记忆提取/Knowledge=知识分析/Image=图片生成/Video=视频生成/Embedding=向量化")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Source", "请求来源。Chat=主对话/Gateway=网关/Title=标题生成/Compact=上下文压缩/Memory=记忆提取/Knowledge=知识分析/Image=图片生成/Video=视频生成/Embedding=向量化", "")]
    public String? Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

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

    private String? _CreateIP;
    /// <summary>创建地址</summary>
    [Category("扩展")]
    [DisplayName("创建地址")]
    [Description("创建地址")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateIP", "创建地址", "")]
    public String? CreateIP { get => _CreateIP; set { if (OnPropertyChanging("CreateIP", value)) { _CreateIP = value; OnPropertyChanged("CreateIP"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IUsageRecord model)
    {
        Id = model.Id;
        UserId = model.UserId;
        ProjectId = model.ProjectId;
        AppKeyId = model.AppKeyId;
        ConversationId = model.ConversationId;
        MessageId = model.MessageId;
        ModelId = model.ModelId;
        ModelName = model.ModelName;
        InputTokens = model.InputTokens;
        OutputTokens = model.OutputTokens;
        TotalTokens = model.TotalTokens;
        CachedInputTokens = model.CachedInputTokens;
        ReasoningTokens = model.ReasoningTokens;
        InputAudioTokens = model.InputAudioTokens;
        InputTextTokens = model.InputTextTokens;
        OutputAudioTokens = model.OutputAudioTokens;
        OutputTextTokens = model.OutputTextTokens;
        InputCost = model.InputCost;
        OutputCost = model.OutputCost;
        TotalCost = model.TotalCost;
        ImageCount = model.ImageCount;
        VideoSeconds = model.VideoSeconds;
        EmbeddingCount = model.EmbeddingCount;
        Resolution = model.Resolution;
        ParentMessageId = model.ParentMessageId;
        ElapsedMs = model.ElapsedMs;
        Source = model.Source;
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
            "AppKeyId" => _AppKeyId,
            "ConversationId" => _ConversationId,
            "MessageId" => _MessageId,
            "ModelId" => _ModelId,
            "ModelName" => _ModelName,
            "InputTokens" => _InputTokens,
            "OutputTokens" => _OutputTokens,
            "TotalTokens" => _TotalTokens,
            "CachedInputTokens" => _CachedInputTokens,
            "ReasoningTokens" => _ReasoningTokens,
            "InputAudioTokens" => _InputAudioTokens,
            "InputTextTokens" => _InputTextTokens,
            "OutputAudioTokens" => _OutputAudioTokens,
            "OutputTextTokens" => _OutputTextTokens,
            "InputCost" => _InputCost,
            "OutputCost" => _OutputCost,
            "TotalCost" => _TotalCost,
            "ImageCount" => _ImageCount,
            "VideoSeconds" => _VideoSeconds,
            "EmbeddingCount" => _EmbeddingCount,
            "Resolution" => _Resolution,
            "ParentMessageId" => _ParentMessageId,
            "ElapsedMs" => _ElapsedMs,
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
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "AppKeyId": _AppKeyId = value.ToInt(); break;
                case "ConversationId": _ConversationId = value.ToLong(); break;
                case "MessageId": _MessageId = value.ToLong(); break;
                case "ModelId": _ModelId = value.ToInt(); break;
                case "ModelName": _ModelName = Convert.ToString(value); break;
                case "InputTokens": _InputTokens = value.ToInt(); break;
                case "OutputTokens": _OutputTokens = value.ToInt(); break;
                case "TotalTokens": _TotalTokens = value.ToInt(); break;
                case "CachedInputTokens": _CachedInputTokens = value.ToInt(); break;
                case "ReasoningTokens": _ReasoningTokens = value.ToInt(); break;
                case "InputAudioTokens": _InputAudioTokens = value.ToInt(); break;
                case "InputTextTokens": _InputTextTokens = value.ToInt(); break;
                case "OutputAudioTokens": _OutputAudioTokens = value.ToInt(); break;
                case "OutputTextTokens": _OutputTextTokens = value.ToInt(); break;
                case "InputCost": _InputCost = Convert.ToDecimal(value); break;
                case "OutputCost": _OutputCost = Convert.ToDecimal(value); break;
                case "TotalCost": _TotalCost = Convert.ToDecimal(value); break;
                case "ImageCount": _ImageCount = value.ToInt(); break;
                case "VideoSeconds": _VideoSeconds = value.ToInt(); break;
                case "EmbeddingCount": _EmbeddingCount = value.ToInt(); break;
                case "Resolution": _Resolution = Convert.ToString(value); break;
                case "ParentMessageId": _ParentMessageId = value.ToLong(); break;
                case "ElapsedMs": _ElapsedMs = value.ToInt(); break;
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

    /// <summary>应用密钥</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public AppKey? AppKey => Extends.Get(nameof(AppKey), k => AppKey.FindById(AppKeyId));

    /// <summary>应用密钥</summary>
    [Map(nameof(AppKeyId), typeof(AppKey), "Id")]
    public String? AppKeyName => AppKey?.ToString();

    /// <summary>会话</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Conversation? Conversation => Extends.Get(nameof(Conversation), k => Conversation.FindById(ConversationId));

    /// <summary>会话</summary>
    [Map(nameof(ConversationId), typeof(Conversation), "Id")]
    public String? Title => Conversation?.ToString();

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static UsageRecord? FindById(Int64 id)
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

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByProjectId(Int32 projectId)
    {
        if (projectId < 0) return [];

        return FindAll(_.ProjectId == projectId);
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

    /// <summary>根据请求来源查找</summary>
    /// <param name="source">请求来源</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllBySource(String? source)
    {
        if (source == null) return [];

        return FindAll(_.Source == source);
    }

    /// <summary>根据父消息查找</summary>
    /// <param name="parentMessageId">父消息</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> FindAllByParentMessageId(Int64 parentMessageId)
    {
        if (parentMessageId < 0) return [];

        return FindAll(_.ParentMessageId == parentMessageId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户</param>
    /// <param name="projectId">项目。所属项目编号，0=个人</param>
    /// <param name="appKeyId">应用密钥。通过API网关调用时关联的AppKey</param>
    /// <param name="conversationId">会话</param>
    /// <param name="modelId">模型。引用ModelConfig.Id</param>
    /// <param name="parentMessageId">父消息。额外LLM调用（标题/压缩/记忆/知识等）关联到主对话消息编号，0表示主调用</param>
    /// <param name="source">请求来源。Chat=主对话/Gateway=网关/Title=标题生成/Compact=上下文压缩/Memory=记忆提取/Knowledge=知识分析/Image=图片生成/Video=视频生成/Embedding=向量化</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UsageRecord> Search(Int32 userId, Int32 projectId, Int32 appKeyId, Int64 conversationId, Int32 modelId, Int64 parentMessageId, String? source, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (appKeyId >= 0) exp &= _.AppKeyId == appKeyId;
        if (conversationId >= 0) exp &= _.ConversationId == conversationId;
        if (modelId >= 0) exp &= _.ModelId == modelId;
        if (parentMessageId >= 0) exp &= _.ParentMessageId == parentMessageId;
        if (!source.IsNullOrEmpty()) exp &= _.Source == source;
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

        /// <summary>项目。所属项目编号，0=个人</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>应用密钥。通过API网关调用时关联的AppKey</summary>
        public static readonly Field AppKeyId = FindByName("AppKeyId");

        /// <summary>会话</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");

        /// <summary>消息。对应的AI回复消息</summary>
        public static readonly Field MessageId = FindByName("MessageId");

        /// <summary>模型。引用ModelConfig.Id</summary>
        public static readonly Field ModelId = FindByName("ModelId");

        /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
        public static readonly Field ModelName = FindByName("ModelName");

        /// <summary>输入Token数</summary>
        public static readonly Field InputTokens = FindByName("InputTokens");

        /// <summary>输出Token数</summary>
        public static readonly Field OutputTokens = FindByName("OutputTokens");

        /// <summary>总Token数</summary>
        public static readonly Field TotalTokens = FindByName("TotalTokens");

        /// <summary>缓存输入Token数</summary>
        public static readonly Field CachedInputTokens = FindByName("CachedInputTokens");

        /// <summary>推理Token数</summary>
        public static readonly Field ReasoningTokens = FindByName("ReasoningTokens");

        /// <summary>音频输入Token数</summary>
        public static readonly Field InputAudioTokens = FindByName("InputAudioTokens");

        /// <summary>文本输入Token数</summary>
        public static readonly Field InputTextTokens = FindByName("InputTextTokens");

        /// <summary>音频输出Token数</summary>
        public static readonly Field OutputAudioTokens = FindByName("OutputAudioTokens");

        /// <summary>文本输出Token数</summary>
        public static readonly Field OutputTextTokens = FindByName("OutputTextTokens");

        /// <summary>输入费用。单位：元</summary>
        public static readonly Field InputCost = FindByName("InputCost");

        /// <summary>输出费用。单位：元</summary>
        public static readonly Field OutputCost = FindByName("OutputCost");

        /// <summary>总费用。单位：元</summary>
        public static readonly Field TotalCost = FindByName("TotalCost");

        /// <summary>图片数量。本次生成图片数（多模态计费）</summary>
        public static readonly Field ImageCount = FindByName("ImageCount");

        /// <summary>视频时长。本次生成视频秒数（多模态计费）</summary>
        public static readonly Field VideoSeconds = FindByName("VideoSeconds");

        /// <summary>向量化条数。Embedding 调用的批量条数</summary>
        public static readonly Field EmbeddingCount = FindByName("EmbeddingCount");

        /// <summary>分辨率。如720P/1080P/4K，匹配ModelConfig.PriceTiers</summary>
        public static readonly Field Resolution = FindByName("Resolution");

        /// <summary>父消息。额外LLM调用（标题/压缩/记忆/知识等）关联到主对话消息编号，0表示主调用</summary>
        public static readonly Field ParentMessageId = FindByName("ParentMessageId");

        /// <summary>耗时。毫秒</summary>
        public static readonly Field ElapsedMs = FindByName("ElapsedMs");

        /// <summary>请求来源。Chat=主对话/Gateway=网关/Title=标题生成/Compact=上下文压缩/Memory=记忆提取/Knowledge=知识分析/Image=图片生成/Video=视频生成/Embedding=向量化</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>链路。方便问题排查</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得用量记录字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户</summary>
        public const String UserId = "UserId";

        /// <summary>项目。所属项目编号，0=个人</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>应用密钥。通过API网关调用时关联的AppKey</summary>
        public const String AppKeyId = "AppKeyId";

        /// <summary>会话</summary>
        public const String ConversationId = "ConversationId";

        /// <summary>消息。对应的AI回复消息</summary>
        public const String MessageId = "MessageId";

        /// <summary>模型。引用ModelConfig.Id</summary>
        public const String ModelId = "ModelId";

        /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
        public const String ModelName = "ModelName";

        /// <summary>输入Token数</summary>
        public const String InputTokens = "InputTokens";

        /// <summary>输出Token数</summary>
        public const String OutputTokens = "OutputTokens";

        /// <summary>总Token数</summary>
        public const String TotalTokens = "TotalTokens";

        /// <summary>缓存输入Token数</summary>
        public const String CachedInputTokens = "CachedInputTokens";

        /// <summary>推理Token数</summary>
        public const String ReasoningTokens = "ReasoningTokens";

        /// <summary>音频输入Token数</summary>
        public const String InputAudioTokens = "InputAudioTokens";

        /// <summary>文本输入Token数</summary>
        public const String InputTextTokens = "InputTextTokens";

        /// <summary>音频输出Token数</summary>
        public const String OutputAudioTokens = "OutputAudioTokens";

        /// <summary>文本输出Token数</summary>
        public const String OutputTextTokens = "OutputTextTokens";

        /// <summary>输入费用。单位：元</summary>
        public const String InputCost = "InputCost";

        /// <summary>输出费用。单位：元</summary>
        public const String OutputCost = "OutputCost";

        /// <summary>总费用。单位：元</summary>
        public const String TotalCost = "TotalCost";

        /// <summary>图片数量。本次生成图片数（多模态计费）</summary>
        public const String ImageCount = "ImageCount";

        /// <summary>视频时长。本次生成视频秒数（多模态计费）</summary>
        public const String VideoSeconds = "VideoSeconds";

        /// <summary>向量化条数。Embedding 调用的批量条数</summary>
        public const String EmbeddingCount = "EmbeddingCount";

        /// <summary>分辨率。如720P/1080P/4K，匹配ModelConfig.PriceTiers</summary>
        public const String Resolution = "Resolution";

        /// <summary>父消息。额外LLM调用（标题/压缩/记忆/知识等）关联到主对话消息编号，0表示主调用</summary>
        public const String ParentMessageId = "ParentMessageId";

        /// <summary>耗时。毫秒</summary>
        public const String ElapsedMs = "ElapsedMs";

        /// <summary>请求来源。Chat=主对话/Gateway=网关/Title=标题生成/Compact=上下文压缩/Memory=记忆提取/Knowledge=知识分析/Image=图片生成/Video=视频生成/Embedding=向量化</summary>
        public const String Source = "Source";

        /// <summary>链路。方便问题排查</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";
    }
    #endregion
}
