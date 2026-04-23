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
        if (Meta.Session.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("开始初始化SuggestedQuestion[推荐问题]数据……");

        var items = new[]
        {
            new { Title = "写C#代码", Question = "请帮我用C#实现一个线程安全的单例模式，要求支持懒加载并分析其适用场景", Icon = "code", Color = "text-blue-500" },
            new { Title = "解释技术原理", Question = "请详细解释HTTP/2相比HTTP/1.1的主要改进，包括多路复用、头部压缩等核心特性", Icon = "science", Color = "text-purple-500" },
            new { Title = "数据库优化", Question = "帮我分析常见的SQL慢查询场景，并给出索引优化和查询重写的最佳实践", Icon = "storage", Color = "text-orange-500" },
            new { Title = "讲解设计模式", Question = "请用C#示例讲解单例、工厂、观察者和策略这四种常用设计模式，说明各自的适用场景", Icon = "architecture", Color = "text-teal-500" },
            new { Title = "制定学习路线", Question = "我想系统学习全栈开发，请帮我制定一份详细的学习计划，包括前后端和数据库的学习路径", Icon = "school", Color = "text-green-500" },
            new { Title = "代码审查", Question = "请帮我审查以下代码的潜在问题，包括性能、安全性和可维护性方面的改进建议", Icon = "rate_review", Color = "text-amber-500" },
            new { Title = "分析错误日志", Question = "帮我分析以下程序错误日志，找出根本原因并给出修复方案和预防措施", Icon = "bug_report", Color = "text-red-500" },
            new { Title = "系统架构建议", Question = "我需要设计一个支持高并发的消息推送系统，请从架构角度分析技术选型和关键设计决策", Icon = "hub", Color = "text-yellow-500" },
            new { Title = "技术概念对比", Question = "请对比解释微服务架构、SOA和单体架构的优缺点，以及各自适用的业务场景", Icon = "compare_arrows", Color = "text-pink-500" },
        };

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var entity = new SuggestedQuestion
            {
                Title = item.Title,
                Question = item.Question,
                Icon = item.Icon,
                Color = item.Color,
                Sort = items.Length - i,
                Enable = true,
            };
            entity.Insert();
        }

        if (XTrace.Debug) XTrace.WriteLine("完成初始化SuggestedQuestion[推荐问题]数据！");
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

    /// <summary>从实体缓存中查找今日匹配指定问题且已缓存回答的推荐问题。用于命中缓存时直接返回</summary>
    /// <param name="question">问题内容</param>
    /// <returns>匹配的推荐问题，未命中返回 null</returns>
    public static SuggestedQuestion? FindCachedTodayByQuestion(String question)
        => Meta.Cache.FindAll(q => q.Enable && q.Question == question && !q.Response.IsNullOrEmpty() && q.UpdateTime.Date == DateTime.Today).FirstOrDefault();

    /// <summary>从实体缓存中查找启用且匹配指定问题的推荐问题。用于回写缓存时定位目标记录</summary>
    /// <param name="question">问题内容</param>
    /// <returns>匹配的推荐问题，未命中返回 null</returns>
    public static SuggestedQuestion? FindCachedByQuestion(String question)
        => Meta.Cache.FindAll(q => q.Enable && q.Question == question).FirstOrDefault();
    #endregion

    #region 业务操作
    #endregion
}
