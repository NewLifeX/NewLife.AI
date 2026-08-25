using System;
using System.ComponentModel;
using NewLife.AI.Clients;
using NewLife.AI.Clients.DashScope;
using NewLife.AI.Clients.OpenAI;
using Xunit;

namespace XUnitTest.Providers;

/// <summary>模型家族规则引擎单元测试。验证版本通配兼容与跨平台能力共享</summary>
/// <remarks>
/// 家族档案（ModelFamilies）将 qwen/deepseek 等命名规律抽为通配规则，供所有服务商共享：
/// <list type="bullet">
/// <item>版本通配：qwen3.8/3.9 等新版本无需改代码自动获得正确能力</item>
/// <item>跨平台共享：deepseek 在 DashScope/腾讯/火山等任何 OpenAI 兼容平台能力一致</item>
/// </list>
/// </remarks>
public class ModelFamilyTests
{
    #region glob 匹配
    [Theory]
    [DisplayName("GlobMatch_通配符_正确匹配")]
    [InlineData("qwen3*", "qwen3-max", true)]
    [InlineData("qwen3*", "qwen3.6-plus", true)]
    [InlineData("qwen3*", "qwen3-coder-plus", true)]
    [InlineData("qwen3*", "qwen2.5-72b", false)]
    [InlineData("qwen3.*-plus", "qwen3.6-plus", true)]
    // 全锚定：不带 * 后缀时不匹配带日期后缀的变体（家族规则用 * 后缀容忍）
    [InlineData("qwen3.*-plus", "qwen3.6-plus-2026-04-02", false)]
    [InlineData("qwen3.*-plus*", "qwen3.6-plus-2026-04-02", true)]
    // 点号要求：避免把 qwen3-coder-plus 误判为多模态
    [InlineData("qwen3.*-plus", "qwen3-coder-plus", false)]
    [InlineData("qwen3.*-plus", "qwen-plus", false)]
    // 稳定版别名含日期变体
    [InlineData("qwen-max*", "qwen-max-2026-01-23", true)]
    [InlineData("qwen-max*", "qwen-vl-max", false)]
    // | 多备选
    [InlineData("qwen-tts*|qwen3-tts*", "qwen3-tts-flash", true)]
    [InlineData("qwen-tts*|qwen3-tts*", "qwen-image-plus", false)]
    [InlineData("deepseek-v4-*", "deepseek-v4-pro", true)]
    [InlineData("deepseek-v4-*", "deepseek-r1", false)]
    public void GlobMatch_Wildcard_Matches(String pattern, String modelId, Boolean expected)
    {
        Assert.Equal(expected, ModelFamily.GlobMatch(pattern, modelId));
    }
    #endregion

    #region 版本通配（新版本自动覆盖）
    [Theory]
    [DisplayName("Qwen3新版本_通配自动覆盖_能力正确")]
    [InlineData("qwen3.8-max", true, false)]
    [InlineData("qwen3.8-plus", true, true)]
    [InlineData("qwen3.8-flash", true, true)]
    [InlineData("qwen3.8-turbo", true, true)]
    [InlineData("qwen3.9-max", true, false)]
    [InlineData("qwen3.9-plus", true, true)]
    [InlineData("qwen3.5-max", true, false)]
    [InlineData("qwen3.5-plus", true, true)]
    [InlineData("qwen3.7-max", true, false)]
    [InlineData("qwen3.7-plus", true, true)]
    public void Qwen3NewVersion_AutoCovered(String modelId, Boolean expectThinking, Boolean expectVision)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThinking, caps!.SupportThinking);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.True(caps.SupportFunction);
    }

    [Fact]
    [DisplayName("Qwen3新版本_上下文与价格_自动覆盖")]
    public void Qwen3NewVersion_ContextAndPricing()
    {
        // qwen3.8 未在硬编码列表中，应自动按 qwen3* 系列推断上下文 131K
        var caps = ModelFamilyRegistry.Match("qwen3.8-plus");
        Assert.NotNull(caps);
        Assert.Equal(131_072, caps!.ContextLength);
        Assert.NotNull(caps.Pricing);
        Assert.Equal(0.7m, caps.Pricing!.InputPrice);
    }

    [Fact]
    [DisplayName("Qwen3.7系列_上下文1M_家族规则覆盖")]
    public void Qwen37_ContextLength_1M()
    {
        // qwen3.7-max/plus/flash 均命中 qwen3.7* → 上下文 1M（与元数据表 dashscope.json 的 Context=1048576 一致）
        foreach (var modelId in new[] { "qwen3.7-max", "qwen3.7-plus", "qwen3.7-flash" })
        {
            var caps = ModelFamilyRegistry.Match(modelId);
            Assert.NotNull(caps);
            Assert.Equal(1_048_576, caps!.ContextLength);
        }

        // 分档验证：qwen3.6-max-preview 命中更具体规则 → 262K（后规则覆盖先规则）
        var preview = ModelFamilyRegistry.Match("qwen3.6-max-preview");
        Assert.NotNull(preview);
        Assert.Equal(262_144, preview!.ContextLength);
    }
    #endregion

    #region 跨平台 deepseek（腾讯/火山等通用 OpenAI 兼容平台）
    [Theory]
    [DisplayName("跨平台DeepSeek_通用基类推断_能力一致")]
    [InlineData("deepseek-v4-pro", true, true, 1_048_576)]
    [InlineData("deepseek-v4-flash", true, true, 1_048_576)]
    [InlineData("deepseek-v4-flash-2026-06-01", true, true, 1_048_576)]
    [InlineData("deepseek-reasoner", true, false, 1_048_576)]
    [InlineData("deepseek-chat", false, true, 1_048_576)]
    // 修复：deepseek-r1 旧代码推断为不思考，家族规则修正为始终思考
    [InlineData("deepseek-r1", true, false, 65_536)]
    public void CrossPlatformDeepSeek_BaseInference(String modelId, Boolean expectThinking, Boolean expectFunction, Int32 expectContext)
    {
        // 用基类模拟腾讯/火山等未定制推断逻辑的 OpenAI 兼容平台
        var client = new OpenAIClientBase(new AiClientOptions { Endpoint = "https://example.com/v1" });
        var caps = client.InferModelCapabilities(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThinking, caps!.SupportThinking);
        Assert.Equal(expectFunction, caps.SupportFunction);
        Assert.Equal(expectContext, caps.ContextLength);
    }

    [Fact]
    [DisplayName("跨平台DeepSeek_官方客户端_家族推断")]
    public void CrossPlatformDeepSeek_OfficialClient()
    {
        // 官方 DeepSeek 客户端删除自实现推断后，由基类家族规则接管
        var client = new DeepSeekChatClient(new AiClientOptions { Endpoint = "https://api.deepseek.com" });
        var caps = client.InferModelCapabilities("deepseek-v4-pro");
        Assert.NotNull(caps);
        Assert.True(caps!.SupportThinking);
        Assert.Equal(1_048_576, caps.ContextLength);
        Assert.Equal("high,max", caps.ReasoningEfforts);
    }
    #endregion

    #region DashScope 委托基类
    [Theory]
    [DisplayName("DashScope_委托基类_家族模型能力正确")]
    [InlineData("qwen3.8-max", true, false, true)]
    [InlineData("qwen3.8-plus", true, true, true)]
    [InlineData("deepseek-v4-pro", true, false, true)]
    [InlineData("qwen-omni-turbo", false, true, false)]
    public void DashScope_DelegatesToFamily(String modelId, Boolean expectThinking, Boolean expectVision, Boolean expectFunction)
    {
        var client = new DashScopeChatClient(new AiClientOptions { Endpoint = "https://dashscope.aliyuncs.com" });
        var caps = client.InferModelCapabilities(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThinking, caps!.SupportThinking);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectFunction, caps.SupportFunction);
    }

    [Fact]
    [DisplayName("DashScope_媒体家族_语音合成")]
    public void DashScope_MediaFamily_Speech()
    {
        var client = new DashScopeChatClient(new AiClientOptions { Endpoint = "https://dashscope.aliyuncs.com" });
        var caps = client.InferModelCapabilities("qwen3-tts-flash");
        Assert.NotNull(caps);
        Assert.True(caps!.SupportSpeech);
        Assert.False(caps.SupportThinking);
    }
    #endregion

    #region 规则覆盖语义
    [Theory]
    [DisplayName("规则覆盖_后规则覆盖先规则")]
    [InlineData("qwen3-coder-plus", false, false)]      // qwen3* 思考被 coder 排除，且点号规则避免误判视觉
    [InlineData("qwen3-235b-a22b-instruct-2507", false, false)]
    [InlineData("qwen3-235b-a22b-thinking-2507", true, false)]
    [InlineData("qwen2.5-72b-instruct", false, false)]
    [InlineData("qwen-long", false, false)]
    [InlineData("qwen-vl-max", false, true)]            // vl 视觉不受 qwen3.*-max 规则影响
    public void RuleOverride_LaterWins(String modelId, Boolean expectThinking, Boolean expectVision)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThinking, caps!.SupportThinking);
        Assert.Equal(expectVision, caps.SupportVision);
    }

    [Fact]
    [DisplayName("未知模型_家族未命中_返回null")]
    public void UnknownModel_ReturnsNull()
    {
        Assert.Null(ModelFamilyRegistry.Match("totally-unknown-model-xyz"));
        Assert.Null(ModelFamilyRegistry.Match(null));
    }
    #endregion

    #region 注册表
    [Fact]
    [DisplayName("注册表_内置家族已注册")]
    public void Registry_HasBuiltinFamilies()
    {
        Assert.NotNull(ModelFamilyRegistry.Find("qwen"));
        Assert.NotNull(ModelFamilyRegistry.Find("deepseek"));
        Assert.NotNull(ModelFamilyRegistry.Find("qwen-media"));
        Assert.NotNull(ModelFamilyRegistry.Find("qwq"));
        Assert.NotNull(ModelFamilyRegistry.Find("qvq"));
        Assert.Null(ModelFamilyRegistry.Find("nope"));
    }

    [Fact]
    [DisplayName("注册表_同名家族后注册原位替换")]
    public void Registry_ReRegister_OverridesInPlace()
    {
        var original = ModelFamilyRegistry.Find("deepseek");
        Assert.NotNull(original);

        var family = new ModelFamily("deepseek", "deepseek*")
        {
            Rules = [new ModelCapabilityRule { Pattern = "deepseek*", Thinking = false }],
        };
        ModelFamilyRegistry.Register(family);

        // 同名家族替换后按新规则推断
        var caps = ModelFamilyRegistry.Match("deepseek-v4-pro");
        Assert.NotNull(caps);
        Assert.False(caps!.SupportThinking);

        // 恢复内置家族原实例，避免影响其他测试
        ModelFamilyRegistry.Register(original!);
    }
    #endregion
}
