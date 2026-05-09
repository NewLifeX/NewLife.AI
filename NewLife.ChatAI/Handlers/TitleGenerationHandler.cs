using NewLife.Caching;
using NewLife.Collections;
using NewLife.Log;

namespace NewLife.ChatAI.Handlers;

/// <summary>会话标题异步生成处理器。事前在会话无标题时启动后台任务，与主 LLM 调用并行生成简短标题，内置内容指纹去重缓存</summary>
/// <param name="modelService">模型服务（用于建模型客户端）</param>
/// <param name="setting">对话配置</param>
/// <param name="cacheProvider"></param>
/// <param name="tracer">追踪器</param>
/// <param name="log">日志</param>
[ChatHandlerOrder(30)]
public class TitleGenerationHandler(ModelService modelService, IChatSetting setting, ICacheProvider cacheProvider, ITracer? tracer, ILog? log) : IChatHandler
{
    /// <summary>模型服务（供派生类访问）</summary>
    protected readonly ModelService ModelServiceInstance = modelService;

    /// <summary>追踪器（供派生类访问）</summary>
    protected readonly ITracer? Tracer = tracer;

    /// <summary>日志（供派生类访问）</summary>
    protected readonly ILog? Log = log;

    /// <inheritdoc/>
    public virtual ChatHandlerCapabilities Capabilities => ChatHandlerCapabilities.Before;

    /// <inheritdoc/>
    public Task OnBefore(IChatContext context, CancellationToken cancellationToken)
    {
        if (!setting.AutoGenerateTitle) return Task.CompletedTask;
        if (context is not MessageFlowContext flow) return Task.CompletedTask;

        var conversation = context.Conversation;
        // 已有标题则跳过（标题一旦生成就保持，不重复生成）
        if (!conversation.Title.IsNullOrEmpty()) return Task.CompletedTask;

        var userContent = context.UserMessage?.Content;
        var titleText = ExtractTitleText(userContent);
        if (titleText.IsNullOrEmpty() && !context.UserMessage?.Attachments.IsNullOrEmpty() == true)
            titleText = "[图片] 对话";

        if (titleText.IsNullOrEmpty()) return Task.CompletedTask;

        // 异步生成：与主 LLM 调用并行执行，不阻塞响应流
        _ = Task.Run(() => GenerateTitleAsync(flow, titleText, CancellationToken.None));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnAfter(IChatContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>异步生成会话标题。短文本直接采用，否则调用模型生成。派生类可覆盖以增强</summary>
    /// <param name="flow">上下文</param>
    /// <param name="userMessage">用户首条消息内容</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成或截取的标题</returns>
    public virtual async Task<String?> GenerateTitleAsync(MessageFlowContext flow, String userMessage, CancellationToken cancellationToken)
    {
        var conversation = flow.Conversation;
        if (conversation == null) return null;

        // 较短内容直接作为标题
        userMessage = ExtractTitleText(userMessage) ?? userMessage;
        var cleanMsg = userMessage.Replace("\n", " ").Replace("\r", "").Trim();
        if (cleanMsg.Length <= 16)
        {
            if (!String.IsNullOrWhiteSpace(cleanMsg) && cleanMsg != conversation.Title)
            {
                conversation.Title = cleanMsg;
                conversation.Update();
            }
            return cleanMsg;
        }

        // 命中内容缓存：直接写回，跳过 LLM 调用
        if (cleanMsg.Length < 64)
        {
            var cachedTitle = cacheProvider.Cache.Get<String>($"ai:title:{cleanMsg}");
            if (cachedTitle != conversation.Title)
            {
                conversation.Title = cachedTitle;
                conversation.Update();
            }
            return cachedTitle;
        }

        using var span = Tracer?.NewSpan("ai:GenerateTitle");

        var model = ModelServiceInstance.ResolveModel(conversation.ModelId);
        if (model != null)
        {
            try
            {
                var prompt = "请用16个字以内为以下对话生成一个简短标题，只输出标题文字，不要加任何标点和引号：";
                var title = await ModelServiceInstance.CallAsync(
                    model,
                    flow.Conversation,
                    $"{prompt}\n{userMessage}",
                    null,
                    new ChatOptions { MaxTokens = 30, Temperature = 0.3 },
                    "Title",
                    cancellationToken).ConfigureAwait(false);

                if (!String.IsNullOrWhiteSpace(title))
                {
                    title = title.Trim().Trim('"', '\u201c', '\u201d', '\'', '\u300a', '\u300b');
                    if (title.Length > 30) title = title[..30];
                    span?.AppendTag(title!);

                    // 写入内容缓存
                    if (cleanMsg.Length < 64 && !title.IsNullOrEmpty())
                        cacheProvider.Cache.Set($"ai:title:{cleanMsg}", title, TimeSpan.FromHours(1));

                    conversation.Title = title;
                    conversation.Update();
                    return title;
                }
            }
            catch (Exception ex)
            {
                span?.SetError(ex);
                Log?.Warn("模型生成标题失败，回退截取: {0}", ex.Message);
            }
        }

        // 回退方案：截取前16个字符（考虑到中文），并清理换行等空白符
        var fallbackTitle = userMessage.Length > 16 ? userMessage[..16] : userMessage;
        fallbackTitle = fallbackTitle.Replace("\n", " ").Replace("\r", "").Trim();
        if (!fallbackTitle.IsNullOrWhiteSpace() && fallbackTitle != conversation.Title)
        {
            conversation.Title = fallbackTitle;
            conversation.Update();
        }
        return fallbackTitle;
    }

    /// <summary>从用户消息中提取纯文本，支持 JSON 编码的多模态内容数组。用于标题生成等场景</summary>
    /// <param name="userMessage">用户消息文本，可能是纯文本或 JSON 多模态内容数组</param>
    /// <returns>提取到的纯文本，无文本时返回 null</returns>
    public static String? ExtractTitleText(String? userMessage)
    {
        if (userMessage.IsNullOrEmpty()) return null;

        // 快速判断：不以 [ 开头则视为普通文本
        if (!userMessage.StartsWith('[')) return userMessage;

        // 尝试按 OpenAI 多模态格式解析 [{"type":"text","text":"..."},...] 提取文本片段
        var contents = AiChatMessage.ParseMultimodalContent(userMessage);
        if (contents == null || contents.Count == 0) return userMessage;

        var sb = Pool.StringBuilder.Get();
        foreach (var item in contents)
        {
            if (item is TextContent text && !String.IsNullOrEmpty(text.Text))
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(text.Text);
            }
        }
        var result = sb.Return(true);

        // 有文本则返回；全是图片等非文本内容时返回 null
        return !result.IsNullOrEmpty() ? result : null;
    }
}
