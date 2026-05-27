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
            new { Title = "写C#单例模式", Question = "请帮我用C#实现一个线程安全的单例模式，要求支持懒加载并分析适用场景", Icon = "code", Color = "text-blue-500" },
            new { Title = "写一首春天的诗", Question = "请帮我写一首关于春天的诗，表达春意盎然的美好，风格清新自然", Icon = "brush", Color = "text-pink-500" },
            new { Title = "解释量子计算", Question = "请通俗地解释量子计算的基本原理，以及它与传统计算机的本质区别", Icon = "science", Color = "text-purple-500" },
            new { Title = "推荐几本好书", Question = "请推荐几本值得精读的经典好书，涵盖思维提升和人文社科方面，并说明各自的理由", Icon = "menu_book", Color = "text-orange-500" },
            new { Title = "制定健身计划", Question = "请帮我制定一份适合上班族的周健身计划，包含有氧和力量训练，每次控制在30分钟内", Icon = "fitness_center", Color = "text-red-500" },
            new { Title = "查询我这里天气", Question = "请查询我当前所在位置的天气情况，并给出今日出行建议", Icon = "cloud", Color = "text-cyan-500" },
            new { Title = "我在哪里", Question = "请帮我查询我的当前位置，我在哪个城市？附近有哪些值得去的地方？", Icon = "location_on", Color = "text-teal-500" },
            new { Title = "写一封请假邮件", Question = "请帮我写一封请病假的邮件，收件人是部门主管，语气正式得体，简明扼要", Icon = "edit_note", Color = "text-green-500" },
            new { Title = "分析投资理财策略", Question = "对于普通投资者，请分析当前经济环境下有哪些稳健的理财和投资策略，如何做好风险控制", Icon = "trending_up", Color = "text-yellow-500" },
            new { Title = "Mermaid画注册流程图", Question = "请用 Mermaid flowchart TD 格式绘制完整的用户注册与登录流程图：输入表单→格式校验→发送邮箱验证码→验证通过→注册成功；同时包含忘记密码→重置密码路径；使用判断菱形区分成功与失败分支", Icon = "schema", Color = "text-orange-500" },
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
        => Meta.Cache.FindAll(q => q.Enable && q.Question == question && q.MessageId > 0 && q.UpdateTime.Date == DateTime.Today).FirstOrDefault();

    /// <summary>从实体缓存中查找启用且匹配指定问题的推荐问题。用于回写缓存时定位目标记录</summary>
    /// <param name="question">问题内容</param>
    /// <returns>匹配的推荐问题，未命中返回 null</returns>
    public static SuggestedQuestion? FindCachedByQuestion(String question)
        => Meta.Cache.FindAll(q => q.Enable && q.Question == question).FirstOrDefault();
    #endregion

    #region 业务操作
    #endregion
}