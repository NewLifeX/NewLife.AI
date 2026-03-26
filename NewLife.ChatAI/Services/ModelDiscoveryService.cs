using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.Log;
using NewLife.ChatAI.Entity;
using NewLife.Threading;

namespace NewLife.ChatAI.Services;

/// <summary>模型发现服务。定期探测本地 Ollama 实例并同步模型列表</summary>
/// <remarks>实例化模型发现服务</remarks>
/// <param name="log">日志</param>
public class ModelDiscoveryService(ILog log) : IHostedService
{
    private TimerX? _timer;

    /// <summary>启动服务</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 首次延迟 30 秒执行，之后每 10 分钟探测一次
        _timer = new TimerX(DoDiscover, null, 10_000, 10 * 60 * 1000) { Async = true };

        return Task.CompletedTask;
    }

    /// <summary>停止服务</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();
        _timer = null;

        return Task.CompletedTask;
    }

    private async Task DoDiscover(Object? state)
    {
        // 遍历所有已启用的提供商配置，尝试通过 OpenAI（/v1/models）发现模型
        var enabledConfigs = ProviderConfig.FindAllEnabled();
        foreach (var providerConfig in enabledConfigs)
        {
            // Ollama 单独处理
            if (providerConfig.Code == "Ollama" || providerConfig.Code == "OllamaCloud") continue;

            // 只处理 OpenAI 兆容协议（其余协议不支持 /v1/models）
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
    private async Task DiscoverLocalOllamaAsync()
    {
        var providerConfig = ProviderConfig.FindByCode("Ollama");
        if (providerConfig == null) return;

        // 未启用时，仅当创建时间不足10分钟才忽略启用开关（快速激活窗口）
        if (!providerConfig.Enable && (DateTime.Now - providerConfig.CreateTime).TotalMinutes >= 10) return;

        var opts = new AiClientOptions
        {
            Endpoint = providerConfig.Endpoint.IsNullOrEmpty() ? null : providerConfig.Endpoint,
        };
        using var client = new OllamaChatClient(opts);

        // 检查 Ollama 是否在线
        var version = await client.GetVersionAsync().ConfigureAwait(false);
        if (version == null) return;

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
        if (tags?.Models == null || tags.Models.Length == 0) return;

        SyncModelsToConfig(tags, providerConfig);
    }

    /// <summary>探测云端 Ollama 并同步模型到数据库</summary>
    private async Task DiscoverCloudOllamaAsync()
    {
        var providerConfig = ProviderConfig.FindByCode("OllamaCloud");
        if (providerConfig == null || !providerConfig.Enable || providerConfig.ApiKey.IsNullOrEmpty()) return;

        var opts = new AiClientOptions
        {
            Endpoint = providerConfig.Endpoint,
            ApiKey = providerConfig.ApiKey,
        };
        using var client = new OllamaChatClient(opts);

        var tags = await client.ListModelsAsync().ConfigureAwait(false);
        if (tags?.Models == null || tags.Models.Length == 0) return;

        SyncModelsToConfig(tags, providerConfig);
    }

    /// <summary>将 Ollama 模型同步到提供商配置和模型配置</summary>
    /// <param name="tags">Ollama 模型标签列表</param>
    /// <param name="providerConfig">提供商配置</param>
    private void SyncModelsToConfig(OllamaTagsResponse tags, ProviderConfig providerConfig)
    {
        if (tags.Models == null || tags.Models.Length == 0) return;

        // 同步每个模型
        var synced = 0;
        foreach (var model in tags.Models)
        {
            if (model.Name == null) continue;

            // 尊重 ModelLimit 设置
            if (providerConfig.ModelLimit > 0 && synced >= providerConfig.ModelLimit) break;

            var modelCode = model.Model;
            var config = ModelConfig.FindByProviderIdAndCode(providerConfig.Id, modelCode);
            //if (config != null) continue;

            var name = model.Name.TrimEnd(":latest");
            if (name.IsNullOrEmpty()) name = model.Details?.Family ?? modelCode;

            var psize = model.Details?.ParameterSize;
            if (!psize.IsNullOrEmpty()) name = $"{name} ({psize})";

            config ??= new ModelConfig
            {
                ProviderId = providerConfig.Id,
                Code = modelCode,
                //Name = name,
                Enable = providerConfig.Enable,
                SupportFunctionCalling = true,
            };
            config.Name = name;
            if (model.ModifiedAt > DateTime.MinValue) config.ModelTime = model.ModifiedAt;

            if (config.Save() > 0)
            {
                synced++;
                log?.Info("同步 {0} 模型：{1}", providerConfig.Name, modelCode);
            }
        }
    }

    /// <summary>通用 OpenAI 兼容模型发现。通过创建 OpenAiChatClient 调用 ListModelsAsync 获取并同步模型列表</summary>
    /// <param name="providerConfig">提供商配置</param>
    private async Task DiscoverByProviderAsync(ProviderConfig providerConfig)
    {
        // 按编码查找描述符，调用描述符点指的工厕创建客户端
        var descriptor = AiClientRegistry.Default.GetDescriptor(providerConfig.Provider)
            ?? AiClientRegistry.Default.GetDescriptor(providerConfig.Code);

        var opts = new AiClientOptions
        {
            Endpoint = providerConfig.Endpoint.IsNullOrEmpty() ? descriptor?.DefaultEndpoint : providerConfig.Endpoint,
            ApiKey = providerConfig.ApiKey,
        };

        using var client = descriptor?.Factory(opts) ?? new OpenAIChatClient(opts);
        if (client is not OpenAIChatClient openAiClient) return;

        var modelList = await openAiClient.ListModelsAsync().ConfigureAwait(false);
        if (modelList?.Data == null || modelList.Data.Length == 0) return;

        // 如果配置未启用但刚创建，发现可用模型则自动启用
        if (!providerConfig.Enable)
        {
            providerConfig.Enable = true;
            providerConfig.Save();
            log?.Info("{0} 服务提供者已自动启用，发现 {1} 个可用模型", providerConfig.Name, modelList.Data.Length);
        }

        SyncModelsFromList(providerConfig, modelList);
    }

    /// <summary>将 OpenAI 兼容模型列表同步到模型配置表</summary>
    /// <param name="providerConfig">提供商配置</param>
    /// <param name="modelList">远端模型列表</param>
    private void SyncModelsFromList(ProviderConfig providerConfig, OpenAiModelListResponse modelList)
    {
        if (modelList.Data == null) return;

        var models = modelList.Data.AsEnumerable();

        // 按 ModelFilter 过滤：逗号分隔的关键词，任一匹配则保留（大小写不敏感）
        if (!providerConfig.ModelFilter.IsNullOrEmpty())
        {
            var filters = providerConfig.ModelFilter!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            models = models.Where(m => m.Id != null && filters.Any(f => m.Id.Contains(f, StringComparison.OrdinalIgnoreCase)));
        }

        // 按 ModelLimit 限制最大发现数量
        var limit = providerConfig.ModelLimit;
        if (limit > 0) models = models.OrderByDescending(e => e.Created).Take(limit);

        foreach (var model in models)
        {
            if (model.Id.IsNullOrEmpty()) continue;

            var config = ModelConfig.FindByProviderIdAndCode(providerConfig.Id, model.Id!);
            //if (config != null) continue;

            config ??= new ModelConfig
            {
                ProviderId = providerConfig.Id,
                Code = model.Id!,
                //Name = model.Name!,
                Enable = providerConfig.Enable,
                SupportFunctionCalling = true,
            };

            if (!model.Name.IsNullOrEmpty()) config.Name = model.Name;
            if (model.Created > DateTime.MinValue) config.ModelTime = model.Created;

            if (config.Save() > 0)
                log?.Info("同步 {0} 模型：{1}", providerConfig.Name, model.Id);
        }
    }
}