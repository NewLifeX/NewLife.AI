using System;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Models;
using Xunit;

namespace XUnitTest.Clients;

/// <summary>模型元数据解析链 <see cref="ModelMetadataResolverChain"/> 单元测试：
/// 验证多解析器按 Order 升序合成、高优先级声明优先、低优先级只补缺、价格整块不混写、
/// 同类型后注册覆盖、能力分级兜底等核心语义</summary>
/// <remarks>测试使用的 fake 解析器只匹配专属 provider 编码（ChainTest 前缀），
/// 避免污染其它测试对解析链的消费；解析链为进程级静态注册表，注册结果可残留但无副作用</remarks>
public class ModelMetadataChainTests
{
    /// <summary>可控 fake 解析器：仅当 provider 匹配指定编码时返回给定块，否则 null（不干扰其它模型）</summary>
    /// <param name="order">优先级</param>
    /// <param name="provider">匹配的提供商编码</param>
    /// <param name="block">返回的元数据块</param>
    private sealed class FakeResolver(Int32 order, String? provider, AiModelMetadata? block = null) : IModelMetadataResolver
    {
        public Int32 Order => order;
        public String? Provider => provider;
        public AiModelMetadata? Block => block;

        public AiModelMetadata? Resolve(AiModelMetadata? current, String? provider, String? model)
            => Provider != null && String.Equals(Provider, provider, StringComparison.OrdinalIgnoreCase) ? Block : null;
    }

    private const String P = "ChainTest-Provider";

    [Fact]
    [DisplayName("合并：高优先级声明字段生效，低优先级只补未声明字段")]
    public void Resolve_MergeHighThenLow()
    {
        ModelMetadataResolverChain.Register(new FakeResolver(1, P, new AiModelMetadata(SupportThinking: true, ContextLength: 131072)));
        ModelMetadataResolverChain.Register(new FakeResolver(2, P, new AiModelMetadata(SupportVision: true)));

        var md = ModelMetadataResolverChain.Resolve(P, "m1");

        Assert.NotNull(md);
        Assert.True(md!.SupportThinking);
        Assert.Equal(131072, md.ContextLength);
        Assert.True(md.SupportVision);
    }

    [Fact]
    [DisplayName("冲突字段：高优先级（Order 小）声明优先于低优先级")]
    public void Resolve_HighPriorityWinsConflict()
    {
        ModelMetadataResolverChain.Register(new FakeResolver(1, P, new AiModelMetadata(SupportThinking: false)));
        ModelMetadataResolverChain.Register(new FakeResolver(2, P, new AiModelMetadata(SupportThinking: true)));

        var md = ModelMetadataResolverChain.Resolve(P, "m2");

        Assert.NotNull(md);
        Assert.False(md!.SupportThinking);
    }

    [Fact]
    [DisplayName("价格整块：已有 Token 四档价时不补入低优先级非 Token 单价")]
    public void Resolve_PriceKeptWhole_TokenFirst()
    {
        ModelMetadataResolverChain.Register(new FakeResolver(1, P, new AiModelMetadata(
            PricingMode: PricingMode.Token,
            Pricing: new AiModelPricing(1m, 2m, 3m, 4m))));
        ModelMetadataResolverChain.Register(new FakeResolver(2, P, new AiModelMetadata(
            PricingMode: PricingMode.Embedding, UnitPrice: 0.5m, UnitName: "百万Token")));

        var md = ModelMetadataResolverChain.Resolve(P, "m3");

        Assert.NotNull(md);
        Assert.NotNull(md!.Pricing);
        Assert.Equal(1m, md.Pricing.InputPrice);
        Assert.Equal(PricingMode.Token, md.PricingMode);
        // 非 Token 单价不补入，避免 Token/单价并存
        Assert.Null(md.UnitPrice);
    }

    [Fact]
    [DisplayName("价格整块：已有非 Token 单价时不补入低优先级 Token 四档价")]
    public void Resolve_PriceKeptWhole_UnitFirst()
    {
        ModelMetadataResolverChain.Register(new FakeResolver(1, P, new AiModelMetadata(
            PricingMode: PricingMode.Image, UnitPrice: 0.2m, UnitName: "张")));
        ModelMetadataResolverChain.Register(new FakeResolver(2, P, new AiModelMetadata(
            PricingMode: PricingMode.Token,
            Pricing: new AiModelPricing(2m, 12m, 0.2m, 0m))));

        var md = ModelMetadataResolverChain.Resolve(P, "m4");

        Assert.NotNull(md);
        Assert.Equal(PricingMode.Image, md!.PricingMode);
        Assert.Equal(0.2m, md.UnitPrice);
        Assert.Null(md.Pricing);
    }

    [Fact]
    [DisplayName("同类型解析器后注册覆盖：测试/不同目录场景可替换同款解析器实例")]
    public void Register_SameTypeReplace()
    {
        ModelMetadataResolverChain.Register(new FakeResolver(1, P, new AiModelMetadata(Name: "旧名")));
        ModelMetadataResolverChain.Register(new FakeResolver(1, P, new AiModelMetadata(Name: "新名")));

        var md = ModelMetadataResolverChain.Resolve(P, "m5");

        Assert.NotNull(md);
        Assert.Equal("新名", md!.Name);
    }

    [Fact]
    [DisplayName("能力分级兜底：嵌入模型按能力位给非 Token 单价，标记 FromFallbackPricing")]
    public void Resolve_FallbackUnitPriceByCapability()
    {
        ModelMetadataResolverChain.Register(new FakeResolver(1, P, new AiModelMetadata(SupportEmbedding: true)));

        var md = ModelMetadataResolverChain.Resolve(P, "m6");

        Assert.NotNull(md);
        Assert.True(md!.SupportEmbedding);
        Assert.Equal(PricingMode.Embedding, md.PricingMode);
        Assert.Equal(0.5m, md.UnitPrice);
        Assert.Equal("百万Token", md.UnitName);
        Assert.True(md.FromFallbackPricing);
    }

    [Fact]
    [DisplayName("未知模型：能力位全空，仅由末位兜底给 Token 保守价")]
    public void Resolve_UnknownModelGetsFallbackTokenPrice()
    {
        var md = ModelMetadataResolverChain.Resolve("ChainTest-NoSuch", "m7");

        Assert.NotNull(md);
        Assert.NotNull(md!.Pricing);
        Assert.Equal(PricingMode.Token, md.PricingMode);
        Assert.True(md.FromFallbackPricing);
        // 兜底不兜底能力位
        Assert.Null(md.SupportThinking);
        Assert.Null(md.SupportFunction);
    }

    [Fact]
    [DisplayName("记录语义：纯价格块无能力但 HasAny；能力块 HasCapability；全空记录均为 false")]
    public void Record_HasAnyHasCapability()
    {
        var price = new AiModelMetadata(PricingMode: PricingMode.Token, Pricing: new AiModelPricing(1m, 2m, 0.1m, 0m));
        Assert.False(price.HasCapability);
        Assert.True(price.HasAny);

        var cap = new AiModelMetadata(SupportThinking: true);
        Assert.True(cap.HasCapability);
        Assert.True(cap.HasAny);

        var empty = new AiModelMetadata();
        Assert.False(empty.HasCapability);
        Assert.False(empty.HasAny);
    }

    [Fact]
    [DisplayName("默认解析器静态就位：首次访问即含描述符与分级兜底，且顺序为描述符在前")]
    public void DefaultResolvers_RegisteredOnFirstUse()
    {
        var list = ModelMetadataResolverChain.List;
        Assert.Contains(list, e => e is DescriptorModelMetadataResolver);
        Assert.Contains(list, e => e is FallbackModelMetadataResolver);

        var idxDesc = Array.FindIndex(list, e => e is DescriptorModelMetadataResolver);
        var idxFall = Array.FindIndex(list, e => e is FallbackModelMetadataResolver);
        Assert.True(idxDesc >= 0 && idxDesc < idxFall);
    }
}
