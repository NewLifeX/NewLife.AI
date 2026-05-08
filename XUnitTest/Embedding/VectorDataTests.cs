using System;
using System.ComponentModel;
using NewLife;
using NewLife.AI.Embedding;
using Xunit;

namespace XUnitTest.Embedding;

/// <summary>VectorData 序列化包装单元测试</summary>
[DisplayName("VectorData 单元测试")]
public class VectorDataTests
{
    // ── FromVector ───────────────────────────────────────────────────────────

    #region FromVector

    [Fact]
    [DisplayName("FromVector—正常向量可往返解码")]
    public void FromVector_Normal_RoundTrip()
    {
        var vector = new Single[] { 0.1f, 0.5f, -0.3f, 0.8f };
        var vd = VectorData.FromVector("local-hash-v1", vector);

        Assert.Equal("local-hash-v1", vd.Model);
        Assert.Equal(4, vd.Dims);
        Assert.False(vd.Data.IsNullOrEmpty());

        var decoded = vd.ToVector();
        Assert.Equal(vector.Length, decoded.Length);
        for (var i = 0; i < vector.Length; i++)
            Assert.Equal(vector[i], decoded[i], precision: 6);
    }

    [Fact]
    [DisplayName("FromVector—单元素向量可往返解码")]
    public void FromVector_SingleElement_RoundTrip()
    {
        var vector = new Single[] { 1.0f };
        var vd = VectorData.FromVector("test-model", vector);

        Assert.Equal(1, vd.Dims);
        var decoded = vd.ToVector();
        Assert.Single(decoded);
        Assert.Equal(1.0f, decoded[0], precision: 6);
    }

    [Fact]
    [DisplayName("FromVector—空向量生成 Dims=0 的实例")]
    public void FromVector_EmptyVector_DimsZero()
    {
        var vd = VectorData.FromVector("m", []);
        Assert.Equal(0, vd.Dims);
        Assert.Equal("", vd.Data); // Buffer.BlockCopy 零字节 → Base64("") = ""
    }

    [Fact]
    [DisplayName("FromVector—null model 抛 ArgumentNullException")]
    public void FromVector_NullModel_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => VectorData.FromVector(null!, [1.0f]));
    }

    [Fact]
    [DisplayName("FromVector—null vector 抛 ArgumentNullException")]
    public void FromVector_NullVector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => VectorData.FromVector("m", null!));
    }

    #endregion

    // ── Parse ────────────────────────────────────────────────────────────────

    #region Parse

    [Fact]
    [DisplayName("Parse—null 返回 null")]
    public void Parse_Null_ReturnsNull()
    {
        Assert.Null(VectorData.Parse(null));
    }

    [Fact]
    [DisplayName("Parse—空字符串返回 null")]
    public void Parse_Empty_ReturnsNull()
    {
        Assert.Null(VectorData.Parse(""));
    }

    [Fact]
    [DisplayName("Parse—纯空白字符串返回 null")]
    public void Parse_Whitespace_ReturnsNull()
    {
        Assert.Null(VectorData.Parse("   "));
    }

    [Fact]
    [DisplayName("Parse—旧格式 JSON 数组（[ 开头）返回 null")]
    public void Parse_LegacyArrayFormat_ReturnsNull()
    {
        Assert.Null(VectorData.Parse("[0.1, 0.2, 0.3]"));
    }

    [Fact]
    [DisplayName("Parse—旧格式前置空白加 [ 也返回 null")]
    public void Parse_LegacyArrayWithLeadingSpaces_ReturnsNull()
    {
        Assert.Null(VectorData.Parse("  [0.1]"));
    }

    [Fact]
    [DisplayName("Parse—合法 JSON 正确解析各字段")]
    public void Parse_ValidJson_ParsesCorrectly()
    {
        var vector = new Single[] { 0.5f, -0.5f };
        var original = VectorData.FromVector("local-hash-v1", vector);
        var json = original.ToJson();

        var parsed = VectorData.Parse(json);
        Assert.NotNull(parsed);
        Assert.Equal("local-hash-v1", parsed!.Model);
        Assert.Equal(2, parsed.Dims);
        Assert.False(parsed.Data.IsNullOrEmpty());
    }

    [Fact]
    [DisplayName("Parse—畸形 JSON 返回 null")]
    public void Parse_MalformedJson_ReturnsNull()
    {
        Assert.Null(VectorData.Parse("{not valid json"));
    }

    [Fact]
    [DisplayName("Parse—有效 JSON 但字段缺失时默认值正确")]
    public void Parse_JsonMissingFields_DefaultValues()
    {
        var parsed = VectorData.Parse("{\"model\":\"x\"}");
        Assert.NotNull(parsed);
        Assert.Equal("x", parsed!.Model);
        Assert.Equal(0, parsed.Dims);
        Assert.Equal("", parsed.Data);
    }

    #endregion

    // ── ToVector ─────────────────────────────────────────────────────────────

    #region ToVector

    [Fact]
    [DisplayName("ToVector—Data 为空时返回空数组")]
    public void ToVector_EmptyData_ReturnsEmpty()
    {
        var vd = new VectorData { Model = "m", Dims = 0, Data = "" };
        Assert.Empty(vd.ToVector());
    }

    [Fact]
    [DisplayName("ToVector—正常 Base64 还原正确向量")]
    public void ToVector_Normal_CorrectVector()
    {
        var vector = new Single[] { 1.0f, 2.0f, 3.0f };
        var vd = VectorData.FromVector("m", vector);
        var result = vd.ToVector();

        Assert.Equal(3, result.Length);
        Assert.Equal(1.0f, result[0], precision: 6);
        Assert.Equal(2.0f, result[1], precision: 6);
        Assert.Equal(3.0f, result[2], precision: 6);
    }

    #endregion

    // ── IsStale ──────────────────────────────────────────────────────────────

    #region IsStale

    [Fact]
    [DisplayName("IsStale—currentModel 为 null 时返回 false")]
    public void IsStale_NullCurrentModel_ReturnsFalse()
    {
        var vd = VectorData.FromVector("local-hash-v1", [1.0f]);
        Assert.False(vd.IsStale(null!));
    }

    [Fact]
    [DisplayName("IsStale—currentModel 为空字符串时返回 false")]
    public void IsStale_EmptyCurrentModel_ReturnsFalse()
    {
        var vd = VectorData.FromVector("local-hash-v1", [1.0f]);
        Assert.False(vd.IsStale(""));
    }

    [Fact]
    [DisplayName("IsStale—同名同维（currentDimensions=0）返回 false")]
    public void IsStale_SameModelNoDimCheck_ReturnsFalse()
    {
        var vector = new Single[512];
        var vd = VectorData.FromVector("local-hash-v1", vector);
        Assert.False(vd.IsStale("local-hash-v1", 0));
    }

    [Fact]
    [DisplayName("IsStale—同名同维（currentDimensions=512）返回 false")]
    public void IsStale_SameModelSameDims_ReturnsFalse()
    {
        var vector = new Single[512];
        var vd = VectorData.FromVector("local-hash-v1", vector);
        Assert.False(vd.IsStale("local-hash-v1", 512));
    }

    [Fact]
    [DisplayName("IsStale—同名异维（currentDimensions=256）返回 true")]
    public void IsStale_SameModelDifferentDims_ReturnsTrue()
    {
        var vector = new Single[512];
        var vd = VectorData.FromVector("local-hash-v1", vector);
        Assert.True(vd.IsStale("local-hash-v1", 256));
    }

    [Fact]
    [DisplayName("IsStale—异名不校验维度（currentDimensions=0）返回 true")]
    public void IsStale_DifferentModelNoDimCheck_ReturnsTrue()
    {
        var vd = VectorData.FromVector("local-hash-v1", [1.0f]);
        Assert.True(vd.IsStale("text-embedding-3-small", 0));
    }

    [Fact]
    [DisplayName("IsStale—异名且维度不同返回 true")]
    public void IsStale_DifferentModelDifferentDims_ReturnsTrue()
    {
        var vector = new Single[512];
        var vd = VectorData.FromVector("local-hash-v1", vector);
        Assert.True(vd.IsStale("text-embedding-3-small", 256));
    }

    [Fact]
    [DisplayName("IsStale—大小写不敏感比较模型名")]
    public void IsStale_CaseInsensitiveModelName_ReturnsFalse()
    {
        var vd = VectorData.FromVector("local-hash-v1", [1.0f]);
        Assert.False(vd.IsStale("LOCAL-HASH-V1", 0));
    }

    #endregion

    // ── ToJson ───────────────────────────────────────────────────────────────

    #region ToJson

    [Fact]
    [DisplayName("ToJson—序列化包含 model、dims、data 字段")]
    public void ToJson_ContainsExpectedFields()
    {
        var vd = VectorData.FromVector("local-hash-v1", new Single[] { 0.5f });
        var json = vd.ToJson();

        Assert.Contains("local-hash-v1", json);
        // NewLife JSON 序列化输出 PascalCase 键
        Assert.True(json.Contains("\"Model\"") || json.Contains("\"model\""), "应包含 Model 字段");
        Assert.True(json.Contains("\"Dims\"") || json.Contains("\"dims\""), "应包含 Dims 字段");
        Assert.True(json.Contains("\"Data\"") || json.Contains("\"data\""), "应包含 Data 字段");
    }

    [Fact]
    [DisplayName("ToJson—序列化后可被 Parse 还原")]
    public void ToJson_CanBeRoundTripped()
    {
        var vector = new Single[] { 0.3f, -0.7f, 0.1f };
        var original = VectorData.FromVector("some-model", vector);
        var json = original.ToJson();
        var parsed = VectorData.Parse(json);

        Assert.NotNull(parsed);
        Assert.Equal(original.Model, parsed!.Model);
        Assert.Equal(original.Dims, parsed.Dims);
        Assert.Equal(original.Data, parsed.Data);
    }

    #endregion
}
