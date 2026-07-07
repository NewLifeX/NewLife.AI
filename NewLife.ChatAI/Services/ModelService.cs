using NewLife.AI.Clients;
using NewLife.AI.Clients.Ollama;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Embedding;
using NewLife.AI.Interfaces;
using NewLife.Log;
using NewLife.Serialization;
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

    /// <summary>获取所有公开可用的模型列表。不做 AppKey 权限过滤，仅保留启用且提供商可用的模型</summary>
    /// <returns>所有公开模型列表，按排序降序、编号降序排列</returns>
    public IList<ModelConfig> GetAllPublicModels() => ModelConfig.FindAllEnabled();

    /// <summary>对模型列表做关键字和能力二次过滤</summary>
    /// <param name="models">待过滤的模型列表</param>
    /// <param name="keyword">关键字，匹配模型的 Code 或 Name（忽略大小写子串匹配）</param>
    /// <param name="capabilities">逗号分隔的能力枚举，如 vision,function。要求模型同时具备所列全部能力</param>
    /// <param name="supportThinking">支持思考</param>
    /// <param name="supportFunction">支持函数调用</param>
    /// <param name="supportVision">支持视觉</param>
    /// <param name="supportAudio">支持音频</param>
    /// <param name="supportSpeech">支持语音合成</param>
    /// <param name="supportImage">支持图像生成</param>
    /// <param name="supportVideo">支持视频生成</param>
    /// <param name="supportEmbedding">支持嵌入向量</param>
    /// <param name="supportRerank">支持重排序</param>
    /// <returns>过滤后的模型列表</returns>
    public IList<ModelConfig> FilterModels(IList<ModelConfig> models, String? keyword, String? capabilities,
        Boolean? supportThinking, Boolean? supportFunction, Boolean? supportVision, Boolean? supportAudio,
        Boolean? supportSpeech, Boolean? supportImage, Boolean? supportVideo, Boolean? supportEmbedding, Boolean? supportRerank)
    {
        if (models.Count == 0) return models;

        // 解析能力枚举
        HashSet<String> capSet = [];
        if (!capabilities.IsNullOrEmpty())
        {
            foreach (var item in capabilities!.Split([',', '，', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
            {
                var v = item.Trim();
                if (v.Length > 0) capSet.Add(v.ToLower());
            }
        }

        var query = models.AsEnumerable();

        // 关键字过滤
        if (!keyword.IsNullOrEmpty())
        {
            var kw = keyword!;
            query = query.Where(e =>
                (!e.Code.IsNullOrEmpty() && e.Code.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                (!e.Name.IsNullOrEmpty() && e.Name.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }

        // 能力枚举过滤（AND 逻辑：模型必须同时具备所有指定能力）
        foreach (var cap in capSet)
        {
            query = cap switch
            {
                "chat" => query.Where(e => e.IsChatModel),
                "thinking" => query.Where(e => e.SupportThinking),
                "function" => query.Where(e => e.SupportFunction),
                "vision" => query.Where(e => e.SupportVision),
                "audio" => query.Where(e => e.SupportAudio),
                "speech" => query.Where(e => e.SupportSpeech),
                "image" => query.Where(e => e.SupportImage),
                "video" => query.Where(e => e.SupportVideo),
                "embedding" => query.Where(e => e.SupportEmbedding),
                "rerank" => query.Where(e => e.SupportRerank),
                _ => query,
            };
        }

        // OpenAI 风格独立能力过滤（与能力枚举叠加，AND 逻辑）
        if (supportThinking.HasValue) query = query.Where(e => e.SupportThinking == supportThinking.Value);
        if (supportFunction.HasValue) query = query.Where(e => e.SupportFunction == supportFunction.Value);
        if (supportVision.HasValue) query = query.Where(e => e.SupportVision == supportVision.Value);
        if (supportAudio.HasValue) query = query.Where(e => e.SupportAudio == supportAudio.Value);
        if (supportSpeech.HasValue) query = query.Where(e => e.SupportSpeech == supportSpeech.Value);
        if (supportImage.HasValue) query = query.Where(e => e.SupportImage == supportImage.Value);
        if (supportVideo.HasValue) query = query.Where(e => e.SupportVideo == supportVideo.Value);
        if (supportEmbedding.HasValue) query = query.Where(e => e.SupportEmbedding == supportEmbedding.Value);
        if (supportRerank.HasValue) query = query.Where(e => e.SupportRerank == supportRerank.Value);

        return query.ToList();
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

    /// <summary>解析重排序模型配置。优先按 ChatSetting.RerankModel 编码查找；未配置则返回 null（调用方跳过 CrossEncoder 重排步骤）</summary>
    /// <returns>重排序模型配置，未配置或模型不存在返回 null</returns>
    public ModelConfig? GetRerankModel()
    {
        if (chatSetting.RerankModel.IsNullOrEmpty()) return null;

        var config = ModelConfig.FindByCode(chatSetting.RerankModel);
        if (config != null && config.Enable) return config;

        return null;
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
            Organization = providerConfig?.Organization,
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

    /// <summary>构建嵌入请求并应用模型定制设置</summary>
    /// <param name="model">嵌入模型配置</param>
    /// <param name="input">嵌入文本</param>
    /// <returns>嵌入请求</returns>
    private static EmbeddingRequest BuildEmbeddingRequest(ModelConfig model, IList<String> input)
    {
        var req = new EmbeddingRequest { Input = input };

        // 应用模型定制设置
        if (!model.Settings.IsNullOrEmpty())
        {
            try
            {
                var settings = model.Settings.ToJsonEntity<EmbeddingModelSetting>();
                if (settings != null)
                {
                    if (settings.EncodingFormat != null) req.EncodingFormat = settings.EncodingFormat;
                    if (settings.Dimensions != null) req.Dimensions = settings.Dimensions;

                    // Items 中的额外参数通过 IExtend 传递
                    if (settings.Items is { Count: > 0 })
                    {
                        foreach (var kv in settings.Items)
                        {
                            if (kv.Value != null)
                                req.Items[kv.Key] = kv.Value;
                        }
                    }
                }
            }
            catch
            {
                // 解析失败时忽略，使用默认请求
            }
        }

        return req;
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

        var req = BuildEmbeddingRequest(model, [text]);
        var response = await client.GenerateAsync(req, cancellationToken).ConfigureAwait(false);

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
            var req = BuildEmbeddingRequest(model, batch);
            var response = await client.GenerateAsync(req, cancellationToken).ConfigureAwait(false);
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

    /// <summary>遍历所有 ModelConfig 记录，从 AiClientDescriptor 刷新模型能力（含 ReasoningEfforts）</summary>
    /// <remarks>
    /// 启动时由 DataPreloadService 调用一次，确保所有模型的能力字段与最新的 [AiClientModel] 注解保持一致。
    /// <para>保护策略：仅当模型全部能力标记均为默认值（false/空）时才覆盖，若管理员已手动配置过任何能力字段则跳过，避免覆盖人工调整。</para>
    /// </remarks>
    public async Task RefreshModelCapabilitiesAsync()
    {
        var allModels = ModelConfig.FindAll();
        var count = 0;

        foreach (var model in allModels)
        {
            try
            {
                var provider = model.ProviderInfo;
                if (provider == null || provider.Provider.IsNullOrEmpty()) continue;

                var descriptor = _registry.GetDescriptor(provider.Provider);
                if (descriptor == null) continue;

                var caps = descriptor.FindModelCapabilities(model.Code);

                // 未注册的模型通过 InferModelCapabilities 模式匹配推断（如 cosyvoice-v3.5-*、qwen-tts-* 变体）
                if (caps == null && model.Code != null)
                {
                    using var client = descriptor.Factory(new AiClientOptions { Endpoint = "" });
                    caps = (client as OpenAIClientBase)?.InferModelCapabilities(model.Code);
                }
                if (caps == null) continue;

                // 保护策略：仅当模型未锁定时覆盖（管理员手动保存后自动锁定，禁止自动覆盖）
                if (!model.Locked)
                {
                    model.SupportThinking = caps.SupportThinking;
                    model.SupportFunction = caps.SupportFunction;
                    model.SupportVision = caps.SupportVision;
                    model.SupportAudio = caps.SupportAudio;
                    model.SupportSpeech = caps.SupportSpeech;
                    model.SupportImage = caps.SupportImage;
                    model.SupportVideo = caps.SupportVideo;
                    model.SupportEmbedding = caps.SupportEmbedding;
                    model.SupportRerank = caps.SupportRerank;
                    if (caps.ContextLength > 0) model.ContextLength = caps.ContextLength;
                }

                // ReasoningEfforts 是新增字段，当前 DB 中必然为 null，安全写入
                if (model.ReasoningEfforts.IsNullOrEmpty() && !caps.ReasoningEfforts.IsNullOrEmpty())
                    model.ReasoningEfforts = caps.ReasoningEfforts;

                // 嵌入向量模型且 Settings 为空时，自动写入默认设置项（保留 null 字段，使管理员看到可配置项）
                if (model.SupportEmbedding && model.Settings.IsNullOrEmpty())
                {
                    var defaultSettings = new EmbeddingModelSetting();
                    model.Settings = defaultSettings.ToJson(false, false, false);
                }

                count += model.Save();
            }
            catch (Exception ex)
            {
                log?.Debug("刷新模型能力 {0}/{1} 失败：{2}", model.ProviderId, model.Code, ex.Message);
            }
        }

        if (count > 0)
            XTrace.WriteLine("刷新模型能力完成，更新 {0} 个模型配置", count);
    }

    /// <summary>初始化指定提供商的所有模型。设置未锁定模型的能力、价格和默认 Settings</summary>
    /// <param name="providerConfig">提供商配置</param>
    /// <returns>初始化结果描述</returns>
    public async Task<String> InitModelsByProviderAsync(ProviderConfig providerConfig)
    {
        if (providerConfig == null) return "提供商配置为空";

        var descriptor = _registry.GetDescriptor(providerConfig.Provider)
            ?? _registry.GetDescriptor(providerConfig.Code);
        if (descriptor == null) return $"未找到服务商 '{providerConfig.Provider}' 的描述符";

        var models = ModelConfig.FindAllByProviderId(providerConfig.Id);
        if (models == null || models.Count == 0) return $"提供商 '{providerConfig.Name}' 下没有模型";

        var updated = 0;
        foreach (var model in models)
        {
            var modelInfo = descriptor.FindModelInfo(model.Code);
            var caps = modelInfo?.Capabilities ?? descriptor.FindModelCapabilities(model.Code);
            if (caps == null) continue;

            model.Name = modelInfo?.DisplayName ?? model.Name;

            // 未锁定时更新能力
            if (!model.Locked)
            {
                model.SupportThinking = caps.SupportThinking;
                model.SupportFunction = caps.SupportFunction;
                model.SupportVision = caps.SupportVision;
                model.SupportAudio = caps.SupportAudio;
                model.SupportSpeech = caps.SupportSpeech;
                model.SupportImage = caps.SupportImage;
                model.SupportVideo = caps.SupportVideo;
                model.SupportEmbedding = caps.SupportEmbedding;
                model.SupportRerank = caps.SupportRerank;
                if (caps.ContextLength > 0) model.ContextLength = caps.ContextLength;
            }

            // 价格初始化（StarChat 专属）
#if STARCHAT
            if (modelInfo?.Pricing != null)
            {
                var pricing = modelInfo.Pricing;
                model.PricingMode = NewLife.AI.Models.PricingMode.Token;
                model.InputPrice = pricing.InputPrice;
                model.OutputPrice = pricing.OutputPrice;
                model.CachedInputPrice = pricing.CachedInputPrice > 0 ? pricing.CachedInputPrice : 0;
                model.CacheCreationPrice = pricing.CacheCreationPrice > 0 ? pricing.CacheCreationPrice : 0;
                // 嵌入/重排序等非对话模型使用 UnitPrice 承载单价
                if (caps.SupportEmbedding)
                    model.PricingMode = NewLife.AI.Models.PricingMode.Embedding;
            }
#endif

            // 嵌入向量模型且 Settings 为空时自动初始化
            if (caps.SupportEmbedding && model.Settings.IsNullOrEmpty())
            {
                var defaultSettings = new EmbeddingModelSetting();
                model.Settings = defaultSettings.ToJson(false, false, false);
            }

            if (model.Save() > 0) updated++;
        }

        return $"初始化完成，已更新 {updated}/{models.Count} 个模型";
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

                // 嵌入向量模型且 Settings 为空时，自动写入默认设置项
                if (config.SupportEmbedding && config.Settings.IsNullOrEmpty())
                {
                    var defaultSettings = new EmbeddingModelSetting();
                    config.Settings = defaultSettings.ToJson(false, false, false);
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

            // 新建模型且名称为空时，优先从已注册列表取显示名，其次按命名规律（连字符各段首字母大写）推断
            if (isNew && config.Name.IsNullOrEmpty())
                config.Name = descriptor?.FindModelInfo(model.Id)?.DisplayName
                    ?? (client as OpenAIClientBase)?.InferModelDisplayName(model.Id);

            if (model.Created > DateTime.MinValue) config.ModelTime = model.Created;

            // 推断模型能力：新建模型总是推断；已有模型仅当未锁定时才覆盖
            if (isNew || !config.Locked)
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
                    if (config.ReasoningEfforts.IsNullOrEmpty())
                        config.ReasoningEfforts = caps.ReasoningEfforts;
                    if (caps.ContextLength > 0) config.ContextLength = caps.ContextLength;
                }

                // 嵌入向量模型且 Settings 为空时，自动写入默认设置项
                if (config.SupportEmbedding && config.Settings.IsNullOrEmpty())
                {
                    var defaultSettings = new EmbeddingModelSetting();
                    config.Settings = defaultSettings.ToJson(false, false, false);
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
