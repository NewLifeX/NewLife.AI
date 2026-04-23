using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.AI.Clients;
using NewLife.Common;
using NewLife.Data;
using NewLife.Log;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Membership;

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

        if (ModelLimit == 0) ModelLimit = 10;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正
        if (Code.IsNullOrEmpty() && !Name.IsNullOrEmpty()) Code = PinYin.Get(Name);
        if (Name.IsNullOrEmpty()) Name = Code;

        return true;
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // 从 AiClientRegistry 获取所有内置服务商描述符，写入尚未存在的提供商配置
        var registry = AiClientRegistry.Default;
        var descriptors = registry.Descriptors.Values.ToList();
        if (descriptors.Count == 0) return;

        // 获取已有编码集合，用于跳过已存在的配置
        var list = FindAll();

        var count = 0;
        var sort = list.Count > 0 ? list.Max(e => e.Sort) + 1 : 1;
        foreach (var descriptor in descriptors)
        {
            // 向后兼容：oldFullName 用于 Provider 字段（GatewayService.GetDescriptor 支持按 Code 或旧全名查找）
            var oldFullName = $"NewLife.AI.Providers.{descriptor.Code}Provider";
            var entity = FindOrCreate(list, descriptor.Code, oldFullName, descriptor.DisplayName, descriptor.Code, descriptor.DefaultEndpoint, descriptor.Protocol, descriptor.Description ?? "", ref sort);

            // NewLifeAI 默认启用并设置演示密钥，方便首次使用开箱即用
            if (descriptor.Code == "NewLifeAI")
            {
                if (entity.Id == 0) entity.Enable = true;
                if (entity.ApiKey.IsNullOrEmpty()) entity.ApiKey = "sk-NewLifeAI2026";
            }

            count += entity.Save();

            // Ollama 额外生成一条云端 Ollama 配置条目（默认禁用）
            if (descriptor.Code == "Ollama")
            {
                var cloud = FindOrCreate(list, "OllamaCloud", null, "云端Ollama", descriptor.Code, "https://ollama.com", descriptor.Protocol, descriptor.Description ?? "", ref sort);
                count += cloud.Save();
            }
        }

        if (count > 0)
            XTrace.WriteLine("完成初始化ProviderConfig[提供商配置]数据，修改 {0} 个提供商配置！", count);
    }
    #endregion

    #region 扩展属性
    /// <summary>角色组名</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    [Map(__.RoleIds)]
    public virtual String? RoleNames => Extends.Get(nameof(RoleNames), k => RoleIds.SplitAsInt().Select(e => Role.FindByID(e)).Join(",", e => e.Name));

    /// <summary>部门组名</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    [Map(__.DepartmentIds)]
    public virtual String? DepartmentNames => Extends.Get(nameof(DepartmentNames), k => DepartmentIds.SplitAsInt().Select(e => Department.FindByID(e)).Join(",", e => e.Name));
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="code">编码。提供商实例唯一标识，如my-openai</param>
    /// <param name="provider">实现类。IAiProvider实现类完整类名，如NewLife.AI.Providers.OpenAiProvider</param>
    /// <param name="enable">启用</param>
    /// <param name="start">更新时间开始</param>
    /// <param name="end">更新时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<ProviderConfig> Search(String? code, String? provider, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (!provider.IsNullOrEmpty()) exp &= _.Provider == provider;
        if (enable != null) exp &= _.Enable == enable;
        exp &= _.UpdateTime.Between(start, end);
        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }

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
    /// <summary>根据编码查找提供商配置</summary>
    /// <param name="code">编码</param>
    /// <returns></returns>
    public static ProviderConfig FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        return Find(_.Code == code);
    }

    /// <summary>根据提供商类型全名查找</summary>
    /// <param name="provider">提供商类型全名</param>
    /// <returns>实体对象</returns>
    public static ProviderConfig FindByProvider(String? provider)
    {
        if (provider.IsNullOrEmpty()) return null;

        // 实体缓存
        if (Meta.Session.Count < MaxCacheCount) return Meta.Cache.Find(e => e.Provider == provider);

        return Find(_.Provider == provider);
    }

    /// <summary>获取所有启用的提供商配置</summary>
    /// <returns></returns>
    public static IList<ProviderConfig> FindAllEnabled()
    {
        return FindAll(_.Enable == true, _.Sort.Desc() & _.Id.Desc(), null, 0, 0);
    }

    /// <summary>查找或初始化提供商配置，已存在则更新基本信息（不覆盖用户配置的 Endpoint/ApiKey）</summary>
    /// <param name="list">当前已有配置列表</param>
    /// <param name="code">编码</param>
    /// <param name="typeFallback">按提供商类型全名兜底查找的键，传 null 则跳过兜底</param>
    /// <param name="name">名称</param>
    /// <param name="providerFullName">提供商类型全名</param>
    /// <param name="defaultEndpoint">默认端点</param>
    /// <param name="apiProtocol">API 协议</param>
    /// <param name="remark">备注</param>
    /// <param name="sort">当前排序号（新建时自增）</param>
    /// <returns>待保存的实体</returns>
    private static ProviderConfig FindOrCreate(IList<ProviderConfig> list, String code, String? typeFallback, String name, String providerFullName, String defaultEndpoint, String apiProtocol, String remark, ref Int32 sort)
    {
        var entity = list.FirstOrDefault(e => e.Code == code);
        if (entity == null && !typeFallback.IsNullOrEmpty())
            entity = list.FirstOrDefault(e => e.Provider == typeFallback);

        if (entity == null)
        {
            entity = new ProviderConfig
            {
                Code = code,
                Name = name,
                Endpoint = defaultEndpoint,
                Enable = false,
                Sort = sort++,
            };
            XTrace.WriteLine("发现新提供商配置：{0}（{1}）", name, providerFullName);
        }
        else
        {
            if (entity.Endpoint.IsNullOrEmpty()) entity.Endpoint = defaultEndpoint;
        }

        if (entity.Name.IsNullOrEmpty()) entity.Name = name;
        entity.Provider = providerFullName;
        entity.ApiProtocol = apiProtocol;
        entity.ModelLimit = 10;
        entity.Remark = remark;

        return entity;
    }

    /// <summary>检查用户是否有权限使用此提供商</summary>
    /// <param name="roleIds">用户角色组</param>
    /// <param name="departmentId">用户部门编号</param>
    /// <returns>true表示有权限，false表示无权限</returns>
    public Boolean CheckPermission(Int32[] roleIds, Int32 departmentId)
    {
        // 未设置角色组和部门组，不限制
        if (RoleIds.IsNullOrEmpty() && DepartmentIds.IsNullOrEmpty()) return true;

        // 检查角色权限
        if (!RoleIds.IsNullOrEmpty() && roleIds != null && roleIds.Length > 0)
        {
            var roleArray = RoleIds.SplitAsInt();
            if (roleArray.Intersect(roleIds).Any()) return true;
        }

        // 检查部门权限
        if (!DepartmentIds.IsNullOrEmpty())
        {
            var deptArray = DepartmentIds.SplitAsInt();
            if (deptArray.Contains(departmentId)) return true;
        }

        return false;
    }

    /// <summary>获取用户可用的提供商列表</summary>
    /// <param name="roleIds">用户角色组</param>
    /// <param name="departmentId">用户部门编号</param>
    /// <returns></returns>
    public static IList<ProviderConfig> FindAllByPermission(Int32[] roleIds, Int32 departmentId)
    {
        var list = FindAllEnabled();
        if (list.Count == 0) return list;

        // 过滤有权限的提供商
        return list.Where(e => e.CheckPermission(roleIds, departmentId)).ToList();
    }
    #endregion
}
