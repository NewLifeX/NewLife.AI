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

/// <summary>技能。可复用的AI行为指令，Markdown格式的结构化提示文本</summary>
[Serializable]
[DataObject]
[Description("技能。可复用的AI行为指令，Markdown格式的结构化提示文本")]
[BindIndex("IU_Skill_Code", true, "Code")]
[BindIndex("IX_Skill_Category", false, "Category")]
[BindTable("Skill", Description = "技能。可复用的AI行为指令，Markdown格式的结构化提示文本", ConnName = "ChatAI", DbType = DatabaseType.None)]
public partial class Skill : ISkill, IEntity<ISkill>
{
    #region 属性
    private Int32 _Id;
    /// <summary>编号</summary>
    [DisplayName("编号")]
    [Description("编号")]
    [DataObjectField(true, true, false, 0)]
    [BindColumn("Id", "编号", "")]
    public Int32 Id { get => _Id; set { if (OnPropertyChanging("Id", value)) { _Id = value; OnPropertyChanged("Id"); } } }

    private String? _Code;
    /// <summary>编码。英文标识，唯一，如coder、translator</summary>
    [DisplayName("编码")]
    [Description("编码。英文标识，唯一，如coder、translator")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Code", "编码。英文标识，唯一，如coder、translator", "")]
    public String? Code { get => _Code; set { if (OnPropertyChanging("Code", value)) { _Code = value; OnPropertyChanged("Code"); } } }

    private String? _Name;
    /// <summary>名称。技能展示名称，同时也是@引用的标识</summary>
    [DisplayName("名称")]
    [Description("名称。技能展示名称，同时也是@引用的标识")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Name", "名称。技能展示名称，同时也是@引用的标识", "", Master = true)]
    public String? Name { get => _Name; set { if (OnPropertyChanging("Name", value)) { _Name = value; OnPropertyChanged("Name"); } } }

    private String? _Icon;
    /// <summary>图标。Material Icon名称，如code、translate</summary>
    [DisplayName("图标")]
    [Description("图标。Material Icon名称，如code、translate")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Icon", "图标。Material Icon名称，如code、translate", "")]
    public String? Icon { get => _Icon; set { if (OnPropertyChanging("Icon", value)) { _Icon = value; OnPropertyChanged("Icon"); } } }

    private String? _Category;
    /// <summary>分类。通用/开发/创作/分析</summary>
    [DisplayName("分类")]
    [Description("分类。通用/开发/创作/分析")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("Category", "分类。通用/开发/创作/分析", "")]
    public String? Category { get => _Category; set { if (OnPropertyChanging("Category", value)) { _Category = value; OnPropertyChanged("Category"); } } }

    private String? _Description;
    /// <summary>描述。一句话说明该技能做什么</summary>
    [DisplayName("描述")]
    [Description("描述。一句话说明该技能做什么")]
    [DataObjectField(false, false, true, 500)]
    [BindColumn("Description", "描述。一句话说明该技能做什么", "")]
    public String? Description { get => _Description; set { if (OnPropertyChanging("Description", value)) { _Description = value; OnPropertyChanged("Description"); } } }

    private String? _Content;
    /// <summary>技能正文。Markdown格式，包含完整的行为指令、规则和示例</summary>
    [DisplayName("技能正文")]
    [Description("技能正文。Markdown格式，包含完整的行为指令、规则和示例")]
    [DataObjectField(false, false, true, -1)]
    [BindColumn("Content", "技能正文。Markdown格式，包含完整的行为指令、规则和示例", "", ItemType = "markdown", ShowIn = "Auto,-List,-Search")]
    public String? Content { get => _Content; set { if (OnPropertyChanging("Content", value)) { _Content = value; OnPropertyChanged("Content"); } } }

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

    private Boolean _IsSystem;
    /// <summary>系统。是否系统内置，内置技能不可删除</summary>
    [DisplayName("系统")]
    [Description("系统。是否系统内置，内置技能不可删除")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("IsSystem", "系统。是否系统内置，内置技能不可删除", "")]
    public Boolean IsSystem { get => _IsSystem; set { if (OnPropertyChanging("IsSystem", value)) { _IsSystem = value; OnPropertyChanged("IsSystem"); } } }

    private Int32 _Version;
    /// <summary>版本。每次编辑自增</summary>
    [DisplayName("版本")]
    [Description("版本。每次编辑自增")]
    [DataObjectField(false, false, false, 0)]
    [BindColumn("Version", "版本。每次编辑自增", "")]
    public Int32 Version { get => _Version; set { if (OnPropertyChanging("Version", value)) { _Version = value; OnPropertyChanged("Version"); } } }

    private String? _CreateUser;
    /// <summary>创建者</summary>
    [Category("扩展")]
    [DisplayName("创建者")]
    [Description("创建者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("CreateUser", "创建者", "")]
    public String? CreateUser { get => _CreateUser; set { if (OnPropertyChanging("CreateUser", value)) { _CreateUser = value; OnPropertyChanged("CreateUser"); } } }

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

    private String? _UpdateUser;
    /// <summary>更新者</summary>
    [Category("扩展")]
    [DisplayName("更新者")]
    [Description("更新者")]
    [DataObjectField(false, false, true, 50)]
    [BindColumn("UpdateUser", "更新者", "")]
    public String? UpdateUser { get => _UpdateUser; set { if (OnPropertyChanging("UpdateUser", value)) { _UpdateUser = value; OnPropertyChanged("UpdateUser"); } } }

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
    public void Copy(ISkill model)
    {
        Id = model.Id;
        Code = model.Code;
        Name = model.Name;
        Icon = model.Icon;
        Category = model.Category;
        Description = model.Description;
        Content = model.Content;
        Sort = model.Sort;
        Enable = model.Enable;
        IsSystem = model.IsSystem;
        Version = model.Version;
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
            "Code" => _Code,
            "Name" => _Name,
            "Icon" => _Icon,
            "Category" => _Category,
            "Description" => _Description,
            "Content" => _Content,
            "Sort" => _Sort,
            "Enable" => _Enable,
            "IsSystem" => _IsSystem,
            "Version" => _Version,
            "CreateUser" => _CreateUser,
            "CreateUserID" => _CreateUserID,
            "CreateIP" => _CreateIP,
            "CreateTime" => _CreateTime,
            "UpdateUser" => _UpdateUser,
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
                case "Id": _Id = value.ToInt(); break;
                case "Code": _Code = Convert.ToString(value); break;
                case "Name": _Name = Convert.ToString(value); break;
                case "Icon": _Icon = Convert.ToString(value); break;
                case "Category": _Category = Convert.ToString(value); break;
                case "Description": _Description = Convert.ToString(value); break;
                case "Content": _Content = Convert.ToString(value); break;
                case "Sort": _Sort = value.ToInt(); break;
                case "Enable": _Enable = value.ToBoolean(); break;
                case "IsSystem": _IsSystem = value.ToBoolean(); break;
                case "Version": _Version = value.ToInt(); break;
                case "CreateUser": _CreateUser = Convert.ToString(value); break;
                case "CreateUserID": _CreateUserID = value.ToInt(); break;
                case "CreateIP": _CreateIP = Convert.ToString(value); break;
                case "CreateTime": _CreateTime = value.ToDateTime(); break;
                case "UpdateUser": _UpdateUser = Convert.ToString(value); break;
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
    public static Skill? FindById(Int32 id)
    {
        if (id < 0) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据编码查找</summary>
    /// <param name="code">编码</param>
    /// <returns>实体对象</returns>
    public static Skill? FindByCode(String? code)
    {
        if (code == null) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Code.EqualIgnoreCase(code));

        return Find(_.Code == code);
    }

    /// <summary>根据分类查找</summary>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<Skill> FindAllByCategory(String? category)
    {
        if (category == null) return [];

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.FindAll(e => e.Category.EqualIgnoreCase(category));

        return FindAll(_.Category == category);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="code">编码。英文标识，唯一，如coder、translator</param>
    /// <param name="category">分类。通用/开发/创作/分析</param>
    /// <param name="isSystem">系统。是否系统内置，内置技能不可删除</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<Skill> Search(String? code, String? category, Boolean? isSystem, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (isSystem != null) exp &= _.IsSystem == isSystem;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 字段名
    /// <summary>取得技能字段信息的快捷方式</summary>
    public partial class _
    {
        /// <summary>编号</summary>
        public static readonly Field Id = FindByName("Id");

        /// <summary>编码。英文标识，唯一，如coder、translator</summary>
        public static readonly Field Code = FindByName("Code");

        /// <summary>名称。技能展示名称，同时也是@引用的标识</summary>
        public static readonly Field Name = FindByName("Name");

        /// <summary>图标。Material Icon名称，如code、translate</summary>
        public static readonly Field Icon = FindByName("Icon");

        /// <summary>分类。通用/开发/创作/分析</summary>
        public static readonly Field Category = FindByName("Category");

        /// <summary>描述。一句话说明该技能做什么</summary>
        public static readonly Field Description = FindByName("Description");

        /// <summary>技能正文。Markdown格式，包含完整的行为指令、规则和示例</summary>
        public static readonly Field Content = FindByName("Content");

        /// <summary>排序。越大越靠前</summary>
        public static readonly Field Sort = FindByName("Sort");

        /// <summary>启用</summary>
        public static readonly Field Enable = FindByName("Enable");

        /// <summary>系统。是否系统内置，内置技能不可删除</summary>
        public static readonly Field IsSystem = FindByName("IsSystem");

        /// <summary>版本。每次编辑自增</summary>
        public static readonly Field Version = FindByName("Version");

        /// <summary>创建者</summary>
        public static readonly Field CreateUser = FindByName("CreateUser");

        /// <summary>创建用户</summary>
        public static readonly Field CreateUserID = FindByName("CreateUserID");

        /// <summary>创建地址</summary>
        public static readonly Field CreateIP = FindByName("CreateIP");

        /// <summary>创建时间</summary>
        public static readonly Field CreateTime = FindByName("CreateTime");

        /// <summary>更新者</summary>
        public static readonly Field UpdateUser = FindByName("UpdateUser");

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

    /// <summary>取得技能字段名称的快捷方式</summary>
    public partial class __
    {
        /// <summary>编号</summary>
        public const String Id = "Id";

        /// <summary>编码。英文标识，唯一，如coder、translator</summary>
        public const String Code = "Code";

        /// <summary>名称。技能展示名称，同时也是@引用的标识</summary>
        public const String Name = "Name";

        /// <summary>图标。Material Icon名称，如code、translate</summary>
        public const String Icon = "Icon";

        /// <summary>分类。通用/开发/创作/分析</summary>
        public const String Category = "Category";

        /// <summary>描述。一句话说明该技能做什么</summary>
        public const String Description = "Description";

        /// <summary>技能正文。Markdown格式，包含完整的行为指令、规则和示例</summary>
        public const String Content = "Content";

        /// <summary>排序。越大越靠前</summary>
        public const String Sort = "Sort";

        /// <summary>启用</summary>
        public const String Enable = "Enable";

        /// <summary>系统。是否系统内置，内置技能不可删除</summary>
        public const String IsSystem = "IsSystem";

        /// <summary>版本。每次编辑自增</summary>
        public const String Version = "Version";

        /// <summary>创建者</summary>
        public const String CreateUser = "CreateUser";

        /// <summary>创建用户</summary>
        public const String CreateUserID = "CreateUserID";

        /// <summary>创建地址</summary>
        public const String CreateIP = "CreateIP";

        /// <summary>创建时间</summary>
        public const String CreateTime = "CreateTime";

        /// <summary>更新者</summary>
        public const String UpdateUser = "UpdateUser";

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
