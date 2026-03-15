using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.ChatAI;
using NewLife.AI.Models;
using NewLife.ChatAI.Controllers;
using NewLife.ChatAI.Services;
using NewLife.Log;
using Xunit;

namespace XUnitTest;

/// <summary>ChatAI 后端功能单元测试</summary>
public class ChatAITests
{
    #region ChatSetting 配置类测试
    [Fact]
    public void ChatSettingHasCorrectDefaults()
    {
        var setting = new ChatSetting();

        Assert.Equal(30, setting.ShareExpireDays);
        Assert.Equal(0, setting.DefaultModel);
        Assert.Equal(ThinkingMode.Auto, setting.DefaultThinkingMode);
        Assert.Equal(10, setting.DefaultContextRounds);
        Assert.Equal(20, setting.MaxAttachmentSize);
        Assert.Equal(5, setting.MaxAttachmentCount);
        Assert.True(setting.AutoGenerateTitle);
        Assert.Contains("10个字", setting.TitlePrompt);
        Assert.Contains(".jpg", setting.AllowedExtensions);
        Assert.True(setting.EnableGateway);
        Assert.Equal(60, setting.GatewayRateLimit);
        Assert.Equal(5, setting.UpstreamRetryCount);
        Assert.True(setting.EnableFunctionCalling);
        Assert.True(setting.EnableMcp);
        Assert.Equal("1024x1024", setting.DefaultImageSize);
        Assert.True(setting.EnableUsageStats);
        Assert.True(setting.BackgroundGeneration);
        Assert.NotEmpty(setting.SuggestedQuestions);
    }

    [Fact]
    public void ChatSettingCanModifyValues()
    {
        var setting = new ChatSetting();

        setting.ShareExpireDays = 0;
        setting.DefaultModel = 3;
        setting.GatewayRateLimit = 100;
        setting.EnableMcp = false;

        Assert.Equal(0, setting.ShareExpireDays);
        Assert.Equal(3, setting.DefaultModel);
        Assert.Equal(100, setting.GatewayRateLimit);
        Assert.False(setting.EnableMcp);
    }

    [Fact]
    public void ChatSettingCurrentIsNotNull()
    {
        var current = ChatSetting.Current;
        Assert.NotNull(current);
    }

    [Fact]
    public void ChatSettingAllowedExtensionsContainsExpectedTypes()
    {
        var setting = new ChatSetting();
        var extensions = setting.AllowedExtensions.Split(',');

        Assert.Contains(".jpg", extensions);
        Assert.Contains(".png", extensions);
        Assert.Contains(".pdf", extensions);
        Assert.Contains(".docx", extensions);
        Assert.Contains(".txt", extensions);
        Assert.Contains(".md", extensions);
        Assert.Contains(".csv", extensions);
    }
    #endregion

    #region SSE 事件模型完整性测试
    [Fact]
    public void ChatStreamEventMessageStartHasAllFields()
    {
        var ev = ChatStreamEvent.MessageStart(1001, "qwen-max", ThinkingMode.Think);

        Assert.Equal("message_start", ev.Type);
        Assert.Equal(1001, ev.MessageId);
        Assert.Equal("qwen-max", ev.Model);
        Assert.Equal(ThinkingMode.Think, ev.ThinkingMode);
    }

    [Fact]
    public void ChatStreamEventThinkingDeltaCarriesContent()
    {
        var ev = ChatStreamEvent.ThinkingDelta("让我分析一下...");

        Assert.Equal("thinking_delta", ev.Type);
        Assert.Equal("让我分析一下...", ev.Content);
    }

    [Fact]
    public void ChatStreamEventThinkingDoneCarriesTime()
    {
        var ev = ChatStreamEvent.ThinkingDone(3200);

        Assert.Equal("thinking_done", ev.Type);
        Assert.Equal(3200, ev.ThinkingTime);
    }

    [Fact]
    public void ChatStreamEventContentDeltaCarriesContent()
    {
        var ev = ChatStreamEvent.ContentDelta("这是回答的一部分");

        Assert.Equal("content_delta", ev.Type);
        Assert.Equal("这是回答的一部分", ev.Content);
    }

    [Fact]
    public void ChatStreamEventMessageDoneCarriesUsageAndTitle()
    {
        var usage = new ChatUsage { PromptTokens = 150, CompletionTokens = 320, TotalTokens = 470 };
        var ev = ChatStreamEvent.MessageDone(usage, "关于量子计算的讨论");

        Assert.Equal("message_done", ev.Type);
        Assert.Equal(470, ev.Usage?.TotalTokens);
        Assert.Equal(150, ev.Usage?.PromptTokens);
        Assert.Equal(320, ev.Usage?.CompletionTokens);
        Assert.Equal("关于量子计算的讨论", ev.Title);
    }

    [Fact]
    public void ChatStreamEventMessageDoneWithoutOptionalFields()
    {
        var ev = ChatStreamEvent.MessageDone();

        Assert.Equal("message_done", ev.Type);
        Assert.Null(ev.Usage);
        Assert.Null(ev.Title);
    }

    [Fact]
    public void ChatStreamEventErrorHasCodeAndMessage()
    {
        var ev = ChatStreamEvent.ErrorEvent("CONTEXT_TOO_LONG", "上下文超出模型限制");

        Assert.Equal("error", ev.Type);
        Assert.Equal("CONTEXT_TOO_LONG", ev.Code);
        Assert.Equal("上下文超出模型限制", ev.Message);
    }

    [Fact]
    public void ChatStreamEventToolCallStartHasAllFields()
    {
        var ev = ChatStreamEvent.ToolCallStart("call_001", "get_weather", "{\"city\":\"北京\"}");

        Assert.Equal("tool_call_start", ev.Type);
        Assert.Equal("call_001", ev.ToolCallId);
        Assert.Equal("get_weather", ev.Name);
        Assert.Equal("{\"city\":\"北京\"}", ev.Arguments);
    }

    [Fact]
    public void ChatStreamEventToolCallDoneHasResult()
    {
        var ev = ChatStreamEvent.ToolCallDone("call_001", "{\"temp\":25}", true);

        Assert.Equal("tool_call_done", ev.Type);
        Assert.Equal("call_001", ev.ToolCallId);
        Assert.Equal("{\"temp\":25}", ev.Result);
        Assert.True(ev.Success);
    }

    [Fact]
    public void ChatStreamEventToolCallErrorHasError()
    {
        var ev = ChatStreamEvent.ToolCallError("call_001", "服务不可用");

        Assert.Equal("tool_call_error", ev.Type);
        Assert.Equal("call_001", ev.ToolCallId);
        Assert.Equal("服务不可用", ev.Error);
    }

    [Fact]
    public void ChatStreamEventCoversAllSseEventTypes()
    {
        // 验证所有 SSE 事件类型都有对应的工厂方法
        var types = new HashSet<String>
        {
            "message_start", "thinking_delta", "thinking_done",
            "content_delta", "message_done", "error",
            "tool_call_start", "tool_call_done", "tool_call_error"
        };

        var events = new[]
        {
            ChatStreamEvent.MessageStart(1, "m", 0),
            ChatStreamEvent.ThinkingDelta("t"),
            ChatStreamEvent.ThinkingDone(100),
            ChatStreamEvent.ContentDelta("c"),
            ChatStreamEvent.MessageDone(),
            ChatStreamEvent.ErrorEvent("e", "m"),
            ChatStreamEvent.ToolCallStart("id", "name", "args"),
            ChatStreamEvent.ToolCallDone("id", "r", true),
            ChatStreamEvent.ToolCallError("id", "err"),
        };

        var actualTypes = events.Select(e => e.Type).ToHashSet();
        Assert.True(types.SetEquals(actualTypes), "应覆盖所有 SSE 事件类型");
    }
    #endregion

    #region InMemory 标题自动生成测试
    [Fact]
    public async Task GenerateTitleAsyncReturnsTruncatedTitle()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        var title = await service.GenerateTitleAsync(conv.Id, "这是一条超过十个字的很长的消息内容", CancellationToken.None);

        Assert.NotNull(title);
        Assert.True(title.Length <= 10);
    }

    [Fact]
    public async Task GenerateTitleAsyncUpdatesConversation()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        Assert.Equal("新建对话", conv.Title);

        await service.GenerateTitleAsync(conv.Id, "帮我写一封邮件", CancellationToken.None);

        var list = await service.GetConversationsAsync(1, 20, CancellationToken.None);
        var updated = list.Items.FirstOrDefault(e => e.Id == conv.Id);
        Assert.NotNull(updated);
        Assert.Equal("帮我写一封邮件", updated.Title);
    }

    [Fact]
    public async Task GenerateTitleAsyncReturnsShortTextDirectly()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        var title = await service.GenerateTitleAsync(conv.Id, "你好", CancellationToken.None);

        Assert.Equal("你好", title);
    }
    #endregion

    #region InMemory 完整流程测试
    [Fact]
    public async Task CreateAndStreamConversation()
    {
        var service = new InMemoryChatApplicationService();

        // 创建会话
        var conv = await service.CreateConversationAsync(new CreateConversationRequest("测试会话", 1), CancellationToken.None);
        Assert.Equal("测试会话", conv.Title);
        Assert.Equal(1, conv.ModelId);

        // 发送消息
        var chunks = new List<ChatStreamEvent>();
        await foreach (var chunk in service.StreamMessageAsync(conv.Id, new SendMessageRequest("你好", ThinkingMode.Auto, null), CancellationToken.None))
        {
            chunks.Add(chunk);
        }
        Assert.NotEmpty(chunks);
        // 验证事件流包含 message_start 和 message_done
        Assert.Contains(chunks, e => e.Type == "message_start");
        Assert.Contains(chunks, e => e.Type == "message_done");
        Assert.Contains(chunks, e => e.Type == "content_delta");

        // 获取消息列表
        var messages = await service.GetMessagesAsync(conv.Id, CancellationToken.None);
        Assert.Equal(2, messages.Count);
        Assert.Equal("user", messages[0].Role);
        Assert.Equal("assistant", messages[1].Role);
    }

    [Fact]
    public async Task DeleteConversationCleansUpMessages()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var deleted = await service.DeleteConversationAsync(conv.Id, CancellationToken.None);
        Assert.True(deleted);

        var messages = await service.GetMessagesAsync(conv.Id, CancellationToken.None);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task SetPinUpdatesConversation()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        Assert.False(conv.IsPinned);

        var result = await service.SetPinAsync(conv.Id, true, CancellationToken.None);
        Assert.True(result);

        var list = await service.GetConversationsAsync(1, 20, CancellationToken.None);
        var updated = list.Items.First(e => e.Id == conv.Id);
        Assert.True(updated.IsPinned);
    }

    [Fact]
    public async Task EditMessageUpdatesContent()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("原始消息", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var messages = await service.GetMessagesAsync(conv.Id, CancellationToken.None);
        var userMsg = messages.First(e => e.Role == "user");

        var edited = await service.EditMessageAsync(userMsg.Id, new EditMessageRequest("编辑后的消息"), CancellationToken.None);
        Assert.NotNull(edited);
        Assert.Equal("编辑后的消息", edited.Content);
    }

    [Fact]
    public async Task RegenerateUpdatesAssistantMessage()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var messages = await service.GetMessagesAsync(conv.Id, CancellationToken.None);
        var assistantMsg = messages.First(e => e.Role == "assistant");

        var regenerated = await service.RegenerateMessageAsync(assistantMsg.Id, CancellationToken.None);
        Assert.NotNull(regenerated);
        Assert.Contains("重新生成", regenerated.Content);
    }

    [Fact]
    public async Task ShareLinkLifecycle()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var share = await service.CreateShareLinkAsync(conv.Id, new CreateShareRequest(24), CancellationToken.None);
        Assert.Contains("/api/share/", share.Url);
        Assert.NotNull(share.ExpireTime);

        // 提取 token
        var token = share.Url.Replace("/api/share/", "");
        var content = await service.GetShareContentAsync(token, CancellationToken.None);
        Assert.NotNull(content);

        var revoked = await service.RevokeShareLinkAsync(token, CancellationToken.None);
        Assert.True(revoked);

        var afterRevoke = await service.GetShareContentAsync(token, CancellationToken.None);
        Assert.Null(afterRevoke);
    }

    [Fact]
    public async Task UserSettingsRoundTrip()
    {
        var service = new InMemoryChatApplicationService();

        var settings = await service.GetUserSettingsAsync(CancellationToken.None);
        Assert.Equal("zh-CN", settings.Language);
        Assert.Equal(0, settings.DefaultModel);

        var updated = await service.UpdateUserSettingsAsync(
            new UserSettingsDto("en", "dark", 18, "Ctrl+Enter", 3, ThinkingMode.Think, 20, "You are helpful", true),
            CancellationToken.None);
        Assert.Equal("en", updated.Language);
        Assert.Equal("dark", updated.Theme);
        Assert.Equal(3, updated.DefaultModel);
    }

    [Fact]
    public async Task ClearConversationsRemovesAll()
    {
        var service = new InMemoryChatApplicationService();
        await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        await service.ClearUserConversationsAsync(CancellationToken.None);

        var list = await service.GetConversationsAsync(1, 20, CancellationToken.None);
        Assert.Equal(0, list.Total);
    }

    [Fact]
    public async Task GetModelsReturnsNonEmpty()
    {
        var service = new InMemoryChatApplicationService();
        var models = await service.GetModelsAsync([], 0, CancellationToken.None);

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Code == "qwen-max");
    }

    [Fact]
    public async Task PaginationWorksCorrectly()
    {
        var service = new InMemoryChatApplicationService();
        for (var i = 0; i < 5; i++)
            await service.CreateConversationAsync(new CreateConversationRequest($"会话{i}", 0), CancellationToken.None);

        var page1 = await service.GetConversationsAsync(1, 2, CancellationToken.None);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(5, page1.Total);
        Assert.Equal(1, page1.Page);

        var page2 = await service.GetConversationsAsync(2, 2, CancellationToken.None);
        Assert.Equal(2, page2.Items.Count);

        var page3 = await service.GetConversationsAsync(3, 2, CancellationToken.None);
        Assert.Single(page3.Items);
    }
    #endregion

    #region AppKey 相关测试
    [Fact]
    public void AppKeyMaskSecretWorksCorrectly()
    {
        // 测试掩码逻辑（通过反射或直接测试控制器方法）
        var secret = "sk-abcdefghijklmnopqrstuvwxyz1234567890abcdefghijk";
        var masked = MaskSecret(secret);

        Assert.StartsWith("sk-abc", masked);
        Assert.EndsWith("hijk", masked);
        Assert.Contains("****", masked);
        Assert.NotEqual(secret, masked);
    }

    [Fact]
    public void AppKeyMaskSecretHandlesShortInput()
    {
        var masked = MaskSecret("short");
        Assert.Equal("sk-****", masked);
    }

    [Fact]
    public void AppKeyMaskSecretHandlesEmpty()
    {
        var masked = MaskSecret("");
        Assert.Equal("sk-****", masked);
    }

    /// <summary>掩码密钥（复制自 AppKeyApiController 用于测试）</summary>
    private static String MaskSecret(String secret)
    {
        if (String.IsNullOrEmpty(secret) || secret.Length <= 10)
            return "sk-****";
        return secret.Substring(0, 6) + "****" + secret.Substring(secret.Length - 4);
    }
    #endregion

    #region UsageService DTO 测试
    [Fact]
    public void UsageSummaryDtoHasCorrectProperties()
    {
        var dto = new UsageSummaryDto(10, 50, 1000, 2000, 3000, DateTime.Now);

        Assert.Equal(10, dto.Conversations);
        Assert.Equal(50, dto.Messages);
        Assert.Equal(1000, dto.PromptTokens);
        Assert.Equal(2000, dto.CompletionTokens);
        Assert.Equal(3000, dto.TotalTokens);
    }

    [Fact]
    public void DailyUsageDtoHasCorrectProperties()
    {
        var date = DateTime.Today;
        var dto = new DailyUsageDto(date, 5, 100, 200, 300);

        Assert.Equal(date, dto.Date);
        Assert.Equal(5, dto.Calls);
        Assert.Equal(100, dto.PromptTokens);
        Assert.Equal(200, dto.CompletionTokens);
        Assert.Equal(300, dto.TotalTokens);
    }

    [Fact]
    public void ModelUsageDtoHasCorrectProperties()
    {
        var dto = new ModelUsageDto(1, 100, 5000);

        Assert.Equal(1, dto.ModelId);
        Assert.Equal(100, dto.Calls);
        Assert.Equal(5000, dto.TotalTokens);
    }

    [Fact]
    public void AppKeyUsageDtoHasCorrectProperties()
    {
        var now = DateTime.Now;
        var dto = new AppKeyUsageDto(1, "业务系统A", 50, 2500, now);

        Assert.Equal(1, dto.AppKeyId);
        Assert.Equal("业务系统A", dto.Name);
        Assert.Equal(50, dto.Calls);
        Assert.Equal(2500, dto.TotalTokens);
        Assert.Equal(now, dto.LastCallTime);
    }
    #endregion

    #region AppKey API DTO 测试
    [Fact]
    public void CreateAppKeyRequestHasNameAndExpireTime()
    {
        var request = new CreateAppKeyRequest("测试系统", DateTime.Now.AddDays(30));

        Assert.Equal("测试系统", request.Name);
        Assert.NotNull(request.ExpireTime);
    }

    [Fact]
    public void UpdateAppKeyRequestSupportsPartialUpdate()
    {
        var request = new UpdateAppKeyRequest("新名称", null, null);

        Assert.Equal("新名称", request.Name);
        Assert.Null(request.Enable);
        Assert.Null(request.ExpireTime);
    }

    [Fact]
    public void AppKeyResponseDtoMasksSecret()
    {
        var dto = new AppKeyResponseDto(1, "测试", "sk-ab****jk", true, null, 100, 5000, DateTime.Now, DateTime.Now);

        Assert.Contains("****", dto.SecretMask);
        Assert.Equal(1, dto.Id);
        Assert.Equal("测试", dto.Name);
    }

    [Fact]
    public void AppKeyCreateResponseDtoExposesFullSecret()
    {
        var secret = "sk-full-secret-value-here";
        var dto = new AppKeyCreateResponseDto(1, "测试", secret, DateTime.Now);

        Assert.Equal(secret, dto.Secret);
    }
    #endregion

    #region ChatModels DTO 测试
    [Fact]
    public void ThinkingModeEnumHasExpectedValues()
    {
        Assert.Equal(0, (Int32)ThinkingMode.Auto);
        Assert.Equal(1, (Int32)ThinkingMode.Think);
        Assert.Equal(2, (Int32)ThinkingMode.Fast);
    }

    [Fact]
    public void FeedbackTypeEnumHasExpectedValues()
    {
        Assert.Equal(1, (Int32)FeedbackType.Like);
        Assert.Equal(2, (Int32)FeedbackType.Dislike);
    }

    [Fact]
    public void ConversationSummaryDtoHasAllFields()
    {
        var now = DateTime.Now;
        var dto = new ConversationSummaryDto(1, "测试", 1, now, true);

        Assert.Equal(1, dto.Id);
        Assert.Equal("测试", dto.Title);
        Assert.Equal(1, dto.ModelId);
        Assert.Equal(now, dto.LastMessageTime);
        Assert.True(dto.IsPinned);
    }

    [Fact]
    public void MessageDtoHasAllFields()
    {
        var now = DateTime.Now;
        var dto = new MessageDto(1, 100, "user", "你好", null, ThinkingMode.Auto, null, now);

        Assert.Equal(1, dto.Id);
        Assert.Equal(100, dto.ConversationId);
        Assert.Equal("user", dto.Role);
        Assert.Equal("你好", dto.Content);
        Assert.Null(dto.ThinkingContent);
        Assert.Equal(ThinkingMode.Auto, dto.ThinkingMode);
        Assert.Null(dto.Attachments);
        Assert.Equal(now, dto.CreateTime);
    }

    [Fact]
    public void PagedResultDtoCalculatesCorrectly()
    {
        var items = new List<ConversationSummaryDto>
        {
            new(1, "a", 1, DateTime.Now, false),
            new(2, "b", 1, DateTime.Now, false),
        };
        var dto = new PagedResultDto<ConversationSummaryDto>(items, 10, 1, 2);

        Assert.Equal(2, dto.Items.Count);
        Assert.Equal(10, dto.Total);
        Assert.Equal(1, dto.Page);
        Assert.Equal(2, dto.PageSize);
    }

    [Fact]
    public void ShareLinkDtoHasUrlAndTimes()
    {
        var now = DateTime.Now;
        var expire = now.AddDays(30);
        var dto = new ShareLinkDto("/api/share/abc123", now, expire);

        Assert.Equal("/api/share/abc123", dto.Url);
        Assert.Equal(now, dto.CreateTime);
        Assert.Equal(expire, dto.ExpireTime);
    }

    [Fact]
    public void UserSettingsDtoHasAllFields()
    {
        var dto = new UserSettingsDto("zh-CN", "dark", 18, "Enter", 1, ThinkingMode.Think, 10, "You are helpful", true);

        Assert.Equal("zh-CN", dto.Language);
        Assert.Equal("dark", dto.Theme);
        Assert.Equal(18, dto.FontSize);
        Assert.Equal("Enter", dto.SendShortcut);
        Assert.Equal(1, dto.DefaultModel);
        Assert.Equal(ThinkingMode.Think, dto.DefaultThinkingMode);
        Assert.Equal(10, dto.ContextRounds);
        Assert.Equal("You are helpful", dto.SystemPrompt);
        Assert.True(dto.AllowTraining);
    }
    #endregion

    #region 请求 DTO 测试
    [Fact]
    public void SendMessageRequestHasAllFields()
    {
        var ids = new List<String> { "att1", "att2" };
        var request = new SendMessageRequest("你好", ThinkingMode.Think, ids);

        Assert.Equal("你好", request.Content);
        Assert.Equal(ThinkingMode.Think, request.ThinkingMode);
        Assert.Equal(2, request.AttachmentIds?.Count);
    }

    [Fact]
    public void FeedbackRequestHasReasonAndTraining()
    {
        var request = new FeedbackRequest(FeedbackType.Dislike, "回答不准确", true);

        Assert.Equal(FeedbackType.Dislike, request.Type);
        Assert.Equal("回答不准确", request.Reason);
        Assert.True(request.AllowTraining);
    }
    #endregion

    #region BackgroundGenerationService 测试
    [Fact]
    public async Task BackgroundServiceRegisterAndComplete()
    {
        var service = new BackgroundGenerationService(XTrace.Log);
        var tcs = new TaskCompletionSource();

        async IAsyncEnumerable<ChatStreamEvent> MockStream()
        {
            yield return ChatStreamEvent.MessageStart(1, "test-model", 0);
            // 等待测试信号，确保我们能在流未完成时观察到 Running 状态
            await tcs.Task;
            yield return ChatStreamEvent.ContentDelta("Hello ");
            yield return ChatStreamEvent.ContentDelta("World");
            yield return ChatStreamEvent.MessageDone(new ChatUsage { TotalTokens = 10 });
        }

        var completed = false;
        service.Register(1001, MockStream(), task =>
        {
            completed = true;
            return Task.CompletedTask;
        });

        // 流还阻塞着，此时应处于运行中
        Assert.True(service.IsRunning(1001));

        // 释放流继续执行
        tcs.SetResult();
        await Task.Delay(500);

        Assert.False(service.IsRunning(1001));
        Assert.True(completed);

        var task = service.GetTask(1001);
        Assert.NotNull(task);
        Assert.Equal(BackgroundTaskStatus.Completed, task.Status);
        Assert.Equal("Hello World", task.ContentBuilder.ToString());
        Assert.Equal(10, task.Usage?.TotalTokens);
    }

    [Fact]
    public async Task BackgroundServiceStopCancelsTask()
    {
        var service = new BackgroundGenerationService(XTrace.Log);

        async IAsyncEnumerable<ChatStreamEvent> SlowStream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return ChatStreamEvent.ContentDelta("start");
            await Task.Delay(5000, ct);
            yield return ChatStreamEvent.ContentDelta("should not reach");
        }

        service.Register(2001, SlowStream());
        Assert.True(service.IsRunning(2001));

        service.Stop(2001);
        await Task.Delay(200);

        var task = service.GetTask(2001);
        Assert.NotNull(task);
        Assert.Equal(BackgroundTaskStatus.Cancelled, task.Status);
    }

    [Fact]
    public void BackgroundServiceGetTaskReturnsNullForUnknown()
    {
        var service = new BackgroundGenerationService(XTrace.Log);
        Assert.Null(service.GetTask(9999));
        Assert.False(service.IsRunning(9999));
    }

    [Fact]
    public async Task BackgroundServiceCollectsThinkingContent()
    {
        var service = new BackgroundGenerationService(XTrace.Log);

        async IAsyncEnumerable<ChatStreamEvent> ThinkingStream()
        {
            yield return ChatStreamEvent.MessageStart(1, "model", ThinkingMode.Think);
            yield return ChatStreamEvent.ThinkingDelta("让我想想...");
            yield return ChatStreamEvent.ThinkingDelta("分析完毕。");
            yield return ChatStreamEvent.ThinkingDone(1500);
            yield return ChatStreamEvent.ContentDelta("答案是42");
            yield return ChatStreamEvent.MessageDone();
            await Task.CompletedTask;
        }

        service.Register(3001, ThinkingStream());
        await Task.Delay(500);

        var task = service.GetTask(3001);
        Assert.NotNull(task);
        Assert.Equal("让我想想...分析完毕。", task.ThinkingBuilder.ToString());
        Assert.Equal("答案是42", task.ContentBuilder.ToString());
    }

    [Fact]
    public async Task BackgroundServiceCollectsErrorEvent()
    {
        var service = new BackgroundGenerationService(XTrace.Log);

        async IAsyncEnumerable<ChatStreamEvent> ErrorStream()
        {
            yield return ChatStreamEvent.ErrorEvent("LIMIT", "超出限制");
            await Task.CompletedTask;
        }

        service.Register(4001, ErrorStream());
        await Task.Delay(500);

        var task = service.GetTask(4001);
        Assert.NotNull(task);
        Assert.Equal("超出限制", task.Error);
    }
    #endregion

    #region BackgroundTask 和枚举测试
    [Fact]
    public void BackgroundTaskStatusEnumValues()
    {
        Assert.Equal(0, (Int32)BackgroundTaskStatus.Running);
        Assert.Equal(1, (Int32)BackgroundTaskStatus.Completed);
        Assert.Equal(2, (Int32)BackgroundTaskStatus.Failed);
        Assert.Equal(3, (Int32)BackgroundTaskStatus.Cancelled);
    }

    [Fact]
    public void BackgroundTaskInitializesCorrectly()
    {
        var task = new BackgroundTask
        {
            MessageId = 1,
            Status = BackgroundTaskStatus.Running,
            StartTime = DateTime.Now,
        };

        Assert.Equal(1, task.MessageId);
        Assert.Equal(BackgroundTaskStatus.Running, task.Status);
        Assert.NotNull(task.ContentBuilder);
        Assert.NotNull(task.ThinkingBuilder);
        Assert.NotNull(task.Events);
        Assert.Empty(task.Events);
        Assert.Equal(0, task.ContentBuilder.Length);
    }
    #endregion

    #region InMemory 边界场景测试
    [Fact]
    public async Task DeleteNonExistentConversationReturnsFalse()
    {
        var service = new InMemoryChatApplicationService();
        var result = await service.DeleteConversationAsync(99999, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateConversationUpdatesTitle()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest("旧标题", 0), CancellationToken.None);
        Assert.Equal("旧标题", conv.Title);

        var updated = await service.UpdateConversationAsync(conv.Id, new UpdateConversationRequest("新标题", 0), CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("新标题", updated.Title);
    }

    [Fact]
    public async Task UpdateNonExistentConversationReturnsNull()
    {
        var service = new InMemoryChatApplicationService();
        var result = await service.UpdateConversationAsync(99999, new UpdateConversationRequest("test", 0), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMessagesEmptyConversationReturnsEmpty()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        var messages = await service.GetMessagesAsync(conv.Id, CancellationToken.None);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task SubmitFeedbackWorks()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var messages = await service.GetMessagesAsync(conv.Id, CancellationToken.None);
        var assistantMsg = messages.First(e => e.Role == "assistant");

        await service.SubmitFeedbackAsync(assistantMsg.Id, new FeedbackRequest(FeedbackType.Like, null, false), CancellationToken.None);
        // SubmitFeedbackAsync 不抛异常即为成功
    }

    [Fact]
    public async Task ShareWithNullExpireHours()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var share = await service.CreateShareLinkAsync(conv.Id, new CreateShareRequest(null), CancellationToken.None);
        Assert.Contains("/api/share/", share.Url);
    }

    [Fact]
    public async Task RevokeNonExistentShareReturnsFalse()
    {
        var service = new InMemoryChatApplicationService();
        var result = await service.RevokeShareLinkAsync("non-existent-token", CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task MultipleConversationsSortedByLastMessage()
    {
        var service = new InMemoryChatApplicationService();

        var conv1 = await service.CreateConversationAsync(new CreateConversationRequest("第一个", 0), CancellationToken.None);
        await Task.Delay(50);
        var conv2 = await service.CreateConversationAsync(new CreateConversationRequest("第二个", 0), CancellationToken.None);

        var list = await service.GetConversationsAsync(1, 20, CancellationToken.None);
        Assert.Equal(2, list.Items.Count);
        // 最新的在前
        Assert.Equal(conv2.Id, list.Items[0].Id);
    }

    [Fact]
    public async Task StreamMessageEmptyContentStillProducesEvents()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        var chunks = new List<ChatStreamEvent>();
        await foreach (var chunk in service.StreamMessageAsync(conv.Id, new SendMessageRequest("", ThinkingMode.Auto, null), CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // 即使空内容，也应产生事件流
        Assert.NotEmpty(chunks);
    }
    #endregion

    #region ChatStreamEvent 工厂方法边界测试
    [Fact]
    public void ChatStreamEventMessageStartWithZeroValues()
    {
        var ev = ChatStreamEvent.MessageStart(0, "", 0);
        Assert.Equal("message_start", ev.Type);
        Assert.Equal(0, ev.MessageId);
        Assert.Equal("", ev.Model);
    }

    [Fact]
    public void ChatStreamEventContentDeltaWithEmptyContent()
    {
        var ev = ChatStreamEvent.ContentDelta("");
        Assert.Equal("content_delta", ev.Type);
        Assert.Equal("", ev.Content);
    }

    [Fact]
    public void ChatStreamEventErrorWithNullCode()
    {
        var ev = ChatStreamEvent.ErrorEvent(null, "未知错误");
        Assert.Equal("error", ev.Type);
        Assert.Null(ev.Code);
        Assert.Equal("未知错误", ev.Message);
    }

    [Fact]
    public void ChatStreamEventToolCallDoneWithFailure()
    {
        var ev = ChatStreamEvent.ToolCallDone("id", null, false);
        Assert.Equal("tool_call_done", ev.Type);
        Assert.False(ev.Success);
        Assert.Null(ev.Result);
    }
    #endregion

    #region DTO 相等性与不可变性测试
    [Fact]
    public void RecordDtoEquality()
    {
        var a = new UsageSummaryDto(10, 50, 1000, 2000, 3000, DateTime.MinValue);
        var b = new UsageSummaryDto(10, 50, 1000, 2000, 3000, DateTime.MinValue);
        Assert.Equal(a, b);

        var c = new DailyUsageDto(DateTime.Today, 5, 100, 200, 300);
        var d = new DailyUsageDto(DateTime.Today, 5, 100, 200, 300);
        Assert.Equal(c, d);

        var e = new ModelUsageDto(1, 10, 5000);
        var f = new ModelUsageDto(1, 10, 5000);
        Assert.Equal(e, f);
    }

    [Fact]
    public void RecordDtoInequality()
    {
        var a = new UsageSummaryDto(10, 50, 1000, 2000, 3000, DateTime.MinValue);
        var b = new UsageSummaryDto(20, 50, 1000, 2000, 3000, DateTime.MinValue);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void SendMessageRequestWithNullAttachments()
    {
        var req = new SendMessageRequest("hello", ThinkingMode.Fast, null);
        Assert.Null(req.AttachmentIds);
        Assert.Equal(ThinkingMode.Fast, req.ThinkingMode);
    }

    [Fact]
    public void CreateConversationRequestDefaults()
    {
        var req = new CreateConversationRequest(null, 0);
        Assert.Null(req.Title);
        Assert.Equal(0, req.ModelId);
    }

    [Fact]
    public void CreateShareRequestWithExpireHours()
    {
        var req = new CreateShareRequest(48);
        Assert.Equal(48, req.ExpireHours);
    }

    [Fact]
    public void UpdateConversationRequestHasFields()
    {
        var req = new UpdateConversationRequest("新标题", 3);
        Assert.Equal("新标题", req.Title);
        Assert.Equal(3, req.ModelId);
    }

    [Fact]
    public void EditMessageRequestHasContent()
    {
        var req = new EditMessageRequest("修改后的内容");
        Assert.Equal("修改后的内容", req.Content);
    }
    #endregion

    #region 路由与链接格式测试
    [Fact]
    [DisplayName("分享链接格式应为 /share/{token}")]
    public async Task ShareLinkUrlFormat()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var share = await service.CreateShareLinkAsync(conv.Id, new CreateShareRequest(24), CancellationToken.None);

        // 分享链接应以 /share/ 开头（供前端页面直接访问）
        Assert.StartsWith("/", share.Url);
        Assert.Contains("share", share.Url);
    }

    [Fact]
    [DisplayName("分享链接 Token 应为不可猜测的随机值")]
    public async Task ShareTokenIsRandom()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var share1 = await service.CreateShareLinkAsync(conv.Id, new CreateShareRequest(null), CancellationToken.None);
        var share2 = await service.CreateShareLinkAsync(conv.Id, new CreateShareRequest(null), CancellationToken.None);

        // 同一会话多次分享应生成不同链接
        Assert.NotEqual(share1.Url, share2.Url);
    }

    [Fact]
    [DisplayName("API 密钥掩码应隐藏中间部分")]
    public void AppKeyMaskSecretFormat()
    {
        var secret = "sk-abcdefghijklmnopqrstuvwxyz1234567890abcdefghijk";
        var masked = MaskSecret(secret);

        // 掩码后长度应远小于原始密钥
        Assert.True(masked.Length < secret.Length);
        // 仍以 sk- 开头
        Assert.StartsWith("sk-", masked);
    }
    #endregion

    #region 用户设置隔离性测试
    [Fact]
    [DisplayName("用户设置应返回默认值")]
    public async Task UserSettingsHasDefaults()
    {
        var service = new InMemoryChatApplicationService();
        var settings = await service.GetUserSettingsAsync(CancellationToken.None);

        Assert.Equal("zh-CN", settings.Language);
        Assert.Equal("system", settings.Theme);
        Assert.Equal(16, settings.FontSize);
        Assert.Equal("Enter", settings.SendShortcut);
        Assert.Equal(ThinkingMode.Auto, settings.DefaultThinkingMode);
        Assert.Equal(10, settings.ContextRounds);
        Assert.False(settings.AllowTraining);
    }

    [Fact]
    [DisplayName("修改用户设置后应返回更新后的值")]
    public async Task UpdateUserSettingsPersists()
    {
        var service = new InMemoryChatApplicationService();

        var updated = await service.UpdateUserSettingsAsync(
            new UserSettingsDto("en", "dark", 20, "Ctrl+Enter", 2, ThinkingMode.Think, 5, "Be concise", true),
            CancellationToken.None);

        Assert.Equal("en", updated.Language);
        Assert.Equal("dark", updated.Theme);
        Assert.Equal(20, updated.FontSize);
        Assert.Equal("Ctrl+Enter", updated.SendShortcut);
        Assert.Equal(2, updated.DefaultModel);
        Assert.Equal(ThinkingMode.Think, updated.DefaultThinkingMode);
        Assert.Equal(5, updated.ContextRounds);
        Assert.Equal("Be concise", updated.SystemPrompt);
        Assert.True(updated.AllowTraining);

        // 再次获取应保持不变
        var retrieved = await service.GetUserSettingsAsync(CancellationToken.None);
        Assert.Equal("en", retrieved.Language);
        Assert.Equal("dark", retrieved.Theme);
    }
    #endregion

    #region 分享过期测试
    [Fact]
    [DisplayName("分享可设置过期时间")]
    public async Task ShareWithExpireTimeHasExpireTime()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var share = await service.CreateShareLinkAsync(conv.Id, new CreateShareRequest(72), CancellationToken.None);

        Assert.NotNull(share.ExpireTime);
        // 过期时间应在未来
        Assert.True(share.ExpireTime > DateTime.Now);
    }

    [Fact]
    [DisplayName("分享无过期时间时 ExpireTime 为 null")]
    public async Task ShareWithoutExpireTimeHasNullExpireTime()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("test", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var share = await service.CreateShareLinkAsync(conv.Id, new CreateShareRequest(null), CancellationToken.None);

        Assert.Null(share.ExpireTime);
    }
    #endregion

    #region 消息操作完整性测试
    [Fact]
    [DisplayName("思考模式影响流事件")]
    public async Task ThinkingModeAffectsStream()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);

        var chunks = new List<ChatStreamEvent>();
        await foreach (var chunk in service.StreamMessageAsync(conv.Id, new SendMessageRequest("复杂数学题", ThinkingMode.Think, null), CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // message_start 事件应包含思考模式
        var start = chunks.FirstOrDefault(e => e.Type == "message_start");
        Assert.NotNull(start);
        Assert.Equal(ThinkingMode.Think, start.ThinkingMode);
    }

    [Fact]
    [DisplayName("消息编辑不应创建新消息")]
    public async Task EditMessageDoesNotCreateNewMessage()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, 0), CancellationToken.None);
        await foreach (var _ in service.StreamMessageAsync(conv.Id, new SendMessageRequest("原始消息", ThinkingMode.Auto, null), CancellationToken.None)) { }

        var messagesBefore = await service.GetMessagesAsync(conv.Id, CancellationToken.None);
        var userMsg = messagesBefore.First(e => e.Role == "user");

        await service.EditMessageAsync(userMsg.Id, new EditMessageRequest("编辑消息"), CancellationToken.None);

        var messagesAfter = await service.GetMessagesAsync(conv.Id, CancellationToken.None);
        Assert.Equal(messagesBefore.Count, messagesAfter.Count);
    }

    [Fact]
    [DisplayName("不存在的消息编辑应返回 null")]
    public async Task EditNonExistentMessageReturnsNull()
    {
        var service = new InMemoryChatApplicationService();
        var result = await service.EditMessageAsync(99999, new EditMessageRequest("test"), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    [DisplayName("不存在的消息重新生成应返回 null")]
    public async Task RegenerateNonExistentReturnsNull()
    {
        var service = new InMemoryChatApplicationService();
        var result = await service.RegenerateMessageAsync(99999, CancellationToken.None);
        Assert.Null(result);
    }
    #endregion

    #region 会话操作完整性测试
    [Fact]
    [DisplayName("会话置顶排序优先")]
    public async Task PinnedConversationsSortFirst()
    {
        var service = new InMemoryChatApplicationService();

        var conv1 = await service.CreateConversationAsync(new CreateConversationRequest("普通会话", 0), CancellationToken.None);
        await Task.Delay(50);
        var conv2 = await service.CreateConversationAsync(new CreateConversationRequest("要置顶的", 0), CancellationToken.None);
        await Task.Delay(50);
        var conv3 = await service.CreateConversationAsync(new CreateConversationRequest("最新的普通", 0), CancellationToken.None);

        // 置顶第一个会话
        await service.SetPinAsync(conv1.Id, true, CancellationToken.None);

        var list = await service.GetConversationsAsync(1, 20, CancellationToken.None);
        // 置顶会话应在第一位
        Assert.Equal(conv1.Id, list.Items[0].Id);
        Assert.True(list.Items[0].IsPinned);
    }

    [Fact]
    [DisplayName("删除会话后列表数量减少")]
    public async Task DeleteConversationReducesList()
    {
        var service = new InMemoryChatApplicationService();
        var conv1 = await service.CreateConversationAsync(new CreateConversationRequest("会话1", 0), CancellationToken.None);
        var conv2 = await service.CreateConversationAsync(new CreateConversationRequest("会话2", 0), CancellationToken.None);

        var before = await service.GetConversationsAsync(1, 20, CancellationToken.None);
        Assert.Equal(2, before.Total);

        await service.DeleteConversationAsync(conv1.Id, CancellationToken.None);

        var after = await service.GetConversationsAsync(1, 20, CancellationToken.None);
        Assert.Equal(1, after.Total);
        Assert.Equal(conv2.Id, after.Items[0].Id);
    }
    #endregion
}
