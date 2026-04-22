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

/// <summary>会话。一次完整的多轮对话上下文</summary>
[Serializable]
[DataObject]
[Description("会话。一次完整的多轮对话上下文")]
[BindIndex("IX_Conversation_UserId_Id", false, "UserId,Id")]
[BindIndex("IX_Conversation_UserId_ProjectId_Id", false, "UserId,ProjectId,Id")]
[BindIndex("IX_Conversation_UserId_IsPinned_Id", false, "UserId,IsPinned,Id")]
[BindIndex("IX_Conversation_Source", false, "Source")]
[BindTable("Conversation", Description = "会话。一次完整的多轮对话上下文", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class Conversation : IConversation, IEntity<IConversation>
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

    private Int32 _ProjectId;
    /// <summary>项目。所属项目编号，0=个人对话</summary>
    [DisplayName("项目")]
    [Description("项目。所属项目编号，0=个人对话")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ProjectId", "项目。所属项目编号，0=个人对话", "", DefaultValue = "0")]
    public Int32 ProjectId { get => _ProjectId; set { if (OnPropertyChanging("ProjectId", value)) { _ProjectId = value; OnPropertyChanged("ProjectId"); } } }

    private String? _UserName;
    /// <summary>用户名</summary>
    [DisplayName("用户名")]
    [Description("用户名")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UserName", "用户名", "")]
    public String? UserName { get => _UserName; set { if (OnPropertyChanging("UserName", value)) { _UserName = value; OnPropertyChanged("UserName"); } } }

    private String? _Title;
    /// <summary>标题。会话标题，显示在侧边栏</summary>
    [DisplayName("标题")]
    [Description("标题。会话标题，显示在侧边栏")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Title", "标题。会话标题，显示在侧边栏", "", Master = true)]
    public String? Title { get => _Title; set { if (OnPropertyChanging("Title", value)) { _Title = value; OnPropertyChanged("Title"); } } }

    private Int32 _ModelId;
    /// <summary>模型。当前使用的模型Id，引用ModelConfig.Id</summary>
    [DisplayName("模型")]
    [Description("模型。当前使用的模型Id，引用ModelConfig.Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ModelId", "模型。当前使用的模型Id，引用ModelConfig.Id", "")]
    public Int32 ModelId { get => _ModelId; set { if (OnPropertyChanging("ModelId", value)) { _ModelId = value; OnPropertyChanged("ModelId"); } } }

    private String? _ModelName;
    /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
    [DisplayName("模型名称")]
    [Description("模型名称。冗余存储模型名称，方便历史数据检索")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("ModelName", "模型名称。冗余存储模型名称，方便历史数据检索", "")]
    public String? ModelName { get => _ModelName; set { if (OnPropertyChanging("ModelName", value)) { _ModelName = value; OnPropertyChanged("ModelName"); } } }

    private Int32 _SkillId;
    /// <summary>技能。当前会话使用的技能，引用Skill.Id</summary>
    [DisplayName("技能")]
    [Description("技能。当前会话使用的技能，引用Skill.Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("SkillId", "技能。当前会话使用的技能，引用Skill.Id", "")]
    public Int32 SkillId { get => _SkillId; set { if (OnPropertyChanging("SkillId", value)) { _SkillId = value; OnPropertyChanged("SkillId"); } } }

    private String? _SkillName;
    /// <summary>技能名称。冗余存储技能名称，方便历史数据检索</summary>
    [DisplayName("技能名称")]
    [Description("技能名称。冗余存储技能名称，方便历史数据检索")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SkillName", "技能名称。冗余存储技能名称，方便历史数据检索", "")]
    public String? SkillName { get => _SkillName; set { if (OnPropertyChanging("SkillName", value)) { _SkillName = value; OnPropertyChanged("SkillName"); } } }

    private NewLife.AI.Models.ThinkingMode _ThinkingMode;
    /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
    [DisplayName("思考模式")]
    [Description("思考模式。Auto=0自动, Think=1思考, Fast=2快速")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ThinkingMode", "思考模式。Auto=0自动, Think=1思考, Fast=2快速", "")]
    public NewLife.AI.Models.ThinkingMode ThinkingMode { get => _ThinkingMode; set { if (OnPropertyChanging("ThinkingMode", value)) { _ThinkingMode = value; OnPropertyChanged("ThinkingMode"); } } }

    private String? _KnowledgeBases;
    /// <summary>知识库。逗号分隔的知识库ID列表，会话级绑定</summary>
    [DisplayName("知识库")]
    [Description("知识库。逗号分隔的知识库ID列表，会话级绑定")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("KnowledgeBases", "知识库。逗号分隔的知识库ID列表，会话级绑定", "")]
    public String? KnowledgeBases { get => _KnowledgeBases; set { if (OnPropertyChanging("KnowledgeBases", value)) { _KnowledgeBases = value; OnPropertyChanged("KnowledgeBases"); } } }

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

    private Int32 _InputTokens;
    /// <summary>输入Token数。会话累计消耗的输入Token数（含额外LLM调用）</summary>
    [DisplayName("输入Token数")]
    [Description("输入Token数。会话累计消耗的输入Token数（含额外LLM调用）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("InputTokens", "输入Token数。会话累计消耗的输入Token数（含额外LLM调用）", "")]
    public Int32 InputTokens { get => _InputTokens; set { if (OnPropertyChanging("InputTokens", value)) { _InputTokens = value; OnPropertyChanged("InputTokens"); } } }

    private Int32 _OutputTokens;
    /// <summary>输出Token数。会话累计消耗的输出Token数（含额外LLM调用）</summary>
    [DisplayName("输出Token数")]
    [Description("输出Token数。会话累计消耗的输出Token数（含额外LLM调用）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("OutputTokens", "输出Token数。会话累计消耗的输出Token数（含额外LLM调用）", "")]
    public Int32 OutputTokens { get => _OutputTokens; set { if (OnPropertyChanging("OutputTokens", value)) { _OutputTokens = value; OnPropertyChanged("OutputTokens"); } } }

    private Int32 _TotalTokens;
    /// <summary>总Token数。会话累计消耗的总Token数（含额外LLM调用）</summary>
    [DisplayName("总Token数")]
    [Description("总Token数。会话累计消耗的总Token数（含额外LLM调用）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalTokens", "总Token数。会话累计消耗的总Token数（含额外LLM调用）", "")]
    public Int32 TotalTokens { get => _TotalTokens; set { if (OnPropertyChanging("TotalTokens", value)) { _TotalTokens = value; OnPropertyChanged("TotalTokens"); } } }

    private Decimal _TotalCost;
    /// <summary>总费用。会话累计消耗的总费用，单位：元（含标题/压缩/记忆等额外LLM调用）</summary>
    [DisplayName("总费用")]
    [Description("总费用。会话累计消耗的总费用，单位：元（含标题/压缩/记忆等额外LLM调用）")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("TotalCost", "总费用。会话累计消耗的总费用，单位：元（含标题/压缩/记忆等额外LLM调用）", "", Precision = 18, Scale = 6)]
    public Decimal TotalCost { get => _TotalCost; set { if (OnPropertyChanging("TotalCost", value)) { _TotalCost = value; OnPropertyChanged("TotalCost"); } } }

    private Int32 _ElapsedMs;
    /// <summary>耗时。毫秒</summary>
    [DisplayName("耗时")]
    [Description("耗时。毫秒")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ElapsedMs", "耗时。毫秒", "")]
    public Int32 ElapsedMs { get => _ElapsedMs; set { if (OnPropertyChanging("ElapsedMs", value)) { _ElapsedMs = value; OnPropertyChanged("ElapsedMs"); } } }

    private String? _Source;
    /// <summary>来源。Web/Gateway/Channel等，标识对话入口</summary>
    [DisplayName("来源")]
    [Description("来源。Web/Gateway/Channel等，标识对话入口")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Source", "来源。Web/Gateway/Channel等，标识对话入口", "")]
    public String? Source { get => _Source; set { if (OnPropertyChanging("Source", value)) { _Source = value; OnPropertyChanged("Source"); } } }

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

    private String? _Remark;
    /// <summary>备注</summary>
    [Category("扩展")]
    [DisplayName("备注")]
    [Description("备注")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Remark", "备注", "")]
    public String? Remark { get => _Remark; set { if (OnPropertyChanging("Remark", value)) { _Remark = value; OnPropertyChanged("Remark"); } } }
    #endregion

    #region 拷贝
    /// <summary>拷贝模型对象</summary>
    /// <param name="model">模型</param>
    public void Copy(IConversation model)
    {
        Id = model.Id;
        UserId = model.UserId;
        ProjectId = model.ProjectId;
        UserName = model.UserName;
        Title = model.Title;
        ModelId = model.ModelId;
        ModelName = model.ModelName;
        SkillId = model.SkillId;
        SkillName = model.SkillName;
        ThinkingMode = model.ThinkingMode;
        KnowledgeBases = model.KnowledgeBases;
        IsPinned = model.IsPinned;
        MessageCount = model.MessageCount;
        LastMessageTime = model.LastMessageTime;
        InputTokens = model.InputTokens;
        OutputTokens = model.OutputTokens;
        TotalTokens = model.TotalTokens;
        TotalCost = model.TotalCost;
        ElapsedMs = model.ElapsedMs;
        Source = model.Source;
        Remark = model.Remark;
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
            "UserName" => _UserName,
            "Title" => _Title,
            "ModelId" => _ModelId,
            "ModelName" => _ModelName,
            "SkillId" => _SkillId,
            "SkillName" => _SkillName,
            "ThinkingMode" => _ThinkingMode,
            "KnowledgeBases" => _KnowledgeBases,
            "IsPinned" => _IsPinned,
            "MessageCount" => _MessageCount,
            "LastMessageTime" => _LastMessageTime,
            "InputTokens" => _InputTokens,
            "OutputTokens" => _OutputTokens,
            "TotalTokens" => _TotalTokens,
            "TotalCost" => _TotalCost,
            "ElapsedMs" => _ElapsedMs,
            "Source" => _Source,
            "TraceId" => _TraceId,
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
                case "ProjectId": _ProjectId = value.ToInt(); break;
                case "UserName": _UserName = Convert.ToString(value); break;
                case "Title": _Title = Convert.ToString(value); break;
                case "ModelId": _ModelId = value.ToInt(); break;
                case "ModelName": _ModelName = Convert.ToString(value); break;
                case "SkillId": _SkillId = value.ToInt(); break;
                case "SkillName": _SkillName = Convert.ToString(value); break;
                case "ThinkingMode": _ThinkingMode = (NewLife.AI.Models.ThinkingMode)value.ToInt(); break;
                case "KnowledgeBases": _KnowledgeBases = Convert.ToString(value); break;
                case "IsPinned": _IsPinned = value.ToBoolean(); break;
                case "MessageCount": _MessageCount = value.ToInt(); break;
                case "LastMessageTime": _LastMessageTime = value.ToDateTime(); break;
                case "InputTokens": _InputTokens = value.ToInt(); break;
                case "OutputTokens": _OutputTokens = value.ToInt(); break;
                case "TotalTokens": _TotalTokens = value.ToInt(); break;
                case "TotalCost": _TotalCost = Convert.ToDecimal(value); break;
                case "ElapsedMs": _ElapsedMs = value.ToInt(); break;
                case "Source": _Source = Convert.ToString(value); break;
                case "TraceId": _TraceId = Convert.ToString(value); break;
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
    /// <summary>模型</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public ModelConfig? Model => Extends.Get(nameof(Model), k => ModelConfig.FindById(ModelId));

    /// <summary>技能</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Skill? Skill => Extends.Get(nameof(Skill), k => Skill.FindById(SkillId));

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static Conversation? FindById(Int64 id)
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

    /// <summary>根据用户、项目查找</summary>
    /// <param name="userId">用户</param>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<Conversation> FindAllByUserIdAndProjectId(Int32 userId, Int32 projectId)
    {
        if (userId < 0) return [];
        if (projectId < 0) return [];

        return FindAll(_.UserId == userId & _.ProjectId == projectId);
    }

    /// <summary>根据来源查找</summary>
    /// <param name="source">来源</param>
    /// <returns>实体列表</returns>
    public static IList<Conversation> FindAllBySource(String? source)
    {
        if (source == null) return [];

        return FindAll(_.Source == source);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户。会话所属用户</param>
    /// <param name="projectId">项目。所属项目编号，0=个人对话</param>
    /// <param name="isPinned">置顶。是否置顶显示</param>
    /// <param name="source">来源。Web/Gateway/Channel等，标识对话入口</param>
    /// <param name="modelId">模型。当前使用的模型Id，引用ModelConfig.Id</param>
    /// <param name="skillId">技能。当前会话使用的技能，引用Skill.Id</param>
    /// <param name="thinkingMode">思考模式。Auto=0自动, Think=1思考, Fast=2快速</param>
    /// <param name="start">编号开始</param>
    /// <param name="end">编号结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Conversation> Search(Int32 userId, Int32 projectId, Boolean? isPinned, String? source, Int32 modelId, Int32 skillId, NewLife.AI.Models.ThinkingMode thinkingMode, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (isPinned != null) exp &= _.IsPinned == isPinned;
        if (!source.IsNullOrEmpty()) exp &= _.Source == source;
        if (modelId >= 0) exp &= _.ModelId == modelId;
        if (skillId >= 0) exp &= _.SkillId == skillId;
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
    /// <summary>取得会话字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>用户。会话所属用户</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>项目。所属项目编号，0=个人对话</summary>
        public static readonly Field ProjectId = FindByName("ProjectId");

        /// <summary>用户名</summary>
        public static readonly Field UserName = FindByName("UserName");

        /// <summary>标题。会话标题，显示在侧边栏</summary>
        public static readonly Field Title = FindByName("Title");

        /// <summary>模型。当前使用的模型Id，引用ModelConfig.Id</summary>
        public static readonly Field ModelId = FindByName("ModelId");

        /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
        public static readonly Field ModelName = FindByName("ModelName");

        /// <summary>技能。当前会话使用的技能，引用Skill.Id</summary>
        public static readonly Field SkillId = FindByName("SkillId");

        /// <summary>技能名称。冗余存储技能名称，方便历史数据检索</summary>
        public static readonly Field SkillName = FindByName("SkillName");

        /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
        public static readonly Field ThinkingMode = FindByName("ThinkingMode");

        /// <summary>知识库。逗号分隔的知识库ID列表，会话级绑定</summary>
        public static readonly Field KnowledgeBases = FindByName("KnowledgeBases");

        /// <summary>置顶。是否置顶显示</summary>
        public static readonly Field IsPinned = FindByName("IsPinned");

        /// <summary>消息数</summary>
        public static readonly Field MessageCount = FindByName("MessageCount");

        /// <summary>最后消息时间。用于排序</summary>
        public static readonly Field LastMessageTime = FindByName("LastMessageTime");

        /// <summary>输入Token数。会话累计消耗的输入Token数（含额外LLM调用）</summary>
        public static readonly Field InputTokens = FindByName("InputTokens");

        /// <summary>输出Token数。会话累计消耗的输出Token数（含额外LLM调用）</summary>
        public static readonly Field OutputTokens = FindByName("OutputTokens");

        /// <summary>总Token数。会话累计消耗的总Token数（含额外LLM调用）</summary>
        public static readonly Field TotalTokens = FindByName("TotalTokens");

        /// <summary>总费用。会话累计消耗的总费用，单位：元（含标题/压缩/记忆等额外LLM调用）</summary>
        public static readonly Field TotalCost = FindByName("TotalCost");

        /// <summary>耗时。毫秒</summary>
        public static readonly Field ElapsedMs = FindByName("ElapsedMs");

        /// <summary>来源。Web/Gateway/Channel等，标识对话入口</summary>
        public static readonly Field Source = FindByName("Source");

        /// <summary>链路。方便问题排查</summary>
        public static readonly Field TraceId = FindByName("TraceId");

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

        static Field FindByName(String name) => Meta.Table.FindByName(name)!;
    }

    /// <summary>取得会话字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户。会话所属用户</summary>
        public const String UserId = "UserId";

        /// <summary>项目。所属项目编号，0=个人对话</summary>
        public const String ProjectId = "ProjectId";

        /// <summary>用户名</summary>
        public const String UserName = "UserName";

        /// <summary>标题。会话标题，显示在侧边栏</summary>
        public const String Title = "Title";

        /// <summary>模型。当前使用的模型Id，引用ModelConfig.Id</summary>
        public const String ModelId = "ModelId";

        /// <summary>模型名称。冗余存储模型名称，方便历史数据检索</summary>
        public const String ModelName = "ModelName";

        /// <summary>技能。当前会话使用的技能，引用Skill.Id</summary>
        public const String SkillId = "SkillId";

        /// <summary>技能名称。冗余存储技能名称，方便历史数据检索</summary>
        public const String SkillName = "SkillName";

        /// <summary>思考模式。Auto=0自动, Think=1思考, Fast=2快速</summary>
        public const String ThinkingMode = "ThinkingMode";

        /// <summary>知识库。逗号分隔的知识库ID列表，会话级绑定</summary>
        public const String KnowledgeBases = "KnowledgeBases";

        /// <summary>置顶。是否置顶显示</summary>
        public const String IsPinned = "IsPinned";

        /// <summary>消息数</summary>
        public const String MessageCount = "MessageCount";

        /// <summary>最后消息时间。用于排序</summary>
        public const String LastMessageTime = "LastMessageTime";

        /// <summary>输入Token数。会话累计消耗的输入Token数（含额外LLM调用）</summary>
        public const String InputTokens = "InputTokens";

        /// <summary>输出Token数。会话累计消耗的输出Token数（含额外LLM调用）</summary>
        public const String OutputTokens = "OutputTokens";

        /// <summary>总Token数。会话累计消耗的总Token数（含额外LLM调用）</summary>
        public const String TotalTokens = "TotalTokens";

        /// <summary>总费用。会话累计消耗的总费用，单位：元（含标题/压缩/记忆等额外LLM调用）</summary>
        public const String TotalCost = "TotalCost";

        /// <summary>耗时。毫秒</summary>
        public const String ElapsedMs = "ElapsedMs";

        /// <summary>来源。Web/Gateway/Channel等，标识对话入口</summary>
        public const String Source = "Source";

        /// <summary>链路。方便问题排查</summary>
        public const String TraceId = "TraceId";

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
