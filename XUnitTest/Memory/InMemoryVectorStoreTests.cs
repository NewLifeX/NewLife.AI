using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Memory;
using Xunit;

namespace XUnitTest.Memory;

/// <summary>InMemoryVectorStore 单元测试。覆盖所有方法的正常路径、边界场景及错误处理</summary>
[DisplayName("InMemoryVectorStore 单元测试")]
public class InMemoryVectorStoreTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // UpsertAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region UpsertAsync

    [Fact]
    [DisplayName("UpsertAsync—null 记录抛 ArgumentNullException")]
    public async Task UpsertAsync_NullRecord_Throws()
    {
        var store = new InMemoryVectorStore();
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpsertAsync(null!));
    }

    [Fact]
    [DisplayName("UpsertAsync—Id 为空字符串抛 ArgumentException")]
    public async Task UpsertAsync_EmptyId_Throws()
    {
        var store = new InMemoryVectorStore();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.UpsertAsync(new VectorRecord { Id = "", Vector = [1f] }));
    }

    [Fact]
    [DisplayName("UpsertAsync—写入包含 Payload 的记录，GetAsync 完整取回")]
    public async Task UpsertAsync_WithPayload_AllFieldsPreserved()
    {
        var store = new InMemoryVectorStore();
        var record = new VectorRecord { Id = "r1", Vector = [0.1f, 0.9f, 0.3f] };
        record.Payload["source"] = "unit-test";
        record.Payload["score"]  = 42;

        await store.UpsertAsync(record);

        var got = await store.GetAsync("r1");
        Assert.NotNull(got);
        Assert.Equal("r1", got.Id);
        Assert.Equal(3, got.Vector.Length);
        Assert.Equal("unit-test", got.Payload["source"]?.ToString());
        Assert.Equal(42, (Int32)got.Payload["score"]!);
    }

    [Fact]
    [DisplayName("UpsertAsync—相同 Id 二次写入覆盖旧向量和 Payload")]
    public async Task UpsertAsync_SameId_OverwritesVectorAndPayload()
    {
        var store = new InMemoryVectorStore();
        var old = new VectorRecord { Id = "dup", Vector = [1f, 0f] };
        old.Payload["ver"] = "v1";
        await store.UpsertAsync(old);

        var newer = new VectorRecord { Id = "dup", Vector = [0f, 1f] };
        newer.Payload["ver"] = "v2";
        await store.UpsertAsync(newer);

        var got = await store.GetAsync("dup");
        Assert.NotNull(got);
        Assert.Equal(0f, got.Vector[0]);
        Assert.Equal(1f, got.Vector[1]);
        Assert.Equal("v2", got.Payload["ver"]?.ToString());
        Assert.Equal(1L, await store.CountAsync());   // 覆盖后数量不变
    }

    [Fact]
    [DisplayName("UpsertAsync—向量为空数组时正常写入（不做 Vector 校验）")]
    public async Task UpsertAsync_EmptyVector_StoresRecord()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "empty-vec", Vector = [] });

        var got = await store.GetAsync("empty-vec");
        Assert.NotNull(got);
        Assert.Empty(got.Vector);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // UpsertBatchAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region UpsertBatchAsync

    [Fact]
    [DisplayName("UpsertBatchAsync—null 列表抛 ArgumentNullException")]
    public async Task UpsertBatchAsync_NullList_Throws()
    {
        var store = new InMemoryVectorStore();
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.UpsertBatchAsync(null!));
    }

    [Fact]
    [DisplayName("UpsertBatchAsync—混合合法/null/空Id 记录，只保留合法条目")]
    public async Task UpsertBatchAsync_MixedRecords_OnlyValidStored()
    {
        var store = new InMemoryVectorStore();
        var records = new VectorRecord?[]
        {
            new VectorRecord { Id = "good1", Vector = [1f, 0f] },
            null,
            new VectorRecord { Id = "",      Vector = [0f, 1f] },
            new VectorRecord { Id = "good2", Vector = [0.5f, 0.5f] },
        };

        await store.UpsertBatchAsync(records!);

        Assert.Equal(2L, await store.CountAsync());
        Assert.NotNull(await store.GetAsync("good1"));
        Assert.NotNull(await store.GetAsync("good2"));
        Assert.Null(await store.GetAsync(""));
    }

    [Fact]
    [DisplayName("UpsertBatchAsync—空列表不改变 Count")]
    public async Task UpsertBatchAsync_EmptyList_CountUnchanged()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "pre", Vector = [1f] });
        await store.UpsertBatchAsync([]);

        Assert.Equal(1L, await store.CountAsync());
    }

    [Fact]
    [DisplayName("UpsertBatchAsync—批量写入后逐条 GetAsync 均可取回")]
    public async Task UpsertBatchAsync_AllRetrievable()
    {
        var store = new InMemoryVectorStore();
        var ids = new[] { "a", "b", "c", "d", "e" };
        var records = ids.Select((id, i) =>
            new VectorRecord { Id = id, Vector = [i * 0.2f, 1f - i * 0.2f] }).ToArray();

        await store.UpsertBatchAsync(records);

        foreach (var id in ids)
        {
            var got = await store.GetAsync(id);
            Assert.NotNull(got);
            Assert.Equal(id, got.Id);
        }
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // GetAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region GetAsync

    [Fact]
    [DisplayName("GetAsync—不存在的 Id 返回 null")]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var store = new InMemoryVectorStore();
        Assert.Null(await store.GetAsync("nonexistent"));
    }

    [Fact]
    [DisplayName("GetAsync—传入取消令牌不影响正常读取")]
    public async Task GetAsync_WithCancellationToken_Works()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "ct", Vector = [1f] });

        using var cts = new CancellationTokenSource();
        var got = await store.GetAsync("ct", cts.Token);
        Assert.NotNull(got);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // SearchAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region SearchAsync

    [Fact]
    [DisplayName("SearchAsync—null 查询向量抛 ArgumentNullException")]
    public async Task SearchAsync_NullQuery_Throws()
    {
        var store = new InMemoryVectorStore();
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SearchAsync(null!));
    }

    [Fact]
    [DisplayName("SearchAsync—空查询向量抛 ArgumentNullException")]
    public async Task SearchAsync_EmptyQuery_Throws()
    {
        var store = new InMemoryVectorStore();
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SearchAsync([]));
    }

    [Fact]
    [DisplayName("SearchAsync—存储为空时返回空列表")]
    public async Task SearchAsync_EmptyStore_ReturnsEmpty()
    {
        var store = new InMemoryVectorStore();
        Assert.Empty(await store.SearchAsync([1f, 0f]));
    }

    [Fact]
    [DisplayName("SearchAsync—结果按余弦相似度降序排列")]
    public async Task SearchAsync_ResultsOrderedByCosineSimilarity()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "x", Vector = [1f, 0f, 0f] });
        await store.UpsertAsync(new VectorRecord { Id = "y", Vector = [0f, 1f, 0f] });
        await store.UpsertAsync(new VectorRecord { Id = "z", Vector = [0f, 0f, 1f] });

        // 查询向量接近 x 轴
        var results = await store.SearchAsync([0.98f, 0.1f, 0.05f]);

        Assert.Equal("x", results[0].Record.Id);
        for (var i = 0; i < results.Count - 1; i++)
            Assert.True(results[i].Score >= results[i + 1].Score);
    }

    [Fact]
    [DisplayName("SearchAsync—topK 正确限制返回数量")]
    public async Task SearchAsync_TopK_LimitsCount()
    {
        var store = new InMemoryVectorStore();
        for (var i = 0; i < 8; i++)
            await store.UpsertAsync(new VectorRecord { Id = $"r{i}", Vector = [i * 0.1f, 1f] });

        Assert.Equal(3, (await store.SearchAsync([0.5f, 1f], topK: 3)).Count);
        Assert.Equal(5, (await store.SearchAsync([0.5f, 1f], topK: 5)).Count);
    }

    [Fact]
    [DisplayName("SearchAsync—topK=0 返回全部符合 minScore 的记录")]
    public async Task SearchAsync_TopKZero_ReturnsAll()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "a", Vector = [1f, 0f] });
        await store.UpsertAsync(new VectorRecord { Id = "b", Vector = [0f, 1f] });
        await store.UpsertAsync(new VectorRecord { Id = "c", Vector = [0.7f, 0.7f] });

        var results = await store.SearchAsync([1f, 0f], topK: 0);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    [DisplayName("SearchAsync—minScore 过滤低相似度记录，只返回超过阈值的条目")]
    public async Task SearchAsync_MinScore_FiltersLowScore()
    {
        var store = new InMemoryVectorStore();
        // 与 [1,0] 的余弦：near ≈ 1.0，far ≈ 0
        await store.UpsertAsync(new VectorRecord { Id = "near", Vector = [1f, 0.01f] });
        await store.UpsertAsync(new VectorRecord { Id = "far",  Vector = [0f, 1f]   });

        var results = await store.SearchAsync([1f, 0f], topK: 10, minScore: 0.9);
        Assert.Single(results);
        Assert.Equal("near", results[0].Record.Id);
    }

    [Fact]
    [DisplayName("SearchAsync—向量为空的记录不参与相似度计算")]
    public async Task SearchAsync_EmptyVectorRecord_Excluded()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "no-vec", Vector = [] });
        await store.UpsertAsync(new VectorRecord { Id = "has-vec", Vector = [1f, 0f] });

        var results = await store.SearchAsync([1f, 0f]);
        Assert.DoesNotContain(results, r => r.Record.Id == "no-vec");
        Assert.Contains(results, r => r.Record.Id == "has-vec");
    }

    [Fact]
    [DisplayName("SearchAsync—相同方向向量余弦为 1，反向向量余弦为 -1")]
    public async Task SearchAsync_CosineSimilarity_SameAndOpposite()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "same",     Vector = [1f, 0f] });
        await store.UpsertAsync(new VectorRecord { Id = "opposite", Vector = [-1f, 0f] });

        var results = await store.SearchAsync([1f, 0f], topK: 2, minScore: -1.0);

        Assert.Equal("same", results[0].Record.Id);
        Assert.True(results[0].Score > 0.99, $"同向向量余弦应接近 1，实际：{results[0].Score}");
        Assert.True(results[1].Score < -0.99, $"反向向量余弦应接近 -1，实际：{results[1].Score}");
    }

    [Fact]
    [DisplayName("SearchAsync—查询向量维度与存储向量不同时取较短维度计算（不抛异常）")]
    public async Task SearchAsync_DimensionMismatch_HandledGracefully()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "v3", Vector = [1f, 0f, 0f] });

        // 查询向量只有 2 维，应取 min(2,3)=2 维计算，不抛异常
        var ex = await Record.ExceptionAsync(() => store.SearchAsync([1f, 0f]));
        Assert.Null(ex);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // DeleteAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region DeleteAsync

    [Fact]
    [DisplayName("DeleteAsync—删除已存在记录后 GetAsync 返回 null 且 Count 减 1")]
    public async Task DeleteAsync_ExistingRecord_RemovedAndCountDecreased()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "keep", Vector = [1f] });
        await store.UpsertAsync(new VectorRecord { Id = "del",  Vector = [0f] });

        await store.DeleteAsync("del");

        Assert.Null(await store.GetAsync("del"));
        Assert.NotNull(await store.GetAsync("keep"));
        Assert.Equal(1L, await store.CountAsync());
    }

    [Fact]
    [DisplayName("DeleteAsync—删除不存在的 Id 不抛异常")]
    public async Task DeleteAsync_NonExistent_NoException()
    {
        var store = new InMemoryVectorStore();
        var ex = await Record.ExceptionAsync(() => store.DeleteAsync("ghost"));
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("DeleteAsync—删除后重新写入相同 Id 可正常使用")]
    public async Task DeleteAsync_ThenReInsert_Works()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "recycle", Vector = [1f, 0f] });
        await store.DeleteAsync("recycle");
        await store.UpsertAsync(new VectorRecord { Id = "recycle", Vector = [0f, 1f] });

        var got = await store.GetAsync("recycle");
        Assert.NotNull(got);
        Assert.Equal(0f, got.Vector[0]);
        Assert.Equal(1f, got.Vector[1]);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // CountAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region CountAsync

    [Fact]
    [DisplayName("CountAsync—空存储返回 0")]
    public async Task CountAsync_EmptyStore_Zero()
    {
        var store = new InMemoryVectorStore();
        Assert.Equal(0L, await store.CountAsync());
    }

    [Fact]
    [DisplayName("CountAsync—Upsert→Delete 后数量随操作正确变化")]
    public async Task CountAsync_TracksUpsertAndDelete()
    {
        var store = new InMemoryVectorStore();

        await store.UpsertAsync(new VectorRecord { Id = "a", Vector = [1f] });
        Assert.Equal(1L, await store.CountAsync());

        await store.UpsertAsync(new VectorRecord { Id = "b", Vector = [0f] });
        Assert.Equal(2L, await store.CountAsync());

        await store.UpsertAsync(new VectorRecord { Id = "a", Vector = [0.5f] }); // 覆盖不增加
        Assert.Equal(2L, await store.CountAsync());

        await store.DeleteAsync("b");
        Assert.Equal(1L, await store.CountAsync());
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 线程安全
    // ══════════════════════════════════════════════════════════════════════════

    #region 线程安全

    [Fact]
    [DisplayName("线程安全—多线程并发 Upsert 不抛异常且 Count 精确")]
    public async Task ConcurrentUpsert_ThreadSafe()
    {
        var store = new InMemoryVectorStore();
        var tasks = Enumerable.Range(0, 50).Select(i =>
            store.UpsertAsync(new VectorRecord { Id = $"t{i}", Vector = [i * 0.02f, 1f] }));

        await Task.WhenAll(tasks);
        Assert.Equal(50L, await store.CountAsync());
    }

    [Fact]
    [DisplayName("线程安全—并发读写同一条记录不抛异常")]
    public async Task ConcurrentReadWrite_SameRecord_NoException()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "shared", Vector = [1f, 0f] });

        var writes = Enumerable.Range(0, 20).Select(i =>
            store.UpsertAsync(new VectorRecord { Id = "shared", Vector = [i * 0.05f, 1f] }));
        var reads = Enumerable.Range(0, 20).Select(_ =>
            store.GetAsync("shared"));

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(writes.Concat<Task>(reads)));
        Assert.Null(ex);
    }

    #endregion
}
