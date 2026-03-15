using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace NewLife.ChatAI.Entity;

public partial class Skill : Entity<Skill>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static Skill()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(Sort));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add(new IPInterceptor { AllowEmpty = false });

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 处理当前已登录用户信息，可以由UserInterceptor拦截器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (method == DataMethod.Insert && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
            if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
        }*/
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        // 检查唯一索引
        // CheckExist(method == DataMethod.Insert, nameof(Code));

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化Skill[技能]数据……");

    //    var entity = new Skill();
    //    entity.Code = "abc";
    //    entity.Name = "abc";
    //    entity.Icon = "abc";
    //    entity.Category = "abc";
    //    entity.Description = "abc";
    //    entity.Content = "abc";
    //    entity.Sort = 0;
    //    entity.Enable = true;
    //    entity.IsSystem = true;
    //    entity.Version = 0;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化Skill[技能]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        if (Meta.Session.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("开始初始化Skill[技能]数据……");

        Add("general", "通用助手", "smart_toy", "通用", "通用AI对话助手", "你是一个知识渊博、乐于助人的AI助手。请用简洁清晰的语言回答用户的问题。", 100, true);
        Add("coder", "编程助手", "code", "开发", "专业编程开发助手", "你是一个专业的编程助手。请提供准确、高质量的代码和技术解答。注意代码的可读性、性能和最佳实践。", 90, true);
        Add("translator", "翻译助手", "translate", "通用", "多语言翻译助手", "你是一个专业翻译助手。请准确翻译用户提供的文本，保持原文语义和风格。如果用户输入中文，翻译为英文；如果输入英文或其他语言，翻译为中文。", 80, true);
        Add("writer", "写作助手", "edit_note", "创作", "文案与内容创作助手", "你是一个专业的写作助手。请帮助用户完善文案、撰写文章、优化表达。注意逻辑清晰、语言流畅、风格得体。", 70, true);
        Add("analyst", "数据分析", "analytics", "分析", "数据分析与洞察助手", "你是一个数据分析专家。请帮助用户分析数据、发现规律、提供洞察和建议。用清晰的逻辑和可视化描述呈现分析结果。", 60, true);
        Add("researcher", "深度研究", "travel_explore", "分析", "复杂问题的深度调研与多角度分析", "你是一个专业的研究助手。请对复杂问题进行深度调研，提供多角度的分析，引用可靠来源，给出有据可查的结论和建议。", 50, true);

        if (XTrace.Debug) XTrace.WriteLine("完成初始化Skill[技能]数据！");
    }

    private static void Add(String code, String name, String icon, String category, String description, String content, Int32 sort, Boolean isSystem)
    {
        var entity = new Skill
        {
            Code = code,
            Name = name,
            Icon = icon,
            Category = category,
            Description = description,
            Content = content,
            Sort = sort,
            Enable = true,
            IsSystem = isSystem,
        };
        entity.Insert();
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Category From Skill Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    static readonly FieldCache<Skill> _CategoryCache = new(nameof(Category))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取分类列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    public SkillModel ToModel()
    {
        var model = new SkillModel();
        model.Copy(this);

        return model;
    }

    #endregion
}
