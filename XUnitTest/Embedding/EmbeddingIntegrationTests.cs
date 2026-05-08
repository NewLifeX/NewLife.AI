#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Embedding;
using NewLife.AI.Memory;
using Xunit;
using Xunit.Sdk;

namespace XUnitTest.Embedding;

/// <summary>Embedding 集成测试：本地/远程生成器对一批文本编码存储，再用新文本嵌入后检索，验证语义搜索端到端流程</summary>
[DisplayName("Embedding 集成测试")]
public class EmbeddingIntegrationTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // 第一部分：本地生成器（HashTextEmbedder）端到端集成
    // ══════════════════════════════════════════════════════════════════════════

    #region 本地生成器集成

    [Fact]
    [DisplayName("本地—多主题语料批量编码存储，新查询文本嵌入后搜索返回语义最近邻")]
    public async Task Local_EncodeCorpus_NewQueryReturnsSemanticallySimilar()
    {
        var embedder = new HashTextEmbedder(512);
        IVectorStore store = new InMemoryVectorStore();

        // 准备主题差异明显的语料并批量写入
        var corpus = new[]
        {
            ("ai",      "人工智能深度学习神经网络机器学习算法模型训练"),
            ("food",    "美食烹饪食谱厨师料理餐厅菜肴佳肴"),
            ("travel",  "旅游景点酒店机票城市旅行出行攻略"),
            ("sports",  "足球篮球运动员比赛训练体育竞技"),
            ("finance", "股票基金投资理财经济市场证券"),
        };

        var records = corpus.Select(c => new VectorRecord
        {
            Id     = c.Item1,
            Vector = embedder.Embed(c.Item2),
        }).ToArray();

        await store.UpsertBatchAsync(records);
        Assert.Equal(corpus.Length, (Int32)await store.CountAsync());

        // 用新文本（未出现在语料中）查询，验证语义匹配正确
        var aiQuery    = embedder.Embed("神经网络算法与大模型训练");
        var foodQuery  = embedder.Embed("厨师烹饪技艺与料理食谱");
        var sportQuery = embedder.Embed("体育竞技比赛训练场地");

        var aiResults    = await store.SearchAsync(aiQuery,    topK: 1);
        var foodResults  = await store.SearchAsync(foodQuery,  topK: 1);
        var sportResults = await store.SearchAsync(sportQuery, topK: 1);

        Assert.Equal("ai",     aiResults[0].Record.Id);
        Assert.Equal("food",   foodResults[0].Record.Id);
        Assert.Equal("sports", sportResults[0].Record.Id);
    }

    [Fact]
    [DisplayName("本地—VectorData 序列化持久化后恢复，搜索相似度与原向量一致")]
    public async Task Local_VectorDataRoundTrip_SearchScoreUnchanged()
    {
        var embedder = new HashTextEmbedder(256);
        IVectorStore store = new InMemoryVectorStore();

        // 生成并通过 VectorData JSON 序列化再反序列化（模拟持久化）
        var texts = new[]
        {
            ("doc1", "区块链分布式账本加密货币比特币"),
            ("doc2", "云计算虚拟化容器微服务架构"),
            ("doc3", "量子计算量子纠缠超导量子比特"),
        };

        // 第一遍：直接存入原始向量，记录基准得分
        IVectorStore storeRaw = new InMemoryVectorStore();
        var rawVecs = new Dictionary<String, Single[]>();
        foreach (var (id, text) in texts)
        {
            var raw = embedder.Embed(text);
            rawVecs[id] = raw;
            await storeRaw.UpsertAsync(new VectorRecord { Id = id, Vector = raw });
        }

        var query      = embedder.Embed("云原生容器化部署微服务");
        var baseResult = await storeRaw.SearchAsync(query, topK: 2);
        Assert.Equal("doc2", baseResult[0].Record.Id);

        // 第二遍：通过 VectorData JSON 序列化再反序列化后存入，验证得分完全一致
        foreach (var (id, text) in texts)
        {
            var json = VectorData.FromVector(embedder.ModelName, rawVecs[id]).ToJson();
            var vec  = VectorData.Parse(json)!.ToVector();     // 模拟从 DB 读取后反序列化
            Assert.Equal(rawVecs[id], vec);                    // 字节级一致是 round-trip 核心保证
            await store.UpsertAsync(new VectorRecord { Id = id, Vector = vec });
        }

        var results = await store.SearchAsync(query, topK: 2);
        Assert.Equal("doc2", results[0].Record.Id);
        // 序列化往返后向量完全相同，得分应与基准完全一致
        Assert.Equal(baseResult[0].Score, results[0].Score);
    }

    [Fact]
    [DisplayName("本地—增量写入新文档后搜索结果随语料更新而变化")]
    public async Task Local_IncrementalUpsert_SearchResultUpdates()
    {
        var embedder = new HashTextEmbedder(512);
        IVectorStore store = new InMemoryVectorStore();

        // 初始语料只有两个主题
        await store.UpsertAsync(new VectorRecord { Id = "finance", Vector = embedder.Embed("股票基金投资理财") });
        await store.UpsertAsync(new VectorRecord { Id = "travel",  Vector = embedder.Embed("旅游景点酒店机票") });

        // 查询医学相关文本——此时只能返回最相近的已有条目
        var medQuery = embedder.Embed("中医药方剂针灸治疗");
        var before   = await store.SearchAsync(medQuery, topK: 1);
        var beforeId = before[0].Record.Id;

        // 增量写入医学文档
        await store.UpsertAsync(new VectorRecord { Id = "medical", Vector = embedder.Embed("医学中医药方剂中药针灸治疗") });

        // 再次搜索，新增文档应成为更近邻
        var after = await store.SearchAsync(medQuery, topK: 1);
        Assert.Equal("medical", after[0].Record.Id);
        Assert.True(after[0].Score > before[0].Score, "新增语义最近邻后得分应提升");
    }

    #endregion

    // ══════════════════════════════════════════════════════════════════════════
    // 第二部分：远程生成器（DashScope）端到端集成
    // ══════════════════════════════════════════════════════════════════════════

    #region DashScope 远程集成

    /// <summary>从 config/DashScope.key 或环境变量 DASHSCOPE_API_KEY 加载 API Key</summary>
    private static String? LoadDashScopeApiKey()
    {
        var configPath = "config/DashScope.key".GetFullPath();
        if (File.Exists(configPath))
        {
            var key = File.ReadAllText(configPath).Trim();
            if (!String.IsNullOrWhiteSpace(key)) return key;
        }
        return Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY");
    }

    /// <summary>未配置 API Key 时跳过远程集成测试</summary>
    private static String EnsureApiKey()
    {
        var key = LoadDashScopeApiKey();
        if (String.IsNullOrWhiteSpace(key))
            throw SkipException.ForSkip("未检测到可用 DashScope API Key（config/DashScope.key 或环境变量 DASHSCOPE_API_KEY），跳过远程 Embedding 集成测试");
        return key!;
    }

    /// <summary>创建用于 Embedding 的 DashScope 客户端（兼容模式）</summary>
    private static DashScopeChatClient CreateEmbeddingClient(String apiKey) =>
        new DashScopeChatClient(new AiClientOptions
        {
            ApiKey   = apiKey,
            Protocol = "ChatCompletions",
        });

    [Fact]
    [DisplayName("DashScope—多主题语料批量嵌入存储，新查询文本语义搜索返回正确主题")]
    public async Task DashScope_BatchEmbedCorpus_NewQueryReturnsSemanticallySimilar()
    {
        var apiKey = EnsureApiKey();
        using var client = CreateEmbeddingClient(apiKey);
        IVectorStore store = new InMemoryVectorStore();

        var corpus = new[]
        {
            ("tech",    "人工智能、机器学习和深度学习是现代科技的核心领域，大语言模型正在改变软件开发方式"),
            ("food",    "中华美食博大精深，八大菜系各有特色，粤菜鲁菜川菜淮扬菜驰名中外"),
            ("sports",  "足球是世界上最受欢迎的运动项目，世界杯每四年举办一次吸引全球数十亿球迷"),
            ("health",  "健康饮食规律作息适度运动是保持身体健康的三大要素，中医强调未病先防"),
        };

        // 批量嵌入语料
        var batchResp = await client.GenerateAsync(new EmbeddingRequest
        {
            Input = corpus.Select(c => c.Item2).ToList(),
            Model = "text-embedding-v3",
        });

        Assert.Equal(corpus.Length, batchResp.Data.Count);

        var records = corpus.Select((c, i) =>
        {
            var r = new VectorRecord { Id = c.Item1, Vector = batchResp.Data[i].Embedding! };
            r.Payload["text"] = c.Item2;
            return r;
        }).ToArray();

        await store.UpsertBatchAsync(records);
        Assert.Equal(corpus.Length, (Int32)await store.CountAsync());

        // 用不同主题的新文本检索，验证语义最近邻正确
        var techQuery   = await client.GenerateAsync(new EmbeddingRequest { Input = ["神经网络与大语言模型在人工智能中的应用"],  Model = "text-embedding-v3" });
        var foodQuery   = await client.GenerateAsync(new EmbeddingRequest { Input = ["四川火锅麻辣鲜香是中国饮食文化的代表"],    Model = "text-embedding-v3" });
        var sportsQuery = await client.GenerateAsync(new EmbeddingRequest { Input = ["欧洲冠军联赛精彩进球与顶级球星表现"],      Model = "text-embedding-v3" });

        var techResults   = await store.SearchAsync(techQuery.Data[0].Embedding!,   topK: 1);
        var foodResults   = await store.SearchAsync(foodQuery.Data[0].Embedding!,   topK: 1);
        var sportsResults = await store.SearchAsync(sportsQuery.Data[0].Embedding!, topK: 1);

        Assert.Equal("tech",   techResults[0].Record.Id);
        Assert.Equal("food",   foodResults[0].Record.Id);
        Assert.Equal("sports", sportsResults[0].Record.Id);

        // 结果按相似度降序
        var allResults = await store.SearchAsync(techQuery.Data[0].Embedding!, topK: 4);
        Assert.Equal(4, allResults.Count);
        for (var i = 0; i < allResults.Count - 1; i++)
            Assert.True(allResults[i].Score >= allResults[i + 1].Score);
    }

    [Fact]
    [DisplayName("DashScope—全路径：批量嵌入→覆盖写入→语义检索→删除→再次检索不含已删条目")]
    public async Task DashScope_FullPipeline_UpsertSearchDelete()
    {
        var apiKey = EnsureApiKey();
        using var client = CreateEmbeddingClient(apiKey);
        IVectorStore store = new InMemoryVectorStore();

        var texts = new[]
        {
            ("cat",  "猫是一种常见的宠物，喜欢睡觉和玩耍，叫声柔和"),
            ("dog",  "狗是人类最忠实的朋友，聪明忠诚，善于看家护院"),
            ("fish", "鱼在水中游动，是安静的观赏宠物，饲养简单"),
        };

        var batchResp = await client.GenerateAsync(new EmbeddingRequest
        {
            Input = texts.Select(t => t.Item2).ToList(),
            Model = "text-embedding-v3",
        });

        await store.UpsertBatchAsync(texts.Select((t, i) =>
            new VectorRecord { Id = t.Item1, Vector = batchResp.Data[i].Embedding! }).ToArray());

        // 覆盖写入 cat 的向量（模拟重新生成向量）
        var newCatResp = await client.GenerateAsync(new EmbeddingRequest
        {
            Input = ["猫咪慵懒可爱，喜欢晒太阳和玩毛线球"],
            Model = "text-embedding-v3",
        });
        await store.UpsertAsync(new VectorRecord { Id = "cat", Vector = newCatResp.Data[0].Embedding! });
        Assert.Equal(3L, await store.CountAsync());   // 覆盖不增加数量

        // 语义检索——猫相关查询应命中 cat
        var queryResp = await client.GenerateAsync(new EmbeddingRequest
        {
            Input = ["我养了一只可爱的猫咪"],
            Model = "text-embedding-v3",
        });
        var results = await store.SearchAsync(queryResp.Data[0].Embedding!, topK: 3);
        Assert.Equal("cat", results[0].Record.Id);

        // 删除 fish 后再搜索不含 fish
        await store.DeleteAsync("fish");
        Assert.Null(await store.GetAsync("fish"));
        Assert.Equal(2L, await store.CountAsync());

        var afterDelete = await store.SearchAsync(queryResp.Data[0].Embedding!, topK: 3);
        Assert.DoesNotContain(afterDelete, r => r.Record.Id == "fish");
    }

    #endregion
}
