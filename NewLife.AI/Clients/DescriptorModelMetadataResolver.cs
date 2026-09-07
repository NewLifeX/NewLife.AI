using NewLife.AI.Models;

namespace NewLife.AI.Clients;

/// <summary>描述符模型元数据解析器。能力与显示名来自 [AiClientModel] 精确注册/全局模型家族/命名推断，
/// 价格优先取 [AiClientModel].Pricing，未注册时取推断价（含服务商层通用价格探测）。
/// NewLife.AI 默认解析器之一（Order=1000）；上层（如 StarChat ModelData 元数据表）注册 Order 更小的解析器，
/// 其精确声明将优先于本解析器产出</summary>
/// <remarks>
/// 能力位来自 <see cref="AiClientDescriptor.FindModelCapabilities"/>（精确+前缀）或
/// <see cref="AiClientBase.InferModelCapabilities"/>（家族/启发式）；非对话模型（嵌入/重排/语音/图像/视频）
/// 按能力切到对应计费模式并以推断价承载单价，对话模型按 Token 四档输出。
/// </remarks>
public class DescriptorModelMetadataResolver : IModelMetadataResolver
{
    /// <summary>优先级：描述符推断（高于能力分级兜底）</summary>
    public Int32 Order => 1000;

    /// <summary>解析模型能力/价格/显示名。provider 可为空，按模型编码全局匹配描述符</summary>
    /// <param name="current">当前已合成结果（本解析器不依赖）</param>
    /// <param name="provider">提供商编码（Code 优先，Provider 兜底），可为空</param>
    /// <param name="model">模型编码</param>
    /// <returns>元数据块；描述符未识别时返回 null</returns>
    public AiModelMetadata? Resolve(AiModelMetadata? current, String? provider, String? model)
    {
        if (model.IsNullOrEmpty()) return null;

        var reg = AiClientRegistry.Default;

        // 先按提供商解析描述符；提供商无法解析时扫描全部描述符按模型编码匹配（模型编码全局唯一）
        var descriptor = provider.IsNullOrEmpty() ? null : reg.GetDescriptor(provider);
        descriptor ??= reg.Descriptors.Values.FirstOrDefault(d => d.FindModelInfo(model) != null);
        if (descriptor == null) return null;

        // [AiClientModel] 精确注册优先：完整信息（显示名/能力/价格）
        var mi = descriptor.FindModelInfo(model);
        var caps = mi?.Capabilities ?? descriptor.FindModelCapabilities(model);

        // 未注册模型：按命名规律推断（家族/通用启发式，含服务商层价格探测）
        if (caps == null)
        {
            using var client = descriptor.Factory(new AiClientOptions { Endpoint = "" });
            caps = (client as AiClientBase)?.InferModelCapabilities(model);
        }
        if (caps == null) return null;

        // 能力位与上下文（cap 恒有值；Context 0 视为未知不覆盖）
        var md = new AiModelMetadata(
            Name: mi?.DisplayName,
            SupportThinking: caps.SupportThinking,
            SupportFunction: caps.SupportFunction,
            SupportVision: caps.SupportVision,
            SupportAudio: caps.SupportAudio,
            SupportSpeech: caps.SupportSpeech,
            SupportImage: caps.SupportImage,
            SupportVideo: caps.SupportVideo,
            SupportEmbedding: caps.SupportEmbedding,
            SupportRerank: caps.SupportRerank,
            ContextLength: caps.ContextLength > 0 ? caps.ContextLength : null,
            ReasoningEfforts: caps.ReasoningEfforts);

        // 价格：精确注册价优先，否则推断价（含服务商层探测）
        var pricing = mi?.Pricing ?? caps.Pricing;
        if (pricing == null) return md;

        // 非对话模型切非 Token 计费：单价=推断价.InputPrice（与 InferNonChatCapabilities 语义一致）
        var unit = ResolveUnit(caps, pricing, out var mode, out var unitName);
        if (mode != null)
            return md with { PricingMode = mode, UnitPrice = unit, UnitName = unitName };
        // 对话/嵌入按 Token：四档价
        return md with { Pricing = pricing };
    }

    /// <summary>按能力判定非 Token 计费模式与单价；对话模型返回 mode=null（按 Token）</summary>
    /// <param name="caps">能力</param>
    /// <param name="pricing">推断/注册价格</param>
    /// <param name="mode">命中的非 Token 计费模式，null=Token</param>
    /// <param name="unitName">单价单位标签</param>
    /// <returns>单价，非 Token 模式为推断价.InputPrice</returns>
    private static Decimal? ResolveUnit(AiProviderCapabilities caps, AiModelPricing pricing, out PricingMode? mode, out String? unitName)
    {
        mode = null;
        unitName = null;

        // 判定顺序与能力分级兜底一致（见 FallbackModelMetadataResolver）
        if (caps.SupportEmbedding && !caps.SupportThinking && !caps.SupportFunction)
        {
            mode = PricingMode.Embedding; unitName = "百万Token";
        }
        else if (caps.SupportRerank && !caps.SupportThinking)
        {
            mode = PricingMode.Embedding; unitName = "百万Token";
        }
        else if (caps.SupportSpeech && !caps.SupportThinking && !caps.SupportFunction && !caps.SupportVision)
        {
            mode = PricingMode.Speech; unitName = "千字符";
        }
        else if (caps.SupportImage && !caps.SupportThinking && !caps.SupportVision)
        {
            mode = PricingMode.Image; unitName = "张";
        }
        else if (caps.SupportVideo)
        {
            mode = PricingMode.Video; unitName = "秒";
        }

        return mode == null ? null : pricing.InputPrice;
    }
}
