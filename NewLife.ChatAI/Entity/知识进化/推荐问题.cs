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

/// <summary>推荐问题。欢迎页展示的推荐问题，支持缓存响应以加速体验</summary>
[Serializable]
[DataObject]
[Description("推荐问题。欢迎页展示的推荐问题，支持缓存响应以加速体验")]
[BindIndex("IX_SuggestedQuestion_Sort", false, "Sort")]
[BindIndex("IX_SuggestedQuestion_Enable_Sort", false, "Enable,Sort")]
[BindTable("SuggestedQuestion", Description = "推荐问题。欢迎页展示的推荐问题，支持缓存响应以加速体验", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class SuggestedQuestion : ISuggestedQuestion, IEntity<ISuggestedQuestion>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Question;
    /// <summary>问题。推荐问题文本</summary>
    [DisplayName("问题")]
    [Description("问题。推荐问题文本")]
    [DataObjectField(false, false, true, 200)]
    [BindColumn("Question", "问题。推荐问题文本", "", Master = true)]
    public String? Question { get => _Question; set { if (OnPropertyChanging("Question", value)) { _Question = value; OnPropertyChanged("Question"); } } }

    private String? _Response;
    /// <summary>响应。缓存的AI回复内容，Markdown格式</summary>
    [DisplayName("响应")]
    [Description("响应。缓存的AI回复内容，Markdown格式")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Response", "响应。缓存的AI回复内容，Markdown格式", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? Response { get => _Response; set { if (OnPropertyChanging("Response", value)) { _Response = value; OnPropertyChanged("Response"); } } }

    private String? _ThinkingResponse;
    /// <summary>推理响应。缓存的思考过程内容</summary>
    [DisplayName("推理响应")]
    [Description("推理响应。缓存的思考过程内容")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("ThinkingResponse", "推理响应。缓存的思考过程内容", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? ThinkingResponse { get => _ThinkingResponse; set { if (OnPropertyChanging("ThinkingResponse", value)) { _ThinkingResponse = value; OnPropertyChanged("ThinkingResponse"); } } }

    private Int32 _ModelId;
    /// <summary>模型。生成缓存响应时使用的模型</summary>
    [DisplayName("模型")]
    [Description("模型。生成缓存响应时使用的模型")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("ModelId", "模型。生成缓存响应时使用的模型", "")]
    public Int32 ModelId { get => _ModelId; set { if (OnPropertyChanging("ModelId", value)) { _ModelId = value; OnPropertyChanged("ModelId"); } } }

    private String? _Icon;
    /// <summary>图标。Material Icon名称，如chat_bubble_outline</summary>
    [DisplayName("图标")]
    [Description("图标。Material Icon名称，如chat_bubble_outline")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Icon", "图标。Material Icon名称，如chat_bubble_outline", "")]
    public String? Icon { get => _Icon; set { if (OnPropertyChanging("Icon", value)) { _Icon = value; OnPropertyChanged("Icon"); } } }

    private String? _Color;
    /// <summary>颜色。图标CSS颜色类，如text-blue-500</summary>
    [DisplayName("颜色")]
    [Description("颜色。图标CSS颜色类，如text-blue-500")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Color", "颜色。图标CSS颜色类，如text-blue-500", "")]
    public String? Color { get => _Color; set { if (OnPropertyChanging("Color", value)) { _Color = value; OnPropertyChanged("Color"); } } }

    private Int32 _Sort;
    /// <summary>排序。越大越靠前</summary>
    [DisplayName("排序")]
    [Description("排序。越大越靠前")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Sort", "排序。越大越靠前", "")]
    public Int32 Sort { get => _Sort; set { if (OnPropertyChanging("Sort", value)) { _Sort = value; OnPropertyChanged("Sort"); } } }

    private Boolean _Enable;
    /// <summary>启用</summary>
    [DisplayName("启用")]
    [Description("启用")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Enable", "启用", "")]
    public Boolean Enable { get => _Enable; set { if (OnPropertyChanging("Enable", value)) { _Enable = value; OnPropertyChanged("Enable"); } } }

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
    public void Copy(ISuggestedQuestion model)
    {
        Id = model.Id;
        Question = model.Question;
        Response = model.Response;
        ThinkingResponse = model.ThinkingResponse;
        ModelId = model.ModelId;
        Icon = model.Icon;
        Color = model.Color;
        Sort = model.Sort;
        Enable = model.Enable;
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
            "Question" => _Question,
            "Response" => _Response,
            "ThinkingResponse" => _ThinkingResponse,
            "ModelId" => _ModelId,
            "Icon" => _Icon,
            "Color" => _Color,
            "Sort" => _Sort,
            "Enable" => _Enable,
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
                case "Question": _Question = Convert.ToString(value); break;
                case "Response": _Response = Convert.ToString(value); break;
                case "ThinkingResponse": _ThinkingResponse = Convert.ToString(value); break;
                case "ModelId": _ModelId = value.ToInt(); break;
                case "Icon": _Icon = Convert.ToString(value); break;
                case "Color": _Color = Convert.ToString(value); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
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
    /// <summary>模型</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public ModelConfig? Model => Extends.Get(nameof(Model), k => ModelConfig.FindById(ModelId));

    /// <summary>模型</summary>
    [Map(nameof(ModelId), typeof(ModelConfig), "Id")]
    public String? ModelName => Model?.Name;

    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static SuggestedQuestion? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据排序查找</summary>
    /// <param name="sort">排序</param>
    /// <returns>实体列表</returns>
    public static IList<SuggestedQuestion> FindAllBySort(Int32 sort)
    {
        if (sort < 0) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Sort == sort);

        return FindAll(_.Sort == sort);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="sort">排序。越大越靠前</param>
    /// <param name="enable">启用</param>
    /// <param name="modelId">模型。生成缓存响应时使用的模型</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<SuggestedQuestion> Search(Int32 sort, Boolean? enable, Int32 modelId, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (sort >= 0) exp &= _.Sort == sort;
        if (enable != null) exp &= _.Enable == enable;
        if (modelId >= 0) exp &= _.ModelId == modelId;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得推荐问题字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>问题。推荐问题文本</summary>
        public static readonly Field Question = FindByName("Question");

        /// <summary>响应。缓存的AI回复内容，Markdown格式</summary>
        public static readonly Field Response = FindByName("Response");

        /// <summary>推理响应。缓存的思考过程内容</summary>
        public static readonly Field ThinkingResponse = FindByName("ThinkingResponse");

        /// <summary>模型。生成缓存响应时使用的模型</summary>
        public static readonly Field ModelId = FindByName("ModelId");

        /// <summary>图标。Material Icon名称，如chat_bubble_outline</summary>
        public static readonly Field Icon = FindByName("Icon");

        /// <summary>颜色。图标CSS颜色类，如text-blue-500</summary>
        public static readonly Field Color = FindByName("Color");

        /// <summary>排序。越大越靠前</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

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

    /// <summary>取得推荐问题字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>问题。推荐问题文本</summary>
        public const String Question = "Question";

        /// <summary>响应。缓存的AI回复内容，Markdown格式</summary>
        public const String Response = "Response";

        /// <summary>推理响应。缓存的思考过程内容</summary>
        public const String ThinkingResponse = "ThinkingResponse";

        /// <summary>模型。生成缓存响应时使用的模型</summary>
        public const String ModelId = "ModelId";

        /// <summary>图标。Material Icon名称，如chat_bubble_outline</summary>
        public const String Icon = "Icon";

        /// <summary>颜色。图标CSS颜色类，如text-blue-500</summary>
        public const String Color = "Color";

        /// <summary>排序。越大越靠前</summary>
        public const String Sort = "Sort";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

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
