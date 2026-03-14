using System.Net;
using System.Runtime.CompilerServices;
using NewLife.AI.Models;
using NewLife.AI.Providers;
using NewLife.ChatAI.Entity;
using NewLife.Log;

namespace NewLife.ChatAI.Services;

/// <summary>API 网关服务。按 model 字段路由到对应的模型提供商，支持认证校验和限流重试</summary>
/// <remarks>实例化网关服务</remarks>
/// <param name="usageService">用量统计服务</param>
/// <param name="log">日志</param>
public class GatewayService(UsageService? usageService, IServiceProvider serviceProvider, ILog log)
{
    #region 属性
    private readonly AiProviderFactory _providerFactory = AiProviderFactory.Default;

    /// <summary>上游重试最大次数</summary>
    private const Int32 MaxRetryCount = 5;

    /// <summary>重试最大等待时间（秒）</summary>
    private const Int32 MaxRetryDelaySec = 30;
    #endregion

    #region 认证
    /// <summary>校验 AppKey 并返回对应实体</summary>
    /// <param name="authorization">Authorization 头的值，格式为 Bearer sk-xxx</param>
    /// <returns>有效的 AppKey 实体，无效时返回 null</returns>
    public AppKey? ValidateAppKey(String? authorization)
    {
        if (String.IsNullOrWhiteSpace(authorization)) return null;

        // 解析 Bearer Token
        var secret = authorization;
        if (secret.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            secret = secret.Substring(7).Trim();

        if (String.IsNullOrWhiteSpace(secret)) return null;

        var appKey = AppKey.FindBySecret(secret);
        if (appKey == null) return null;

        // 检查启用状态
        if (!appKey.Enable) return null;

        // 检查过期时间
        if (appKey.ExpireTime.Year > 2000 && appKey.ExpireTime < DateTime.Now) return null;

        return appKey;
    }
    #endregion

    #region 模型路由
    /// <summary>根据模型编号查找模型配置</summary>
    /// <param name="modelId">模型编号</param>
    /// <returns>模型配置，未找到返回 null</returns>
    public ModelConfig? ResolveModel(Int32 modelId)
    {
        if (modelId <= 0) return null;

        var config = ModelConfig.FindById(modelId);
        if (config == null || !config.Enable) return null;

        return config;
    }

    /// <summary>根据模型编码查找模型配置（网关场景，按Code匹配第一个启用的模型）</summary>
    /// <param name="modelCode">模型编码</param>
    /// <returns>模型配置，未找到返回 null</returns>
    public ModelConfig? ResolveModelByCode(String? modelCode)
    {
        if (String.IsNullOrWhiteSpace(modelCode)) return null;

        var list = ModelConfig.FindAll();
        var config = list.FirstOrDefault(e => e.Code.EqualIgnoreCase(modelCode) && e.Enable);

        return config;
    }

    /// <summary>根据模型配置获取对应的 AI 服务商实例。按 ProviderConfig.Id 缓存，配置变更后自动重建</summary>
    /// <param name="config">模型配置</param>
    /// <returns>服务商实例，未找到返回 null</returns>
    public IAiProvider? GetProvider(ModelConfig config)
    {
        if (config == null) return null;

        var providerConfig = config.ProviderInfo;
        if (providerConfig == null) return null;

        return _providerFactory.GetProviderForConfig(providerConfig.Id, providerConfig.Provider, serviceProvider);
    }

    /// <summary>构建服务商连接选项。从关联的 ProviderConfig 获取 Endpoint/ApiKey</summary>
    /// <param name="config">模型配置</param>
    /// <returns></returns>
    public static AiProviderOptions BuildOptions(ModelConfig config)
    {
        return new AiProviderOptions
        {
            Endpoint = config.GetEffectiveEndpoint(),
            ApiKey = config.GetEffectiveApiKey(),
        };
    }
    #endregion

    #region 请求转发
    /// <summary>非流式对话转发。支持上游 429 限流重试</summary>
    /// <param name="request">对话请求</param>
    /// <param name="config">模型配置</param>
    /// <param name="appKey">应用密钥（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<ChatCompletionResponse> ChatAsync(ChatCompletionRequest request, ModelConfig config, AppKey? appKey, CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(config);
        if (provider == null)
            throw new InvalidOperationException($"未找到服务商，模型 '{config.Code}' 关联的提供商类型 '{config.ProviderInfo?.Provider}' 未注册");

        var options = BuildOptions(config);

        using var client = provider.CreateClient(options);
        ChatCompletionResponse? response = null;
        for (var i = 0; i <= MaxRetryCount; i++)
        {
            try
            {
                response = await client.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (HttpRequestException ex) when (Is429(ex) && i < MaxRetryCount)
            {
                var delay = GetRetryDelay(i);
                log?.Info("上游限流 429，第 {0} 次重试，等待 {1}ms", i + 1, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        if (response == null)
            throw new InvalidOperationException("上游服务限流，重试次数已耗尽");

        // 更新 AppKey 统计
        UpdateAppKeyUsage(appKey, response.Usage);

        // 写入用量记录
        RecordUsage(appKey, config.Id, response.Usage);

        return response;
    }

    /// <summary>流式对话转发。支持上游 429 限流重试</summary>
    /// <param name="request">对话请求</param>
    /// <param name="config">模型配置</param>
    /// <param name="appKey">应用密钥（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async IAsyncEnumerable<ChatCompletionResponse> ChatStreamAsync(ChatCompletionRequest request, ModelConfig config, AppKey? appKey, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(config);
        if (provider == null)
            throw new InvalidOperationException($"未找到服务商，模型 '{config.Code}' 关联的提供商类型 '{config.ProviderInfo?.Provider}' 未注册");

        var options = BuildOptions(config);

        using var streamClient = provider.CreateClient(options);
        IAsyncEnumerable<ChatCompletionResponse>? stream = null;
        for (var i = 0; i <= MaxRetryCount; i++)
        {
            try
            {
                stream = streamClient.CompleteStreamingAsync(request, cancellationToken);
                break;
            }
            catch (HttpRequestException ex) when (Is429(ex) && i < MaxRetryCount)
            {
                var delay = GetRetryDelay(i);
                log?.Info("上游限流 429，第 {0} 次重试，等待 {1}ms", i + 1, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        if (stream == null)
            throw new InvalidOperationException("上游服务限流，重试次数已耗尽");

        ChatUsage? lastUsage = null;
        await foreach (var chunk in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Usage != null) lastUsage = chunk.Usage;
            yield return chunk;
        }

        // 更新 AppKey 统计
        UpdateAppKeyUsage(appKey, lastUsage);

        // 写入用量记录
        RecordUsage(appKey, config.Id, lastUsage);
    }
    #endregion

    #region 辅助
    /// <summary>判断异常是否为 HTTP 429 限流</summary>
    /// <param name="ex">HTTP 请求异常</param>
    /// <returns></returns>
    public static Boolean Is429(HttpRequestException ex)
    {
        // HttpRequestException.StatusCode 在 .NET 5+ 可用
        if (ex.StatusCode == HttpStatusCode.TooManyRequests) return true;

        // 兼容回退：检查异常消息中是否包含 429
        return ex.Message.Contains("429");
    }

    /// <summary>计算指数退避延迟（含随机抖动）</summary>
    /// <param name="retryIndex">重试序号（从0开始）</param>
    /// <returns>延迟毫秒数</returns>
    public static Int32 GetRetryDelay(Int32 retryIndex)
    {
        // 基础延迟：1s, 2s, 4s, 8s, 16s...
        var baseDelay = (Int32)Math.Pow(2, retryIndex) * 1000;
        if (baseDelay > MaxRetryDelaySec * 1000) baseDelay = MaxRetryDelaySec * 1000;

        // 随机抖动 0~250ms
        var jitter = Random.Shared.Next(0, 251);
        return baseDelay + jitter;
    }

    /// <summary>写入用量记录到 UsageRecord 表</summary>
    /// <param name="appKey">应用密钥</param>
    /// <param name="modelId">模型编号</param>
    /// <param name="usage">用量统计</param>
    private void RecordUsage(AppKey? appKey, Int32 modelId, ChatUsage? usage)
    {
        if (usage == null) return;

        usageService?.Record(
            appKey?.UserId ?? 0,
            appKey?.Id ?? 0,
            0, 0,
            modelId,
            usage.PromptTokens,
            usage.CompletionTokens,
            usage.TotalTokens,
            "Gateway");
    }

    /// <summary>更新 AppKey 的调用次数和 Token 用量</summary>
    /// <param name="appKey">应用密钥</param>
    /// <param name="usage">用量统计</param>
    private void UpdateAppKeyUsage(AppKey? appKey, ChatUsage? usage)
    {
        if (appKey == null) return;

        appKey.Calls++;
        appKey.LastCallTime = DateTime.Now;

        if (usage != null)
            appKey.TotalTokens += usage.TotalTokens;

        try
        {
            appKey.Update();
        }
        catch (Exception ex)
        {
            log?.Error("更新 AppKey 用量失败: {0}", ex.Message);
        }
    }
    #endregion
}
