using System.ComponentModel;
using NewLife.Data;
using NewLife.Log;
using XCode;

namespace NewLife.ChatData.Entity;

public partial class ChatPreset : Entity<ChatPreset>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static ChatPreset()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(UserId));

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
        // CheckExist(method == DataMethod.Insert, nameof(UserId), nameof(Name));

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        if (Meta.Session.Count > 0) return;

        if (XTrace.Debug) XTrace.WriteLine("开始初始化ChatPreset[对话预设]数据……");

        var entity = new ChatPreset
        {
            UserId = 0,
            Name = "通用助手",
            ModelId = 0,
            ModelName = "qwen3.5-plus",
            SystemPrompt = "你是一个有帮助的AI助手，能够回答各种问题并提供实用的建议。",
            Prompt = "请问有什么我可以帮助你的？",
            ThinkingMode = NewLife.AI.Models.ThinkingMode.Auto,
            IsDefault = false,
            Sort = 0,
            Enable = true,
        };
        entity.Insert();

        if (XTrace.Debug) XTrace.WriteLine("完成初始化ChatPreset[对话预设]数据！");
    }

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    /// <summary>查询用户可用的预设列表（含系统级预设），按排序降序、编号降序排列</summary>
    /// <param name="userId">用户编号</param>
    /// <returns>预设列表</returns>
    public static IList<ChatPreset> FindAllAvailable(Int32 userId)
        => FindAll((_.UserId == userId | _.UserId == 0) & _.Enable == true, _.Sort.Desc() + "," + _.Id.Desc(), null, 0, 0);

    /// <summary>高级查询</summary>
    /// <param name="userId">用户。所属用户，0表示系统级预设</param>
    /// <param name="modelId">模型。关联的模型配置</param>
    /// <param name="sort">排序。越大越靠前</param>
    /// <param name="enable">启用</param>
    /// <param name="isDefault">默认预设。是否为用户默认选中的预设</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ChatPreset> Search(Int32 userId, Int32 modelId, Int32 sort, Boolean? enable, Boolean? isDefault, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (userId >= 0) exp &= _.UserId == userId;
        if (modelId >= 0) exp &= _.ModelId == modelId;
        if (sort >= 0) exp &= _.Sort == sort;
        if (enable != null) exp &= _.Enable == enable;
        if (isDefault != null) exp &= _.IsDefault == isDefault;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= _.Name.Contains(key) | _.ModelName.Contains(key);

        return FindAll(exp, page);
    }

    #endregion

    #region 业务操作
    #endregion
}
