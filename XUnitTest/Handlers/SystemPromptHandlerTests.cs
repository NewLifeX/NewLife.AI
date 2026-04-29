using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.ChatAI.Entity;
using NewLife.ChatAI.Handlers;
using NewLife.ChatAI.Services;
using Xunit;

namespace XUnitTest.Handlers;

/// <summary>SystemPromptHandler 单元测试：OnBefore 向 ContextMessages 注入 system 消息</summary>
[DisplayName("SystemPromptHandler 测试")]
public class SystemPromptHandlerTests
{
    #region OnBefore — BuildSystemPromptAsync 返回 null 时不注入

    [Fact]
    [DisplayName("OnBefore—基类 BuildSystemPromptAsync 返回 null，不插入 system 消息")]
    public async Task OnBefore_NullAddition_NoSystemMessageInjected()
    {
        var handler = new SystemPromptHandler(null);
        var ctx = BuildContext();

        await handler.OnBefore(ctx, CancellationToken.None);

        Assert.Empty(ctx.ContextMessages);
        Assert.Null(ctx.SystemPrompt);
    }

    [Fact]
    [DisplayName("OnBefore—基类 BuildSystemPromptAsync 返回 null，原有 system 消息不变")]
    public async Task OnBefore_NullAddition_ExistingSystemMessageUnchanged()
    {
        var handler = new SystemPromptHandler(null);
        var ctx = BuildContext(existingSystem: "已有系统提示词");

        await handler.OnBefore(ctx, CancellationToken.None);

        Assert.Single(ctx.ContextMessages);
        Assert.Equal("已有系统提示词", ctx.SystemPrompt);
    }

    #endregion

    #region OnBefore — 注入到空上下文

    [Fact]
    [DisplayName("OnBefore—有追加内容且无 system 消息时插入到首位")]
    public async Task OnBefore_WithAddition_NoExistingSystem_InsertsAtFirst()
    {
        var handler = new StubSystemPromptHandler(null, "你是一个专业助手");
        var ctx = BuildContext(extraMessages: ["user:你好"]);

        await handler.OnBefore(ctx, CancellationToken.None);

        Assert.Equal(2, ctx.ContextMessages.Count);
        Assert.Equal("system", ctx.ContextMessages[0].Role);
        Assert.Equal("你是一个专业助手", ctx.ContextMessages[0].Content as String);
        Assert.Equal("你是一个专业助手", ctx.SystemPrompt);
    }

    [Fact]
    [DisplayName("OnBefore—有追加内容且无任何消息时插入 system 消息")]
    public async Task OnBefore_WithAddition_EmptyContext_InsertsSystem()
    {
        var handler = new StubSystemPromptHandler(null, "系统提示");
        var ctx = BuildContext();

        await handler.OnBefore(ctx, CancellationToken.None);

        Assert.Single(ctx.ContextMessages);
        Assert.Equal("system", ctx.ContextMessages[0].Role);
        Assert.Equal("系统提示", ctx.SystemPrompt);
    }

    #endregion

    #region OnBefore — 追加到已有 system 消息

    [Fact]
    [DisplayName("OnBefore—已有 system 消息时在末尾追加（两段间隔空行）")]
    public async Task OnBefore_WithAddition_ExistingSystem_Appends()
    {
        var handler = new StubSystemPromptHandler(null, "商用增强内容");
        var ctx = BuildContext(existingSystem: "基础提示词");

        await handler.OnBefore(ctx, CancellationToken.None);

        Assert.Single(ctx.ContextMessages); // 只有一条 system 消息
        var systemContent = ctx.ContextMessages[0].Content as String;
        Assert.Contains("基础提示词", systemContent);
        Assert.Contains("商用增强内容", systemContent);
        Assert.Equal("基础提示词\n\n商用增强内容", systemContent);
    }

    [Fact]
    [DisplayName("OnBefore—已有 system 消息且追加内容时 SystemPrompt 为合并后内容")]
    public async Task OnBefore_WithAddition_ExistingSystem_SystemPromptIsMerged()
    {
        var handler = new StubSystemPromptHandler(null, "附加内容");
        var ctx = BuildContext(existingSystem: "原始内容");

        await handler.OnBefore(ctx, CancellationToken.None);

        Assert.Equal("原始内容\n\n附加内容", ctx.SystemPrompt);
    }

    #endregion

    #region OnAfter — 总是返回 CompletedTask

    [Fact]
    [DisplayName("OnAfter—始终立即返回，不抛异常")]
    public async Task OnAfter_AlwaysReturnsImmediately()
    {
        var handler = new SystemPromptHandler(null);
        await handler.OnAfter(BuildContext(), CancellationToken.None);
    }

    #endregion

    // ── 工厂方法 ──────────────────────────────────────────────────────────

    private static MessageFlowContext BuildContext(String? existingSystem = null, IEnumerable<String>? extraMessages = null)
    {
        var ctx = new MessageFlowContext
        {
            Conversation = new Conversation(),
            ModelConfig = new ModelConfig(),
        };

        if (existingSystem != null)
            ctx.ContextMessages.Add(new AiChatMessage { Role = "system", Content = existingSystem });

        if (extraMessages != null)
        {
            foreach (var raw in extraMessages)
            {
                var parts = raw.Split(':', 2);
                ctx.ContextMessages.Add(new AiChatMessage { Role = parts[0], Content = parts.Length > 1 ? parts[1] : "" });
            }
        }

        return ctx;
    }

    // ── 测试辅助：返回固定追加文本的 SystemPromptHandler 子类 ──────────────

    private sealed class StubSystemPromptHandler(ITracer? tracer, String? addition)
        : SystemPromptHandler(tracer)
    {
        protected override Task<String?> BuildSystemPromptAsync(IChatContext context, CancellationToken cancellationToken)
            => Task.FromResult(addition);
    }
}
