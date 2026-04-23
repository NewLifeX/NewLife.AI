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

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // 改为按 Code 逐个 upsert：表非空也会补齐缺失的内置技能；
        // 已存在的内置技能仅在用户未自定义修改时补齐缺失字段，避免覆盖业务专家定制。

        if (XTrace.Debug) XTrace.WriteLine("开始初始化Skill[技能]数据……");

        //Add("general", "通用助手", "smart_toy", "通用", "通用AI对话助手", "你是一个知识渊博、乐于助人的AI助手。请用简洁清晰的语言回答用户的问题。", 100);
        //Add("coder", "编程助手", "code", "开发", "专业编程开发助手", "你是一个专业的编程助手。请提供准确、高质量的代码和技术解答。注意代码的可读性、性能和最佳实践。", 90);
        Add("translator", "翻译助手", "translate", "通用", "多语言翻译助手", "你是一个专业翻译助手。请准确翻译用户提供的文本，保持原文语义和风格。如果用户输入中文，翻译为英文；如果输入英文或其他语言，翻译为中文。", 80);
        //Add("writer", "写作助手", "edit_note", "创作", "文案与内容创作助手", "你是一个专业的写作助手。请帮助用户完善文案、撰写文章、优化表达。注意逻辑清晰、语言流畅、风格得体。", 70);
        //Add("analyst", "数据分析", "analytics", "分析", "数据分析与洞察助手", "你是一个数据分析专家。请帮助用户分析数据、发现规律、提供洞察和建议。用清晰的逻辑和可视化描述呈现分析结果。", 60);
        //Add("researcher", "深度研究", "travel_explore", "分析", "复杂问题的深度调研与多角度分析", "你是一个专业的研究助手。请对复杂问题进行深度调研，提供多角度的分析，引用可靠来源，给出有据可查的结论和建议。", 50);
        Add("case_teacher", "案例教学", "school", "教学", "从业务专家案例中提炼规则和分析流程，自动创建或更新技能", CaseTeachingContent, 40);
        Add("business_rule_template", "业务规则模板", "rule", "业务规则", "业务专家编写规则式技能的起点模板，每行一条if-then规则，自带强约束SystemPrompt", BusinessRuleTemplateContent, 30);

        if (XTrace.Debug) XTrace.WriteLine("完成初始化Skill[技能]数据！");
    }

    /// <summary>新增或补齐内置技能（按 Code 幂等 upsert）。
    /// 不存在时插入；存在且为系统内置（IsSystem=true）时仅补齐空字段，不覆盖用户已修改的值。</summary>
    private static void Add(String code, String name, String icon, String category, String description, String content, Int32 sort)
    {
        var entity = FindByCode(code);
        if (entity == null)
        {
            entity = new Skill
            {
                Code = code,
                Name = name,
                Icon = icon,
                Category = category,
                Description = description,
                Content = content,
                Sort = sort,
                Enable = true,
                IsSystem = true,
            };
            entity.Insert();
            return;
        }

        // 已存在：只补齐空字段，不覆盖用户的定制
        var dirty = false;
        if (entity.Name.IsNullOrEmpty()) { entity.Name = name; dirty = true; }
        if (entity.Icon.IsNullOrEmpty()) { entity.Icon = icon; dirty = true; }
        if (entity.Category.IsNullOrEmpty()) { entity.Category = category; dirty = true; }
        if (entity.Description.IsNullOrEmpty()) { entity.Description = description; dirty = true; }
        if (entity.Content.IsNullOrEmpty()) { entity.Content = content; dirty = true; }
        if (!entity.IsSystem) { entity.IsSystem = true; dirty = true; }

        if (dirty) entity.Update();
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

    /// <summary>业务规则模板技能内容（强约束，专为非工程业务专家设计）</summary>
    private const String BusinessRuleTemplateContent = """
        # 业务规则执行助手

        你是一个**严格按规则办事**的业务执行助手。
        你的任务不是发挥创造力，而是**忠实地按下面的规则手册做出判断和回答**。

        ## 强制约束（最高优先级）
        1. **只使用本技能中明确写出的规则**进行判断，不要根据自己的常识猜测
        2. 当用户问题**不命中任何规则**时，必须明确回答："此场景未在规则手册中定义，请联系业务专家补充规则。"
        3. 当多条规则**冲突**时，按 Sort 数字大的优先；若仍无法判定，列出所有冲突规则并请用户确认
        4. 引用规则时**必须标注规则编号或名称**，例如：「依据 R-003 规则，……」
        5. 不要回答与本规则手册无关的问题（即使用户追问），礼貌引导回业务话题

        ## 规则手册（请业务专家在此处编写）

        > **写规则的格式建议**（每条规则独立成段）：
        >
        > **R-001 规则名称**
        > - **触发条件**：什么场景下使用本规则（关键词、用户意图、上下文特征）
        > - **判断逻辑**：if 条件 then 结论 的明确表达
        > - **输出模板**：给用户的标准回复模板（可使用 {变量} 占位符）
        > - **示例**：1-2 个典型问答示例
        > - **例外**：哪些场景不适用本规则

        ### R-001 [示例：在此填写第一条业务规则]
        - **触发条件**：（例如：用户咨询订单退款）
        - **判断逻辑**：（例如：if 订单状态=已发货 and 距下单 < 7天 then 可全额退款；else 只退商品款）
        - **输出模板**：（例如：「您的订单符合 {退款类型}，请提交退款申请。」）
        - **示例**：（写 1 个真实问答）
        - **例外**：（例如：定制商品不适用）

        ### R-002 [继续添加更多规则……]

        ## 使用建议
        - 规则越细化、条件越明确，AI 表现越稳定
        - 用真实业务术语，不要写抽象描述
        - 定期由业务专家维护更新规则手册
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
    /// <summary>按编码查找系统内置技能（UserId=0, ProjectId=0）。向后兼容方法，业务层优先级查找请使用 SkillService</summary>
    /// <param name="code">技能编码</param>
    /// <returns></returns>
    public static Skill? FindByCode(String code) => Find(_.Code == code);

    public static IList<Skill> GetSystemSkills()
    {
        if (Meta.Count < MaxCacheCount)
            return FindAllWithCache().Where(e => e.IsSystem && e.Enable).OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();

        return FindAll(_.IsSystem == true & _.Enable == true, _.Sort.Desc() & _.Id.Desc(), null, 0, 0);
    }
    #endregion
}
