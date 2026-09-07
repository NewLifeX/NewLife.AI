using NewLife.AI.Models;

namespace NewLife.AI.Clients;

/// <summary>能力分级兜底解析器。作为解析链末位，按已合成能力位（<see cref="IModelMetadataResolver.Resolve"/> 的
/// current 参数）为价格缺失的模型给出分级兜底价：非 Token 模型给单价，Token 对话模型给四档保守价。
/// 仅提供价格，不兜底能力（保持"未知模型能力位不自动覆盖"的行为）；返回块标记
/// <see cref="AiModelMetadata.FromFallbackPricing"/>，写库方据此保留管理员已设置的价格</summary>
public class FallbackModelMetadataResolver : IModelMetadataResolver
{
    /// <summary>优先级：能力分级兜底（最低）</summary>
    public Int32 Order => 9999;

    /// <summary>按能力分级给出兜底价格块。能力未知时按 Token 保守中档处理</summary>
    /// <param name="current">当前已合成结果（更高优先级贡献），读取其能力位用于分级</param>
    /// <param name="provider">提供商编码，可为空</param>
    /// <param name="model">模型编码</param>
    /// <returns>价格块（仅价格字段非空），模型编码为空时返回 null</returns>
    public AiModelMetadata? Resolve(AiModelMetadata? current, String? provider, String? model)
    {
        if (model.IsNullOrEmpty()) return null;

        var c = current;
        var thinking = c?.SupportThinking == true;
        var function = c?.SupportFunction == true;
        var vision = c?.SupportVision == true;
        var speech = c?.SupportSpeech == true;
        var image = c?.SupportImage == true;
        var video = c?.SupportVideo == true;
        var embedding = c?.SupportEmbedding == true;
        var rerank = c?.SupportRerank == true;

        // 非 Token 模型：单价 + 计费模式（判定顺序与原有能力分级兜底一致）
        if (embedding && !thinking && !function)
            return Unit(PricingMode.Embedding, 0.5m, "百万Token");
        if (rerank && !thinking)
            return Unit(PricingMode.Embedding, 1m, "百万Token");
        if (speech && !thinking && !function && !vision)
            return Unit(PricingMode.Speech, 0.2m, "千字符");
        if (image && !thinking && !vision)
            return Unit(PricingMode.Image, 0.2m, "张");
        if (video)
            return Unit(PricingMode.Video, 0.6m, "秒");

        // Token 对话模型：按能力分级给四档保守价
        Decimal input, output, cached, creation;
        if (thinking && vision) { input = 3m; output = 18m; cached = 0.3m; creation = 0m; }      // 多模态思考旗舰
        else if (thinking) { input = 2m; output = 12m; cached = 0.2m; creation = 0m; }          // 纯文本思考
        else if (vision) { input = 2.5m; output = 15m; cached = 0.25m; creation = 0m; }         // 视觉对话
        else if (function) { input = 1m; output = 4m; cached = 0.1m; creation = 0m; }           // 基础对话+工具
        else { input = 2m; output = 12m; cached = 0.2m; creation = 0m; }                        // 兜底（保守中档）

        return new AiModelMetadata(PricingMode: PricingMode.Token, Pricing: new AiModelPricing(input, output, cached, creation), FromFallbackPricing: true);
    }

    /// <summary>构造非 Token 单价块</summary>
    /// <param name="mode">计费模式</param>
    /// <param name="price">单价（元/单位）</param>
    /// <param name="unit">单位标签</param>
    /// <returns>仅含价格字段的元数据块</returns>
    private static AiModelMetadata Unit(PricingMode mode, Decimal price, String unit)
        => new(PricingMode: mode, UnitPrice: price, UnitName: unit, FromFallbackPricing: true);
}
