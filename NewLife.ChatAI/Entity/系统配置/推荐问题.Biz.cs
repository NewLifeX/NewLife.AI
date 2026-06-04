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

public partial class SuggestedQuestion : Entity<SuggestedQuestion>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    // 预设图标与颜色调色板，新建推荐问题时若未填写则自动选取
    private static readonly String[] _autoIcons = ["chat", "code", "science", "menu_book", "brush", "fitness_center", "cloud", "location_on", "edit_note", "trending_up", "lightbulb", "psychology", "travel_explore", "calculate", "music_note"];
    private static readonly String[] _autoColors = ["text-blue-500", "text-pink-500", "text-purple-500", "text-orange-500", "text-red-500", "text-cyan-500", "text-teal-500", "text-green-500", "text-yellow-500", "text-indigo-500"];

    static SuggestedQuestion()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(ModelId));

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

        // 新建时若未填写图标或颜色，根据问题文本自动选取，确保欢迎页展示效果一致
        if (method == DataMethod.Insert && (Icon.IsNullOrEmpty() || Color.IsNullOrEmpty()))
        {
            var idx = Math.Abs((Question ?? Title ?? "").GetHashCode());
            if (Icon.IsNullOrEmpty()) Icon = _autoIcons[idx % _autoIcons.Length];
            if (Color.IsNullOrEmpty()) Color = _autoColors[(idx + 5) % _autoColors.Length];
        }

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

        return true;
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        if (XTrace.Debug) XTrace.WriteLine("开始初始化SuggestedQuestion[推荐问题]数据……");

        // 原来是否已有数据，决定新增项是否默认启用
        var hasExisting = Meta.Session.Count > 0;

        // 以标题为去重键，已有标题的跳过，仅插入新增项
        var existingTitles = hasExisting
            ? Meta.Cache.FindAll(e => true).Select(e => e.Title).ToHashSet()
            : new HashSet<String?>();

        var items = new[]
        {
            new { Title = "C#线程安全单例模式实现", Question = "请帮我用C#实现一个线程安全的单例模式，要求支持懒加载并分析适用场景", Icon = "code", Color = "text-blue-500" },
            new { Title = "今日天气与春意诗词", Question = "今天天气怎么样？请结合当前天气和春天时令，为我写一首描绘春色的诗词", Icon = "cloud", Color = "text-cyan-500" },
            new { Title = "通俗解读量子计算原理", Question = "请通俗地解释量子计算的基本原理，以及它与传统计算机的本质区别", Icon = "science", Color = "text-purple-500" },
            new { Title = "了解我喜好的个性化荐书", Question = "请先了解一下我的个人信息，然后结合我的阅读偏好，推荐几本值得精读的经典好书", Icon = "menu_book", Color = "text-orange-500" },
            new { Title = "制定上班族周健身计划", Question = "请帮我制定一份适合上班族的周健身计划，包含有氧和力量训练，每次控制在30分钟内", Icon = "fitness_center", Color = "text-red-500" },
            new { Title = "搜索AI领域最新进展", Question = "帮我搜索最近大语言模型领域有什么重要进展，整理成摘要", Icon = "search", Color = "text-teal-500" },
            new { Title = "查询近期节假日安排", Question = "帮我查一下今天是工作日还是假期，近期有哪些连休安排", Icon = "event", Color = "text-green-500" },
            new { Title = "Mermaid绘制注册流程图", Question = "请用 Mermaid 流程图格式绘制完整的用户注册与登录流程，包含邮箱验证和找回密码路径，使用判断菱形区分成功与失败分支", Icon = "schema", Color = "text-indigo-500" },
            new { Title = "写一封请假邮件", Question = "请帮我写一封请病假的邮件，收件人是部门主管，语气正式得体，简明扼要", Icon = "edit_note", Color = "text-yellow-500" },
        };

        var added = 0;
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (existingTitles.Contains(item.Title)) continue;

            var entity = new SuggestedQuestion
            {
                Title = item.Title,
                Question = item.Question,
                Icon = item.Icon,
                Color = item.Color,
                Enable = !hasExisting,
            };
            entity.Insert();
            added++;
        }

        if (XTrace.Debug) XTrace.WriteLine($"完成初始化SuggestedQuestion[推荐问题]数据，新增 {added} 条！");
    }

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    /// <summary>会话</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Conversation? Conversation => Extends.Get(nameof(Conversation), k => Conversation.FindById(ConversationId));

    /// <summary>会话</summary>
    [Map(nameof(ConversationId), typeof(Conversation), "Id")]
    public String? ConversationTitle => Conversation?.Title;
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Category From SuggestedQuestion Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<SuggestedQuestion> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();

    /// <summary>从实体缓存中获取所有启用的推荐问题</summary>
    /// <returns>启用的推荐问题列表</returns>
    public static IList<SuggestedQuestion> FindAllCachedEnabled()
        => Meta.Cache.FindAll(q => q.Enable);

    /// <summary>从实体缓存中查找今日匹配指定问题且已关联助手消息的推荐问题。用于命中缓存时直接返回</summary>
    /// <param name="question">问题内容</param>
    /// <returns>匹配的推荐问题，未命中返回 null</returns>
    public static SuggestedQuestion? FindCachedTodayByQuestion(String question)
    {
        var now = DateTime.Now;
        return Meta.Cache.FindAll(q => q.Enable && q.Question == question && q.MessageId > 0 && IsCacheValid(q, now)).FirstOrDefault();
    }

    /// <summary>从实体缓存中查找启用且匹配指定问题的推荐问题。用于回写缓存时定位目标记录</summary>
    /// <param name="question">问题内容</param>
    /// <returns>匹配的推荐问题，未命中返回 null</returns>
    public static SuggestedQuestion? FindCachedByQuestion(String question)
        => Meta.Cache.FindAll(q => q.Enable && q.Question == question).FirstOrDefault();

    /// <summary>判断推荐问题的缓存是否仍然有效。CacheDuration=-1永不缓存；0=当天有效；正数=从更新时间起N分钟内有效</summary>
    /// <param name="q">推荐问题实体</param>
    /// <param name="now">当前时间</param>
    /// <returns>缓存是否有效</returns>
    private static Boolean IsCacheValid(SuggestedQuestion q, DateTime now) => q.CacheDuration switch
    {
        -1 => false,
        0 => q.UpdateTime.Date == now.Date,
        _ => now < q.UpdateTime.AddMinutes(q.CacheDuration),
    };
    #endregion

    #region 业务操作
    // 72小时半衰期对应的EMA衰减系数 λ = ln(2) / 72
    private const Double _heatDecayLambda = 0.009627; // ln(2)/72 ≈ 0.009627

    /// <summary>记录一次命中，更新热度分数（EMA）和命中计数。使用静态SQL Update绕过TimeInterceptor，不刷新UpdateTime</summary>
    public void RecordHit()
    {
        var now = DateTime.Now;
        Double newScore;
        if (LastHitTime == default)
        {
            newScore = 1.0;
        }
        else
        {
            var elapsedHours = (now - LastHitTime).TotalHours;
            newScore = HeatScore * Math.Exp(-_heatDecayLambda * elapsedHours) + 1.0;
        }

        var newCount = HitCount + 1;

        // 直接走SQL，绕过TimeInterceptor，确保UpdateTime（缓存内容写入时间）不被刷新
        Update(_.HeatScore == newScore & _.HitCount == newCount & _.LastHitTime == now, _.Id == Id);

        // 同步更新内存中的实体缓存字段值
        HeatScore = newScore;
        HitCount = newCount;
        LastHitTime = now;
    }
    #endregion
}