using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using NewLife.AI.Services;
using NewLife.AI.Tools;
using Xunit;

namespace XUnitTest.Tools;

/// <summary>ToolChatClient Token 预算守卫与工具步折叠测试。验证工具结果逐轮累积时按请求级 MaxInputTokens
/// 预算折叠早期工具步为摘要，消除每轮整包重发的二次方累积</summary>
[DisplayName("ToolChatClient上下文预算与折叠测试")]
public class ToolChatClientByteGuardTests
{
    /// <summary>返回指定大小中文大文本结果的工具提供者（LLM 受众），并统计真实执行次数</summary>
    private sealed class BigResultToolProvider(Int32 charCount = 3000) : IToolProvider
    {
        /// <summary>真实执行次数</summary>
        public Int32 CallCount { get; private set; }

        /// <inheritdoc/>
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null, Boolean includeSystem = true)
            => [new ChatTool { Function = new FunctionDefinition { Name = "big_result", Description = "返回大结果" } }];

        /// <inheritdoc/>
        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IToolResult>(new ToolResult(new String('大', charCount)));
        }
    }

    /// <summary>返回"头部标记 + 长正文 + 尾部标记"结果，用于验证超限截断保留头尾结论</summary>
    private sealed class HeadTailToolProvider : IToolProvider
    {
        /// <inheritdoc/>
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null, Boolean includeSystem = true)
            => [new ChatTool { Function = new FunctionDefinition { Name = "big_result", Description = "返回大结果" } }];

        /// <inheritdoc/>
        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IToolResult>(new ToolResult("HEAD_" + new String('中', 300) + "_TAIL"));
    }

    /// <summary>恒返回失败结果的工具提供者，并统计真实执行次数（验证同参失败跨轮去重）</summary>
    private sealed class FailingToolProvider : IToolProvider
    {
        /// <inheritdoc/>
        public IList<ChatTool> GetTools(ISet<String>? filterNames = null, Boolean includeSystem = true)
            => [new ChatTool { Function = new FunctionDefinition { Name = "big_result", Description = "返回失败" } }];

        /// <summary>真实执行次数</summary>
        public Int32 CallCount { get; private set; }

        /// <inheritdoc/>
        public Task<IToolResult> CallToolAsync(String toolName, String? arguments, ToolCallContext? context = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IToolResult>(ToolResult.ForAudiences("查询失败：数据库无连接", "错误码 ERR_CONN：数据库无连接，请修改查询条件或改用其他工具", true));
        }
    }

    /// <summary>前 toolRounds 轮返回工具调用、之后返回最终回复的假客户端；
    /// 记录每轮请求的消息数与调用次数。sameArgs=true 时每轮返回相同参数（模拟模型反复重试同一调用）</summary>
    private sealed class RecordingClient(Int32 toolRounds, Boolean sameArgs = false) : IChatClient
    {
        private Int32 _callCount;

        /// <summary>已发起的 LLM 调用次数</summary>
        public Int32 CallCount => _callCount;

        /// <summary>每轮收到的请求消息数</summary>
        public List<Int32> MsgCounts { get; } = [];

        /// <summary>每轮收到的 role=tool 消息内容（用于验证截断/预览策略）</summary>
        public List<String?> ToolMsgContents { get; } = [];

        /// <inheritdoc/>
        public Task<IChatResponse> GetResponseAsync(IChatRequest request, CancellationToken ct = default)
        {
            _callCount++;
            MsgCounts.Add(request.Messages.Count);
            ToolMsgContents.Add(request.Messages.LastOrDefault(m => m?.Role == "tool")?.Content as String);

            if (_callCount <= toolRounds)
            {
                var args = sameArgs ? "{\"q\":\"1\"}" : $"{{\"q\":\"{_callCount}\"}}";
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
                                        Id = $"call_{_callCount}",
                                        Type = "function",
                                        Function = new FunctionCall { Name = "big_result", Arguments = args }
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
                    new ChatChoice { Message = new ChatMessage { Role = "assistant", Content = $"最终答复{_callCount}" } }
                ]
            });
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<IChatResponse> GetStreamingResponseAsync(IChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        /// <inheritdoc/>
        public void Dispose() { }
    }

    /// <summary>构造启用 Token 预算的请求。maxInput≤0/缺省时不注入预算（守卫禁用）</summary>
    private static ChatRequest CreateRequest(Int32? maxInput = null)
    {
        var request = new ChatRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "请反复调用 big_result 直到完成" }],
        };
        if (maxInput is > 0) request["MaxInputTokens"] = maxInput;
        return request;
    }

    /// <summary>构造启用折叠的默认工具配置（结果不限长由守卫接管）</summary>
    private static ToolSetting CreateFoldToolSetting()
        => new() { ToolMaxIterations = 10, ToolResultMaxChars = 0 };

    [Fact]
    [DisplayName("多轮工具累积时折叠早期工具步，Token 受控且正常完成")]
    public async Task ToolChatClient_Fold_KeepsRequestBounded()
    {
        var innerClient = new RecordingClient(4);
        var client = new ToolChatClient(innerClient, new BigResultToolProvider(2000))
        {
            ToolSetting = CreateFoldToolSetting(),
        };

        // 每轮结果 2000 中文字 ≈ 2000 tokens；4 轮朴素累积约 8000+ tokens 远超 6000 预算。
        // 折叠使每轮请求受控于 折叠阈值（预算×0.6=3600）附近，消息数不随轮数膨胀
        var request = CreateRequest(6000);

        var response = await client.GetResponseAsync(request, default);
        var content = response.Messages?.FirstOrDefault()?.Message?.Content as String;

        Assert.StartsWith("最终答复", content);
        Assert.NotEqual(ToolLoopStopReason.ContextLimit, client.StopReason);
        Assert.Equal(5, innerClient.CallCount);
        // 折叠后早期组被压缩为摘要，最后请求消息数不随轮数线性膨胀（对照组 >8）
        Assert.True(innerClient.MsgCounts[^1] <= 6, $"折叠后消息数应受控，实际 {innerClient.MsgCounts[^1]}");
    }

    [Fact]
    [DisplayName("对照组：预算充足未达折叠阈值时消息逐轮累积不折叠")]
    public async Task ToolChatClient_NoFold_Accumulates()
    {
        var innerClient = new RecordingClient(4);
        var client = new ToolChatClient(innerClient, new BigResultToolProvider(2000))
        {
            ToolSetting = CreateFoldToolSetting(),
        };

        // 折叠比例内置 0.6，此处预算 60000 远高于 4 轮累积（每轮 2000 token 合计约 8000），
        // 未达 预算×0.6=36000 折叠阈值 → 折叠不触发，早期工具步仅截断不删除，消息数随轮数线性增长
        // （对照主测试的折叠受控形态，同时验证折叠在达到阈值前不误触发）
        var request = CreateRequest(60000);

        await client.GetResponseAsync(request, default);

        Assert.True(innerClient.MsgCounts[^1] >= 9, $"未达折叠阈值时消息数应膨胀，实际 {innerClient.MsgCounts[^1]}");
    }
    [Fact]
    [DisplayName("工具结果超限时头尾预览保留结论而非盲截头部")]
    public async Task ToolChatClient_TruncateResult_KeepsHeadAndTail()
    {
        var innerClient = new RecordingClient(1);
        var client = new ToolChatClient(innerClient, new HeadTailToolProvider())
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 10, ToolResultMaxChars = 40 },
        };

        // 结果 "HEAD_ + 300 中文字 + _TAIL" 远超 40 字符上限 → 应保留头部与尾部各半
        var request = CreateRequest();

        var response = await client.GetResponseAsync(request, default);
        var content = response.Messages?.FirstOrDefault()?.Message?.Content as String;

        Assert.Equal("最终答复2", content);
        var toolContent = innerClient.ToolMsgContents.LastOrDefault();
        Assert.NotNull(toolContent);
        Assert.Contains("HEAD_", toolContent);
        Assert.Contains("_TAIL", toolContent);
    }

    [Fact]
    [DisplayName("达到工具轮次上限时终止原因为MaxIterations")]
    public async Task ToolChatClient_MaxIterations_StopReason()
    {
        var innerClient = new RecordingClient(20); // 一直返回工具调用
        var client = new ToolChatClient(innerClient, new BigResultToolProvider(100))
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 3, ToolResultMaxChars = 0 },
        };

        var request = CreateRequest();

        await client.GetResponseAsync(request, default);

        Assert.Equal(ToolLoopStopReason.MaxIterations, client.StopReason);
    }

    [Fact]
    [DisplayName("同步达到轮次上限前执行满全部工具轮次而非提前丢弃")]
    public async Task ToolChatClient_MaxIterations_ExecutesAllRounds()
    {
        var provider = new BigResultToolProvider(100);
        var innerClient = new RecordingClient(20); // 一直返回工具调用
        var client = new ToolChatClient(innerClient, provider)
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 3, ToolResultMaxChars = 0 },
        };

        var request = CreateRequest();

        await client.GetResponseAsync(request, default);

        Assert.Equal(ToolLoopStopReason.MaxIterations, client.StopReason);
        // 模型第 3 次请求的工具调用也被真正执行并回传结果（原实现第 maxIterations 轮被提前丢弃），
        // 与流式 GetStreamingResponseAsync 的执行轮数保持一致
        Assert.Equal(3, provider.CallCount);
    }

    [Fact]
    [DisplayName("同参失败跨轮去重：不重复执行失败的相同调用")]
    public async Task ToolChatClient_FailedCallDedup_NoReExecute()
    {
        var provider = new FailingToolProvider();
        var innerClient = new RecordingClient(5, sameArgs: true); // 模型每轮都请求同一工具+参数
        var client = new ToolChatClient(innerClient, provider)
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 10, ToolResultMaxChars = 0 },
        };

        var request = CreateRequest();

        await client.GetResponseAsync(request, default);

        // 仅首次真实执行，后续 4 轮相同调用被失败去重拦截（回传失败原因引导模型改换思路）
        Assert.Equal(1, provider.CallCount);
        Assert.NotEqual(ToolLoopStopReason.MaxIterations, client.StopReason);
    }

    [Fact]
    [DisplayName("不同参数的工具调用仍正常执行不受失败去重影响")]
    public async Task ToolChatClient_FailedCallDedup_DifferentArgs_Executes()
    {
        var provider = new FailingToolProvider();
        var innerClient = new RecordingClient(3, sameArgs: false); // 每轮参数不同
        var client = new ToolChatClient(innerClient, provider)
        {
            ToolSetting = new ToolSetting { ToolMaxIterations = 10, ToolResultMaxChars = 0 },
        };

        var request = CreateRequest();

        await client.GetResponseAsync(request, default);

        // 参数不同视为新调用，应每次真实执行（连续失败 3 轮触发具名升级提示，但各轮均执行）
        Assert.Equal(3, provider.CallCount);
    }
}
