using System.ComponentModel;
using NewLife.AI.Providers;
using NewLife.Common;
using NewLife.Log;
using NewLife.Reflection;
using XCode;
using XCode.Cache;

namespace NewLife.ChatAI.Entity;

public partial class ModelConfig : Entity<ModelConfig>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static ModelConfig()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(MaxTokens));

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

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // 从 AiProviderFactory 检测所有已注册的服务商，写入尚未存在的模型配置
        var factory = AiProviderFactory.Default;
        var providers = factory.Providers;
        if (providers == null || providers.Count == 0) return;

        // 获取已有编码集合，用于跳过已存在的配置
        var list = FindAll();
        var exists = list.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);

        var count = 0;
        var sort = list.Count > 0 ? list.Max(e => e.Sort) + 1 : 1;
        foreach (var item in providers)
        {
            var provider = item.Value;
            var code = provider.GetType().Name.TrimEnd("Provider");
            if (!exists.TryGetValue(code, out var entity))
            {
                entity = new ModelConfig
                {
                    Name = provider.Name,
                    Enable = false,
                    Sort = sort++,
                };

                if (XTrace.Debug) XTrace.WriteLine("发现新模型配置：{0}（{1}）", provider.Name, provider.DefaultEndpoint);
            }

            entity.Code = code;
            entity.ModelName = code;
            entity.Provider = provider.Name;
            entity.Endpoint = provider.DefaultEndpoint;
            entity.ApiProtocol = provider.ApiProtocol;

            // 同步服务商默认能力
            var caps = provider.DefaultCapabilities;
            if (caps != null)
            {
                entity.SupportThinking = caps.SupportThinking;
                entity.SupportVision = caps.SupportVision;
                entity.SupportImageGeneration = caps.SupportImageGeneration;
                entity.SupportFunctionCalling = caps.SupportFunctionCalling;
            }

            count += entity.Save();
        }

        if (count > 0 && XTrace.Debug)
            XTrace.WriteLine("完成初始化ModelConfig[模型配置]数据，修改 {0} 个服务商配置！", count);
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

    // Select Count(Id) as Id,Provider From ModelConfig Where CreateTime>'2020-01-24 00:00:00' Group By Provider Order By Id Desc limit 20
    static readonly FieldCache<ModelConfig> _ProviderCache = new(nameof(Provider))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取提供商列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetProviderList() => _ProviderCache.FindAllName();
    #endregion

    #region 业务操作
    /// <summary>转为模型类</summary>
    /// <returns></returns>
    public ModelConfigModel ToModel()
    {
        var model = new ModelConfigModel();
        model.Copy(this);

        return model;
    }

    /// <summary>获取有效接口地址。为空时递归从父级继承</summary>
    /// <returns></returns>
    public String GetEffectiveEndpoint()
    {
        if (!Endpoint.IsNullOrEmpty()) return Endpoint;
        if (ParentId <= 0) return Endpoint;

        // 递归查找父级，最多向上查找10层防止循环引用
        var parent = FindById(ParentId);
        for (var i = 0; i < 10 && parent != null; i++)
        {
            if (!parent.Endpoint.IsNullOrEmpty()) return parent.Endpoint;
            if (parent.ParentId <= 0) break;

            parent = FindById(parent.ParentId);
        }

        return Endpoint;
    }

    /// <summary>获取有效密钥。为空时递归从父级继承</summary>
    /// <returns></returns>
    public String GetEffectiveApiKey()
    {
        if (!ApiKey.IsNullOrEmpty()) return ApiKey;
        if (ParentId <= 0) return ApiKey;

        // 递归查找父级，最多向上查找10层防止循环引用
        var parent = FindById(ParentId);
        for (var i = 0; i < 10 && parent != null; i++)
        {
            if (!parent.ApiKey.IsNullOrEmpty()) return parent.ApiKey;
            if (parent.ParentId <= 0) break;

            parent = FindById(parent.ParentId);
        }

        return ApiKey;
    }

    /// <summary>检查用户是否有权限使用此模型</summary>
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
            var roleArray = RoleIds.Split(',').Select(x => x.ToInt()).ToArray();
            if (roleArray.Intersect(roleIds).Any()) return true;
        }

        // 检查部门权限
        if (!DepartmentIds.IsNullOrEmpty())
        {
            var deptArray = DepartmentIds.Split(',').Select(x => x.ToInt()).ToArray();
            if (deptArray.Contains(departmentId)) return true;
        }

        return false;
    }

    /// <summary>获取用户可用的模型列表</summary>
    /// <param name="roleIds">用户角色组</param>
    /// <param name="departmentId">用户部门编号</param>
    /// <returns></returns>
    public static IList<ModelConfig> FindAllByPermission(Int32[] roleIds, Int32 departmentId)
    {
        var list = FindAll(_.Enable == true, _.Sort.Asc(), null, 0, 0);
        if (list == null || list.Count == 0) return list;

        // 过滤有权限的模型
        return list.Where(e => e.CheckPermission(roleIds, departmentId)).ToList();
    }
    #endregion
}
