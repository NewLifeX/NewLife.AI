using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using NewLife.AI.Memory;
using Xunit;

namespace XUnitTest.Memory;

[DisplayName("内存语义存储测试")]
public class MemoryTests
{
    #region InMemorySemanticMemory

    [Fact]
    [DisplayName("保存与检索—按 Id 精确获取")]
    public async Task SemanticMemory_SaveAndGet()
    {
        var mem = new InMemorySemanticMemory();
        var entry = new MemoryEntry
        {
            Id = "e1",
            Collection = "test",
            Text = "Hello World",
            Vector = [1f, 0f, 0f],
        };

        await mem.SaveAsync(entry);

        var result = await mem.GetAsync("test", "e1");
        Assert.NotNull(result);
        Assert.Equal("Hello World", result.Text);
    }

    [Fact]
    [DisplayName("覆盖—保存同 Id 条目后读取新值")]
    public async Task SemanticMemory_Overwrite()
    {
        var mem = new InMemorySemanticMemory();
        await mem.SaveAsync(new MemoryEntry { Id = "x", Collection = "col", Text = "old", Vector = [1f] });
        await mem.SaveAsync(new MemoryEntry { Id = "x", Collection = "col", Text = "new", Vector = [1f] });

        var r = await mem.GetAsync("col", "x");
        Assert.Equal("new", r!.Text);
    }

    [Fact]
    [DisplayName("删除—RemoveAsync 后 GetAsync 返回 null")]
    public async Task SemanticMemory_Remove()
    {
        var mem = new InMemorySemanticMemory();
        await mem.SaveAsync(new MemoryEntry { Id = "del", Collection = "c", Text = "bye", Vector = [1f] });
        await mem.RemoveAsync("c", "del");

        var r = await mem.GetAsync("c", "del");
        Assert.Null(r);
    }

    [Fact]
    [DisplayName("相似度检索—余弦相似返回最近邻")]
    public async Task SemanticMemory_Search_ByCosineSimilarity()
    {
        var mem = new InMemorySemanticMemory();

        await mem.SaveAsync(new MemoryEntry { Id = "a", Collection = "c", Text = "A", Vector = [1f, 0f, 0f] });
        await mem.SaveAsync(new MemoryEntry { Id = "b", Collection = "c", Text = "B", Vector = [0f, 1f, 0f] });
        await mem.SaveAsync(new MemoryEntry { Id = "c", Collection = "c", Text = "C", Vector = [0f, 0f, 1f] });

        // 查询向量接近 "a"
        var results = await mem.SearchAsync("c", [0.99f, 0.01f, 0.01f], topN: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("a", results[0].Entry.Id);  // 最近邻应是 a
        Assert.True(results[0].Relevance > results[1].Relevance);
    }

    [Fact]
    [DisplayName("相似度检索—minRelevance 过滤低分条目")]
    public async Task SemanticMemory_Search_MinRelevance()
    {
        var mem = new InMemorySemanticMemory();
        await mem.SaveAsync(new MemoryEntry { Id = "a", Collection = "c", Text = "A", Vector = [1f, 0f] });
        await mem.SaveAsync(new MemoryEntry { Id = "b", Collection = "c", Text = "B", Vector = [0f, 1f] });

        // 高 minRelevance 只返回 a（余弦相似度 1.0）
        var results = await mem.SearchAsync("c", [1f, 0f], topN: 5, minRelevance: 0.9);
        Assert.Single(results);
        Assert.Equal("a", results[0].Entry.Id);
    }

    [Fact]
    [DisplayName("ListIdsAsync—返回集合内所有 Id")]
    public async Task SemanticMemory_ListIds()
    {
        var mem = new InMemorySemanticMemory();
        await mem.SaveAsync(new MemoryEntry { Id = "1", Collection = "col", Text = "t1", Vector = [1f] });
        await mem.SaveAsync(new MemoryEntry { Id = "2", Collection = "col", Text = "t2", Vector = [1f] });

        var ids = await mem.ListIdsAsync("col");
        Assert.Equal(2, ids.Count);
        Assert.Contains("1", ids);
        Assert.Contains("2", ids);
    }

    [Fact]
    [DisplayName("参数验证—Id 为空时抛出 ArgumentException")]
    public async Task SemanticMemory_Validate_EmptyId()
    {
        var mem = new InMemorySemanticMemory();
        var entry = new MemoryEntry { Id = "", Collection = "c", Text = "t", Vector = [1f] };
        await Assert.ThrowsAsync<ArgumentException>(() => mem.SaveAsync(entry));
    }

    #endregion

    #region InMemoryVectorStore

    [Fact]
    [DisplayName("VectorStore—Upsert 后 GetAsync 可取回记录")]
    public async Task VectorStore_UpsertAndGet()
    {
        var store = new InMemoryVectorStore();
        var record = new VectorRecord { Id = "v1", Vector = [1f, 2f, 3f] };
        record.Payload["key"] = "value";

        await store.UpsertAsync(record);

        var got = await store.GetAsync("v1");
        Assert.NotNull(got);
        Assert.Equal(3, got.Vector.Length);
        Assert.Equal("value", got.Payload["key"]?.ToString());
    }

    [Fact]
    [DisplayName("VectorStore—BatchUpsert 后 Count 正确")]
    public async Task VectorStore_BatchUpsert()
    {
        var store = new InMemoryVectorStore();
        var records = new[]
        {
            new VectorRecord { Id = "r1", Vector = [1f, 0f] },
            new VectorRecord { Id = "r2", Vector = [0f, 1f] },
            new VectorRecord { Id = "r3", Vector = [1f, 1f] },
        };

        await store.UpsertBatchAsync(records);

        Assert.Equal(3L, await store.CountAsync());
    }

    [Fact]
    [DisplayName("VectorStore—SearchAsync 返回 Top-K")]
    public async Task VectorStore_Search_TopK()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "x", Vector = [1f, 0f] });
        await store.UpsertAsync(new VectorRecord { Id = "y", Vector = [0f, 1f] });
        await store.UpsertAsync(new VectorRecord { Id = "z", Vector = [1f, 1f] });

        var results = await store.SearchAsync([1f, 0.1f], topK: 2);
        Assert.Equal(2, results.Count);
        Assert.Equal("x", results[0].Record.Id);  // 最接近 [1,0]
    }

    [Fact]
    [DisplayName("VectorStore—DeleteAsync 后记录不存在")]
    public async Task VectorStore_Delete()
    {
        var store = new InMemoryVectorStore();
        await store.UpsertAsync(new VectorRecord { Id = "del", Vector = [1f] });
        await store.DeleteAsync("del");

        Assert.Null(await store.GetAsync("del"));
        Assert.Equal(0L, await store.CountAsync());
    }

    #endregion
}
