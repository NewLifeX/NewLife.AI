using System.ComponentModel;
using NewLife.AI.Providers;
using NewLife.Common;
using NewLife.Data;
using NewLife.Log;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;

namespace NewLife.ChatAI.Entity;

public partial class ProviderConfig : Entity<ProviderConfig>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static ProviderConfig()
    {
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
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正
        if (Code.IsNullOrEmpty() && !Name.IsNullOrEmpty()) Code = PinYin.Get(Name);

        return true;
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // 从 AiProviderFactory 反射扫描得到所有已注册的 IAiProvider 原型，写入尚未存在的提供商配置
        var factory = AiProviderFactory.Default;
        var prototypes = factory.RegisteredTypes.ToList();
        if (prototypes == null || prototypes.Count == 0) return;

        // 获取已有编码集合，用于跳过已存在的配置（按 Provider=FullName 去重，每种类型只初始化一条）
        var list = FindAll();

        var count = 0;
        var sort = list.Count > 0 ? list.Max(e => e.Sort) + 1 : 1;
        foreach (var type in prototypes)
        {
            var provider = factory.GetProvider(type)
                ?? throw new InvalidOperationException($"无法创建提供商实例：{type.FullName}");
            var entity = list.FirstOrDefault(e => e.Code == provider.Code) ?? list.FirstOrDefault(e => e.Provider == type.FullName);
            if (entity == null)
            {
                entity = new ProviderConfig
                {
                    Code = provider.Code,
                    Name = provider.Name,
                    Provider = type.FullName!,
                    Endpoint = provider.DefaultEndpoint,
                    ApiProtocol = provider.ApiProtocol,
                    Remark = provider.Description!,
                    Enable = true,
                    Sort = sort++,
                };

                XTrace.WriteLine("发现新提供商配置：{0}（{1}）", provider.Name, type.FullName);
            }
            else
            {
                // 更新已有配置的基本信息（不覆盖用户配置的 Endpoint/ApiKey）
                entity.Name = provider.Name;
                entity.Provider = type.FullName!;
                if (entity.Endpoint.IsNullOrEmpty()) entity.Endpoint = provider.DefaultEndpoint;
                entity.ApiProtocol = provider.ApiProtocol;
                entity.Remark = provider.Description!;
            }

            count += entity.Save();
        }

        if (count > 0)
            XTrace.WriteLine("完成初始化ProviderConfig[提供商配置]数据，修改 {0} 个提供商配置！", count);
    }

    /// <summary>已重载。配置变更后使工厂缓存失效，确保下次调用使用新实例</summary>
    /// <returns></returns>
    protected override Int32 OnUpdate()
    {
        var result = base.OnUpdate();
        AiProviderFactory.Default.InvalidateConfig(Id);
        return result;
    }

    /// <summary>已重载。删除后清理工厂缓存</summary>
    /// <returns></returns>
    protected override Int32 OnDelete()
    {
        var result = base.OnDelete();
        AiProviderFactory.Default.InvalidateConfig(Id);
        return result;
    }
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询
    // Select Count(Id) as Id,Provider From ProviderConfig Where CreateTime>'2020-01-24 00:00:00' Group By Provider Order By Id Desc limit 20
    static readonly FieldCache<ProviderConfig> _ProviderCache = new(nameof(Provider))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取协议列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetProviderList() => _ProviderCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>转为模型类</summary>
    /// <returns></returns>
    public ProviderConfigModel ToModel()
    {
        var model = new ProviderConfigModel
        {
            Id = Id,
            Code = Code,
            Name = Name,
            Provider = Provider,
            Endpoint = Endpoint,
            ApiKey = ApiKey,
            ApiProtocol = ApiProtocol,
            Enable = Enable,
            Sort = Sort,
            CreateUserID = CreateUserID,
            CreateIP = CreateIP,
            CreateTime = CreateTime,
            UpdateUserID = UpdateUserID,
            UpdateIP = UpdateIP,
            UpdateTime = UpdateTime,
            Remark = Remark,
        };

        return model;
    }

    /// <summary>根据编码查找提供商配置</summary>
    /// <param name="code">编码</param>
    /// <returns></returns>
    public static ProviderConfig FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        return Find(_.Code == code);
    }

    /// <summary>获取所有启用的提供商配置</summary>
    /// <returns></returns>
    public static IList<ProviderConfig> FindAllEnabled()
    {
        return FindAll(_.Enable == true, _.Sort.Asc(), null, 0, 0);
    }

    /// <summary>高级搜索。用于魔方前台列表页</summary>
    /// <param name="code">编码</param>
    /// <param name="provider">协议</param>
    /// <param name="enable">启用</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数</param>
    /// <returns></returns>
    public static IList<ProviderConfig> Search(String code, String provider, Boolean? enable, DateTime start, DateTime end, String key, Pager page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!provider.IsNullOrEmpty()) exp &= _.Provider == provider;
        if (enable != null) exp &= _.Enable == enable.Value;

        exp &= _.CreateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion
}
