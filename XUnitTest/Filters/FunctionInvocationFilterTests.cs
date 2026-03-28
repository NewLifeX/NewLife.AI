using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Filters;
using Xunit;

namespace XUnitTest.Filters;

/// <summary>FunctionInvocationContext 及洋葱链过滤器行为测试</summary>
[DisplayName("函数调用过滤器测试")]
public class FunctionInvocationFilterTests
{
    // ── FunctionInvocationContext 属性 ────────────────────────────────────

    [Fact]
    [DisplayName("FunctionInvocationContext—默认值正确")]
    public void FunctionInvocationContext_DefaultValues()
    {
        var ctx = new FunctionInvocationContext();

        Assert.Equal(String.Empty, ctx.FunctionName);
        Assert.Null(ctx.Arguments);
        Assert.Null(ctx.Result);
        Assert.NotNull(ctx.ExtraData);
        Assert.Empty(ctx.ExtraData);
    }

    [Fact]
    [DisplayName("FunctionInvocationContext—属性可读写")]
    public void FunctionInvocationContext_Properties_ReadWrite()
    {
        var ctx = new FunctionInvocationContext
        {
            FunctionName = "calculate",
            Arguments = "{\"expression\":\"2+3\"}",
            Result = "5",
        };
        ctx.ExtraData["key"] = "value";

        Assert.Equal("calculate", ctx.FunctionName);
        Assert.Equal("{\"expression\":\"2+3\"}", ctx.Arguments);
        Assert.Equal("5", ctx.Result);
        Assert.Equal("value", ctx.ExtraData["key"]);
    }

    // ── 单过滤器——修改结果 ────────────────────────────────────────────────

    [Fact]
    [DisplayName("IFunctionInvocationFilter—过滤器可在 next 之后修改 Result")]
    public async Task Filter_CanModifyResultAfterNext()
    {
        var ctx = new FunctionInvocationContext { FunctionName = "calc", Arguments = null };
        var filter = new MultiplyResultFilter(3);

        // next 把 Result 设为 "4"
        await filter.OnFunctionInvocationAsync(ctx, static (c, _) =>
        {
            c.Result = "4";
            return Task.CompletedTask;
        }, CancellationToken.None);

        // filter 应将 "4" * 3 → "12"
        Assert.Equal("12", ctx.Result);
    }

    [Fact]
    [DisplayName("IFunctionInvocationFilter—过滤器可在 next 之前修改 Arguments")]
    public async Task Filter_CanModifyArgumentsBeforeNext()
    {
        String capturedArgs = null;
        var ctx = new FunctionInvocationContext
        {
            FunctionName = "greet",
            Arguments = "{\"name\":\"Alice\"}",
        };
        var filter = new InjectArgumentFilter("{\"name\":\"Bob\"}");

        await filter.OnFunctionInvocationAsync(ctx, (c, _) =>
        {
            capturedArgs = c.Arguments;
            return Task.CompletedTask;
        }, CancellationToken.None);

        // next 收到已被改写的 Arguments
        Assert.Equal("{\"name\":\"Bob\"}", capturedArgs);
    }

    // ── 短路——不调用 next ──────────────────────────────────────────────────

    [Fact]
    [DisplayName("IFunctionInvocationFilter—过滤器可短路（不调用 next）")]
    public async Task Filter_CanShortCircuit()
    {
        var nextCalled = false;
        var ctx = new FunctionInvocationContext { FunctionName = "blocked" };
        var filter = new ShortCircuitFilter("cached_result");

        await filter.OnFunctionInvocationAsync(ctx, (c, _) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.False(nextCalled);
        Assert.Equal("cached_result", ctx.Result);
    }

    // ── 多层洋葱链——顺序追踪 ─────────────────────────────────────────────

    [Fact]
    [DisplayName("IFunctionInvocationFilter—多层过滤器按洋葱顺序执行")]
    public async Task Filter_OnionChain_OrderTracked()
    {
        var order = new List<String>();
        var ctx = new FunctionInvocationContext { FunctionName = "op" };

        // 代表真正的"内核"
        Func<FunctionInvocationContext, CancellationToken, Task> innerCore = (c, _) =>
        {
            order.Add("core");
            c.Result = "done";
            return Task.CompletedTask;
        };

        // filter2 包裹 core
        Func<FunctionInvocationContext, CancellationToken, Task> next2 = (c, ct) =>
        {
            order.Add("filter2_before");
            var t = innerCore(c, ct).ContinueWith(_ => order.Add("filter2_after"), TaskContinuationOptions.ExecuteSynchronously);
            return t;
        };

        // filter1 包裹 filter2
        Func<FunctionInvocationContext, CancellationToken, Task> next1 = async (c, ct) =>
        {
            order.Add("filter1_before");
            await next2(c, ct);
            order.Add("filter1_after");
        };

        await next1(ctx, CancellationToken.None);

        Assert.Equal(["filter1_before", "filter2_before", "core", "filter2_after", "filter1_after"], order);
        Assert.Equal("done", ctx.Result);
    }

    // ── ExtraData 用于跨过滤器传递状态 ───────────────────────────────────

    [Fact]
    [DisplayName("FunctionInvocationContext—ExtraData 可在过滤器链中传递自定义状态")]
    public async Task FunctionInvocationContext_ExtraData_PassesAcrossFilters()
    {
        var ctx = new FunctionInvocationContext { FunctionName = "ping" };
        var filter = new ExtraDataWriterFilter("trace_id", "abc-123");

        await filter.OnFunctionInvocationAsync(ctx, static (_, _) => Task.CompletedTask, CancellationToken.None);

        Assert.Equal("abc-123", ctx.ExtraData["trace_id"]);
    }

    // ── 私有辅助过滤器实现 ────────────────────────────────────────────────

    /// <summary>将 next 赋值的数字 Result 乘以给定倍数</summary>
    private sealed class MultiplyResultFilter(Int32 multiplier) : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, Task> next,
            CancellationToken cancellationToken = default)
        {
            await next(context, cancellationToken);
            if (Int32.TryParse(context.Result, out var n))
                context.Result = (n * multiplier).ToString();
        }
    }

    /// <summary>在调用 next 之前将 Arguments 替换为指定 JSON</summary>
    private sealed class InjectArgumentFilter(String newArgs) : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, Task> next,
            CancellationToken cancellationToken = default)
        {
            context.Arguments = newArgs;
            await next(context, cancellationToken);
        }
    }

    /// <summary>不调用 next，直接设置 Result（短路）</summary>
    private sealed class ShortCircuitFilter(String cachedResult) : IFunctionInvocationFilter
    {
        public Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, Task> next,
            CancellationToken cancellationToken = default)
        {
            context.Result = cachedResult;
            return Task.CompletedTask;
        }
    }

    /// <summary>向 ExtraData 写入指定键值</summary>
    private sealed class ExtraDataWriterFilter(String key, Object value) : IFunctionInvocationFilter
    {
        public async Task OnFunctionInvocationAsync(
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, Task> next,
            CancellationToken cancellationToken = default)
        {
            await next(context, cancellationToken);
            context.ExtraData[key] = value;
        }
    }
}
