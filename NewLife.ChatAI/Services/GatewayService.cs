using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Filters;
using NewLife.Collections;
using NewLife.Serialization;
using ILog = NewLife.Log.ILog;

namespace NewLife.ChatAI.Services;

/// <summary>网关响应协议格式枚举</summary>
public enum GatewayProtocol
{
    /// <summary>OpenAI Chat Completions / Response API 协议</summary>
    OpenAI,

    /// <summary>Anthropic Messages API 协议</summary>
    Anthropic,

    /// <summary>Google Gemini API 协议</summary>
    Gemini,
}

/// <summary>API 网关服务。按 model 字段路由到对应的模型提供商，支持认证校验和限流重试</summary>
/// <remarks>实例化网关服务</remarks>
/// <param name="usageService">用量统计服务</param>
/// <param name="modelService">模型服务。统一负责模型可用性判断与 IChatClient 创建</param>
/// <param name="chatFilters">对话过滤器链（日志、监控等横切关注点；ConversationId=0 时过滤器应 graceful no-op）</param>
/// <param name="chatSetting">对话配置</param>
/// <param name="log">日志</param>
public class GatewayService(UsageService usageService, ModelService modelService, IEnumerable<IChatFilter>? chatFilters, ChatSetting chatSetting, ILog log)
{
    #region 属性
    /// <summary>对话过滤器链（日志、监控等横切关注点），由 DI 解析</summary>
    private readonly IReadOnlyList<IChatFilter> _chatFilters = chatFilters?.ToArray() ?? [];

    /// <summary>重试最大等待时间（秒）</summary>
    private const Int32 MaxRetryDelaySec = 30;

    /// <summary>snake_case 序列化选项。用于写出符合 OpenAI / Anthropic 协议的响应体</summary>
    public static readonly JsonSerializerOptions SnakeCaseOptions;

    /// <summary>camelCase 序列化选项。用于写出符合 Gemini 协议的响应体</summary>
    public static readonly JsonSerializerOptions CamelCaseOptions;
    #endregion

    #region 构造
    static GatewayService()
    {
        var snake = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        SystemJson.Apply(snake, true);
        SnakeCaseOptions = snake;

        var camel = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        SystemJson.Apply(camel, true);
        CamelCaseOptions = camel;
    }
    #endregion

    #region 认证
    /// <summary>校验配额是否允许调用。超限时抛出 QuotaExceededException；通过时返回软警告信息（null 表示无警告）。
    /// 基类不做任何检查（ChatAI 无配额功能），由 StarChat GatewayService2 重写实现商用配额逻辑</summary>
    /// <param name="appKey">应用密钥</param>
    /// <returns>软警告信息，用于写入 X-RateLimit-Warning 响应头；null 表示无警告</returns>
    public virtual String? ValidateQuota(AppKey? appKey) => null;

    /// <summary>校验 AppKey 并返回对应实体</summary>
    /// <param name="authorization">Authorization 头的值，格式为 Bearer sk-xxx</param>
    /// <returns>有效的 AppKey 实体，无效时返回 null</returns>
    public AppKey? ValidateAppKey(String? authorization)
    {
        if (String.IsNullOrWhiteSpace(authorization)) return null;

        // 解析 Bearer Token
        var secret = authorization;
        if (secret.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            secret = secret[7..].Trim();

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

    #region 消息构建
    /// <summary>为网关请求构建上下文消息列表。注入系统提示词（用户信息+UserSetting+ModelConfig），过滤请求中原有系统消息</summary>
    /// <param name="request">网关请求</param>
    /// <param name="appKey">应用密钥</param>
    /// <param name="config">模型配置</param>
    /// <returns>上下文消息列表</returns>
    public IList<AiChatMessage> BuildContextMessages(IChatRequest request, AppKey appKey, ModelConfig config)
    {
        var messages = new List<AiChatMessage>();

        // 构建系统消息（包含用户信息 + UserSetting + ModelConfig SystemPrompt）
        var sysMsg = MessageFlow.BuildSystemMessage(appKey.UserId, config);
        if (sysMsg != null) messages.Add(sysMsg);

        // 添加请求中的对话消息（跳过系统消息，已由管道注入）
        foreach (var msg in request.Messages ?? [])
        {
            if (msg.Role?.Equals("system", StringComparison.OrdinalIgnoreCase) == true) continue;
            messages.Add(msg);
        }

        return messages;
    }
    #endregion

    #region 请求转发
    /// <summary>非流式对话转发。支持上游 429 限流重试</summary>
    /// <param name="request">对话请求</param>
    /// <param name="model">模型配置</param>
    /// <param name="appKey">应用密钥（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<ChatResponse> ChatAsync(IChatRequest request, ModelConfig model, AppKey? appKey, CancellationToken cancellationToken = default)
    {
        using var rawClient = modelService.CreateClient(model);
        if (rawClient == null)
            throw new InvalidOperationException($"未找到服务商，模型 '{model.Code}' 关联的提供商类型 '{model.ProviderInfo?.Provider}' 未注册");

        // 应用 IChatFilter 链（通用横切：日志、监控等；网关场景 ConversationId=0，filter 实现需自行处理）
        var clientBuilder = rawClient.AsBuilder();
        foreach (var f in _chatFilters)
            clientBuilder = clientBuilder.UseFilters(f);
        using var client = clientBuilder.Build();

        ChatResponse? response = null;
        var maxRetry = chatSetting.UpstreamRetryCount;
        for (var i = 0; i <= maxRetry; i++)
        {
            try
            {
                response = ChatResponse.From(await client.GetResponseAsync(request, cancellationToken).ConfigureAwait(false));
                break;
            }
            catch (HttpRequestException ex) when (Is429(ex) && i < maxRetry)
            {
                var delay = GetRetryDelay(i);
                log?.Info("上游限流 429，第 {0} 次重试，等待 {1}ms", i + 1, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        if (response == null)
            throw new InvalidOperationException("上游服务限流，重试次数已耗尽");

        // 写入用量记录（内部完成费用计算 + 配额累加）
        RecordUsage(appKey, model, request.ConversationId.ToLong(), response.Usage);

        return response;
    }

    /// <summary>流式对话转发。支持上游 429 限流重试</summary>
    /// <param name="request">对话请求</param>
    /// <param name="config">模型配置</param>
    /// <param name="appKey">应用密钥（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async IAsyncEnumerable<ChatResponse> ChatStreamAsync(IChatRequest request, ModelConfig config, AppKey? appKey, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var rawStreamClient = modelService.CreateClient(config);
        if (rawStreamClient == null)
            throw new InvalidOperationException($"未找到服务商，模型 '{config.Code}' 关联的提供商类型 '{config.ProviderInfo?.Provider}' 未注册");

        // 应用 IChatFilter 链
        var streamBuilder = rawStreamClient.AsBuilder();
        foreach (var f in _chatFilters)
            streamBuilder = streamBuilder.UseFilters(f);
        using var streamClient = streamBuilder.Build();

        IAsyncEnumerable<IChatResponse>? stream = null;
        var maxRetry = chatSetting.UpstreamRetryCount;
        for (var i = 0; i <= maxRetry; i++)
        {
            try
            {
                stream = streamClient.GetStreamingResponseAsync(request, cancellationToken);
                break;
            }
            catch (HttpRequestException ex) when (Is429(ex) && i < maxRetry)
            {
                var delay = GetRetryDelay(i);
                log?.Info("上游限流 429，第 {0} 次重试，等待 {1}ms", i + 1, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        if (stream == null)
            throw new InvalidOperationException("上游服务限流，重试次数已耗尽");

        UsageDetails? lastUsage = null;
        await foreach (var rawChunk in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var chunk = ChatResponse.From(rawChunk);
            if (chunk.Usage != null) lastUsage = chunk.Usage;
            yield return chunk;
        }

        // 写入用量记录（费用与配额累加一起完成）
        RecordUsage(appKey, config, request.ConversationId.ToLong(), lastUsage);
    }
    #endregion

    #region 协议格式化
    /// <summary>将 ChatStreamEvent 转换为 OpenAI 兼容的 ChatResponse 流式块</summary>
    /// <param name="evt">管道事件</param>
    /// <param name="model">模型编码</param>
    /// <returns>ChatResponse；不需要输出的事件返回 null</returns>
    public static ChatResponse? ConvertEventToChunk(ChatStreamEvent evt, String? model)
    {
        var chunk = new ChatResponse
        {
            Object = "chat.completion.chunk",
            Model = model,
            Created = DateTimeOffset.UtcNow,
        };

        switch (evt.Type)
        {
            case "content_delta":
                chunk.AddDelta(evt.Content);
                return chunk;
            case "thinking_delta":
                chunk.AddDelta(null, evt.Content);
                return chunk;
            case "message_done":
                chunk.AddDelta(null, finishReason: FinishReason.Stop);
                if (evt.Usage != null) chunk.Usage = evt.Usage;
                return chunk;
            default:
                return null;
        }
    }

    /// <summary>将流式块按协议格式转换为 SSE 事件字符串列表</summary>
    /// <param name="chunk">内部统一流式块</param>
    /// <param name="protocol">目标协议</param>
    /// <returns>SSE 事件字符串列表</returns>
    public static IList<String> FormatStreamEvents(ChatResponse chunk, GatewayProtocol protocol)
    {
        var events = new List<String>();
        switch (protocol)
        {
            case GatewayProtocol.Anthropic:
                foreach (var evt in AnthropicResponse.CreateStreamDelta(chunk))
                {
                    var json = JsonSerializer.Serialize(evt, SnakeCaseOptions);
                    events.Add($"event: {evt.EventName}\ndata: {json}\n\n");
                }
                break;
            case GatewayProtocol.Gemini:
                {
                    var geminiChunk = GeminiResponse.FromChunk(chunk);
                    events.Add($"data: {JsonSerializer.Serialize(geminiChunk, CamelCaseOptions)}\n\n");
                    break;
                }
            default:
                {
                    var openaiChunk = ChatCompletionResponse.FromChunk(chunk);
                    events.Add($"data: {JsonSerializer.Serialize(openaiChunk, SnakeCaseOptions)}\n\n");
                    break;
                }
        }
        return events;
    }

    /// <summary>生成流式开始事件列表（仅 Anthropic 需要）</summary>
    /// <param name="model">模型编码</param>
    /// <param name="protocol">目标协议</param>
    /// <returns>SSE 事件字符串列表</returns>
    public static IList<String> FormatStreamStart(String model, GatewayProtocol protocol)
    {
        if (protocol != GatewayProtocol.Anthropic) return [];

        var events = new List<String>();
        foreach (var evt in AnthropicResponse.CreateStreamStart(model))
        {
            var json = JsonSerializer.Serialize(evt, SnakeCaseOptions);
            events.Add($"event: {evt.EventName}\ndata: {json}\n\n");
        }
        return events;
    }

    /// <summary>生成流式结束标记</summary>
    /// <param name="protocol">目标协议</param>
    /// <returns>SSE 结束标记字符串，不需要时返回 null</returns>
    public static String? FormatStreamEnd(GatewayProtocol protocol)
    {
        switch (protocol)
        {
            case GatewayProtocol.Anthropic:
                var stopEvt = AnthropicResponse.CreateStreamEnd();
                var stopJson = JsonSerializer.Serialize(stopEvt, SnakeCaseOptions);
                return $"event: {stopEvt.EventName}\ndata: {stopJson}\n\n";
            case GatewayProtocol.Gemini:
                return null;
            default:
                return "data: [DONE]\n\n";
        }
    }

    /// <summary>非流式响应序列化</summary>
    /// <param name="result">对话响应</param>
    /// <param name="protocol">目标协议</param>
    /// <returns>JSON 字符串</returns>
    public static String FormatResponse(ChatResponse result, GatewayProtocol protocol)
    {
        return protocol switch
        {
            GatewayProtocol.Anthropic => JsonSerializer.Serialize(AnthropicResponse.From(result), SnakeCaseOptions),
            GatewayProtocol.Gemini => JsonSerializer.Serialize(GeminiResponse.From(result), CamelCaseOptions),
            _ => JsonSerializer.Serialize(ChatCompletionResponse.From(result), SnakeCaseOptions),
        };
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

    /// <summary>写入用量记录到 UsageRecord 表，并完成费用计算与配额累加</summary>
    /// <param name="appKey">应用密钥</param>
    /// <param name="model">模型配置</param>
    /// <param name="conversationId">关联会话编号</param>
    /// <param name="usage">用量统计</param>
    public virtual void RecordUsage(AppKey? appKey, ModelConfig model, Int64 conversationId, UsageDetails? usage)
    {
        if (usage == null || model == null) return;

        var conv = new Conversation
        {
            Id = conversationId,
            UserId = appKey?.UserId ?? 0,
            AppKeyId = appKey?.Id ?? 0,
        };
        usageService.Record(conv, null, appKey, model, usage, "Gateway");
    }

    /// <summary>从 AI 消息中提取纯文本内容。支持多模态消息（Contents 列表中提取 TextContent）</summary>
    /// <param name="message">AI 对话消息</param>
    /// <returns>纯文本内容，无文本时返回 null</returns>
    public static String? ExtractTextContent(AiChatMessage? message)
    {
        if (message == null) return null;

        // 确保多模态内容已解析（Content 可能是未解析的 JSON 数组对象）
        message.ResolveContents();

        // 优先从 Contents 中提取 TextContent
        if (message.Contents is { Count: > 0 } contents)
        {
            var sb = Pool.StringBuilder.Get();
            foreach (var item in contents)
            {
                if (item is TextContent text && !String.IsNullOrEmpty(text.Text))
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(text.Text);
                }
            }
            var result = sb.Return(true);
            if (!result.IsNullOrEmpty()) return result;
        }

        // 回退到 Content 属性
        var content = message.Content;
        if (content is String str) return str;

        return content?.ToString();
    }

    /// <summary>预创建网关会话。在对话执行前插入 Conversation 骨架并返回其 Id，供后续 UsageRecord 关联。
    /// 若提取不到用户消息内容则返回 0</summary>
    /// <param name="request">对话请求</param>
    /// <param name="config">模型配置</param>
    /// <param name="appKey">应用密钥</param>
    /// <returns>会话，失败时返回 null</returns>
    public Conversation? CreateGatewayConversation(IChatRequest request, ModelConfig config, AppKey appKey)
    {
        try
        {
            var lastUserMsg = request.Messages?.LastOrDefault(m => "user".Equals(m.Role, StringComparison.OrdinalIgnoreCase));
            var userContent = ExtractTextContent(lastUserMsg);
            if (userContent.IsNullOrEmpty()) return null;

            var conversation = new Conversation
            {
                UserId = appKey.UserId,
                UserName = appKey.Name,
                AppKeyId = appKey.Id,
                Title = userContent.Length > 50 ? userContent[..50] + "..." : userContent,
                ModelId = config.Id,
                ModelName = config.Name,
                Source = "Gateway",
                LastMessageTime = DateTime.Now,
                Enable = true,
            };
            //conversation.Insert();
            return conversation;
        }
        catch (Exception ex)
        {
            log?.Error("预创建网关会话失败: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>记录网关对话。持久化 ChatMessage，并更新预创建会话的用量统计；若未预创建则同时插入 Conversation</summary>
    /// <param name="request">对话请求</param>
    /// <param name="config">模型配置</param>
    /// <param name="appKey">应用密钥</param>
    /// <param name="responseContent">AI 回复内容</param>
    /// <param name="thinkingContent">思考过程</param>
    /// <param name="usage">Token 用量统计</param>
    public void RecordGatewayConversation(IChatRequest request, ModelConfig config, AppKey appKey, String? responseContent, String? thinkingContent, UsageDetails? usage)
    {
        try
        {
            // 提取最后一条用户消息作为对话内容（支持多模态）
            var lastUserMsg = request.Messages?.LastOrDefault(m => "user".Equals(m.Role, StringComparison.OrdinalIgnoreCase));
            var userContent = ExtractTextContent(lastUserMsg);
            if (userContent.IsNullOrEmpty()) return;

            Conversation? conversation;
            var existingId = request.ConversationId.ToLong();
            if (existingId > 0)
            {
                // 复用预创建的会话，补充用量统计
                conversation = Conversation.FindById(existingId);
                if (conversation != null)
                {
                    conversation.MessageCount = responseContent.IsNullOrEmpty() ? 1 : 2;
                    conversation.InputTokens = usage?.InputTokens ?? 0;
                    conversation.OutputTokens = usage?.OutputTokens ?? 0;
                    conversation.TotalTokens = usage?.TotalTokens ?? 0;
                    conversation.ElapsedMs = usage?.ElapsedMs ?? 0;
                    conversation.LastMessageTime = DateTime.Now;
                    conversation.Update();
                }
            }
            else
            {
                // 未预创建时回退到直接插入
                conversation = new Conversation
                {
                    UserId = appKey.UserId,
                    UserName = appKey.Name,
                    AppKeyId = appKey.Id,
                    Title = userContent.Length > 50 ? userContent[..50] + "..." : userContent,
                    ModelId = config.Id,
                    ModelName = config.Name,
                    Source = "Gateway",
                    LastMessageTime = DateTime.Now,
                    MessageCount = responseContent.IsNullOrEmpty() ? 1 : 2,
                    InputTokens = usage?.InputTokens ?? 0,
                    OutputTokens = usage?.OutputTokens ?? 0,
                    TotalTokens = usage?.TotalTokens ?? 0,
                    ElapsedMs = usage?.ElapsedMs ?? 0,
                    Enable = true,
                };
                conversation.Insert();
            }

            if (conversation == null) return;

            // 创建用户消息
            var userMsg = new DbChatMessage
            {
                ConversationId = conversation.Id,
                Role = "user",
                Content = userContent,
                //InputTokens = usage?.InputTokens ?? 0,
                Enable = true,
            };
            userMsg.Insert();

            // 创建 AI 回复消息
            if (!responseContent.IsNullOrEmpty())
            {
                var assistantMsg = new DbChatMessage
                {
                    ConversationId = conversation.Id,
                    Role = "assistant",
                    Content = responseContent,
                    ThinkingContent = thinkingContent.IsNullOrEmpty() ? null : thinkingContent,
                    ModelName = config.Code,
                    InputTokens = usage?.InputTokens ?? 0,
                    OutputTokens = usage?.OutputTokens ?? 0,
                    TotalTokens = usage?.TotalTokens ?? 0,
                    ElapsedMs = usage?.ElapsedMs ?? 0,
                    Enable = true,
                };
                assistantMsg.Insert();
            }
        }
        catch (Exception ex)
        {
            // 记录失败不影响 API 响应
            log?.Error("网关对话记录失败: {0}", ex.Message);
        }
    }
    #endregion
}
