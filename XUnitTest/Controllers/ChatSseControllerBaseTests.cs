#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife.AI.Models;
using NewLife.ChatAI.Controllers;
using Xunit;

namespace XUnitTest.Controllers;

/// <summary>SSE 基类 <see cref="ChatSseControllerBase"/> 流式写循环单元测试</summary>
/// <remarks>复现生产缺陷：客户端断开（token 取消）时，<see cref="ChatSseControllerBase.StreamEventsAsync"/> 在途
/// <c>MoveNextAsync</c> 尚未完成即调用 <c>DisposeAsync</c>，导致编译器生成的 async iterator 释放逻辑
/// 抛出 <see cref="NotSupportedException"/>（Specified method is not supported）。</remarks>
[DisplayName("ChatSseControllerBase SSE 写循环测试")]
public class ChatSseControllerBaseTests
{
    /// <summary>暴露受保护的 <see cref="ChatSseControllerBase.StreamEventsAsync"/>，供单元测试驱动</summary>
    private sealed class TestSseController : ChatSseControllerBase
    {
        /// <summary>以默认错误码运行 SSE 写循环</summary>
        public Task RunAsync(IAsyncEnumerable<ChatStreamEvent> events, CancellationToken cancellationToken, Action<Exception>? onError = null)
            => StreamEventsAsync(events, cancellationToken, "STREAM_ERROR", onError);
    }

    /// <summary>确定性模拟编译器生成的 async iterator 释放语义：
    /// 第二次 <see cref="MoveNextAsync"/> 在途等待（响应取消），取消后延迟完成，确保写循环先进入 finally；
    /// 在途时调用 <see cref="DisposeAsync"/> 抛 <see cref="NotSupportedException"/>（与编译器生成行为一致）。</summary>
    private sealed class InFlightFakeStream : IAsyncEnumerable<ChatStreamEvent>, IAsyncEnumerator<ChatStreamEvent>
    {
        private readonly CancellationToken _ct;
        private readonly TaskCompletionSource _secondMoveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private Boolean _secondMoveInFlight;
        private Boolean _disposed;

        /// <summary>第二次 MoveNextAsync 已启动并在途等待（确定性同步点）</summary>
        public Task SecondMoveStarted => _secondMoveStarted.Task;

        /// <summary>迭代器是否已被成功释放（DisposeAsync 未抛异常）</summary>
        public Boolean Disposed => _disposed;

        public InFlightFakeStream(CancellationToken ct) => _ct = ct;

        public ChatStreamEvent Current { get; private set; } = null!;

        public IAsyncEnumerator<ChatStreamEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;

        public ValueTask<Boolean> MoveNextAsync()
        {
            if (Current == null)
            {
                // 首次：立即产出 message_start
                Current = ChatStreamEvent.MessageStart(1, "test-model");
                return ValueTask.FromResult(true);
            }

            // 第二次：进入在途等待，直到取消
            _secondMoveInFlight = true;
            _secondMoveStarted.TrySetResult();
            return new ValueTask<Boolean>(WaitForCancelAsync());
        }

        /// <summary>在途等待：令牌取消后延迟 100ms 再完成（模拟真实取消沿链路传播的延迟，
        /// 确保写循环在 DisposeAsync 判定时仍处于在途状态，确定性复现生产竞态）</summary>
        private async Task<Boolean> WaitForCancelAsync()
        {
            try
            {
                await Task.Delay(Timeout.Infinite, _ct).ConfigureAwait(false);
                return false;
            }
            catch (OperationCanceledException)
            {
                await Task.Delay(100).ConfigureAwait(false);
                return false;
            }
            finally
            {
                _secondMoveInFlight = false;
            }
        }

        public ValueTask DisposeAsync()
        {
            // 与编译器生成的 async iterator 一致：MoveNextAsync 在途时释放抛 NotSupportedException
            if (_secondMoveInFlight)
                throw new NotSupportedException();

            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>构造带内存响应体的 SSE 控制器</summary>
    private static TestSseController CreateController()
    {
        return new TestSseController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } },
            },
        };
    }

    [Fact]
    [DisplayName("StreamEventsAsync_客户端断开_在途MoveNextAsync不在途释放_不抛NotSupportedException")]
    public async Task StreamEventsAsync_CancelWhileMoveNextInFlight_NoNotSupportedException()
    {
        var controller = CreateController();
        using var cts = new CancellationTokenSource();
        var stream = new InFlightFakeStream(cts.Token);
        var errors = new List<Exception>();

        var task = controller.RunAsync(stream, cts.Token, errors.Add);

        // 等待第二次 MoveNextAsync 已启动并在途等待（确定性同步点，避免时序竞态）
        await stream.SecondMoveStarted.WaitAsync(TimeSpan.FromSeconds(5));

        // 模拟客户端断开：取消令牌
        cts.Cancel();

        // 写循环应正常结束，不得上报 NotSupportedException（修复前：在途 DisposeAsync 会抛）
        await task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.DoesNotContain(errors, e => e is NotSupportedException);
        // 迭代器应被成功释放（在途 MoveNextAsync 已等待完成后再释放）
        Assert.True(stream.Disposed);
    }
}
