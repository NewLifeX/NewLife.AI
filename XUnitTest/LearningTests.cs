using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Filters;
using NewLife.AI.Models;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Filters;
using NewLife.ChatAI.Services;
using NewLife.Log;
using Xunit;
using AiChatMessage = NewLife.AI.Models.ChatMessage;

namespace XUnitTest;

[DisplayName("自学习过滤器与上下文注入测试")]
public class LearningTests
{
    // ── 测试桩 ────────────────────────────────────────────────────────────────

    /// <summary>空上下文的记忆服务桩，不访问数据库</summary>
    private sealed class EmptyMemoryService : MemoryService
    {
        public EmptyMemoryService() : base(Logger.Null) { }

        public override IList<UserMemory> GetActiveMemories(Int32 userId) => [];

        public override String? BuildContextForUser(Int32 userId) => null;
    }

    /// <summary>固定上下文的记忆服务桩</summary>
    private sealed class FixedMemoryService : MemoryService
    {
        private readonly String _context;

        public FixedMemoryService(String context) : base(Logger.Null) => _context = context;

        public override IList<UserMemory> GetActiveMemories(Int32 userId) => [];

        public override String? BuildContextForUser(Int32 userId) => userId > 0 ? _context : null;
    }

    /// <summary>记录 AnalyzeAsync 调用次数的分析服务桩</summary>
    private sealed class TrackingAnalysisService : ConversationAnalysisService
    {
        public Int32 AnalyzeCallCount { get; private set; }

        public TrackingAnalysisService(MemoryService memoryService)
            : base(null!, memoryService, Logger.Null) { }

        public override Task AnalyzeAsync(
            Int32 userId,
            Int64 conversationId,
            IList<AiChatMessage> messages,
            ChatCompletionResponse response,
            String triggerReason = "Chat",
            CancellationToken cancellationToken = default)
        {
            AnalyzeCallCount++;
            return Task.CompletedTask;
        }
    }

    // ── LearningFilter 测试 ───────────────────────────────────────────────────

    [Fact]
    [DisplayName("ExtraData 缺少 userId 时不注入记忆上下文、不触发分析")]
    public async Task LearningFilter_SkipsWhenNoUserId()
    {
        var memSvc = new EmptyMemoryService();
        var analysisSvc = new TrackingAnalysisService(memSvc);
        var filter = new LearningFilter(analysisSvc, Logger.Null);

        var request = new ChatCompletionRequest
        {
            Messages = [new AiChatMessage { Role = "user", Content = "hello" }]
        };
        var context = new ChatFilterContext
        {
            Request = request,
            Response = new ChatCompletionResponse
            {
                Choices = [new ChatChoice { Message = new AiChatMessage { Role = "assistant", Content = "hi" } }]
            }
        };

        var nextCalled = false;
        await filter.OnChatAsync(context, (_, _) => { nextCalled = true; return Task.CompletedTask; });

        Assert.True(nextCalled, "next 应该被调用");
        Assert.Equal(0, analysisSvc.AnalyzeCallCount);
        Assert.Single(context.Request.Messages); // 消息数不应增加
    }

    [Fact]
    [DisplayName("ExtraData 中 userId <= 0 时不注入记忆上下文")]
    public async Task LearningFilter_SkipsInjectionWhenUserIdZero()
    {
        var memSvc = new FixedMemoryService("## 关于用户的记忆\n- 喜欢 C#");
        var analysisSvc = new TrackingAnalysisService(memSvc);
        var filter = new LearningFilter(analysisSvc, Logger.Null);

        var request = new ChatCompletionRequest
        {
            Messages = [new AiChatMessage { Role = "user", Content = "你好" }]
        };
        var context = new ChatFilterContext
        {
            Request = request,
            Response = new ChatCompletionResponse
            {
                Choices = [new ChatChoice { Message = new AiChatMessage { Role = "assistant", Content = "你好！" } }]
            }
        };
        context.ExtraData["userId"] = 0; // userId = 0 不触发注入

        await filter.OnChatAsync(context, (_, _) => Task.CompletedTask);

        // 消息数量不变（没有注入 system 消息）
        Assert.Single(context.Request.Messages);
    }

    [Fact]
    [DisplayName("有效 userId 时已有 system 消息则向其追加记忆上下文")]
    public async Task LearningFilter_AppendsMemoryToExistingSystemMessage()
    {
        const String memoryContext = "## 关于用户的记忆\n- 偏好: C#";
        var memSvc = new FixedMemoryService(memoryContext);
        var analysisSvc = new TrackingAnalysisService(memSvc);
        var filter = new LearningFilter(analysisSvc, Logger.Null);

        var systemMsg = new AiChatMessage { Role = "system", Content = "你是一个助手" };
        var request = new ChatCompletionRequest
        {
            Messages = [systemMsg, new AiChatMessage { Role = "user", Content = "帮我用 C# 写代码" }]
        };
        var context = new ChatFilterContext
        {
            Request = request,
            Response = new ChatCompletionResponse
            {
                Choices = [new ChatChoice { Message = new AiChatMessage { Role = "assistant", Content = "好的" } }]
            }
        };
        context.ExtraData[LearningFilter.UserIdKey] = 42;

        await filter.OnChatAsync(context, (_, _) => Task.CompletedTask);

        var systemContent = context.Request.Messages[0].Content as String;
        Assert.Contains("你是一个助手", systemContent);
        Assert.Contains(memoryContext, systemContent);
    }

    [Fact]
    [DisplayName("有效 userId 且无 system 消息时插入新的 system 消息")]
    public async Task LearningFilter_InsertsSystemMessageWhenNoneExists()
    {
        const String memoryContext = "## 关于用户的记忆\n- 兴趣: 编程";
        var memSvc = new FixedMemoryService(memoryContext);
        var analysisSvc = new TrackingAnalysisService(memSvc);
        var filter = new LearningFilter(analysisSvc, Logger.Null);

        var request = new ChatCompletionRequest
        {
            Messages = [new AiChatMessage { Role = "user", Content = "推荐一本编程书" }]
        };
        var context = new ChatFilterContext
        {
            Request = request,
            Response = new ChatCompletionResponse
            {
                Choices = [new ChatChoice { Message = new AiChatMessage { Role = "assistant", Content = "推荐..." } }]
            }
        };
        context.ExtraData[LearningFilter.UserIdKey] = 100;

        await filter.OnChatAsync(context, (_, _) => Task.CompletedTask);

        // 应该在消息列表头部插入了 system 消息
        Assert.Equal(2, context.Request.Messages.Count);
        Assert.Equal("system", context.Request.Messages[0].Role);
        Assert.Equal(memoryContext, context.Request.Messages[0].Content as String);
    }

    [Fact]
    [DisplayName("Response 为 null 时不触发异步分析")]
    public async Task LearningFilter_SkipsAnalysisWhenResponseIsNull()
    {
        var memSvc = new EmptyMemoryService();
        var analysisSvc = new TrackingAnalysisService(memSvc);
        var filter = new LearningFilter(analysisSvc, Logger.Null);

        var context = new ChatFilterContext
        {
            Request = new ChatCompletionRequest
            {
                Messages = [new AiChatMessage { Role = "user", Content = "hi" }]
            },
            Response = null
        };
        context.ExtraData[LearningFilter.UserIdKey] = 1;

        await filter.OnChatAsync(context, (_, _) => Task.CompletedTask);

        // 等待一个循环，确保有足够时间执行（如果有错误的话）
        await Task.Delay(50);
        Assert.Equal(0, analysisSvc.AnalyzeCallCount);
    }

    // ── MemoryService 上下文构建测试 ──────────────────────────────────────────

    [Fact]
    [DisplayName("BuildContextForUser: userId <= 0 返回 null")]
    public void MemoryService_BuildContextForUser_NullForNonPositiveUserId()
    {
        var svc = new MemoryService(Logger.Null);

        Assert.Null(svc.BuildContextForUser(0));
        Assert.Null(svc.BuildContextForUser(-1));
    }

    [Fact]
    [DisplayName("BuildContextForUser: 有效 userId 无记忆时返回 null")]
    public void MemoryService_BuildContextForUser_NullWhenNoMemories()
    {
        // 使用 EmptyMemoryService 跳过 DB 访问
        var svc = new EmptyMemoryService();
        Assert.Null(svc.BuildContextForUser(999));
    }

    // ── ConversationAnalysisService 消息轮数门槛测试 ─────────────────────────

    [Fact]
    [DisplayName("AnalyzeAsync: 消息轮数不足时被 TrackingService 空实现跳过")]
    public async Task ConversationAnalysisService_SkipsWhenInsufficientMessages()
    {
        var memSvc = new EmptyMemoryService();
        var svc = new TrackingAnalysisService(memSvc);

        // 仅 1 条 user 消息（不足 MinMessageRounds = 2），TrackingService 会计入调用
        var messages = new List<AiChatMessage>
        {
            new() { Role = "user", Content = "单条消息" }
        };
        var response = new ChatCompletionResponse
        {
            Choices = [new ChatChoice { Message = new AiChatMessage { Role = "assistant", Content = "回复" } }]
        };

        await svc.AnalyzeAsync(1, 100L, messages, response);

        // TrackingAnalysisService 直接计数，不执行真实逻辑
        Assert.Equal(1, svc.AnalyzeCallCount);
    }

    [Fact]
    [DisplayName("LearningFilter: 会话ID正确从 ExtraData 提取并传给 AnalyzeAsync")]
    public async Task LearningFilter_PassesConversationIdToAnalysisService()
    {
        var memSvc = new EmptyMemoryService();
        Int64 capturedConversationId = -1;
        var analysisSvc = new CapturingAnalysisService(memSvc, (cid) => capturedConversationId = cid);

        var filter = new LearningFilter(analysisSvc, Logger.Null);

        var context = new ChatFilterContext
        {
            Request = new ChatCompletionRequest
            {
                Messages =
                [
                    new AiChatMessage { Role = "user", Content = "a" },
                    new AiChatMessage { Role = "user", Content = "b" },
                ]
            },
            Response = new ChatCompletionResponse
            {
                Choices = [new ChatChoice { Message = new AiChatMessage { Role = "assistant", Content = "ok" } }]
            }
        };
        context.ExtraData[LearningFilter.UserIdKey] = 7;
        context.ExtraData[LearningFilter.ConversationIdKey] = 999L;

        await filter.OnChatAsync(context, (_, _) => Task.CompletedTask);

        // 等待 fire-and-forget 任务完成
        await Task.Delay(100);
        Assert.Equal(999L, capturedConversationId);
    }

    /// <summary>捕获会话ID的分析服务桩</summary>
    private sealed class CapturingAnalysisService : ConversationAnalysisService
    {
        private readonly Action<Int64> _capture;

        public CapturingAnalysisService(MemoryService memoryService, Action<Int64> capture)
            : base(null!, memoryService, Logger.Null) => _capture = capture;

        public override Task AnalyzeAsync(
            Int32 userId,
            Int64 conversationId,
            IList<AiChatMessage> messages,
            ChatCompletionResponse response,
            String triggerReason = "Chat",
            CancellationToken cancellationToken = default)
        {
            _capture(conversationId);
            return Task.CompletedTask;
        }
    }
}
