using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Cache;

namespace NewLife.ChatAI.Entity;

public partial class UsageRecord : Entity<UsageRecord>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static UsageRecord()
    {
        //Meta.Table.DataTable.InsertOnly = true;

        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(UserId));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add(new IPInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TraceInterceptor>();
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
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;

        return true;
    }
    #endregion

    #region 扩展属性
    ///// <summary>应用密钥</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    //public AppKey AppKey => Extends.Get(nameof(AppKey), k => AppKey.FindById(AppKeyId));

    ///// <summary>应用密钥</summary>
    //[Map(nameof(AppKeyId), typeof(AppKey), "Id")]
    //public String AppKeyName => AppKey?.Name;

    ///// <summary>会话</summary>
    //[XmlIgnore, IgnoreDataMember, ScriptIgnore]
    //public Conversation Conversation => Extends.Get(nameof(Conversation), k => Conversation.FindById(ConversationId));

    ///// <summary>会话</summary>
    //[Map(nameof(ConversationId), typeof(Conversation), "Id")]
    //public String ConversationTitle => Conversation?.Title;
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,ModelId From UsageRecord Where CreateTime>'2020-01-24 00:00:00' Group By ModelId Order By Id Desc limit 20
    static readonly FieldCache<UsageRecord> _ModelIdCache = new(nameof(ModelId))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取模型列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetModelIdList() => _ModelIdCache.FindAllName();

    /// <summary>根据会话编号集合批量查找用量记录</summary>
    /// <param name="convIds">会话编号集合</param>
    /// <returns>用量记录列表</returns>
    public static IList<UsageRecord> FindAllByConversationIds(Int64[] convIds)
    {
        if (convIds == null || convIds.Length == 0) return [];

        return FindAll(_.ConversationId.In(convIds));
    }
    #endregion

    #region 业务操作
    #endregion
}
