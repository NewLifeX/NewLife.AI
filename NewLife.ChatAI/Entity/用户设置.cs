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

/// <summary>用户设置。用户的个性化配置</summary>
[Serializable]
[DataObject]
[Description("用户设置。用户的个性化配置")]
[BindIndex("IU_UserSetting_UserId", true, "UserId")]
[BindTable("UserSetting", Description = "用户设置。用户的个性化配置", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class UserSetting
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private Int32 _UserId;
    /// <summary>用户。设置所属用户</summary>
    [DisplayName("用户")]
    [Description("用户。设置所属用户")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("UserId", "用户。设置所属用户", "")]
    public Int32 UserId { get => _UserId; set { if (OnPropertyChanging("UserId", value)) { _UserId = value; OnPropertyChanged("UserId"); } } }

    private String _Language;
    /// <summary>语言。zh-CN/zh-TW/en</summary>
    [DisplayName("语言")]
    [Description("语言。zh-CN/zh-TW/en")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Language", "语言。zh-CN/zh-TW/en", "")]
    public String Language { get => _Language; set { if (OnPropertyChanging("Language", value)) { _Language = value; OnPropertyChanged("Language"); } } }

    private String _Theme;
    /// <summary>主题。light/dark/system</summary>
    [DisplayName("主题")]
    [Description("主题。light/dark/system")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Theme", "主题。light/dark/system", "")]
    public String Theme { get => _Theme; set { if (OnPropertyChanging("Theme", value)) { _Theme = value; OnPropertyChanged("Theme"); } } }

    private Int32 _FontSize;
    /// <summary>字体大小。14~20，默认16</summary>
    [DisplayName("字体大小")]
    [Description("字体大小。14~20，默认16")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("FontSize", "字体大小。14~20，默认16", "")]
    public Int32 FontSize { get => _FontSize; set { if (OnPropertyChanging("FontSize", value)) { _FontSize = value; OnPropertyChanged("FontSize"); } } }

    private String _SendShortcut;
    /// <summary>发送快捷键。Enter或Ctrl+Enter</summary>
    [DisplayName("发送快捷键")]
    [Description("发送快捷键。Enter或Ctrl+Enter")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("SendShortcut", "发送快捷键。Enter或Ctrl+Enter", "")]
    public String SendShortcut { get => _SendShortcut; set { if (OnPropertyChanging("SendShortcut", value)) { _SendShortcut = value; OnPropertyChanged("SendShortcut"); } } }

    private Int32 _DefaultModel;
    /// <summary>默认模型。新会话的默认模型配置Id</summary>
    [DisplayName("默认模型")]
    [Description("默认模型。新会话的默认模型配置Id")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DefaultModel", "默认模型。新会话的默认模型配置Id", "")]
    public Int32 DefaultModel { get => _DefaultModel; set { if (OnPropertyChanging("DefaultModel", value)) { _DefaultModel = value; OnPropertyChanged("DefaultModel"); } } }

    private NewLife.AI.Models.ThinkingMode _DefaultThinkingMode;
    /// <summary>默认思考模式。Auto=0, Think=1, Fast=2</summary>
    [DisplayName("默认思考模式")]
    [Description("默认思考模式。Auto=0, Think=1, Fast=2")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("DefaultThinkingMode", "默认思考模式。Auto=0, Think=1, Fast=2", "")]
    public NewLife.AI.Models.ThinkingMode DefaultThinkingMode { get => _DefaultThinkingMode; set { if (OnPropertyChanging("DefaultThinkingMode", value)) { _DefaultThinkingMode = value; OnPropertyChanged("DefaultThinkingMode"); } } }

    private Int32 _ContextRounds;
    /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
    [DisplayName("上下文轮数")]
    [Description("上下文轮数。每次请求携带的历史对话轮数，默认10")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ContextRounds", "上下文轮数。每次请求携带的历史对话轮数，默认10", "")]
    public Int32 ContextRounds { get => _ContextRounds; set { if (OnPropertyChanging("ContextRounds", value)) { _ContextRounds = value; OnPropertyChanged("ContextRounds"); } } }

    private String _SystemPrompt;
    /// <summary>系统提示词。全局System Prompt</summary>
    [DisplayName("系统提示词")]
    [Description("系统提示词。全局System Prompt")]
    [DataObjectField(false, false, true, 2000)]
    [BindColumn("SystemPrompt", "系统提示词。全局System Prompt", "", ShowIn = "Auto,-List,-Search")]
    public String SystemPrompt { get => _SystemPrompt; set { if (OnPropertyChanging("SystemPrompt", value)) { _SystemPrompt = value; OnPropertyChanged("SystemPrompt"); } } }

    private Boolean _McpEnabled;
    /// <summary>启用MCP。是否启用MCP工具调用</summary>
    [DisplayName("启用MCP")]
    [Description("启用MCP。是否启用MCP工具调用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("McpEnabled", "启用MCP。是否启用MCP工具调用", "")]
    public Boolean McpEnabled { get => _McpEnabled; set { if (OnPropertyChanging("McpEnabled", value)) { _McpEnabled = value; OnPropertyChanged("McpEnabled"); } } }

    private Int32 _StreamingSpeed;
    /// <summary>流式速度。流式输出速度等级，1~5，默认3</summary>
    [DisplayName("流式速度")]
    [Description("流式速度。流式输出速度等级，1~5，默认3")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("StreamingSpeed", "流式速度。流式输出速度等级，1~5，默认3", "")]
    public Int32 StreamingSpeed { get => _StreamingSpeed; set { if (OnPropertyChanging("StreamingSpeed", value)) { _StreamingSpeed = value; OnPropertyChanged("StreamingSpeed"); } } }

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
            "Language" => _Language,
            "Theme" => _Theme,
            "FontSize" => _FontSize,
            "SendShortcut" => _SendShortcut,
            "DefaultModel" => _DefaultModel,
            "DefaultThinkingMode" => _DefaultThinkingMode,
            "ContextRounds" => _ContextRounds,
            "SystemPrompt" => _SystemPrompt,
            "McpEnabled" => _McpEnabled,
            "StreamingSpeed" => _StreamingSpeed,
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
                case "UserId": _UserId = value.ToInt(); break;
                case "Language": _Language = Convert.ToString(value); break;
                case "Theme": _Theme = Convert.ToString(value); break;
                case "FontSize": _FontSize = value.ToInt(); break;
                case "SendShortcut": _SendShortcut = Convert.ToString(value); break;
                case "DefaultModel": _DefaultModel = value.ToInt(); break;
                case "DefaultThinkingMode": _DefaultThinkingMode = (NewLife.AI.Models.ThinkingMode)value.ToInt(); break;
                case "ContextRounds": _ContextRounds = value.ToInt(); break;
                case "SystemPrompt": _SystemPrompt = Convert.ToString(value); break;
                case "McpEnabled": _McpEnabled = value.ToBoolean(); break;
                case "StreamingSpeed": _StreamingSpeed = value.ToInt(); break;
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
    public static UserSetting FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据用户查找</summary>
    /// <param name="userId">用户</param>
    /// <returns>实体对象</returns>
    public static UserSetting FindByUserId(Int32 userId)
    {
        if (userId < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.UserId == userId);

        return Find(_.UserId == userId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="userId">用户。设置所属用户</param>
    /// <param name="defaultThinkingMode">默认思考模式。Auto=0, Think=1, Fast=2</param>
    /// <param name="mcpEnabled">启用MCP。是否启用MCP工具调用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<UserSetting> Search(Int32 userId, NewLife.AI.Models.ThinkingMode defaultThinkingMode, Boolean? mcpEnabled, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (defaultThinkingMode >= 0) exp &= _.DefaultThinkingMode == defaultThinkingMode;
        if (mcpEnabled != null) exp &= _.McpEnabled == mcpEnabled;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得用户设置字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>用户。设置所属用户</summary>
        public static readonly Field UserId = FindByName("UserId");

        /// <summary>语言。zh-CN/zh-TW/en</summary>
        public static readonly Field Language = FindByName("Language");

        /// <summary>主题。light/dark/system</summary>
        public static readonly Field Theme = FindByName("Theme");

        /// <summary>字体大小。14~20，默认16</summary>
        public static readonly Field FontSize = FindByName("FontSize");

        /// <summary>发送快捷键。Enter或Ctrl+Enter</summary>
        public static readonly Field SendShortcut = FindByName("SendShortcut");

        /// <summary>默认模型。新会话的默认模型配置Id</summary>
        public static readonly Field DefaultModel = FindByName("DefaultModel");

        /// <summary>默认思考模式。Auto=0, Think=1, Fast=2</summary>
        public static readonly Field DefaultThinkingMode = FindByName("DefaultThinkingMode");

        /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
        public static readonly Field ContextRounds = FindByName("ContextRounds");

        /// <summary>系统提示词。全局System Prompt</summary>
        public static readonly Field SystemPrompt = FindByName("SystemPrompt");

        /// <summary>启用MCP。是否启用MCP工具调用</summary>
        public static readonly Field McpEnabled = FindByName("McpEnabled");

        /// <summary>流式速度。流式输出速度等级，1~5，默认3</summary>
        public static readonly Field StreamingSpeed = FindByName("StreamingSpeed");

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

    /// <summary>取得用户设置字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>用户。设置所属用户</summary>
        public const String UserId = "UserId";

        /// <summary>语言。zh-CN/zh-TW/en</summary>
        public const String Language = "Language";

        /// <summary>主题。light/dark/system</summary>
        public const String Theme = "Theme";

        /// <summary>字体大小。14~20，默认16</summary>
        public const String FontSize = "FontSize";

        /// <summary>发送快捷键。Enter或Ctrl+Enter</summary>
        public const String SendShortcut = "SendShortcut";

        /// <summary>默认模型。新会话的默认模型配置Id</summary>
        public const String DefaultModel = "DefaultModel";

        /// <summary>默认思考模式。Auto=0, Think=1, Fast=2</summary>
        public const String DefaultThinkingMode = "DefaultThinkingMode";

        /// <summary>上下文轮数。每次请求携带的历史对话轮数，默认10</summary>
        public const String ContextRounds = "ContextRounds";

        /// <summary>系统提示词。全局System Prompt</summary>
        public const String SystemPrompt = "SystemPrompt";

        /// <summary>启用MCP。是否启用MCP工具调用</summary>
        public const String McpEnabled = "McpEnabled";

        /// <summary>流式速度。流式输出速度等级，1~5，默认3</summary>
        public const String StreamingSpeed = "StreamingSpeed";

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
