using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife.AI.Clients;
using NewLife.AI.Clients.Anthropic;
using NewLife.AI.Clients.Gemini;
using NewLife.AI.Clients.OpenAI;
using NewLife.AI.Models;
using NewLife.Collections;
using NewLife.Serialization;
using NewLife.ChatAI.Entity;
using XCode.Membership;
using AiChatMessage = NewLife.AI.Models.ChatMessage;
using DbChatMessage = NewLife.ChatAI.Entity.ChatMessage;
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
/// <param name="log">日志</param>
public class GatewayService(UsageService? usageService, ModelService modelService, ILog log)
{
    #region 属性
    /// <summary>上游重试最大次数</summary>
    private const Int32 MaxRetryCount = 5;

    /// <summary>重试最大等待时间（秒）</summary>
    private const Int32 MaxRetryDelaySec = 30;

    /// <summary>snake_case 序列化选项。用于写出符合 OpenAI / Anthropic 协议的响应体</summary>
    public static readonly JsonSerializerOptions SnakeCaseOptions;

    /// <summary>camelCase 序列化选项。用于写出符合 Gemini 协议的响应体</summary>
    public static readonly JsonSerializerOptions CamelCaseOptions;

    static GatewayService()
    {
        var snake = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        SystemJson.Apply(snake, true);
        SnakeCaseOptions = snake;

        var camel = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        SystemJson.Apply(camel, true);
        CamelCaseOptions = camel;
    }
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

    #region 系统提示词
    /// <summary>为网关请求构建系统提示词。拼接：用户基本信息→UserSetting.SystemPrompt→ModelConfig.SystemPrompt</summary>
    /// <param name="appKey">应用密鑰</param>
    /// <param name="config">模型配置</param>
    /// <returns>系统消息，无内容时返回 null</returns>
    public AiChatMessage? BuildSystemMessage(AppKey appKey, ModelConfig config)
    {
        var parts = new List<String>();

        // 0. 当前用户基础信息
        if (appKey.UserId > 0)
        {
            var iuser = ManageProvider.Provider?.FindByID(appKey.UserId) as IUser;
            if (iuser != null)
            {
                var sb = Pool.StringBuilder.Get();
                sb.Append($"当前用户：{iuser.DisplayName}（{iuser.Name}）");
                var roleIds = iuser.RoleIds?.SplitAsInt();
                if (roleIds?.Length > 0)
                {
                    var roleNames = roleIds.Select(id => Role.FindByID(id)?.Name).Where(n => !n.IsNullOrEmpty()).Join(",");
                    if (!roleNames.IsNullOrEmpty()) sb.Append($"，角色：{roleNames}");
                }
                if (iuser.DepartmentID > 0)
                {
                    var dept = Department.FindByID(iuser.DepartmentID);
                    if (dept != null) sb.Append($"，部门：{dept.Name}");
                }
                parts.Add(sb.Return(true));
            }
        }

        // 1. 个性化定制
        var userSetting = appKey.UserId > 0 ? UserSetting.FindByUserId(appKey.UserId) : null;
        if (userSetting != null)
        {
            if (!String.IsNullOrWhiteSpace(userSetting.Nickname))
                parts.Add($"用户希望你称呼他为「{userSetting.Nickname.Trim()}」");

            if (!String.IsNullOrWhiteSpace(userSetting.UserBackground))
                parts.Add($"## 用户背景信息\n{userSetting.UserBackground.Trim()}");

            var stylePrompt = userSetting.ResponseStyle switch
            {
                ResponseStyle.Precise => "请给出准确、确定性高的回答。优先引用事实和数据，避免模糊表述和不确定的推测。回答简洁有条理。",
                ResponseStyle.Vivid => "请用丰富的表达方式回答，善于使用类比、举例和故事来解释概念。让回答有温度、易于理解，适当展开讨论。",
                ResponseStyle.Creative => "请大胆发散思维，提供新颖独特的视角和创意方案。鼓励联想、跨界类比和非常规思路，不必拘泥于常规答案。",
                _ => null
            };
            if (stylePrompt != null) parts.Add(stylePrompt);
        }

        // 2. 用户自定义指令
        if (userSetting != null && !String.IsNullOrWhiteSpace(userSetting.SystemPrompt))
            parts.Add(userSetting.SystemPrompt.Trim());

        // 3. 模型级系统提示词
        if (!String.IsNullOrWhiteSpace(config.SystemPrompt))
            parts.Add(config.SystemPrompt.Trim());

        if (parts.Count == 0) return null;

        return new AiChatMessage { Role = "system", Content = String.Join("\n\n", parts) };
    }

    /// <summary>为网关请求构建上下文消息列表。注入系统提示词（用户信息+UserSetting+ModelConfig），过滤请求中原有系统消息</summary>
    /// <param name="request">网关请求</param>
    /// <param name="appKey">应用密钥</param>
    /// <param name="config">模型配置</param>
    /// <returns>上下文消息列表</returns>
    public IList<AiChatMessage> BuildContextMessages(IChatRequest request, AppKey appKey, ModelConfig config)
    {
        var messages = new List<AiChatMessage>();

        // 构建系统消息（包含用户信息 + UserSetting + ModelConfig SystemPrompt）
        var sysMsg = BuildSystemMessage(appKey, config);
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
    /// <param name="config">模型配置</param>
    /// <param name="appKey">应用密钥（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<ChatResponse> ChatAsync(IChatRequest request, ModelConfig config, AppKey? appKey, CancellationToken cancellationToken = default)
    {
        using var client = modelService.CreateClient(config);
        if (client == null)
            throw new InvalidOperationException($"未找到服务商，模型 '{config.Code}' 关联的提供商类型 '{config.ProviderInfo?.Provider}' 未注册");

        ChatResponse? response = null;
        for (var i = 0; i <= MaxRetryCount; i++)
        {
            try
            {
                response = ChatResponse.From(await client.GetResponseAsync(request, cancellationToken).ConfigureAwait(false));
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
        RecordUsage(appKey, config.Id, request.ConversationId.ToLong(), response.Usage);

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
        using var streamClient = modelService.CreateClient(config);
        if (streamClient == null)
            throw new InvalidOperationException($"未找到服务商，模型 '{config.Code}' 关联的提供商类型 '{config.ProviderInfo?.Provider}' 未注册");

        IAsyncEnumerable<IChatResponse>? stream = null;
        for (var i = 0; i <= MaxRetryCount; i++)
        {
            try
            {
                stream = streamClient.GetStreamingResponseAsync(request, cancellationToken);
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

        UsageDetails? lastUsage = null;
        await foreach (var rawChunk in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var chunk = ChatResponse.From(rawChunk);
            if (chunk.Usage != null) lastUsage = chunk.Usage;
            yield return chunk;
        }

        // 更新 AppKey 统计
        UpdateAppKeyUsage(appKey, lastUsage);

        // 写入用量记录
        RecordUsage(appKey, config.Id, request.ConversationId.ToLong(), lastUsage);
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
        switch (protocol)
        {
            case GatewayProtocol.Anthropic:
                return JsonSerializer.Serialize(AnthropicResponse.From(result), SnakeCaseOptions);
            case GatewayProtocol.Gemini:
                return JsonSerializer.Serialize(GeminiResponse.From(result), CamelCaseOptions);
            default:
                return JsonSerializer.Serialize(ChatCompletionResponse.From(result), SnakeCaseOptions);
        }
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
    /// <param name="conversationId">关联会话编号</param>
    /// <param name="usage">用量统计</param>
    internal void RecordUsage(AppKey? appKey, Int32 modelId, Int64 conversationId, UsageDetails? usage)
    {
        if (usage == null) return;

        usageService?.Record(
            appKey?.UserId ?? 0,
            appKey?.Id ?? 0,
            conversationId, 0,
            modelId,
            usage.InputTokens,
            usage.OutputTokens,
            usage.TotalTokens,
            "Gateway");
    }

    /// <summary>更新 AppKey 的调用次数和 Token 用量</summary>
    /// <param name="appKey">应用密钥</param>
    /// <param name="usage">用量统计</param>
    private void UpdateAppKeyUsage(AppKey? appKey, UsageDetails? usage)
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
    /// <returns>会话编号，失败时返回 0</returns>
    public Int64 CreateGatewayConversation(IChatRequest request, ModelConfig config, AppKey appKey)
    {
        try
        {
            var lastUserMsg = request.Messages?.LastOrDefault(m => "user".Equals(m.Role, StringComparison.OrdinalIgnoreCase));
            var userContent = ExtractTextContent(lastUserMsg);
            if (userContent.IsNullOrEmpty()) return 0;

            var conversation = new Conversation
            {
                UserId = appKey.UserId,
                UserName = appKey.Name,
                Title = userContent.Length > 50 ? userContent[..50] + "..." : userContent,
                ModelId = config.Id,
                ModelName = config.Name,
                Source = "Gateway",
                LastMessageTime = DateTime.Now,
            };
            conversation.Insert();
            return conversation.Id;
        }
        catch (Exception ex)
        {
            log?.Error("预创建网关会话失败: {0}", ex.Message);
            return 0;
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
                InputTokens = usage?.InputTokens ?? 0,
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
                    OutputTokens = usage?.OutputTokens ?? 0,
                    TotalTokens = usage?.TotalTokens ?? 0,
                    ElapsedMs = usage?.ElapsedMs ?? 0,
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
