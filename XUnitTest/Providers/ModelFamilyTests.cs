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
    [InlineData("qwen3.8-max", true, true)]
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
    [DisplayName("Qwen3新版本_上下文自动覆盖_家族不含价格")]
    public void Qwen3NewVersion_ContextOnly()
    {
        // qwen3.8 系列为 1M 上下文（官方 2026-Q3），自动按 qwen3.8* 规则推断
        var caps = ModelFamilyRegistry.Match("qwen3.8-plus");
        Assert.NotNull(caps);
        Assert.Equal(1_048_576, caps!.ContextLength);
        // 家族只承载特性，不含价格（价格由服务商层与模型元数据表提供）
        Assert.Null(caps.Pricing);

        var max = ModelFamilyRegistry.Match("qwen3.8-max");
        Assert.NotNull(max);
        Assert.Equal(1_048_576, max!.ContextLength);
        Assert.Null(max.Pricing);
    }

    [Fact]
    [DisplayName("Qwen3.7系列_上下文1M_家族规则覆盖")]
    public void Qwen37_ContextLength_1M()
    {
        // qwen3.8/3.7-max/plus/flash 均命中对应规则 → 上下文 1M（与官方 1M 上下文一致）
        foreach (var modelId in new[] { "qwen3.8-max", "qwen3.8-plus", "qwen3.7-max", "qwen3.7-plus", "qwen3.7-flash" })
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
    [InlineData("deepseek-v4-flash-vision-exp", true, true, 1_048_576)]
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

    [Fact]
    [DisplayName("DeepSeek官方客户端_[AiClientModel]注册_思考能力")]
    public void DeepSeekOfficial_RegisteredModel_Thinking()
    {
        // 官方 [AiClientModel] 精确注册（描述符列表）是 StarChat 模型配置表 SupportThinking 的来源；
        // v4-pro 曾漏标 Thinking=true 导致模型被识别为不支持思考（v4-flash 有标，家族/推断层均正常）
        var descriptor = AiClientRegistry.Default.GetDescriptor("DeepSeek");
        Assert.NotNull(descriptor);
        var info = descriptor!.FindModelInfo("deepseek-v4-pro");
        Assert.NotNull(info);
        Assert.True(info!.Capabilities.SupportThinking);
        Assert.True(info.Capabilities.SupportFunction);
        Assert.Equal("high,max", info.Capabilities.ReasoningEfforts);
    }

    [Fact]
    [DisplayName("DeepSeek视觉模型_家族推断视觉能力")]
    public void DeepSeekVision_InfersVision()
    {
        // deepseek-v4-flash-vision-exp 应被家族规则推断为：思考 + 工具 + 视觉输入，且非文生图
        var caps = ModelFamilyRegistry.Match("deepseek-v4-flash-vision-exp");
        Assert.NotNull(caps);
        Assert.True(caps!.SupportVision);
        Assert.False(caps.SupportImage);
        Assert.True(caps.SupportThinking);
        Assert.True(caps.SupportFunction);
        Assert.Equal(1_048_576, caps.ContextLength);
    }
    #endregion

    #region DashScope 委托基类
    [Theory]
    [DisplayName("DashScope_委托基类_家族模型能力正确")]
    [InlineData("qwen3.8-max", true, true, true)]
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

    #region 新增 12 家族（hunyuan/glm/gpt/doubao/minimax/kimi/ernie/spark/grok/mistral/llama/gemma）
    [Fact]
    [DisplayName("注册表_新增12家族已注册")]
    public void Registry_HasNewFamilies()
    {
        foreach (var name in new[] { "hunyuan", "glm", "gpt", "doubao", "minimax", "kimi", "ernie", "spark", "grok", "mistral", "llama", "gemma" })
            Assert.NotNull(ModelFamilyRegistry.Find(name));
    }

    [Theory]
    [DisplayName("Hunyuan家族_能力推断")]
    [InlineData("hy3", true, true, false, 262_144)]
    [InlineData("hunyuan-t1", true, true, true, 262_144)]
    [InlineData("hunyuan-turbo", true, true, true, 131_072)]
    [InlineData("hunyuan-pro", false, true, true, 131_072)]
    [InlineData("hunyuan-lite", false, false, false, 32_768)]
    public void Hunyuan_InferCapabilities(String modelId, Boolean expectThink, Boolean expectFunc, Boolean expectVision, Int32 expectContext)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThink, caps!.SupportThinking);
        Assert.Equal(expectFunc, caps.SupportFunction);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectContext, caps.ContextLength);
    }

    [Fact]
    [DisplayName("Hunyuan_hy3_思考多档_家族不含价格")]
    public void Hunyuan_Hy3_EffortsOnly()
    {
        var caps = ModelFamilyRegistry.Match("hy3");
        Assert.NotNull(caps);
        Assert.Equal("no_think,think_low,think_high", caps!.ReasoningEfforts);
        Assert.Null(caps.Pricing);
    }

    [Theory]
    [DisplayName("GLM家族_能力推断")]
    [InlineData("glm-5.2", true, true, true, 262_144)]
    [InlineData("glm-4.7", true, true, true, 262_144)]
    [InlineData("glm-4.6", true, true, true, 262_144)]
    [InlineData("glm-4", true, true, true, 131_072)]
    [InlineData("glm-4v-plus", false, true, true, 8_192)]
    [InlineData("glm-4-flash", false, false, false, 131_072)]
    [InlineData("glm-4-alltools", false, true, false, 131_072)]
    public void Glm_InferCapabilities(String modelId, Boolean expectThink, Boolean expectFunc, Boolean expectVision, Int32 expectContext)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThink, caps!.SupportThinking);
        Assert.Equal(expectFunc, caps.SupportFunction);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectContext, caps.ContextLength);
    }

    [Fact]
    [DisplayName("GLM_cogview图像_家族不含价格_平台价走注册")]
    public void Glm_CogViewImage_NoFamilyPricing()
    {
        // cogview 文生图：不支持工具
        var cog = ModelFamilyRegistry.Match("cogview-3");
        Assert.NotNull(cog);
        Assert.True(cog!.SupportImage);
        Assert.False(cog.SupportFunction);
        // glm-5.x 家族只提供特性（思考+视觉），不含价格
        var g5 = ModelFamilyRegistry.Match("glm-5.2");
        Assert.NotNull(g5);
        Assert.True(g5!.SupportThinking);
        Assert.True(g5.SupportVision);
        Assert.Null(g5.Pricing);
        // DashScope 托管价 8/28 由 [AiClientModel] 精确注册承载（描述符层）
        var info = AiClientRegistry.Default.GetDescriptor("DashScope")!.FindModelInfo("glm-5.2");
        Assert.NotNull(info);
        Assert.Equal(8m, info!.Pricing!.InputPrice);
    }

    [Theory]
    [DisplayName("GPT家族_对话模型能力推断")]
    [InlineData("gpt-5.6", true, true, true, 922_000)]
    [InlineData("gpt-5.4-mini", true, true, true, 272_000)]
    [InlineData("gpt-5-mini", true, true, true, 400_000)]
    [InlineData("gpt-4.1", false, true, true, 1_048_576)]
    [InlineData("gpt-4o", false, true, true, 131_072)]
    [InlineData("gpt-4o-mini", false, true, true, 131_072)]
    [InlineData("gpt-4-turbo", false, true, false, 128_000)]
    [InlineData("gpt-4", false, true, false, 8_192)]
    [InlineData("gpt-3.5-turbo", false, true, false, 16_385)]
    [InlineData("o4-mini", true, true, true, 200_000)]
    [InlineData("o3-mini", true, true, false, 200_000)]
    public void Gpt_ChatCapabilities(String modelId, Boolean expectThink, Boolean expectFunc, Boolean expectVision, Int32 expectContext)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThink, caps!.SupportThinking);
        Assert.Equal(expectFunc, caps.SupportFunction);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectContext, caps.ContextLength);
    }

    [Theory]
    [DisplayName("GPT家族_媒体子模型能力不丢失")]
    [InlineData("gpt-4o-audio", false, false, false, true, true)]        // 实时音频：输入+输出
    [InlineData("gpt-4o-mini-tts", false, false, false, false, true)]    // 语音合成
    [InlineData("gpt-4o-transcribe", false, false, false, true, false)]  // 语音识别
    [InlineData("gpt-image-1", false, false, false, false, false)]       // 文生图（SupportImage 单独断言）
    public void Gpt_MediaModels_Capabilities(String modelId, Boolean expectThink, Boolean expectFunc, Boolean expectVision, Boolean expectAudio, Boolean expectSpeech)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThink, caps!.SupportThinking);
        Assert.Equal(expectFunc, caps.SupportFunction);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectAudio, caps.SupportAudio);
        Assert.Equal(expectSpeech, caps.SupportSpeech);
        // 文生图：仅 gpt-image* 为 true
        Assert.Equal(modelId.StartsWith("gpt-image", StringComparison.OrdinalIgnoreCase), caps.SupportImage);
    }

    [Fact]
    [DisplayName("GPT家族_推理强度_价格走服务商层探测")]
    public void Gpt_ReasoningEffortsAndPricing()
    {
        // 家族只提供特性（含推理强度），不含价格
        var g56 = ModelFamilyRegistry.Match("gpt-5.6");
        Assert.NotNull(g56);
        Assert.Equal("low,medium,high", g56!.ReasoningEfforts);
        Assert.Null(g56.Pricing);

        // 服务商层（OpenAI 兼容基类）在家族特性上补充通用价格探测
        var client = new OpenAIClientBase(new AiClientOptions { Endpoint = "https://example.com/v1" });
        var o3 = client.InferModelCapabilities("o3-mini");
        Assert.NotNull(o3);
        Assert.True(o3!.SupportThinking);
        Assert.False(o3.SupportVision);
        Assert.Equal(200_000, o3.ContextLength);
        Assert.Equal("low,medium,high", o3.ReasoningEfforts);
        Assert.Equal(7.59m, o3.Pricing!.InputPrice);
        Assert.Equal(30.36m, o3.Pricing!.OutputPrice);
        // gpt-4o-audio 音频能力不因家族化而丢失，价格由服务商层探测（gpt-4o 档）
        var audio = client.InferModelCapabilities("gpt-4o-audio");
        Assert.NotNull(audio);
        Assert.True(audio!.SupportAudio);
        Assert.True(audio.SupportSpeech);
        Assert.False(audio.SupportFunction);
        Assert.Equal(17.25m, audio.Pricing!.InputPrice);
        // 非 gpt 前缀媒体模型仍走基类通用兜底（无探测价则为 null）
        var sora = client.InferModelCapabilities("sora-2");
        Assert.NotNull(sora);
        Assert.True(sora!.SupportVideo);
        Assert.False(sora.SupportFunction);
        Assert.Null(sora.Pricing);
    }

    [Theory]
    [DisplayName("Doubao家族_能力推断")]
    [InlineData("doubao-seed-1.6", true, true, true, 262_144)]
    [InlineData("doubao-seed-1.6-thinking", true, true, true, 262_144)]
    [InlineData("doubao-1.5-pro-32k", true, true, true, 32_768)]
    [InlineData("doubao-1.5-vision-pro-32k", true, true, true, 32_768)]
    [InlineData("doubao-1.5-lite-32k", false, false, false, 32_768)]
    public void Doubao_InferCapabilities(String modelId, Boolean expectThink, Boolean expectFunc, Boolean expectVision, Int32 expectContext)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThink, caps!.SupportThinking);
        Assert.Equal(expectFunc, caps.SupportFunction);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectContext, caps.ContextLength);
    }

    [Theory]
    [DisplayName("MiniMax家族_能力推断_含斜杠ID")]
    [InlineData("MiniMax-M2.5", true, true, false, 196_608)]
    [InlineData("MiniMax-M3", true, true, true, 196_608)]
    [InlineData("MiniMax/MiniMax-M3", true, true, true, 196_608)]   // DashScope 托管斜杠前缀
    [InlineData("MiniMax-Text-01", false, true, false, 1_048_576)]
    [InlineData("abab6.5s-chat", false, true, false, 32_768)]
    public void MiniMax_InferCapabilities(String modelId, Boolean expectThink, Boolean expectFunc, Boolean expectVision, Int32 expectContext)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThink, caps!.SupportThinking);
        Assert.Equal(expectFunc, caps.SupportFunction);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectContext, caps.ContextLength);
    }

    [Fact]
    [DisplayName("MiniMax_斜杠ID_glob多备选匹配")]
    public void MiniMax_SlashId_GlobMatch()
    {
        Assert.True(ModelFamily.GlobMatch("minimax*/minimax-m3*", "MiniMax/MiniMax-M3"));
        Assert.False(ModelFamily.GlobMatch("minimax-m3*", "MiniMax/MiniMax-M3"));
    }

    [Theory]
    [DisplayName("Kimi家族_能力推断")]
    [InlineData("kimi-k3", true, true, true, 262_144)]
    [InlineData("kimi-k2.6", true, true, false, 262_144)]
    [InlineData("kimi-k2-0905", true, true, false, 262_144)]
    [InlineData("kimi-k1.5", true, true, false, 131_072)]
    [InlineData("moonshot-v1-8k", false, true, false, 8_192)]
    [InlineData("moonshot-v1-32k", false, true, false, 32_768)]
    [InlineData("moonshot-v1-128k", false, true, false, 131_072)]
    public void Kimi_InferCapabilities(String modelId, Boolean expectThink, Boolean expectFunc, Boolean expectVision, Int32 expectContext)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThink, caps!.SupportThinking);
        Assert.Equal(expectFunc, caps.SupportFunction);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectContext, caps.ContextLength);
    }

    [Theory]
    [DisplayName("文心/星火/Grok家族_能力推断")]
    [InlineData("ernie-4.5-turbo", true, true, true, 8_192)]
    [InlineData("ernie-speed", false, false, false, 8_192)]
    [InlineData("spark-4.0-ultra", true, true, false, 131_072)]
    [InlineData("spark-3.5-max", false, false, false, 131_072)]
    [InlineData("grok-3", true, true, true, 131_072)]
    [InlineData("grok-3-mini", true, true, false, 131_072)]
    [InlineData("grok-2-vision", false, true, true, 32_768)]
    public void ErnieSparkGrok_InferCapabilities(String modelId, Boolean expectThink, Boolean expectFunc, Boolean expectVision, Int32 expectContext)
    {
        var caps = ModelFamilyRegistry.Match(modelId);
        Assert.NotNull(caps);
        Assert.Equal(expectThink, caps!.SupportThinking);
        Assert.Equal(expectFunc, caps.SupportFunction);
        Assert.Equal(expectVision, caps.SupportVision);
        Assert.Equal(expectContext, caps.ContextLength);
    }

    [Fact]
    [DisplayName("精简家族_默认能力_不误伤")]
    public void LeanFamilies_DefaultCapabilities()
    {
        // llama/gemma/mistral 默认支持工具、不思考
        foreach (var model in new[] { "llama-3.3-70b-versatile", "gemma2-9b-it", "mistral-large-latest" })
        {
            var caps = ModelFamilyRegistry.Match(model);
            Assert.NotNull(caps);
            Assert.False(caps!.SupportThinking);
            Assert.True(caps.SupportFunction);
        }
        Assert.Equal(131_072, ModelFamilyRegistry.Match("mistral-large-latest")!.ContextLength);
        Assert.Equal(131_072, ModelFamilyRegistry.Match("llama-3.3-70b-versatile")!.ContextLength);
        // mistral-embed 嵌入模型：不支持工具
        var embed = ModelFamilyRegistry.Match("mistral-embed");
        Assert.NotNull(embed);
        Assert.True(embed!.SupportEmbedding);
        Assert.False(embed.SupportFunction);
        // 锚定：非家族前缀不误伤
        Assert.Null(ModelFamilyRegistry.Match("text-embedding-3-large"));
        Assert.Null(ModelFamilyRegistry.Match("claude-sonnet-4"));
    }
    #endregion

    #region 价格分层与跨平台（家族特性 / 服务商探测 / 平台注册）
    [Fact]
    [DisplayName("价格分层_家族无价_服务商探测与平台注册")]
    public void PricingLayering_FamilyNoPricing_ProbeVsProvider()
    {
        // 家族：只提供特性，不含价格
        var family = ModelFamilyRegistry.Match("deepseek-v4-pro");
        Assert.NotNull(family);
        Assert.True(family!.SupportThinking);
        Assert.Null(family.Pricing);

        // 服务商层通用探测：OpenAI 兼容基类按命名规律给 DeepSeek 系列典型价 3/6
        var client = new OpenAIClientBase(new AiClientOptions { Endpoint = "https://example.com/v1" });
        var probe = client.InferModelCapabilities("deepseek-v4-pro");
        Assert.NotNull(probe);
        Assert.Equal(3m, probe!.Pricing!.InputPrice);

        // 平台专属注册：DashScope [AiClientModel] 精确价 12/24（优先于服务商探测）
        var dashScope = AiClientRegistry.Default.GetDescriptor("DashScope");
        Assert.NotNull(dashScope);
        var info = dashScope!.FindModelInfo("deepseek-v4-pro");
        Assert.NotNull(info);
        Assert.Equal(12m, info!.Pricing!.InputPrice);
    }

    [Fact]
    [DisplayName("DashScope_第三方家族模型_预检删除后家族接管")]
    public void DashScope_ThirdPartyFamilies_DelegatedToFamily()
    {
        var client = new DashScopeChatClient(new AiClientOptions { Endpoint = "https://dashscope.aliyuncs.com" });
        // glm-5.3 未在 [AiClientModel]，由 glm 家族推断
        var glm = client.InferModelCapabilities("glm-5.3");
        Assert.NotNull(glm);
        Assert.True(glm!.SupportThinking);
        Assert.True(glm.SupportVision);
        Assert.Equal(262_144, glm.ContextLength);
        // kimi-k2.6 由 kimi 家族推断
        var kimi = client.InferModelCapabilities("kimi-k2.6");
        Assert.NotNull(kimi);
        Assert.True(kimi!.SupportThinking);
        Assert.Equal(262_144, kimi.ContextLength);
        // MiniMax/MiniMax-M3（斜杠 ID）由 minimax 家族推断
        var mini = client.InferModelCapabilities("MiniMax/MiniMax-M3");
        Assert.NotNull(mini);
        Assert.True(mini!.SupportThinking);
        Assert.True(mini.SupportVision);
        Assert.Equal(196_608, mini.ContextLength);
        // hunyuan/doubao 等经 OpenAI 兼容平台基类推断
        var hy = new OpenAIClientBase(new AiClientOptions { Endpoint = "https://example.com/v1" }).InferModelCapabilities("hunyuan-t1");
        Assert.NotNull(hy);
        Assert.True(hy!.SupportThinking);
        Assert.True(hy.SupportVision);
        var db = ModelFamilyRegistry.Match("doubao-seed-1.6");
        Assert.NotNull(db);
        Assert.True(db!.SupportThinking);
    }
    #endregion
}
