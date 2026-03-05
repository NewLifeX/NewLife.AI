using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife.AI.Providers;
using NewLife.Common;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;

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
        // 新逻辑：从 ProviderConfig 表读取已配置的提供商，为每个提供商创建默认模型配置
        var providers = ProviderConfig.FindAllEnabled();
        if (providers == null || providers.Count == 0)
        {
            if (XTrace.Debug) XTrace.WriteLine("未找到启用的提供商配置，跳过ModelConfig初始化");
            return;
        }

        // 获取已有编码集合，用于跳过已存在的配置
        var list = FindAll();
        var exists = list.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);

        var count = 0;
        var sort = list.Count > 0 ? list.Max(e => e.Sort) + 1 : 1;
        
        foreach (var provider in providers)
        {
            // 为每个提供商创建一个默认模型配置
            var code = $"{provider.Code}-default";
            if (!exists.TryGetValue(code, out var entity))
            {
                entity = new ModelConfig
                {
                    Code = code,
                    Name = $"{provider.Name} 默认模型",
                    ProviderId = provider.Id,
                    Enable = provider.Enable,
                    Sort = sort++,
                };

                if (XTrace.Debug) XTrace.WriteLine("为提供商 {0} 创建默认模型配置", provider.Name);
            }

            count += entity.Save();
        }

        if (count > 0 && XTrace.Debug)
            XTrace.WriteLine("完成初始化ModelConfig[模型配置]数据，修改 {0} 个模型配置！", count);
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
    /// <summary>关联的提供商配置</summary>
    [XmlIgnore, IgnoreDataMember, ScriptIgnore]
    public ProviderConfig ProviderInfo => Extends.Get(nameof(ProviderInfo), k => ProviderConfig.FindById(ProviderId));
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,ProviderId From ModelConfig Where CreateTime>'2020-01-24 00:00:00' Group By ProviderId Order By Id Desc limit 20
    static readonly FieldCache<ModelConfig> _ProviderCache = new(nameof(ProviderId))
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

    /// <summary>获取有效接口地址。从关联的提供商配置中获取</summary>
    /// <returns></returns>
    public String GetEffectiveEndpoint()
    {
        var provider = ProviderInfo;
        return provider?.Endpoint ?? "";
    }

    /// <summary>获取有效密钥。从关联的提供商配置中获取</summary>
    /// <returns></returns>
    public String GetEffectiveApiKey()
    {
        var provider = ProviderInfo;
        return provider?.ApiKey ?? "";
    }

    /// <summary>获取有效的提供商代码。从关联的提供商配置中获取</summary>
    /// <returns></returns>
    public String GetEffectiveProvider()
    {
        var provider = ProviderInfo;
        return provider?.Provider ?? "";
    }

    /// <summary>获取有效的API协议。从关联的提供商配置中获取</summary>
    /// <returns></returns>
    public String GetEffectiveApiProtocol()
    {
        var provider = ProviderInfo;
        return provider?.ApiProtocol ?? "";
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

    /// <summary>高级搜索。用于魔方前台列表页</summary>
    /// <param name="providerId">提供商编号</param>
    /// <param name="code">编码</param>
    /// <param name="supportThinking">支持思考</param>
    /// <param name="supportVision">支持视觉</param>
    /// <param name="supportImageGeneration">支持图像生成</param>
    /// <param name="supportFunctionCalling">支持函数调用</param>
    /// <param name="enable">启用</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数</param>
    /// <returns></returns>
    public static IList<ModelConfig> Search(Int32 providerId, String code, Boolean? supportThinking, Boolean? supportVision, Boolean? supportImageGeneration, Boolean? supportFunctionCalling, Boolean? enable, DateTime start, DateTime end, String key, Pager page)
    {
        var exp = new WhereExpression();

        if (providerId >= 0) exp &= _.ProviderId == providerId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (supportThinking != null) exp &= _.SupportThinking == supportThinking.Value;
        if (supportVision != null) exp &= _.SupportVision == supportVision.Value;
        if (supportImageGeneration != null) exp &= _.SupportImageGeneration == supportImageGeneration.Value;
        if (supportFunctionCalling != null) exp &= _.SupportFunctionCalling == supportFunctionCalling.Value;
        if (enable != null) exp &= _.Enable == enable.Value;

        exp &= _.CreateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion
}
