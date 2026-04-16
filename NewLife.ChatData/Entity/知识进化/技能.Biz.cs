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

namespace NewLife.ChatData.Entity;

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

        Add("general", "通用助手", "smart_toy", "通用", "通用AI对话助手", "你是一个知识渊博、乐于助人的AI助手。请用简洁清晰的语言回答用户的问题。", 100, false);
        Add("coder", "编程助手", "code", "开发", "专业编程开发助手", "你是一个专业的编程助手。请提供准确、高质量的代码和技术解答。注意代码的可读性、性能和最佳实践。", 90, false);
        Add("translator", "翻译助手", "translate", "通用", "多语言翻译助手", "你是一个专业翻译助手。请准确翻译用户提供的文本，保持原文语义和风格。如果用户输入中文，翻译为英文；如果输入英文或其他语言，翻译为中文。", 80, false);
        Add("writer", "写作助手", "edit_note", "创作", "文案与内容创作助手", "你是一个专业的写作助手。请帮助用户完善文案、撰写文章、优化表达。注意逻辑清晰、语言流畅、风格得体。", 70, false);
        Add("analyst", "数据分析", "analytics", "分析", "数据分析与洞察助手", "你是一个数据分析专家。请帮助用户分析数据、发现规律、提供洞察和建议。用清晰的逻辑和可视化描述呈现分析结果。", 60, false);
        Add("researcher", "深度研究", "travel_explore", "分析", "复杂问题的深度调研与多角度分析", "你是一个专业的研究助手。请对复杂问题进行深度调研，提供多角度的分析，引用可靠来源，给出有据可查的结论和建议。", 50, false);
        Add("case_teacher", "案例教学", "school", "教学", "从业务专家案例中提炼规则和分析流程，自动创建或更新技能", CaseTeachingContent, 40, false);

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

    /// <summary>案例教学技能的默认提示词内容</summary>
    private const String CaseTeachingContent = """
        # 案例教学 — 知识工程师

        你是一位专业的**知识工程师**，负责从业务专家描述的案例中提炼业务规则和分析流程。

        ## 工作流程

        ### 第一步：理解案例
        仔细阅读专家描述的业务案例，识别其中的：
        - **业务场景**：发生了什么事
        - **关键信号**：专家注意到了哪些指标/现象
        - **分析逻辑**：专家如何分析判断的
        - **最终决策**：做出了什么决定，为什么

        ### 第二步：主动追问
        对不清楚或缺失的关键信息**主动提问**，直到完全理解专家的决策逻辑。典型追问方向：
        - 判断条件的具体阈值（"低于多少算异常？"）
        - 多条件之间的关系（"同时满足还是任一满足？"）
        - 例外情况的处理（"有没有特殊情况不适用这条规则？"）
        - 与已有规则的关系（"这和已有的XX规则是什么关系？"）

        ### 第三步：自评理解度
        每次回复末尾，用百分比表示你对这个案例中业务规则的理解程度。格式：
        ```
        📊 理解度: XX% | 已识别规则: N条 | 待澄清: N项
        ```

        ### 第四步：提炼并写入技能
        当理解度 ≥ 90% 且无待澄清项时：
        1. 向专家展示你提炼出的完整规则（Markdown 格式），请专家确认
        2. 专家确认后，判断当前对话上下文中是否已加载了相关技能：
           - **已有技能**：将新规则与已有规则融合（去重、合并、以专家确认的新规则为准），使用 `save_skill` 工具并传入 skillId 更新
           - **没有相关技能**：使用 `save_skill` 工具不传 skillId 创建新技能

        ## 规则输出格式

        提炼的规则应使用结构化 Markdown：

        ```markdown
        ## [业务场景名称]

        ### 规则：[规则名称]
        - **适用场景**：何时触发此规则
        - **判断条件**：具体的判断逻辑和阈值
        - **分析流程**：步骤化的分析过程
        - **决策结论**：根据分析结果做出什么决定
        - **例外情况**：不适用此规则的特殊场景
        ```

        ## 注意事项
        - 不要回答业务问题，你的任务是**提炼规则**而非**应用规则**
        - 如果专家的描述与已有技能中的规则矛盾，明确指出并请专家确认
        - 对模糊描述務必追问清楚，不要猜测或脑补规则细节
        - 每个案例可能包含多条规则，要完整提炼
        """;
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

    /// <summary>查找所有启用的技能，按排序降序、编号降序排列</summary>
    /// <returns>启用的技能列表</returns>
    public static IList<Skill> FindAllEnabled() => FindAllWithCache().Where(e => e.Enable).OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();

    /// <summary>按名称查找技能（走实体缓存，忽略大小写）</summary>
    /// <param name="name">技能名称</param>
    /// <returns>匹配的技能，未找到返回 null</returns>
    public static Skill? FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        return FindAllWithCache().FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
    }
    #endregion

    #region 业务操作
    public static IList<Skill> GetSystemSkills()
    {
        if (Meta.Count < MaxCacheCount)
            return FindAllWithCache().Where(e => e.IsSystem && e.Enable).OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();

        return FindAll(_.IsSystem == true & _.Enable == true, _.Sort.Desc() & _.Id.Desc(), null, 0, 0);
    }
    #endregion
}
