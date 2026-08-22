using System;
using System.ComponentModel;
using System.Threading.Tasks;
using NewLife.AI.Memory;
using Xunit;

namespace XUnitTest.Memory;

[DisplayName("内存向量存储测试")]
public class MemoryTests
{
    #region InMemoryVectorStore

    [Fact]
    [DisplayName("VectorStore—Upsert 后 GetAsync 可取回记录")]
    public async Task VectorStore_UpsertAndGet()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        var record = new VectorRecord { Id = "v1", Vector = [1f, 2f, 3f] };
        record.Payload["key"] = "value";

        await col.UpsertAsync(record);

        var got = await col.GetAsync("v1");
        Assert.NotNull(got);
        Assert.Equal(3, got.Vector.Length);
        Assert.Equal("value", got.Payload["key"]?.ToString());
    }

    [Fact]
    [DisplayName("VectorStore—批量 Upsert 后 Count 正确")]
    public async Task VectorStore_BatchUpsert()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        var records = new[]
        {
            new VectorRecord { Id = "r1", Vector = [1f, 0f] },
            new VectorRecord { Id = "r2", Vector = [0f, 1f] },
            new VectorRecord { Id = "r3", Vector = [1f, 1f] },
        };

        await col.UpsertAsync(records);

        Assert.Equal(3L, await col.CountAsync());
    }

    [Fact]
    [DisplayName("VectorStore—SearchAsync 返回 Top-K")]
    public async Task VectorStore_Search_TopK()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "x", Vector = [1f, 0f] });
        await col.UpsertAsync(new VectorRecord { Id = "y", Vector = [0f, 1f] });
        await col.UpsertAsync(new VectorRecord { Id = "z", Vector = [1f, 1f] });

        var results = await col.SearchAsync([1f, 0.1f], top: 2);
        Assert.Equal(2, results.Count);
        Assert.Equal("x", results[0].Record.Id);  // 最接近 [1,0]
    }

    [Fact]
    [DisplayName("VectorStore—DeleteAsync 后记录不存在")]
    public async Task VectorStore_Delete()
    {
        var col = new InMemoryVectorStore().GetCollection("c");
        await col.UpsertAsync(new VectorRecord { Id = "del", Vector = [1f] });
        await col.DeleteAsync("del");

        Assert.Null(await col.GetAsync("del"));
        Assert.Equal(0L, await col.CountAsync());
    }

    #endregion
}
