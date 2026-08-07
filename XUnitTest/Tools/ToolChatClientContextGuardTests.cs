using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>ToolChatClient 逐轮上下文窗口预算守卫测试。验证工具结果逐轮累积时能按请求级 MaxInputTokens 预算中断循环</summary>
[DisplayName("ToolChatClient上下文预算守卫测试")]
public class ToolChatClientContextGuardTests
{
    /// <summary>返回大文本结果的工具提供者，用于撑大工具结果累积量</summary>
    private sealed class BigResultToolProvider : IToolProvider
    {
        /// <inheritdoc/>
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null, Boolean includeSystem = true)
            => [new ChatTool { Function = new FunctionDefinition { Name = "big_result", Description = "返回大结果" } }];

        /// <inheritdoc/>
        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IToolResult>(new ToolResult(new String('大', 3000)));
    }

    /// <summary>第一轮返回工具调用、第二轮返回最终回复的假客户端，并记录调用次数</summary>
    private sealed class ToolCallThenReplyCountingClient : IChatClient
    {
        private readonly String _toolName;
        private readonly String _toolArgs;
        private readonly String _finalReply;
        private Int32 _callCount;

        public Int32 CallCount => _callCount;

        public ToolCallThenReplyCountingClient(String toolName, String toolArgs, String finalReply)
        {
            _toolName = toolName;
            _toolArgs = toolArgs;
            _finalReply = finalReply;
        }

        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken ct = default)
        {
            _callCount++;
            if (_callCount == 1)
            {
                return Task.FromResult<IChatResponse>(new ChatResponse
                {
                    Messages =
                    [
                        new ChatChoice
                        {
                            Message = new ChatMessage
                            {
                                Role = "assistant",
                                Content = null,
                                ToolCalls =
                                [
                                    new ToolCall
                                    {
                                        Id = "call_001",
                                        Type = "function",
                                        Function = new FunctionCall { Name = _toolName, Arguments = _toolArgs }
                                    }
                                ]
                            }
                        }
                    ]
                });
            }

            return Task.FromResult<IChatResponse>(new ChatResponse
            {
                Messages =
                [
                    new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = _finalReply } }
                ]
            });
        }

        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public void Dispose() { }
    }

    /// <summary>构造启用预算的请求。budget 为 0 时不注入预算（守卫禁用）</summary>
    private static ChatRequest CreateRequest(Int32 budget)
    {
        var request = new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "请调用 big_result" }],
        };
        if (budget > 0)
            request["MaxInputTokens"] = budget;
        return request;
    }

    [Fact]
    [DisplayName("预算极小第一轮即中断")]
    public async Task ToolChatClient_ContextLimit_TinyBudget_StopsFirstRound()
    {
        var innerClient = new ToolCallThenReplyCountingClient("big_result", "{}", "已完成");
        var nativeClient = new ToolChatClient(innerClient, new BigResultToolProvider());

        var request = CreateRequest(5);
        Assert.Equal(5, request["MaxInputTokens"]);

        await nativeClient.GetResponseAsync(request, default);

        Assert.True(nativeClient.IsContextLimitExceeded);
        Assert.Equal(0, innerClient.CallCount);
    }

    [Fact]
    [DisplayName("工具结果累积超限时中断循环并置位")]
    public async Task ToolChatClient_ContextLimit_StopsWhenToolResultsAccumulate()
    {
        var innerClient = new ToolCallThenReplyCountingClient("big_result", "{}", "已完成");
        var nativeClient = new ToolChatClient(innerClient, new BigResultToolProvider());

        // 预算 100：第一轮（user 消息 + 工具 schema）通过；工具结果（3000 中文字）累积后第二轮超限
        var request = CreateRequest(100);

        await nativeClient.GetResponseAsync(request, default);

        Assert.True(nativeClient.IsContextLimitExceeded);
        // 第一轮 LLM 调用后工具结果累积超限，第二轮不再发起 LLM 调用（含兜底强制回答）
        Assert.Equal(1, innerClient.CallCount);
    }

    [Fact]
    [DisplayName("未超预算时正常完成工具循环")]
    public async Task ToolChatClient_ContextLimit_UnderLimit_Completes()
    {
        var innerClient = new ToolCallThenReplyCountingClient("big_result", "{}", "已完成");
        var nativeClient = new ToolChatClient(innerClient, new BigResultToolProvider());

        var request = CreateRequest(100_000);

        var response = await nativeClient.GetResponseAsync(request, default);
        var content = response.Messages?.FirstOrDefault()?.Message?.Content as String;

        Assert.Equal("已完成", content);
        Assert.False(nativeClient.IsContextLimitExceeded);
        Assert.Equal(2, innerClient.CallCount);
    }

    [Fact]
    [DisplayName("未设置预算时不启用守卫")]
    public async Task ToolChatClient_ContextLimit_NoBudget_Disabled()
    {
        var innerClient = new ToolCallThenReplyCountingClient("big_result", "{}", "已完成");
        var nativeClient = new ToolChatClient(innerClient, new BigResultToolProvider());

        var request = CreateRequest(0);

        var response = await nativeClient.GetResponseAsync(request, default);
        var content = response.Messages?.FirstOrDefault()?.Message?.Content as String;

        Assert.Equal("已完成", content);
        Assert.False(nativeClient.IsContextLimitExceeded);
        Assert.Equal(2, innerClient.CallCount);
    }
}
