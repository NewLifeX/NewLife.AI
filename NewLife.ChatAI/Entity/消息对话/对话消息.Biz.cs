using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.Data;
using XCode;

namespace NewLife.ChatAI.Entity;

public partial class ChatMessage : Entity<ChatMessage>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static ChatMessage()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(ConversationId));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add(new IPInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TraceInterceptor>();

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

    /// <summary>已重载。</summary>
    public override String ToString() => $"[{Role}]{Content?[..Math.Min(64, Content.Length)]}";
    #endregion

    #region 扩展属性
    /// <summary>会话</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public Conversation? Conversation => Extends.Get(nameof(Conversation), k => Conversation.FindById(ConversationId));

    /// <summary>会话</summary>
    [Map(nameof(ConversationId), typeof(Conversation), "Id")]
    public String? ConversationTitle => Conversation?.Title;

    /// <summary>是否主消息（用户或助手）</summary>
    public Boolean IsMain => Role.EqualIgnoreCase("user", "assistant");
    #endregion

    #region 高级查询
    /// <summary>根据会话查找，按创建时间降序排列</summary>
    /// <param name="conversationId">会话</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> FindAllByConversationIdDesc(Int64 conversationId, Int32 count = -1)
    {
        if (conversationId < 0) return [];

        return FindAll(_.ConversationId == conversationId, _.Id.Desc(), null, 0, count);
    }

    /// <summary>根据会话查找，按创建时间升序排列</summary>
    /// <param name="conversationId">会话</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> FindAllByConversationIdOrdered(Int64 conversationId, Int32 count = -1)
    {
        if (conversationId < 0) return [];

        return FindAll(_.ConversationId == conversationId, _.Id.Asc(), null, 0, count);
    }

    /// <summary>根据会话查找指定消息之前的消息，按创建时间升序排列</summary>
    /// <param name="conversationId">会话</param>
    /// <param name="messageId">消息编号上限（不含）</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> FindAllBeforeId(Int64 conversationId, Int64 messageId)
    {
        if (conversationId < 0) return [];

        return FindAll(_.ConversationId == conversationId & _.Id < messageId, _.Id.Asc(), null, 0, 0);
    }

    /// <summary>根据会话查找指定消息之后的消息</summary>
    /// <param name="conversationId">会话</param>
    /// <param name="messageId">消息编号下限（不含）</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> FindAllAfterId(Int64 conversationId, Int64 messageId)
    {
        if (conversationId < 0) return [];

        return FindAll(_.ConversationId == conversationId & _.Id > messageId, null, null, 0, 0);
    }

    /// <summary>获取会话最后一条消息</summary>
    /// <param name="conversationId">会话</param>
    /// <returns>最后一条消息，不存在则返回null</returns>
    public static ChatMessage? FindLastByConversationId(Int64 conversationId)
    {
        if (conversationId < 0) return null;

        var list = FindAll(_.ConversationId == conversationId, _.Id.Desc(), null, 0, 1);
        return list.Count > 0 ? list[0] : null;
    }

    /// <summary>根据共享快照查找会话消息，按创建时间升序排列</summary>
    /// <param name="conversationId">会话</param>
    /// <param name="snapshotMessageId">快照截止消息编号，0表示不限制</param>
    /// <returns>实体列表</returns>
    public static IList<ChatMessage> FindByShareSnapshot(Int64 conversationId, Int64 snapshotMessageId)
    {
        if (conversationId < 0) return [];

        var exp = _.ConversationId == conversationId;
        if (snapshotMessageId > 0) exp &= _.Id <= snapshotMessageId;

        return FindAll(exp, _.Id.Asc(), null, 0, 0);
    }

    // Select Count(Id) as Id,Category From ChatMessage Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By Id Desc limit 20
    //static readonly FieldCache<ChatMessage> _CategoryCache = new(nameof(Category))
    //{
    //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    //};

    ///// <summary>获取类别列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    ///// <returns></returns>
    //public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();

    /// <summary>在指定会话集合中按关键词全文搜索消息内容，按消息编号降序分页</summary>
    /// <param name="convIds">会话编号集合</param>
    /// <param name="key">搜索关键词</param>
    /// <param name="page">分页参数</param>
    /// <returns>消息列表</returns>
    public static IList<ChatMessage> Search(Int64[] convIds, String key, PageParameter page)
    {
        if (convIds == null || convIds.Length == 0) return [];

        page.Sort = _.Id.Desc();

        var exp = new WhereExpression();
        exp &= _.ConversationId.In(convIds);
        if (!key.IsNullOrEmpty()) exp &= _.Content.Contains(key.Trim());

        return FindAll(exp, page);
    }

    /// <summary>根据会话编号集合批量查找消息</summary>
    /// <param name="convIds">会话编号集合</param>
    /// <returns>消息列表</returns>
    public static IList<ChatMessage> FindAllByConversationIds(Int64[] convIds)
    {
        if (convIds == null || convIds.Length == 0) return [];

        return FindAll(_.ConversationId.In(convIds));
    }
    #endregion

    #region 业务操作
    #endregion
}
