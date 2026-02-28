using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using NewLife.AI.ChatAI.Contracts;
using NewLife.AI.Models;

namespace NewLife.ChatAI.Services;

/// <summary>内存版对话应用服务</summary>
public class InMemoryChatApplicationService : IChatApplicationService
{
    private readonly ConcurrentDictionary<Int64, ConversationSummaryDto> _conversations = new();
    private readonly ConcurrentDictionary<Int64, List<MessageDto>> _messages = new();
    private readonly ConcurrentDictionary<String, (Int64 ConversationId, DateTime CreateTime, DateTime? ExpireTime)> _shares = new();
    private UserSettingsDto _settings = new("zh-CN", "system", 16, "Enter", "qwen-max", ThinkingMode.Auto, 10, String.Empty, false);
    private Int64 _conversationSeed = 1000;
    private Int64 _messageSeed = 5000;

    public Task<ConversationSummaryDto> CreateConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _conversationSeed);
        var modelCode = request.ModelCode ?? _settings.DefaultModel;
        var title = String.IsNullOrWhiteSpace(request.Title) ? "新建对话" : request.Title.Trim();
        var item = new ConversationSummaryDto(id, title, modelCode, DateTime.Now, false);
        _conversations[id] = item;
        _messages.TryAdd(id, []);
        return Task.FromResult(item);
    }

    public Task<PagedResultDto<ConversationSummaryDto>> GetConversationsAsync(Int32 page, Int32 pageSize, CancellationToken cancellationToken)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var all = _conversations.Values
            .OrderByDescending(e => e.IsPinned)
            .ThenByDescending(e => e.LastMessageTime)
            .ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResultDto<ConversationSummaryDto>(items, all.Count, page, pageSize));
    }

    public Task<ConversationSummaryDto?> UpdateConversationAsync(Int64 conversationId, UpdateConversationRequest request, CancellationToken cancellationToken)
    {
        if (!_conversations.TryGetValue(conversationId, out var current)) return Task.FromResult<ConversationSummaryDto?>(null);

        var title = String.IsNullOrWhiteSpace(request.Title) ? current.Title : request.Title.Trim();
        var modelCode = String.IsNullOrWhiteSpace(request.ModelCode) ? current.ModelCode : request.ModelCode.Trim();
        var updated = new ConversationSummaryDto(current.Id, title, modelCode, DateTime.Now, current.IsPinned);
        _conversations[conversationId] = updated;
        return Task.FromResult<ConversationSummaryDto?>(updated);
    }

    public Task<Boolean> DeleteConversationAsync(Int64 conversationId, CancellationToken cancellationToken)
    {
        var result = _conversations.TryRemove(conversationId, out _);
        _messages.TryRemove(conversationId, out _);
        return Task.FromResult(result);
    }

    public Task<Boolean> SetPinAsync(Int64 conversationId, Boolean isPinned, CancellationToken cancellationToken)
    {
        if (!_conversations.TryGetValue(conversationId, out var current)) return Task.FromResult(false);

        _conversations[conversationId] = new ConversationSummaryDto(current.Id, current.Title, current.ModelCode, DateTime.Now, isPinned);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<MessageDto>> GetMessagesAsync(Int64 conversationId, CancellationToken cancellationToken)
    {
        if (!_messages.TryGetValue(conversationId, out var list)) return Task.FromResult<IReadOnlyList<MessageDto>>([]);
        return Task.FromResult<IReadOnlyList<MessageDto>>(list.OrderBy(e => e.CreateTime).ToList());
    }

    public Task<MessageDto?> EditMessageAsync(Int64 messageId, EditMessageRequest request, CancellationToken cancellationToken)
    {
        foreach (var item in _messages)
        {
            var index = item.Value.FindIndex(e => e.Id == messageId);
            if (index < 0) continue;

            var source = item.Value[index];
            var updated = new MessageDto(source.Id, source.ConversationId, source.Role, request.Content, source.ThinkingContent, source.ThinkingMode, source.Attachments, DateTime.Now);
            item.Value[index] = updated;
            return Task.FromResult<MessageDto?>(updated);
        }

        return Task.FromResult<MessageDto?>(null);
    }

    public Task<MessageDto?> RegenerateMessageAsync(Int64 messageId, CancellationToken cancellationToken)
    {
        foreach (var item in _messages)
        {
            var index = item.Value.FindIndex(e => e.Id == messageId && e.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase));
            if (index < 0) continue;

            var source = item.Value[index];
            var updated = new MessageDto(source.Id, source.ConversationId, source.Role, "这是重新生成的示例回复。", null, source.ThinkingMode, source.Attachments, DateTime.Now);
            item.Value[index] = updated;
            return Task.FromResult<MessageDto?>(updated);
        }

        return Task.FromResult<MessageDto?>(null);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(Int64 conversationId, SendMessageRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_messages.TryGetValue(conversationId, out var list))
        {
            list = [];
            _messages.TryAdd(conversationId, list);
        }

        var userMessage = new MessageDto(Interlocked.Increment(ref _messageSeed), conversationId, "user", request.Content, null, request.ThinkingMode, null, DateTime.Now);
        list.Add(userMessage);

        var assistantMessageId = Interlocked.Increment(ref _messageSeed);

        // 发送 message_start
        yield return ChatStreamEvent.MessageStart(assistantMessageId, "qwen-max", (Int32)request.ThinkingMode);

        var answer = "这是流式回复骨架。后续可接入真实模型推理与上下文管理。";
        var chunks = answer.Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var content = new StringBuilder();
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = chunk + "。";
            content.Append(line);
            yield return ChatStreamEvent.ContentDelta(line);
            await Task.Delay(120, cancellationToken).ConfigureAwait(false);
        }

        list.Add(new MessageDto(assistantMessageId, conversationId, "assistant", content.ToString(), null, request.ThinkingMode, null, DateTime.Now));

        if (_conversations.TryGetValue(conversationId, out var conversation))
            _conversations[conversationId] = new ConversationSummaryDto(conversation.Id, conversation.Title, conversation.ModelCode, DateTime.Now, conversation.IsPinned);

        // 发送 message_done
        yield return ChatStreamEvent.MessageDone();
    }

    public Task StopGenerateAsync(Int64 messageId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<String?> GenerateTitleAsync(Int64 conversationId, String userMessage, CancellationToken cancellationToken)
    {
        // 内存版模拟标题生成：截取前10个字符作为标题
        var title = userMessage.Length > 10 ? userMessage.Substring(0, 10) : userMessage;
        if (_conversations.TryGetValue(conversationId, out var conversation))
            _conversations[conversationId] = new ConversationSummaryDto(conversation.Id, title, conversation.ModelCode, conversation.LastMessageTime, conversation.IsPinned);

        return Task.FromResult<String?>(title);
    }

    public Task SubmitFeedbackAsync(Int64 messageId, FeedbackRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteFeedbackAsync(Int64 messageId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<ShareLinkDto> CreateShareLinkAsync(Int64 conversationId, CreateShareRequest request, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var createTime = DateTime.Now;
        DateTime? expireTime = null;
        if (request.ExpireHours is > 0)
            expireTime = createTime.AddHours(request.ExpireHours.Value);

        _shares[token] = (conversationId, createTime, expireTime);
        return Task.FromResult(new ShareLinkDto($"/api/share/{token}", createTime, expireTime));
    }

    public Task<Object?> GetShareContentAsync(String token, CancellationToken cancellationToken)
    {
        if (!_shares.TryGetValue(token, out var share)) return Task.FromResult<Object?>(null);
        if (share.ExpireTime != null && share.ExpireTime < DateTime.Now) return Task.FromResult<Object?>(null);

        _messages.TryGetValue(share.ConversationId, out var list);
        var result = new
        {
            ConversationId = share.ConversationId,
            Messages = list?.OrderBy(e => e.CreateTime).ToList() ?? [],
            share.CreateTime,
            share.ExpireTime
        };
        return Task.FromResult<Object?>(result);
    }

    public Task<Boolean> RevokeShareLinkAsync(String token, CancellationToken cancellationToken) => Task.FromResult(_shares.TryRemove(token, out _));

    public async Task<UploadAttachmentResult> UploadAttachmentAsync(String fileName, Int64 size, Stream stream, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);

        var id = Guid.NewGuid().ToString("N");
        return new UploadAttachmentResult(id, fileName, $"/api/attachments/{id}", size);
    }

    public Task<ModelInfoDto[]> GetModelsAsync(CancellationToken cancellationToken)
    {
        var models = new[]
        {
            new ModelInfoDto("qwen-max", "Qwen-Max", true, true, false, true),
            new ModelInfoDto("deepseek-r1", "DeepSeek-R1", true, false, false, true),
            new ModelInfoDto("gpt-4o", "GPT-4o", true, true, false, true)
        };
        return Task.FromResult(models);
    }

    public Task<UserSettingsDto> GetUserSettingsAsync(CancellationToken cancellationToken) => Task.FromResult(_settings);

    public Task<UserSettingsDto> UpdateUserSettingsAsync(UserSettingsDto settings, CancellationToken cancellationToken)
    {
        _settings = settings;
        return Task.FromResult(_settings);
    }

    public Task<Stream> ExportUserDataAsync(CancellationToken cancellationToken)
    {
        var json = "{\"message\":\"TODO: export user chat data\"}";
        Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return Task.FromResult(stream);
    }

    public Task ClearUserConversationsAsync(CancellationToken cancellationToken)
    {
        _conversations.Clear();
        _messages.Clear();
        _shares.Clear();
        return Task.CompletedTask;
    }
}
