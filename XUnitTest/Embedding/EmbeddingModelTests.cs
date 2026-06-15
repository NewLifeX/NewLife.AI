using System;
using System.Collections.Generic;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Clients.OpenAI;
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
    // 通过 OpenAIChatClient 实例化为 IEmbeddingClient 验证元数据属性

    #region EmbeddingClientMetadata

    [Fact]
    [DisplayName("EmbeddingClientMetadata—通过 OpenAIChatClient 验证 ProviderName 为 OpenAI")]
    public void EmbeddingClientMetadata_ViaOpenAIChatClient_ProviderName()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAIChatClient(opts);
        var embedding = (IEmbeddingClient)client;
        Assert.Equal("OpenAI", embedding.Metadata.ProviderName);
    }

    [Fact]
    [DisplayName("EmbeddingClientMetadata—Endpoint 使用 options.Endpoint 覆盖默认地址")]
    public void EmbeddingClientMetadata_ViaOpenAIChatClient_EndpointOverride()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test", Endpoint = "https://custom.example.com" };
        var client = new OpenAIChatClient(opts);
        var embedding = (IEmbeddingClient)client;
        Assert.Equal("https://custom.example.com", embedding.Metadata.Endpoint);
    }

    [Fact]
    [DisplayName("EmbeddingClientMetadata—DefaultModel 等于 options.Model")]
    public void EmbeddingClientMetadata_DefaultModel_FromOptions()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test", Model = "text-embedding-3-small" };
        var client = new OpenAIChatClient(opts);
        Assert.Equal("text-embedding-3-small", ((IEmbeddingClient)client).Metadata.DefaultModel);
    }

    [Fact]
    [DisplayName("EmbeddingClientMetadata—options.Model 未设置时 DefaultModel 为 null")]
    public void EmbeddingClientMetadata_DefaultModel_Null_WhenNotSet()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAIChatClient(opts);
        Assert.Null(((IEmbeddingClient)client).Metadata.DefaultModel);
    }

    #endregion

    // ── OpenAIChatClient as IEmbeddingClient ─────────────────────────────────

    #region OpenAIChatClient 嵌入实现

    [Fact]
    [DisplayName("OpenAIChatClient—实现 IEmbeddingClient 接口")]
    public void OpenAIChatClient_Implements_IEmbeddingClient()
    {
        var client = new OpenAIChatClient(new AiClientOptions { ApiKey = "sk-test" });
        Assert.IsAssignableFrom<IEmbeddingClient>(client);
    }

    [Fact]
    [DisplayName("OpenAIChatClient—Metadata.ProviderName 从类名自动设为 OpenAI")]
    public void OpenAIChatClient_EmbeddingMetadata_ProviderName()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAIChatClient(opts);
        Assert.Equal("OpenAI", client.Metadata.ProviderName);
    }

    [Fact]
    [DisplayName("OpenAIChatClient—Metadata.Endpoint 使用 options.Endpoint 覆盖")]
    public void OpenAIChatClient_EmbeddingMetadata_Endpoint_UseOptionsEndpoint()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test", Endpoint = "https://custom.endpoint.com" };
        var client = new OpenAIChatClient(opts);
        Assert.Equal("https://custom.endpoint.com", client.Metadata.Endpoint);
    }

    [Fact]
    [DisplayName("OpenAIChatClient—Metadata.Endpoint 未设置时返回 OpenAI 官方地址")]
    public void OpenAIChatClient_EmbeddingMetadata_Endpoint_Default()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test", Endpoint = null };
        var client = new OpenAIChatClient(opts);
        // 注册表将 OpenAI 映射到 https://api.openai.com
        Assert.Contains("openai.com", client.Metadata.Endpoint);
    }

    [Fact]
    [DisplayName("OpenAIChatClient—Dispose 不抛异常")]
    public void OpenAIChatClient_Dispose_DoesNotThrow()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAIChatClient(opts);
        client.Dispose(); // should not throw
    }

    [Fact]
    [DisplayName("OpenAIChatClient—默认 Timeout 为 5 分钟")]
    public void OpenAIChatClient_DefaultTimeout_FiveMinutes()
    {
        var opts = new AiClientOptions { ApiKey = "sk-test" };
        var client = new OpenAIChatClient(opts);
        Assert.Equal(TimeSpan.FromMinutes(5), client.Timeout);
    }

    #endregion
}
