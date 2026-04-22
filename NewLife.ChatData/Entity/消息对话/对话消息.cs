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

/// <summary>对话消息。会话中的单条发言，包括用户消息和AI回复</summary>
[Serializable]
[DataObject]
[Description("对话消息。会话中的单条发言，包括用户消息和AI回复")]
[BindIndex("IX_ChatMessage_ConversationId_Id", false, "ConversationId,Id")]
[BindIndex("IX_ChatMessage_ParentId", false, "ParentId")]
[BindTable("ChatMessage", Description = "对话消息。会话中的单条发言，包括用户消息和AI回复", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class ChatMessage : IChatMessage, IEntity<IChatMessage>
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
    /// <summary>会话。所属会话</summary>
    [DisplayName("会话")]
    [Description("会话。所属会话")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ConversationId", "会话。所属会话", "")]
    public Int64 ConversationId { get => _ConversationId; set { if (OnPropertyChanging("ConversationId", value)) { _ConversationId = value; OnPropertyChanged("ConversationId"); } } }

    private Int64 _ParentId;
    /// <summary>父消息。消息树父节点ID，0表示对话根节点。用于对话分叉时记录消息的层级关系</summary>
    [DisplayName("父消息")]
    [Description("父消息。消息树父节点ID，0表示对话根节点。用于对话分叉时记录消息的层级关系")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ParentId", "父消息。消息树父节点ID，0表示对话根节点。用于对话分叉时记录消息的层级关系", "")]
    public Int64 ParentId { get => _ParentId; set { if (OnPropertyChanging("ParentId", value)) { _ParentId = value; OnPropertyChanged("ParentId"); } } }

    private Int32 _BranchIndex;
    /// <summary>分支序号。同一父节点下的分支序号，0=主线，1/2...=分叉支线</summary>
    [DisplayName("分支序号")]
    [Description("分支序号。同一父节点下的分支序号，0=主线，1/2...=分叉支线")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("BranchIndex", "分支序号。同一父节点下的分支序号，0=主线，1/2...=分叉支线", "")]
    public Int32 BranchIndex { get => _BranchIndex; set { if (OnPropertyChanging("BranchIndex", value)) { _BranchIndex = value; OnPropertyChanged("BranchIndex"); } } }

    private String? _Role;
    /// <summary>角色。User=用户, Assistant=AI助手</summary>
    [DisplayName("角色")]
    [Description("角色。User=用户, Assistant=AI助手")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Role", "角色。User=用户, Assistant=AI助手", "")]
    public String? Role { get => _Role; set { if (OnPropertyChanging("Role", value)) { _Role = value; OnPropertyChanged("Role"); } } }

    private String? _Content;
    /// <summary>内容。Markdown格式文本</summary>
    [DisplayName("内容")]
    [Description("内容。Markdown格式文本")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Content", "内容。Markdown格式文本", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

    private String? _ThinkingContent;
    /// <summary>思考内容。role=assistant时存AI推理过程；role=user时存本轮注入的系统上下文全文（含技能提示词与记忆注入，调试用）</summary>
    [DisplayName("思考内容")]
    [Description("思考内容。role=assistant时存AI推理过程；role=user时存本轮注入的系统上下文全文（含技能提示词与记忆注入，调试用）")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ThinkingContent", "思考内容。role=assistant时存AI推理过程；role=user时存本轮注入的系统上下文全文（含技能提示词与记忆注入，调试用）", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? ThinkingContent { get => _ThinkingContent; set { if (OnPropertyChanging("ThinkingContent", value)) { _ThinkingContent = value; OnPropertyChanged("ThinkingContent"); } } }

    private NewLife.AI.Models.ThinkingMode _ThinkingMode;
    /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
    [DisplayName("思考模式")]
    [Description("思考模式。Auto=0自动, Think=1思考, Fast=2快速")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThinkingMode", "思考模式。Auto=0自动, Think=1思考, Fast=2快速", "")]
    public NewLife.AI.Models.ThinkingMode ThinkingMode { get => _ThinkingMode; set { if (OnPropertyChanging("ThinkingMode", value)) { _ThinkingMode = value; OnPropertyChanged("ThinkingMode"); } } }

    private String? _Attachments;
    /// <summary>附件列表。存储魔方附件ID数组</summary>
    [DisplayName("附件列表")]
    [Description("附件列表。存储魔方附件ID数组")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Attachments", "附件列表。存储魔方附件ID数组", "", ShowIn = "Auto,-List,-Search")]
    public String? Attachments { get => _Attachments; set { if (OnPropertyChanging("Attachments", value)) { _Attachments = value; OnPropertyChanged("Attachments"); } } }

    private String? _SkillNames;
    /// <summary>技能列表。本轮激活的技能名称，多个逗号分隔</summary>
    [DisplayName("技能列表")]
    [Description("技能列表。本轮激活的技能名称，多个逗号分隔")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("SkillNames", "技能列表。本轮激活的技能名称，多个逗号分隔", "")]
    public String? SkillNames { get => _SkillNames; set { if (OnPropertyChanging("SkillNames", value)) { _SkillNames = value; OnPropertyChanged("SkillNames"); } } }

    private String? _ToolNames;
    /// <summary>工具列表。role=user时记录本轮可用工具名（逗号分隔）；role=assistant时记录实际调用的工具名（逗号分隔）</summary>
    [DisplayName("工具列表")]
    [Description("工具列表。role=user时记录本轮可用工具名（逗号分隔）；role=assistant时记录实际调用的工具名（逗号分隔）")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("ToolNames", "工具列表。role=user时记录本轮可用工具名（逗号分隔）；role=assistant时记录实际调用的工具名（逗号分隔）", "")]
    public String? ToolNames { get => _ToolNames; set { if (OnPropertyChanging("ToolNames", value)) { _ToolNames = value; OnPropertyChanged("ToolNames"); } } }

    private String? _ToolCalls;
    /// <summary>工具调用。role=assistant时存实际调用链路ToolCallDto[]（含入参Arguments和执行结果Result）</summary>
    [DisplayName("工具调用")]
    [Description("工具调用。role=assistant时存实际调用链路ToolCallDto[]（含入参Arguments和执行结果Result）")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ToolCalls", "工具调用。role=assistant时存实际调用链路ToolCallDto[]（含入参Arguments和执行结果Result）", "", ShowIn = "Auto,-List,-Search")]
    public String? ToolCalls { get => _ToolCalls; set { if (OnPropertyChanging("ToolCalls", value)) { _ToolCalls = value; OnPropertyChanged("ToolCalls"); } } }

    private String? _ModelName;
    /// <summary>模型名称。实际使用的模型编码，方便回溯</summary>
    [DisplayName("模型名称")]
    [Description("模型名称。实际使用的模型编码，方便回溯")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ModelName", "模型名称。实际使用的模型编码，方便回溯", "")]
    public String? ModelName { get => _ModelName; set { if (OnPropertyChanging("ModelName", value)) { _ModelName = value; OnPropertyChanged("ModelName"); } } }

    private Int32 _MaxTokens;
    /// <summary>最大Token数。本次请求的最大生成Token数限制</summary>
    [DisplayName("最大Token数")]
    [Description("最大Token数。本次请求的最大生成Token数限制")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("MaxTokens", "最大Token数。本次请求的最大生成Token数限制", "")]
    public Int32 MaxTokens { get => _MaxTokens; set { if (OnPropertyChanging("MaxTokens", value)) { _MaxTokens = value; OnPropertyChanged("MaxTokens"); } } }

    private Double _Temperature;
    /// <summary>温度。本次请求的采样温度参数</summary>
    [DisplayName("温度")]
    [Description("温度。本次请求的采样温度参数")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Temperature", "温度。本次请求的采样温度参数", "")]
    public Double Temperature { get => _Temperature; set { if (OnPropertyChanging("Temperature", value)) { _Temperature = value; OnPropertyChanged("Temperature"); } } }

    private String? _FinishReason;
    /// <summary>完成原因。stop=正常结束/length=截断/tool_calls=工具调用/error=异常</summary>
    [DisplayName("完成原因")]
    [Description("完成原因。stop=正常结束/length=截断/tool_calls=工具调用/error=异常")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("FinishReason", "完成原因。stop=正常结束/length=截断/tool_calls=工具调用/error=异常", "")]
    public String? FinishReason { get => _FinishReason; set { if (OnPropertyChanging("FinishReason", value)) { _FinishReason = value; OnPropertyChanged("FinishReason"); } } }

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

    private Decimal _TotalCost;
    /// <summary>总费用。本条消息消耗费用，单位：元</summary>
    [DisplayName("总费用")]
    [Description("总费用。本条消息消耗费用，单位：元")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalCost", "总费用。本条消息消耗费用，单位：元", "", Precision = 18, Scale = 4)]
    public Decimal TotalCost { get => _TotalCost; set { if (OnPropertyChanging("TotalCost", value)) { _TotalCost = value; OnPropertyChanged("TotalCost"); } } }

    private Int32 _ElapsedMs;
    /// <summary>耗时。毫秒</summary>
    [DisplayName("耗时")]
    [Description("耗时。毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ElapsedMs", "耗时。毫秒", "")]
    public Int32 ElapsedMs { get => _ElapsedMs; set { if (OnPropertyChanging("ElapsedMs", value)) { _ElapsedMs = value; OnPropertyChanged("ElapsedMs"); } } }

    private String? _TraceId;
    /// <summary>链路。方便问题排查</summary>
    [Category("扩展")]
    [DisplayName("链路")]
    [Description("链路。方便问题排查")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("TraceId", "链路。方便问题排查", "")]
    public String? TraceId { get => _TraceId; set { if (OnPropertyChanging("TraceId", value)) { _TraceId = value; OnPropertyChanged("TraceId"); } } }

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
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IChatMessage model)
    {
        Id = model.Id;
        ConversationId = model.ConversationId;
        ParentId = model.ParentId;
        BranchIndex = model.BranchIndex;
        Role = model.Role;
        Content = model.Content;
        ThinkingContent = model.ThinkingContent;
        ThinkingMode = model.ThinkingMode;
        Attachments = model.Attachments;
        SkillNames = model.SkillNames;
        ToolNames = model.ToolNames;
        ToolCalls = model.ToolCalls;
        ModelName = model.ModelName;
        MaxTokens = model.MaxTokens;
        Temperature = model.Temperature;
        FinishReason = model.FinishReason;
        InputTokens = model.InputTokens;
        OutputTokens = model.OutputTokens;
        TotalTokens = model.TotalTokens;
        TotalCost = model.TotalCost;
        ElapsedMs = model.ElapsedMs;
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
            "ParentId" => _ParentId,
            "BranchIndex" => _BranchIndex,
            "Role" => _Role,
            "Content" => _Content,
            "ThinkingContent" => _ThinkingContent,
            "ThinkingMode" => _ThinkingMode,
            "Attachments" => _Attachments,
            "SkillNames" => _SkillNames,
            "ToolNames" => _ToolNames,
            "ToolCalls" => _ToolCalls,
            "ModelName" => _ModelName,
            "MaxTokens" => _MaxTokens,
            "Temperature" => _Temperature,
            "FinishReason" => _FinishReason,
            "InputTokens" => _InputTokens,
            "OutputTokens" => _OutputTokens,
            "TotalTokens" => _TotalTokens,
            "TotalCost" => _TotalCost,
            "ElapsedMs" => _ElapsedMs,
            "TraceId" => _TraceId,
            "CreateUserID" => _CreateUserID,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            _ => base[name]
        };
        set
        {
            switch (name)
            {
                case "Id": _Id = value.ToLong(); break;
                case "ConversationId": _ConversationId = value.ToLong(); break;
                case "ParentId": _ParentId = value.ToLong(); break;
                case "BranchIndex": _BranchIndex = value.ToInt(); break;
                case "Role": _Role = Convert.ToString(value); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "ThinkingContent": _ThinkingContent = Convert.ToString(value); break;
                case "ThinkingMode": _ThinkingMode = (NewLife.AI.Models.ThinkingMode)value.ToInt(); break;
                case "Attachments": _Attachments = Convert.ToString(value); break;
                case "SkillNames": _SkillNames = Convert.ToString(value); break;
                case "ToolNames": _ToolNames = Convert.ToString(value); break;
                case "ToolCalls": _ToolCalls = Convert.ToString(value); break;
                case "ModelName": _ModelName = Convert.ToString(value); break;
                case "MaxTokens": _MaxTokens = value.ToInt(); break;
                case "Temperature": _Temperature = value.ToDouble(); break;
                case "FinishReason": _FinishReason = Convert.ToString(value); break;
                case "InputTokens": _InputTokens = value.ToInt(); break;
                case "OutputTokens": _OutputTokens = value.ToInt(); break;
                case "TotalTokens": _TotalTokens = value.ToInt(); break;
                case "TotalCost": _TotalCost = Convert.ToDecimal(value); break;
                case "ElapsedMs": _ElapsedMs = value.ToInt(); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
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

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static ChatMessage? FindById(Int64 id)
    {
        if (id < 0) return null;

        return Find(_.Id == id);
    }

    /// <summary>根据会话查找</summary>
    /// <param name="conversationId">会话</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> FindAllByConversationId(Int64 conversationId)
    {
        if (conversationId < 0) return [];

        return FindAll(_.ConversationId == conversationId);
    }

    /// <summary>根据父消息查找</summary>
    /// <param name="parentId">父消息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> FindAllByParentId(Int64 parentId)
    {
        if (parentId < 0) return [];

        return FindAll(_.ParentId == parentId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="conversationId">会话。所属会话</param>
    /// <param name="parentId">父消息。消息树父节点ID，0表示对话根节点。用于对话分叉时记录消息的层级关系</param>
    /// <param name="thinkingMode">思考模式。Auto=0自动, Think=1思考, Fast=2快速</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> Search(Int64 conversationId, Int64 parentId, NewLife.AI.Models.ThinkingMode thinkingMode, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (conversationId >= 0) exp &= _.ConversationId == conversationId;
        if (parentId >= 0) exp &= _.ParentId == parentId;
        if (thinkingMode >= 0) exp &= _.ThinkingMode == thinkingMode;
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
    /// <summary>取得对话消息字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>会话。所属会话</summary>
        public static readonly Field ConversationId = FindByName("ConversationId");

        /// <summary>父消息。消息树父节点ID，0表示对话根节点。用于对话分叉时记录消息的层级关系</summary>
        public static readonly Field ParentId = FindByName("ParentId");

        /// <summary>分支序号。同一父节点下的分支序号，0=主线，1/2...=分叉支线</summary>
        public static readonly Field BranchIndex = FindByName("BranchIndex");

        /// <summary>角色。User=用户, Assistant=AI助手</summary>
        public static readonly Field Role = FindByName("Role");

        /// <summary>内容。Markdown格式文本</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>思考内容。role=assistant时存AI推理过程；role=user时存本轮注入的系统上下文全文（含技能提示词与记忆注入，调试用）</summary>
        public static readonly Field ThinkingContent = FindByName("ThinkingContent");

        /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
        public static readonly Field ThinkingMode = FindByName("ThinkingMode");

        /// <summary>附件列表。存储魔方附件ID数组</summary>
        public static readonly Field Attachments = FindByName("Attachments");

        /// <summary>技能列表。本轮激活的技能名称，多个逗号分隔</summary>
        public static readonly Field SkillNames = FindByName("SkillNames");

        /// <summary>工具列表。role=user时记录本轮可用工具名（逗号分隔）；role=assistant时记录实际调用的工具名（逗号分隔）</summary>
        public static readonly Field ToolNames = FindByName("ToolNames");

        /// <summary>工具调用。role=assistant时存实际调用链路ToolCallDto[]（含入参Arguments和执行结果Result）</summary>
        public static readonly Field ToolCalls = FindByName("ToolCalls");

        /// <summary>模型名称。实际使用的模型编码，方便回溯</summary>
        public static readonly Field ModelName = FindByName("ModelName");

        /// <summary>最大Token数。本次请求的最大生成Token数限制</summary>
        public static readonly Field MaxTokens = FindByName("MaxTokens");

        /// <summary>温度。本次请求的采样温度参数</summary>
        public static readonly Field Temperature = FindByName("Temperature");

        /// <summary>完成原因。stop=正常结束/length=截断/tool_calls=工具调用/error=异常</summary>
        public static readonly Field FinishReason = FindByName("FinishReason");

        /// <summary>输入Token数</summary>
        public static readonly Field InputTokens = FindByName("InputTokens");

        /// <summary>输出Token数</summary>
        public static readonly Field OutputTokens = FindByName("OutputTokens");

        /// <summary>总Token数</summary>
        public static readonly Field TotalTokens = FindByName("TotalTokens");

        /// <summary>总费用。本条消息消耗费用，单位：元</summary>
        public static readonly Field TotalCost = FindByName("TotalCost");

        /// <summary>耗时。毫秒</summary>
        public static readonly Field ElapsedMs = FindByName("ElapsedMs");

        /// <summary>链路。方便问题排查</summary>
        public static readonly Field TraceId = FindByName("TraceId");

        /// <summary>创建用户</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得对话消息字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>会话。所属会话</summary>
        public const String ConversationId = "ConversationId";

        /// <summary>父消息。消息树父节点ID，0表示对话根节点。用于对话分叉时记录消息的层级关系</summary>
        public const String ParentId = "ParentId";

        /// <summary>分支序号。同一父节点下的分支序号，0=主线，1/2...=分叉支线</summary>
        public const String BranchIndex = "BranchIndex";

        /// <summary>角色。User=用户, Assistant=AI助手</summary>
        public const String Role = "Role";

        /// <summary>内容。Markdown格式文本</summary>
        public const String Content = "Content";

        /// <summary>思考内容。role=assistant时存AI推理过程；role=user时存本轮注入的系统上下文全文（含技能提示词与记忆注入，调试用）</summary>
        public const String ThinkingContent = "ThinkingContent";

        /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
        public const String ThinkingMode = "ThinkingMode";

        /// <summary>附件列表。存储魔方附件ID数组</summary>
        public const String Attachments = "Attachments";

        /// <summary>技能列表。本轮激活的技能名称，多个逗号分隔</summary>
        public const String SkillNames = "SkillNames";

        /// <summary>工具列表。role=user时记录本轮可用工具名（逗号分隔）；role=assistant时记录实际调用的工具名（逗号分隔）</summary>
        public const String ToolNames = "ToolNames";

        /// <summary>工具调用。role=assistant时存实际调用链路ToolCallDto[]（含入参Arguments和执行结果Result）</summary>
        public const String ToolCalls = "ToolCalls";

        /// <summary>模型名称。实际使用的模型编码，方便回溯</summary>
        public const String ModelName = "ModelName";

        /// <summary>最大Token数。本次请求的最大生成Token数限制</summary>
        public const String MaxTokens = "MaxTokens";

        /// <summary>温度。本次请求的采样温度参数</summary>
        public const String Temperature = "Temperature";

        /// <summary>完成原因。stop=正常结束/length=截断/tool_calls=工具调用/error=异常</summary>
        public const String FinishReason = "FinishReason";

        /// <summary>输入Token数</summary>
        public const String InputTokens = "InputTokens";

        /// <summary>输出Token数</summary>
        public const String OutputTokens = "OutputTokens";

        /// <summary>总Token数</summary>
        public const String TotalTokens = "TotalTokens";

        /// <summary>总费用。本条消息消耗费用，单位：元</summary>
        public const String TotalCost = "TotalCost";

        /// <summary>耗时。毫秒</summary>
        public const String ElapsedMs = "ElapsedMs";

        /// <summary>链路。方便问题排查</summary>
        public const String TraceId = "TraceId";

        /// <summary>创建用户</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";
    }
    #endregion
}
