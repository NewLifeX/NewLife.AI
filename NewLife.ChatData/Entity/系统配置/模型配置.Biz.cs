using System.ComponentModel;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.AI.Clients;
using NewLife.Common;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.Cache;
using XCode.Membership;

namespace NewLife.ChatData.Entity;

public partial class ModelConfig : Entity<ModelConfig>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static ModelConfig()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(ContextLength));

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
        if (Name.IsNullOrEmpty()) Name = Code;

        return true;
    }

    /// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void InitData()
    {
        // 新逻辑：从 ProviderConfig 表读取已配置的提供商，为每个提供商创建默认模型配置
        var providers = ProviderConfig.FindAll();
        if (providers == null || providers.Count == 0)
        {
            if (XTrace.Debug) XTrace.WriteLine("未找到启用的提供商配置，跳过ModelConfig初始化");
            return;
        }

        // 获取已有编码集合，用于跳过已存在的配置
        var list = FindAll();

        var count = 0;
        var sort = list.Count > 0 ? list.Max(e => e.Sort) + 1 : 1;

        foreach (var provider in providers)
        {
            if (provider.Provider.IsNullOrEmpty()) continue;

            // 为每个提供商创建一个默认模型配置
            var descriptor = AiClientRegistry.Default.GetDescriptor(provider.Provider)
                ?? AiClientRegistry.Default.GetDescriptor(provider.Code);
            if (descriptor == null || descriptor.Models == null || descriptor.Models.Length == 0) continue;

            foreach (var model in descriptor.Models)
            {
                var entity = list.FirstOrDefault(e => e.ProviderId == provider.Id && e.Code.EqualIgnoreCase(model.Model));
                if (entity == null)
                {
                    entity = new ModelConfig
                    {
                        Code = model.Model,
                        //Name = model.DisplayName ?? model.Model,
                        ProviderId = provider.Id,
                        Enable = provider.Enable,
                        Sort = sort++,
                    };

                    XTrace.WriteLine("为提供商 {0} 创建默认模型配置", provider.Name);
                }

                entity.Name = model.DisplayName ?? model.Model;
                if (model.Capabilities != null)
                {
                    entity.SupportThinking = model.Capabilities.SupportThinking;
                    entity.SupportFunctionCalling = model.Capabilities.SupportFunctionCalling;
                    entity.SupportVision = model.Capabilities.SupportVision;
                    entity.SupportAudio = model.Capabilities.SupportAudio;
                    entity.SupportImageGeneration = model.Capabilities.SupportImageGeneration;
                    entity.SupportVideoGeneration = model.Capabilities.SupportVideoGeneration;
                }

                count += entity.Save();
            }
        }

        if (count > 0)
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

    /// <summary>角色组名</summary>
    [Map(__.RoleIds)]
    public virtual String? RoleNames => Extends.Get(nameof(RoleNames), k => RoleIds.SplitAsInt().Select(e => Role.FindByID(e)).Join(",", e => e.Name));

    /// <summary>部门组名</summary>
    [Map(__.DepartmentIds)]
    public virtual String? DepartmentNames => Extends.Get(nameof(DepartmentNames), k => DepartmentIds.SplitAsInt().Select(e => Department.FindByID(e)).Join(",", e => e.Name));
    #endregion

    #region 高级查询
    /// <summary>获取所有启用的模型配置，按排序降序、编号降序。模型自身已启用且关联提供商未禁用时才认为可用</summary>
    public static IList<ModelConfig> FindAllEnabled()
    {
        var list = Meta.Count < MaxCacheCount ? FindAllWithCache() : FindAll(_.Enable == true);
        return list.Where(e => e.Enable && e.ProviderInfo?.Enable == true).OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();
    }

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

    /// <summary>根据编码查找启用的模型配置。模型自身已启用且关联提供商未禁用时才认为可用</summary>
    /// <param name="code">模型编码</param>
    /// <returns>模型配置，未找到返回null</returns>
    public static ModelConfig FindByCode(String code)
    {
        if (code.IsNullOrEmpty()) return null;

        return FindAllWithCache().FirstOrDefault(e => e.Enable && e.Code.EqualIgnoreCase(code) && e.ProviderInfo?.Enable == true);
    }

    /// <summary>根据编码或名称查找启用的模型配置。优先按Code匹配，再按Name匹配</summary>
    /// <param name="name">模型编码或名称</param>
    /// <returns>模型配置，未找到返回null</returns>
    public static ModelConfig? FindByCodeOrName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        var list = FindAllWithCache().Where(e => e.Enable && e.ProviderInfo?.Enable == true).ToList();
        return list.FirstOrDefault(e => e.Code.EqualIgnoreCase(name))
            ?? list.FirstOrDefault(e => e.Name.EqualIgnoreCase(name));
    }

    /// <summary>获取用户可用的模型列表</summary>
    /// <param name="roleIds">用户角色组</param>
    /// <param name="departmentId">用户部门编号</param>
    /// <returns></returns>
    public static IList<ModelConfig> FindAllByPermission(Int32[] roleIds, Int32 departmentId)
    {
        //var list = FindAll(_.Enable == true, _.Sort.Desc() & _.Id.Desc(), null, 0, 0);
        var list = FindAllWithCache().Where(e => e.Enable && e.ProviderInfo?.Enable == true).OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();
        if (list.Count == 0) return list;

        // 过滤有权限的模型，同时检查提供商级别权限
        return list.Where(e => e.CheckPermission(roleIds, departmentId) && (e.ProviderInfo == null || e.ProviderInfo.CheckPermission(roleIds, departmentId))).ToList();
    }

    /// <summary>高级搜索。用于魔方前台列表页</summary>
    /// <param name="providerId">提供商编号</param>
    /// <param name="code">编码</param>
    /// <param name="supportThinking">支持思考</param>
    /// <param name="supportFunctionCalling">支持函数调用</param>
    /// <param name="supportVision">支持视觉</param>
    /// <param name="supportAudio">支持音频</param>
    /// <param name="supportImageGeneration">支持图像生成</param>
    /// <param name="supportVideoGeneration">支持视频生成</param>
    /// <param name="enable">启用</param>
    /// <param name="start">创建时间开始</param>
    /// <param name="end">创建时间结束</param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数</param>
    /// <returns></returns>
    public static IList<ModelConfig> Search(Int32 providerId, String code, Boolean? supportThinking, Boolean? supportFunctionCalling, Boolean? supportVision, Boolean? supportAudio, Boolean? supportImageGeneration, Boolean? supportVideoGeneration, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (providerId >= 0) exp &= _.ProviderId == providerId;
        if (!code.IsNullOrEmpty()) exp &= _.Code == code;
        if (supportThinking != null) exp &= _.SupportThinking == supportThinking.Value;
        if (supportFunctionCalling != null) exp &= _.SupportFunctionCalling == supportFunctionCalling.Value;
        if (supportVision != null) exp &= _.SupportVision == supportVision.Value;
        if (supportAudio != null) exp &= _.SupportAudio == supportAudio.Value;
        if (supportImageGeneration != null) exp &= _.SupportImageGeneration == supportImageGeneration.Value;
        if (supportVideoGeneration != null) exp &= _.SupportVideoGeneration == supportVideoGeneration.Value;
        if (enable != null) exp &= _.Enable == enable.Value;

        exp &= _.CreateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }
    #endregion
}
