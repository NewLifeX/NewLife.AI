using NewLife.AI.Clients;
using NewLife.ChatData.Entity;
using NewLife.Log;
using XCode.Membership;
using ILog = NewLife.Log.ILog;

namespace NewLife.ChatData.Services;

/// <summary>模型服务。封装模型解析与客户端创建，解耦业务服务对 GatewayService 的依赖</summary>
/// <remarks>
/// 将模型路由（按 ID/Code 查找 ModelConfig）与客户端工厂（BuildOptions + AiClientRegistry.Factory）
/// 统一收口，业务服务只需注入 ModelService 即可获取可用模型和对应的 IChatClient 实例。
/// </remarks>
public class ModelService(ITracer tracer, ILog log)
{
    private readonly AiClientRegistry _registry = AiClientRegistry.Default;

    #region 模型解析
    /// <summary>根据 AppKey 获取该密钥所属用户可使用的模型列表</summary>
    /// <param name="appKey">应用密钥实体</param>
    /// <returns>经权限过滤的启用模型列表</returns>
    public IList<ModelConfig> GetModelsForAppKey(AppKey appKey)
    {
        Int32[] roleIds = [];
        var departmentId = 0;

        if (appKey.UserId > 0)
        {
            var iuser = ManageProvider.Provider?.FindByID(appKey.UserId) as IUser;
            roleIds = iuser?.RoleIds?.SplitAsInt() ?? [];
            departmentId = iuser?.DepartmentID ?? 0;
        }

        var models = ModelConfig.FindAllByPermission(roleIds, departmentId);
        return models.Where(e => IsModelAllowed(appKey, e)).ToList();
    }

    /// <summary>检查 AppKey 是否允许访问指定模型。若未配置模型限制则放行</summary>
    /// <param name="appKey">应用密钥</param>
    /// <param name="config">模型配置</param>
    /// <returns>true 表示允许访问</returns>
    public Boolean IsModelAllowed(AppKey appKey, ModelConfig config)
    {
        if (appKey == null || config == null) return false;

        var set = appKey.GetAllowedModels();
        if (set.Count == 0) return true;

        if (!config.Code.IsNullOrEmpty() && set.Contains(config.Code)) return true;
        if (!config.Name.IsNullOrEmpty() && set.Contains(config.Name)) return true;

        return false;
    }

    /// <summary>根据模型编号查找模型配置</summary>
    /// <param name="modelId">模型编号</param>
    /// <returns>模型配置，未找到或未启用返回 null</returns>
    public ModelConfig? ResolveModel(Int32 modelId)
    {
        if (modelId <= 0) return null;

        var config = ModelConfig.FindById(modelId);
        if (config == null || !config.Enable) return null;

        return config;
    }

    /// <summary>根据模型编号查找模型配置，当编号为 0 或找不到时自动降级为系统默认模型</summary>
    /// <param name="modelId">模型编号，0 表示自动选择默认模型</param>
    /// <returns>模型配置，系统无可用模型时返回 null</returns>
    public ModelConfig? ResolveModelOrDefault(Int32 modelId)
    {
        if (modelId > 0)
        {
            var config = ModelConfig.FindById(modelId);
            if (config != null && config.Enable) return config;
        }

        var setting = ChatSetting.Current;
        var models = ModelConfig.FindAllEnabled();
        return SelectDefaultModel(models, setting.DefaultModel);
    }

    /// <summary>根据模型编码查找模型配置</summary>
    /// <param name="modelCode">模型编码</param>
    /// <returns>模型配置，未找到返回 null</returns>
    public ModelConfig? ResolveModelByCode(String? modelCode)
    {
        if (String.IsNullOrWhiteSpace(modelCode)) return null;

        return ModelConfig.FindByCode(modelCode);
    }

    /// <summary>解析轻量模型配置。优先按 ChatSetting.LightweightModel 编码查找，未配置时回退到 LearningModel，最终回退到指定的 fallbackModelId</summary>
    /// <param name="fallbackModelId">回退模型编号（通常为当前对话模型）</param>
    /// <returns>模型配置，未找到返回 null</returns>
    public ModelConfig? ResolveLightweightModel(Int32 fallbackModelId = 0)
    {
        var setting = ChatSetting.Current;

        if (!setting.LightweightModel.IsNullOrEmpty())
        {
            var config = ModelConfig.FindByCode(setting.LightweightModel);
            if (config != null && config.Enable) return config;
        }

        if (!setting.LearningModel.IsNullOrEmpty())
        {
            var config = ModelConfig.FindByCode(setting.LearningModel);
            if (config != null && config.Enable) return config;
        }

        return ResolveModelOrDefault(fallbackModelId);
    }

    /// <summary>从已启用模型列表中按优先级选出默认模型</summary>
    /// <param name="models">已启用的模型列表</param>
    /// <param name="defaultModelId">系统配置的默认模型编号，0 表示不指定</param>
    /// <returns>选出的模型配置，列表为空时返回 null</returns>
    public static ModelConfig? SelectDefaultModel(IList<ModelConfig> models, Int32 defaultModelId)
    {
        if (models == null || models.Count == 0) return null;

        if (defaultModelId > 0)
        {
            var preferred = models.FirstOrDefault(e => e.Id == defaultModelId);
            if (preferred != null) return preferred;
        }

        return models.OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).FirstOrDefault();
    }
    #endregion

    #region 客户端创建
    /// <summary>根据模型配置创建 AI 客户端实例</summary>
    /// <param name="config">模型配置</param>
    /// <returns>已绑定连接参数的客户端实例，服务商未注册时返回 null</returns>
    public IChatClient? CreateClient(ModelConfig config)
    {
        if (config == null) return null;

        var providerConfig = config.ProviderInfo;
        if (providerConfig == null || providerConfig.Provider.IsNullOrWhiteSpace()) return null;

        var descriptor = _registry.GetDescriptor(providerConfig.Provider);
        if (descriptor == null) return null;

        var client = descriptor.Factory(BuildOptions(config));
        if (client is ITracerFeature tf) tf.Tracer = tracer;
        if (client is ILogFeature lf) lf.Log = log;

        return client;
    }

    /// <summary>检查模型的服务商是否已注册可用</summary>
    /// <param name="config">模型配置</param>
    /// <returns>true 表示可创建客户端</returns>
    public Boolean IsAvailable(ModelConfig? config)
    {
        if (config == null) return false;

        var providerConfig = config.ProviderInfo;
        if (providerConfig == null || providerConfig.Provider.IsNullOrWhiteSpace()) return false;

        return _registry.GetDescriptor(providerConfig.Provider) != null;
    }

    /// <summary>构建服务商连接选项。从关联的 ProviderConfig 获取 Endpoint/ApiKey，从 ModelConfig 获取默认模型和协议</summary>
    /// <param name="config">模型配置</param>
    /// <returns>连接选项</returns>
    public static AiClientOptions BuildOptions(ModelConfig config)
    {
        var providerConfig = config.ProviderInfo;
        return new AiClientOptions
        {
            Endpoint = config.GetEffectiveEndpoint(),
            ApiKey = config.GetEffectiveApiKey(),
            Model = config.Code,
            Protocol = providerConfig?.ApiProtocol,
        };
    }
    #endregion
}
