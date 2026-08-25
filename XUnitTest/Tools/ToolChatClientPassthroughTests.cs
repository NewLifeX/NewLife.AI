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

/// <summary>ToolChatClient 混合工具分流测试。验证含客户端工具（无 Provider 路由）的轮次整轮透传不执行，
/// 仅全部为 StarChat 工具时才由服务端执行（网关领域模式 + 客户端自带工具场景）</summary>
[DisplayName("ToolChatClient混合工具分流测试")]
public class ToolChatClientPassthroughTests
{
    /// <summary>提供 StarChat 侧工具（server_tool）的提供者，记录服务端调用次数</summary>
    private sealed class ServerToolProvider : IToolProvider
    {
        /// <summary>服务端执行次数</summary>
        public Int32 CallCount { get; private set; }

        /// <inheritdoc/>
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null, Boolean includeSystem = true)
            => [new ChatTool { Function = new FunctionDefinition { Name = "server_tool", Description = "服务端工具" } }];

        /// <inheritdoc/>
        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IToolResult>(new ToolResult("服务端执行成功"));
        }
    }

    /// <summary>按脚本返回响应的假客户端：前 N 轮返回指定 tool_calls，后续返回最终回复，并记录调用次数</summary>
    private sealed class ScriptedClient : IChatClient
    {
        private readonly IList<String[][]> _roundToolCalls;
        private readonly String _finalReply;
        private Int32 _callCount;

        /// <summary>LLM 调用次数</summary>
        public Int32 CallCount => _callCount;

        public ScriptedClient(IList<String[][]> roundToolCalls, String finalReply)
        {
            _roundToolCalls = roundToolCalls;
            _finalReply = finalReply;
        }

        /// <inheritdoc/>
        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken ct = default)
        {
            _callCount++;
            var idx = _callCount - 1;
            if (idx < _roundToolCalls.Count)
            {
                var calls = _roundToolCalls[idx]
                    .Select((c, i) => new ToolCall
                    {
                        Id = $"call_{idx}_{i}",
                        Type = "function",
                        Function = new FunctionCall { Name = c[0], Arguments = c[1] },
                    })
                    .ToList();
                return Task.FromResult<IChatResponse>(new ChatResponse
                {
                    Messages =
                    [
                        new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = null, ToolCalls = calls } }
                    ]
                });
            }

            return Task.FromResult<IChatResponse>(new ChatResponse
            {
                Messages = [new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = _finalReply } }]
            });
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>构造请求。客户端工具 client_tool 通过 options.Tools 注入（模拟业务方自带工具）</summary>
    private static ChatRequest CreateRequest()
    {
        var request = new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "请执行工具" }],
            Tools =
            [
                new ChatTool { Function = new FunctionDefinition { Name = "client_tool", Description = "客户端工具" } },
            ],
        };
        return request;
    }

    [Fact]
    [DisplayName("全为StarChat工具时服务端执行并继续循环")]
    public async Task AllServerTools_ExecutedServerSide()
    {
        var provider = new ServerToolProvider();
        var inner = new ScriptedClient(new[] { new[] { new[] { "server_tool", "{}" } } }, "已完成");
        var client = new ToolChatClient(inner, provider);

        var response = await client.GetResponseAsync(CreateRequest(), default);

        Assert.Equal(1, provider.CallCount);  // 服务端执行一次
        Assert.Equal(2, inner.CallCount);     // 工具轮 + 最终轮
        Assert.Equal("已完成", response.Text);
    }

    [Fact]
    [DisplayName("含客户端工具时整轮透传不执行")]
    public async Task ContainsClientTool_PassthroughWholeRound()
    {
        var provider = new ServerToolProvider();
        var inner = new ScriptedClient(new[] { new[] { new[] { "client_tool", "{\"q\":1}" } } }, "不应到达");
        var client = new ToolChatClient(inner, provider);

        var response = await client.GetResponseAsync(CreateRequest(), default);

        Assert.Equal(0, provider.CallCount);          // 服务端不执行
        Assert.Equal(1, inner.CallCount);             // 仅一次 LLM 调用，无循环
        var tc = response.Messages?.FirstOrDefault()?.Message?.ToolCalls;
        Assert.NotNull(tc);
        Assert.Equal("client_tool", tc![0].Function?.Name);  // 原样透传 tool_calls
    }

    [Fact]
    [DisplayName("混合轮（StarChat+客户端）整轮透传")]
    public async Task MixedRound_PassthroughWholeRound()
    {
        var provider = new ServerToolProvider();
        var inner = new ScriptedClient(new[] { new[] { new[] { "server_tool", "{}" }, new[] { "client_tool", "{}" } } }, "不应到达");
        var client = new ToolChatClient(inner, provider);

        var response = await client.GetResponseAsync(CreateRequest(), default);

        Assert.Equal(0, provider.CallCount);  // 含客户端工具 → 整轮透传，服务端不执行
        Assert.Equal(1, inner.CallCount);
        var names = response.Messages?.FirstOrDefault()?.Message?.ToolCalls?.Select(t => t.Function?.Name).ToList();
        Assert.Equal(new[] { "server_tool", "client_tool" }, names);  // 原样返回全部 tool_calls
    }
}
