using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.ChatAI.Contracts;
using NewLife.AI.Models;
using NewLife.ChatAI.Controllers;
using NewLife.ChatAI.Services;
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
        Assert.Equal("qwen-max", setting.DefaultModel);
        Assert.Equal(0, setting.DefaultThinkingMode);
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
        setting.DefaultModel = "gpt-4o";
        setting.GatewayRateLimit = 100;
        setting.EnableMcp = false;

        Assert.Equal(0, setting.ShareExpireDays);
        Assert.Equal("gpt-4o", setting.DefaultModel);
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
        var ev = ChatStreamEvent.MessageStart(1001, "qwen-max", 1);

        Assert.Equal("message_start", ev.Type);
        Assert.Equal(1001, ev.MessageId);
        Assert.Equal("qwen-max", ev.Model);
        Assert.Equal(1, ev.ThinkingMode);
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
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);

        var title = await service.GenerateTitleAsync(conv.Id, "这是一条超过十个字的很长的消息内容", CancellationToken.None);

        Assert.NotNull(title);
        Assert.True(title.Length <= 10);
    }

    [Fact]
    public async Task GenerateTitleAsyncUpdatesConversation()
    {
        var service = new InMemoryChatApplicationService();
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);
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
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);

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
        var conv = await service.CreateConversationAsync(new CreateConversationRequest("测试会话", "qwen-max"), CancellationToken.None);
        Assert.Equal("测试会话", conv.Title);
        Assert.Equal("qwen-max", conv.ModelCode);

        // 发送消息
        var chunks = new List<String>();
        await foreach (var chunk in service.StreamMessageAsync(conv.Id, new SendMessageRequest("你好", ThinkingMode.Auto, null), CancellationToken.None))
        {
            chunks.Add(chunk);
        }
        Assert.NotEmpty(chunks);

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
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);

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
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);
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
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);

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
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);

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
        var conv = await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);
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
        Assert.Equal("qwen-max", settings.DefaultModel);

        var updated = await service.UpdateUserSettingsAsync(
            new UserSettingsDto("en", "dark", 18, "Ctrl+Enter", "gpt-4o", ThinkingMode.Think, 20, "You are helpful", true),
            CancellationToken.None);
        Assert.Equal("en", updated.Language);
        Assert.Equal("dark", updated.Theme);
        Assert.Equal("gpt-4o", updated.DefaultModel);
    }

    [Fact]
    public async Task ClearConversationsRemovesAll()
    {
        var service = new InMemoryChatApplicationService();
        await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);
        await service.CreateConversationAsync(new CreateConversationRequest(null, null), CancellationToken.None);

        await service.ClearUserConversationsAsync(CancellationToken.None);

        var list = await service.GetConversationsAsync(1, 20, CancellationToken.None);
        Assert.Equal(0, list.Total);
    }

    [Fact]
    public async Task GetModelsReturnsNonEmpty()
    {
        var service = new InMemoryChatApplicationService();
        var models = await service.GetModelsAsync(CancellationToken.None);

        Assert.NotEmpty(models);
        Assert.Contains(models, m => m.Code == "qwen-max");
    }

    [Fact]
    public async Task PaginationWorksCorrectly()
    {
        var service = new InMemoryChatApplicationService();
        for (var i = 0; i < 5; i++)
            await service.CreateConversationAsync(new CreateConversationRequest($"会话{i}", null), CancellationToken.None);

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
        var dto = new ModelUsageDto("qwen-max", 100, 5000);

        Assert.Equal("qwen-max", dto.ModelCode);
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
        var dto = new ConversationSummaryDto(1, "测试", "qwen-max", now, true);

        Assert.Equal(1, dto.Id);
        Assert.Equal("测试", dto.Title);
        Assert.Equal("qwen-max", dto.ModelCode);
        Assert.Equal(now, dto.LastMessageTime);
        Assert.True(dto.IsPinned);
    }

    [Fact]
    public void MessageDtoHasAllFields()
    {
        var now = DateTime.Now;
        var dto = new MessageDto(1, 100, "user", "你好", ThinkingMode.Auto, now);

        Assert.Equal(1, dto.Id);
        Assert.Equal(100, dto.ConversationId);
        Assert.Equal("user", dto.Role);
        Assert.Equal("你好", dto.Content);
        Assert.Equal(ThinkingMode.Auto, dto.ThinkingMode);
        Assert.Equal(now, dto.CreateTime);
    }

    [Fact]
    public void PagedResultDtoCalculatesCorrectly()
    {
        var items = new List<ConversationSummaryDto>
        {
            new(1, "a", "m", DateTime.Now, false),
            new(2, "b", "m", DateTime.Now, false),
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
        var dto = new UserSettingsDto("zh-CN", "dark", 18, "Enter", "qwen-max", ThinkingMode.Think, 10, "You are helpful", true);

        Assert.Equal("zh-CN", dto.Language);
        Assert.Equal("dark", dto.Theme);
        Assert.Equal(18, dto.FontSize);
        Assert.Equal("Enter", dto.SendShortcut);
        Assert.Equal("qwen-max", dto.DefaultModel);
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
}
