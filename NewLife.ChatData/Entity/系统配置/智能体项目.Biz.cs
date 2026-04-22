using NewLife.Common;
using XCode;

namespace NewLife.ChatData.Entity;

public partial class AgentProject : Entity<AgentProject>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static AgentProject()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(OwnerId));

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

        if (Code.IsNullOrEmpty() && !Name.IsNullOrEmpty()) Code = PinYin.Get(Name);

        return true;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Category From AgentProject Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<AgentProject> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
    #endregion

    #region 业务操作
    ///// <summary>刷新文档数、文章数和总Token数</summary>
    //public void RefreshCounts()
    //{
    //    DocumentCount = (Int32)KnowledgeDocument.FindCount(KnowledgeDocument._.ProjectId == Id);
    //    ArticleCount = (Int32)KnowledgeArticle.FindCount(KnowledgeArticle._.ProjectId == Id);
    //    TotalTokens = (Int64)KnowledgeArticle.FindAll(KnowledgeArticle._.ProjectId == Id).Sum(e => (Int64)e.Tokens);
    //    Update();
    //}
    #endregion
}
