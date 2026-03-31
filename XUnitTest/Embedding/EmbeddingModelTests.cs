using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Embedding;
using Xunit;

namespace XUnitTest.Embedding;

/// <summary>Embedding 嵌入向量模型层单元测试</summary>
[DisplayName("Embedding 模型层单元测试")]
public class EmbeddingModelTests
{
    // ── EmbeddingRequest ─────────────────────────────────────────────────────

    #region EmbeddingRequest

    [Fact]
    [DisplayName("EmbeddingRequest—默认 Input 为空列表")]
    public void EmbeddingRequest_DefaultInput_Empty()
    {
        var req = new EmbeddingRequest();
        Assert.NotNull(req.Input);
        Assert.Empty(req.Input);
    }

    [Fact]
    [DisplayName("EmbeddingRequest—Input 列表可读写")]
    public void EmbeddingRequest_Input_ReadWrite()
    {
        var req = new EmbeddingRequest { Input = ["text1", "text2"] };
        Assert.Equal(2, req.Input.Count);
        Assert.Equal("text1", req.Input[0]);
    }

    [Fact]
    [DisplayName("EmbeddingRequest—Model 属性读写")]
    public void EmbeddingRequest_Model_ReadWrite()
    {
        var req = new EmbeddingRequest { Model = "text-embedding-3-small" };
        Assert.Equal("text-embedding-3-small", req.Model);
    }

    [Fact]
    [DisplayName("EmbeddingRequest—Dimensions 属性读写")]
    public void EmbeddingRequest_Dimensions_ReadWrite()
    {
        var req = new EmbeddingRequest { Dimensions = 256 };
        Assert.Equal(256, req.Dimensions);
    }

    [Fact]
    [DisplayName("EmbeddingRequest—EncodingFormat 属性读写")]
    public void EmbeddingRequest_EncodingFormat_ReadWrite()
    {
        var req = new EmbeddingRequest { EncodingFormat = "base64" };
        Assert.Equal("base64", req.EncodingFormat);
    }

    [Fact]
    [DisplayName("EmbeddingRequest—User 属性读写")]
    public void EmbeddingRequest_User_ReadWrite()
    {
        var req = new EmbeddingRequest { User = "test-user" };
        Assert.Equal("test-user", req.User);
    }

    [Fact]
    [DisplayName("EmbeddingRequest—默认 Model/Dimensions/EncodingFormat/User 为 null")]
    public void EmbeddingRequest_DefaultNulls()
    {
        var req = new EmbeddingRequest();
        Assert.Null(req.Model);
        Assert.Null(req.Dimensions);
        Assert.Null(req.EncodingFormat);
        Assert.Null(req.User);
    }

    #endregion

    // ── EmbeddingResponse ────────────────────────────────────────────────────

    #region EmbeddingResponse

    [Fact]
    [DisplayName("EmbeddingResponse—默认 Data 为空列表")]
    public void EmbeddingResponse_DefaultData_Empty()
    {
        var resp = new EmbeddingResponse();
        Assert.NotNull(resp.Data);
        Assert.Empty(resp.Data);
    }

    [Fact]
    [DisplayName("EmbeddingResponse—Model 属性读写")]
    public void EmbeddingResponse_Model_ReadWrite()
    {
        var resp = new EmbeddingResponse { Model = "text-embedding-ada-002" };
        Assert.Equal("text-embedding-ada-002", resp.Model);
    }

    [Fact]
    [DisplayName("EmbeddingResponse—Usage 属性读写")]
    public void EmbeddingResponse_Usage_ReadWrite()
    {
        var resp = new EmbeddingResponse
        {
            Usage = new EmbeddingUsage { PromptTokens = 10, TotalTokens = 10 }
        };
        Assert.NotNull(resp.Usage);
        Assert.Equal(10, resp.Usage!.PromptTokens);
    }

    [Fact]
    [DisplayName("EmbeddingResponse—Data 列表可添加 EmbeddingItem")]
    public void EmbeddingResponse_Data_CanAddItems()
    {
        var resp = new EmbeddingResponse();
        resp.Data.Add(new EmbeddingItem { Index = 0, Embedding = [1.0f, 0.5f, 0.0f] });
        Assert.Single(resp.Data);
        Assert.Equal(0, resp.Data[0].Index);
    }

    #endregion

    // ── EmbeddingItem ────────────────────────────────────────────────────────

    #region EmbeddingItem

    [Fact]
    [DisplayName("EmbeddingItem—Index 属性读写")]
    public void EmbeddingItem_Index_ReadWrite()
    {
        var item = new EmbeddingItem { Index = 2 };
        Assert.Equal(2, item.Index);
    }

    [Fact]
    [DisplayName("EmbeddingItem—Embedding 数组读写")]
    public void EmbeddingItem_Embedding_ReadWrite()
    {
        var vec = new Single[] { 0.1f, 0.2f, 0.3f };
        var item = new EmbeddingItem { Embedding = vec };
        Assert.Equal(vec, item.Embedding);
        Assert.Equal(3, item.Embedding!.Length);
    }

    [Fact]
    [DisplayName("EmbeddingItem—默认 Embedding 为 null")]
    public void EmbeddingItem_DefaultEmbedding_Null()
    {
        var item = new EmbeddingItem();
        Assert.Null(item.Embedding);
    }

    #endregion

    // ── EmbeddingUsage ───────────────────────────────────────────────────────

    #region EmbeddingUsage

    [Fact]
    [DisplayName("EmbeddingUsage—PromptTokens 和 TotalTokens 读写")]
    public void EmbeddingUsage_Properties_ReadWrite()
    {
        var usage = new EmbeddingUsage { PromptTokens = 50, TotalTokens = 50 };
        Assert.Equal(50, usage.PromptTokens);
        Assert.Equal(50, usage.TotalTokens);
    }

    [Fact]
    [DisplayName("EmbeddingUsage—默认值均为 0")]
    public void EmbeddingUsage_Defaults_Zero()
    {
        var usage = new EmbeddingUsage();
        Assert.Equal(0, usage.PromptTokens);
        Assert.Equal(0, usage.TotalTokens);
    }

    #endregion

    // ── EmbeddingClientMetadata ──────────────────────────────────────────────
    // 通过 OpenAiEmbeddingClient 构造函数间接验证元数据属性（避免直接使用 init 属性出现兼容问题）

    #region EmbeddingClientMetadata

    [Fact]
    [DisplayName("EmbeddingClientMetadata—通过 Client 构造函数验证 ProviderName 和 Endpoint")]
    public void EmbeddingClientMetadata_ViaClient_ProviderNameAndEndpoint()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAiEmbeddingClient("AliDashScope", "https://default.example.com", opts);
        Assert.Equal("AliDashScope", client.Metadata.ProviderName);
        Assert.Equal("https://default.example.com", client.Metadata.Endpoint);
    }

    [Fact]
    [DisplayName("EmbeddingClientMetadata—Endpoint 默认为 null（DefaultModelId 未设置时）")]
    public void EmbeddingClientMetadata_DefaultModelId_Null()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAiEmbeddingClient("Test", "https://x.com", opts);
        // DefaultModelId 在 OpenAiEmbeddingClient 构造时不设置
        Assert.Null(client.Metadata.DefaultModelId);
    }

    #endregion

    // ── OpenAiEmbeddingClient ────────────────────────────────────────────────

    #region OpenAiEmbeddingClient

    [Fact]
    [DisplayName("OpenAiEmbeddingClient—构造后 Metadata.ProviderName 正确")]
    public void OpenAiEmbeddingClient_Metadata_ProviderName()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAiEmbeddingClient("DashScope", "https://dashscope.aliyuncs.com", opts);
        Assert.Equal("DashScope", client.Metadata.ProviderName);
    }

    [Fact]
    [DisplayName("OpenAiEmbeddingClient—构造后 Metadata.Endpoint 使用 options.Endpoint 覆盖")]
    public void OpenAiEmbeddingClient_Metadata_Endpoint_UseOptionsEndpoint()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test", Endpoint = "https://custom.endpoint.com" };
        var client = new OpenAiEmbeddingClient("Test", "https://default.com", opts);
        Assert.Equal("https://custom.endpoint.com", client.Metadata.Endpoint);
    }

    [Fact]
    [DisplayName("OpenAiEmbeddingClient—options.Endpoint 为 null 时使用默认 Endpoint")]
    public void OpenAiEmbeddingClient_Metadata_Endpoint_UseDefault()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test", Endpoint = null };
        var client = new OpenAiEmbeddingClient("Test", "https://default.com", opts);
        Assert.Equal("https://default.com", client.Metadata.Endpoint);
    }

    [Fact]
    [DisplayName("OpenAiEmbeddingClient—null providerName 抛 ArgumentNullException")]
    public void OpenAiEmbeddingClient_NullProviderName_Throws()
    {
        var opts = new AiClientOptions();
        Assert.Throws<ArgumentNullException>(() => new OpenAiEmbeddingClient(null!, "https://x.com", opts));
    }

    [Fact]
    [DisplayName("OpenAiEmbeddingClient—null defaultEndpoint 抛 ArgumentNullException")]
    public void OpenAiEmbeddingClient_NullEndpoint_Throws()
    {
        var opts = new AiClientOptions();
        Assert.Throws<ArgumentNullException>(() => new OpenAiEmbeddingClient("Test", null!, opts));
    }

    [Fact]
    [DisplayName("OpenAiEmbeddingClient—null options 抛 ArgumentNullException")]
    public void OpenAiEmbeddingClient_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OpenAiEmbeddingClient("Test", "https://x.com", null!));
    }

    [Fact]
    [DisplayName("OpenAiEmbeddingClient—Dispose 不抛异常")]
    public void OpenAiEmbeddingClient_Dispose_DoesNotThrow()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAiEmbeddingClient("Test", "https://x.com", opts);
        client.Dispose(); // should not throw
    }

    [Fact]
    [DisplayName("OpenAiEmbeddingClient—默认 Timeout 为 2 分钟")]
    public void OpenAiEmbeddingClient_DefaultTimeout_TwoMinutes()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAiEmbeddingClient("Test", "https://x.com", opts);
        Assert.Equal(TimeSpan.FromMinutes(2), client.Timeout);
    }

    #endregion
}
