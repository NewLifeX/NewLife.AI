using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Memory;
using Xunit;

namespace XUnitTest.Memory;

/// <summary>InMemoryVectorStore 单元测试。覆盖存储级（集合管理）与集合级（记录 CRUD + 检索）两级 API</summary>
[DisplayName("InMemoryVectorStore 单元测试")]
public class InMemoryVectorStoreTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // 存储级：GetCollection
    // ══════════════════════════════════════════════════════════════════════════

    #region GetCollection

    [Fact]
    [DisplayName("GetCollection—同名返回同一实例，异名返回不同集合")]
    public void GetCollection_SameName_SameInstance()
    {
        var store = new InMemoryVectorStore();
        var c1 = store.GetCollection("col1");
        var c2 = store.GetCollection("col1");
        var c3 = store.GetCollection("col2");

        Assert.Same(c1, c2);
        Assert.NotSame(c1, c3);
        Assert.Equal("col1", c1.Name);
        Assert.Equal("col2", c3.Name);
    }

    [Fact]
    [DisplayName("GetCollection—空名称抛 ArgumentNullException")]
    public void GetCollection_EmptyName_Throws()
    {
        var store = new InMemoryVectorStore();
        Assert.Throws<ArgumentNullException>(() => store.GetCollection(""));
    }

    [Fact]
    [DisplayName("GetCollection—懒创建空集合，Count 为 0")]
    public async Task GetCollection_LazyCreate_EmptyCollection()
    {
        var store = new InMemoryVectorStore();
        var col = store.GetCollection("fresh");

        Assert.Equal(0L, await col.CountAsync());
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 存储级：集合生命周期
    // ══════════════════════════════════════════════════════════════════════════

    #region 集合生命周期

    [Fact]
    [DisplayName("ListCollectionNamesAsync—空存储返回空列表")]
    public async Task ListCollectionNames_EmptyStore_Empty()
    {
        var store = new InMemoryVectorStore();
        Assert.Empty(await store.ListCollectionNamesAsync());
    }

    [Fact]
    [DisplayName("ListCollectionNamesAsync—列出全部已创建集合")]
    public async Task ListCollectionNames_ReturnsAll()
    {
        var store = new InMemoryVectorStore();
        store.GetCollection("a");
        store.GetCollection("b");
        store.GetCollection("c");

        var names = await store.ListCollectionNamesAsync();
        Assert.Equal(3, names.Count);
        Assert.Contains("a", names);
        Assert.Contains("b", names);
        Assert.Contains("c", names);
    }

    [Fact]
    [DisplayName("RemoveCollectionAsync—删除集合及其全部记录，再次 GetCollection 得到新空集合")]
    public async Task RemoveCollection_RemovesAll()
    {
        var store = new InMemoryVectorStore();
        var col = store.GetCollection("drop");
        await col.UpsertAsync(new VectorRecord { Id = "r1", Vector = [1f] });

        await store.RemoveCollectionAsync("drop");

        Assert.Empty(await store.ListCollectionNamesAsync());

        // 重新获取是新的空集合
        var again = store.GetCollection("drop");
        Assert.Equal(0L, await again.CountAsync());
    }

    [Fact]
    [DisplayName("RemoveCollectionAsync—删除不存在的集合不抛异常")]
    public async Task RemoveCollection_NonExistent_NoException()
    {
        var store = new InMemoryVectorStore();
        var ex = await Record.ExceptionAsync(() => store.RemoveCollectionAsync("ghost"));
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("集合隔离—不同集合记录互不可见，检索互不干扰")]
    public async Task CollectionIsolation_RecordsAndSearch()
    {
        var store = new InMemoryVectorStore();
        var colA = store.GetCollection("A");
        var colB = store.GetCollection("B");

        await colA.UpsertAsync(new VectorRecord { Id = "x", Vector = [1f, 0f] });
        await colB.UpsertAsync(new VectorRecord { Id = "x", Vector = [0f, 1f] });

        // 各自 Count 独立
        Assert.Equal(1L, await colA.CountAsync());
        Assert.Equal(1L, await colB.CountAsync());

        // A 检索 [1,0] 命中的是 A 自己的记录
        var resultsA = await colA.SearchAsync([1f, 0f], top: 1);
        Assert.Single(resultsA);
        Assert.Equal(1f, resultsA[0].Record.Vector[0]);

        // B 不受 A 影响
        var resultsB = await colB.SearchAsync([1f, 0f], top: 1);
        Assert.Equal(0f, resultsB[0].Record.Vector[0]);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 集合级：UpsertAsync（单条）
    // ══════════════════════════════════════════════════════════════════════════

    #region UpsertAsync 单条

    [Fact]
    [DisplayName("UpsertAsync—null 记录抛 ArgumentNullException")]
    public async Task UpsertAsync_NullRecord_Throws()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await Assert.ThrowsAsync<ArgumentNullException>(() => col.UpsertAsync((VectorRecord)null!));
    }

    [Fact]
    [DisplayName("UpsertAsync—Id 为空字符串抛 ArgumentException")]
    public async Task UpsertAsync_EmptyId_Throws()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            col.UpsertAsync(new VectorRecord { Id = "", Vector = [1f] }));
    }

    [Fact]
    [DisplayName("UpsertAsync—写入包含 Payload 的记录，GetAsync 完整取回")]
    public async Task UpsertAsync_WithPayload_AllFieldsPreserved()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        var record = new VectorRecord { Id = "r1", Vector = [0.1f, 0.9f, 0.3f] };
        record.Payload["source"] = "unit-test";
        record.Payload["score"]  = 42;

        await col.UpsertAsync(record);

        var got = await col.GetAsync("r1");
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
        var col = new InMemoryVectorStore().GetCollection("c");
        var old = new VectorRecord { Id = "dup", Vector = [1f, 0f] };
        old.Payload["ver"] = "v1";
        await col.UpsertAsync(old);

        var newer = new VectorRecord { Id = "dup", Vector = [0f, 1f] };
        newer.Payload["ver"] = "v2";
        await col.UpsertAsync(newer);

        var got = await col.GetAsync("dup");
        Assert.NotNull(got);
        Assert.Equal(0f, got.Vector[0]);
        Assert.Equal(1f, got.Vector[1]);
        Assert.Equal("v2", got.Payload["ver"]?.ToString());
        Assert.Equal(1L, await col.CountAsync());   // 覆盖后数量不变
    }

    [Fact]
    [DisplayName("UpsertAsync—向量为空数组时正常写入（不做 Vector 校验）")]
    public async Task UpsertAsync_EmptyVector_StoresRecord()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "empty-vec", Vector = [] });

        var got = await col.GetAsync("empty-vec");
        Assert.NotNull(got);
        Assert.Empty(got.Vector);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 集合级：UpsertAsync（批量重载）
    // ══════════════════════════════════════════════════════════════════════════

    #region UpsertAsync 批量

    [Fact]
    [DisplayName("UpsertAsync 批量—null 列表抛 ArgumentNullException")]
    public async Task UpsertBatch_NullList_Throws()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await Assert.ThrowsAsync<ArgumentNullException>(() => col.UpsertAsync((IEnumerable<VectorRecord>)null!));
    }

    [Fact]
    [DisplayName("UpsertAsync 批量—混合合法/null/空Id 记录，只保留合法条目")]
    public async Task UpsertBatch_MixedRecords_OnlyValidStored()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        var records = new VectorRecord[]
        {
            new VectorRecord { Id = "good1", Vector = [1f, 0f] },
            null!,
            new VectorRecord { Id = "",      Vector = [0f, 1f] },
            new VectorRecord { Id = "good2", Vector = [0.5f, 0.5f] },
        };

        await col.UpsertAsync(records);

        Assert.Equal(2L, await col.CountAsync());
        Assert.NotNull(await col.GetAsync("good1"));
        Assert.NotNull(await col.GetAsync("good2"));
        Assert.Null(await col.GetAsync(""));
    }

    [Fact]
    [DisplayName("UpsertAsync 批量—空列表不改变 Count")]
    public async Task UpsertBatch_EmptyList_CountUnchanged()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "pre", Vector = [1f] });
        await col.UpsertAsync(Array.Empty<VectorRecord>());

        Assert.Equal(1L, await col.CountAsync());
    }

    [Fact]
    [DisplayName("UpsertAsync 批量—批量写入后逐条 GetAsync 均可取回")]
    public async Task UpsertBatch_AllRetrievable()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        var ids = new[] { "a", "b", "c", "d", "e" };
        var records = ids.Select((id, i) =>
            new VectorRecord { Id = id, Vector = [i * 0.2f, 1f - i * 0.2f] }).ToArray();

        await col.UpsertAsync(records);

        foreach (var id in ids)
        {
            var got = await col.GetAsync(id);
            Assert.NotNull(got);
            Assert.Equal(id, got.Id);
        }
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 集合级：GetAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region GetAsync

    [Fact]
    [DisplayName("GetAsync—不存在的 Id 返回 null")]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        Assert.Null(await col.GetAsync("nonexistent"));
    }

    [Fact]
    [DisplayName("GetAsync—传入取消令牌不影响正常读取")]
    public async Task GetAsync_WithCancellationToken_Works()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "ct", Vector = [1f] });

        using var cts = new CancellationTokenSource();
        var got = await col.GetAsync("ct", cts.Token);
        Assert.NotNull(got);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 集合级：SearchAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region SearchAsync

    [Fact]
    [DisplayName("SearchAsync—null 查询向量抛 ArgumentNullException")]
    public async Task SearchAsync_NullQuery_Throws()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await Assert.ThrowsAsync<ArgumentNullException>(() => col.SearchAsync(null!));
    }

    [Fact]
    [DisplayName("SearchAsync—空查询向量抛 ArgumentNullException")]
    public async Task SearchAsync_EmptyQuery_Throws()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await Assert.ThrowsAsync<ArgumentNullException>(() => col.SearchAsync([]));
    }

    [Fact]
    [DisplayName("SearchAsync—存储为空时返回空列表")]
    public async Task SearchAsync_EmptyStore_ReturnsEmpty()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        Assert.Empty(await col.SearchAsync([1f, 0f]));
    }

    [Fact]
    [DisplayName("SearchAsync—结果按余弦相似度降序排列")]
    public async Task SearchAsync_ResultsOrderedByCosineSimilarity()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "x", Vector = [1f, 0f, 0f] });
        await col.UpsertAsync(new VectorRecord { Id = "y", Vector = [0f, 1f, 0f] });
        await col.UpsertAsync(new VectorRecord { Id = "z", Vector = [0f, 0f, 1f] });

        // 查询向量接近 x 轴
        var results = await col.SearchAsync([0.98f, 0.1f, 0.05f]);

        Assert.Equal("x", results[0].Record.Id);
        for (var i = 0; i < results.Count - 1; i++)
            Assert.True(results[i].Score >= results[i + 1].Score);
    }

    [Fact]
    [DisplayName("SearchAsync—top 正确限制返回数量")]
    public async Task SearchAsync_Top_LimitsCount()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        for (var i = 0; i < 8; i++)
            await col.UpsertAsync(new VectorRecord { Id = $"r{i}", Vector = [i * 0.1f, 1f] });

        Assert.Equal(3, (await col.SearchAsync([0.5f, 1f], top: 3)).Count);
        Assert.Equal(5, (await col.SearchAsync([0.5f, 1f], top: 5)).Count);
    }

    [Fact]
    [DisplayName("SearchAsync—top=0 返回全部符合 minScore 的记录")]
    public async Task SearchAsync_TopZero_ReturnsAll()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "a", Vector = [1f, 0f] });
        await col.UpsertAsync(new VectorRecord { Id = "b", Vector = [0f, 1f] });
        await col.UpsertAsync(new VectorRecord { Id = "c", Vector = [0.7f, 0.7f] });

        var results = await col.SearchAsync([1f, 0f], top: 0);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    [DisplayName("SearchAsync—minScore 过滤低相似度记录，只返回超过阈值的条目")]
    public async Task SearchAsync_MinScore_FiltersLowScore()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        // 与 [1,0] 的余弦：near ≈ 1.0，far ≈ 0
        await col.UpsertAsync(new VectorRecord { Id = "near", Vector = [1f, 0.01f] });
        await col.UpsertAsync(new VectorRecord { Id = "far",  Vector = [0f, 1f]   });

        var results = await col.SearchAsync([1f, 0f], top: 10, minScore: 0.9);
        Assert.Single(results);
        Assert.Equal("near", results[0].Record.Id);
    }

    [Fact]
    [DisplayName("SearchAsync—向量为空的记录不参与相似度计算")]
    public async Task SearchAsync_EmptyVectorRecord_Excluded()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "no-vec", Vector = [] });
        await col.UpsertAsync(new VectorRecord { Id = "has-vec", Vector = [1f, 0f] });

        var results = await col.SearchAsync([1f, 0f]);
        Assert.DoesNotContain(results, r => r.Record.Id == "no-vec");
        Assert.Contains(results, r => r.Record.Id == "has-vec");
    }

    [Fact]
    [DisplayName("SearchAsync—相同方向向量余弦为 1，反向向量余弦为 -1")]
    public async Task SearchAsync_CosineSimilarity_SameAndOpposite()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "same",     Vector = [1f, 0f] });
        await col.UpsertAsync(new VectorRecord { Id = "opposite", Vector = [-1f, 0f] });

        var results = await col.SearchAsync([1f, 0f], top: 2, minScore: -1.0);

        Assert.Equal("same", results[0].Record.Id);
        Assert.True(results[0].Score > 0.99, $"同向向量余弦应接近 1，实际：{results[0].Score}");
        Assert.True(results[1].Score < -0.99, $"反向向量余弦应接近 -1，实际：{results[1].Score}");
    }

    [Fact]
    [DisplayName("SearchAsync—查询向量维度与存储向量不同时取较短维度计算（不抛异常）")]
    public async Task SearchAsync_DimensionMismatch_HandledGracefully()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "v3", Vector = [1f, 0f, 0f] });

        // 查询向量只有 2 维，应取 min(2,3)=2 维计算，不抛异常
        var ex = await Record.ExceptionAsync(() => col.SearchAsync([1f, 0f]));
        Assert.Null(ex);
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 集合级：DeleteAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region DeleteAsync

    [Fact]
    [DisplayName("DeleteAsync—删除已存在记录后 GetAsync 返回 null 且 Count 减 1")]
    public async Task DeleteAsync_ExistingRecord_RemovedAndCountDecreased()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "keep", Vector = [1f] });
        await col.UpsertAsync(new VectorRecord { Id = "del",  Vector = [0f] });

        await col.DeleteAsync("del");

        Assert.Null(await col.GetAsync("del"));
        Assert.NotNull(await col.GetAsync("keep"));
        Assert.Equal(1L, await col.CountAsync());
    }

    [Fact]
    [DisplayName("DeleteAsync—删除不存在的 Id 不抛异常")]
    public async Task DeleteAsync_NonExistent_NoException()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        var ex = await Record.ExceptionAsync(() => col.DeleteAsync("ghost"));
        Assert.Null(ex);
    }

    [Fact]
    [DisplayName("DeleteAsync—删除后重新写入相同 Id 可正常使用")]
    public async Task DeleteAsync_ThenReInsert_Works()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "recycle", Vector = [1f, 0f] });
        await col.DeleteAsync("recycle");
        await col.UpsertAsync(new VectorRecord { Id = "recycle", Vector = [0f, 1f] });

        var got = await col.GetAsync("recycle");
        Assert.NotNull(got);
        Assert.Equal(0f, got.Vector[0]);
        Assert.Equal(1f, got.Vector[1]);
    }

    [Fact]
    [DisplayName("DeleteAsync 批量—一次删除多条记录，忽略不存在的 Id")]
    public async Task DeleteBatch_RemovesMultiple()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "a", Vector = [1f] });
        await col.UpsertAsync(new VectorRecord { Id = "b", Vector = [0f] });
        await col.UpsertAsync(new VectorRecord { Id = "c", Vector = [1f] });

        await col.DeleteAsync(new[] { "a", "c", "ghost" });

        Assert.Null(await col.GetAsync("a"));
        Assert.NotNull(await col.GetAsync("b"));
        Assert.Null(await col.GetAsync("c"));
        Assert.Equal(1L, await col.CountAsync());
    }

    [Fact]
    [DisplayName("DeleteAsync 批量—null 列表抛 ArgumentNullException")]
    public async Task DeleteBatch_NullList_Throws()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await Assert.ThrowsAsync<ArgumentNullException>(() => col.DeleteAsync((IEnumerable<String>)null!));
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 集合级：CountAsync
    // ══════════════════════════════════════════════════════════════════════════

    #region CountAsync

    [Fact]
    [DisplayName("CountAsync—空集合返回 0")]
    public async Task CountAsync_EmptyStore_Zero()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        Assert.Equal(0L, await col.CountAsync());
    }

    [Fact]
    [DisplayName("CountAsync—Upsert→Delete 后数量随操作正确变化")]
    public async Task CountAsync_TracksUpsertAndDelete()
    {
        var col = new InMemoryVectorStore().GetCollection("c");

        await col.UpsertAsync(new VectorRecord { Id = "a", Vector = [1f] });
        Assert.Equal(1L, await col.CountAsync());

        await col.UpsertAsync(new VectorRecord { Id = "b", Vector = [0f] });
        Assert.Equal(2L, await col.CountAsync());

        await col.UpsertAsync(new VectorRecord { Id = "a", Vector = [0.5f] }); // 覆盖不增加
        Assert.Equal(2L, await col.CountAsync());

        await col.DeleteAsync("b");
        Assert.Equal(1L, await col.CountAsync());
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
        var col = new InMemoryVectorStore().GetCollection("c");
        var tasks = Enumerable.Range(0, 50).Select(i =>
            col.UpsertAsync(new VectorRecord { Id = $"t{i}", Vector = [i * 0.02f, 1f] }));

        await Task.WhenAll(tasks);
        Assert.Equal(50L, await col.CountAsync());
    }

    [Fact]
    [DisplayName("线程安全—并发读写同一条记录不抛异常")]
    public async Task ConcurrentReadWrite_SameRecord_NoException()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "shared", Vector = [1f, 0f] });

        var writes = Enumerable.Range(0, 20).Select(i =>
            col.UpsertAsync(new VectorRecord { Id = "shared", Vector = [i * 0.05f, 1f] }));
        var reads = Enumerable.Range(0, 20).Select(_ =>
            col.GetAsync("shared"));

        var ex = await Record.ExceptionAsync(() => Task.WhenAll(writes.Concat<Task>(reads)));
        Assert.Null(ex);
    }

    #endregion
}
