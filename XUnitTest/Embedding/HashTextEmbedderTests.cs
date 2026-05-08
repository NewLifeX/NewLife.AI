using System;
using System.ComponentModel;
using System.Linq;
using NewLife;
using NewLife.AI.Embedding;
using Xunit;

namespace XUnitTest.Embedding;

/// <summary>HashTextEmbedder 本地哈希嵌入器单元测试</summary>
[DisplayName("HashTextEmbedder 单元测试")]
public class HashTextEmbedderTests
{
    // ── 构造 ─────────────────────────────────────────────────────────────────

    #region 构造

    [Fact]
    [DisplayName("构造—默认维度为 512")]
    public void Constructor_Default_Dimensions512()
    {
        var embedder = new HashTextEmbedder();
        Assert.Equal(512, embedder.Dimensions);
    }

    [Fact]
    [DisplayName("构造—自定义维度 256 生效")]
    public void Constructor_Custom256_DimensionsSet()
    {
        var embedder = new HashTextEmbedder(256);
        Assert.Equal(256, embedder.Dimensions);
    }

    [Fact]
    [DisplayName("构造—维度 1 时正常构造")]
    public void Constructor_Dimension1_Works()
    {
        var embedder = new HashTextEmbedder(1);
        Assert.Equal(1, embedder.Dimensions);
    }

    [Fact]
    [DisplayName("构造—维度 0 抛 ArgumentOutOfRangeException")]
    public void Constructor_ZeroDimensions_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HashTextEmbedder(0));
    }

    [Fact]
    [DisplayName("构造—负数维度抛 ArgumentOutOfRangeException")]
    public void Constructor_NegativeDimensions_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HashTextEmbedder(-1));
    }

    #endregion

    // ── ModelName / Dimensions 属性 ──────────────────────────────────────────

    #region 属性

    [Fact]
    [DisplayName("ModelName—固定返回 local-hash-v1（不含维度）")]
    public void ModelName_AlwaysLocalHashV1()
    {
        var e512 = new HashTextEmbedder(512);
        var e256 = new HashTextEmbedder(256);
        Assert.Equal("local-hash-v1", e512.ModelName);
        Assert.Equal("local-hash-v1", e256.ModelName);
    }

    [Fact]
    [DisplayName("Dimensions—与构造参数一致")]
    public void Dimensions_MatchesConstructorArg()
    {
        Assert.Equal(128, new HashTextEmbedder(128).Dimensions);
        Assert.Equal(1024, new HashTextEmbedder(1024).Dimensions);
    }

    #endregion

    // ── Embed —— 输入边界 ────────────────────────────────────────────────────

    #region Embed 边界输入

    [Fact]
    [DisplayName("Embed—null 文本返回零向量")]
    public void Embed_Null_ReturnsZeroVector()
    {
        var embedder = new HashTextEmbedder(512);
        var result = embedder.Embed(null!);
        Assert.Equal(512, result.Length);
        Assert.All(result, v => Assert.Equal(0f, v));
    }

    [Fact]
    [DisplayName("Embed—空字符串返回零向量")]
    public void Embed_EmptyString_ReturnsZeroVector()
    {
        var embedder = new HashTextEmbedder(512);
        var result = embedder.Embed("");
        Assert.Equal(512, result.Length);
        Assert.All(result, v => Assert.Equal(0f, v));
    }

    [Fact]
    [DisplayName("Embed—纯空白字符串返回零向量")]
    public void Embed_Whitespace_ReturnsZeroVector()
    {
        var embedder = new HashTextEmbedder(512);
        var result = embedder.Embed("   \t\n");
        Assert.Equal(512, result.Length);
        Assert.All(result, v => Assert.Equal(0f, v));
    }

    [Fact]
    [DisplayName("Embed—纯标点（无有效 token）返回零向量")]
    public void Embed_PunctuationOnly_ReturnsZeroVector()
    {
        var embedder = new HashTextEmbedder(512);
        // 单个字母长度 < 2 被 FlushWord 丢弃；标点不产生 token
        var result = embedder.Embed("!@#$%^&*().,");
        Assert.Equal(512, result.Length);
        Assert.All(result, v => Assert.Equal(0f, v));
    }

    #endregion

    // ── Embed —— 向量属性 ────────────────────────────────────────────────────

    #region Embed 向量属性

    [Fact]
    [DisplayName("Embed—中文文本向量长度等于 Dimensions")]
    public void Embed_Chinese_VectorLengthEqualsDimensions()
    {
        var embedder = new HashTextEmbedder(256);
        var result = embedder.Embed("这是一段中文文本");
        Assert.Equal(256, result.Length);
    }

    [Fact]
    [DisplayName("Embed—英文文本向量长度等于 Dimensions")]
    public void Embed_English_VectorLengthEqualsDimensions()
    {
        var embedder = new HashTextEmbedder(128);
        var result = embedder.Embed("hello world this is english text");
        Assert.Equal(128, result.Length);
    }

    [Fact]
    [DisplayName("Embed—中文文本 L2 范数约等于 1.0")]
    public void Embed_Chinese_L2NormApprox1()
    {
        var embedder = new HashTextEmbedder(512);
        var vec = embedder.Embed("向量化测试：新生命团队开发的嵌入算法");
        var norm = MathF.Sqrt(vec.Sum(v => v * v));
        Assert.True(MathF.Abs(norm - 1.0f) < 1e-5f, $"L2 norm = {norm}, expected ≈ 1.0");
    }

    [Fact]
    [DisplayName("Embed—英文文本 L2 范数约等于 1.0")]
    public void Embed_English_L2NormApprox1()
    {
        var embedder = new HashTextEmbedder(512);
        var vec = embedder.Embed("the quick brown fox jumps over the lazy dog");
        var norm = MathF.Sqrt(vec.Sum(v => v * v));
        Assert.True(MathF.Abs(norm - 1.0f) < 1e-5f, $"L2 norm = {norm}, expected ≈ 1.0");
    }

    [Fact]
    [DisplayName("Embed—相同输入产生确定性输出")]
    public void Embed_SameInput_DeterministicOutput()
    {
        var embedder = new HashTextEmbedder(512);
        var text = "确定性测试 deterministic test";
        var v1 = embedder.Embed(text);
        var v2 = embedder.Embed(text);
        Assert.Equal(v1, v2);
    }

    [Fact]
    [DisplayName("Embed—不同文本产生不同向量")]
    public void Embed_DifferentInputs_DifferentVectors()
    {
        var embedder = new HashTextEmbedder(512);
        var v1 = embedder.Embed("苹果是水果");
        var v2 = embedder.Embed("汽车是交通工具");
        Assert.False(v1.SequenceEqual(v2));
    }

    [Fact]
    [DisplayName("Embed—维度不同时向量长度不同")]
    public void Embed_DifferentDimensions_DifferentLengths()
    {
        var e256 = new HashTextEmbedder(256);
        var e512 = new HashTextEmbedder(512);
        var text = "测试维度";
        Assert.Equal(256, e256.Embed(text).Length);
        Assert.Equal(512, e512.Embed(text).Length);
    }

    #endregion

    // ── Embed —— 各语言文本 ──────────────────────────────────────────────────

    #region Embed 语言覆盖

    [Fact]
    [DisplayName("Embed—单个 CJK 字符返回非零向量（单字 token）")]
    public void Embed_SingleCjkChar_NonZeroVector()
    {
        var embedder = new HashTextEmbedder(512);
        var result = embedder.Embed("好");
        var norm = MathF.Sqrt(result.Sum(v => v * v));
        Assert.True(norm > 0f, "单个 CJK 字应产生非零向量");
    }

    [Fact]
    [DisplayName("Embed—韩文文本产生非零向量")]
    public void Embed_Korean_NonZeroVector()
    {
        var embedder = new HashTextEmbedder(512);
        var result = embedder.Embed("안녕하세요");
        var norm = MathF.Sqrt(result.Sum(v => v * v));
        Assert.True(norm > 0f, "韩文文本应产生非零向量");
        Assert.Equal(512, result.Length);
    }

    [Fact]
    [DisplayName("Embed—日文平假名文本产生非零向量")]
    public void Embed_Japanese_NonZeroVector()
    {
        var embedder = new HashTextEmbedder(512);
        var result = embedder.Embed("こんにちは");
        var norm = MathF.Sqrt(result.Sum(v => v * v));
        Assert.True(norm > 0f, "日文平假名文本应产生非零向量");
        Assert.Equal(512, result.Length);
    }

    [Fact]
    [DisplayName("Embed—中英混合文本 L2 范数约等于 1.0")]
    public void Embed_Mixed_L2NormApprox1()
    {
        var embedder = new HashTextEmbedder(512);
        var vec = embedder.Embed("新生命团队 NewLife Team 开发了 AI 框架");
        var norm = MathF.Sqrt(vec.Sum(v => v * v));
        Assert.True(MathF.Abs(norm - 1.0f) < 1e-5f, $"L2 norm = {norm}, expected ≈ 1.0");
    }

    [Fact]
    [DisplayName("Embed—重复词文本产生有效归一化向量")]
    public void Embed_RepeatedWords_ValidNormalizedVector()
    {
        var embedder = new HashTextEmbedder(512);
        var vec = embedder.Embed("测试 测试 测试 测试 测试");
        var norm = MathF.Sqrt(vec.Sum(v => v * v));
        Assert.True(MathF.Abs(norm - 1.0f) < 1e-5f);
    }

    #endregion

    // ── ILocalTextEmbedder 接口 ──────────────────────────────────────────────

    #region 接口实现

    [Fact]
    [DisplayName("HashTextEmbedder 实现 ILocalTextEmbedder 接口")]
    public void HashTextEmbedder_ImplementsInterface()
    {
        ILocalTextEmbedder embedder = new HashTextEmbedder(512);
        Assert.Equal("local-hash-v1", embedder.ModelName);
        Assert.Equal(512, embedder.Dimensions);
        var result = embedder.Embed("接口测试");
        Assert.Equal(512, result.Length);
    }

    #endregion
}
