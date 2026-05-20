using NewLife.AI.Clients;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Embedding;
using NewLife.AI.Interfaces;
using NewLife.Log;
using XCode.Membership;
using ILog = NewLife.Log.ILog;

namespace NewLife.ChatAI.Services;

/// <summary>模型服务。封装模型解析与客户端创建，解耦业务服务对 GatewayService 的依赖</summary>
/// <remarks>
/// 将模型路由（按 ID/Code 查找 ModelConfig）与客户端工厂（BuildOptions + AiClientRegistry.Factory）
/// 统一收口，业务服务只需注入 ModelService 即可获取可用模型和对应的 IChatClient 实例。
/// </remarks>
public class ModelService(IChatSetting chatSetting, UsageService? usageService, ITracer tracer, ILog log)
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
    /// <param name="model">模型配置</param>
    /// <returns>true 表示允许访问</returns>
    public Boolean IsModelAllowed(AppKey appKey, ModelConfig model)
    {
        if (appKey == null || model == null) return false;

        var set = appKey.GetAllowedModels();
        if (set.Count == 0) return true;

        if (!model.Code.IsNullOrEmpty() && set.Contains(model.Code)) return true;
        if (!model.Name.IsNullOrEmpty() && set.Contains(model.Name)) return true;

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

        var models = ModelConfig.FindAllEnabled().OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();
        return SelectDefaultModel(models, chatSetting.DefaultModel);
    }

    /// <summary>根据模型编码查找模型配置</summary>
    /// <param name="modelCode">模型编码</param>
    /// <returns>模型配置，未找到返回 null</returns>
    public ModelConfig? ResolveModelByCode(String? modelCode)
    {
        if (String.IsNullOrWhiteSpace(modelCode)) return null;

        return ModelConfig.FindByCode(modelCode);
    }

    /// <summary>解析轻量模型配置。优先按 ChatSetting.LightweightModel 编码查找；未配置时选择优先级最高的 flash/lite/mini/small 轻量文本模型；仍未找到时依次回退到 fallbackModelId 或主模型</summary>
    /// <param name="fallbackModelId">兜底模型编号（通常为当前对话模型），0 表示使用主模型</param>
    /// <returns>模型配置，未找到返回 null</returns>
    public ModelConfig? ResolveLightweightModel(Int32 fallbackModelId = 0)
    {
        if (!chatSetting.LightweightModel.IsNullOrEmpty())
        {
            var config = ModelConfig.FindByCode(chatSetting.LightweightModel);
            if (config != null && config.Enable) return config;
        }

        var models = ModelConfig.FindAllEnabled().OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();
        var lightweight = models.FirstOrDefault(e => !e.SupportEmbedding && IsLightweightCode(e.Code, e.Name));
        if (lightweight != null) return lightweight;

        if (fallbackModelId > 0)
        {
            var fallback = models.FirstOrDefault(e => e.Id == fallbackModelId);
            if (fallback != null) return fallback;
        }

        return SelectDefaultModel(models, chatSetting.DefaultModel);
    }

    /// <summary>解析嵌入模型配置。优先按 ChatSetting.EmbedModel 编码查找；未配置时选择优先级最高的嵌入模型（SupportEmbedding=true）；仍未找到则返回 null（调用方退化到本地哈希嵌入）</summary>
    /// <returns>嵌入模型配置，未找到返回 null</returns>
    public ModelConfig? GetEmbeddingModel()
    {
        if (!chatSetting.EmbedModel.IsNullOrEmpty())
        {
            var config = ModelConfig.FindByCode(chatSetting.EmbedModel);
            if (config != null && config.Enable) return config;
        }

        var models = ModelConfig.FindAllEnabled().OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();
        return models.FirstOrDefault(e => e.SupportEmbedding);
    }

    /// <summary>从已启用模型列表中按优先级选出默认文本模型</summary>
    /// <param name="models">已启用的模型列表</param>
    /// <param name="defaultModelId">系统配置的默认模型编号，0 表示不指定</param>
    /// <returns>选出的模型配置，列表为空时返回 null</returns>
    private static ModelConfig? SelectDefaultModel(IList<ModelConfig> models, Int32 defaultModelId)
    {
        if (models == null || models.Count == 0) return null;

        if (defaultModelId > 0)
        {
            var preferred = models.FirstOrDefault(e => e.Id == defaultModelId);
            if (preferred != null) return preferred;
        }

        // 优先选择文本模型（SupportEmbedding=false），按 Sort 降序取优先级最高的
        //var sorted = models.OrderByDescending(e => e.Sort).ThenByDescending(e => e.Id).ToList();
        return models.FirstOrDefault(e => !e.SupportEmbedding) ?? models.FirstOrDefault();
    }

    /// <summary>判断模型编码或名称是否含有轻量模型标识（flash/lite/mini/small）</summary>
    private static Boolean IsLightweightCode(String? code, String? name)
    {
        var text = ((code ?? "") + " " + (name ?? "")).ToLower();
        return text.Contains("flash") || text.Contains("lite") || text.Contains("mini") || text.Contains("small");
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

    /// <summary>根据模型配置创建嵌入向量客户端。通过注册表创建 IChatClient 后转型为 IEmbeddingClient，支持所有已注册服务商</summary>
    /// <param name="config">模型配置（embedding 专用模型）</param>
    /// <returns>已绑定连接参数的嵌入客户端，配置为空或客户端不支持嵌入时返回 null</returns>
    public IEmbeddingClient? CreateEmbeddingClient(ModelConfig config)
    {
        if (config == null) return null;

        var client = CreateClient(config);
        return client as IEmbeddingClient;
    }

    /// <summary>构建服务商连接选项。从关联的 ProviderConfig 获取 Endpoint/ApiKey，从 ModelConfig 获取默认模型和协议</summary>
    /// <param name="model">模型配置</param>
    /// <returns>连接选项</returns>
    protected virtual AiClientOptions BuildOptions(ModelConfig model)
    {
        var providerConfig = model.ProviderInfo;
        return new AiClientOptions
        {
            Endpoint = model.GetEffectiveEndpoint(),
            ApiKey = model.GetEffectiveApiKey(),
            Model = model.GetEffectiveModelCode(),
            Protocol = providerConfig?.ApiProtocol,
        };
    }
    #endregion

    #region 包装调用
    /// <summary>调用 LLM 并记录用量。封装创建客户端→调用→记录用量→释放全流程</summary>
    /// <param name="model">模型配置</param>
    /// <param name="conversation">会话上下文，null 时不记录用量</param>
    /// <param name="userMessage">用户消息文本</param>
    /// <param name="systemMessage">系统消息文本，null 时不发送系统消息</param>
    /// <param name="options">LLM 调用选项</param>
    /// <param name="source">用量来源标记（Title/Compact/Knowledge 等）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型输出文本，客户端不可用或调用失败时返回 null</returns>
    public async Task<String?> CallAsync(ModelConfig model, IConversation? conversation, String userMessage, String? systemMessage, ChatOptions? options, String source, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(model);
        if (client == null) return null;

        IList<AiChatMessage> messages = systemMessage.IsNullOrEmpty()
            ? [new AiChatMessage { Role = "user", Content = userMessage }]
            : [new AiChatMessage { Role = "system", Content = systemMessage }, new AiChatMessage { Role = "user", Content = userMessage }];

        options ??= new();
        options.EnableThinking ??= false;
        var response = await client.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        if (conversation != null && usageService != null && response.Usage != null)
            usageService.Record(conversation, null, null, model, response.Usage, source);

        return response.Text;
    }

    /// <summary>嵌入单条文本并记录用量。API 客户端不可用时返回 null，调用方自行回退本地哈希嵌入</summary>
    /// <param name="model">嵌入模型配置</param>
    /// <param name="conversation">会话上下文，null 时不记录用量</param>
    /// <param name="text">嵌入文本</param>
    /// <param name="source">用量来源标记，默认 Embedding</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>嵌入向量，客户端不可用时返回 null</returns>
    public async Task<Single[]?> EmbedAsync(ModelConfig model, IConversation? conversation, String text, String source = "Embedding", CancellationToken cancellationToken = default)
    {
        using var client = CreateEmbeddingClient(model);
        if (client == null) return null;

        var response = await client.GenerateAsync(new EmbeddingRequest { Input = [text] }, cancellationToken).ConfigureAwait(false);

        if (conversation != null && usageService != null && response.Usage != null)
        {
            var usage = response.Usage;
            var ud = new UsageDetails { InputTokens = usage.PromptTokens, OutputTokens = usage.TotalTokens - usage.PromptTokens, TotalTokens = usage.TotalTokens };
            usageService.Record(conversation, null, null, model, ud, source);
        }

        return response.Data.FirstOrDefault()?.Embedding;
    }

    /// <summary>批量嵌入文本列表并记录用量。每批次独立记录；API 客户端不可用时返回空数组，调用方自行回退本地哈希嵌入</summary>
    /// <param name="model">嵌入模型配置</param>
    /// <param name="conversation">会话上下文，null 时不记录用量</param>
    /// <param name="texts">文本列表</param>
    /// <param name="source">用量来源标记，默认 Embedding</param>
    /// <param name="batchSize">每批次嵌入文本数，默认 20</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>与 texts 等长的向量数组，API 客户端不可用时返回空数组</returns>
    public async Task<Single[][]> BulkEmbedAsync(ModelConfig model, IConversation? conversation, IList<String> texts, String source = "Embedding", Int32 batchSize = 20, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return [];

        using var client = CreateEmbeddingClient(model);
        if (client == null) return [];

        var result = new Single[texts.Count][];
        for (var offset = 0; offset < texts.Count; offset += batchSize)
        {
            var batch = texts.Skip(offset).Take(batchSize).ToList();
            var response = await client.GenerateAsync(new EmbeddingRequest { Input = batch }, cancellationToken).ConfigureAwait(false);
            var baseIdx = offset;
            foreach (var item in response.Data.OrderBy(x => x.Index))
            {
                var idx = baseIdx + item.Index;
                if (idx < result.Length)
                    result[idx] = item.Embedding ?? [];
            }
            if (conversation != null && usageService != null && response.Usage != null)
            {
                var usage = response.Usage;
                var ud = new UsageDetails { InputTokens = usage.PromptTokens, OutputTokens = usage.TotalTokens - usage.PromptTokens, TotalTokens = usage.TotalTokens };
                usageService.Record(conversation, null, null, model, ud, source);
            }
        }
        return result;
    }
    #endregion

    #region 模型发现
    /// <summary>探测指定提供商的模型列表并同步到数据库。支持 Ollama 和 OpenAI 兼容协议</summary>
    /// <param name="providerConfig">提供商配置</param>
    /// <returns>发现结果描述字符串</returns>
    public async Task<String> DiscoverAsync(ProviderConfig providerConfig)
    {
        String[] models;
        if (providerConfig.Code == "Ollama")
            models = await DiscoverLocalOllamaAsync().ConfigureAwait(false);
        else if (providerConfig.Code == "OllamaCloud")
            models = await DiscoverCloudOllamaAsync().ConfigureAwait(false);
        else
            models = await DiscoverByProviderAsync(providerConfig).ConfigureAwait(false);

        return models.Length == 0
            ? $"{providerConfig.Name} 未发现任何模型"
            : $"{providerConfig.Name} 发现 {models.Length} 个模型：{models.Join("、")}";
    }

    /// <summary>遍历所有已启用提供商并触发模型发现。由后台定时器周期调用</summary>
    public async Task DoDiscoverAsync()
    {
        // 遍历所有已启用的提供商配置，尝试通过 OpenAI（/v1/models）发现模型
        var enabledConfigs = ProviderConfig.FindAllEnabled();
        foreach (var providerConfig in enabledConfigs)
        {
            // Ollama 单独处理
            if (providerConfig.Code == "Ollama" || providerConfig.Code == "OllamaCloud") continue;

            // 只处理 OpenAI 兼容协议（其余协议不支持 /v1/models）
            var protocol = providerConfig.ApiProtocol;
            if (!protocol.IsNullOrEmpty() && protocol != "OpenAI" && protocol != "ChatCompletions") continue;

            if (!providerConfig.Enable || providerConfig.ApiKey.IsNullOrEmpty()) continue;

            // 快速激活窗口：尝试在创建时10分钟内即便未开启也发现
            if (!providerConfig.Enable)
            {
                if (providerConfig.Code == "NewLifeAI" && (DateTime.Now - providerConfig.CreateTime).TotalMinutes < 10)
                {
                    if (providerConfig.ApiKey.IsNullOrEmpty()) providerConfig.ApiKey = "sk-NewLifeAI2026";
                }
                else continue;
            }

            // 配置了 ApiKey 才尝试发现
            if (providerConfig.ApiKey.IsNullOrEmpty()) continue;

            try
            {
                await DiscoverByProviderAsync(providerConfig).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log?.Debug("{0} 模型发现异常：{1}", providerConfig.Name, ex.Message);
            }
        }

        try
        {
            await DiscoverCloudOllamaAsync().ConfigureAwait(false);
            await DiscoverLocalOllamaAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log?.Debug("Ollama 探测异常：{0}", ex.Message);
        }
    }

    /// <summary>探测本地 Ollama 实例并同步模型到数据库</summary>
    private async Task<String[]> DiscoverLocalOllamaAsync()
    {
        var providerConfig = ProviderConfig.FindByCode("Ollama");
        if (providerConfig == null) return [];

        // 未启用时，仅当创建时间不足10分钟才忽略启用开关（快速激活窗口）
        if (!providerConfig.Enable && (DateTime.Now - providerConfig.CreateTime).TotalMinutes >= 10) return [];

        var opts = new AiClientOptions
        {
            Endpoint = providerConfig.Endpoint.IsNullOrEmpty() ? null : providerConfig.Endpoint,
        };
        using var client = new OllamaChatClient(opts);

        // 检查 Ollama 是否在线
        var version = await client.GetVersionAsync().ConfigureAwait(false);
        if (version == null) return [];

        // 如果配置未启用但刚创建，Ollama 在线则自动启用
        if (!providerConfig.Enable)
        {
            providerConfig.Enable = true;
            providerConfig.Save();
            log?.Info("Ollama 服务提供者已自动启用，版本：{0}", version);
        }

        // 获取已安装模型列表
        var tags = await client.ListModelsAsync().ConfigureAwait(false);
        if (tags?.Models == null || tags.Models.Length == 0)
        {
            // Ollama 在线但没有任何模型，自动拉取默认轻量模型
            const String defaultModel = "qwen3.5:0.8b";
            log?.Info("Ollama 尚无模型，开始拉取 {0}（预计需数分钟）...", defaultModel);
            try
            {
                var status = await client.PullModelAsync(defaultModel).ConfigureAwait(false);
                log?.Info("Ollama 拉取 {0} 完成，状态：{1}", defaultModel, status?.Status);
            }
            catch (Exception ex)
            {
                log?.Debug("Ollama 拉取 {0} 失败：{1}", defaultModel, ex.Message);
            }

            // 拉取后重新获取列表
            tags = await client.ListModelsAsync().ConfigureAwait(false);
        }
        if (tags?.Models == null || tags.Models.Length == 0) return [];

        return SyncModelsToConfig(tags, providerConfig, client);
    }

    /// <summary>探测云端 Ollama 并同步模型到数据库</summary>
    private async Task<String[]> DiscoverCloudOllamaAsync()
    {
        var providerConfig = ProviderConfig.FindByCode("OllamaCloud");
        if (providerConfig == null || !providerConfig.Enable || providerConfig.ApiKey.IsNullOrEmpty()) return [];

        var opts = new AiClientOptions
        {
            Endpoint = providerConfig.Endpoint,
            ApiKey = providerConfig.ApiKey,
        };
        using var client = new OllamaChatClient(opts);

        var tags = await client.ListModelsAsync().ConfigureAwait(false);
        if (tags?.Models == null || tags.Models.Length == 0) return [];

        return SyncModelsToConfig(tags, providerConfig, client);
    }

    /// <summary>将 Ollama 模型列表同步到模型配置表</summary>
    /// <param name="tags">Ollama 模型标签列表</param>
    /// <param name="providerConfig">提供商配置</param>
    /// <param name="client">Ollama 客户端，用于推断模型能力</param>
    /// <returns>已处理的模型编码列表</returns>
    private String[] SyncModelsToConfig(OllamaTagsResponse tags, ProviderConfig providerConfig, OllamaChatClient? client = null)
    {
        if (tags.Models == null || tags.Models.Length == 0) return [];

        // 查找 Ollama 描述符，用于已知模型精确匹配
        var descriptor = _registry.GetDescriptor("Ollama");

        var codes = new List<String>();
        var synced = 0;
        foreach (var model in tags.Models)
        {
            if (model.Name == null) continue;

            // 尊重 ModelLimit 设置
            if (providerConfig.ModelLimit > 0 && synced >= providerConfig.ModelLimit) break;

            var modelCode = model.Model;
            codes.Add(modelCode!);
            var config = ModelConfig.FindByProviderIdAndCode(providerConfig.Id, modelCode);
            //if (config != null) continue;

            var name = model.Name.TrimSuffix(":latest");
            if (name.IsNullOrEmpty()) name = model.Details?.Family ?? modelCode;

            var psize = model.Details?.ParameterSize;
            if (!psize.IsNullOrEmpty()) name = $"{name} ({psize})";

            var isNew = config == null;
            config ??= new ModelConfig
            {
                ProviderId = providerConfig.Id,
                Code = modelCode,
                //Name = name,
                Enable = providerConfig.Enable,
            };
            config.Name = name;
            if (model.ModifiedAt > DateTime.MinValue) config.ModelTime = model.ModifiedAt;

            // 推断模型能力：新建模型总是推断；已有模型仅当全未配置时才覆盖（保护用户手动设置）
            if (isNew || (!config.SupportThinking && !config.SupportVision && !config.SupportImage))
            {
                var caps = descriptor?.FindModelCapabilities(modelCode) ?? client?.InferModelCapabilities(modelCode, model.Details);
                if (caps != null)
                {
                    config.SupportThinking = caps.SupportThinking;
                    config.SupportFunction = caps.SupportFunction;
                    config.SupportVision = caps.SupportVision;
                    config.SupportAudio = caps.SupportAudio;
                    config.SupportImage = caps.SupportImage;
                    config.SupportVideo = caps.SupportVideo;
                    config.SupportEmbedding = caps.SupportEmbedding;
                    if (caps.ContextLength > 0) config.ContextLength = caps.ContextLength;
                }
            }

            if (config.Save() > 0)
            {
                synced++;
                log?.Info("同步 {0} 模型：{1}", providerConfig.Name, modelCode);
            }
        }
        return [.. codes];
    }

    /// <summary>通用 OpenAI 兼容模型发现。通过创建 OpenAIChatClient 调用 ListModelsAsync 获取并同步模型列表</summary>
    /// <param name="providerConfig">提供商配置</param>
    /// <returns>已处理的模型编码列表</returns>
    private async Task<String[]> DiscoverByProviderAsync(ProviderConfig providerConfig)
    {
        // 按编码查找描述符，调用描述符所指的工厂创建客户端
        var descriptor = _registry.GetDescriptor(providerConfig.Provider)
            ?? _registry.GetDescriptor(providerConfig.Code);

        var opts = new AiClientOptions
        {
            Endpoint = providerConfig.Endpoint.IsNullOrEmpty() ? descriptor?.DefaultEndpoint : providerConfig.Endpoint,
            ApiKey = providerConfig.ApiKey,
        };

        using var client = descriptor?.Factory(opts) ?? new OpenAIChatClient(opts);
        if (client is not IModelListClient listClient) return [];

        var modelList = await listClient.ListModelsAsync().ConfigureAwait(false);
        if (modelList?.Data == null || modelList.Data.Length == 0) return [];

        // 如果配置未启用但刚创建，发现可用模型则自动启用
        if (!providerConfig.Enable)
        {
            providerConfig.Enable = true;
            providerConfig.Save();
            log?.Info("{0} 服务提供者已自动启用，发现 {1} 个可用模型", providerConfig.Name, modelList.Data.Length);
        }

        return SyncModelsFromList(providerConfig, modelList, descriptor, listClient);
    }

    /// <summary>将 OpenAI 兼容模型列表同步到模型配置表</summary>
    /// <param name="providerConfig">提供商配置</param>
    /// <param name="modelList">远端模型列表</param>
    /// <param name="descriptor">服务商描述符，用于查找已知模型能力</param>
    /// <param name="client">协议客户端，用于按命名规律推断模型能力</param>
    /// <returns>已处理的模型编码列表</returns>
    private String[] SyncModelsFromList(ProviderConfig providerConfig, ModelListResponse modelList, AiClientDescriptor? descriptor = null, IModelListClient? client = null)
    {
        if (modelList.Data == null) return [];

        var models = modelList.Data.AsEnumerable();
        var codes = new List<String>();

        // 按 ModelFilter 过滤：逗号分隔的关键词，任一匹配则保留（大小写不敏感）
        if (!providerConfig.ModelFilter.IsNullOrEmpty())
        {
            var filters = providerConfig.ModelFilter!
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => f.Length > 0)
                .ToArray();
            models = models.Where(m => m.Id != null && filters.Any(f => m.Id!.Contains(f, StringComparison.OrdinalIgnoreCase)));
        }

        // 按 ModelLimit 限制最大发现数量
        var limit = providerConfig.ModelLimit;
        if (limit > 0) models = models.OrderByDescending(e => e.Created).Take(limit);

        foreach (var model in models)
        {
            if (model.Id.IsNullOrEmpty()) continue;

            var config = ModelConfig.FindByProviderIdAndCode(providerConfig.Id, model.Id!);
            //if (config != null) continue;

            codes.Add(model.Id!);
            var isNew = config == null;
            config ??= new ModelConfig
            {
                ProviderId = providerConfig.Id,
                Code = model.Id!,
                //Name = model.Name!,
                Enable = providerConfig.Enable,
            };

            if (!model.Name.IsNullOrEmpty()) config.Name = model.Name;
            if (model.Created > DateTime.MinValue) config.ModelTime = model.Created;

            // 推断模型能力：新建模型总是推断；已有模型仅当全未配置时才覆盖（保护用户手动配置）
            if (isNew || (!config.SupportThinking && !config.SupportVision && !config.SupportImage))
            {
                var caps = descriptor?.FindModelCapabilities(model.Id) ?? (client as OpenAIClientBase)?.InferModelCapabilities(model.Id);
                if (caps != null)
                {
                    config.SupportThinking = caps.SupportThinking;
                    config.SupportFunction = caps.SupportFunction;
                    config.SupportVision = caps.SupportVision;
                    config.SupportAudio = caps.SupportAudio;
                    config.SupportImage = caps.SupportImage;
                    config.SupportVideo = caps.SupportVideo;
                    config.SupportEmbedding = caps.SupportEmbedding;
                    if (caps.ContextLength > 0) config.ContextLength = caps.ContextLength;
                }
            }

            // API 返回的上下文长度优先（如 OpenRouter）
            if (model.ContextLength > 0) config.ContextLength = model.ContextLength;

            if (config.Save() > 0)
                log?.Info("同步 {0} 模型：{1}", providerConfig.Name, model.Id);
        }
        return [.. codes];
    }
    #endregion
}
